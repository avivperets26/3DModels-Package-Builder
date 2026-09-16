"""Generate original CC0 asymmetric acceptance geometry using the existing normalized FBX exporter."""

import os
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
if not output.resolve().is_relative_to(REPO / "artifacts/ue") or not output.is_dir():
    raise ValueError("Fixture output must be an owned Unreal job input.")
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1.5))
cube = bpy.context.active_object
cube.name = "Asymmetric"
cube.dimensions = (3, 3, 3) if os.environ.get("PB_UNREAL_OVERVIEW_FIXTURE") == "1" else (1, 2, 3)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
cube.data.materials.append(bpy.data.materials.new("FixtureSurface"))
plan = NormalizedFbxExportPlan(
    "Asymmetric", "static", output, "Asymmetric.fbx", ("Asymmetric",), ("FixtureSurface",)
)
result = export_normalized_fbx(
    tuple(bpy.data.objects),
    tuple(bpy.data.actions),
    bpy.context.view_layer,
    plan,
    bpy.ops.export_scene.fbx,
)
if not result.succeeded:
    raise RuntimeError(str(result.findings))
