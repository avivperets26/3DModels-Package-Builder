"""Deterministic product-case inference from validated Blender inspection reports."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .inspection_common import InspectionFinding

STATIC = "static"
RIGGED = "rigged"
RIGGED_ANIMATED = "rigged-animated"
ITEM_SET = "item-set"
ITEM_COLLECTION = "item-collection"
_CASES = {STATIC, RIGGED, RIGGED_ANIMATED, ITEM_SET, ITEM_COLLECTION}


@dataclass(frozen=True, slots=True)
class ProductCaseInferenceReport:
    """Detected source facts and the resolved manifest product case."""

    detected_case: str
    resolved_case: str
    manifest_supplied: bool
    requires_manifest_for_grouping: bool
    mesh_count: int
    skeleton_count: int
    skinned_mesh_count: int
    animated_clip_count: int
    reasons: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class ProductCaseInferenceResult:
    """Non-throwing expected result of automatic case inference."""

    report: ProductCaseInferenceReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether a complete, internally consistent inference was produced."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-product-case-inference")


def _count(report: Any, attribute: str) -> int:
    value = getattr(report, attribute)
    if type(value) is not int or value < 0:
        raise ValueError
    return value


def infer_product_case(
    geometry_report: Any,
    rig_report: Any,
    animation_report: Any,
    manifest_case: str | None = None,
) -> ProductCaseInferenceResult:
    """Infer only static/rigged/animated; set/collection always requires a manifest case."""

    try:
        mesh_count = _count(geometry_report, "mesh_count")
        skeleton_count = _count(rig_report, "skeleton_count")
        skinned_mesh_count = _count(rig_report, "skinned_mesh_count")
        animated_clip_count = _count(animation_report, "animated_clip_count")
        _count(animation_report, "action_count")
        if mesh_count == 0:
            raise ValueError
        if (skeleton_count == 0) != (skinned_mesh_count == 0):
            return ProductCaseInferenceResult(
                None,
                (
                    _finding(
                        "BLENDER_CASE_RIG_INCOMPLETE",
                        "The source has only one of a skeleton or a skinned mesh binding.",
                        "Repair the armature binding or explicitly remove the incomplete rig.",
                    ),
                ),
            )
        if animated_clip_count > 0 and skeleton_count == 0:
            return ProductCaseInferenceResult(
                None,
                (
                    _finding(
                        "BLENDER_CASE_ANIMATION_WITHOUT_RIG",
                        "The source contains motion but no complete rig and skin binding.",
                        "Provide the matching rig or remove the incompatible animation.",
                    ),
                ),
            )
        if manifest_case is not None and manifest_case not in _CASES:
            raise ValueError
    except Exception:
        return ProductCaseInferenceResult(
            None,
            (
                _finding(
                    "BLENDER_CASE_INPUT_INVALID",
                    "Inspection facts or the manifest product case are incomplete or invalid.",
                    "Run geometry, rig, and animation inspection and select a canonical case.",
                ),
            ),
        )

    if animated_clip_count:
        detected = RIGGED_ANIMATED
        reasons = ("mesh_present", "complete_rig_present", "motion_clip_present")
    elif skeleton_count:
        detected = RIGGED
        reasons = ("mesh_present", "complete_rig_present", "no_motion_clip")
    else:
        detected = STATIC
        reasons = ("mesh_present", "no_complete_rig", "no_motion_clip")

    if manifest_case in {STATIC, RIGGED, RIGGED_ANIMATED} and manifest_case != detected:
        return ProductCaseInferenceResult(
            None,
            (
                _finding(
                    "BLENDER_CASE_MANIFEST_CONFLICT",
                    "The manifest product case conflicts with the inspected source facts.",
                    "Correct the manifest or repair the source before normalization.",
                ),
            ),
        )
    grouped = manifest_case in {ITEM_SET, ITEM_COLLECTION}
    resolved = manifest_case if manifest_case is not None else detected
    return ProductCaseInferenceResult(
        ProductCaseInferenceReport(
            detected,
            resolved,
            manifest_case is not None,
            True,
            mesh_count,
            skeleton_count,
            skinned_mesh_count,
            animated_clip_count,
            reasons + (("grouping_declared_by_manifest",) if grouped else ()),
        ),
        (),
    )
