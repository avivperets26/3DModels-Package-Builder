"""Explicit commandlet bootstrap; never auto-executes when a customer project opens."""

import os
import sys
from pathlib import Path

import unreal

# Trusted harness paths; a product manifest cannot select Python code.
root = Path(os.environ["PACKAGEBUILDER_WORKERS_ROOT"]).resolve(strict=True)
sys.path[:0] = [str(root / "shared"), str(root / "unreal")]

from package_builder_protocol import resolve_logical_reference  # noqa: E402

from package_builder_unreal.worker import run  # noqa: E402

request = Path(os.environ["PACKAGEBUILDER_UNREAL_REQUEST"])
# Unreal owns native stdout framing. A dedicated flushed JSONL stream remains readable by PB-0209.
events = resolve_logical_reference(request.parent, "output/worker-events.jsonl")
events.parent.mkdir(parents=True, exist_ok=True)
with events.open("w", encoding="utf-8", newline="\n") as stream:
    code = run(request, unreal, stream, sys.stderr)
if code:
    # Unreal treats a Python exception as commandlet failure; SystemExit(0) is not required.
    raise RuntimeError(f"Unreal worker failed with protocol exit code {code}.")
