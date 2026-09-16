"""PB-1103 engine-adapter tests. Fake assets never count as live engine acceptance."""

import io
import json
import os
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

REPO = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(REPO / "workers/shared"), str(REPO / "workers/unreal")]

from package_builder_unreal.worker import ASSET, run  # noqa: E402


class UnrealWorkerTests(unittest.TestCase):
    def setUp(self):
        root = REPO / "artifacts/validation/PB-1103"
        root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.project = self.root / "project"
        self.project.mkdir()
        self.asset = self.project / "Content/Pack/PB_WorkerProbe.uasset"
        self.library = Mock()
        self.library.does_asset_exist.return_value = False
        self.library.save_loaded_asset.side_effect = self.save
        self.library.load_asset.side_effect = lambda _: object() if self.asset.exists() else None
        self.tools = Mock()
        self.engine = types.SimpleNamespace(
            Paths=types.SimpleNamespace(project_dir=lambda: str(self.project)),
            SystemLibrary=types.SimpleNamespace(
                get_engine_version=lambda: "5.8.2-123+++UE5+Release"
            ),
            EditorAssetLibrary=self.library,
            AssetToolsHelpers=types.SimpleNamespace(get_asset_tools=lambda: self.tools),
            Material=object,
            MaterialFactoryNew=object,
        )
        self.request = {
            "protocolVersion": 1,
            "jobId": "Job-Unreal-01",
            "operation": "probe-unreal-worker",
            "productManifestReference": "input/product.json",
            "inputDirectoryReference": "input",
            "outputDirectoryReference": "output",
            "resultFileReference": "output/result.json",
            "engineVersion": "5.8.2",
            "target": "unreal",
        }

    def save(self, *_args, **_kwargs):
        self.asset.parent.mkdir(parents=True, exist_ok=True)
        self.asset.write_bytes(b"test-only-uasset")
        return True

    def execute(self, raw=None):
        request = self.root / "request.json"
        request.write_text(raw or json.dumps(self.request), encoding="utf-8")
        self.stdout, self.stderr = io.StringIO(), io.StringIO()
        return run(request, self.engine, self.stdout, self.stderr)

    def result(self):
        return json.loads((self.root / "output/result.json").read_text("utf-8"))

    def test_save_and_independent_reopen_preserve_exact_artifact_bytes(self):
        self.assertEqual(0, self.execute())
        result = self.result()
        self.assertEqual("success", result["status"])
        self.assertFalse(result["outputsPromoted"])
        self.assertEqual(16, result["artifacts"][0]["byteCount"])
        self.library.save_loaded_asset.assert_called_once()
        self.tools.create_asset.assert_called_once()
        events = [json.loads(line) for line in self.stdout.getvalue().splitlines()]
        self.assertEqual([0, 100], [event["percent"] for event in events])
        self.request["operation"] = "verify-unreal-worker"
        self.assertEqual(0, self.execute())
        self.library.load_asset.assert_called_once_with(ASSET)
        self.library.save_loaded_asset.assert_called_once()
        self.assertEqual(result["artifacts"], self.result()["artifacts"])
        golden = json.loads(
            (REPO / "tests/fixtures/workers/valid/unreal-probe-result.json").read_text("utf-8")
        )
        self.assertEqual(golden, result)

    def test_missing_asset_is_not_a_successful_reopen(self):
        self.request["operation"] = "verify-unreal-worker"
        self.assertEqual(5, self.execute())
        self.assertEqual("requires-cleanup", self.result()["retrySafety"])

    def test_existing_probe_is_never_overwritten(self):
        self.save()
        self.assertEqual(5, self.execute())
        self.tools.create_asset.assert_not_called()
        self.assertEqual(b"test-only-uasset", self.asset.read_bytes())

    def test_wrong_version_does_not_touch_assets(self):
        self.request["engineVersion"] = "5.8.1"
        self.assertEqual(5, self.execute())
        self.assertEqual("UNREAL_ENGINE_VERSION_MISMATCH", self.result()["findings"][0]["code"])
        self.tools.create_asset.assert_not_called()

    def test_unsupported_operation_does_not_touch_assets(self):
        self.request["operation"] = "build-unreal-target"
        self.assertEqual(4, self.execute())
        self.tools.create_asset.assert_not_called()

    def test_save_failure_is_blocking_and_does_not_claim_artifacts(self):
        self.library.save_loaded_asset.side_effect = lambda *_args, **_kw: False
        self.assertEqual(5, self.execute())
        self.assertEqual([], self.result()["artifacts"])
        self.assertTrue(self.result()["findings"][0]["blocksRelease"])

    def test_engine_exceptions_are_sanitized(self):
        self.tools.create_asset.side_effect = RuntimeError("private supplier path")
        self.assertEqual(5, self.execute())
        self.assertNotIn("private supplier", self.stdout.getvalue() + self.stderr.getvalue())

    def test_invalid_requests_fail_before_engine_calls(self):
        for field, value in [
            ("protocolVersion", True),
            ("target", []),
            ("target", "unity"),
            ("engineVersion", "5.8.2-preview"),
            ("resultFileReference", "../outside"),
            ("resultFileReference", "input/product.json"),
            ("outputDirectoryReference", "project"),
            ("inputDirectoryReference", "output"),
        ]:
            with self.subTest(field=field, value=value):
                original = self.request[field]
                self.request[field] = value
                self.assertEqual(3, self.execute())
                self.tools.create_asset.assert_not_called()
                self.request[field] = original

    def test_duplicate_keys_are_rejected(self):
        self.assertEqual(3, self.execute('{"protocolVersion":1,"protocolVersion":1}'))

    def test_foreign_project_is_rejected(self):
        self.engine.Paths.project_dir = lambda: str(REPO)
        self.assertEqual(5, self.execute())
        self.tools.create_asset.assert_not_called()

    def test_result_write_failure_has_distinct_exit_code(self):
        with patch("package_builder_unreal.worker.atomic_write_result", side_effect=OSError()):
            self.assertEqual(6, self.execute())
        self.assertEqual("UNREAL_WORKER_RESULT_WRITE_FAILED\n", self.stderr.getvalue())

    def test_oversized_request_is_rejected_without_engine_calls(self):
        self.assertEqual(3, self.execute(" " * (1024 * 1024 + 1)))
        self.tools.create_asset.assert_not_called()

    def test_linked_output_is_rejected_without_writing_through_it(self):
        destination = self.root / "protected"
        destination.mkdir()
        link = self.root / "output"
        try:
            os.symlink(destination, link, target_is_directory=True)
        except OSError:
            self.skipTest("Creating symlinks requires developer mode or privilege on this host.")
        self.addCleanup(link.unlink)
        self.assertEqual(3, self.execute())
        self.assertEqual([], list(destination.iterdir()))
        self.tools.create_asset.assert_not_called()


if __name__ == "__main__":
    unittest.main()
