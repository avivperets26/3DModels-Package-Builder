"""Generate the minimal real armature-and-skin FBX used by PB-0701/PB-0702."""

from __future__ import annotations

import sys
from pathlib import Path

import bpy


def _output_path() -> Path:
    """Read and validate the single output path supplied after Blender's separator."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 1:
        raise ValueError("Expected exactly one FBX output path.")
    output = Path(values[0]).resolve()
    if output.suffix.lower() != ".fbx":
        raise ValueError("The PB-0701 fixture output must be an FBX file.")
    output.parent.mkdir(parents=True, exist_ok=True)
    return output


def _create_fixture() -> tuple[bpy.types.Object, bpy.types.Object]:
    """Create a two-bone deform rig and a fully weighted mesh with stable names."""

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
        ((-0.4, 0.0, 0.0), (0.4, 0.0, 0.0), (-0.4, 0.0, 2.0), (0.4, 0.0, 2.0)),
        (),
        ((0, 1, 3), (0, 3, 2)),
    )
    mesh.update()
    mesh_object = bpy.data.objects.new("P_RiggedProp", mesh)
    bpy.context.scene.collection.objects.link(mesh_object)
    mesh_object.parent = armature

    root_group = mesh_object.vertex_groups.new(name="Root")
    root_group.add((0, 1), 1.0, "REPLACE")
    tip_group = mesh_object.vertex_groups.new(name="Tip")
    tip_group.add((2, 3), 1.0, "REPLACE")
    modifier = mesh_object.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature
    return armature, mesh_object


def main() -> None:
    """Export only the intended rig and mesh with no animation or helper objects."""

    output = _output_path()
    armature, mesh_object = _create_fixture()
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
        bake_anim=False,
        apply_unit_scale=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError("Blender did not create the PB-0701 FBX fixture.")


if __name__ == "__main__":
    main()
