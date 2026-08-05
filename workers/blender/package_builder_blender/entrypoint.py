"""Blender-compatible command entrypoint for the Package Builder worker shell."""

from __future__ import annotations

import argparse
import importlib
import sys
from collections.abc import Sequence
from enum import IntEnum
from pathlib import Path
from typing import Any, TextIO

from package_builder_blender import __version__
from package_builder_blender.protocol import (
    PROTOCOL_VERSION,
    WorkerInputError,
    atomic_write_result,
    emit_event,
    load_request,
    resolve_logical_reference,
)


class WorkerExitCode(IntEnum):
    """Stable process exit codes documented for the .NET process boundary."""

    SUCCESS = 0
    INVALID_INVOCATION = 2
    INVALID_REQUEST = 3
    UNSUPPORTED_OPERATION = 4
    EXECUTION_FAILED = 5
    RESULT_WRITE_FAILED = 6


def _progress(job_id: str, stage: str, message: str, percent: float) -> dict[str, Any]:
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "eventKind": "progress",
        "jobId": job_id,
        "stage": stage,
        "message": message,
        "percent": percent,
    }


def _finding(job_id: str, finding: dict[str, Any]) -> dict[str, Any]:
    return {
        "protocolVersion": PROTOCOL_VERSION,
        "eventKind": "finding",
        "jobId": job_id,
        "finding": finding,
    }


def _result(
    request: dict[str, Any],
    status: str,
    retry_safety: str,
    findings: list[dict[str, Any]] | None = None,
) -> dict[str, Any]:
    result: dict[str, Any] = {
        "protocolVersion": PROTOCOL_VERSION,
        "jobId": request["jobId"],
        "status": status,
        "workerVersion": __version__,
    }
    if "engineVersion" in request:
        result["engineVersion"] = request["engineVersion"]
    result.update(
        {
            "outputsPromoted": False,
            "artifacts": [],
            "findings": findings or [],
            "metrics": [],
            "logReferences": [],
            "retrySafety": retry_safety,
        }
    )
    return result


def _runtime_blender_version() -> str | None:
    """Read Blender's runtime tuple when hosted by Blender; remain testable in plain Python."""

    try:
        bpy = importlib.import_module("bpy")
    except ModuleNotFoundError:
        return None
    version = getattr(getattr(bpy, "app", None), "version", None)
    if not isinstance(version, tuple) or len(version) < 3:
        return None
    return ".".join(str(component) for component in version[:3])


def _write_result(
    result_path: Path, result: dict[str, Any], stderr: TextIO
) -> WorkerExitCode | None:
    try:
        atomic_write_result(result_path, result)
    except Exception:
        stderr.write("BLENDER_WORKER_RESULT_WRITE_FAILED\n")
        stderr.flush()
        return WorkerExitCode.RESULT_WRITE_FAILED
    return None


def run(request_path: Path, stdout: TextIO, stderr: TextIO) -> WorkerExitCode:
    """Execute one validated worker request and return a stable process code."""

    try:
        request = load_request(request_path)
        workspace = request_path.parent.resolve(strict=True)
        result_path = resolve_logical_reference(workspace, request["resultFileReference"])
    except (OSError, WorkerInputError):
        stderr.write("BLENDER_WORKER_REQUEST_INVALID\n")
        stderr.flush()
        return WorkerExitCode.INVALID_REQUEST

    job_id = request["jobId"]
    emit_event(
        stdout,
        _progress(job_id, "worker-starting", "Blender worker request accepted.", 0),
    )

    try:
        runtime_version = _runtime_blender_version()
    except Exception:
        finding = {
            "code": "BLENDER_WORKER_EXECUTION_FAILED",
            "severity": "fatal",
            "explanation": "The Blender worker could not initialize its runtime.",
            "source": "blender-worker",
            "suggestedAction": "Review the retained worker logs and retry with a verified installation.",
            "blocksRelease": True,
        }
        emit_event(stdout, _finding(job_id, finding))
        write_failure = _write_result(
            result_path,
            _result(request, "failure", "safe", [finding]),
            stderr,
        )
        return write_failure or WorkerExitCode.EXECUTION_FAILED

    if (
        runtime_version is not None
        and "engineVersion" in request
        and request["engineVersion"] != runtime_version
    ):
        finding = {
            "code": "BLENDER_ENGINE_VERSION_MISMATCH",
            "severity": "fatal",
            "explanation": "The running Blender version does not match the requested version.",
            "source": "blender-worker",
            "suggestedAction": "Run the request with its exact approved Blender executable.",
            "blocksRelease": True,
        }
        emit_event(stdout, _finding(job_id, finding))
        write_failure = _write_result(
            result_path,
            _result(request, "failure", "safe", [finding]),
            stderr,
        )
        return write_failure or WorkerExitCode.EXECUTION_FAILED

    if runtime_version is not None and "engineVersion" not in request:
        request["engineVersion"] = runtime_version

    if request["operation"] != "probe-blender-worker":
        finding = {
            "code": "BLENDER_OPERATION_UNSUPPORTED",
            "severity": "error",
            "explanation": "This Blender worker version does not support the requested operation.",
            "source": "blender-worker",
            "suggestedAction": "Use an operation implemented by the selected worker version.",
            "blocksRelease": True,
        }
        emit_event(stdout, _finding(job_id, finding))
        write_failure = _write_result(
            result_path,
            _result(request, "failure", "safe", [finding]),
            stderr,
        )
        return write_failure or WorkerExitCode.UNSUPPORTED_OPERATION

    emit_event(
        stdout,
        _progress(job_id, "worker-complete", "Blender worker probe completed.", 100),
    )
    write_failure = _write_result(
        result_path,
        _result(request, "success", "unsafe"),
        stderr,
    )
    return write_failure or WorkerExitCode.SUCCESS


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="package-builder-blender-worker")
    parser.add_argument("--request", required=True, help="Absolute protocol-v1 request file path.")
    return parser


def main(arguments: Sequence[str] | None = None) -> int:
    """Parse worker-only arguments and execute the request."""

    try:
        namespace = _parser().parse_args(arguments)
    except SystemExit as error:
        return int(error.code) if error.code else int(WorkerExitCode.INVALID_INVOCATION)
    return int(run(Path(namespace.request), sys.stdout, sys.stderr))


def main_from_blender(arguments: Sequence[str] | None = None) -> int:
    """Extract arguments after Blender's required ``--`` separator."""

    values = list(sys.argv if arguments is None else arguments)
    if "--" not in values:
        sys.stderr.write("BLENDER_WORKER_ARGUMENT_SEPARATOR_MISSING\n")
        return int(WorkerExitCode.INVALID_INVOCATION)
    return main(values[values.index("--") + 1 :])
