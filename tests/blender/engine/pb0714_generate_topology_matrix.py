"""Generate redistribution-safe Generic-rig topology fixtures and a shared matrix manifest."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path
from typing import Any

import bpy
from mathutils import Vector

_CASES: tuple[dict[str, Any], ...] = (
    {
        "id": "mechanical-bow",
        "category": "articulated-mechanical-bow",
        "assetId": "MechanicalBow",
        "rootBone": "BowRoot",
        "movingBone": "RightLimb",
        "bones": (
            ("BowRoot", None, (0, 0, 0), (0, 0, 1)),
            ("LeftLimb", "BowRoot", (0, 0, 1), (-0.8, 0, 1.8)),
            ("RightLimb", "BowRoot", (0, 0, 1), (0.8, 0, 1.8)),
            ("StringAnchor", "BowRoot", (0, 0, 1), (0, 0, 2)),
        ),
    },
    {
        "id": "mechanical-vehicle",
        "category": "articulated-mechanical-vehicle",
        "assetId": "MechanicalVehicle",
        "rootBone": "VehicleRoot",
        "movingBone": "FrontWheelRight",
        "bones": (
            ("VehicleRoot", None, (0, 0, 0), (0, 0, 0.5)),
            ("Chassis", "VehicleRoot", (0, 0, 0.5), (0, 0, 1.2)),
            ("FrontAxle", "VehicleRoot", (0, 0, 0.5), (0, 1, 0.5)),
            ("FrontWheelLeft", "FrontAxle", (0, 1, 0.5), (-0.8, 1, 0.5)),
            ("FrontWheelRight", "FrontAxle", (0, 1, 0.5), (0.8, 1, 0.5)),
            ("RearAxle", "VehicleRoot", (0, 0, 0.5), (0, -1, 0.5)),
            ("RearWheelLeft", "RearAxle", (0, -1, 0.5), (-0.8, -1, 0.5)),
            ("RearWheelRight", "RearAxle", (0, -1, 0.5), (0.8, -1, 0.5)),
        ),
    },
    {
        "id": "quadruped-tail",
        "category": "quadruped-with-tail",
        "assetId": "QuadrupedTail",
        "rootBone": "BodyRoot",
        "movingBone": "TailTip",
        "bones": (
            ("BodyRoot", None, (0, 0, 0), (0, 0, 1)),
            ("Spine", "BodyRoot", (0, 0, 1), (0, 0, 2)),
            ("Neck", "Spine", (0, 0, 2), (0, 0.7, 2.4)),
            ("FrontLegLeft", "Spine", (0, 0.4, 1.5), (-0.5, 0.5, 0.2)),
            ("FrontLegRight", "Spine", (0, 0.4, 1.5), (0.5, 0.5, 0.2)),
            ("RearLegLeft", "BodyRoot", (0, -0.4, 0.8), (-0.5, -0.5, 0.1)),
            ("RearLegRight", "BodyRoot", (0, -0.4, 0.8), (0.5, -0.5, 0.1)),
            ("TailBase", "BodyRoot", (0, 0, 1), (0, -0.9, 1.3)),
            ("TailTip", "TailBase", (0, -0.9, 1.3), (0, -1.8, 1.6)),
        ),
    },
    {
        "id": "winged-creature",
        "category": "winged-creature",
        "assetId": "WingedCreature",
        "rootBone": "CreatureRoot",
        "movingBone": "WingRightTip",
        "bones": (
            ("CreatureRoot", None, (0, 0, 0), (0, 0, 1)),
            ("Chest", "CreatureRoot", (0, 0, 1), (0, 0, 2)),
            ("WingLeftBase", "Chest", (0, 0, 1.7), (-0.9, 0, 2)),
            ("WingLeftTip", "WingLeftBase", (-0.9, 0, 2), (-1.8, 0, 1.8)),
            ("WingRightBase", "Chest", (0, 0, 1.7), (0.9, 0, 2)),
            ("WingRightTip", "WingRightBase", (0.9, 0, 2), (1.8, 0, 1.8)),
            ("Tail", "CreatureRoot", (0, 0, 1), (0, -1.2, 0.8)),
        ),
    },
    {
        "id": "biped-tail",
        "category": "non-humanoid-biped-with-tail",
        "assetId": "BipedTail",
        "rootBone": "PelvisRoot",
        "movingBone": "TailTip",
        "bones": (
            ("PelvisRoot", None, (0, 0, 0), (0, 0, 1)),
            ("Spine", "PelvisRoot", (0, 0, 1), (0, 0, 2)),
            ("Head", "Spine", (0, 0, 2), (0, 0, 2.7)),
            ("LeftLeg", "PelvisRoot", (0, 0, 0.5), (-0.5, 0, -0.8)),
            ("RightLeg", "PelvisRoot", (0, 0, 0.5), (0.5, 0, -0.8)),
            ("LeftArm", "Spine", (0, 0, 1.8), (-1.0, 0, 1.4)),
            ("RightArm", "Spine", (0, 0, 1.8), (1.0, 0, 1.4)),
            ("TailBase", "PelvisRoot", (0, 0, 0.8), (0, -0.8, 0.5)),
            ("TailTip", "TailBase", (0, -0.8, 0.5), (0, -1.5, 0.2)),
        ),
    },
)


def _arguments() -> Path:
    """Read and create the one requested output directory."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 1:
        raise ValueError("Expected exactly one topology-matrix output directory.")
    output = Path(values[0]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    return output


def _cube(
    center: Vector, radius: float = 0.12
) -> tuple[list[tuple[float, ...]], list[tuple[int, ...]]]:
    """Return one small disconnected cube used to expose a bone's deformation."""

    vertices = [
        tuple(center + Vector((x, y, z)))
        for x in (-radius, radius)
        for y in (-radius, radius)
        for z in (-radius, radius)
    ]
    faces = [
        (0, 1, 3, 2),
        (4, 6, 7, 5),
        (0, 4, 5, 1),
        (2, 3, 7, 6),
        (0, 2, 6, 4),
        (1, 5, 7, 3),
    ]
    return vertices, faces


def _create_fixture(
    case: dict[str, Any], *, animated: bool
) -> tuple[bpy.types.Object, bpy.types.Object]:
    """Create one armature whose weighted disconnected cubes make every declared bone observable."""

    bpy.ops.wm.read_factory_settings(use_empty=True)
    asset_id = case["assetId"]
    armature_data = bpy.data.armatures.new(f"SK_{asset_id}")
    armature = bpy.data.objects.new(f"R_{asset_id}", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    edit_bones: dict[str, bpy.types.EditBone] = {}
    for name, parent_name, head, tail in case["bones"]:
        bone = armature_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = True
        if parent_name is not None:
            bone.parent = edit_bones[parent_name]
            bone.use_connect = (
                Vector(head) - Vector(edit_bones[parent_name].tail)
            ).length < 0.000001
        edit_bones[name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)

    vertices: list[tuple[float, ...]] = []
    faces: list[tuple[int, ...]] = []
    vertex_ranges: dict[str, tuple[int, ...]] = {}
    for name, _parent, head, tail in case["bones"]:
        cube_vertices, cube_faces = _cube((Vector(head) + Vector(tail)) * 0.5)
        offset = len(vertices)
        vertices.extend(cube_vertices)
        faces.extend(tuple(index + offset for index in face) for face in cube_faces)
        vertex_ranges[name] = tuple(range(offset, offset + len(cube_vertices)))

    mesh = bpy.data.meshes.new(f"MS_{asset_id}")
    mesh.from_pydata(vertices, (), faces)
    mesh.update()
    mesh_object = bpy.data.objects.new(f"P_{asset_id}", mesh)
    bpy.context.scene.collection.objects.link(mesh_object)
    mesh_object.parent = armature
    for name, indices in vertex_ranges.items():
        mesh_object.vertex_groups.new(name=name).add(indices, 1.0, "REPLACE")
    modifier = mesh_object.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature

    material = bpy.data.materials.new(f"M_{asset_id}")
    material.diffuse_color = (0.12, 0.55, 0.78, 1.0)
    mesh.materials.append(material)

    if animated:
        bpy.context.view_layer.objects.active = armature
        armature.animation_data_create()
        action = bpy.data.actions.new("Deform")
        armature.animation_data.action = action
        moving = armature.pose.bones[case["movingBone"]]
        moving.rotation_mode = "XYZ"
        for frame, angle in ((1, 0.0), (11, math.radians(35.0)), (21, 0.0)):
            moving.rotation_euler[1] = angle
            moving.keyframe_insert(data_path="rotation_euler", frame=frame)
        bpy.context.scene.frame_start = 1
        bpy.context.scene.frame_end = 21
        bpy.context.scene.render.fps = 30
    return armature, mesh_object


def _export(
    output: Path, armature: bpy.types.Object, mesh_object: bpy.types.Object, *, animated: bool
) -> None:
    """Export exactly one selected rig and mesh with reviewed deterministic FBX settings."""

    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    mesh_object.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=animated,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"Blender did not create the topology fixture: {output.name}")


def _manifest_case(case: dict[str, Any]) -> dict[str, Any]:
    """Return engine-neutral expectations without Blender-only data types."""

    return {
        "id": case["id"],
        "category": case["category"],
        "assetId": case["assetId"],
        "modelFile": f"{case['assetId']}.fbx",
        "rendererName": f"P_{case['assetId']}",
        "rootBone": case["rootBone"],
        "movingBone": case["movingBone"],
        "clipId": "Deform",
        "firstFrame": 1,
        "lastFrame": 21,
        "frameRate": 30,
        "bones": [
            {"name": name, "parent": parent or ""} for name, parent, _head, _tail in case["bones"]
        ],
    }


def main() -> None:
    """Generate five valid animated topologies, one invalid multi-root rig, and their manifest."""

    output_root = _arguments()
    manifest_cases: list[dict[str, Any]] = []
    for case in _CASES:
        armature, mesh_object = _create_fixture(case, animated=True)
        _export(output_root / f"{case['assetId']}.fbx", armature, mesh_object, animated=True)
        manifest_cases.append(_manifest_case(case))

    invalid_case = {
        "assetId": "InvalidMultiRoot",
        "bones": (
            ("RootA", None, (-0.5, 0, 0), (-0.5, 0, 1)),
            ("RootB", None, (0.5, 0, 0), (0.5, 0, 1)),
        ),
    }
    armature, mesh_object = _create_fixture(invalid_case, animated=False)
    _export(output_root / "InvalidMultiRoot.fbx", armature, mesh_object, animated=False)

    manifest = {
        "schemaVersion": 1,
        "cases": manifest_cases,
        "invalidCases": [
            {
                "id": "multi-root",
                "modelFile": "InvalidMultiRoot.fbx",
                "expectedFinding": "UNITY_TOPOLOGY_ROOT_COUNT_INVALID",
            }
        ],
    }
    manifest_path = output_root / "topology-matrix.txt"
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )


if __name__ == "__main__":
    main()
