"""PB-0410 transactional manifest naming tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.naming_normalization import (  # noqa: E402
    ACTION,
    ARMATURE,
    IMAGE,
    MATERIAL,
    MESH,
    OBJECT,
    BlenderNamingPlan,
    ExportedAssetName,
    NamingAssignment,
    normalize_blender_names,
)


class _Block:
    def __init__(self, name: str, reject_once: str | None = None) -> None:
        self._name = name
        self._reject_once = reject_once

    @property
    def name(self) -> str:
        return self._name

    @name.setter
    def name(self, value: str) -> None:
        if value == self._reject_once:
            self._reject_once = None
            self._name = value + ".001"
        else:
            self._name = value


def _data(*, rejected: str | None = None):
    return {
        OBJECT: (_Block("Object"),),
        MESH: (_Block("Mesh"),),
        ARMATURE: (_Block("Rig"),),
        MATERIAL: (_Block("Material"),),
        IMAGE: (_Block("Image"),),
        ACTION: (_Block("Action", rejected),),
    }


def _plan(**changes) -> BlenderNamingPlan:
    assignments = (
        NamingAssignment(OBJECT, "Object", "SilverwingTalonbow"),
        NamingAssignment(MESH, "Mesh", "MS_SilverwingTalonbow"),
        NamingAssignment(ARMATURE, "Rig", "SKEL_SilverwingTalonbow"),
        NamingAssignment(MATERIAL, "Material", "M_SilverwingTalonbow_URP"),
        NamingAssignment(IMAGE, "Image", "T_SilverwingTalonbow_Albedo"),
        NamingAssignment(ACTION, "Action", "A_SilverwingTalonbow_Shoot"),
    )
    values = {
        "asset_id": "SilverwingTalonbow",
        "folder_name": "Silverwing_Talonbow",
        "assignments": assignments,
        "exported_assets": (
            ExportedAssetName("portable-fbx", "SilverwingTalonbow.fbx"),
            ExportedAssetName("rigged-glb", "Silverwing_Talonbow_rigged.glb"),
        ),
    }
    values.update(changes)
    return BlenderNamingPlan(**values)


class NamingNormalizationTests(unittest.TestCase):
    def test_applies_all_names_and_export_plan_deterministically(self) -> None:
        data = _data()
        result = normalize_blender_names(data, _plan())
        self.assertTrue(result.succeeded)
        self.assertEqual(
            {
                OBJECT: "SilverwingTalonbow",
                MESH: "MS_SilverwingTalonbow",
                ARMATURE: "SKEL_SilverwingTalonbow",
                MATERIAL: "M_SilverwingTalonbow_URP",
                IMAGE: "T_SilverwingTalonbow_Albedo",
                ACTION: "A_SilverwingTalonbow_Shoot",
            },
            {category: blocks[0].name for category, blocks in data.items()},
        )
        assert result.report is not None
        self.assertEqual(
            ("portable-fbx", "rigged-glb"),
            tuple(item.role for item in result.report.exported_assets),
        )

    def test_handles_two_way_name_swap_without_blender_suffixes(self) -> None:
        data = _data()
        data[MATERIAL] = (_Block("One"), _Block("M_SilverwingTalonbow_One"))
        assignments = (
            *(item for item in _plan().assignments if item.category != MATERIAL),
            NamingAssignment(MATERIAL, "One", "M_SilverwingTalonbow_One"),
            NamingAssignment(MATERIAL, "M_SilverwingTalonbow_One", "M_SilverwingTalonbow_Two"),
        )
        result = normalize_blender_names(data, _plan(assignments=assignments))
        self.assertTrue(result.succeeded)
        self.assertEqual(
            ("M_SilverwingTalonbow_One", "M_SilverwingTalonbow_Two"),
            tuple(item.name for item in data[MATERIAL]),
        )

    def test_rejects_collision_before_modification(self) -> None:
        data = _data()
        data[MESH] = (_Block("Mesh"), _Block("Mesh2"))
        assignments = (
            *_plan().assignments,
            NamingAssignment(MESH, "Mesh2", "MS_SilverwingTalonbow"),
        )
        result = normalize_blender_names(data, _plan(assignments=assignments))
        self.assertFalse(result.succeeded)
        self.assertEqual(("Mesh", "Mesh2"), tuple(item.name for item in data[MESH]))

    def test_rejects_missing_extra_or_unknown_category(self) -> None:
        data = _data()
        del data[ACTION]
        self.assertFalse(normalize_blender_names(data, _plan()).succeeded)
        extra = _data()
        extra["curve"] = ()
        self.assertFalse(normalize_blender_names(extra, _plan()).succeeded)

        assignments = (*_plan().assignments, NamingAssignment("curve", "Curve", "Curve"))
        self.assertFalse(normalize_blender_names(_data(), _plan(assignments=assignments)).succeeded)

    def test_rejects_duplicate_source_identity_and_incomplete_mapping(self) -> None:
        duplicate = _data()
        duplicate[MESH] = (_Block("Mesh"), _Block("Mesh"))
        self.assertFalse(normalize_blender_names(duplicate, _plan()).succeeded)

        missing = tuple(item for item in _plan().assignments if item.category != ACTION)
        self.assertFalse(normalize_blender_names(_data(), _plan(assignments=missing)).succeeded)

        mismatched = tuple(
            NamingAssignment(item.category, "Unknown", item.desired_name)
            if item.category == ACTION
            else item
            for item in _plan().assignments
        )
        self.assertFalse(normalize_blender_names(_data(), _plan(assignments=mismatched)).succeeded)

    def test_rejects_noncanonical_identity_or_prefix(self) -> None:
        self.assertFalse(
            normalize_blender_names(_data(), _plan(asset_id="Silverwing_Talonbow")).succeeded
        )
        bad = tuple(
            NamingAssignment(item.category, item.source_name, "Wrong")
            if item.category == MESH
            else item
            for item in _plan().assignments
        )
        self.assertFalse(normalize_blender_names(_data(), _plan(assignments=bad)).succeeded)

    def test_rejects_unsafe_or_colliding_export_names(self) -> None:
        unsafe = (ExportedAssetName("fbx", "../SilverwingTalonbow.fbx"),)
        duplicate = (
            ExportedAssetName("one", "SilverwingTalonbow.fbx"),
            ExportedAssetName("two", "silverwingtalonbow.FBX"),
        )
        self.assertFalse(normalize_blender_names(_data(), _plan(exported_assets=unsafe)).succeeded)
        self.assertFalse(
            normalize_blender_names(_data(), _plan(exported_assets=duplicate)).succeeded
        )
        for exports in (
            (ExportedAssetName("fbx", "SilverwingTalonbow.obj"),),
            (ExportedAssetName("fbx", "Other.fbx"),),
            (
                ExportedAssetName("same", "SilverwingTalonbow.fbx"),
                ExportedAssetName("same", "Silverwing_Talonbow_rigged.glb"),
            ),
        ):
            with self.subTest(exports=exports):
                self.assertFalse(
                    normalize_blender_names(_data(), _plan(exported_assets=exports)).succeeded
                )

    def test_rolls_back_when_blender_changes_requested_name(self) -> None:
        data = _data(rejected="A_SilverwingTalonbow_Shoot")
        originals = {category: blocks[0].name for category, blocks in data.items()}
        result = normalize_blender_names(data, _plan())
        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_NAMING_APPLY_FAILED", result.findings[0].code)
        self.assertEqual(originals, {category: blocks[0].name for category, blocks in data.items()})


if __name__ == "__main__":
    unittest.main()
