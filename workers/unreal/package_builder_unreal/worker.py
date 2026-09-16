"""Bounded protocol shell with engine operations isolated behind Unreal's Editor APIs."""

from __future__ import annotations

import hashlib
import re
from pathlib import Path
from typing import Any, TextIO

from package_builder_protocol import (
    WorkerInputError,
    atomic_write_result,
    emit_event,
    load_request,
    resolve_logical_reference,
)

from package_builder_unreal import __version__

ASSET = "/Game/Pack/PB_WorkerProbe"


def _progress(stream: TextIO, job: str, stage: str, percent: int) -> None:
    emit_event(
        stream,
        {
            "protocolVersion": 1,
            "eventKind": "progress",
            "jobId": job,
            "stage": stage,
            "percent": percent,
        },
    )


def _asset(unreal: Any, workspace: Path, operation: str) -> dict[str, Any]:
    """Create/save or independently reopen one fixed probe asset in a disposable clone."""
    project = Path(unreal.Paths.project_dir()).resolve(strict=True)
    if project != resolve_logical_reference(workspace, "project"):
        raise WorkerInputError("The engine is not running in the owned project clone.")
    destination = resolve_logical_reference(workspace, "project/Content/Pack/PB_WorkerProbe.uasset")
    library = unreal.EditorAssetLibrary
    if operation == "probe-unreal-worker":
        if destination.exists() or library.does_asset_exist(ASSET):
            raise WorkerInputError("The probe must not replace existing assets.")
        asset = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            "PB_WorkerProbe", "/Game/Pack", unreal.Material, unreal.MaterialFactoryNew()
        )
        if asset is None or not library.save_loaded_asset(asset, only_if_is_dirty=False):
            raise RuntimeError("The probe asset was not saved.")
    elif library.load_asset(ASSET) is None:
        raise RuntimeError("The saved probe asset could not be loaded.")
    # Recheck after the engine writes, before opening its output for hashing.
    destination = resolve_logical_reference(workspace, "project/Content/Pack/PB_WorkerProbe.uasset")
    digest = hashlib.sha256()
    with destination.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    if destination.stat().st_size == 0:
        raise RuntimeError("The saved asset is empty.")
    return {
        "artifactId": "unreal-probe",
        "role": "engine-asset",
        "logicalReference": "project/Content/Pack/PB_WorkerProbe.uasset",
        "target": "unreal",
        "sha256": digest.hexdigest(),
        "byteCount": destination.stat().st_size,
    }


def run(request_path: Path, unreal: Any, stdout: TextIO, stderr: TextIO) -> int:
    """Run one request using the existing Python exit-code boundary (0, 3-6).

    A failed engine operation requires clone cleanup; no output is promoted. Native Unreal
    diagnostics stay in its log, while this stream contains only protocol JSON Lines.
    """
    try:
        request = load_request(request_path)
        workspace = request_path.parent.resolve(strict=True)
        result_path = resolve_logical_reference(workspace, request["resultFileReference"])
        output = resolve_logical_reference(workspace, request["outputDirectoryReference"])
        if not result_path.is_relative_to(output) or result_path == output:
            raise WorkerInputError("Result must be inside the output directory.")
        project = resolve_logical_reference(workspace, "project")
        manifest = resolve_logical_reference(workspace, request["productManifestReference"])
        source = resolve_logical_reference(workspace, request["inputDirectoryReference"])
        if (
            output.is_relative_to(project)
            or project.is_relative_to(output)
            or result_path in {manifest, request_path}
            or output.is_relative_to(source)
            or source.is_relative_to(output)
        ):
            raise WorkerInputError("Worker outputs overlap protected inputs or the project.")
        if request.get("target") != "unreal" or not re.fullmatch(
            r"\d+\.\d+\.\d+", request.get("engineVersion", "")
        ):
            raise WorkerInputError("An exact stable Unreal version and target are required.")
    except (OSError, ValueError, TypeError):
        stderr.write("UNREAL_WORKER_REQUEST_INVALID\n")
        return 3
    job = request["jobId"]
    result = {
        "protocolVersion": 1,
        "jobId": job,
        "status": "failure",
        "workerVersion": __version__,
        "engineVersion": request["engineVersion"],
        "outputsPromoted": False,
        "artifacts": [],
        "findings": [],
        "metrics": [],
        "logReferences": [],
        "retrySafety": "safe",
    }
    _progress(stdout, job, "worker-starting", 0)
    code = 5
    failure = "UNREAL_WORKER_EXECUTION_FAILED"
    try:
        runtime = unreal.SystemLibrary.get_engine_version().split("-", 1)[0]
        if runtime != request["engineVersion"]:
            failure = "UNREAL_ENGINE_VERSION_MISMATCH"
        elif request["operation"] not in {"probe-unreal-worker", "verify-unreal-worker"}:
            failure = "UNREAL_OPERATION_UNSUPPORTED"
            code = 4
        else:
            result["retrySafety"] = "requires-cleanup"
            artifact = _asset(unreal, workspace, request["operation"])
            artifact["jobId"] = job
            result.update(status="success", retrySafety="unsafe", artifacts=[artifact])
            _progress(stdout, job, "worker-complete", 100)
            code = 0
    except Exception:
        failure = "UNREAL_WORKER_EXECUTION_FAILED"
    if code:
        finding = {
            "code": failure,
            "severity": "error",
            "blocksRelease": True,
            "source": "unreal-worker",
            "explanation": "The Unreal worker request failed.",
            "suggestedAction": "Inspect the local engine log and use a fresh project clone.",
        }
        result["findings"] = [finding]
        emit_event(
            stdout, {"protocolVersion": 1, "eventKind": "finding", "jobId": job, "finding": finding}
        )
    try:
        atomic_write_result(result_path, result)
    except Exception:
        stderr.write("UNREAL_WORKER_RESULT_WRITE_FAILED\n")
        return 6
    return code
