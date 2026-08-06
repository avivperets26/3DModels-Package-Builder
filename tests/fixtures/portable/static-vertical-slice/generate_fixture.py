"""Regenerate the PB-0507 self-authored static cube fixture with approved Blender."""

from pathlib import Path

import bpy

fixture_root = Path(__file__).resolve().parent
output_path = fixture_root / "source" / "StoneArch.fbx"
output_path.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(size=2.0, location=(0.0, 0.0, 1.0))
cube = bpy.context.active_object
cube.name = "StoneArch"
cube.data.name = "MS_StoneArch"

material = bpy.data.materials.new(name="M_StoneArch")
material.diffuse_color = (0.32, 0.36, 0.42, 1.0)
cube.data.materials.append(material)

bpy.ops.export_scene.fbx(
    filepath=str(output_path),
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    bake_space_transform=False,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(output_path), use_anim=False)
mesh_objects = [item for item in bpy.context.scene.objects if item.type == "MESH"]
armatures = [item for item in bpy.context.scene.objects if item.type == "ARMATURE"]
assert len(mesh_objects) == 1, "The regenerated fixture must clean-reimport exactly one mesh."
assert not armatures, "The static fixture must not contain an armature."
assert not bpy.data.actions, "The static fixture must not contain animation actions."
