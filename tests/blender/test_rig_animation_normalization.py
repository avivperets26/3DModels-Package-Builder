"""PB-0414 rig/action naming, baking, and clip-boundary tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.rig_animation_normalization import (  # noqa: E402
    ClipBakePlan,
    RigAnimationNormalizationPlan,
    normalize_rig_animation,
)


class _Point:
    def __init__(self, frame: float, value: float) -> None:
        self.co = (frame, value)


class _Curve:
    def __init__(self, frames=(1.0, 5.0), values=(0.0, 2.0)) -> None:
        self.data_path = 'pose.bones["String"].location'
        self.array_index = 0
        self.keyframe_points = [
            _Point(frame, value) for frame, value in zip(frames, values, strict=True)
        ]
        self.sampled_points = []
        self.modifiers = []


class _Action:
    def __init__(self, name: str, *, moving: bool = True) -> None:
        self.name = name
        self.fcurves = [_Curve(values=(0.0, 2.0) if moving else (0.0, 0.0))]
        self.layers = []
        self.frame_range = (1.0, 5.0)


class _Rig:
    def __init__(self) -> None:
        root = SimpleNamespace(name="Root", use_deform=True)
        string = SimpleNamespace(name="String", use_deform=True)
        control = SimpleNamespace(name="Control", use_deform=False)
        self.name = "RigObject"
        self.type = "ARMATURE"
        self.data = SimpleNamespace(name="RigData", bones=(root, string, control))
        self.pose = SimpleNamespace(
            bones=tuple(
                SimpleNamespace(name=bone.name, bone=SimpleNamespace(select=False))
                for bone in (root, string, control)
            )
        )
        self.animation_data = SimpleNamespace(action=None)


def _scene():
    return SimpleNamespace(
        frame_start=0,
        frame_end=250,
        render=SimpleNamespace(fps=30, fps_base=1.0),
    )


def _plan(**changes) -> RigAnimationNormalizationPlan:
    values = {
        "armature_object_name": "RigObject",
        "skeleton_name": "SKEL_SilverwingTalonbow",
        "clips": (ClipBakePlan("Shoot", "A_SilverwingTalonbow_Shoot", 1, 5, 2),),
        "bake_animation": True,
        "deform_bones_only": True,
    }
    values.update(changes)
    return RigAnimationNormalizationPlan(**values)


class RigAnimationNormalizationTests(unittest.TestCase):
    def test_applies_reviewed_blender_five_bake_policy_and_preserves_motion(self) -> None:
        rig = _Rig()
        action = _Action("Shoot")
        scene = _scene()
        calls = []

        def bake(**arguments):
            calls.append(arguments)
            action.fcurves[0] = _Curve((1.0, 3.0, 5.0), (0.0, 1.0, 2.0))
            return {"FINISHED"}

        result = normalize_rig_animation(rig, (action,), scene, _plan(), bake)
        self.assertTrue(result.succeeded)
        self.assertEqual("SKEL_SilverwingTalonbow", rig.data.name)
        self.assertEqual("A_SilverwingTalonbow_Shoot", action.name)
        self.assertEqual(
            {
                "frame_start": 1,
                "frame_end": 5,
                "step": 2,
                "only_selected": True,
                "visual_keying": True,
                "clear_constraints": False,
                "clear_parents": False,
                "use_current_action": True,
                "clean_curves": False,
                "bake_types": {"POSE"},
                "channel_types": {"BBONE", "LOCATION", "PROPS", "ROTATION", "SCALE"},
            },
            calls[0],
        )
        assert result.report is not None
        self.assertEqual(("Root", "String"), result.report.deform_bone_names)
        self.assertTrue(result.report.clips[0].has_motion)
        self.assertEqual((0, 250), (scene.frame_start, scene.frame_end))
        self.assertIsNone(rig.animation_data.action)
        self.assertFalse(any(item.bone.select for item in rig.pose.bones))

    def test_non_bake_policy_still_verifies_exact_existing_boundaries(self) -> None:
        result = normalize_rig_animation(
            _Rig(),
            (_Action("Shoot"),),
            _scene(),
            _plan(bake_animation=False, clips=(ClipBakePlan("Shoot", "A_Bow_Shoot", 1, 5),)),
            lambda **_arguments: self.fail("baker must not run"),
        )
        self.assertTrue(result.succeeded)

    def test_rejects_incomplete_action_inventory_bad_bounds_or_motionless_clip(self) -> None:
        for actions, plan in (
            ((_Action("Shoot"), _Action("Idle")), _plan()),
            ((_Action("Shoot"),), _plan(clips=(ClipBakePlan("Shoot", "BadName", 1, 5),))),
            ((_Action("Shoot"),), _plan(clips=(ClipBakePlan("Shoot", "A_Bow", -1, 5),))),
            ((_Action("Shoot", moving=False),), _plan()),
        ):
            result = normalize_rig_animation(
                _Rig(), actions, _scene(), plan, lambda **_kw: {"FINISHED"}
            )
            self.assertFalse(result.succeeded)

    def test_rejects_missing_deform_bones(self) -> None:
        rig = _Rig()
        for bone in rig.data.bones:
            bone.use_deform = False
        result = normalize_rig_animation(
            rig, (_Action("Shoot"),), _scene(), _plan(), lambda **_kw: {"FINISHED"}
        )
        self.assertFalse(result.succeeded)

    def test_failed_bake_restores_scene_selection_assignment_and_names(self) -> None:
        rig = _Rig()
        action = _Action("Shoot")
        previous = _Action("Previous")
        scene = _scene()
        rig.animation_data.action = previous
        rig.pose.bones[2].bone.select = True
        result = normalize_rig_animation(
            rig, (action,), scene, _plan(), lambda **_arguments: {"CANCELLED"}
        )
        self.assertFalse(result.succeeded)
        self.assertEqual("RigData", rig.data.name)
        self.assertEqual("Shoot", action.name)
        self.assertEqual((0, 250), (scene.frame_start, scene.frame_end))
        self.assertIs(previous, rig.animation_data.action)
        self.assertEqual((False, False, True), tuple(item.bone.select for item in rig.pose.bones))


if __name__ == "__main__":
    unittest.main()
