"""Read-only Blender armature, skin, bind-data, and vertex-weight inspection."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass
from typing import Any

from .inspection_common import (
    InspectionFinding,
    finite_components,
    finite_number,
    matrix_components,
    required_name,
)


@dataclass(frozen=True, slots=True)
class BoneRigReport:
    """One rest-pose bone and its hierarchy/deformation metadata."""

    name: str
    parent_name: str | None
    use_deform: bool
    head: tuple[float, float, float]
    tail: tuple[float, float, float]
    rest_matrix: tuple[float, ...]


@dataclass(frozen=True, slots=True)
class ArmatureRigReport:
    """One deterministic armature skeleton inventory."""

    object_name: str
    bones: tuple[BoneRigReport, ...]
    root_bone_names: tuple[str, ...]
    bone_count: int
    deform_bone_count: int


@dataclass(frozen=True, slots=True)
class SkinnedMeshReport:
    """One mesh-to-armature binding and its weight-quality summary."""

    object_name: str
    armature_object_name: str
    vertex_count: int
    vertex_group_names: tuple[str, ...]
    missing_deform_group_names: tuple[str, ...]
    unmatched_vertex_group_names: tuple[str, ...]
    unweighted_vertex_indices: tuple[int, ...]
    maximum_deform_influences: int
    parent_inverse_matrix: tuple[float, ...]


@dataclass(frozen=True, slots=True)
class RigInspectionReport:
    """Scene-wide armature and skinned-mesh inspection facts."""

    armatures: tuple[ArmatureRigReport, ...]
    skinned_meshes: tuple[SkinnedMeshReport, ...]
    skeleton_count: int
    bone_count: int
    deform_bone_count: int
    skinned_mesh_count: int
    unweighted_vertex_count: int


@dataclass(frozen=True, slots=True)
class RigInspectionResult:
    """Non-throwing expected result of Blender rig inspection."""

    report: RigInspectionReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether a complete internally consistent report was produced."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-rig-inspector")


def _inspect_armature(value: Any) -> ArmatureRigReport:
    object_name = required_name(value.name)
    source_bones = tuple(value.data.bones)
    names: set[str] = set()
    parent_by_name: dict[str, str | None] = {}
    reports: list[BoneRigReport] = []
    for bone in source_bones:
        name = required_name(bone.name)
        if name in names:
            raise ValueError
        names.add(name)
        parent = bone.parent
        parent_name = None if parent is None else required_name(parent.name)
        parent_by_name[name] = parent_name
        reports.append(
            BoneRigReport(
                name,
                parent_name,
                bool(bone.use_deform),
                finite_components(bone.head_local, 3),
                finite_components(bone.tail_local, 3),
                matrix_components(bone.matrix_local),
            )
        )
    if not reports:
        raise ValueError
    if any(parent is not None and parent not in names for parent in parent_by_name.values()):
        raise ValueError
    for name in names:
        visited: set[str] = set()
        current: str | None = name
        while current is not None:
            if current in visited:
                raise ValueError
            visited.add(current)
            current = parent_by_name[current]
    ordered = tuple(sorted(reports, key=lambda item: item.name))
    roots = tuple(sorted(item.name for item in ordered if item.parent_name is None))
    return ArmatureRigReport(
        object_name,
        ordered,
        roots,
        len(ordered),
        sum(item.use_deform for item in ordered),
    )


def _armature_modifier(value: Any) -> Any | None:
    modifiers = tuple(
        modifier for modifier in tuple(value.modifiers) if modifier.type == "ARMATURE"
    )
    if len(modifiers) > 1:
        raise ValueError
    return None if not modifiers else modifiers[0]


def _inspect_skinned_mesh(
    value: Any, armatures: dict[str, ArmatureRigReport]
) -> SkinnedMeshReport | None:
    modifier = _armature_modifier(value)
    if modifier is None:
        return None
    armature = modifier.object
    if armature is None:
        raise ValueError
    armature_name = required_name(armature.name)
    if armature_name not in armatures:
        raise ValueError
    skeleton = armatures[armature_name]
    deform_names = {item.name for item in skeleton.bones if item.use_deform}

    group_names: dict[int, str] = {}
    for group in tuple(value.vertex_groups):
        index = group.index
        name = required_name(group.name)
        if (
            type(index) is not int
            or index < 0
            or index in group_names
            or name in group_names.values()
        ):
            raise ValueError
        group_names[index] = name

    unweighted: list[int] = []
    maximum_influences = 0
    vertices = tuple(value.data.vertices)
    if not vertices:
        raise ValueError
    for expected_index, vertex in enumerate(vertices):
        index = vertex.index
        if type(index) is not int or index != expected_index:
            raise ValueError
        seen_groups: set[int] = set()
        deform_influences = 0
        for membership in tuple(vertex.groups):
            group_index = membership.group
            if (
                type(group_index) is not int
                or group_index in seen_groups
                or group_index not in group_names
            ):
                raise ValueError
            seen_groups.add(group_index)
            weight = finite_number(membership.weight)
            if weight < 0.0 or weight > 1.0:
                raise ValueError
            if weight > 0.0 and group_names[group_index] in deform_names:
                deform_influences += 1
        maximum_influences = max(maximum_influences, deform_influences)
        if deform_influences == 0:
            unweighted.append(index)

    present_names = set(group_names.values())
    return SkinnedMeshReport(
        required_name(value.name),
        armature_name,
        len(vertices),
        tuple(group_names[index] for index in sorted(group_names)),
        tuple(sorted(deform_names - present_names)),
        tuple(sorted(present_names - {item.name for item in skeleton.bones})),
        tuple(unweighted),
        maximum_influences,
        matrix_components(value.matrix_parent_inverse),
    )


def inspect_rigs(objects: Iterable[Any]) -> RigInspectionResult:
    """Report armatures and raw skin weights without evaluating or mutating the scene."""

    try:
        source_objects = tuple(objects)
    except Exception:
        return RigInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_RIG_INPUT_INVALID",
                    "Blender scene objects could not be enumerated safely.",
                    "Discard the workspace and retry inspection in a new Blender process.",
                ),
            ),
        )

    try:
        object_names: set[str] = set()
        for value in source_objects:
            name = required_name(value.name)
            required_name(value.type)
            if name in object_names:
                raise ValueError
            object_names.add(name)
        armature_reports = tuple(
            sorted(
                (_inspect_armature(value) for value in source_objects if value.type == "ARMATURE"),
                key=lambda item: item.object_name,
            )
        )
        armatures = {item.object_name: item for item in armature_reports}
        mesh_reports = tuple(
            report
            for value in sorted(source_objects, key=lambda item: item.name)
            if value.type == "MESH"
            for report in (_inspect_skinned_mesh(value, armatures),)
            if report is not None
        )
    except Exception:
        return RigInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_RIG_DATA_INVALID",
                    "Blender returned incomplete, inconsistent, or non-finite rig or weight data.",
                    "Review the retained worker log, repair the armature or skin data, and retry.",
                ),
            ),
        )

    return RigInspectionResult(
        RigInspectionReport(
            armature_reports,
            mesh_reports,
            len(armature_reports),
            sum(item.bone_count for item in armature_reports),
            sum(item.deform_bone_count for item in armature_reports),
            len(mesh_reports),
            sum(len(item.unweighted_vertex_indices) for item in mesh_reports),
        ),
        (),
    )
