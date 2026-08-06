"""PB-0413 canonical separate-map normalization tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.material_normalization import (  # noqa: E402
    MaterialImageNormalizationPlan,
    TextureNormalizationAssignment,
    normalize_material_images,
)
from package_builder_blender.texture_inspection import (  # noqa: E402
    ImageTextureReport,
    MaterialTextureConnectionReport,
    TextureInspectionReport,
)


class _Image:
    def __init__(self, name: str, *, reject_color: str | None = None) -> None:
        self.name = name
        self.filepath_raw = f"C:/source/{name}.png"
        self.size = (1024, 1024)
        self.file_format = "PNG"
        self.colorspace_settings = _ColorSpace("sRGB", reject_color)


class _ColorSpace:
    def __init__(self, value: str, rejected: str | None = None) -> None:
        self._name = value
        self._rejected = rejected

    @property
    def name(self) -> str:
        return self._name

    @name.setter
    def name(self, value: str) -> None:
        if value == self._rejected:
            self._rejected = None
            self._name = value + " rejected"
        else:
            self._name = value


def _inspection(*roles: tuple[str, str]) -> TextureInspectionReport:
    names = tuple(dict(roles))
    images = tuple(
        ImageTextureReport(name, 1024, 1024, "PNG", "sRGB", "srgb", "external", None, 0, 1)
        for name in names
    )
    connections = tuple(
        MaterialTextureConnectionReport("M_Bow", f"Node-{index}", name, (), role, "name_hint")
        for index, (name, role) in enumerate(roles)
    )
    return TextureInspectionReport(images, connections, len(images), 0, len(images), len(images))


class MaterialNormalizationTests(unittest.TestCase):
    def test_normalizes_separate_maps_and_correct_color_spaces(self) -> None:
        images = (_Image("Base"), _Image("Normal"), _Image("Rough"))
        plan = MaterialImageNormalizationPlan(
            "SilverwingTalonbow",
            (
                TextureNormalizationAssignment("Base", "albedo", "T_SilverwingTalonbow_Albedo.png"),
                TextureNormalizationAssignment(
                    "Normal", "normal", "T_SilverwingTalonbow_Normal.png"
                ),
                TextureNormalizationAssignment(
                    "Rough", "roughness", "T_SilverwingTalonbow_Roughness.png"
                ),
            ),
        )
        result = normalize_material_images(
            images,
            _inspection(("Base", "albedo"), ("Normal", "normal"), ("Rough", "roughness")),
            plan,
        )
        self.assertTrue(result.succeeded)
        self.assertEqual("//Textures/T_SilverwingTalonbow_Albedo.png", images[0].filepath_raw)
        self.assertEqual("sRGB", images[0].colorspace_settings.name)
        self.assertEqual("Non-Color", images[1].colorspace_settings.name)
        self.assertEqual("Non-Color", images[2].colorspace_settings.name)
        self.assertEqual((1024, 1024), images[0].size)

    def test_ambiguous_unknown_or_contradictory_role_blocks_by_default(self) -> None:
        image = _Image("Map")
        plan = MaterialImageNormalizationPlan(
            "Bow",
            (TextureNormalizationAssignment("Map", "normal", "T_Bow_Normal.png"),),
        )
        for role in ("ambiguous", "unknown", "albedo"):
            result = normalize_material_images((image,), _inspection(("Map", role)), plan)
            self.assertFalse(result.succeeded)
            self.assertEqual("BLENDER_TEXTURE_ROLE_AMBIGUOUS", result.findings[0].code)

    def test_explicit_manifest_override_resolves_reviewed_ambiguity(self) -> None:
        image = _Image("Reviewed")
        plan = MaterialImageNormalizationPlan(
            "Bow",
            (TextureNormalizationAssignment("Reviewed", "metallic", "T_Bow_Metallic.png", True),),
        )
        result = normalize_material_images((image,), _inspection(("Reviewed", "ambiguous")), plan)
        self.assertTrue(result.succeeded)

    def test_rejects_combined_orm_unsafe_colliding_or_incomplete_plans(self) -> None:
        images = (_Image("One"), _Image("Two"))
        inspection = _inspection(("One", "metallic"), ("Two", "roughness"))
        invalid = (
            MaterialImageNormalizationPlan(
                "Bow",
                (TextureNormalizationAssignment("One", "metallic", "T_Bow_Metallic_ORM.png"),),
            ),
            MaterialImageNormalizationPlan(
                "Bow",
                (
                    TextureNormalizationAssignment("One", "metallic", "../T_Bow_Metallic.png"),
                    TextureNormalizationAssignment("Two", "roughness", "T_Bow_Roughness.png"),
                ),
            ),
            MaterialImageNormalizationPlan(
                "Bow",
                (
                    TextureNormalizationAssignment("One", "metallic", "T_Bow_Metallic.png"),
                    TextureNormalizationAssignment("Two", "roughness", "t_bow_metallic.PNG"),
                ),
            ),
        )
        for plan in invalid:
            result = normalize_material_images(images, inspection, plan)
            self.assertFalse(result.succeeded)

    def test_rolls_back_all_references_when_blender_rejects_a_value(self) -> None:
        albedo = _Image("Base")
        normal = _Image("Normal", reject_color="Non-Color")
        original = tuple(
            (item.filepath_raw, item.colorspace_settings.name) for item in (albedo, normal)
        )
        plan = MaterialImageNormalizationPlan(
            "Bow",
            (
                TextureNormalizationAssignment("Base", "albedo", "T_Bow_Albedo.png"),
                TextureNormalizationAssignment("Normal", "normal", "T_Bow_Normal.png"),
            ),
        )
        result = normalize_material_images(
            (albedo, normal), _inspection(("Base", "albedo"), ("Normal", "normal")), plan
        )
        self.assertFalse(result.succeeded)
        self.assertEqual(
            original,
            tuple((item.filepath_raw, item.colorspace_settings.name) for item in (albedo, normal)),
        )


if __name__ == "__main__":
    unittest.main()
