"""PB-0403 deterministic FBX adapter and failure-boundary tests."""

from __future__ import annotations

import math
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.fbx_import import (  # noqa: E402
    FbxImportSettings,
    import_fbx,
)


class _Object:
    def __init__(self, object_type: str) -> None:
        self.type = object_type


class _Data:
    def __init__(self) -> None:
        self.objects: list[object] = [_Object("CAMERA")]
        self.batch_calls: list[tuple[object, ...]] = []
        self.purge_calls = 0
        self.fail_cleanup = False

    def batch_remove(self, *, ids: tuple[object, ...]) -> None:
        if self.fail_cleanup:
            raise RuntimeError("private cleanup detail")
        self.batch_calls.append(ids)
        self.objects = [value for value in self.objects if all(value is not item for item in ids)]

    def orphans_purge(self, **_options: bool) -> int:
        self.purge_calls += 1
        return 0


class _Importer:
    def __init__(self, data: _Data, object_types: tuple[str, ...]) -> None:
        self.data = data
        self.object_types = object_types
        self.calls: list[dict[str, object]] = []
        self.result: object = {"FINISHED"}
        self.error: Exception | None = None

    def __call__(self, **options: object) -> object:
        self.calls.append(options)
        self.data.objects.extend(_Object(value) for value in self.object_types)
        if self.error is not None:
            raise self.error
        return self.result


class BlenderFbxImportTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0403"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.input_root = Path(self.workspace.name) / "inputs"
        self.input_root.mkdir()

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def _source(self, name: str = "model.fbx", content: bytes = b"fixture") -> Path:
        path = self.input_root / name
        path.write_bytes(content)
        return path

    def test_static_fixture_import_records_exact_axis_and_unit_settings(self) -> None:
        data = _Data()
        importer = _Importer(data, ("MESH",))
        settings = FbxImportSettings("X", "Z", 0.01)

        result = import_fbx(data, importer, self._source("static.fbx"), self.input_root, settings)

        self.assertTrue(result.succeeded)
        self.assertEqual((), result.findings)
        self.assertIsNotNone(result.report)
        report = result.report
        assert report is not None
        self.assertEqual("static.fbx", report.source_name)
        self.assertEqual(settings, report.settings)
        self.assertEqual((1, 1, 0), (report.object_count, report.mesh_count, report.armature_count))
        self.assertEqual(1, data.purge_calls)
        self.assertEqual(1, len(data.batch_calls))
        self.assertEqual(
            {
                "filepath": str((self.input_root / "static.fbx").resolve()),
                "global_scale": 0.01,
                "use_manual_orientation": True,
                "axis_forward": "X",
                "axis_up": "Z",
                "bake_space_transform": False,
                "use_custom_normals": True,
                "colors_type": "SRGB",
                "use_image_search": False,
                "use_anim": True,
                "anim_offset": 1.0,
                "use_subsurf": False,
                "use_custom_props": False,
                "ignore_leaf_bones": False,
                "force_connect_children": False,
                "automatic_bone_orientation": False,
                "use_prepost_rot": True,
            },
            importer.calls[0],
        )

    def test_skinned_fixture_import_records_mesh_and_armature_counts(self) -> None:
        data = _Data()
        importer = _Importer(data, ("ARMATURE", "MESH", "EMPTY"))

        result = import_fbx(data, importer, self._source("skinned.FBX"), self.input_root)

        self.assertTrue(result.succeeded)
        self.assertIsNotNone(result.report)
        report = result.report
        assert report is not None
        self.assertEqual((3, 1, 1), (report.object_count, report.mesh_count, report.armature_count))
        self.assertEqual(FbxImportSettings(), report.settings)

    def test_invalid_sources_fail_before_scene_or_importer_side_effects(self) -> None:
        outside = Path(self.workspace.name) / "outside.fbx"
        outside.write_bytes(b"fixture")
        empty = self._source("empty.fbx", b"")
        wrong_extension = self._source("model.glb")
        missing = self.input_root / "missing.fbx"
        relative = Path("relative.fbx")
        cases = (outside, empty, wrong_extension, missing, relative)

        for source in cases:
            with self.subTest(source=source):
                data = _Data()
                importer = _Importer(data, ("MESH",))
                result = import_fbx(data, importer, source, self.input_root)
                self.assertFalse(result.succeeded)
                self.assertEqual("BLENDER_FBX_SOURCE_INVALID", result.findings[0].code)
                self.assertEqual([], importer.calls)
                self.assertEqual(0, data.purge_calls)

    def test_linked_source_is_rejected_when_links_are_available(self) -> None:
        original = self._source()
        linked = self.input_root / "linked.fbx"
        try:
            linked.symlink_to(original)
        except OSError:
            self.skipTest("Symbolic links are unavailable in this test environment.")

        result = import_fbx(_Data(), _Importer(_Data(), ("MESH",)), linked, self.input_root)

        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_FBX_SOURCE_INVALID", result.findings[0].code)

    def test_invalid_axis_and_scale_settings_fail_before_side_effects(self) -> None:
        invalid_settings = (
            FbxImportSettings("Z", "-Z"),
            FbxImportSettings("Q", "Y"),
            FbxImportSettings("-Z", "Y", 0),
            FbxImportSettings("-Z", "Y", 0.0009),
            FbxImportSettings("-Z", "Y", -1),
            FbxImportSettings("-Z", "Y", math.nan),
            FbxImportSettings("-Z", "Y", math.inf),
            FbxImportSettings("-Z", "Y", 1001),
            FbxImportSettings("-Z", "Y", True),
        )
        for settings in invalid_settings:
            with self.subTest(settings=settings):
                data = _Data()
                importer = _Importer(data, ("MESH",))
                result = import_fbx(data, importer, self._source(), self.input_root, settings)
                self.assertEqual("BLENDER_FBX_IMPORT_SETTINGS_INVALID", result.findings[0].code)
                self.assertEqual([], importer.calls)
                self.assertEqual(0, data.purge_calls)

    def test_operator_exception_is_sanitized_and_partial_import_is_cleaned(self) -> None:
        data = _Data()
        importer = _Importer(data, ("MESH", "ARMATURE"))
        importer.error = RuntimeError("private source path and parser detail")

        result = import_fbx(data, importer, self._source(), self.input_root)

        self.assertFalse(result.succeeded)
        self.assertEqual(
            ("BLENDER_FBX_IMPORT_FAILED",), tuple(value.code for value in result.findings)
        )
        self.assertEqual([], data.objects)
        protocol = result.findings[0].as_protocol_value()
        self.assertEqual("blender-fbx-importer", protocol["source"])
        self.assertTrue(protocol["blocksRelease"])
        self.assertNotIn("private", repr(protocol))

    def test_rejected_operator_result_is_structured_and_partial_import_is_cleaned(self) -> None:
        data = _Data()
        importer = _Importer(data, ("MESH",))
        importer.result = {"CANCELLED"}

        result = import_fbx(data, importer, self._source(), self.input_root)

        self.assertEqual(
            ("BLENDER_FBX_IMPORT_REJECTED",), tuple(value.code for value in result.findings)
        )
        self.assertEqual([], data.objects)

    def test_failed_partial_cleanup_adds_stable_cleanup_finding(self) -> None:
        data = _Data()
        importer = _Importer(data, ("MESH",))
        importer.error = RuntimeError("private import detail")

        def importer_with_cleanup_failure(**options: object) -> object:
            importer.calls.append(options)
            data.objects.append(_Object("MESH"))
            data.fail_cleanup = True
            raise RuntimeError("private import detail")

        result = import_fbx(data, importer_with_cleanup_failure, self._source(), self.input_root)

        self.assertEqual(
            ("BLENDER_FBX_IMPORT_FAILED", "BLENDER_FBX_IMPORT_CLEANUP_FAILED"),
            tuple(value.code for value in result.findings),
        )
        self.assertEqual(1, len(data.objects))

    def test_empty_successful_import_returns_blocking_finding(self) -> None:
        data = _Data()
        importer = _Importer(data, ())

        result = import_fbx(data, importer, self._source(), self.input_root)

        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_FBX_IMPORT_EMPTY", result.findings[0].code)

    def test_scene_reset_failure_prevents_import(self) -> None:
        data = _Data()
        data.fail_cleanup = True
        importer = _Importer(data, ("MESH",))

        result = import_fbx(data, importer, self._source(), self.input_root)

        self.assertEqual("BLENDER_SCENE_RESET_FAILED", result.findings[0].code)
        self.assertEqual([], importer.calls)

    def test_unreadable_imported_scene_becomes_stable_finding(self) -> None:
        class _UnreadableData(_Data):
            @property
            def objects(self) -> list[object]:
                if getattr(self, "import_complete", False):
                    raise RuntimeError("private Blender data detail")
                return self._objects

            @objects.setter
            def objects(self, value: list[object]) -> None:
                self._objects = value

        data = _UnreadableData()

        def importer(**_options: object) -> object:
            data.import_complete = True
            return {"FINISHED"}

        result = import_fbx(data, importer, self._source(), self.input_root)

        self.assertEqual("BLENDER_FBX_IMPORT_RESULT_INVALID", result.findings[0].code)


if __name__ == "__main__":
    unittest.main()
