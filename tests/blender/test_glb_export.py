"""PB-0416 embedded-texture normalized GLB export tests."""

from __future__ import annotations

import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path
from types import SimpleNamespace

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.case_inference import (  # noqa: E402
    RIGGED,
    RIGGED_ANIMATED,
    STATIC,
)
from package_builder_blender.glb_export import (  # noqa: E402
    NormalizedGlbExportPlan,
    export_normalized_glb,
)
from package_builder_blender.texture_inspection import (  # noqa: E402
    ImageTextureReport,
    TextureInspectionReport,
)


class _Object:
    def __init__(self, name: str, object_type: str, materials=()) -> None:
        self.name = name
        self.type = object_type
        self.material_slots = tuple(
            SimpleNamespace(material=SimpleNamespace(name=value)) for value in materials
        )
        self._selected = False
        self._hidden = False
        self.hide_viewport = False
        self.hide_render = False

    def select_get(self) -> bool:
        return self._selected

    def select_set(self, value: bool) -> None:
        self._selected = value

    def hide_get(self) -> bool:
        return self._hidden

    def hide_set(self, value: bool) -> None:
        self._hidden = value


def _glb_bytes() -> bytes:
    payload = b"{}  "
    total = 20 + len(payload)
    return (
        b"glTF"
        + (2).to_bytes(4, "little")
        + total.to_bytes(4, "little")
        + len(payload).to_bytes(4, "little")
        + b"JSON"
        + payload
    )


def _texture_report(name: str = "T_Bow_Albedo") -> TextureInspectionReport:
    image = ImageTextureReport(
        name,
        1024,
        1024,
        "PNG",
        "sRGB",
        "srgb",
        "packed",
        f"{name}.png",
        4096,
        1,
    )
    return TextureInspectionReport((image,), (), 1, 1, 0, 1)


class GlbExportTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0416"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.output_root = Path(self.workspace.name)

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def _plan(self, product_case: str, objects: tuple[str, ...], actions=()):
        return NormalizedGlbExportPlan(
            "Bow",
            product_case,
            self.output_root,
            f"Bow-{product_case}.glb",
            objects,
            ("M_Bow_URP",),
            ("T_Bow_Albedo",),
            actions,
            "Copyright 2026 Example Publisher",
        )

    def _view_layer(self, active=None):
        return SimpleNamespace(objects=SimpleNamespace(active=active))

    def test_static_rigged_and_animated_fixtures_embed_intended_content(self) -> None:
        cases = (
            (STATIC, (_Object("P_Model", "MESH", ("M_Bow_URP",)),), ()),
            (
                RIGGED,
                (_Object("P_Model", "MESH", ("M_Bow_URP",)), _Object("Rig", "ARMATURE")),
                (),
            ),
            (
                RIGGED_ANIMATED,
                (_Object("P_Model", "MESH", ("M_Bow_URP",)), _Object("Rig", "ARMATURE")),
                ("A_Bow_Shoot",),
            ),
        )
        for product_case, objects, action_names in cases:
            with self.subTest(product_case=product_case):
                calls = []

                def exporter(_calls=calls, **options):
                    _calls.append(options)
                    Path(options["filepath"]).write_bytes(_glb_bytes())
                    return {"FINISHED"}

                result = export_normalized_glb(
                    objects,
                    (SimpleNamespace(name="T_Bow_Albedo"),),
                    tuple(SimpleNamespace(name=name) for name in action_names),
                    _texture_report(),
                    self._view_layer(),
                    self._plan(product_case, tuple(item.name for item in objects), action_names),
                    exporter,
                )
                self.assertTrue(result.succeeded)
                assert result.report is not None
                self.assertEqual(len(_glb_bytes()), result.report.byte_count)
                self.assertEqual(("T_Bow_Albedo",), result.report.image_names)
                self.assertEqual("GLB", calls[0]["export_format"])
                self.assertEqual("EXPORT", calls[0]["export_materials"])
                self.assertEqual("AUTO", calls[0]["export_image_format"])
                self.assertFalse(calls[0]["export_unused_images"])
                self.assertFalse(calls[0]["export_use_gltfpack"])
                self.assertTrue(calls[0]["use_selection"])
                self.assertTrue(calls[0]["export_def_bones"])
                self.assertFalse(calls[0]["export_leaf_bone"])
                self.assertNotIn("export_action_filter", calls[0])
                self.assertEqual(product_case == RIGGED_ANIMATED, calls[0]["export_animations"])

    def test_selection_and_hide_state_restore_after_export(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        camera = _Object("Camera", "CAMERA")
        mesh._hidden = True
        mesh.hide_viewport = True
        mesh.hide_render = True
        camera.select_set(True)
        layer = self._view_layer(camera)
        observed = []

        def exporter(**options):
            observed.append(
                (
                    mesh.select_get(),
                    camera.select_get(),
                    mesh.hide_get(),
                    mesh.hide_render,
                    layer.objects.active,
                )
            )
            Path(options["filepath"]).write_bytes(_glb_bytes())
            return {"FINISHED"}

        result = export_normalized_glb(
            (mesh, camera),
            (SimpleNamespace(name="T_Bow_Albedo"),),
            (),
            _texture_report(),
            layer,
            self._plan(STATIC, ("P_Model",)),
            exporter,
        )
        self.assertTrue(result.succeeded)
        self.assertEqual((True, False, False, False, mesh), observed[0])
        self.assertFalse(mesh.select_get())
        self.assertTrue(camera.select_get())
        self.assertTrue(mesh.hide_get())
        self.assertTrue(mesh.hide_viewport)
        self.assertTrue(mesh.hide_render)
        self.assertIs(camera, layer.objects.active)

    def test_missing_unconnected_or_unexpected_texture_blocks_before_export(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        unconnected = _texture_report()
        unconnected_image = replace(unconnected.images[0], material_connection_count=0)
        cases = (
            ((), _texture_report()),
            ((SimpleNamespace(name="Other"),), _texture_report()),
            (
                (SimpleNamespace(name="T_Bow_Albedo"),),
                TextureInspectionReport((unconnected_image,), (), 1, 1, 0, 0),
            ),
        )
        for images, report in cases:
            calls = []
            result = export_normalized_glb(
                (mesh,),
                images,
                (),
                report,
                self._view_layer(),
                self._plan(STATIC, ("P_Model",)),
                lambda _calls=calls, **options: _calls.append(options),
            )
            self.assertFalse(result.succeeded)
            self.assertEqual([], calls)

    def test_rejects_corrupt_or_cancelled_glb_and_removes_partial_output(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        plan = self._plan(STATIC, ("P_Model",))
        unaligned = (
            b"glTF"
            + (2).to_bytes(4, "little")
            + (21).to_bytes(4, "little")
            + (1).to_bytes(4, "little")
            + b"JSON{"
        )
        for content, operator_result in (
            (b"not-glb", {"FINISHED"}),
            (b"bad!" + bytes(16), {"FINISHED"}),
            (unaligned, {"FINISHED"}),
            (_glb_bytes(), {"CANCELLED"}),
        ):
            with self.subTest(operator_result=operator_result):

                def exporter(_content=content, _operator_result=operator_result, **options):
                    Path(options["filepath"]).write_bytes(_content)
                    return _operator_result

                result = export_normalized_glb(
                    (mesh,),
                    (SimpleNamespace(name="T_Bow_Albedo"),),
                    (),
                    _texture_report(),
                    self._view_layer(),
                    plan,
                    exporter,
                )
                self.assertFalse(result.succeeded)
                self.assertFalse((self.output_root / plan.output_filename).exists())

    def test_refuses_existing_outside_or_noncanonical_output(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        valid = self._plan(STATIC, ("P_Model",))
        (self.output_root / valid.output_filename).write_bytes(b"existing")
        invalid = (
            valid,
            NormalizedGlbExportPlan(
                "Bow",
                STATIC,
                self.output_root,
                "../Bow.glb",
                ("P_Model",),
                ("M_Bow_URP",),
                ("T_Bow_Albedo",),
            ),
            NormalizedGlbExportPlan(
                "Bow",
                STATIC,
                Path("relative"),
                "Bow.glb",
                ("P_Model",),
                ("M_Bow_URP",),
                ("T_Bow_Albedo",),
            ),
        )
        for plan in invalid:
            calls = []
            result = export_normalized_glb(
                (mesh,),
                (SimpleNamespace(name="T_Bow_Albedo"),),
                (),
                _texture_report(),
                self._view_layer(),
                plan,
                lambda _calls=calls, **options: _calls.append(options),
            )
            self.assertFalse(result.succeeded)
            self.assertEqual([], calls)

    def test_case_shape_and_missing_material_slot_fail_before_export(self) -> None:
        missing_material = _Object("P_Model", "MESH", ("M_Bow_URP",))
        missing_material.material_slots = (SimpleNamespace(material=None),)
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        rig = _Object("Rig", "ARMATURE")
        helper = _Object("Helper", "EMPTY")
        cases = (
            ((missing_material,), self._plan(STATIC, ("P_Model",)), ()),
            ((helper,), self._plan(STATIC, ("Helper",)), ()),
            ((mesh,), self._plan(STATIC, ("Missing",)), ()),
            ((mesh, rig), self._plan(STATIC, ("P_Model", "Rig")), ()),
            ((mesh,), self._plan(RIGGED, ("P_Model",)), ()),
            ((mesh, rig), self._plan(RIGGED_ANIMATED, ("P_Model", "Rig")), ()),
        )
        for objects, plan, actions in cases:
            with self.subTest(plan=plan, objects=objects):
                calls = []
                result = export_normalized_glb(
                    objects,
                    (SimpleNamespace(name="T_Bow_Albedo"),),
                    actions,
                    _texture_report(),
                    self._view_layer(),
                    plan,
                    lambda _calls=calls, **options: _calls.append(options),
                )
                self.assertFalse(result.succeeded)
                self.assertEqual("BLENDER_GLB_EXPORT_PLAN_INVALID", result.findings[0].code)
                self.assertEqual([], calls)

    def test_partial_output_cleanup_failure_is_reported(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_Bow_URP",))
        plan = self._plan(STATIC, ("P_Model",))

        def exporter(**options):
            Path(options["filepath"]).mkdir()
            return {"FINISHED"}

        result = export_normalized_glb(
            (mesh,),
            (SimpleNamespace(name="T_Bow_Albedo"),),
            (),
            _texture_report(),
            self._view_layer(),
            plan,
            exporter,
        )

        self.assertEqual(
            ("BLENDER_GLB_EXPORT_FAILED", "BLENDER_GLB_EXPORT_CLEANUP_FAILED"),
            tuple(finding.code for finding in result.findings),
        )
        (self.output_root / plan.output_filename).rmdir()


if __name__ == "__main__":
    unittest.main()
