"""Normalize the shared static source for Unreal through the existing smoothing-aware FBX exporter."""

import hashlib
import sys
from pathlib import Path

import bpy

REPO = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(REPO / "workers/blender"), str(REPO / "workers/shared")]
from package_builder_protocol import reject_linked_path  # noqa: E402

from package_builder_blender.fbx_export import (  # noqa: E402
    NormalizedFbxExportPlan,
    export_normalized_fbx,
)

output = Path(sys.argv[sys.argv.index("--") + 1])
reject_linked_path(output)
output = output.resolve()
if not output.is_relative_to(REPO / "artifacts/ue") or not output.is_dir():
    raise ValueError("Normalization requires an owned Unreal input directory.")
source = REPO / "tests/fixtures/portable/static-vertical-slice/source/StoneArch.fbx"
reject_linked_path(source)
original = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
result = export_normalized_fbx(
    tuple(bpy.data.objects),
    tuple(bpy.data.actions),
    bpy.context.view_layer,
    NormalizedFbxExportPlan(
        "Asymmetric", "static", output, "Asymmetric.fbx", ("StoneArch",), ("M_StoneArch",)
    ),
    bpy.ops.export_scene.fbx,
)
if not result.succeeded or hashlib.sha256(source.read_bytes()).hexdigest() != original:
    raise RuntimeError("Normalized static export failed or changed its source.")
