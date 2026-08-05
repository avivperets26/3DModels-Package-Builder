"""PB-0402 context-independent scene reset and temporary cleanup tests."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.scene_utils import (  # noqa: E402
    SceneResetReport,
    TemporaryDataBlocks,
    reset_scene,
)


class _ForbiddenUiBoundary:
    def __getattr__(self, name: str) -> object:
        raise AssertionError(f"Scene utilities touched forbidden UI state: {name}")


class _FakeData:
    def __init__(self, objects: list[object] | None = None, orphan_count: int = 0) -> None:
        self.objects = list(objects or [])
        self.orphan_count: object = orphan_count
        self.batch_calls: list[tuple[object, ...]] = []
        self.purge_calls: list[dict[str, bool]] = []
        self.fail_batch = False

    def batch_remove(self, *, ids: tuple[object, ...]) -> None:
        if self.fail_batch:
            raise RuntimeError("simulated Blender cleanup failure")
        self.batch_calls.append(ids)
        self.objects = [item for item in self.objects if all(item is not value for value in ids)]

    def orphans_purge(self, **options: bool) -> object:
        self.purge_calls.append(options)
        return self.orphan_count


class BlenderSceneUtilityTests(unittest.TestCase):
    def test_reset_removes_all_objects_and_local_orphans_without_ui_state(self) -> None:
        camera, mesh, light = object(), object(), object()
        data = _FakeData([camera, mesh, light], orphan_count=7)
        blender = type(
            "Bpy",
            (),
            {"data": data, "context": _ForbiddenUiBoundary(), "ops": _ForbiddenUiBoundary()},
        )()

        report = reset_scene(blender.data)

        self.assertEqual(SceneResetReport(3, 7), report)
        self.assertEqual([(camera, mesh, light)], data.batch_calls)
        self.assertEqual([], data.objects)
        self.assertEqual(
            [{"do_local_ids": True, "do_linked_ids": False, "do_recursive": True}],
            data.purge_calls,
        )

    def test_empty_reset_still_purges_orphans_without_empty_batch_call(self) -> None:
        data = _FakeData(orphan_count=2)

        report = reset_scene(data)

        self.assertEqual(SceneResetReport(0, 2), report)
        self.assertEqual([], data.batch_calls)
        self.assertEqual(1, len(data.purge_calls))

    def test_invalid_orphan_count_fails_closed(self) -> None:
        data = _FakeData(orphan_count=-1)

        with self.assertRaisesRegex(RuntimeError, "invalid orphan-purge count"):
            reset_scene(data)

    def test_temporary_data_is_deduplicated_and_removed_in_reverse_order(self) -> None:
        data = _FakeData()
        mesh, object_block = object(), object()
        temporary = TemporaryDataBlocks(data)

        self.assertIs(mesh, temporary.register(mesh))
        temporary.register(object_block)
        temporary.register(mesh)

        self.assertEqual(2, temporary.close())
        self.assertEqual([(mesh, object_block)], data.batch_calls)
        self.assertEqual(0, temporary.close())
        with self.assertRaisesRegex(RuntimeError, "already closed"):
            temporary.register(object())

    def test_context_manager_disposes_temporary_data_after_body_failure(self) -> None:
        data = _FakeData()
        temporary = object()

        with (
            self.assertRaisesRegex(ValueError, "processing failed"),
            TemporaryDataBlocks(data) as owned,
        ):
            owned.register(temporary)
            raise ValueError("processing failed")

        self.assertEqual([(temporary,)], data.batch_calls)

    def test_failed_cleanup_keeps_ownership_retryable(self) -> None:
        data = _FakeData()
        temporary = object()
        owned = TemporaryDataBlocks(data)
        owned.register(temporary)
        data.fail_batch = True

        with self.assertRaisesRegex(RuntimeError, "cleanup failure"):
            owned.close()

        data.fail_batch = False
        self.assertEqual(1, owned.close())
        self.assertEqual([(temporary,)], data.batch_calls)

    def test_none_cannot_be_registered(self) -> None:
        with self.assertRaisesRegex(ValueError, "cannot be None"):
            TemporaryDataBlocks(_FakeData()).register(None)


if __name__ == "__main__":
    unittest.main()
