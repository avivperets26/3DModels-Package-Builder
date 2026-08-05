"""PB-0406 texture and material-connection inspection tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.inspection_common import (  # noqa: E402
    finite_components,
    matrix_components,
    required_name,
    safe_filename,
)
from package_builder_blender.texture_inspection import inspect_textures  # noqa: E402


class _PackedFile:
    def __init__(self, size: int) -> None:
        self.size = size


class _PackedEntry:
    def __init__(self, size: int) -> None:
        self.packed_file = _PackedFile(size)


class _ColorSpace:
    def __init__(self, name: str) -> None:
        self.name = name


class _Image:
    def __init__(
        self,
        name: str,
        *,
        size: tuple[int, int] = (2048, 1024),
        file_format: str = "PNG",
        color_space: str = "sRGB",
        source: str = "FILE",
        filepath: str = "C:/private/source/texture.png",
        packed_sizes: tuple[int, ...] = (),
    ) -> None:
        self.name = name
        self.size = size
        self.file_format = file_format
        self.colorspace_settings = _ColorSpace(color_space)
        self.source = source
        self.filepath = filepath
        self.packed_files = tuple(_PackedEntry(value) for value in packed_sizes)
        self.packed_file = None


class _Socket:
    def __init__(self, name: str) -> None:
        self.name = name
        self.links: list[_Link] = []


class _Link:
    def __init__(self, to_node: _Node, to_socket: _Socket) -> None:
        self.to_node = to_node
        self.to_socket = to_socket


class _Node:
    def __init__(
        self,
        name: str,
        node_type: str,
        *,
        image: _Image | None = None,
        label: str = "",
        outputs: tuple[_Socket, ...] = (),
    ) -> None:
        self.name = name
        self.type = node_type
        self.image = image
        self.label = label
        self.outputs = outputs


class _NodeTree:
    def __init__(self, *nodes: _Node) -> None:
        self.nodes = nodes


class _Material:
    def __init__(self, name: str, *nodes: _Node, use_nodes: bool = True) -> None:
        self.name = name
        self.use_nodes = use_nodes
        self.node_tree = _NodeTree(*nodes)


def _connect(output: _Socket, node: _Node, socket_name: str) -> None:
    output.links.append(_Link(node, _Socket(socket_name)))


class _Unreadable:
    def __iter__(self):
        raise RuntimeError("private source path")


class TextureInspectionTests(unittest.TestCase):
    def test_reports_packed_and_external_images_without_full_paths(self) -> None:
        packed = _Image("Embedded", packed_sizes=(120, 80), filepath="//Embedded.png")
        external = _Image(
            "External",
            color_space="Non-Color",
            filepath="C:\\secret\\Metallic.png",
        )

        result = inspect_textures((external, packed), ())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            ("Embedded", "External"), tuple(item.name for item in result.report.images)
        )
        self.assertEqual(
            (1, 1), (result.report.packed_image_count, result.report.external_image_count)
        )
        self.assertEqual(200, result.report.images[0].packed_byte_count)
        self.assertEqual("Metallic.png", result.report.images[1].source_filename)
        self.assertEqual("linear", result.report.images[1].color_space_kind)
        self.assertNotIn("secret", repr(result.report))

    def test_reports_material_connection_and_probable_albedo_role(self) -> None:
        image = _Image("Bow_Albedo")
        output = _Socket("Color")
        image_node = _Node("Albedo Texture", "TEX_IMAGE", image=image, outputs=(output,))
        shader = _Node("Principled BSDF", "BSDF_PRINCIPLED")
        _connect(output, shader, "Base Color")

        result = inspect_textures((image,), (_Material("Bow", image_node, shader),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        connection = result.report.connections[0]
        self.assertEqual("albedo", connection.probable_role)
        self.assertEqual("material_connection", connection.role_basis)
        self.assertEqual(1, result.report.images[0].material_connection_count)

    def test_traverses_normal_map_connection(self) -> None:
        image = _Image("Detail")
        color = _Socket("Color")
        normal_output = _Socket("Normal")
        texture = _Node("Texture", "TEX_IMAGE", image=image, outputs=(color,))
        normal_map = _Node("Normal Map", "NORMAL_MAP", outputs=(normal_output,))
        shader = _Node("Principled BSDF", "BSDF_PRINCIPLED")
        _connect(color, normal_map, "Color")
        _connect(normal_output, shader, "Normal")

        result = inspect_textures((image,), (_Material("Bow", texture, normal_map, shader),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("normal", result.report.connections[0].probable_role)
        self.assertEqual(2, len(result.report.connections[0].destinations))

    def test_uses_name_hint_only_when_no_connection_role_exists(self) -> None:
        image = _Image("Bow_Roughness")
        texture = _Node("Texture", "TEX_IMAGE", image=image, outputs=(_Socket("Color"),))

        result = inspect_textures((image,), (_Material("Bow", texture),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("roughness", result.report.connections[0].probable_role)
        self.assertEqual("name_hint", result.report.connections[0].role_basis)

    def test_ambiguous_role_is_reported_without_guessing(self) -> None:
        image = _Image("Packed")
        output = _Socket("Color")
        texture = _Node("Texture", "TEX_IMAGE", image=image, outputs=(output,))
        shader = _Node("Shader", "BSDF_PRINCIPLED")
        _connect(output, shader, "Metallic Roughness")

        result = inspect_textures((image,), (_Material("Bow", texture, shader),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("ambiguous", result.report.connections[0].probable_role)

    def test_ambiguous_name_hint_is_reported_without_guessing(self) -> None:
        image = _Image("Metallic_Roughness")
        texture = _Node("Texture", "TEX_IMAGE", image=image)

        result = inspect_textures((image,), (_Material("Bow", texture),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("ambiguous", result.report.connections[0].probable_role)
        self.assertEqual("name_hint", result.report.connections[0].role_basis)

    def test_empty_inventory_is_a_valid_texture_free_scene(self) -> None:
        result = inspect_textures((), ())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(0, result.report.image_count)

    def test_disabled_material_nodes_are_not_reported_as_connections(self) -> None:
        image = _Image("Unused")
        texture = _Node("Texture", "TEX_IMAGE", image=image)

        result = inspect_textures((image,), (_Material("Bow", texture, use_nodes=False),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(0, len(result.report.connections))

    def test_unassigned_image_texture_node_is_ignored(self) -> None:
        result = inspect_textures((), (_Material("Bow", _Node("Empty", "TEX_IMAGE")),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(0, len(result.report.connections))

    def test_reports_custom_color_space_and_generated_source_without_guessing(self) -> None:
        image = _Image("Generated", color_space="ACEScg", source="GENERATED", filepath="")

        result = inspect_textures((image,), ())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        report = result.report.images[0]
        self.assertEqual(
            ("other", "generated", None),
            (
                report.color_space_kind,
                report.source_kind,
                report.source_filename,
            ),
        )

    def test_duplicate_images_and_invalid_dimensions_fail_closed(self) -> None:
        duplicate = inspect_textures((_Image("Same"), _Image("Same")), ())
        dimensions = inspect_textures((_Image("Bad", size=(0, 12)),), ())

        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", duplicate.findings[0].code)
        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", dimensions.findings[0].code)

    def test_connection_to_unlisted_image_fails_closed(self) -> None:
        image = _Image("Missing")
        texture = _Node("Texture", "TEX_IMAGE", image=image, outputs=())

        result = inspect_textures((), (_Material("Bow", texture),))

        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", result.findings[0].code)

    def test_invalid_packed_file_size_fails_closed(self) -> None:
        result = inspect_textures((_Image("Packed", packed_sizes=(0,)),), ())

        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", result.findings[0].code)

    def test_duplicate_material_label_and_bounded_graph_paths(self) -> None:
        duplicate = inspect_textures((), (_Material("Same"), _Material("Same")))
        image = _Image("Image")
        output = _Socket("Color")
        texture = _Node("Texture", "TEX_IMAGE", image=image, outputs=(output,))
        target = _Node("Target", "MIX", label="Role helper")
        for _ in range(513):
            _connect(output, target, "Value")
        bounded = inspect_textures((image,), (_Material("Bow", texture, target),))

        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", duplicate.findings[0].code)
        self.assertEqual("BLENDER_TEXTURE_DATA_INVALID", bounded.findings[0].code)

    def test_common_inspection_guards_cover_invalid_shape_and_filename(self) -> None:
        with self.assertRaises(ValueError):
            required_name("")
        with self.assertRaises(ValueError):
            finite_components((1.0,), 2)
        with self.assertRaises(ValueError):
            matrix_components(((1.0,),))
        self.assertIsNone(safe_filename(None))
        with self.assertRaises(ValueError):
            safe_filename("..")

    def test_unreadable_inputs_are_sanitized(self) -> None:
        result = inspect_textures(_Unreadable(), ())

        self.assertEqual("BLENDER_TEXTURE_INPUT_INVALID", result.findings[0].code)
        self.assertNotIn("private", result.findings[0].explanation)
        self.assertTrue(result.findings[0].as_protocol_value()["blocksRelease"])


if __name__ == "__main__":
    unittest.main()
