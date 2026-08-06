"""Generate real Blender 5 static, rigged, and animated PB-0416 GLB fixtures."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


def _arguments() -> tuple[Path, Path, Path]:
    """Read the repository, output, and expectation paths after Blender's separator."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 3:
        raise ValueError("Expected repository root, output root, and expectation file.")
    return tuple(Path(value).resolve() for value in values)  # type: ignore[return-value]


REPOSITORY_ROOT, OUTPUT_ROOT, EXPECTATION_FILE = _arguments()
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

from package_builder_blender.case_inference import (  # noqa: E402
    RIGGED,
    RIGGED_ANIMATED,
    STATIC,
)
from package_builder_blender.glb_export import (  # noqa: E402
    NormalizedGlbExportPlan,
    export_normalized_glb,
)
from package_builder_blender.texture_inspection import inspect_textures  # noqa: E402


def _reset() -> None:
    """Start each generated fixture from Blender's verified empty factory state."""

    bpy.ops.wm.read_factory_settings(use_empty=True)
    if tuple(bpy.data.objects):
        raise RuntimeError("Factory reset did not produce an empty scene.")


def _material(asset_id: str) -> tuple[Any, Any]:
    """Create one generated image connected to a Principled base-color input."""

    image = bpy.data.images.new(f"T_{asset_id}_Albedo", width=2, height=2, alpha=True)
    image.file_format = "PNG"
    image.pixels = (
        0.8,
        0.2,
        0.1,
        1.0,
        0.2,
        0.8,
        0.1,
        1.0,
        0.1,
        0.2,
        0.8,
        1.0,
        0.8,
        0.8,
        0.2,
        1.0,
    )

    material = bpy.data.materials.new(f"M_{asset_id}")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    texture = nodes.new("ShaderNodeTexImage")
    texture.name = "Albedo"
    texture.label = "Albedo"
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    return material, image


def _mesh(asset_id: str, material: Any) -> Any:
    """Create a small textured triangle with deterministic UVs and normals."""

    mesh = bpy.data.meshes.new(f"MS_{asset_id}")
    mesh.from_pydata(
        ((-0.5, 0.0, 0.0), (0.5, 0.0, 0.0), (0.0, 0.0, 1.0)),
        (),
        ((0, 1, 2),),
    )
    mesh.materials.append(material)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    coordinates = ((0.0, 0.0), (1.0, 0.0), (0.5, 1.0))
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = coordinates[vertex_index]

    scene_object = bpy.data.objects.new(f"P_{asset_id}", mesh)
    bpy.context.scene.collection.objects.link(scene_object)
    return scene_object


def _armature(asset_id: str, mesh_object: Any) -> Any:
    """Create one deform bone and bind every mesh vertex to it."""

    armature_data = bpy.data.armatures.new(f"SK_{asset_id}")
    armature_object = bpy.data.objects.new(f"R_{asset_id}", armature_data)
    bpy.context.scene.collection.objects.link(armature_object)
    bpy.context.view_layer.objects.active = armature_object
    armature_object.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bone = armature_data.edit_bones.new("Root")
    bone.head = (0.0, 0.0, 0.0)
    bone.tail = (0.0, 0.0, 1.0)
    bone.use_deform = True
    bpy.ops.object.mode_set(mode="OBJECT")
    armature_object.select_set(False)

    group = mesh_object.vertex_groups.new(name="Root")
    group.add(tuple(range(len(mesh_object.data.vertices))), 1.0, "REPLACE")
    modifier = mesh_object.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature_object
    mesh_object.parent = armature_object
    return armature_object


def _animate(asset_id: str, armature_object: Any) -> Any:
    """Create one Blender 5 layered Action with measurable deform-bone motion."""

    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = 20
    scene.render.fps = 24
    armature_object.animation_data_create()
    action = bpy.data.actions.new(f"A_{asset_id}_Motion")
    armature_object.animation_data.action = action
    pose_bone = armature_object.pose.bones["Root"]
    pose_bone.location = (0.0, 0.0, 0.0)
    pose_bone.keyframe_insert(data_path="location", frame=1, group=pose_bone.name)
    pose_bone.location = (0.0, 0.0, 0.5)
    pose_bone.keyframe_insert(data_path="location", frame=20, group=pose_bone.name)
    scene.frame_set(1)
    return action


def _mesh_bounds() -> tuple[float, float, float, float, float, float]:
    """Measure deterministic world-space bounds across intended meshes."""

    points = [
        scene_object.matrix_world @ Vector(corner)
        for scene_object in bpy.data.objects
        if scene_object.type == "MESH"
        for corner in scene_object.bound_box
    ]
    if not points:
        raise RuntimeError("Fixture contains no mesh bounds.")
    return (
        min(point.x for point in points),
        min(point.y for point in points),
        min(point.z for point in points),
        max(point.x for point in points),
        max(point.y for point in points),
        max(point.z for point in points),
    )


def _deformation_sample(mesh_object: Any, action_name: str) -> dict[str, object]:
    """Measure one evaluated vertex at the animated end frame."""

    bpy.context.scene.frame_set(20)
    evaluated = mesh_object.evaluated_get(bpy.context.evaluated_depsgraph_get())
    evaluated_mesh = evaluated.to_mesh()
    try:
        position = evaluated.matrix_world @ evaluated_mesh.vertices[2].co
        return {
            "objectName": mesh_object.name,
            "clipName": action_name,
            "frame": 20.0,
            "vertexIndex": 2,
            "position": [position.x, position.y, position.z],
        }
    finally:
        evaluated.to_mesh_clear()
        bpy.context.scene.frame_set(1)


def _glb_inventory(
    path: Path,
    selected_names: tuple[str, ...],
    material_name: str,
    image_name: str,
    action_name: str | None,
    product_case: str,
) -> dict[str, object]:
    """Verify the real GLB JSON chunk contains exact embedded logical content."""

    with path.open("rb") as stream:
        header = stream.read(20)
        json_length = int.from_bytes(header[12:16], "little")
        if header[:4] != b"glTF" or json_length <= 0 or json_length > 16 * 1024 * 1024:
            raise RuntimeError("Exported GLB has an invalid or unbounded JSON chunk.")
        document = json.loads(stream.read(json_length).decode("utf-8").rstrip(" \x00"))

    node_names = tuple(item.get("name", "") for item in document.get("nodes", ()))
    material_names = tuple(item.get("name", "") for item in document.get("materials", ()))
    images = tuple(document.get("images", ()))
    image_names = tuple(item.get("name", "") for item in images)
    animation_names = tuple(item.get("name", "") for item in document.get("animations", ()))
    expected_animations = () if action_name is None else (action_name,)
    expected_skin_count = 0 if product_case == STATIC else 1
    if (
        not set(selected_names).issubset(node_names)
        or material_names != (material_name,)
        or image_names != (image_name,)
        or len(tuple(document.get("textures", ()))) != 1
        or len(tuple(document.get("skins", ()))) != expected_skin_count
        or animation_names != expected_animations
        or any("uri" in image or "bufferView" not in image for image in images)
    ):
        raise RuntimeError("Exported GLB logical inventory does not match the normalized plan.")
    return {
        "nodeNames": list(node_names),
        "materialNames": list(material_names),
        "imageNames": list(image_names),
        "textureCount": len(tuple(document.get("textures", ()))),
        "skinCount": len(tuple(document.get("skins", ()))),
        "animationNames": list(animation_names),
        "imagesEmbedded": True,
    }


def _generate(asset_id: str, product_case: str) -> dict[str, object]:
    """Build, save, export, and measure one canonical product case."""

    _reset()
    material, image = _material(asset_id)
    mesh_object = _mesh(asset_id, material)
    armature_object = None
    action = None
    if product_case in {RIGGED, RIGGED_ANIMATED}:
        armature_object = _armature(asset_id, mesh_object)
    if product_case == RIGGED_ANIMATED:
        action = _animate(asset_id, armature_object)

    texture_result = inspect_textures(tuple(bpy.data.images), tuple(bpy.data.materials))
    if not texture_result.succeeded or texture_result.report is None:
        raise RuntimeError("Real Blender texture inspection failed.")

    selected = (mesh_object.name,)
    if armature_object is not None:
        selected += (armature_object.name,)
    filename = f"{asset_id}.glb"
    plan = NormalizedGlbExportPlan(
        asset_id=asset_id,
        product_case=product_case,
        output_root=OUTPUT_ROOT,
        output_filename=filename,
        selected_object_names=selected,
        expected_material_names=(material.name,),
        expected_image_names=(image.name,),
        expected_action_names=() if action is None else (action.name,),
        copyright_notice="Package Builder synthetic validation fixture",
    )

    blend_path = OUTPUT_ROOT / f"{asset_id}.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), check_existing=False)
    result = export_normalized_glb(
        tuple(bpy.data.objects),
        tuple(bpy.data.images),
        tuple(bpy.data.actions),
        texture_result.report,
        bpy.context.view_layer,
        plan,
        bpy.ops.export_scene.gltf,
    )
    if not result.succeeded or result.report is None:
        codes = [finding.code for finding in result.findings]
        raise RuntimeError(f"PB-0416 export failed for {asset_id}: {codes}")
    inventory = _glb_inventory(
        OUTPUT_ROOT / filename,
        selected,
        material.name,
        image.name,
        None if action is None else action.name,
        product_case,
    )

    counts = {
        "objectCount": len(selected),
        "meshCount": 1,
        "materialCount": 1,
        "skeletonCount": 0 if armature_object is None else 1,
        "animationCount": 0 if action is None else 1,
    }
    samples = () if action is None else (_deformation_sample(mesh_object, action.name),)
    return {
        "sourceFilename": filename,
        "blendFilename": blend_path.name,
        "productCase": product_case,
        "counts": counts,
        "bounds": list(_mesh_bounds()),
        "deformationSamples": list(samples),
        "byteCount": result.report.byte_count,
        "glbInventory": inventory,
    }


def main() -> None:
    """Generate all three real-engine fixtures and their PB-0417 expectations."""

    OUTPUT_ROOT.mkdir(parents=True, exist_ok=False)
    records = [
        _generate("PB0416_Static", STATIC),
        _generate("PB0416_Rigged", RIGGED),
        _generate("PB0416_Animated", RIGGED_ANIMATED),
    ]
    EXPECTATION_FILE.write_text(
        json.dumps({"blenderVersion": bpy.app.version_string, "artifacts": records}, indent=2),
        encoding="utf-8",
    )
    print(f"PB-0416 real exports passed: {len(records)}/3")


if __name__ == "__main__":
    main()
