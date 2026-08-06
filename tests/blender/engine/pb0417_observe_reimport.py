"""Observe one PB-0417 GLB in a distinct empty Blender 5 process."""

from __future__ import annotations

import json
import os
import sys
import uuid
from pathlib import Path
from typing import Any


def _arguments() -> tuple[Path, Path, Path]:
    """Read source GLB, expectation JSON, and observation JSON paths."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 3:
        raise ValueError("Expected source, expectation, and observation paths.")
    return tuple(Path(value).resolve() for value in values)  # type: ignore[return-value]


SOURCE_PATH, EXPECTATION_FILE, OBSERVATION_FILE = _arguments()

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402


def _bounds() -> list[float]:
    """Measure world-space bounds across imported meshes."""

    points = [
        scene_object.matrix_world @ Vector(corner)
        for scene_object in bpy.data.objects
        if scene_object.type == "MESH"
        for corner in scene_object.bound_box
    ]
    if not points:
        raise RuntimeError("Reimport produced no mesh bounds.")
    return [
        min(point.x for point in points),
        min(point.y for point in points),
        min(point.z for point in points),
        max(point.x for point in points),
        max(point.y for point in points),
        max(point.z for point in points),
    ]


def _sample(expected: dict[str, Any]) -> dict[str, object]:
    """Evaluate the imported clip at the same representative vertex and frame."""

    mesh_object = bpy.data.objects.get(expected["objectName"])
    action = bpy.data.actions.get(expected["clipName"])
    armature = next((item for item in bpy.data.objects if item.type == "ARMATURE"), None)
    if mesh_object is None or action is None or armature is None:
        raise RuntimeError("Imported deformation target, Action, or armature is missing.")
    armature.animation_data_create()
    armature.animation_data.action = action
    frame = float(expected["frame"])
    bpy.context.scene.frame_set(int(frame))
    evaluated = mesh_object.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh = evaluated.to_mesh()
    try:
        vertex_index = int(expected["vertexIndex"])
        position = evaluated.matrix_world @ evaluated_mesh.vertices[vertex_index].co
        return {
            "objectName": mesh_object.name,
            "clipName": action.name,
            "frame": frame,
            "vertexIndex": vertex_index,
            "position": [position.x, position.y, position.z],
        }
    finally:
        evaluated.to_mesh_clear()
        bpy.context.scene.frame_set(1)


def main() -> None:
    """Reset, verify emptiness, import once, and persist deterministic observations."""

    records = json.loads(EXPECTATION_FILE.read_text(encoding="utf-8"))["artifacts"]
    expected = next(item for item in records if item["sourceFilename"] == SOURCE_PATH.name)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    started_empty = not tuple(bpy.data.objects)
    result = set(
        bpy.ops.import_scene.gltf(
            filepath=str(SOURCE_PATH),
            import_pack_images=True,
            import_shading="NORMALS",
            bone_heuristic="TEMPERANCE",
            guess_original_bind_pose=False,
            disable_bone_shape=True,
            import_scene_as_collection=False,
            import_scene_extras=False,
            import_select_created_objects=False,
            import_merge_material_slots=False,
            import_webp_texture=False,
            import_unused_materials=False,
            export_import_convert_lighting_mode="SPEC",
            merge_vertices=False,
            loglevel=-1,
        )
    )
    if result != {"FINISHED"}:
        raise RuntimeError(f"GLB import returned {sorted(result)}")

    samples = [_sample(item) for item in expected["deformationSamples"]]
    observation = {
        "processInstanceId": f"{os.getpid()}-{uuid.uuid4()}",
        "startedFromEmptyScene": started_empty,
        "sourceFilename": SOURCE_PATH.name,
        "counts": {
            "objectCount": len(tuple(bpy.data.objects)),
            "meshCount": sum(item.type == "MESH" for item in bpy.data.objects),
            "materialCount": len(tuple(bpy.data.materials)),
            "skeletonCount": sum(item.type == "ARMATURE" for item in bpy.data.objects),
            "animationCount": len(tuple(bpy.data.actions)),
        },
        "bounds": _bounds(),
        "deformationSamples": samples,
    }
    OBSERVATION_FILE.write_text(json.dumps(observation, indent=2), encoding="utf-8")
    print(f"PB-0417 observed {SOURCE_PATH.name} in {observation['processInstanceId']}")


if __name__ == "__main__":
    main()
