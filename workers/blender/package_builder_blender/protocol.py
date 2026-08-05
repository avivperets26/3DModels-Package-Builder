"""Strict protocol-v1 request, progress, and result helpers for the Blender worker."""

from __future__ import annotations

import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any, TextIO

PROTOCOL_VERSION = 1
MAXIMUM_INPUT_BYTES = 1_048_576
MAXIMUM_JSON_DEPTH = 64

_REQUIRED_REQUEST_FIELDS = frozenset(
    {
        "protocolVersion",
        "jobId",
        "operation",
        "productManifestReference",
        "inputDirectoryReference",
        "outputDirectoryReference",
        "resultFileReference",
    }
)
_OPTIONAL_REQUEST_FIELDS = frozenset({"engineVersion", "target"})
_CANONICAL_IDENTIFIER = re.compile(r"^[a-z]+(?:-[a-z]+)*$")
_VERSION_TEXT = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
_TARGETS = frozenset({"portable", "unity", "unreal"})


class WorkerInputError(ValueError):
    """Raised when a request cannot safely cross the worker boundary."""


def _reject_duplicate_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise WorkerInputError("The request contains a duplicate property.")
        result[key] = value
    return result


def _depth(value: Any, current: int = 1) -> int:
    if isinstance(value, dict):
        return max([current, *(_depth(item, current + 1) for item in value.values())])
    if isinstance(value, list):
        return max([current, *(_depth(item, current + 1) for item in value)])
    return current


def _is_identity(value: Any) -> bool:
    return (
        isinstance(value, str)
        and bool(value)
        and not value[0].isspace()
        and not value[-1].isspace()
        and not any(ord(character) < 32 or ord(character) == 127 for character in value)
    )


def _is_logical_reference(value: Any) -> bool:
    if not _is_identity(value) or value.startswith(("/", "\\")) or "\\" in value:
        return False
    if ":" in value or "$" in value or "%" in value or "//" in value:
        return False
    return all(segment not in {"", ".", ".."} for segment in value.split("/"))


def validate_request(value: Any) -> dict[str, Any]:
    """Validate the PB-0112 request subset without requiring third-party packages."""

    if not isinstance(value, dict):
        raise WorkerInputError("The request root must be an object.")
    fields = set(value)
    if not _REQUIRED_REQUEST_FIELDS.issubset(fields):
        raise WorkerInputError("The request is missing a required property.")
    if not fields.issubset(_REQUIRED_REQUEST_FIELDS | _OPTIONAL_REQUEST_FIELDS):
        raise WorkerInputError("The request contains an unknown property.")
    if type(value["protocolVersion"]) is not int or value["protocolVersion"] != PROTOCOL_VERSION:
        raise WorkerInputError("The worker protocol version is not supported.")
    if not _is_identity(value["jobId"]):
        raise WorkerInputError("The job identity is invalid.")
    if not isinstance(value["operation"], str) or not _CANONICAL_IDENTIFIER.fullmatch(
        value["operation"]
    ):
        raise WorkerInputError("The worker operation is invalid.")
    for field in (
        "productManifestReference",
        "inputDirectoryReference",
        "outputDirectoryReference",
        "resultFileReference",
    ):
        if not _is_logical_reference(value[field]):
            raise WorkerInputError("A logical reference is invalid.")
    if "engineVersion" in value and (
        not isinstance(value["engineVersion"], str)
        or not _VERSION_TEXT.fullmatch(value["engineVersion"])
    ):
        raise WorkerInputError("The engine version is invalid.")
    if "target" in value and value["target"] not in _TARGETS:
        raise WorkerInputError("The target is invalid.")
    return value


def load_request(request_path: Path) -> dict[str, Any]:
    """Read one bounded UTF-8 request while rejecting links, duplicates, and excess depth."""

    if not request_path.is_absolute() or request_path.is_symlink() or not request_path.is_file():
        raise WorkerInputError("The request file is unavailable or unsafe.")
    size = request_path.stat().st_size
    if size == 0 or size > MAXIMUM_INPUT_BYTES:
        raise WorkerInputError("The request file size is invalid.")
    try:
        text = request_path.read_text(encoding="utf-8-sig")
        value = json.loads(text, object_pairs_hook=_reject_duplicate_pairs)
    except (OSError, UnicodeError, json.JSONDecodeError, RecursionError) as error:
        raise WorkerInputError("The request is not valid UTF-8 JSON.") from error
    if _depth(value) > MAXIMUM_JSON_DEPTH:
        raise WorkerInputError("The request exceeds the maximum JSON depth.")
    return validate_request(value)


def resolve_logical_reference(workspace: Path, reference: str) -> Path:
    """Resolve a protocol reference beneath the request workspace, including linked parents."""

    if not _is_logical_reference(reference):
        raise WorkerInputError("A logical reference is invalid.")
    root = workspace.resolve(strict=True)
    candidate = (root / Path(*reference.split("/"))).resolve(strict=False)
    if not candidate.is_relative_to(root):
        raise WorkerInputError("A logical reference leaves the request workspace.")
    return candidate


def compact_json(value: dict[str, Any]) -> str:
    """Serialize deterministic compact UTF-8-compatible JSON without non-finite values."""

    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":"))


def emit_event(stream: TextIO, value: dict[str, Any]) -> None:
    """Write and flush exactly one physical JSON Lines record."""

    stream.write(compact_json(value))
    stream.write("\n")
    stream.flush()


def atomic_write_result(path: Path, value: dict[str, Any]) -> None:
    """Durably replace the result without exposing a partial JSON file."""

    if path.exists() and (path.is_symlink() or path.is_dir()):
        raise OSError("The result destination is unsafe.")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(compact_json(value))
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    except BaseException:
        temporary_path.unlink(missing_ok=True)
        raise
