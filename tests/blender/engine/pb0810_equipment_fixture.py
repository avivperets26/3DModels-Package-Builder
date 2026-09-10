"""Generate original CC0 equipment sources, or clean-reimport the actual portable ZIP."""

from __future__ import annotations

import json
import sys
import zipfile
from pathlib import Path

import bpy


def generate(root: Path) -> None:
    """Create two independently importable equipment meshes sharing one external texture."""
    root.mkdir(parents=True, exist_ok=True)
    for name in ("Helmet", "Armour"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if name == "Helmet":
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=0.3)
        else:
            bpy.ops.mesh.primitive_cube_add(size=1)
            bpy.context.object.scale = (0.55, 0.3, 0.7)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        model = bpy.context.object
        model.name = name
        material = bpy.data.materials.new("SharedSteel")
        material.use_nodes = True
        image = bpy.data.images.new("SharedSteel", width=2, height=2)
        image.pixels = [0.4, 0.45, 0.5, 1.0] * 4
        image.filepath_raw = str(root / "T_SharedSteel_Albedo.png")
        image.file_format = "PNG"
        image.save()
        texture = material.node_tree.nodes.new("ShaderNodeTexImage")
        texture.image = image
        material.node_tree.links.new(
            texture.outputs["Color"],
            material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"],
        )
        model.data.materials.append(material)
        bpy.ops.export_scene.fbx(
            filepath=str(root / f"{name}.fbx"),
            use_selection=True,
            object_types={"MESH"},
            bake_anim=False,
            add_leaf_bones=False,
            path_mode="RELATIVE",
            axis_forward="-Z",
            axis_up="Y",
        )


def verify(archive: Path, output: Path) -> None:
    """Import only the ZIP's exact closed inventory into fresh Blender scenes and check external images."""
    expected = {
        "Helmet.fbx",
        "Armour.fbx",
        "T_SharedSteel_Albedo.png",
        "README_EquipmentSet.txt",
        "INVENTORY.md",
    }
    output.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        if len(names) != len(expected) or set(names) != {f"EquipmentSet_fbx/{n}" for n in expected}:
            raise ValueError("Portable equipment archive has an unexpected or duplicate member")
        for entry in package.infolist():
            if entry.file_size > 10_000_000:
                raise ValueError("Fixture archive exceeds its bounded import size")
            (output / Path(entry.filename).name).write_bytes(package.read(entry))
    results = []
    for name in ("Helmet", "Armour"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(output / f"{name}.fbx"))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        if len(meshes) != 1 or len(meshes[0].data.vertices) < 8 or not meshes[0].data.materials:
            raise ValueError(f"{name}: missing geometry or material after clean reimport")
        images = [
            node.image
            for material in meshes[0].data.materials
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE"
        ]
        if not images or any(image is None or not image.has_data for image in images):
            raise ValueError(f"{name}: shared texture failed to resolve from archive")
        expected_texture = (output / "T_SharedSteel_Albedo.png").resolve()
        if any(
            Path(bpy.path.abspath(image.filepath)).resolve() != expected_texture for image in images
        ):
            raise ValueError(f"{name}: reimport resolved a texture outside the extracted archive")
        results.append({"item": name, "vertices": len(meshes[0].data.vertices), "passed": True})
    (output / "reimport-result.json").write_text(
        json.dumps({"passed": True, "items": results, "findings": []}, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1 :]
    if len(arguments) == 2 and arguments[0] == "generate":
        generate(Path(arguments[1]).resolve())
    elif len(arguments) == 3 and arguments[0] == "verify":
        verify(Path(arguments[1]).resolve(), Path(arguments[2]).resolve())
    else:
        raise ValueError(
            "Expected generate <source-directory> or verify <archive> <new-import-directory>"
        )
