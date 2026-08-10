"""Generate reusable real rigged or animated FBX fixtures for Unity policy tests."""

from __future__ import annotations

import sys
from pathlib import Path

import bpy


def _arguments() -> tuple[Path, bool]:
    """Read the output path and optional animated-fixture mode."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) not in (1, 2) or (len(values) == 2 and values[1] != "animated"):
        raise ValueError("Expected an FBX output path and optional 'animated' mode.")
    output = Path(values[0]).resolve()
    if output.suffix.lower() != ".fbx":
        raise ValueError("The PB-0701 fixture output must be an FBX file.")
    output.parent.mkdir(parents=True, exist_ok=True)
    return output, len(values) == 2


def _create_fixture(animated: bool) -> tuple[bpy.types.Object, bpy.types.Object]:
    """Create a two-bone deform rig, weighted mesh, and optional sampled action."""

    bpy.ops.wm.read_factory_settings(use_empty=True)

    armature_data = bpy.data.armatures.new("SK_RiggedProp")
    armature = bpy.data.objects.new("R_RiggedProp", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    root = armature_data.edit_bones.new("Root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.0, 1.0)
    root.use_deform = True
    tip = armature_data.edit_bones.new("Tip")
    tip.head = root.tail
    tip.tail = (0.0, 0.0, 2.0)
    tip.parent = root
    tip.use_connect = True
    tip.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)

    mesh = bpy.data.meshes.new("MS_RiggedProp")
    mesh.from_pydata(
        (
            (-0.4, -0.1, 0.0),
            (0.4, -0.1, 0.0),
            (0.4, 0.1, 0.0),
            (-0.4, 0.1, 0.0),
            (-0.4, -0.1, 2.0),
            (0.4, -0.1, 2.0),
            (0.4, 0.1, 2.0),
            (-0.4, 0.1, 2.0),
        ),
        (),
        (
            (0, 3, 2, 1),
            (4, 5, 6, 7),
            (0, 1, 5, 4),
            (3, 7, 6, 2),
            (0, 4, 7, 3),
            (1, 2, 6, 5),
        ),
    )
    mesh.update()
    mesh_object = bpy.data.objects.new("P_RiggedProp", mesh)
    bpy.context.scene.collection.objects.link(mesh_object)
    mesh_object.parent = armature

    root_group = mesh_object.vertex_groups.new(name="Root")
    root_group.add((0, 1, 2, 3), 1.0, "REPLACE")
    tip_group = mesh_object.vertex_groups.new(name="Tip")
    tip_group.add((4, 5, 6, 7), 1.0, "REPLACE")
    modifier = mesh_object.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature

    material = bpy.data.materials.new("M_RiggedProp")
    material.diffuse_color = (0.18, 0.42, 0.72, 1.0)
    mesh.materials.append(material)

    if animated:
        bpy.context.view_layer.objects.active = armature
        armature.animation_data_create()
        action = bpy.data.actions.new("Bend")
        armature.animation_data.action = action
        pose_bone = armature.pose.bones["Tip"]
        pose_bone.rotation_mode = "XYZ"
        for frame, angle in ((1, 0.0), (11, 0.45), (21, 0.0)):
            pose_bone.rotation_euler[1] = angle
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame)
        bpy.context.scene.frame_start = 1
        bpy.context.scene.frame_end = 21
        bpy.context.scene.render.fps = 30
    return armature, mesh_object


def main() -> None:
    """Export only the intended rig and mesh with no animation or helper objects."""

    output, animated = _arguments()
    armature, mesh_object = _create_fixture(animated)
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
        raise RuntimeError("Blender did not create the PB-0701 FBX fixture.")


if __name__ == "__main__":
    main()
