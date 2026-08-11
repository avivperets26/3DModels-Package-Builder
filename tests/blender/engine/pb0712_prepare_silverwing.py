"""Prepare the private Silverwing Blender scene as one Unity-ready animated FBX."""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import bpy

SOURCE_BOW = "Mesh_0"
SOURCE_STRING = "CleanBowString"
SOURCE_SKELETON = "UniRigArmature"
SOURCE_CURVE_RIG = "BowCurveRig"
SOURCE_BOW_ACTION = "Bow_Flex_Rig"
SOURCE_STRING_ACTION = "Bow_Shot_String.001"
OUTPUT_SKELETON = "SKEL_SilverwingTalonbow"
OUTPUT_BOW = "P_SilverwingTalonbow_Body"
OUTPUT_STRING = "P_SilverwingTalonbow_String"
OUTPUT_ACTION = "Bow_Shot"


def _arguments() -> tuple[Path, Path, Path]:
    """Read repository, FBX output, and structured-report paths."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 3:
        raise ValueError("Expected repository root, FBX output, and report paths.")
    repository_root = Path(values[0]).resolve(strict=True)
    output = Path(values[1]).resolve()
    report = Path(values[2]).resolve()
    if output.suffix.lower() != ".fbx" or report.suffix.lower() != ".json":
        raise ValueError("Silverwing outputs must be one FBX and one JSON report.")
    if output.exists() or report.exists() or output.parent != report.parent:
        raise ValueError("Silverwing outputs must be new files in one retained run directory.")
    output.parent.mkdir(parents=True, exist_ok=True)
    return repository_root, output, report


def _required_object(name: str, object_type: str) -> bpy.types.Object:
    """Return one exact object or reject a changed private source layout."""

    value = bpy.data.objects.get(name)
    if value is None or value.type != object_type:
        raise RuntimeError(f"Required {object_type} object is missing: {name}")
    return value


def _required_action(name: str) -> bpy.types.Action:
    """Return one exact Action or reject a changed private source layout."""

    value = bpy.data.actions.get(name)
    if value is None:
        raise RuntimeError(f"Required Action is missing: {name}")
    return value


def _sample_pose(
    rig: bpy.types.Object,
    action: bpy.types.Action,
    first_frame: int,
    last_frame: int,
) -> dict[int, dict[str, tuple[tuple[float, ...], tuple[float, ...], tuple[float, ...]]]]:
    """Sample local pose transforms without modifying the private source file on disk."""

    rig.animation_data_create()
    rig.animation_data.action = action
    sampled: dict[
        int,
        dict[str, tuple[tuple[float, ...], tuple[float, ...], tuple[float, ...]]],
    ] = {}
    for frame in range(first_frame, last_frame + 1):
        bpy.context.scene.frame_set(frame)
        frame_pose = {}
        for pose_bone in rig.pose.bones:
            translation, rotation, scale = pose_bone.matrix_basis.decompose()
            frame_pose[pose_bone.name] = (
                tuple(translation),
                tuple(rotation),
                tuple(scale),
            )
        sampled[frame] = frame_pose
    rig.animation_data.action = None
    return sampled


def _join_armatures(skeleton: bpy.types.Object, curve_rig: bpy.types.Object) -> None:
    """Join both deform rigs and declare the original skeleton root as the sole root."""

    if not skeleton.matrix_world.is_identity or not curve_rig.matrix_world.is_identity:
        raise RuntimeError("Silverwing armature object transforms must remain identity transforms.")
    bpy.ops.object.mode_set(
        mode="OBJECT"
    ) if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT")
    skeleton.select_set(True)
    curve_rig.select_set(True)
    bpy.context.view_layer.objects.active = skeleton
    result = bpy.ops.object.join()
    if set(result) != {"FINISHED"} or bpy.data.objects.get(SOURCE_CURVE_RIG) is not None:
        raise RuntimeError("Blender could not consolidate the Silverwing armatures.")

    bpy.context.view_layer.objects.active = skeleton
    bpy.ops.object.mode_set(mode="EDIT")
    curve_root = skeleton.data.edit_bones.get("CurveRoot")
    original_root = skeleton.data.edit_bones.get("Bone_000")
    if curve_root is None or original_root is None:
        raise RuntimeError("The consolidated Silverwing root bones are missing.")
    curve_root.parent = original_root
    curve_root.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")


def _bind_meshes(
    skeleton: bpy.types.Object,
    bow: bpy.types.Object,
    bow_string: bpy.types.Object,
) -> None:
    """Bind both visible meshes to one armature with one modifier per renderer."""

    for mesh_object in (bow, bow_string):
        armature_modifiers = [item for item in mesh_object.modifiers if item.type == "ARMATURE"]
        if not armature_modifiers:
            raise RuntimeError(f"Skinned object has no Armature modifier: {mesh_object.name}")
        retained = armature_modifiers[0]
        retained.object = skeleton
        retained.name = "Armature"
        for duplicate in armature_modifiers[1:]:
            mesh_object.modifiers.remove(duplicate)
        mesh_object.parent = skeleton
        mesh_object.matrix_parent_inverse = skeleton.matrix_world.inverted()
        if not mesh_object.vertex_groups:
            raise RuntimeError(f"Skinned object has no vertex groups: {mesh_object.name}")


def _create_combined_action(
    skeleton: bpy.types.Object,
    sampled_poses: tuple[
        dict[int, dict[str, tuple[tuple[float, ...], tuple[float, ...], tuple[float, ...]]]],
        ...,
    ],
    first_frame: int,
    last_frame: int,
) -> bpy.types.Action:
    """Bake both sampled actions into one non-destructive, skeleton-owned Action."""

    for action in tuple(bpy.data.actions):
        bpy.data.actions.remove(action)
    action = bpy.data.actions.new(OUTPUT_ACTION)
    skeleton.animation_data_create()
    skeleton.animation_data.action = action
    for pose_bone in skeleton.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"

    for frame in range(first_frame, last_frame + 1):
        bpy.context.scene.frame_set(frame)
        for source in sampled_poses:
            for bone_name, (location, rotation, scale) in source[frame].items():
                pose_bone = skeleton.pose.bones.get(bone_name)
                if pose_bone is None:
                    raise RuntimeError(f"Sampled Silverwing bone was not consolidated: {bone_name}")
                pose_bone.location = location
                pose_bone.rotation_quaternion = rotation
                pose_bone.scale = scale
                pose_bone.keyframe_insert(data_path="location", frame=frame, group=bone_name)
                pose_bone.keyframe_insert(
                    data_path="rotation_quaternion", frame=frame, group=bone_name
                )
                pose_bone.keyframe_insert(data_path="scale", frame=frame, group=bone_name)
    return action


def _remove_non_product_objects(keep: set[str]) -> None:
    """Remove cameras, lights, hidden source copies, and authoring helpers from memory."""

    for scene_object in tuple(bpy.data.objects):
        if scene_object.name not in keep:
            bpy.data.objects.remove(scene_object, do_unlink=True)


def _sha256(path: Path) -> str:
    """Hash the generated FBX for deterministic retained evidence."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    """Normalize, export, and report the approved private Silverwing fixture."""

    repository_root, output, report_path = _arguments()
    worker_root = repository_root / "workers" / "blender"
    sys.path.insert(0, str(worker_root))
    from package_builder_blender.case_inference import RIGGED_ANIMATED
    from package_builder_blender.fbx_export import (
        NormalizedFbxExportPlan,
        export_normalized_fbx,
    )

    source_path = Path(bpy.data.filepath).resolve(strict=True)
    bow = _required_object(SOURCE_BOW, "MESH")
    bow_string = _required_object(SOURCE_STRING, "MESH")
    skeleton = _required_object(SOURCE_SKELETON, "ARMATURE")
    curve_rig = _required_object(SOURCE_CURVE_RIG, "ARMATURE")
    bow_action = _required_action(SOURCE_BOW_ACTION)
    string_action = _required_action(SOURCE_STRING_ACTION)
    first_frame, last_frame = 1, 60
    sampled_bow = _sample_pose(curve_rig, bow_action, first_frame, last_frame)
    sampled_string = _sample_pose(skeleton, string_action, first_frame, last_frame)

    _join_armatures(skeleton, curve_rig)
    _bind_meshes(skeleton, bow, bow_string)
    action = _create_combined_action(
        skeleton, (sampled_bow, sampled_string), first_frame, last_frame
    )
    skeleton.name = OUTPUT_SKELETON
    skeleton.data.name = OUTPUT_SKELETON
    bow.name = OUTPUT_BOW
    bow.data.name = "MS_SilverwingTalonbow_Body"
    bow_string.name = OUTPUT_STRING
    bow_string.data.name = "MS_SilverwingTalonbow_String"
    _remove_non_product_objects({skeleton.name, bow.name, bow_string.name})

    scene = bpy.context.scene
    scene.frame_start = first_frame
    scene.frame_end = last_frame
    scene.render.fps = 30
    scene.render.fps_base = 1.0
    scene.frame_set(first_frame)
    plan = NormalizedFbxExportPlan(
        asset_id="SilverwingTalonbow",
        product_case=RIGGED_ANIMATED,
        output_root=output.parent,
        output_filename=output.name,
        selected_object_names=(skeleton.name, bow.name, bow_string.name),
        expected_material_names=tuple(
            sorted(
                {
                    slot.material.name
                    for mesh_object in (bow, bow_string)
                    for slot in mesh_object.material_slots
                    if slot.material is not None
                }
            )
        ),
        expected_action_names=(action.name,),
    )
    export_result = export_normalized_fbx(
        tuple(bpy.data.objects),
        tuple(bpy.data.actions),
        bpy.context.view_layer,
        plan,
        bpy.ops.export_scene.fbx,
    )
    if not export_result.succeeded or export_result.report is None:
        codes = ",".join(item.code for item in export_result.findings)
        raise RuntimeError(f"Silverwing normalized FBX export failed: {codes}")

    payload = {
        "schemaVersion": 1,
        "sourceFileName": source_path.name,
        "outputFileName": output.name,
        "outputSha256": _sha256(output),
        "outputByteCount": output.stat().st_size,
        "actionName": action.name,
        "firstFrame": first_frame,
        "lastFrame": last_frame,
        "framesPerSecond": scene.render.fps,
        "armatureCount": 1,
        "meshNames": sorted((bow.name, bow_string.name)),
        "boneCount": len(skeleton.data.bones),
        "rootBoneNames": sorted(bone.name for bone in skeleton.data.bones if bone.parent is None),
        "materialNames": list(export_result.report.material_names),
    }
    report_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
