"""PB-0418 stable Blender failure and regression fixture tests."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
WORKER_ROOT = REPOSITORY_ROOT / "workers" / "blender"
FIXTURE_ROOT = REPOSITORY_ROOT / "tests" / "fixtures" / "blender" / "regression"
sys.path.insert(0, str(WORKER_ROOT))

from package_builder_blender.fbx_import import import_fbx  # noqa: E402
from package_builder_blender.glb_import import import_glb  # noqa: E402
from package_builder_blender.regression_validation import (  # noqa: E402
    RegressionObservation,
    validate_regression_observation,
)


class _Object:
    def __init__(self, object_type: str) -> None:
        self.type = object_type
        self.modifiers: tuple[object, ...] = ()
        self.parent = None


class _Data:
    def __init__(self, *, include_gltf_collections: bool) -> None:
        self.objects: list[object] = [_Object("CAMERA")]
        self.batch_calls: list[tuple[object, ...]] = []
        self.purge_calls = 0
        if include_gltf_collections:
            self.materials: list[object] = []
            self.images: list[object] = []
            self.actions: list[object] = []

    def batch_remove(self, *, ids: tuple[object, ...]) -> None:
        self.batch_calls.append(ids)
        self.objects = [value for value in self.objects if value not in ids]

    def orphans_purge(self, **_options: bool) -> int:
        self.purge_calls += 1
        return 0


def _raise_parser_error(**_options: object) -> object:
    raise RuntimeError("private parser detail must not escape")


class BlenderRegressionFixtureTests(unittest.TestCase):
    def test_manifest_cases_return_the_declared_stable_findings(self) -> None:
        manifest = json.loads((FIXTURE_ROOT / "fixture-cases.json").read_text("utf-8"))
        self.assertEqual(1, manifest["schemaVersion"])
        self.assertEqual(7, len(manifest["fixtures"]))

        for fixture in manifest["fixtures"]:
            with self.subTest(fixture=fixture["id"]):
                if "payloadFile" in fixture:
                    payload = FIXTURE_ROOT / fixture["payloadFile"]
                    self.assertEqual(".payload", payload.suffix)
                    self.assertTrue(payload.is_file())
                    self.assertGreater(payload.stat().st_size, 0)
                result = validate_regression_observation(
                    RegressionObservation(
                        fixture["id"],
                        fixture["sourceFormat"],
                        fixture["parserSucceeded"],
                        fixture["meshCount"],
                        fixture.get("missingImageCount", 0),
                        fixture.get("skeletonCount", 0),
                        fixture.get("meshWithoutUvCount", 0),
                        tuple(fixture.get("unsupportedDataTypes", ())),
                        fixture.get("invalidAnimationCount", 0),
                    )
                )
                self.assertFalse(result.succeeded)
                self.assertEqual(
                    tuple(fixture["expectedCodes"]),
                    tuple(finding.code for finding in result.findings),
                )

    def test_multiple_failures_have_deterministic_order(self) -> None:
        observation = RegressionObservation(
            "combined-regression",
            "glb",
            False,
            1,
            missing_image_count=1,
            skeleton_count=2,
            mesh_without_uv_count=1,
            unsupported_data_types=("VOLUME",),
            invalid_animation_count=1,
        )

        result = validate_regression_observation(observation)

        self.assertEqual(
            (
                "BLENDER_SOURCE_CORRUPT",
                "BLENDER_TEXTURE_REFERENCE_MISSING",
                "BLENDER_MULTIPLE_RIGS_UNSUPPORTED",
                "BLENDER_UV_REQUIRED",
                "BLENDER_DATA_UNSUPPORTED",
                "BLENDER_ANIMATION_INVALID",
            ),
            tuple(finding.code for finding in result.findings),
        )

    def test_invalid_observations_fail_closed_without_throwing(self) -> None:
        cases = (
            RegressionObservation("bad", "obj", True, 1),
            RegressionObservation("bad", "fbx", True, -1),
            RegressionObservation("bad", "fbx", 1, 1),  # type: ignore[arg-type]
            RegressionObservation("../bad", "fbx", True, 1),
            RegressionObservation(
                "bad", "fbx", True, 1, unsupported_data_types=("VOLUME", "VOLUME")
            ),
            RegressionObservation("\x00bad", "fbx", True, 1),
        )
        for observation in cases:
            with self.subTest(observation=observation):
                result = validate_regression_observation(observation)
                self.assertEqual(
                    ("BLENDER_REGRESSION_INPUT_INVALID",),
                    tuple(finding.code for finding in result.findings),
                )

    def test_healthy_observation_returns_a_report(self) -> None:
        result = validate_regression_observation(
            RegressionObservation("healthy-static", "FBX", True, 2)
        )

        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            ("healthy-static", "fbx", 2),
            (
                result.report.fixture_id,
                result.report.source_format,
                result.report.mesh_count,
            ),
        )

        no_mesh = validate_regression_observation(
            RegressionObservation("empty-scene", "glb", True, 0)
        )
        self.assertEqual("BLENDER_MESH_REQUIRED", no_mesh.findings[0].code)

    def test_corrupt_fixture_import_boundaries_return_sanitized_findings(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0418"
        artifact_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifact_root) as workspace:
            input_root = Path(workspace)
            cases = (
                (
                    import_fbx,
                    _Data(include_gltf_collections=False),
                    "corrupt-fbx.payload",
                    "corrupt.fbx",
                    "BLENDER_FBX_IMPORT_FAILED",
                ),
                (
                    import_glb,
                    _Data(include_gltf_collections=True),
                    "corrupt-glb.payload",
                    "corrupt.glb",
                    "BLENDER_GLB_IMPORT_FAILED",
                ),
            )
            for adapter, data, payload_name, source_name, expected_code in cases:
                with self.subTest(source=source_name):
                    source = input_root / source_name
                    source.write_bytes((FIXTURE_ROOT / payload_name).read_bytes())
                    result = adapter(data, _raise_parser_error, source, input_root)
                    self.assertFalse(result.succeeded)
                    self.assertEqual(expected_code, result.findings[0].code)
                    self.assertNotIn("private parser detail", repr(result.findings))
                    self.assertEqual([], data.objects)


if __name__ == "__main__":
    unittest.main()
