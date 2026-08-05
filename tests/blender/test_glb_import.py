"""PB-0404 deterministic GLB adapter and failure-boundary tests."""

from __future__ import annotations

import math
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.glb_import import (  # noqa: E402
    GlbImportSettings,
    import_glb,
)


class _Modifier:
    def __init__(self, modifier_type: str) -> None:
        self.type = modifier_type


class _Object:
    def __init__(
        self,
        object_type: str,
        *,
        modifiers: tuple[_Modifier, ...] = (),
        parent: _Object | None = None,
    ) -> None:
        self.type = object_type
        self.modifiers = modifiers
        self.parent = parent


class _Image:
    def __init__(self, *, packed: bool, tiled: bool = False) -> None:
        self.packed_file = object() if packed and not tiled else None
        self.packed_files = (object(),) if packed and tiled else ()


class _Data:
    def __init__(self) -> None:
        self.objects: list[object] = [_Object("CAMERA")]
        self.materials: list[object] = []
        self.images: list[object] = []
        self.actions: list[object] = []
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
    def __init__(
        self,
        data: _Data,
        *,
        objects: tuple[_Object, ...],
        material_count: int = 0,
        images: tuple[_Image, ...] = (),
        action_count: int = 0,
    ) -> None:
        self.data = data
        self.objects = objects
        self.material_count = material_count
        self.images = images
        self.action_count = action_count
        self.calls: list[dict[str, object]] = []
        self.result: object = {"FINISHED"}
        self.error: Exception | None = None

    def __call__(self, **options: object) -> object:
        self.calls.append(options)
        self.data.objects.extend(self.objects)
        self.data.materials.extend(object() for _ in range(self.material_count))
        self.data.images.extend(self.images)
        self.data.actions.extend(object() for _ in range(self.action_count))
        if self.error is not None:
            raise self.error
        return self.result


class BlenderGlbImportTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0404"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.input_root = Path(self.workspace.name) / "inputs"
        self.input_root.mkdir()

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def _source(self, name: str = "model.glb", content: bytes = b"glTF fixture") -> Path:
        path = self.input_root / name
        path.write_bytes(content)
        return path

    def test_material_image_skin_and_animation_fixture_records_exact_settings(self) -> None:
        data = _Data()
        armature = _Object("ARMATURE")
        mesh = _Object("MESH", modifiers=(_Modifier("ARMATURE"),))
        importer = _Importer(
            data,
            objects=(armature, mesh),
            material_count=2,
            images=(_Image(packed=True), _Image(packed=True, tiled=True)),
            action_count=3,
        )

        result = import_glb(data, importer, self._source("animated.GLB"), self.input_root)

        self.assertTrue(result.succeeded)
        self.assertEqual((), result.findings)
        report = result.report
        assert report is not None
        self.assertEqual("animated.GLB", report.source_name)
        self.assertEqual(GlbImportSettings(), report.settings)
        self.assertEqual(
            (2, 1, 2, 2, 2, 1, 1, 3),
            (
                report.object_count,
                report.mesh_count,
                report.material_count,
                report.image_count,
                report.packed_image_count,
                report.armature_count,
                report.skinned_mesh_count,
                report.animation_count,
            ),
        )
        self.assertEqual(
            {
                "filepath": str((self.input_root / "animated.GLB").resolve()),
                "import_pack_images": True,
                "merge_vertices": False,
                "import_shading": "NORMALS",
                "bone_heuristic": "BLENDER",
                "guess_original_bind_pose": True,
                "disable_bone_shape": True,
                "bone_shape_scale_factor": 1.0,
                "import_webp_texture": False,
                "import_unused_materials": True,
                "import_select_created_objects": False,
                "import_scene_extras": False,
                "import_scene_as_collection": False,
                "import_merge_material_slots": False,
                "export_import_convert_lighting_mode": "SPEC",
            },
            importer.calls[0],
        )

    def test_parented_mesh_is_reported_as_skinned(self) -> None:
        data = _Data()
        armature = _Object("ARMATURE")
        mesh = _Object("MESH", parent=armature)
        result = import_glb(
            data,
            _Importer(data, objects=(armature, mesh)),
            self._source(),
            self.input_root,
        )

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(1, result.report.skinned_mesh_count)

    def test_preexisting_linked_data_is_not_counted_in_import_report(self) -> None:
        data = _Data()
        retained_material = object()
        retained_image = _Image(packed=False)
        retained_action = object()
        data.materials.append(retained_material)
        data.images.append(retained_image)
        data.actions.append(retained_action)
        importer = _Importer(
            data,
            objects=(_Object("MESH"),),
            material_count=1,
            images=(_Image(packed=True),),
            action_count=1,
        )

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            (1, 1, 1),
            (
                result.report.material_count,
                result.report.image_count,
                result.report.animation_count,
            ),
        )

    def test_invalid_sources_fail_before_scene_or_importer_side_effects(self) -> None:
        outside = Path(self.workspace.name) / "outside.glb"
        outside.write_bytes(b"fixture")
        cases = (
            outside,
            self._source("empty.glb", b""),
            self._source("separate.gltf"),
            self._source("model.fbx"),
            self.input_root / "missing.glb",
            Path("relative.glb"),
        )

        for source in cases:
            with self.subTest(source=source):
                data = _Data()
                importer = _Importer(data, objects=(_Object("MESH"),))
                result = import_glb(data, importer, source, self.input_root)
                self.assertFalse(result.succeeded)
                self.assertEqual("BLENDER_GLB_SOURCE_INVALID", result.findings[0].code)
                self.assertEqual([], importer.calls)
                self.assertEqual(0, data.purge_calls)

    def test_linked_source_is_rejected_when_links_are_available(self) -> None:
        original = self._source()
        linked = self.input_root / "linked.glb"
        try:
            linked.symlink_to(original)
        except OSError:
            self.skipTest("Symbolic links are unavailable in this test environment.")

        data = _Data()
        result = import_glb(
            data,
            _Importer(data, objects=(_Object("MESH"),)),
            linked,
            self.input_root,
        )

        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_GLB_SOURCE_INVALID", result.findings[0].code)

    def test_invalid_settings_fail_before_side_effects(self) -> None:
        invalid_settings: tuple[object, ...] = (
            "invalid",
            GlbImportSettings(pack_images=1),
            GlbImportSettings(shading="UNKNOWN"),
            GlbImportSettings(bone_heuristic="UNKNOWN"),
            GlbImportSettings(lighting_mode="UNKNOWN"),
            GlbImportSettings(bone_shape_scale_factor=0),
            GlbImportSettings(bone_shape_scale_factor=math.nan),
            GlbImportSettings(bone_shape_scale_factor=math.inf),
        )
        for settings in invalid_settings:
            with self.subTest(settings=settings):
                data = _Data()
                importer = _Importer(data, objects=(_Object("MESH"),))
                result = import_glb(  # type: ignore[arg-type]
                    data, importer, self._source(), self.input_root, settings
                )
                self.assertEqual("BLENDER_GLB_IMPORT_SETTINGS_INVALID", result.findings[0].code)
                self.assertEqual([], importer.calls)
                self.assertEqual(0, data.purge_calls)

    def test_operator_exception_is_sanitized_and_partial_import_is_cleaned(self) -> None:
        data = _Data()
        importer = _Importer(data, objects=(_Object("MESH"),))
        importer.error = RuntimeError("private source path and parser detail")

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertEqual(("BLENDER_GLB_IMPORT_FAILED",), tuple(x.code for x in result.findings))
        self.assertEqual([], data.objects)
        protocol = result.findings[0].as_protocol_value()
        self.assertEqual("blender-glb-importer", protocol["source"])
        self.assertTrue(protocol["blocksRelease"])
        self.assertNotIn("private", repr(protocol))

    def test_rejected_operator_result_is_structured_and_partial_import_is_cleaned(self) -> None:
        data = _Data()
        importer = _Importer(data, objects=(_Object("MESH"),))
        importer.result = {"CANCELLED"}

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertEqual(("BLENDER_GLB_IMPORT_REJECTED",), tuple(x.code for x in result.findings))
        self.assertEqual([], data.objects)

    def test_failed_partial_cleanup_adds_stable_cleanup_finding(self) -> None:
        data = _Data()

        def importer(**_options: object) -> object:
            data.objects.append(_Object("MESH"))
            data.fail_cleanup = True
            raise RuntimeError("private import detail")

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertEqual(
            ("BLENDER_GLB_IMPORT_FAILED", "BLENDER_GLB_IMPORT_CLEANUP_FAILED"),
            tuple(value.code for value in result.findings),
        )

    def test_empty_successful_import_returns_blocking_finding(self) -> None:
        data = _Data()
        result = import_glb(data, _Importer(data, objects=()), self._source(), self.input_root)

        self.assertFalse(result.succeeded)
        self.assertEqual("BLENDER_GLB_IMPORT_EMPTY", result.findings[0].code)

    def test_scene_reset_failure_prevents_import(self) -> None:
        data = _Data()
        data.fail_cleanup = True
        importer = _Importer(data, objects=(_Object("MESH"),))

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertEqual("BLENDER_SCENE_RESET_FAILED", result.findings[0].code)
        self.assertEqual([], importer.calls)

    def test_unreadable_imported_data_becomes_stable_finding(self) -> None:
        class _UnreadableData(_Data):
            @property
            def materials(self) -> list[object]:
                if getattr(self, "import_complete", False):
                    raise RuntimeError("private Blender data detail")
                return self._materials

            @materials.setter
            def materials(self, value: list[object]) -> None:
                self._materials = value

        data = _UnreadableData()

        def importer(**_options: object) -> object:
            data.objects.append(_Object("MESH"))
            data.import_complete = True
            return {"FINISHED"}

        result = import_glb(data, importer, self._source(), self.input_root)

        self.assertEqual("BLENDER_GLB_IMPORT_RESULT_INVALID", result.findings[0].code)


if __name__ == "__main__":
    unittest.main()
