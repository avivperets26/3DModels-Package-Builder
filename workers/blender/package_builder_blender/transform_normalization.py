"""Transactional unit, axis, pivot, and object-transform normalization for Blender."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass
from typing import Any

from .inspection_common import (
    InspectionFinding,
    finite_components,
    finite_number,
    matrix_components,
)

KEEP = "keep"
BOUNDS_CENTER = "bounds-center"
BOUNDS_BASE = "bounds-base"
_PIVOTS = {KEEP, BOUNDS_CENTER, BOUNDS_BASE}
_AXES = {"X", "Y", "Z", "-X", "-Y", "-Z"}


@dataclass(frozen=True, slots=True)
class TransformNormalizationPlan:
    """Explicit source/target coordinate and display-unit policy."""

    source_forward: str
    source_up: str
    target_forward: str
    target_up: str
    source_scale_length: float
    target_scale_length: float
    target_unit_system: str
    pivot_policy: str


@dataclass(frozen=True, slots=True)
class TransformMetrics:
    """World-space bounds and object counts measured before or after normalization."""

    object_count: int
    mesh_count: int
    minimum: tuple[float, float, float]
    maximum: tuple[float, float, float]
    dimensions: tuple[float, float, float]


@dataclass(frozen=True, slots=True)
class TransformNormalizationReport:
    """Applied coordinate policy and its deterministic before/after metrics."""

    plan: TransformNormalizationPlan
    unit_factor: float
    pivot_translation: tuple[float, float, float]
    before: TransformMetrics
    after: TransformMetrics
    deformation_preserved: bool


@dataclass(frozen=True, slots=True)
class TransformNormalizationResult:
    """Non-throwing expected result of one normalization transaction."""

    report: TransformNormalizationReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether conversion completed and deformation data stayed identical."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-transform-normalizer")


def _axis_vector(token: str) -> tuple[float, float, float]:
    sign = -1.0 if token.startswith("-") else 1.0
    axis = token[-1]
    return tuple(sign if value == axis else 0.0 for value in "XYZ")


def _dot(left: tuple[float, ...], right: tuple[float, ...]) -> float:
    return sum(a * b for a, b in zip(left, right, strict=True))


def _cross(
    left: tuple[float, float, float], right: tuple[float, float, float]
) -> tuple[float, float, float]:
    return (
        left[1] * right[2] - left[2] * right[1],
        left[2] * right[0] - left[0] * right[2],
        left[0] * right[1] - left[1] * right[0],
    )


def _basis(forward: str, up: str) -> tuple[tuple[float, float, float], ...]:
    forward_value = _axis_vector(forward)
    up_value = _axis_vector(up)
    if _dot(forward_value, up_value) != 0.0:
        raise ValueError
    right = _cross(forward_value, up_value)
    return tuple(zip(right, forward_value, up_value, strict=True))


def _transpose(value: tuple[tuple[float, ...], ...]) -> tuple[tuple[float, ...], ...]:
    return tuple(zip(*value, strict=True))


def _multiply(
    left: tuple[tuple[float, ...], ...], right: tuple[tuple[float, ...], ...]
) -> tuple[tuple[float, ...], ...]:
    columns = _transpose(right)
    return tuple(tuple(_dot(row, column) for column in columns) for row in left)


def _matrix4(
    rotation: tuple[tuple[float, ...], ...], scale: float
) -> tuple[tuple[float, ...], ...]:
    return (
        *((*tuple(rotation[row][column] * scale for column in range(3)), 0.0) for row in range(3)),
        (0.0, 0.0, 0.0, 1.0),
    )


def _translation(value: tuple[float, float, float]) -> tuple[tuple[float, ...], ...]:
    return (
        (1.0, 0.0, 0.0, value[0]),
        (0.0, 1.0, 0.0, value[1]),
        (0.0, 0.0, 1.0, value[2]),
        (0.0, 0.0, 0.0, 1.0),
    )


def _point(matrix: tuple[tuple[float, ...], ...], value: Any) -> tuple[float, float, float]:
    x, y, z = finite_components(value, 3)
    source = (x, y, z, 1.0)
    result = tuple(_dot(row, source) for row in matrix)
    if result[3] == 0.0:
        raise ValueError
    return tuple(component / result[3] for component in result[:3])


def _matrix(value: Any) -> tuple[tuple[float, ...], ...]:
    flat = matrix_components(value)
    return tuple(tuple(flat[row * 4 + column] for column in range(4)) for row in range(4))


def _assign_matrix(value: Any, matrix: tuple[tuple[float, ...], ...]) -> None:
    try:
        from mathutils import Matrix
    except ImportError:
        value.matrix_world = matrix
    else:
        value.matrix_world = Matrix(matrix)


def _metrics(objects: tuple[Any, ...]) -> TransformMetrics:
    points: list[tuple[float, float, float]] = []
    mesh_count = 0
    for value in objects:
        matrix = _matrix(value.matrix_world)
        if value.type == "MESH":
            mesh_count += 1
            corners = tuple(value.bound_box)
            if len(corners) != 8:
                raise ValueError
            points.extend(_point(matrix, corner) for corner in corners)
        else:
            points.append(_point(matrix, (0.0, 0.0, 0.0)))
    if not objects or not points:
        raise ValueError
    minimum = tuple(min(point[index] for point in points) for index in range(3))
    maximum = tuple(max(point[index] for point in points) for index in range(3))
    return TransformMetrics(
        len(objects),
        mesh_count,
        minimum,
        maximum,
        tuple(maximum[index] - minimum[index] for index in range(3)),
    )


def _action_curves(action: Any) -> tuple[Any, ...]:
    layered: list[Any] = []
    for layer in tuple(getattr(action, "layers", ())):
        for strip in tuple(layer.strips):
            for channelbag in tuple(getattr(strip, "channelbags", ())):
                layered.extend(tuple(channelbag.fcurves))
    return tuple(layered) if layered else tuple(getattr(action, "fcurves", ()))


def _deformation_signature(objects: tuple[Any, ...], actions: tuple[Any, ...]) -> tuple[Any, ...]:
    values: list[Any] = []
    for value in sorted(objects, key=lambda item: item.name):
        bones = tuple(getattr(getattr(value, "data", None), "bones", ()))
        bone_values = tuple(
            (
                bone.name,
                None if bone.parent is None else bone.parent.name,
                bool(bone.use_deform),
                matrix_components(bone.matrix_local),
            )
            for bone in bones
        )
        vertices = tuple(getattr(getattr(value, "data", None), "vertices", ()))
        weight_values = tuple(
            tuple(
                sorted(
                    (membership.group, finite_number(membership.weight))
                    for membership in tuple(vertex.groups)
                )
            )
            for vertex in vertices
        )
        values.append((value.name, bone_values, weight_values))
    action_values: list[Any] = []
    for action in sorted(actions, key=lambda item: item.name):
        curves = _action_curves(action)
        action_values.append(
            (
                action.name,
                tuple(
                    (
                        curve.data_path,
                        curve.array_index,
                        tuple(
                            finite_components(point.co, 2) for point in tuple(curve.keyframe_points)
                        ),
                        tuple(
                            finite_components(point.co, 2) for point in tuple(curve.sampled_points)
                        ),
                    )
                    for curve in curves
                ),
            )
        )
    return tuple(values), tuple(action_values)


def _validate_plan(plan: TransformNormalizationPlan) -> float:
    if (
        plan.source_forward not in _AXES
        or plan.source_up not in _AXES
        or plan.target_forward not in _AXES
        or plan.target_up not in _AXES
        or plan.pivot_policy not in _PIVOTS
        or plan.target_unit_system not in {"NONE", "METRIC", "IMPERIAL"}
    ):
        raise ValueError
    _basis(plan.source_forward, plan.source_up)
    _basis(plan.target_forward, plan.target_up)
    source = finite_number(plan.source_scale_length)
    target = finite_number(plan.target_scale_length)
    if source <= 0.0 or target <= 0.0:
        raise ValueError
    return source / target


def normalize_transforms(
    objects: Iterable[Any], actions: Iterable[Any], scene: Any, plan: TransformNormalizationPlan
) -> TransformNormalizationResult:
    """Normalize all object world matrices atomically and prove raw deformation is unchanged."""

    try:
        source_objects = tuple(objects)
        source_actions = tuple(actions)
        factor = _validate_plan(plan)
        before = _metrics(source_objects)
        signature = _deformation_signature(source_objects, source_actions)
        source_basis = _basis(plan.source_forward, plan.source_up)
        target_basis = _basis(plan.target_forward, plan.target_up)
        rotation = _multiply(target_basis, _transpose(source_basis))
        base = _matrix4(rotation, factor)
        provisional = tuple(
            _multiply(base, _matrix(value.matrix_world)) for value in source_objects
        )
        snapshots = tuple(_matrix(value.matrix_world) for value in source_objects)
        unit_snapshot = (scene.unit_settings.system, scene.unit_settings.scale_length)
        for value, matrix in zip(source_objects, provisional, strict=True):
            _assign_matrix(value, matrix)
        provisional_metrics = _metrics(source_objects)
        if plan.pivot_policy == KEEP:
            pivot = (0.0, 0.0, 0.0)
        elif plan.pivot_policy == BOUNDS_CENTER:
            pivot = tuple(
                (provisional_metrics.minimum[index] + provisional_metrics.maximum[index]) / 2.0
                for index in range(3)
            )
        else:
            pivot = (
                (provisional_metrics.minimum[0] + provisional_metrics.maximum[0]) / 2.0,
                (provisional_metrics.minimum[1] + provisional_metrics.maximum[1]) / 2.0,
                provisional_metrics.minimum[2],
            )
        pivot_translation = tuple(-component for component in pivot)
        conversion = _multiply(_translation(pivot_translation), base)
        for value, snapshot in zip(source_objects, snapshots, strict=True):
            _assign_matrix(value, _multiply(conversion, snapshot))
        scene.unit_settings.system = plan.target_unit_system
        scene.unit_settings.scale_length = plan.target_scale_length
        after = _metrics(source_objects)
        if _deformation_signature(source_objects, source_actions) != signature:
            raise RuntimeError
    except Exception:
        rollback_failed = False
        if "snapshots" in locals():
            try:
                for value, snapshot in zip(source_objects, snapshots, strict=True):
                    _assign_matrix(value, snapshot)
                scene.unit_settings.system, scene.unit_settings.scale_length = unit_snapshot
            except Exception:
                rollback_failed = True
        return TransformNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_TRANSFORM_NORMALIZATION_FAILED",
                    "The coordinate policy was invalid or could not be applied without deformation.",
                    (
                        "Discard the workspace and retry from the retained source."
                        if rollback_failed
                        else "Correct the transform policy and retry from the restored workspace."
                    ),
                ),
            ),
        )
    return TransformNormalizationResult(
        TransformNormalizationReport(plan, factor, pivot_translation, before, after, True),
        (),
    )
