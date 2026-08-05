"""PB-0408 Action, clip, channel, motion, FPS, and loop-metadata tests."""

from __future__ import annotations

import math
import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.animation_inspection import inspect_animations  # noqa: E402


class _Point:
    def __init__(self, frame: float, value: float) -> None:
        self.co = (frame, value)


class _Modifier:
    def __init__(self, modifier_type: str) -> None:
        self.type = modifier_type


class _Curve:
    def __init__(
        self,
        data_path: str,
        array_index: int = 0,
        *,
        keyframes: tuple[tuple[float, float], ...] = (),
        samples: tuple[tuple[float, float], ...] = (),
        modifiers: tuple[str, ...] = (),
    ) -> None:
        self.data_path = data_path
        self.array_index = array_index
        self.keyframe_points = tuple(_Point(*value) for value in keyframes)
        self.sampled_points = tuple(_Point(*value) for value in samples)
        self.modifiers = tuple(_Modifier(value) for value in modifiers)


class _Slot:
    def __init__(self, identifier: str) -> None:
        self.identifier = identifier


class _ChannelBag:
    def __init__(self, slot: _Slot, *curves: _Curve) -> None:
        self.slot = slot
        self.fcurves = curves


class _HandleChannelBag:
    def __init__(self, handle: int, *curves: _Curve) -> None:
        self.slot = None
        self.slot_handle = handle
        self.fcurves = curves


class _Strip:
    def __init__(self, *channelbags: _ChannelBag) -> None:
        self.channelbags = channelbags


class _Layer:
    def __init__(self, *strips: _Strip, name: str = "Layer") -> None:
        self.name = name
        self.strips = strips


class _Action:
    def __init__(
        self,
        name: str,
        *curves: _Curve,
        frame_range: tuple[float, float] = (1.0, 1.0),
        layers: tuple[_Layer, ...] = (),
    ) -> None:
        self.name = name
        self.fcurves = curves
        self.frame_range = frame_range
        self.layers = layers


class _Render:
    def __init__(self, fps: float = 30.0, fps_base: float = 1.0) -> None:
        self.fps = fps
        self.fps_base = fps_base


class _Scene:
    def __init__(self, fps: float = 30.0, fps_base: float = 1.0) -> None:
        self.render = _Render(fps, fps_base)


class _Unreadable:
    def __iter__(self):
        raise RuntimeError("private animation detail")


class AnimationInspectionTests(unittest.TestCase):
    def test_reports_legacy_action_range_fps_channels_and_motion(self) -> None:
        action = _Action(
            "BowShot",
            _Curve('pose.bones["String"].location', 0, keyframes=((1, 0), (31, 2))),
            _Curve('pose.bones["String"].location', 1, keyframes=((1, 0), (31, 0))),
        )

        result = inspect_animations((action,), _Scene())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        clip = result.report.actions[0]
        self.assertEqual(
            (1.0, 31.0, 1.0), (clip.frame_start, clip.frame_end, clip.duration_seconds)
        )
        self.assertEqual((2, 4), (clip.channel_count, clip.keyframe_count))
        self.assertTrue(clip.has_motion)
        self.assertTrue(clip.has_transform_motion)
        self.assertEqual("unlikely", clip.loop_likelihood)

    def test_reports_blender_five_layered_action_slots(self) -> None:
        action = _Action(
            "IdleLoop",
            frame_range=(1, 20),
            layers=(
                _Layer(
                    _Strip(
                        _ChannelBag(
                            _Slot("OBRig"),
                            _Curve("rotation_euler", 2, keyframes=((1, 0), (20, 1))),
                        )
                    )
                ),
            ),
        )

        result = inspect_animations((action,), _Scene(24, 1.001))

        self.assertTrue(result.succeeded)
        assert result.report is not None
        clip = result.report.actions[0]
        self.assertEqual("OBRig", clip.channels[0].slot_identifier)
        self.assertEqual("Layer", clip.channels[0].layer_name)
        self.assertEqual(0, clip.channels[0].strip_index)
        self.assertAlmostEqual(24 / 1.001, clip.fps)
        self.assertEqual("likely", clip.loop_likelihood)
        self.assertIn("name_hint", clip.loop_reasons)

    def test_cycles_modifier_marks_moving_clip_as_likely_loop(self) -> None:
        action = _Action(
            "Pulse",
            _Curve("scale", keyframes=((0, 1), (10, 2)), modifiers=("CYCLES",)),
        )

        result = inspect_animations((action,), _Scene())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual("likely", result.report.actions[0].loop_likelihood)
        self.assertEqual(("cycles_modifier",), result.report.actions[0].loop_reasons)

    def test_same_channel_in_separate_layers_is_reported_separately(self) -> None:
        first = _Curve("location", keyframes=((1, 0), (2, 1)))
        second = _Curve("location", keyframes=((1, 1), (2, 2)))
        action = _Action(
            "Layered",
            layers=(
                _Layer(_Strip(_ChannelBag(_Slot("OBRig"), first)), name="Base"),
                _Layer(_Strip(_ChannelBag(_Slot("OBRig"), second)), name="Additive"),
            ),
        )

        result = inspect_animations((action,), _Scene())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(2, result.report.actions[0].channel_count)
        self.assertEqual(
            ("Additive", "Base"),
            tuple(channel.layer_name for channel in result.report.actions[0].channels),
        )

    def test_static_and_sampled_channels_are_distinguished(self) -> None:
        static = _Action("Pose", _Curve("location", keyframes=((1, 0),)))
        sampled = _Action("Sampled", _Curve("value", samples=((2, 0), (4, 1))))

        result = inspect_animations((sampled, static), _Scene())

        self.assertTrue(result.succeeded)
        assert result.report is not None
        static_report, sampled_report = result.report.actions
        self.assertEqual("Sampled", sampled_report.name)
        self.assertEqual(2, sampled_report.channels[0].sampled_point_count)
        self.assertTrue(sampled_report.has_motion)
        self.assertFalse(static_report.has_motion)
        self.assertEqual("unknown", static_report.loop_likelihood)

    def test_empty_action_inventory_and_empty_action_are_valid(self) -> None:
        empty = inspect_animations((), _Scene())
        action = inspect_animations((_Action("Empty", frame_range=(3, 8)),), _Scene())

        self.assertTrue(empty.succeeded)
        self.assertTrue(action.succeeded)
        assert empty.report is not None and action.report is not None
        self.assertEqual(0, empty.report.clip_count)
        self.assertEqual(
            (3.0, 8.0), (action.report.actions[0].frame_start, action.report.actions[0].frame_end)
        )

    def test_invalid_fps_range_and_nonfinite_values_fail_closed(self) -> None:
        fps = inspect_animations((), _Scene(0, 1))
        frame_range = inspect_animations((_Action("Bad", frame_range=(5, 2)),), _Scene())
        nonfinite = inspect_animations(
            (_Action("NaN", _Curve("location", keyframes=((1, math.nan),))),),
            _Scene(),
        )

        self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", fps.findings[0].code)
        self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", frame_range.findings[0].code)
        self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", nonfinite.findings[0].code)

    def test_slot_handle_and_invalid_channel_metadata_are_handled(self) -> None:
        handle_action = _Action(
            "Handle",
            layers=(
                _Layer(
                    _Strip(
                        _HandleChannelBag(
                            7,
                            _Curve("location", keyframes=((1, 0), (2, 1))),
                        )
                    )
                ),
            ),
        )
        handled = inspect_animations((handle_action,), _Scene())
        invalid_slot = inspect_animations(
            (
                _Action(
                    "Slot",
                    layers=(_Layer(_Strip(_HandleChannelBag(-1, _Curve("location")))),),
                ),
            ),
            _Scene(),
        )
        descending = inspect_animations(
            (_Action("Descending", _Curve("value", keyframes=((2, 0), (1, 1)))),),
            _Scene(),
        )
        invalid_index = inspect_animations(
            (_Action("Index", _Curve("value", -1)),),
            _Scene(),
        )

        self.assertTrue(handled.succeeded)
        assert handled.report is not None
        self.assertEqual("slot-7", handled.report.actions[0].channels[0].slot_identifier)
        for result in (invalid_slot, descending, invalid_index):
            self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", result.findings[0].code)

    def test_duplicate_actions_and_channels_fail_closed(self) -> None:
        duplicate_actions = inspect_animations((_Action("Same"), _Action("Same")), _Scene())
        curve = _Curve("location", keyframes=((1, 0), (2, 1)))
        duplicate_channels = inspect_animations((_Action("Duplicate", curve, curve),), _Scene())

        self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", duplicate_actions.findings[0].code)
        self.assertEqual("BLENDER_ANIMATION_DATA_INVALID", duplicate_channels.findings[0].code)

    def test_unreadable_input_is_sanitized(self) -> None:
        result = inspect_animations(_Unreadable(), _Scene())

        self.assertEqual("BLENDER_ANIMATION_INPUT_INVALID", result.findings[0].code)
        self.assertNotIn("private", result.findings[0].explanation)
        self.assertTrue(result.findings[0].as_protocol_value()["blocksRelease"])


if __name__ == "__main__":
    unittest.main()
