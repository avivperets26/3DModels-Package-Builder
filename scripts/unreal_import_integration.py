"""Live candidate acceptance; retains compact evidence and disposes generated projects in finally."""

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


def main(editor: Path, timeout: int):
    """Run import, independent reopen and no-overwrite failure through the actual clone adapter."""
    profile = load_bounded_json(REPO / "profiles/engines/unreal-5.8.2-candidate.json")
    expected_editor = (
        Path(profile["userApprovedExternalInstallationRoot"])
        / "Engine/Binaries/Win64/UnrealEditor-Cmd.exe"
    )
    if editor != expected_editor:
        raise ValueError("Only the preflight-approved candidate is allowed.")
    run_id = uuid.uuid4().hex
    job = REPO / "artifacts/ue" / run_id
    evidence = REPO / "artifacts/PB-1104" / run_id
    job.mkdir(parents=True)
    evidence.mkdir(parents=True)
    receipt = {
        "realEngineRun": False,
        "passed": False,
        "cleanupSucceeded": False,
        "engineVersion": profile["version"],
        "operations": [],
        "editorSha256": file_identity(editor)[0],
    }
    try:
        source = job / "input"
        source.mkdir()
        (job / "temp").mkdir()
        for name in ("source/texture.png", "unreal-import-plan.json"):
            shutil.copyfile(REPO / "tests/fixtures/unreal" / name, source / Path(name).name)
        (source / "product.json").write_text("{}", encoding="utf-8")
        clone = UnrealProjectClone(REPO, REPO / profile["template"], job, "PBTextureFixture")
        with clone:
            hashes = None
            # A second host must fail before it can execute another Editor against this project.
            try:
                with UnrealProjectClone(REPO, REPO / profile["template"], job, "PBTextureFixture"):
                    raise AssertionError("Concurrent lease was granted.")
            except OSError:
                receipt["concurrentWriterRejected"] = True
            for operation, expected_failure in (
                ("import-unreal-textures", False),
                ("verify-unreal-textures", False),
                ("import-unreal-textures", True),
            ):
                key = operation + ("-duplicate" if expected_failure else "")
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
                native_log = evidence / (key + ".log")
                environment = dict(
                    os.environ,
                    PACKAGEBUILDER_WORKERS_ROOT=str(REPO / "workers"),
                    PACKAGEBUILDER_UNREAL_REQUEST=str(job / "request.json"),
                    PYTHONDONTWRITEBYTECODE="1",
                    TEMP=str(job / "temp"),
                    TMP=str(job / "temp"),
                )
                environment["UE-LocalDataCachePath"] = str(REPO / "runtime-data/unreal/5.8.2/ddc")
                environment["UE-SharedDataCachePath"] = "None"
                arguments = [
                    str(clone.project / "PBTextureFixture.uproject"),
                    "-run=pythonscript",
                    "-script="
                    + str(
                        clone.project
                        / "Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py"
                    ),
                    "-unattended",
                    "-nosplash",
                    "-nosound",
                    "-NullRHI",
                    "-ddc=(Local)",
                    "-ini:EditorSettings:[/Script/UnrealEd.AnalyticsPrivacySettings]:bSendUsageData=False",
                    "-UserDir=" + str(job / "user"),
                    "-abslog=" + str(native_log),
                ]
                receipt["realEngineRun"] = True
                code = clone.execute(editor, arguments, environment, timeout)
                result = load_bounded_json(job / "output/result.json")
                atomic_write_result(evidence / (key + ".json"), result)
                if (
                    (code != 0) != expected_failure
                    or result["outputsPromoted"]
                    or result["jobId"] != request["jobId"]
                ):
                    raise AssertionError("Unexpected exit/result identity.")
                current = {
                    p.relative_to(clone.project).as_posix(): file_identity(p)
                    for p in clone.project.rglob("*.uasset")
                }
                if hashes is not None and hashes != current:
                    raise AssertionError("Reopen or rejected request changed asset bytes.")
                if not expected_failure:
                    if (
                        result["status"] != "success"
                        or len(result["artifacts"]) != 9
                        or result["findings"]
                        or len(current) != 9
                        or re.search(
                            r"\bWarning:|\bError:|Fatal error:", native_log.read_text("utf-8")
                        )
                    ):
                        raise AssertionError("Import/reopen assets or engine diagnostics failed.")
                    for artifact in result["artifacts"]:
                        if file_identity(job / artifact["logicalReference"]) != (
                            artifact["sha256"],
                            artifact["byteCount"],
                        ):
                            raise AssertionError("Result artifact identity differs.")
                    events = [
                        json.loads(line)
                        for line in (job / "output/worker-events.jsonl")
                        .read_text("utf-8")
                        .splitlines()
                    ]
                    if [event.get("percent") for event in events] != [0, 100]:
                        raise AssertionError("Progress is incomplete.")
                    hashes = current
                elif result["status"] != "failure" or result["artifacts"] or not result["findings"]:
                    raise AssertionError("Overwrite attempt did not fail closed.")
                receipt["operations"].append(key)
            if file_identity(source / "texture.png") != file_identity(
                REPO / "tests/fixtures/unreal/source/texture.png"
            ):
                raise AssertionError("Input texture was mutated.")
            receipt["assets"] = hashes
        receipt["cloneCleanupSucceeded"] = clone.cleanup_succeeded
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
