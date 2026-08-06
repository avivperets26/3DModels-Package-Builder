"""Transactional manifest-driven Blender data-block naming normalization."""

from __future__ import annotations

import re
from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from typing import Any

from .inspection_common import InspectionFinding, required_name

OBJECT = "object"
MESH = "mesh"
ARMATURE = "armature"
MATERIAL = "material"
IMAGE = "image"
ACTION = "action"
_CATEGORIES = (OBJECT, MESH, ARMATURE, MATERIAL, IMAGE, ACTION)
_PREFIXES = {
    MESH: "MS_",
    ARMATURE: "SKEL_",
    MATERIAL: "M_",
    IMAGE: "T_",
    ACTION: "A_",
}
_ASSET_ID = re.compile(r"[A-Za-z][A-Za-z0-9]*\Z")
_FOLDER_NAME = re.compile(r"[A-Za-z][A-Za-z0-9_]*\Z")


@dataclass(frozen=True, slots=True)
class NamingAssignment:
    """One source-to-manifest name assignment within a Blender ID namespace."""

    category: str
    source_name: str
    desired_name: str


@dataclass(frozen=True, slots=True)
class ExportedAssetName:
    """One collision-checked output role and filename from the manifest naming plan."""

    role: str
    filename: str


@dataclass(frozen=True, slots=True)
class BlenderNamingPlan:
    """Complete explicit naming plan for all mutable data blocks and exported files."""

    asset_id: str
    folder_name: str
    assignments: tuple[NamingAssignment, ...]
    exported_assets: tuple[ExportedAssetName, ...]


@dataclass(frozen=True, slots=True)
class NamingNormalizationReport:
    """Deterministic before/after names after one successful transaction."""

    asset_id: str
    folder_name: str
    renamed: tuple[NamingAssignment, ...]
    exported_assets: tuple[ExportedAssetName, ...]


@dataclass(frozen=True, slots=True)
class NamingNormalizationResult:
    """Non-throwing expected result of manifest naming normalization."""

    report: NamingNormalizationReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether every planned name was applied exactly."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-naming-normalizer")


def _safe_name(value: Any) -> str:
    name = required_name(value)
    if name in {".", ".."} or "/" in name or "\\" in name or name != name.strip():
        raise ValueError
    return name


def _validate_convention(category: str, name: str, asset_id: str) -> None:
    if category == OBJECT:
        if name not in {asset_id, "P_Model"} and not name.startswith(f"P_{asset_id}"):
            raise ValueError
        return
    prefix = _PREFIXES[category] + asset_id
    if name != prefix and not name.startswith(prefix + "_"):
        raise ValueError


def _temporary_names(blocks: tuple[Any, ...], desired: set[str], category: str) -> tuple[str, ...]:
    occupied = {required_name(block.name) for block in blocks} | desired
    values: list[str] = []
    for index in range(len(blocks)):
        candidate_index = index
        while True:
            candidate = f"__PB_{category.upper()}_{candidate_index:04d}__"
            if candidate not in occupied:
                occupied.add(candidate)
                values.append(candidate)
                break
            candidate_index += len(blocks) + 1
    return tuple(values)


def normalize_blender_names(
    data_blocks: Mapping[str, Iterable[Any]], plan: BlenderNamingPlan
) -> NamingNormalizationResult:
    """Apply a complete plan with two-phase renaming and rollback on any mismatch."""

    try:
        asset_id = _safe_name(plan.asset_id)
        folder_name = _safe_name(plan.folder_name)
        if not _ASSET_ID.fullmatch(asset_id) or not _FOLDER_NAME.fullmatch(folder_name):
            raise ValueError
        if set(data_blocks) != set(_CATEGORIES):
            raise ValueError
        blocks_by_category = {category: tuple(data_blocks[category]) for category in _CATEGORIES}
        assignments_by_category: dict[str, tuple[NamingAssignment, ...]] = {}
        for category in _CATEGORIES:
            blocks = blocks_by_category[category]
            names = tuple(_safe_name(block.name) for block in blocks)
            if len(set(names)) != len(names):
                raise ValueError
            assignments = tuple(item for item in plan.assignments if item.category == category)
            if len(assignments) != len(blocks):
                raise ValueError
            if {item.source_name for item in assignments} != set(names):
                raise ValueError
            desired = tuple(_safe_name(item.desired_name) for item in assignments)
            if len(set(desired)) != len(desired):
                raise ValueError
            for name in desired:
                _validate_convention(category, name, asset_id)
            assignments_by_category[category] = assignments
        if any(item.category not in _CATEGORIES for item in plan.assignments):
            raise ValueError
        roles = tuple(_safe_name(item.role) for item in plan.exported_assets)
        filenames = tuple(_safe_name(item.filename) for item in plan.exported_assets)
        if len(set(roles)) != len(roles) or len({name.casefold() for name in filenames}) != len(
            filenames
        ):
            raise ValueError
        for filename in filenames:
            if not filename.lower().endswith((".fbx", ".glb", ".zip")):
                raise ValueError
            if not (filename.startswith(asset_id) or filename.startswith(folder_name)):
                raise ValueError
    except Exception:
        return NamingNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_NAMING_PLAN_INVALID",
                    "The naming plan is incomplete, non-canonical, or contains a collision.",
                    "Regenerate the complete manifest naming plan and retry.",
                ),
            ),
        )

    snapshots = {
        category: tuple(block.name for block in blocks_by_category[category])
        for category in _CATEGORIES
    }
    try:
        for category in _CATEGORIES:
            blocks = blocks_by_category[category]
            assignments = assignments_by_category[category]
            by_source = {block.name: block for block in blocks}
            ordered = tuple(by_source[item.source_name] for item in assignments)
            temporary = _temporary_names(
                blocks, {item.desired_name for item in assignments}, category
            )
            for block, name in zip(ordered, temporary, strict=True):
                block.name = name
                if block.name != name:
                    raise ValueError
            for block, item in zip(ordered, assignments, strict=True):
                block.name = item.desired_name
                if block.name != item.desired_name:
                    raise ValueError
    except Exception:
        rollback_failed = False
        try:
            for category in _CATEGORIES:
                blocks = blocks_by_category[category]
                originals = snapshots[category]
                temporary = _temporary_names(blocks, set(originals), category)
                for block, name in zip(blocks, temporary, strict=True):
                    block.name = name
                for block, name in zip(blocks, originals, strict=True):
                    block.name = name
        except Exception:
            rollback_failed = True
        return NamingNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_NAMING_APPLY_FAILED",
                    "Blender did not apply the naming plan exactly; the transaction was rolled back.",
                    (
                        "Discard the workspace and retry from the retained source."
                        if rollback_failed
                        else "Review data-block ownership and retry from the restored workspace."
                    ),
                ),
            ),
        )

    renamed = tuple(
        sorted(
            plan.assignments, key=lambda item: (_CATEGORIES.index(item.category), item.source_name)
        )
    )
    exports = tuple(sorted(plan.exported_assets, key=lambda item: item.role))
    return NamingNormalizationResult(
        NamingNormalizationReport(asset_id, folder_name, renamed, exports), ()
    )
