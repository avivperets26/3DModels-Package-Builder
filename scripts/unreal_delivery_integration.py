"""Native acceptance for sanitized ZIPs and fresh project reopen; no retained test packages."""

import argparse
import os
import shutil
import sys
import uuid
from pathlib import Path
from types import SimpleNamespace

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]
from package_builder_protocol import atomic_write_result, load_bounded_json  # noqa: E402

from package_builder_unreal.import_plan import file_identity  # noqa: E402
from package_builder_unreal.project import (  # noqa: E402
    UnrealProjectClone,
    exclusive_file,
    remove_owned_tree,
)
from package_builder_unreal.project_validation import (  # noqa: E402
    log_findings,
    preview_diagnostics,
)
from unreal_candidate_process import candidate_editor, commandlet  # noqa: E402


def main(editor, timeout, preview=False):
    """Use an owned source clone and independent extraction; keep compact receipts on failure too."""
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    if editor != candidate_editor(profile):
        raise ValueError("Unexpected candidate editor.")
    run_id = uuid.uuid4().hex
    job = REPO / "artifacts/ue" / run_id
    evidence = REPO / "artifacts/PB-1113" / run_id
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
            if preview:
                from unreal_preview_helper import install_helper

                receipt["editorHelperReceipt"] = str(install_helper(REPO, clone).relative_to(REPO))
            env = dict(
                os.environ,
                PYTHONDONTWRITEBYTECODE="1",
                TEMP=str(job / "temp"),
                TMP=str(job / "temp"),
                PB_UNREAL_OVERVIEW_FIXTURE="1",
                PB_UNREAL_OVERVIEW_INPUT=str(source),
                PB_UNREAL_DELIVERY_JOB=str(job),
                PB_UNREAL_INTERACTIVE="1" if preview else "0",
            )

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
                    env,
                    timeout,
                ):
                    raise RuntimeError("Host acceptance failed: " + name)

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
                raise RuntimeError("Fixture geometry failed.")
            dotnet_test("ExportLiveOverviewFixtureWhenRequested")
            if preview:
                shutil.copy2(
                    source / "preview-experience.json", evidence / "preview-experience.json"
                )
            (source / "product.json").write_text("{}", encoding="utf-8")
            original_inputs = {p.name: file_identity(p) for p in source.iterdir()}

            def operation(target, name, key, expected=None):
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
                atomic_write_result(target.job / "request.json", request)
                args, native_env = commandlet(target, REPO, evidence, key)
                if target is not clone:
                    args[2] = "-script=" + str(
                        REPO
                        / "engine-templates/unreal/5.8/Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py"
                    )
                    args.append("-EnablePlugins=PythonScriptPlugin,EditorScriptingUtilities")
                receipt["realEngineRun"] = True
                code = clone.execute(editor, args, native_env, timeout)
                result = load_bounded_json(target.job / "output/result.json")
                atomic_write_result(evidence / (key + ".json"), result)
                if expected:
                    if (
                        not code
                        or result["status"] != "failure"
                        or result["findings"][0]["code"] != expected
                    ):
                        raise AssertionError("Missing native rejection: " + key)
                elif (
                    code
                    or result["status"] != "success"
                    or result["findings"]
                    or log_findings((evidence / (key + ".log")).read_text("utf-8"))
                ):
                    raise RuntimeError("Native operation failed: " + key)
                for item in result["artifacts"]:
                    if file_identity(target.job / item["logicalReference"]) != (
                        item["sha256"],
                        item["byteCount"],
                    ):
                        raise AssertionError("Native output identity mismatch.")
                receipt["operations"].append(key)

            def play(target, key, runtime_only=False, baseline=False):
                """Run real PIE; the clean extraction enables only vendor Python instrumentation."""
                arguments, environment = commandlet(target, REPO, evidence, key, render=True)
                arguments[1:3] = [
                    "-ExecutePythonScript=" + str(REPO / "scripts/unreal_preview_play_test.py")
                ]
                arguments.remove("-AllowCommandletRendering")
                arguments += ["-windowed", "-ResX=1920", "-ResY=1080", "-culture=en"]
                if runtime_only:
                    arguments.append("-EnablePlugins=PythonScriptPlugin,EditorScriptingUtilities")
                environment.update(
                    PB_PREVIEW_EVIDENCE=str(evidence),
                    PB_PREVIEW_PLAY_KEY=key,
                    PB_PREVIEW_RUNTIME_ONLY="1" if runtime_only else "0",
                    PB_PREVIEW_BASELINE="1" if baseline else "0",
                )
                code = clone.execute(editor, arguments, environment, timeout)
                result = load_bounded_json(evidence / (key + ".json"))
                result["processExitCode"] = code
                atomic_write_result(evidence / (key + ".json"), result)
                if code or not result["passed"]:
                    raise RuntimeError("Native PIE failed: " + key)
                text = (evidence / (key + ".log")).read_text("utf-8")
                baseline_text = (
                    text if baseline else (evidence / "engine-baseline.log").read_text("utf-8")
                )
                diagnostics = preview_diagnostics(text, baseline_text)
                atomic_write_result(evidence / (key + "-diagnostics.json"), diagnostics)
                if diagnostics["findings"]:
                    raise RuntimeError("Native PIE emitted diagnostics: " + key)
                receipt["operations"].append(key)

            if preview:
                play(clone, "engine-baseline", baseline=True)
            for name in (
                "import-unreal-textures",
                "import-unreal-surfaces",
                "create-unreal-overview",
                "prepare-unreal-delivery",
            ):
                if preview and name == "prepare-unreal-delivery":
                    play(clone, "preview-play")
                operation(clone, name, name)
            dotnet_test("CreateAndExtractLiveUnrealDeliveryWhenRequested")
            shutil.copy2(job / "archive-receipt.json", evidence / "archive-receipt.json")
            shutil.copy2(job / "output/delivery/inventory.json", evidence / "inventory.json")
            fresh = job / "fresh"
            project = fresh / "project"
            (fresh / "extracted" / clone.project_name).rename(project)
            shutil.copytree(source, fresh / "input")
            (fresh / "temp").mkdir()
            target = SimpleNamespace(job=fresh, project=project, project_name=clone.project_name)
            before = {
                p.relative_to(project).as_posix(): file_identity(p)
                for p in project.rglob("*")
                if p.is_file()
            }
            # The source clone may not satisfy missing references in the clean extraction.
            hidden = job / "source-project-unavailable"
            clone.project.rename(hidden)
            try:
                with exclusive_file(fresh / ".project.lock"):
                    operation(target, "verify-unreal-delivery", "fresh-reopen")
                    if preview:
                        play(target, "fresh-preview-play", runtime_only=True)
                        missing_preview = (
                            project / "Content/PBOverviewFixture/Preview/WBP_Preview.uasset"
                        )
                        parked_preview = fresh / "parked-preview.uasset"
                        missing_preview.rename(parked_preview)
                        try:
                            operation(
                                target,
                                "verify-unreal-delivery",
                                "reject-missing-preview",
                                "UNREAL_ASSET_INVENTORY",
                            )
                        finally:
                            parked_preview.rename(missing_preview)
                    for path, identity in before.items():
                        if file_identity(project / path) != identity:
                            raise AssertionError("Reopen modified delivered content.")
                    missing = project / "Content/PBOverviewFixture/Maps/L_Overview.umap"
                    parked = fresh / "parked-map.umap"
                    missing.rename(parked)
                    try:
                        operation(
                            target,
                            "verify-unreal-delivery",
                            "reject-missing-map",
                            "UNREAL_ASSET_INVENTORY",
                        )
                    finally:
                        parked.rename(missing)
                    texture = project / "Content/PBOverviewFixture/Textures/T_albedo.uasset"
                    backup = fresh / "original-texture.uasset"
                    shutil.copy2(texture, backup)
                    try:
                        args, native_env = commandlet(target, REPO, evidence, "source-path-fault")
                        args[2] = "-script=" + str(REPO / "scripts/unreal_delivery_fault.py")
                        args.append("-EnablePlugins=PythonScriptPlugin,EditorScriptingUtilities")
                        if clone.execute(editor, args, native_env, timeout):
                            raise RuntimeError("Source path fault injection failed.")
                        operation(
                            target,
                            "verify-unreal-delivery",
                            "reject-source-path",
                            "UNREAL_DELIVERY_SOURCE_PATH",
                        )
                    finally:
                        shutil.copy2(backup, texture)
                    descriptor = project / (clone.project_name + ".uproject")
                    descriptor_bytes = descriptor.read_bytes()
                    try:
                        altered = load_bounded_json(descriptor)
                        altered["AdditionalRootDirectories"] = []
                        atomic_write_result(descriptor, altered)
                        operation(
                            target,
                            "verify-unreal-delivery",
                            "reject-extra-descriptor-field",
                            "UNREAL_DELIVERY_DESCRIPTOR",
                        )
                    finally:
                        descriptor.write_bytes(descriptor_bytes)
                    operation(target, "verify-unreal-delivery", "restored-reopen")
                    for path, identity in before.items():
                        if file_identity(project / path) != identity:
                            raise AssertionError("Final extraction identity changed.")
            finally:
                hidden.rename(clone.project)
            if original_inputs != {p.name: file_identity(p) for p in source.iterdir()}:
                raise AssertionError("Input mutation.")
            receipt.update(
                passed=True,
                engineVersion=profile["version"],
                files=len(before),
                sourceUnchanged=True,
                freshProjectHashesUnchanged=True,
            )
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
    parser.add_argument("--preview", action="store_true")
    args = parser.parse_args()
    main(args.editor, args.timeout, args.preview)
