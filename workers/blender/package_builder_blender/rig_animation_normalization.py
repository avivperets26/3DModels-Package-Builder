"""Manifest-driven Blender rig/action naming and deterministic bake policy."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from typing import Any

from .animation_inspection import inspect_animations
from .inspection_common import InspectionFinding, required_name


@dataclass(frozen=True, slots=True)
class ClipBakePlan:
    """One complete source Action mapping and inclusive bake range."""

    source_action_name: str
    target_action_name: str
    frame_start: int
    frame_end: int
    sample_step: int = 1


@dataclass(frozen=True, slots=True)
class RigAnimationNormalizationPlan:
    """Requested skeleton name, clips, and fixed safe bake/export policy."""

    armature_object_name: str
    skeleton_name: str
    clips: tuple[ClipBakePlan, ...]
    bake_animation: bool = True
    deform_bones_only: bool = True


@dataclass(frozen=True, slots=True)
class NormalizedClipReport:
    """One normalized Action and its verified inclusive clip boundary."""

    action_name: str
    frame_start: int
    frame_end: int
    sample_step: int
    has_motion: bool


@dataclass(frozen=True, slots=True)
class RigAnimationNormalizationReport:
    """Normalized skeleton, deform-bone export set, and clips."""

    armature_object_name: str
    skeleton_name: str
    deform_bone_names: tuple[str, ...]
    clips: tuple[NormalizedClipReport, ...]
    baked: bool


@dataclass(frozen=True, slots=True)
class RigAnimationNormalizationResult:
    """Non-throwing expected result for rig/action normalization."""

    report: RigAnimationNormalizationReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether skeleton and every requested clip were verified."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-rig-animation-normalizer")


def _frames(action: Any) -> tuple[float, ...]:
    frames: list[float] = []
    layered = False
    for layer in tuple(getattr(action, "layers", ())):
        for strip in tuple(layer.strips):
            for channelbag in tuple(getattr(strip, "channelbags", ())):
                layered = True
                for curve in tuple(channelbag.fcurves):
                    frames.extend(float(point.co[0]) for point in tuple(curve.keyframe_points))
                    frames.extend(float(point.co[0]) for point in tuple(curve.sampled_points))
    if not layered:
        for curve in tuple(getattr(action, "fcurves", ())):
            frames.extend(float(point.co[0]) for point in tuple(curve.keyframe_points))
            frames.extend(float(point.co[0]) for point in tuple(curve.sampled_points))
    return tuple(frames)


def _bake_arguments(clip: ClipBakePlan) -> dict[str, Any]:
    """Return the reviewed Blender 5 NLA bake policy without destructive cleanup options."""

    return {
        "frame_start": clip.frame_start,
        "frame_end": clip.frame_end,
        "step": clip.sample_step,
        "only_selected": True,
        "visual_keying": True,
        "clear_constraints": False,
        "clear_parents": False,
        "use_current_action": True,
        "clean_curves": False,
        "bake_types": {"POSE"},
        "channel_types": {"BBONE", "LOCATION", "PROPS", "ROTATION", "SCALE"},
    }


def normalize_rig_animation(
    armature_object: Any,
    actions: tuple[Any, ...],
    scene: Any,
    plan: RigAnimationNormalizationPlan,
    bake_operator: Callable[..., Any],
) -> RigAnimationNormalizationResult:
    """Normalize one rig and its complete Action inventory in a disposable workspace."""

    try:
        if required_name(armature_object.name) != required_name(plan.armature_object_name):
            raise ValueError
        if required_name(armature_object.type).upper() != "ARMATURE":
            raise ValueError
        skeleton_name = required_name(plan.skeleton_name)
        if not skeleton_name.startswith("SKEL_"):
            raise ValueError
        source_names = tuple(required_name(item.name) for item in actions)
        clips = plan.clips
        clip_sources = tuple(required_name(item.source_action_name) for item in clips)
        clip_targets = tuple(required_name(item.target_action_name) for item in clips)
        if (
            not clips
            or len(set(source_names)) != len(source_names)
            or set(source_names) != set(clip_sources)
            or len(clip_sources) != len(set(clip_sources))
            or len(clip_targets) != len(set(clip_targets))
            or any(not name.startswith("A_") for name in clip_targets)
            or any(
                type(item.frame_start) is not int
                or type(item.frame_end) is not int
                or type(item.sample_step) is not int
                or item.frame_start < 0
                or item.frame_end < max(1, item.frame_start)
                or not 1 <= item.sample_step <= 120
                for item in clips
            )
        ):
            raise ValueError
        bones = tuple(armature_object.data.bones)
        bone_names = tuple(required_name(item.name) for item in bones)
        deform_names = tuple(sorted(item.name for item in bones if bool(item.use_deform)))
        if len(set(bone_names)) != len(bone_names) or not deform_names:
            raise ValueError
        pose_bones = tuple(armature_object.pose.bones)
        if {required_name(item.name) for item in pose_bones} != set(bone_names):
            raise ValueError
        before = inspect_animations(actions, scene)
        if not before.succeeded or before.report is None:
            raise ValueError
        before_by_name = {item.name: item for item in before.report.actions}
        if any(not before_by_name[item.source_action_name].has_motion for item in clips):
            raise ValueError
    except Exception:
        return RigAnimationNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_RIG_ANIMATION_PLAN_INVALID",
                    "The rig/action plan is incomplete, colliding, motionless, or outside Blender bake bounds.",
                    "Repair the manifest skeleton and clip plan, then retry from a fresh working copy.",
                ),
            ),
        )

    original_armature_data_name = armature_object.data.name
    original_action_names = {id(item): item.name for item in actions}
    original_frame_range = (scene.frame_start, scene.frame_end)
    original_active_action = armature_object.animation_data.action
    pose_selection = {id(item): bool(item.bone.select) for item in pose_bones}
    by_source = {item.name: item for item in actions}
    reports: list[NormalizedClipReport] = []
    try:
        armature_object.data.name = skeleton_name
        if armature_object.data.name != skeleton_name:
            raise ValueError
        for clip in clips:
            action = by_source[clip.source_action_name]
            action.name = clip.target_action_name
            if action.name != clip.target_action_name:
                raise ValueError
            armature_object.animation_data.action = action
            scene.frame_start = clip.frame_start
            scene.frame_end = clip.frame_end
            for pose_bone in pose_bones:
                pose_bone.bone.select = (
                    pose_bone.name in deform_names if plan.deform_bones_only else True
                )
            if plan.bake_animation:
                operator_result = set(bake_operator(**_bake_arguments(clip)))
                if operator_result != {"FINISHED"}:
                    raise RuntimeError
            inspected = inspect_animations((action,), scene)
            if not inspected.succeeded or inspected.report is None:
                raise ValueError
            action_report = inspected.report.actions[0]
            frames = _frames(action)
            if (
                action_report.frame_start != clip.frame_start
                or action_report.frame_end != clip.frame_end
                or not action_report.has_motion
                or clip.frame_start not in frames
                or clip.frame_end not in frames
                or any(
                    frame not in {clip.frame_start, clip.frame_end}
                    and (frame - clip.frame_start) % clip.sample_step != 0
                    for frame in frames
                )
            ):
                raise ValueError
            reports.append(
                NormalizedClipReport(
                    action.name,
                    clip.frame_start,
                    clip.frame_end,
                    clip.sample_step,
                    action_report.has_motion,
                )
            )
    except Exception:
        rollback_failed = False
        try:
            armature_object.data.name = original_armature_data_name
            for action in actions:
                action.name = original_action_names[id(action)]
        except Exception:
            rollback_failed = True
        return RigAnimationNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_RIG_ANIMATION_NORMALIZATION_FAILED",
                    "Blender did not preserve the requested bake boundaries and motion.",
                    (
                        "Discard the workspace and retry from the retained source."
                        if rollback_failed or plan.bake_animation
                        else "Review the restored rig/action data and retry."
                    ),
                ),
            ),
        )
    finally:
        scene.frame_start, scene.frame_end = original_frame_range
        armature_object.animation_data.action = original_active_action
        for pose_bone in pose_bones:
            pose_bone.bone.select = pose_selection[id(pose_bone)]

    return RigAnimationNormalizationResult(
        RigAnimationNormalizationReport(
            armature_object.name,
            skeleton_name,
            deform_names,
            tuple(sorted(reports, key=lambda item: item.action_name)),
            plan.bake_animation,
        ),
        (),
    )
