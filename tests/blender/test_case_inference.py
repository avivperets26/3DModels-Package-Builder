"""PB-0409 automatic product-case inference tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.case_inference import (  # noqa: E402
    ITEM_COLLECTION,
    ITEM_SET,
    RIGGED,
    RIGGED_ANIMATED,
    STATIC,
    infer_product_case,
)


def _facts(
    *, meshes: int = 1, skeletons: int = 0, skins: int = 0, actions: int = 0, moving: int = 0
):
    return (
        SimpleNamespace(mesh_count=meshes),
        SimpleNamespace(skeleton_count=skeletons, skinned_mesh_count=skins),
        SimpleNamespace(action_count=actions, animated_clip_count=moving),
    )


class ProductCaseInferenceTests(unittest.TestCase):
    def test_infers_static_model(self) -> None:
        result = infer_product_case(*_facts())
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(STATIC, result.report.detected_case)
        self.assertEqual(STATIC, result.report.resolved_case)

    def test_infers_rigged_model_when_complete_skin_exists(self) -> None:
        result = infer_product_case(*_facts(skeletons=1, skins=2))
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(RIGGED, result.report.resolved_case)

    def test_infers_animated_only_from_actual_motion(self) -> None:
        still = infer_product_case(*_facts(skeletons=1, skins=1, actions=2, moving=0))
        moving = infer_product_case(*_facts(skeletons=1, skins=1, actions=2, moving=1))
        self.assertEqual(RIGGED, still.report.resolved_case if still.report else None)
        self.assertEqual(RIGGED_ANIMATED, moving.report.resolved_case if moving.report else None)

    def test_set_and_collection_are_never_guessed(self) -> None:
        inferred = infer_product_case(*_facts(meshes=12))
        item_set = infer_product_case(*_facts(meshes=12), manifest_case=ITEM_SET)
        collection = infer_product_case(*_facts(meshes=12), manifest_case=ITEM_COLLECTION)
        self.assertEqual(STATIC, inferred.report.resolved_case if inferred.report else None)
        self.assertTrue(
            inferred.report.requires_manifest_for_grouping if inferred.report else False
        )
        self.assertTrue(
            item_set.report.requires_manifest_for_grouping if item_set.report else False
        )
        self.assertEqual(
            ITEM_COLLECTION, collection.report.resolved_case if collection.report else None
        )

    def test_manifest_single_case_must_match_facts(self) -> None:
        result = infer_product_case(*_facts(), manifest_case=RIGGED)
        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_CASE_MANIFEST_CONFLICT", result.findings[0].code)

    def test_rejects_animation_without_complete_rig(self) -> None:
        result = infer_product_case(*_facts(actions=1, moving=1))
        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_CASE_ANIMATION_WITHOUT_RIG", result.findings[0].code)

    def test_rejects_incomplete_rig(self) -> None:
        result = infer_product_case(*_facts(skeletons=1))
        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_CASE_RIG_INCOMPLETE", result.findings[0].code)

    def test_rejects_empty_malformed_or_unknown_inputs(self) -> None:
        for values in (
            _facts(meshes=0),
            (SimpleNamespace(), _facts()[1], _facts()[2]),
        ):
            with self.subTest(values=values):
                result = infer_product_case(*values)
                self.assertEqual("BLENDER_CASE_INPUT_INVALID", result.findings[0].code)
        unknown = infer_product_case(*_facts(), manifest_case="bundle")
        self.assertEqual("BLENDER_CASE_INPUT_INVALID", unknown.findings[0].code)


if __name__ == "__main__":
    unittest.main()
