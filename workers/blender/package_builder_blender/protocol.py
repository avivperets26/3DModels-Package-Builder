"""Compatibility import for the canonical Python worker protocol (PB-1103)."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "shared"))

from package_builder_protocol import (
    MAXIMUM_INPUT_BYTES as MAXIMUM_INPUT_BYTES,
    MAXIMUM_JSON_DEPTH as MAXIMUM_JSON_DEPTH,
    PROTOCOL_VERSION as PROTOCOL_VERSION,
    WorkerInputError as WorkerInputError,
    atomic_write_result as atomic_write_result,
    compact_json as compact_json,
    emit_event as emit_event,
    load_request as load_request,
    resolve_logical_reference as resolve_logical_reference,
    validate_request as validate_request,
)
