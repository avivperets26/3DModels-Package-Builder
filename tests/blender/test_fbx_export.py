"""PB-0415 selected-content normalized FBX export tests."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.case_inference import (  # noqa: E402
    RIGGED,
    RIGGED_ANIMATED,
    STATIC,
)
from package_builder_blender.fbx_export import (  # noqa: E402
    NormalizedFbxExportPlan,
    export_normalized_fbx,
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

    def select_get(self) -> bool:
        return self._selected

    def select_set(self, value: bool) -> None:
        self._selected = value

    def hide_get(self) -> bool:
        return self._hidden

    def hide_set(self, value: bool) -> None:
        self._hidden = value


class FbxExportTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0415"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.output_root = Path(self.workspace.name)

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def _view_layer(self, active=None):
        return SimpleNamespace(objects=SimpleNamespace(active=active))

    def _plan(self, product_case: str, selected: tuple[str, ...], actions=()):
        return NormalizedFbxExportPlan(
            "SilverwingTalonbow",
            product_case,
            self.output_root,
            f"SilverwingTalonbow-{product_case}.fbx",
            selected,
            ("M_SilverwingTalonbow_URP",),
            actions,
        )

    def _successful_exporter(self, calls):
        def exporter(**options):
            calls.append(options)
            Path(options["filepath"]).write_bytes(b"normalized-fbx-fixture")
            return {"FINISHED"}

        return exporter

    def test_exports_static_rigged_and_animated_fixtures_with_exact_contents(self) -> None:
        cases = (
            (STATIC, (_Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",)),), ()),
            (
                RIGGED,
                (
                    _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",)),
                    _Object("Rig", "ARMATURE"),
                ),
                (),
            ),
            (
                RIGGED_ANIMATED,
                (
                    _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",)),
                    _Object("Rig", "ARMATURE"),
                ),
                ("A_SilverwingTalonbow_Shoot",),
            ),
        )
        for product_case, objects, action_names in cases:
            with self.subTest(product_case=product_case):
                calls = []
                actions = tuple(SimpleNamespace(name=name) for name in action_names)
                plan = self._plan(product_case, tuple(item.name for item in objects), action_names)
                result = export_normalized_fbx(
                    objects,
                    actions,
                    self._view_layer(),
                    plan,
                    self._successful_exporter(calls),
                )
                self.assertTrue(result.succeeded)
                assert result.report is not None
                self.assertEqual(len(b"normalized-fbx-fixture"), result.report.byte_count)
                self.assertEqual(product_case == RIGGED_ANIMATED, calls[0]["bake_anim"])
                self.assertEqual(
                    product_case == RIGGED_ANIMATED, calls[0]["bake_anim_use_all_actions"]
                )
                self.assertTrue(calls[0]["use_selection"])
                self.assertEqual({"ARMATURE", "MESH"}, calls[0]["object_types"])
                self.assertTrue(calls[0]["use_armature_deform_only"])
                self.assertFalse(calls[0]["add_leaf_bones"])
                self.assertFalse(calls[0]["bake_space_transform"])
                self.assertEqual(0.0, calls[0]["bake_anim_simplify_factor"])
                self.assertEqual("RELATIVE", calls[0]["path_mode"])

    def test_selection_is_exact_and_restored_after_success(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",))
        camera = _Object("Camera", "CAMERA")
        camera.select_set(True)
        layer = self._view_layer(camera)
        observed = []

        def exporter(**options):
            observed.append((mesh.select_get(), camera.select_get(), layer.objects.active))
            Path(options["filepath"]).write_bytes(b"fbx")
            return {"FINISHED"}

        result = export_normalized_fbx(
            (mesh, camera), (), layer, self._plan(STATIC, ("P_Model",)), exporter
        )
        self.assertTrue(result.succeeded)
        self.assertEqual((True, False, mesh), observed[0])
        self.assertFalse(mesh.select_get())
        self.assertTrue(camera.select_get())
        self.assertIs(camera, layer.objects.active)

    def test_rejects_case_content_material_action_axis_and_path_mismatches(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",))
        rig = _Object("Rig", "ARMATURE")
        cases = (
            ((mesh, rig), (), self._plan(STATIC, ("P_Model", "Rig"))),
            ((mesh,), (), self._plan(RIGGED, ("P_Model",))),
            ((mesh, rig), (), self._plan(RIGGED_ANIMATED, ("P_Model", "Rig"))),
            ((mesh,), (), self._plan(STATIC, ("Missing",))),
            (
                (mesh,),
                (),
                NormalizedFbxExportPlan(
                    "SilverwingTalonbow",
                    STATIC,
                    self.output_root,
                    "SilverwingTalonbow.fbx",
                    ("P_Model",),
                    ("Wrong",),
                ),
            ),
            (
                (mesh,),
                (),
                NormalizedFbxExportPlan(
                    "SilverwingTalonbow",
                    STATIC,
                    self.output_root,
                    "SilverwingTalonbow.fbx",
                    ("P_Model",),
                    ("M_SilverwingTalonbow_URP",),
                    axis_forward="-Z",
                    axis_up="Z",
                ),
            ),
        )
        for objects, actions, plan in cases:
            called: list[object] = []

            def capture(_called=called, **options):
                _called.append(options)
                return {"FINISHED"}

            result = export_normalized_fbx(objects, actions, self._view_layer(), plan, capture)
            self.assertFalse(result.succeeded)
            self.assertEqual([], called)

    def test_rejected_or_failed_export_removes_partial_file_and_restores_selection(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",))
        mesh.select_set(False)
        layer = self._view_layer()
        plan = self._plan(STATIC, ("P_Model",))

        def rejected(**options):
            Path(options["filepath"]).write_bytes(b"partial")
            return {"CANCELLED"}

        result = export_normalized_fbx((mesh,), (), layer, plan, rejected)
        self.assertFalse(result.succeeded)
        self.assertFalse((self.output_root / plan.output_filename).exists())
        self.assertFalse(mesh.select_get())

    def test_refuses_overwrite_before_invoking_blender(self) -> None:
        mesh = _Object("P_Model", "MESH", ("M_SilverwingTalonbow_URP",))
        plan = self._plan(STATIC, ("P_Model",))
        (self.output_root / plan.output_filename).write_bytes(b"existing")
        called = []
        result = export_normalized_fbx(
            (mesh,), (), self._view_layer(), plan, lambda **options: called.append(options)
        )
        self.assertFalse(result.succeeded)
        self.assertEqual([], called)
        self.assertEqual(b"existing", (self.output_root / plan.output_filename).read_bytes())


if __name__ == "__main__":
    unittest.main()
