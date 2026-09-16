"""Native material/mesh acceptance with independent reopen and unconditional project cleanup."""

import argparse
import json
import os
import re
import shutil
import sys
import uuid
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import atomic_write_result, load_bounded_json  # noqa: E402

from package_builder_unreal.import_plan import file_identity  # noqa: E402
from package_builder_unreal.project import UnrealProjectClone, remove_owned_tree  # noqa: E402
from unreal_candidate_process import candidate_editor, commandlet  # noqa: E402


def main(editor, timeout):
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    if editor != candidate_editor(profile):
        raise ValueError("Only the preflight-approved candidate is allowed.")
    run_id = uuid.uuid4().hex
    job = REPO / "artifacts/ue" / run_id
    evidence = REPO / "artifacts/PB-1107" / run_id
    job.mkdir(parents=True)
    evidence.mkdir(parents=True)
    receipt = {
        "passed": False,
        "realEngineRun": False,
        "cleanupSucceeded": False,
        "operations": [],
        "engineVersion": profile["version"],
        "editorSha256": file_identity(editor)[0],
    }
    try:
        source = job / "input"
        source.mkdir()
        (job / "temp").mkdir()
        with UnrealProjectClone(REPO, REPO / profile["template"], job, "PBSurfaceFixture") as clone:
            env = dict(
                os.environ,
                PYTHONDONTWRITEBYTECODE="1",
                TEMP=str(job / "temp"),
                TMP=str(job / "temp"),
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
                env,
                timeout,
            ):
                raise RuntimeError("Normalized FBX fixture failed.")
            env["PB_UNREAL_SURFACE_FIXTURE"] = str(source)
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
                    "FullyQualifiedName~ExportLiveSurfaceFixtureWhenRequested",
                ],
                env,
                timeout,
            ):
                raise RuntimeError("Host-generated surface fixture failed.")
            (source / "product.json").write_text("{}", encoding="utf-8")
            original = {p.name: file_identity(p) for p in source.iterdir()}
            hashes = None
            for operation, failure in (
                ("import-unreal-textures", False),
                ("import-unreal-surfaces", False),
                ("verify-unreal-surfaces", False),
                ("import-unreal-surfaces", True),
            ):
                key = operation + ("-duplicate" if failure else "")
                request = {
                    "protocolVersion": 1,
                    "jobId": "Job-" + run_id,
                    "operation": operation,
                    "productManifestReference": "input/product.json",
                    "inputDirectoryReference": "input",
                    "outputDirectoryReference": "output",
                    "resultFileReference": "output/result.json",
                    "engineVersion": profile["version"],
                    "target": "unreal",
                }
                atomic_write_result(job / "request.json", request)
                arguments, environment = commandlet(clone, REPO, evidence, key)
                receipt["realEngineRun"] = True
                code = clone.execute(editor, arguments, environment, timeout)
                result = load_bounded_json(job / "output/result.json")
                atomic_write_result(evidence / (key + ".json"), result)
                if (code != 0) != failure or result["status"] != (
                    "failure" if failure else "success"
                ):
                    raise AssertionError("Native operation failed: " + key)
                if (
                    result["outputsPromoted"]
                    or result["jobId"] != request["jobId"]
                    or result["engineVersion"] != profile["version"]
                ):
                    raise AssertionError("Worker result identity or promotion differs.")
                if failure and (result["artifacts"] or not result["findings"]):
                    raise AssertionError("Rejected import did not fail closed.")
                current = {
                    p.relative_to(clone.project).as_posix(): file_identity(p)
                    for p in clone.project.rglob("*.uasset")
                }
                if hashes is not None and hashes != current:
                    raise AssertionError("Reopen/rejection changed saved assets.")
                if operation == "import-unreal-surfaces" and not failure:
                    if len(current) != 14 or len(result["artifacts"]) != 14:
                        raise AssertionError("Surface asset inventory mismatch.")
                    hashes = current
                if not failure:
                    if result["findings"] or re.search(
                        r"\bWarning:|\bError:|Fatal error:",
                        (evidence / (key + ".log")).read_text("utf-8"),
                    ):
                        raise AssertionError("Unexpected native import/reopen diagnostic.")
                    events = [
                        json.loads(line)
                        for line in (job / "output/worker-events.jsonl")
                        .read_text("utf-8")
                        .splitlines()
                    ]
                    if [event.get("percent") for event in events] != [0, 100]:
                        raise AssertionError("Incomplete progress events.")
                    for asset in result["artifacts"]:
                        if file_identity(job / asset["logicalReference"]) != (
                            asset["sha256"],
                            asset["byteCount"],
                        ):
                            raise AssertionError("Artifact identity differs.")
                receipt["operations"].append(key)
            arguments, environment = commandlet(clone, REPO, evidence, "render", render=True)
            arguments[2] = "-script=" + str(REPO / "scripts/unreal_surface_render_probe.py")
            code = clone.execute(editor, arguments, environment, timeout)
            rendered = job / "output/render"
            if rendered.exists():
                shutil.copytree(rendered, evidence / "render")
            if code or not load_bounded_json(evidence / "render/render-receipt.json")["passed"]:
                raise AssertionError("Native rendered material verification failed.")
            if hashes != {
                p.relative_to(clone.project).as_posix(): file_identity(p)
                for p in clone.project.rglob("*.uasset")
            }:
                raise AssertionError("Render probe changed persisted assets.")
            receipt["renderPassed"] = True
            if original != {p.name: file_identity(p) for p in source.iterdir()}:
                raise AssertionError("Source inputs changed.")
            receipt["assets"] = hashes
            receipt["passed"] = True
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
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()
    main(args.editor, args.timeout)
