"""Measure the exact extracted static FBX in a fresh Blender process; no saved scene."""

import hashlib
import json
import sys
from pathlib import Path

import bpy

source, output = [Path(value).resolve() for value in sys.argv[sys.argv.index("--") + 1 :]]
repository = Path(__file__).resolve().parents[3]
source.relative_to(repository / "artifacts" / "PB-0507" / "manual")
output.relative_to(repository / "artifacts")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
passed = len(meshes) == 1 and not any(obj.type == "ARMATURE" for obj in bpy.data.objects)
triangles = 0
for obj in meshes:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
passed = passed and triangles > 0
output.write_text(
    json.dumps(
        {
            "passed": passed,
            "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "blenderVersion": bpy.app.version_string,
            "meshCount": len(meshes),
            "triangles": triangles,
        }
    ),
    encoding="utf-8",
)
if not passed:
    raise RuntimeError("Static FBX inspection failed")
