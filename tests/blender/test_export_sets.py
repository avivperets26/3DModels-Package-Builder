"""PB-0412 cleanup and selection-safe export-set tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.export_sets import (  # noqa: E402
    ExportSetPlan,
    SelectionSafeExport,
    prepare_export_set,
)


class _Object:
    def __init__(self, name: str, object_type: str, *, hidden: bool = False) -> None:
        self.name = name
        self.type = object_type
        self._selected = False
        self._hidden = hidden
        self.hide_viewport = hidden
        self.hide_render = hidden

    def select_get(self) -> bool:
        return self._selected

    def select_set(self, value: bool) -> None:
        self._selected = value

    def hide_get(self) -> bool:
        return self._hidden

    def hide_set(self, value: bool) -> None:
        self._hidden = value


class _Data:
    def __init__(self, objects: list[_Object], orphan_count: int = 3) -> None:
        self.objects = objects
        self.orphan_count = orphan_count
        self.removed: tuple[_Object, ...] = ()
        self.purge_arguments = None

    def batch_remove(self, *, ids) -> None:
        self.removed = tuple(ids)
        self.objects[:] = [item for item in self.objects if item not in self.removed]

    def orphans_purge(self, **arguments) -> int:
        self.purge_arguments = arguments
        return self.orphan_count


class ExportSetTests(unittest.TestCase):
    def test_removes_every_non_manifest_object_and_purges_only_local_orphans(self) -> None:
        data = _Data(
            [
                _Object("P_Model", "MESH"),
                _Object("SKEL_Bow", "ARMATURE"),
                _Object("Camera", "CAMERA"),
                _Object("Key", "LIGHT"),
                _Object("Backup", "MESH", hidden=True),
                _Object("Guide", "EMPTY"),
                _Object("Extra", "MESH"),
            ]
        )
        result = prepare_export_set(data, ExportSetPlan(("P_Model", "SKEL_Bow")))
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(("P_Model", "SKEL_Bow"), result.report.selected_object_names)
        self.assertEqual(5, result.report.removed_object_count)
        self.assertEqual(
            ("hidden_or_backup", "camera", "not_intended", "helper", "light"),
            tuple(item.reason for item in result.report.excluded_objects),
        )
        self.assertEqual(
            {"do_local_ids": True, "do_linked_ids": False, "do_recursive": True},
            data.purge_arguments,
        )

    def test_explicitly_retains_a_hidden_helper(self) -> None:
        data = _Data([_Object("P_Model", "MESH"), _Object("Socket", "EMPTY", hidden=True)])
        result = prepare_export_set(data, ExportSetPlan(("P_Model",), ("Socket",)))
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(("Socket",), result.report.retained_helper_names)
        self.assertEqual({"P_Model", "Socket"}, {item.name for item in data.objects})

    def test_rejects_missing_duplicate_overlap_or_non_content_intended_objects(self) -> None:
        objects = [_Object("P_Model", "MESH"), _Object("Camera", "CAMERA")]
        for plan in (
            ExportSetPlan(()),
            ExportSetPlan(("Missing",)),
            ExportSetPlan(("P_Model", "P_Model")),
            ExportSetPlan(("P_Model",), ("P_Model",)),
            ExportSetPlan(("Camera",)),
        ):
            result = prepare_export_set(_Data(list(objects)), plan)
            self.assertFalse(result.succeeded)

    def test_selection_guard_selects_exact_set_and_prefers_armature_active(self) -> None:
        mesh = _Object("P_Model", "MESH", hidden=True)
        rig = _Object("SKEL_Bow", "ARMATURE")
        camera = _Object("Camera", "CAMERA")
        camera.select_set(True)
        active = SimpleNamespace(active=camera)
        view_layer = SimpleNamespace(objects=active)
        with SelectionSafeExport(
            (mesh, rig, camera), view_layer, ("P_Model", "SKEL_Bow")
        ) as selected:
            self.assertEqual((rig, mesh), selected)
            self.assertTrue(mesh.select_get())
            self.assertTrue(rig.select_get())
            self.assertFalse(camera.select_get())
            self.assertFalse(mesh.hide_get())
            self.assertFalse(mesh.hide_render)
            self.assertIs(rig, active.active)
        self.assertFalse(mesh.select_get())
        self.assertFalse(rig.select_get())
        self.assertTrue(camera.select_get())
        self.assertTrue(mesh.hide_get())
        self.assertTrue(mesh.hide_viewport)
        self.assertTrue(mesh.hide_render)
        self.assertIs(camera, active.active)

    def test_selection_guard_restores_state_when_exporter_raises(self) -> None:
        mesh = _Object("P_Model", "MESH")
        camera = _Object("Camera", "CAMERA")
        camera.select_set(True)
        view_layer = SimpleNamespace(objects=SimpleNamespace(active=camera))
        with (
            self.assertRaisesRegex(RuntimeError, "export failed"),
            SelectionSafeExport((mesh, camera), view_layer, ("P_Model",)),
        ):
            raise RuntimeError("export failed")
        self.assertFalse(mesh.select_get())
        self.assertTrue(camera.select_get())
        self.assertIs(camera, view_layer.objects.active)


if __name__ == "__main__":
    unittest.main()
