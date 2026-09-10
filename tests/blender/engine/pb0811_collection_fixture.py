"""Original CC0 twelve-item fixture and independent portable geometry/reimport oracle."""

from __future__ import annotations

import hashlib
import json
import sys
import tarfile
import zipfile
from pathlib import Path

import bpy


def generate(root: Path) -> None:
    """Create distinct low-poly columns; manifest order deliberately differs from filename order."""
    source = root / "source"
    source.mkdir(parents=True, exist_ok=True)
    items = []
    for number in (12, 1, 10, 2, 11, 3, 9, 4, 8, 5, 7, 6):
        name = f"Column{number:02}"
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=number + 4, radius=0.1 + number * 0.01, depth=0.4 + number * 0.02
        )
        model = bpy.context.object
        model.name = name
        material = bpy.data.materials.new("SharedStone")
        material.use_nodes = True
        image = bpy.data.images.new("SharedStone", width=2, height=2)
        image.pixels = [0.4, 0.45, 0.5, 1.0] * 4
        image.filepath_raw = str(source / "T_SharedStone_Albedo.png")
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
            filepath=str(source / f"{name}.fbx"),
            use_selection=True,
            object_types={"MESH"},
            bake_anim=False,
            add_leaf_bones=False,
            path_mode="RELATIVE",
            axis_forward="-Z",
            axis_up="Y",
        )
        items.append({"id": name, "triangles": 4 * (number + 4) - 4})
    (root / "expectations.json").write_text(
        json.dumps({"items": items}, indent=2) + "\n", encoding="utf-8"
    )


def verify(archive: Path, output: Path, expectations: Path) -> None:
    """Reject incomplete archives, then reimport each actual FBX with only packaged textures."""
    items = json.loads(expectations.read_text(encoding="utf-8"))["items"]
    names = {f"{item['id']}.fbx" for item in items} | {
        "T_SharedStone_Albedo.png",
        "README_TwelveColumns.txt",
        "INVENTORY.md",
    }
    output.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(archive) as package:
        if len(package.namelist()) != 15 or set(package.namelist()) != {
            f"TwelveColumns_fbx/{name}" for name in names
        }:
            raise ValueError("Unexpected, duplicate or missing collection archive member")
        for entry in package.infolist():
            if entry.file_size > 1_048_576:
                raise ValueError("Collection fixture exceeds bounded extraction size")
            (output / Path(entry.filename).name).write_bytes(package.read(entry))
    results = []
    for item in items:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path = output / f"{item['id']}.fbx"
        bpy.ops.import_scene.fbx(filepath=str(path))
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        if len(meshes) != 1:
            raise ValueError("Collection item must retain exactly one mesh")
        mesh = meshes[0].data
        mesh.calc_loop_triangles()
        if len(mesh.loop_triangles) != item["triangles"]:
            raise ValueError(f"{item['id']}: incorrect triangle inventory")
        images = [
            node.image
            for material in mesh.materials
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE"
        ]
        if (
            len(mesh.materials) != 1
            or not images
            or any(
                image is None
                or not image.has_data
                or Path(bpy.path.abspath(image.filepath)).resolve()
                != (output / "T_SharedStone_Albedo.png").resolve()
                for image in images
            )
        ):
            raise ValueError("Collection material or contained shared texture is missing")
        results.append(
            {
                **item,
                "vertices": len(mesh.vertices),
                "dimensions": {
                    "x": meshes[0].dimensions.x,
                    "y": meshes[0].dimensions.z,
                    "z": meshes[0].dimensions.y,
                },
                "materials": len(mesh.materials),
                "textures": len({image.name for image in images}),
                "passed": True,
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                "bytes": path.stat().st_size,
            }
        )
    (output / "reimport-result.json").write_text(
        json.dumps({"passed": True, "items": results, "findings": []}, indent=2) + "\n",
        encoding="utf-8",
    )


def verify_unity(archive_path: Path, inventory: Path, report: Path) -> None:
    """Read actual package records without extracting them; require the exact export plan."""
    expected = set(inventory.read_text(encoding="utf-8-sig").splitlines())
    with tarfile.open(archive_path, "r:gz") as archive:
        paths = []
        for entry in archive.getmembers():
            if entry.name.endswith("/pathname"):
                if not entry.isfile() or entry.size > 1024:
                    raise ValueError("Invalid bounded package pathname record")
                with archive.extractfile(entry) as stream:
                    paths.append(stream.read().decode("utf-8").strip())
    if (
        len(paths) != len(set(paths))
        or set(paths) != expected
        or any(
            path != "Assets/PBTwelveTests" and not path.startswith("Assets/PBTwelveTests/")
            for path in paths
        )
        or sum(path.endswith(".prefab") for path in paths) != 12
    ):
        raise ValueError("Unity collection archive differs from its exact twelve-item plan")
    report.write_text(
        json.dumps(
            {"passed": True, "assetRecords": len(paths), "prefabs": 12, "paths": sorted(paths)},
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1 :]
    if len(args) == 2 and args[0] == "generate":
        generate(Path(args[1]).resolve())
    elif len(args) == 4 and args[0] == "verify":
        verify(*(Path(value).resolve() for value in args[1:]))
    elif len(args) == 4 and args[0] == "verify-unity":
        verify_unity(*(Path(value).resolve() for value in args[1:]))
    else:
        raise ValueError("Expected generate <fixture> or verify <archive> <output> <expectations>")
