"""Focused PB-0405 geometry-inspection acceptance and failure-path tests."""

from __future__ import annotations

import math
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.geometry_inspection import inspect_geometry  # noqa: E402


class _Matrix:
    def __init__(
        self,
        translation: tuple[float, float, float] = (0.0, 0.0, 0.0),
        rotation: tuple[float, float, float, float] = (1.0, 0.0, 0.0, 0.0),
        scale: tuple[float, float, float] = (1.0, 1.0, 1.0),
    ) -> None:
        self.translation = translation
        self.rotation = rotation
        self.scale = scale

    def decompose(self) -> tuple[tuple[float, ...], ...]:
        return self.translation, self.rotation, self.scale

    def __matmul__(self, value: tuple[float, float, float]) -> tuple[float, ...]:
        return tuple(
            value[index] * self.scale[index] + self.translation[index] for index in range(3)
        )


class _UvLayer:
    def __init__(self, name: str, value_count: int, active_render: bool = False) -> None:
        self.name = name
        self.data = [object()] * value_count
        self.active_render = active_render


class _Polygon:
    def __init__(self, *vertices: int) -> None:
        self.vertices = vertices


class _CornerNormal:
    def __init__(self, vector: tuple[float, float, float]) -> None:
        self.vector = vector


class _Loop:
    def __init__(
        self,
        tangent: tuple[float, float, float],
        bitangent_sign: float = 1.0,
    ) -> None:
        self.tangent = tangent
        self.bitangent_sign = bitangent_sign


class _Mesh:
    def __init__(
        self,
        vertex_count: int = 4,
        polygons: tuple[_Polygon, ...] = (_Polygon(0, 1, 2, 3),),
        uv_layers: tuple[_UvLayer, ...] = (_UvLayer("UVMap", 4, True),),
        normals: tuple[_CornerNormal, ...] = tuple(
            _CornerNormal((0.0, 0.0, 1.0)) for _ in range(4)
        ),
        loops: tuple[_Loop, ...] = tuple(_Loop((1.0, 0.0, 0.0)) for _ in range(4)),
        tangent_failure: bool = False,
        cleanup_failure: bool = False,
    ) -> None:
        self.vertices = [object()] * vertex_count
        self.polygons = polygons
        self.uv_layers = uv_layers
        self.corner_normals = normals
        self.loops = loops
        self.loop_triangles: list[object] = []
        self.tangent_failure = tangent_failure
        self.cleanup_failure = cleanup_failure
        self.tangent_uvmap: str | None = None
        self.tangent_free_count = 0

    def calc_loop_triangles(self) -> None:
        triangle_count = sum(len(polygon.vertices) - 2 for polygon in self.polygons)
        self.loop_triangles = [object()] * triangle_count

    def calc_tangents(self, *, uvmap: str) -> None:
        self.tangent_uvmap = uvmap
        if self.tangent_failure:
            raise RuntimeError("private Blender tangent detail")

    def free_tangents(self) -> None:
        self.tangent_free_count += 1
        if self.cleanup_failure:
            raise RuntimeError("private Blender cleanup detail")


class _MaterialSlot:
    def __init__(self, name: str) -> None:
        self.name = name


_UNIT_BOUNDS = tuple((x, y, z) for x in (-1.0, 1.0) for y in (-1.0, 1.0) for z in (-1.0, 1.0))


class _Object:
    def __init__(
        self,
        name: str,
        object_type: str = "MESH",
        mesh: _Mesh | None = None,
        matrix: _Matrix | None = None,
        bounds: tuple[tuple[float, float, float], ...] = _UNIT_BOUNDS,
        materials: tuple[str, ...] = ("Body",),
    ) -> None:
        self.name = name
        self.type = object_type
        self.data = mesh
        self.matrix_world = matrix or _Matrix()
        self.bound_box = bounds
        self.material_slots = tuple(_MaterialSlot(value) for value in materials)


class _UnreadableObjects:
    def __iter__(self):
        raise RuntimeError("private path and implementation detail")


class GeometryInspectionTests(unittest.TestCase):
    def test_reports_complete_geometry_transform_and_shading_facts(self) -> None:
        mesh = _Mesh(
            uv_layers=(
                _UvLayer("Preview", 4),
                _UvLayer("Game", 4, True),
            )
        )
        subject = _Object(
            "Bow",
            mesh=mesh,
            matrix=_Matrix((2.0, 3.0, 4.0), scale=(2.0, 1.0, 0.5)),
            materials=("Bow", "String"),
        )

        result = inspect_geometry((subject,))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            (1, 1, 4, 1, 2),
            (
                result.report.object_count,
                result.report.mesh_count,
                result.report.vertex_count,
                result.report.polygon_count,
                result.report.triangle_count,
            ),
        )
        translation = result.report.objects[0].transform.translation
        self.assertEqual((2.0, 3.0, 4.0), (translation.x, translation.y, translation.z))
        report = result.report.meshes[0]
        self.assertEqual(("Preview", "Game"), tuple(layer.name for layer in report.uv_layers))
        self.assertEqual((4, 4), tuple(layer.value_count for layer in report.uv_layers))
        self.assertEqual(4, report.corner_normal_count)
        self.assertEqual(4, report.tangent_count)
        self.assertEqual(("Bow", "String"), report.material_slot_names)
        self.assertEqual("uint16", report.required_index_format)
        self.assertEqual(
            (-0.0, 2.0, 3.5),
            (
                report.world_bounds.minimum.x,
                report.world_bounds.minimum.y,
                report.world_bounds.minimum.z,
            ),
        )
        self.assertEqual(
            (4.0, 4.0, 4.5),
            (
                report.world_bounds.maximum.x,
                report.world_bounds.maximum.y,
                report.world_bounds.maximum.z,
            ),
        )
        self.assertEqual("Game", mesh.tangent_uvmap)
        self.assertEqual(1, mesh.tangent_free_count)

    def test_sorts_objects_and_aggregates_multiple_world_bounds(self) -> None:
        result = inspect_geometry(
            (
                _Object("Zeta", mesh=_Mesh(), matrix=_Matrix((10.0, 0.0, 0.0))),
                _Object("Camera", object_type="CAMERA"),
                _Object("Alpha", mesh=_Mesh(), matrix=_Matrix((-5.0, 0.0, 0.0))),
            )
        )

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            ("Alpha", "Camera", "Zeta"), tuple(item.name for item in result.report.objects)
        )
        self.assertEqual(
            ("Alpha", "Zeta"), tuple(item.object_name for item in result.report.meshes)
        )
        self.assertEqual(
            (-6.0, -1.0, -1.0),
            (
                result.report.world_bounds.minimum.x,
                result.report.world_bounds.minimum.y,
                result.report.world_bounds.minimum.z,
            ),
        )
        self.assertEqual(
            (11.0, 1.0, 1.0),
            (
                result.report.world_bounds.maximum.x,
                result.report.world_bounds.maximum.y,
                result.report.world_bounds.maximum.z,
            ),
        )

    def test_hosted_blender_bounds_use_mathutils_vectors(self) -> None:
        converted: list[tuple[float, ...]] = []
        mathutils = types.ModuleType("mathutils")

        def vector(value: tuple[float, ...]) -> tuple[float, ...]:
            converted.append(value)
            return value

        mathutils.Vector = vector  # type: ignore[attr-defined]

        with patch.dict(sys.modules, {"mathutils": mathutils}):
            result = inspect_geometry((_Object("Hosted", mesh=_Mesh()),))

        self.assertTrue(result.succeeded)
        self.assertEqual(8, len(converted))

    def test_large_vertex_buffer_requires_uint32_indices(self) -> None:
        mesh = _Mesh(vertex_count=65537, polygons=(_Polygon(0, 1, 2),))

        result = inspect_geometry((_Object("Large", mesh=mesh),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("uint32", result.report.meshes[0].required_index_format)

    def test_mesh_without_uv_reports_zero_tangents(self) -> None:
        mesh = _Mesh(uv_layers=(), loops=())

        result = inspect_geometry((_Object("NoUv", mesh=mesh),))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(0, result.report.meshes[0].tangent_count)
        self.assertIsNone(mesh.tangent_uvmap)
        self.assertEqual(0, mesh.tangent_free_count)

    def test_empty_scene_and_nonmesh_scene_return_stable_findings(self) -> None:
        empty = inspect_geometry(())
        nonmesh = inspect_geometry((_Object("Camera", object_type="CAMERA"),))

        self.assertEqual("BLENDER_GEOMETRY_INPUT_INVALID", empty.findings[0].code)
        self.assertEqual("BLENDER_GEOMETRY_MESH_MISSING", nonmesh.findings[0].code)
        self.assertTrue(empty.findings[0].as_protocol_value()["blocksRelease"])

    def test_invalid_topology_and_duplicate_names_are_sanitized(self) -> None:
        invalid = inspect_geometry((_Object("Broken", mesh=_Mesh(polygons=(_Polygon(0, 1, 8),))),))
        duplicate = inspect_geometry((_Object("Same", mesh=_Mesh()), _Object("Same", mesh=_Mesh())))

        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", invalid.findings[0].code)
        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", duplicate.findings[0].code)
        self.assertNotIn("8", invalid.findings[0].explanation)

    def test_nonfinite_transform_and_bounds_are_rejected(self) -> None:
        transform = inspect_geometry(
            (_Object("BadTransform", mesh=_Mesh(), matrix=_Matrix((math.nan, 0.0, 0.0))),)
        )
        bounds = inspect_geometry(
            (_Object("BadBounds", mesh=_Mesh(), bounds=((math.inf, 0.0, 0.0),) * 8),)
        )

        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", transform.findings[0].code)
        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", bounds.findings[0].code)

    def test_tangent_failures_are_sanitized_and_cleanup_runs_after_reading(self) -> None:
        calculation_mesh = _Mesh(tangent_failure=True)
        cleanup_mesh = _Mesh(cleanup_failure=True)

        calculation = inspect_geometry((_Object("Calculation", mesh=calculation_mesh),))
        cleanup = inspect_geometry((_Object("Cleanup", mesh=cleanup_mesh),))

        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", calculation.findings[0].code)
        self.assertEqual(0, calculation_mesh.tangent_free_count)
        self.assertEqual("BLENDER_GEOMETRY_DATA_INVALID", cleanup.findings[0].code)
        self.assertEqual(1, cleanup_mesh.tangent_free_count)
        self.assertNotIn("private", cleanup.findings[0].explanation)

    def test_unreadable_input_is_sanitized(self) -> None:
        result = inspect_geometry(_UnreadableObjects())

        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_GEOMETRY_INPUT_INVALID", result.findings[0].code)
        self.assertNotIn("private", result.findings[0].explanation)


if __name__ == "__main__":
    unittest.main()
