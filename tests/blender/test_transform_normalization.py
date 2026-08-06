"""PB-0411 unit, axis, pivot, transform, and deformation-preservation tests."""

from __future__ import annotations

import math
import sys
import types
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.transform_normalization import (  # noqa: E402
    BOUNDS_BASE,
    BOUNDS_CENTER,
    KEEP,
    TransformNormalizationPlan,
    normalize_transforms,
)

IDENTITY = (
    (1.0, 0.0, 0.0, 0.0),
    (0.0, 1.0, 0.0, 0.0),
    (0.0, 0.0, 1.0, 0.0),
    (0.0, 0.0, 0.0, 1.0),
)


class _Membership:
    def __init__(self, group: int, weight: float) -> None:
        self.group = group
        self.weight = weight


class _Vertex:
    def __init__(self) -> None:
        self.groups = (_Membership(0, 1.0),)


class _Data:
    def __init__(self) -> None:
        self.vertices = (_Vertex(),)
        self.bones = ()


class _Object:
    def __init__(self, name: str = "Bow") -> None:
        self.name = name
        self.type = "MESH"
        self.matrix_world = IDENTITY
        self.bound_box = tuple(
            (x, y, z) for x in (-1.0, 1.0) for y in (-2.0, 2.0) for z in (0.0, 4.0)
        )
        self.data = _Data()


class _OriginObject:
    def __init__(self) -> None:
        self.name = "Empty"
        self.type = "EMPTY"
        self.matrix_world = IDENTITY
        self.data = None


class _Bone:
    def __init__(self) -> None:
        self.name = "Root"
        self.parent = None
        self.use_deform = True
        self.matrix_local = IDENTITY


class _ArmatureObject(_OriginObject):
    def __init__(self) -> None:
        super().__init__()
        self.name = "Rig"
        self.type = "ARMATURE"
        self.data = type("ArmatureData", (), {"bones": (_Bone(),), "vertices": ()})()


class _Point:
    def __init__(self, frame: float, value: float) -> None:
        self.co = (frame, value)


class _Curve:
    def __init__(self) -> None:
        self.data_path = "location"
        self.array_index = 0
        self.keyframe_points = (_Point(1.0, 0.0), _Point(2.0, 1.0))
        self.sampled_points = ()


class _Action:
    def __init__(self) -> None:
        self.name = "Shoot"
        self.fcurves = (_Curve(),)
        self.layers = ()


class _LayeredAction(_Action):
    def __init__(self) -> None:
        super().__init__()
        channelbag = type("ChannelBag", (), {"fcurves": self.fcurves})()
        strip = type("Strip", (), {"channelbags": (channelbag,)})()
        self.layers = (type("Layer", (), {"strips": (strip,)})(),)
        self.fcurves = ()


class _MutatingObject(_Object):
    def __init__(self) -> None:
        self._matrix_world = IDENTITY
        self._assignments = 0
        super().__init__()

    @property
    def matrix_world(self):
        return self._matrix_world

    @matrix_world.setter
    def matrix_world(self, value) -> None:
        self._matrix_world = value
        self._assignments += 1
        if self._assignments == 3 and hasattr(self, "data"):
            self.data.vertices[0].groups[0].weight = 0.5


class _UnitSettings:
    def __init__(self) -> None:
        self.system = "NONE"
        self.scale_length = 1.0


class _Scene:
    def __init__(self) -> None:
        self.unit_settings = _UnitSettings()


def _plan(*, pivot: str = KEEP, **changes) -> TransformNormalizationPlan:
    values = {
        "source_forward": "Y",
        "source_up": "Z",
        "target_forward": "Y",
        "target_up": "Z",
        "source_scale_length": 1.0,
        "target_scale_length": 1.0,
        "target_unit_system": "METRIC",
        "pivot_policy": pivot,
    }
    values.update(changes)
    return TransformNormalizationPlan(**values)


class TransformNormalizationTests(unittest.TestCase):
    def test_applies_units_and_reports_before_after_metrics(self) -> None:
        value = _Object()
        scene = _Scene()
        result = normalize_transforms(
            (value,), (), scene, _plan(source_scale_length=0.01, target_scale_length=1.0)
        )
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertAlmostEqual(0.01, result.report.unit_factor)
        self.assertEqual((2.0, 4.0, 4.0), result.report.before.dimensions)
        self.assertEqual((0.02, 0.04, 0.04), result.report.after.dimensions)
        self.assertEqual(
            ("METRIC", 1.0), (scene.unit_settings.system, scene.unit_settings.scale_length)
        )

    def test_converts_axis_basis(self) -> None:
        value = _Object()
        result = normalize_transforms(
            (value,), (), _Scene(), _plan(target_forward="-Z", target_up="Y")
        )
        self.assertTrue(result.succeeded)
        self.assertNotEqual(IDENTITY, value.matrix_world)
        assert result.report is not None
        self.assertCountEqual(result.report.before.dimensions, result.report.after.dimensions)

    def test_center_pivot_moves_bounds_center_to_origin(self) -> None:
        result = normalize_transforms((_Object(),), (), _Scene(), _plan(pivot=BOUNDS_CENTER))
        self.assertTrue(result.succeeded)
        assert result.report is not None
        center = tuple(
            (result.report.after.minimum[index] + result.report.after.maximum[index]) / 2
            for index in range(3)
        )
        self.assertEqual((0.0, 0.0, 0.0), center)

    def test_base_pivot_centers_xy_and_places_minimum_z_at_zero(self) -> None:
        result = normalize_transforms((_Object(),), (), _Scene(), _plan(pivot=BOUNDS_BASE))
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(0.0, result.report.after.minimum[2])
        self.assertEqual(
            (0.0, 0.0),
            tuple(
                (result.report.after.minimum[index] + result.report.after.maximum[index]) / 2
                for index in range(2)
            ),
        )

    def test_preserves_raw_weights(self) -> None:
        value = _Object()
        before = value.data.vertices[0].groups[0].weight
        result = normalize_transforms((value,), (), _Scene(), _plan(pivot=BOUNDS_CENTER))
        self.assertTrue(result.succeeded)
        self.assertEqual(before, value.data.vertices[0].groups[0].weight)
        self.assertTrue(result.report.deformation_preserved if result.report else False)

    def test_preserves_bone_rest_and_action_curve_data(self) -> None:
        rig = _ArmatureObject()
        action = _Action()
        result = normalize_transforms((rig,), (action,), _Scene(), _plan())
        self.assertTrue(result.succeeded)
        self.assertEqual(IDENTITY, rig.data.bones[0].matrix_local)
        self.assertEqual((2.0, 1.0), action.fcurves[0].keyframe_points[-1].co)

    def test_preserves_blender_five_layered_action_curve_data(self) -> None:
        action = _LayeredAction()
        result = normalize_transforms((_OriginObject(),), (action,), _Scene(), _plan())
        self.assertTrue(result.succeeded)
        curve = action.layers[0].strips[0].channelbags[0].fcurves[0]
        self.assertEqual((2.0, 1.0), curve.keyframe_points[-1].co)

    def test_nonmesh_origins_are_included_in_metrics(self) -> None:
        result = normalize_transforms((_OriginObject(),), (), _Scene(), _plan())
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual((1, 0), (result.report.after.object_count, result.report.after.mesh_count))

    def test_detects_deformation_change_and_rolls_back_transform_and_units(self) -> None:
        value = _MutatingObject()
        scene = _Scene()
        result = normalize_transforms((value,), (), scene, _plan(pivot=BOUNDS_CENTER))
        self.assertFalse(result.succeeded)
        self.assertEqual(IDENTITY, value.matrix_world)
        self.assertEqual(
            ("NONE", 1.0), (scene.unit_settings.system, scene.unit_settings.scale_length)
        )

    def test_hosted_mathutils_matrix_is_used_when_available(self) -> None:
        original = sys.modules.get("mathutils")

        class _Matrix:
            def __new__(cls, value):
                return tuple(tuple(row) for row in value)

        sys.modules["mathutils"] = types.SimpleNamespace(Matrix=_Matrix)
        try:
            self.assertTrue(normalize_transforms((_Object(),), (), _Scene(), _plan()).succeeded)
        finally:
            if original is None:
                del sys.modules["mathutils"]
            else:
                sys.modules["mathutils"] = original

    def test_rejects_parallel_axes_and_nonfinite_or_nonpositive_units(self) -> None:
        invalid = (
            _plan(source_forward="Z", source_up="-Z"),
            _plan(source_scale_length=0.0),
            _plan(target_scale_length=math.inf),
        )
        for plan in invalid:
            with self.subTest(plan=plan):
                value = _Object()
                result = normalize_transforms((value,), (), _Scene(), plan)
                self.assertFalse(result.succeeded)
                self.assertEqual(IDENTITY, value.matrix_world)

    def test_rejects_unknown_policy_without_mutation(self) -> None:
        value = _Object()
        scene = _Scene()
        result = normalize_transforms((value,), (), scene, _plan(pivot="cursor"))
        self.assertFalse(result.succeeded)
        self.assertEqual(IDENTITY, value.matrix_world)
        self.assertEqual(
            ("NONE", 1.0), (scene.unit_settings.system, scene.unit_settings.scale_length)
        )

    def test_rejects_empty_objects_invalid_bounds_and_projective_matrix(self) -> None:
        self.assertFalse(normalize_transforms((), (), _Scene(), _plan()).succeeded)
        invalid_bounds = _Object()
        invalid_bounds.bound_box = ((0.0, 0.0, 0.0),)
        self.assertFalse(normalize_transforms((invalid_bounds,), (), _Scene(), _plan()).succeeded)
        projective = _Object()
        projective.matrix_world = (*IDENTITY[:3], (0.0, 0.0, 0.0, 0.0))
        self.assertFalse(normalize_transforms((projective,), (), _Scene(), _plan()).succeeded)


if __name__ == "__main__":
    unittest.main()
