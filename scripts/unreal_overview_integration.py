"""Signed-engine acceptance: saved overview, requested views, host media gate and hostile project states."""

import argparse
import os
import shutil
import sys
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import atomic_write_result, load_bounded_json  # noqa: E402

from package_builder_unreal.import_plan import file_identity  # noqa: E402
from package_builder_unreal.project import UnrealProjectClone, remove_owned_tree  # noqa: E402
from package_builder_unreal.project_validation import log_findings, repair_diagnostics  # noqa: E402
from unreal_candidate_process import candidate_editor, commandlet, repair_redirectors  # noqa: E402


def main(editor, timeout):
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    if editor != candidate_editor(profile):
        raise ValueError("Unexpected candidate editor.")
    run_id = uuid.uuid4().hex
    job, evidence = REPO / "artifacts/ue" / run_id, REPO / "artifacts/PB-1110" / run_id
    source = job / "input"
    source.mkdir(parents=True)
    (job / "temp").mkdir()
    evidence.mkdir(parents=True)
    receipt = {
        "passed": False,
        "realEngineRun": False,
        "cleanupSucceeded": False,
        "operations": [],
        "editorSha256": file_identity(editor)[0],
    }
    try:
        with UnrealProjectClone(
            REPO, REPO / profile["template"], job, "PBOverviewFixture"
        ) as clone:
            environment = dict(
                os.environ,
                PYTHONDONTWRITEBYTECODE="1",
                TEMP=str(job / "temp"),
                TMP=str(job / "temp"),
                PB_UNREAL_OVERVIEW_FIXTURE="1",
                PB_UNREAL_OVERVIEW_INPUT=str(source),
            )
            if clone.execute(
                REPO / "tools/blender/5.0.0/blender.exe",
                [
                    "--background",
                    "--factory-startup",
                    "--python",
                    str(REPO / "scripts/unreal_surface_fixture.py"),
                    "--",
                    str(source),
                ],
                environment,
                timeout,
            ):
                raise RuntimeError("Fixture geometry failed.")

            def dotnet_test(name):
                if clone.execute(
                    REPO / "tools/dotnet/10.0.302/dotnet.exe",
                    [
                        "test",
                        str(REPO / "tests/PackageBuilder.App.Wpf.Tests"),
                        "-c",
                        "Release",
                        "--no-build",
                        "--no-restore",
                        "--filter",
                        "FullyQualifiedName~" + name,
                    ],
                    environment,
                    timeout,
                ):
                    raise RuntimeError("Host fixture/media validation failed: " + name)

            dotnet_test("ExportLiveOverviewFixtureWhenRequested")
            (source / "product.json").write_text("{}", encoding="utf-8")
            inputs = {p.name: file_identity(p) for p in source.iterdir()}

            def inventory():
                return {
                    p.relative_to(clone.project).as_posix(): file_identity(p)
                    for p in (clone.project / "Content").rglob("*")
                    if p.is_file()
                }

            def operation(name, key=None, expected=None, render=False):
                key = key or name
                request = {
                    "protocolVersion": 1,
                    "jobId": "Job-" + run_id,
                    "operation": name,
                    "productManifestReference": "input/product.json",
                    "inputDirectoryReference": "input",
                    "outputDirectoryReference": "output",
                    "resultFileReference": "output/result.json",
                    "engineVersion": profile["version"],
                    "target": "unreal",
                }
                atomic_write_result(job / "request.json", request)
                arguments, env = commandlet(clone, REPO, evidence, key, render=render)
                receipt["realEngineRun"] = True
                code = clone.execute(editor, arguments, env, timeout)
                result = load_bounded_json(job / "output/result.json")
                atomic_write_result(evidence / (key + ".json"), result)
                if expected is None:
                    if (
                        code
                        or result["status"] != "success"
                        or result["findings"]
                        or log_findings((evidence / (key + ".log")).read_text("utf-8"))
                    ):
                        atomic_write_result(evidence / (key + "-inventory.json"), inventory())
                        raise RuntimeError("Native operation failed: " + key)
                    for item in result["artifacts"]:
                        if file_identity(job / item["logicalReference"]) != (
                            item["sha256"],
                            item["byteCount"],
                        ):
                            raise AssertionError("Native artifact hash mismatch.")
                elif (
                    not code
                    or result["status"] != "failure"
                    or result["findings"][0]["code"] != expected
                    or result["artifacts"]
                ):
                    raise AssertionError("Expected release blocker missing: " + key)
                if result["outputsPromoted"]:
                    raise AssertionError("Unexpected promotion.")
                receipt["operations"].append(key)

            def fault(mode):
                arguments, env = commandlet(clone, REPO, evidence, "fault-" + mode)
                arguments[2] = "-script=" + str(REPO / "scripts/unreal_overview_fault.py")
                env["PB_OVERVIEW_FAULT"] = mode
                if clone.execute(editor, arguments, env, timeout):
                    raise RuntimeError("Fault injection failed: " + mode)

            operation("import-unreal-textures")
            operation("import-unreal-surfaces")
            operation("create-unreal-overview")
            saved = inventory()
            operation("validate-unreal-overview")
            if inventory() != saved:
                raise AssertionError("Validation changed assets.")
            operation("render-unreal-previews", render=True)
            if inventory() != saved:
                raise AssertionError("Rendering changed saved assets.")
            environment["PB_UNREAL_OVERVIEW_MEDIA"] = str(job / "output/previews")
            try:
                dotnet_test("ValidateLiveOverviewGalleryWhenRequested")
            finally:
                shutil.copytree(job / "output/previews", evidence / "previews")
            fault("unused")
            operation("validate-unreal-overview", "reject-unused", "UNREAL_ASSET_INVENTORY")
            fault("redirector")
            operation("validate-unreal-overview", "reject-redirector", "UNREAL_REDIRECTOR_REMAINS")
            if repair_redirectors(clone, REPO, evidence, editor, timeout):
                raise RuntimeError("Project-only redirector fix failed.")
            repair_logs = [
                repair_diagnostics(
                    (evidence / name).read_text("utf-8"), editor.parent, clone.project / "Content"
                )
                for name in ("fix-redirectors.log", "fix-redirectors-final.log")
            ]
            atomic_write_result(evidence / "repair-diagnostics.json", {"passes": repair_logs})
            if any(item["findings"] for item in repair_logs):
                raise RuntimeError("Redirector repair emitted blocking diagnostics.")
            operation(
                "validate-unreal-overview",
                "redirector-fixed-unused-remains",
                "UNREAL_ASSET_INVENTORY",
            )
            fault("cleanup")
            operation("validate-unreal-overview", "clean-after-fixup")
            fault("bad-light")
            operation("validate-unreal-overview", "reject-bad-light", "UNREAL_LIGHTING_STATE")
            fault("restore-light")
            operation("validate-unreal-overview", "final-reopen")
            if inputs != {p.name: file_identity(p) for p in source.iterdir()}:
                raise AssertionError("Source mutation.")
            receipt.update(
                passed=True,
                mediaPassed=True,
                savedAssets=len(saved),
                engineVersion=profile["version"],
            )
    finally:
        try:
            if (job / "output/previews").is_dir():
                shutil.copytree(job / "output/previews", evidence / "previews", dirs_exist_ok=True)
        finally:
            try:
                remove_owned_tree(job, REPO / "artifacts/ue")
                receipt["cleanupSucceeded"] = not job.exists()
            finally:
                atomic_write_result(evidence / "receipt.json", receipt)
                print("Evidence: " + str(evidence), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--editor", type=Path, required=True)
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()
    main(args.editor, args.timeout)
