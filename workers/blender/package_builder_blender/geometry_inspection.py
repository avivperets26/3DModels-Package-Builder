"""Deterministic direct-data geometry and transform inspection for Blender scenes."""

from __future__ import annotations

import math
from collections.abc import Iterable
from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True, slots=True)
class Vector3Report:
    """Finite three-component value copied out of Blender-owned data."""

    x: float
    y: float
    z: float


@dataclass(frozen=True, slots=True)
class QuaternionReport:
    """Finite world-rotation quaternion in Blender's canonical WXYZ component order."""

    w: float
    x: float
    y: float
    z: float


@dataclass(frozen=True, slots=True)
class TransformReport:
    """World transform decomposed without observing Blender UI context."""

    translation: Vector3Report
    rotation: QuaternionReport
    scale: Vector3Report


@dataclass(frozen=True, slots=True)
class BoundsReport:
    """Axis-aligned world bounds and dimensions derived from all eight transformed corners."""

    minimum: Vector3Report
    maximum: Vector3Report
    dimensions: Vector3Report


@dataclass(frozen=True, slots=True)
class UvLayerReport:
    """One Blender UV layer and its exact face-corner value count."""

    name: str
    value_count: int
    active_render: bool


@dataclass(frozen=True, slots=True)
class MeshGeometryReport:
    """Measured mesh topology and shading inputs for one scene object."""

    object_name: str
    vertex_count: int
    polygon_count: int
    triangle_count: int
    uv_layers: tuple[UvLayerReport, ...]
    corner_normal_count: int
    tangent_count: int
    material_slot_names: tuple[str, ...]
    required_index_format: str
    world_bounds: BoundsReport


@dataclass(frozen=True, slots=True)
class ObjectGeometryReport:
    """Deterministic object identity, type, and world transform."""

    name: str
    object_type: str
    transform: TransformReport


@dataclass(frozen=True, slots=True)
class GeometryInspectionReport:
    """Aggregate and per-object geometry facts for one imported scene."""

    objects: tuple[ObjectGeometryReport, ...]
    meshes: tuple[MeshGeometryReport, ...]
    object_count: int
    mesh_count: int
    vertex_count: int
    polygon_count: int
    triangle_count: int
    world_bounds: BoundsReport


@dataclass(frozen=True, slots=True)
class GeometryInspectionFinding:
    """Stable sanitized failure compatible with the shared validation protocol."""

    code: str
    explanation: str
    suggested_action: str

    def as_protocol_value(self) -> dict[str, Any]:
        """Return the PB-0109-compatible blocking finding representation."""

        return {
            "code": self.code,
            "severity": "error",
            "explanation": self.explanation,
            "source": "blender-geometry-inspector",
            "suggestedAction": self.suggested_action,
            "blocksRelease": True,
        }


@dataclass(frozen=True, slots=True)
class GeometryInspectionResult:
    """Non-throwing expected result of inspecting one Blender scene."""

    report: GeometryInspectionReport | None
    findings: tuple[GeometryInspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether a complete internally consistent report was produced."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, suggested_action: str) -> GeometryInspectionFinding:
    return GeometryInspectionFinding(code, explanation, suggested_action)


def _failure(finding: GeometryInspectionFinding) -> GeometryInspectionResult:
    return GeometryInspectionResult(None, (finding,))


def _finite_components(value: Any, length: int) -> tuple[float, ...]:
    components = tuple(value)
    if (
        len(components) != length
        or any(type(component) not in {float, int} for component in components)
        or any(not math.isfinite(component) for component in components)
    ):
        raise ValueError
    return tuple(float(component) for component in components)


def _vector(value: Any) -> Vector3Report:
    x, y, z = _finite_components(value, 3)
    return Vector3Report(x, y, z)


def _quaternion(value: Any) -> QuaternionReport:
    w, x, y, z = _finite_components(value, 4)
    return QuaternionReport(w, x, y, z)


def _inspect_transform(matrix: Any) -> TransformReport:
    translation, rotation, scale = matrix.decompose()
    return TransformReport(_vector(translation), _quaternion(rotation), _vector(scale))


def _bounds(points: tuple[Vector3Report, ...]) -> BoundsReport:
    if not points:
        raise ValueError
    minimum = Vector3Report(
        min(point.x for point in points),
        min(point.y for point in points),
        min(point.z for point in points),
    )
    maximum = Vector3Report(
        max(point.x for point in points),
        max(point.y for point in points),
        max(point.z for point in points),
    )
    return BoundsReport(
        minimum,
        maximum,
        Vector3Report(
            maximum.x - minimum.x,
            maximum.y - minimum.y,
            maximum.z - minimum.z,
        ),
    )


def _transform_point(matrix: Any, value: Any) -> Vector3Report:
    """Use Blender's Vector type when hosted, while keeping plain-Python tests dependency-free."""

    components = _finite_components(value, 3)
    try:
        from mathutils import Vector
    except ImportError:
        transformed = matrix @ components
    else:
        transformed = matrix @ Vector(components)
    return _vector(transformed)


def _world_bounds(value: Any, matrix: Any) -> BoundsReport:
    corners = tuple(value.bound_box)
    if len(corners) != 8 or all(tuple(corner) == (-1.0, -1.0, -1.0) for corner in corners):
        raise ValueError
    return _bounds(tuple(_transform_point(matrix, corner) for corner in corners))


def _uv_layers(mesh: Any) -> tuple[UvLayerReport, ...]:
    reports: list[UvLayerReport] = []
    names: set[str] = set()
    for layer in tuple(mesh.uv_layers):
        name = layer.name
        if (
            not isinstance(name, str)
            or not name
            or any(ord(character) < 32 for character in name)
            or name in names
        ):
            raise ValueError
        names.add(name)
        reports.append(UvLayerReport(name, len(layer.data), bool(layer.active_render)))
    return tuple(reports)


def _material_slot_names(value: Any) -> tuple[str, ...]:
    names: list[str] = []
    for slot in tuple(value.material_slots):
        name = slot.name
        if not isinstance(name, str) or any(ord(character) < 32 for character in name):
            raise ValueError
        names.append(name)
    return tuple(names)


def _topology(mesh: Any) -> tuple[int, int, int, str]:
    vertex_count = len(mesh.vertices)
    polygons = tuple(mesh.polygons)
    if vertex_count <= 0 or not polygons:
        raise ValueError
    maximum_index = -1
    for polygon in polygons:
        indices = tuple(polygon.vertices)
        if len(indices) < 3:
            raise ValueError
        for index in indices:
            if type(index) is not int or index < 0 or index >= vertex_count:
                raise ValueError
            maximum_index = max(maximum_index, index)
    mesh.calc_loop_triangles()
    triangle_count = len(mesh.loop_triangles)
    if triangle_count <= 0:
        raise ValueError
    return (
        vertex_count,
        len(polygons),
        triangle_count,
        "uint16" if vertex_count <= 65536 and maximum_index <= 65535 else "uint32",
    )


def _normal_count(mesh: Any) -> int:
    normals = tuple(mesh.corner_normals)
    for normal in normals:
        _vector(normal.vector)
    return len(normals)


def _tangent_count(mesh: Any, uv_layers: tuple[UvLayerReport, ...]) -> int:
    if not uv_layers:
        return 0
    render_layer = next((layer for layer in uv_layers if layer.active_render), uv_layers[0])
    mesh.calc_tangents(uvmap=render_layer.name)
    try:
        loops = tuple(mesh.loops)
        for loop in loops:
            _vector(loop.tangent)
            bitangent_sign = loop.bitangent_sign
            if type(bitangent_sign) not in {float, int} or not math.isfinite(bitangent_sign):
                raise ValueError
        return len(loops)
    finally:
        mesh.free_tangents()


def _inspect_mesh(value: Any, matrix: Any, name: str) -> MeshGeometryReport:
    mesh = value.data
    vertex_count, polygon_count, triangle_count, index_format = _topology(mesh)
    layers = _uv_layers(mesh)
    normal_count = _normal_count(mesh)
    tangent_count = _tangent_count(mesh, layers)
    return MeshGeometryReport(
        name,
        vertex_count,
        polygon_count,
        triangle_count,
        layers,
        normal_count,
        tangent_count,
        _material_slot_names(value),
        index_format,
        _world_bounds(value, matrix),
    )


def inspect_geometry(objects: Iterable[Any]) -> GeometryInspectionResult:
    """Inspect imported objects through direct Blender data with no UI or selection dependency."""

    try:
        source_objects = tuple(objects)
    except Exception:
        return _failure(
            _finding(
                "BLENDER_GEOMETRY_INPUT_INVALID",
                "Blender scene objects could not be enumerated safely.",
                "Discard the workspace and retry inspection in a new Blender process.",
            )
        )
    if not source_objects:
        return _failure(
            _finding(
                "BLENDER_GEOMETRY_INPUT_INVALID",
                "The Blender scene contains no objects to inspect.",
                "Import a supported model before running geometry inspection.",
            )
        )

    object_reports: list[ObjectGeometryReport] = []
    mesh_reports: list[MeshGeometryReport] = []
    object_names: set[str] = set()
    try:
        for value in source_objects:
            name = value.name
            object_type = value.type
            matrix = value.matrix_world
            if (
                not isinstance(name, str)
                or not name
                or any(ord(character) < 32 for character in name)
                or name in object_names
                or not isinstance(object_type, str)
                or not object_type
                or any(ord(character) < 32 for character in object_type)
            ):
                raise ValueError
            object_names.add(name)
            object_reports.append(
                ObjectGeometryReport(name, object_type, _inspect_transform(matrix))
            )
            if object_type == "MESH":
                mesh_reports.append(_inspect_mesh(value, matrix, name))
    except Exception:
        return _failure(
            _finding(
                "BLENDER_GEOMETRY_DATA_INVALID",
                "Blender returned incomplete, inconsistent, or non-finite geometry data.",
                "Review the retained worker log, repair the source geometry, and retry.",
            )
        )
    if not mesh_reports:
        return _failure(
            _finding(
                "BLENDER_GEOMETRY_MESH_MISSING",
                "The imported Blender scene contains no mesh objects.",
                "Provide a model containing at least one usable mesh.",
            )
        )

    ordered_objects = tuple(sorted(object_reports, key=lambda report: report.name))
    ordered_meshes = tuple(sorted(mesh_reports, key=lambda report: report.object_name))
    aggregate_points = tuple(
        point
        for mesh in ordered_meshes
        for point in (mesh.world_bounds.minimum, mesh.world_bounds.maximum)
    )
    return GeometryInspectionResult(
        GeometryInspectionReport(
            ordered_objects,
            ordered_meshes,
            len(ordered_objects),
            len(ordered_meshes),
            sum(mesh.vertex_count for mesh in ordered_meshes),
            sum(mesh.polygon_count for mesh in ordered_meshes),
            sum(mesh.triangle_count for mesh in ordered_meshes),
            _bounds(aggregate_points),
        ),
        (),
    )
