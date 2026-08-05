"""PB-0401 worker-shell acceptance and failure-boundary tests."""

from __future__ import annotations

import io
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
FIXTURE_ROOT = REPOSITORY_ROOT / "tests" / "fixtures" / "workers" / "valid"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.entrypoint import (  # noqa: E402
    WorkerExitCode,
    main_from_blender,
    run,
)


class BlenderWorkerEntrypointTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0401"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.workspace_path = Path(self.workspace.name)

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def _request(self, mutate: object | None = None) -> Path:
        value = json.loads((FIXTURE_ROOT / "blender-probe-request.json").read_text("utf-8"))
        if callable(mutate):
            mutate(value)
        path = self.workspace_path / "request.json"
        path.write_text(json.dumps(value, separators=(",", ":")), encoding="utf-8")
        return path

    def _run(self, request: Path) -> tuple[WorkerExitCode, list[str], str]:
        stdout = io.StringIO()
        stderr = io.StringIO()
        code = run(request, stdout, stderr)
        return code, stdout.getvalue().splitlines(), stderr.getvalue()

    def test_probe_emits_shared_json_lines_and_atomically_writes_shared_result(self) -> None:
        code, lines, stderr = self._run(self._request())

        self.assertEqual(WorkerExitCode.SUCCESS, code)
        self.assertEqual("", stderr)
        self.assertEqual(
            [
                (FIXTURE_ROOT / "blender-probe-start.json").read_text("utf-8").strip(),
                (FIXTURE_ROOT / "blender-probe-complete.json").read_text("utf-8").strip(),
            ],
            lines,
        )
        result_path = self.workspace_path / "results" / "blender-probe-result.json"
        self.assertEqual(
            (FIXTURE_ROOT / "blender-probe-result.json").read_text("utf-8").strip(),
            result_path.read_text("utf-8").strip(),
        )
        self.assertEqual([], list(result_path.parent.glob(".blender-probe-result.json.*")))

    def test_unsupported_operation_writes_failure_result_and_finding(self) -> None:
        request = self._request(lambda value: value.update(operation="normalize-blender-source"))

        code, lines, stderr = self._run(request)

        self.assertEqual(WorkerExitCode.UNSUPPORTED_OPERATION, code)
        self.assertEqual("", stderr)
        self.assertEqual(2, len(lines))
        finding_event = json.loads(lines[1])
        self.assertEqual("finding", finding_event["eventKind"])
        self.assertEqual("BLENDER_OPERATION_UNSUPPORTED", finding_event["finding"]["code"])
        result = json.loads(
            (self.workspace_path / "results" / "blender-probe-result.json").read_text("utf-8")
        )
        self.assertEqual("failure", result["status"])
        self.assertFalse(result["outputsPromoted"])
        self.assertEqual("safe", result["retrySafety"])
        self.assertEqual(result["findings"][0], finding_event["finding"])

    def test_duplicate_unknown_traversal_and_oversized_requests_fail_without_side_effects(
        self,
    ) -> None:
        invalid_values = [
            '{"protocolVersion":1,"protocolVersion":1}',
            json.dumps(
                {
                    **json.loads((FIXTURE_ROOT / "blender-probe-request.json").read_text("utf-8")),
                    "unknown": True,
                }
            ),
            json.dumps(
                {
                    **json.loads((FIXTURE_ROOT / "blender-probe-request.json").read_text("utf-8")),
                    "resultFileReference": "../outside.json",
                }
            ),
            " " * 1_048_577,
        ]
        for index, text in enumerate(invalid_values):
            with self.subTest(index=index):
                request = self.workspace_path / f"invalid-{index}.json"
                request.write_text(text, encoding="utf-8")
                code, lines, stderr = self._run(request)
                self.assertEqual(WorkerExitCode.INVALID_REQUEST, code)
                self.assertEqual([], lines)
                self.assertEqual("BLENDER_WORKER_REQUEST_INVALID\n", stderr)
                self.assertFalse((self.workspace_path / "outside.json").exists())

    def test_request_symlink_is_rejected_when_platform_supports_links(self) -> None:
        original = self._request()
        linked = self.workspace_path / "linked-request.json"
        try:
            linked.symlink_to(original)
        except OSError:
            self.skipTest("Symbolic links are unavailable in this test environment.")

        code, lines, stderr = self._run(linked)

        self.assertEqual(WorkerExitCode.INVALID_REQUEST, code)
        self.assertEqual([], lines)
        self.assertEqual("BLENDER_WORKER_REQUEST_INVALID\n", stderr)

    def test_linked_result_parent_cannot_escape_workspace(self) -> None:
        outside = self.workspace_path.parent / f"{self.workspace_path.name}-outside"
        outside.mkdir()
        linked_parent = self.workspace_path / "results"
        try:
            linked_parent.symlink_to(outside, target_is_directory=True)
        except OSError:
            outside.rmdir()
            self.skipTest("Directory links are unavailable in this test environment.")
        try:
            code, lines, stderr = self._run(self._request())

            self.assertEqual(WorkerExitCode.INVALID_REQUEST, code)
            self.assertEqual([], lines)
            self.assertEqual("BLENDER_WORKER_REQUEST_INVALID\n", stderr)
            self.assertEqual([], list(outside.iterdir()))
        finally:
            linked_parent.unlink(missing_ok=True)
            outside.rmdir()

    def test_hosted_blender_version_mismatch_fails_without_processing(self) -> None:
        fake_bpy = types.SimpleNamespace(app=types.SimpleNamespace(version=(5, 1, 0)))
        with mock.patch.dict(sys.modules, {"bpy": fake_bpy}):
            code, lines, stderr = self._run(self._request())

        self.assertEqual(WorkerExitCode.EXECUTION_FAILED, code)
        self.assertEqual("", stderr)
        self.assertEqual(2, len(lines))
        result = json.loads(
            (self.workspace_path / "results" / "blender-probe-result.json").read_text("utf-8")
        )
        self.assertEqual("BLENDER_ENGINE_VERSION_MISMATCH", result["findings"][0]["code"])

    def test_hosted_blender_records_runtime_version_when_request_omits_it(self) -> None:
        request = self._request(lambda value: value.pop("engineVersion"))
        fake_bpy = types.SimpleNamespace(app=types.SimpleNamespace(version=(5, 0, 0)))

        with mock.patch.dict(sys.modules, {"bpy": fake_bpy}):
            code, _, stderr = self._run(request)

        self.assertEqual(WorkerExitCode.SUCCESS, code)
        self.assertEqual("", stderr)
        result = json.loads(
            (self.workspace_path / "results" / "blender-probe-result.json").read_text("utf-8")
        )
        self.assertEqual("5.0.0", result["engineVersion"])

    def test_runtime_initialization_exception_becomes_structured_failure(self) -> None:
        with mock.patch(
            "package_builder_blender.entrypoint.importlib.import_module",
            side_effect=RuntimeError("private runtime detail"),
        ):
            code, lines, stderr = self._run(self._request())

        self.assertEqual(WorkerExitCode.EXECUTION_FAILED, code)
        self.assertEqual("", stderr)
        self.assertEqual(2, len(lines))
        result = json.loads(
            (self.workspace_path / "results" / "blender-probe-result.json").read_text("utf-8")
        )
        self.assertEqual("BLENDER_WORKER_EXECUTION_FAILED", result["findings"][0]["code"])
        self.assertNotIn("private", json.dumps(result))

    def test_result_write_failure_has_stable_exit_and_sanitized_stderr(self) -> None:
        with mock.patch(
            "package_builder_blender.entrypoint.atomic_write_result",
            side_effect=OSError("private path and details"),
        ):
            code, lines, stderr = self._run(self._request())

        self.assertEqual(WorkerExitCode.RESULT_WRITE_FAILED, code)
        self.assertEqual(2, len(lines))
        self.assertEqual("BLENDER_WORKER_RESULT_WRITE_FAILED\n", stderr)
        self.assertNotIn("private", stderr)

    def test_blender_argument_separator_is_required(self) -> None:
        stderr = io.StringIO()
        with mock.patch("sys.stderr", stderr):
            code = main_from_blender(["blender.exe", "--background"])

        self.assertEqual(WorkerExitCode.INVALID_INVOCATION, code)
        self.assertEqual("BLENDER_WORKER_ARGUMENT_SEPARATOR_MISSING\n", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
