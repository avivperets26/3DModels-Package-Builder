"""Read-only Blender 5 layered/legacy Action and animation-channel inspection."""

from __future__ import annotations

from collections.abc import Iterable
from dataclasses import dataclass
from typing import Any

from .inspection_common import (
    InspectionFinding,
    finite_components,
    finite_number,
    required_name,
)


@dataclass(frozen=True, slots=True)
class AnimationChannelReport:
    """One F-curve channel copied from a layered slot or legacy Action."""

    slot_identifier: str | None
    layer_name: str | None
    strip_index: int | None
    data_path: str
    array_index: int
    keyframe_count: int
    sampled_point_count: int
    first_frame: float | None
    last_frame: float | None
    has_motion: bool
    transform_channel: bool


@dataclass(frozen=True, slots=True)
class ActionAnimationReport:
    """One Action interpreted as one source animation clip."""

    name: str
    frame_start: float
    frame_end: float
    duration_seconds: float
    fps: float
    channels: tuple[AnimationChannelReport, ...]
    channel_count: int
    keyframe_count: int
    has_motion: bool
    has_transform_motion: bool
    loop_likelihood: str
    loop_reasons: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class AnimationInspectionReport:
    """Deterministic scene Action/clip inventory."""

    actions: tuple[ActionAnimationReport, ...]
    action_count: int
    clip_count: int
    animated_clip_count: int
    transform_motion_clip_count: int
    scene_fps: float


@dataclass(frozen=True, slots=True)
class AnimationInspectionResult:
    """Non-throwing expected result of Blender animation inspection."""

    report: AnimationInspectionReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether a complete internally consistent report was produced."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-animation-inspector")


def _scene_fps(scene: Any) -> float:
    fps = finite_number(scene.render.fps)
    base = finite_number(scene.render.fps_base)
    if fps <= 0.0 or base <= 0.0:
        raise ValueError
    return fps / base


def _slot_identifier(channelbag: Any) -> str:
    slot = getattr(channelbag, "slot", None)
    if slot is not None:
        for attribute in ("identifier", "name", "display_name"):
            value = getattr(slot, attribute, None)
            if value:
                return required_name(value)
    handle = getattr(channelbag, "slot_handle", None)
    if type(handle) is int and handle >= 0:
        return f"slot-{handle}"
    raise ValueError


def _action_curves(
    action: Any,
) -> tuple[tuple[str | None, str | None, int | None, Any], ...]:
    layered: list[tuple[str | None, str | None, int | None, Any]] = []
    for layer_index, layer in enumerate(tuple(getattr(action, "layers", ()))):
        layer_name = getattr(layer, "name", None) or f"layer-{layer_index}"
        layer_name = required_name(layer_name)
        for strip_index, strip in enumerate(tuple(layer.strips)):
            for channelbag in tuple(getattr(strip, "channelbags", ())):
                slot = _slot_identifier(channelbag)
                layered.extend(
                    (slot, layer_name, strip_index, curve) for curve in tuple(channelbag.fcurves)
                )
    if layered:
        return tuple(layered)
    return tuple((None, None, None, curve) for curve in tuple(getattr(action, "fcurves", ())))


def _curve_points(curve: Any) -> tuple[tuple[float, float], ...]:
    points = tuple(finite_components(point.co, 2) for point in tuple(curve.keyframe_points))
    samples = tuple(finite_components(point.co, 2) for point in tuple(curve.sampled_points))
    combined = points + samples
    if any(combined[index][0] > combined[index + 1][0] for index in range(len(combined) - 1)):
        raise ValueError
    return combined


def _transform_channel(data_path: str) -> bool:
    return any(
        term in data_path
        for term in (
            "location",
            "rotation_euler",
            "rotation_quaternion",
            "rotation_axis_angle",
            "scale",
        )
    )


def _inspect_channel(
    slot: str | None,
    layer_name: str | None,
    strip_index: int | None,
    curve: Any,
) -> tuple[AnimationChannelReport, bool]:
    data_path = required_name(curve.data_path)
    array_index = curve.array_index
    if type(array_index) is not int or array_index < 0:
        raise ValueError
    keyframes = tuple(curve.keyframe_points)
    samples = tuple(curve.sampled_points)
    points = _curve_points(curve)
    values = tuple(point[1] for point in points)
    modifier_types = tuple(
        required_name(modifier.type) for modifier in tuple(getattr(curve, "modifiers", ()))
    )
    has_motion = (len(values) >= 2 and max(values) - min(values) > 1e-7) or bool(
        points and set(modifier_types).intersection({"NOISE", "GENERATOR", "FNGENERATOR"})
    )
    transform = _transform_channel(data_path)
    return (
        AnimationChannelReport(
            slot,
            layer_name,
            strip_index,
            data_path,
            array_index,
            len(keyframes),
            len(samples),
            None if not points else points[0][0],
            None if not points else points[-1][0],
            has_motion,
            transform,
        ),
        "CYCLES" in modifier_types,
    )


def _loop_metadata(
    name: str,
    channels: tuple[AnimationChannelReport, ...],
    cycle_modifier: bool,
) -> tuple[str, tuple[str, ...]]:
    reasons: list[str] = []
    token = "".join(character.lower() for character in name if character.isalnum())
    if any(term in token for term in ("loop", "cycle", "idle", "walk", "run")):
        reasons.append("name_hint")
    if cycle_modifier:
        reasons.append("cycles_modifier")
    moving = tuple(channel for channel in channels if channel.has_motion)
    if not moving:
        return "unknown", tuple(reasons)
    if reasons:
        return "likely", tuple(reasons)
    return "unlikely", ()


def _inspect_action(action: Any, fps: float) -> ActionAnimationReport:
    name = required_name(action.name)
    curve_entries = _action_curves(action)
    reports: list[AnimationChannelReport] = []
    cycle_modifier = False
    identities: set[tuple[str | None, str | None, int | None, str, int]] = set()
    for slot, layer_name, strip_index, curve in curve_entries:
        report, has_cycle = _inspect_channel(slot, layer_name, strip_index, curve)
        identity = (
            report.slot_identifier,
            report.layer_name,
            report.strip_index,
            report.data_path,
            report.array_index,
        )
        if identity in identities:
            raise ValueError
        identities.add(identity)
        reports.append(report)
        cycle_modifier = cycle_modifier or has_cycle
    ordered = tuple(
        sorted(
            reports,
            key=lambda item: (
                item.slot_identifier or "",
                item.layer_name or "",
                -1 if item.strip_index is None else item.strip_index,
                item.data_path,
                item.array_index,
            ),
        )
    )
    frames = tuple(
        frame
        for report in ordered
        for frame in (report.first_frame, report.last_frame)
        if frame is not None
    )
    if frames:
        frame_start, frame_end = min(frames), max(frames)
    else:
        frame_start, frame_end = finite_components(action.frame_range, 2)
    if frame_end < frame_start:
        raise ValueError
    likelihood, reasons = _loop_metadata(name, ordered, cycle_modifier)
    return ActionAnimationReport(
        name,
        frame_start,
        frame_end,
        (frame_end - frame_start) / fps,
        fps,
        ordered,
        len(ordered),
        sum(item.keyframe_count for item in ordered),
        any(item.has_motion for item in ordered),
        any(item.has_motion and item.transform_channel for item in ordered),
        likelihood,
        reasons,
    )


def inspect_animations(actions: Iterable[Any], scene: Any) -> AnimationInspectionResult:
    """Report Blender 5 layered or legacy Actions without evaluating or editing animation."""

    try:
        source_actions = tuple(actions)
    except Exception:
        return AnimationInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_ANIMATION_INPUT_INVALID",
                    "Blender Actions could not be enumerated safely.",
                    "Discard the workspace and retry inspection in a new Blender process.",
                ),
            ),
        )
    try:
        fps = _scene_fps(scene)
        reports = tuple(
            sorted(
                (_inspect_action(action, fps) for action in source_actions),
                key=lambda item: item.name,
            )
        )
        if len({item.name for item in reports}) != len(reports):
            raise ValueError
    except Exception:
        return AnimationInspectionResult(
            None,
            (
                _finding(
                    "BLENDER_ANIMATION_DATA_INVALID",
                    "Blender returned incomplete, inconsistent, or non-finite animation data.",
                    "Review the retained worker log, repair the Action data, and retry.",
                ),
            ),
        )
    return AnimationInspectionResult(
        AnimationInspectionReport(
            reports,
            len(reports),
            len(reports),
            sum(item.has_motion for item in reports),
            sum(item.has_transform_motion for item in reports),
            fps,
        ),
        (),
    )
