"""Focused native Blueprint conformance run; the host always removes its disposable project."""

import json
import os
import sys
from pathlib import Path

import unreal as u

repo = Path(os.environ["PACKAGEBUILDER_WORKERS_ROOT"]).parent
sys.path[:0] = [str(repo / "workers/shared"), str(repo / "workers/unreal")]
from package_builder_unreal.preview_input import keyboard, pointer  # noqa: E402
from package_builder_unreal.preview_widget import (  # noqa: E402
    apply_functions,
    bind_controls,
    focus_functions,
    variables,
    widget_tree,
)

contract = json.loads(Path(os.environ["PB_PREVIEW_CONTRACT"]).read_text("utf-8"))
bp = widget_tree(u, "/Game/PBGraphProbe/Preview", contract)
variables(u, bp, contract)
apply_functions(u, bp, contract)
focus_functions(u, bp)
bind_controls(u, bp, contract)
keyboard(u, bp, contract)
pointer(u, bp, contract)
obj = u.get_default_object(bp.generated_class())
vectors = json.loads(
    (repo / "tests/fixtures/preview/preview-experience-v1-vectors.json").read_text("utf-8")
)
for case in vectors["camera"]:
    for name, number in zip(("Yaw", "Pitch", "Distance"), case["start"], strict=True):
        obj.set_editor_property(name, number)
    if "orbit" in case:
        obj.call_method("Orbit", tuple(case["orbit"]))
    else:
        obj.call_method("Zoom", (case["zoomSteps"], case["step"]))
    actual = [obj.get_editor_property(name) for name in ("Yaw", "Pitch", "Distance")]
    if any(abs(a - b) > 0.00001 for a, b in zip(actual, case["expected"], strict=True)):
        raise AssertionError((case["name"], actual, case["expected"]))
for case in vectors["light"]:
    for name, number in zip(("LightYaw", "LightPitch"), case["start"], strict=True):
        obj.set_editor_property(name, number)
    obj.call_method("AdjustLight", tuple(case["adjust"]))
    actual = [obj.get_editor_property(name) for name in ("LightYaw", "LightPitch")]
    if actual != case["expected"]:
        raise AssertionError((case["name"], actual))
if not u.PackageBuilderPreviewEditorLibrary.send_preview_key(obj, "R", False):
    raise AssertionError("Reset key was not handled by the compiled widget.")
for invalid in (float("nan"), float("inf"), float("-inf")):
    obj.call_method("Orbit", (invalid, 1.0))
    obj.call_method("Zoom", (invalid, 0.14))
    if [obj.get_editor_property(name) for name in ("Yaw", "Pitch", "Distance")] != [0, 0, 1]:
        raise AssertionError("Non-finite input changed camera state.")
Path(os.environ["PB_PREVIEW_EVIDENCE"], "graphs.json").write_text(
    json.dumps(
        {
            "compiled": True,
            "cameraVectors": len(vectors["camera"]),
            "lightVectors": len(vectors["light"]),
            "nonFiniteRejected": True,
            "nativeResetKey": True,
        }
    ),
    encoding="utf-8",
)
