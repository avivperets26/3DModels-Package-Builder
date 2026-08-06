"""PB-0417 fresh-process clean-reimport comparison tests."""

from __future__ import annotations

import sys
import tempfile
import unittest
from dataclasses import replace
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.case_inference import (  # noqa: E402
    RIGGED,
    RIGGED_ANIMATED,
    STATIC,
)
from package_builder_blender.clean_reimport import (  # noqa: E402
    CleanReimportExpectation,
    DeformationSample,
    FreshProcessObservation,
    ReimportCounts,
    validate_clean_reimports,
)


def _expectation(filename: str, *, animated: bool = False) -> CleanReimportExpectation:
    if animated:
        return CleanReimportExpectation(
            filename,
            RIGGED_ANIMATED,
            ReimportCounts(2, 1, 1, 1, 1),
            (-1.0, -2.0, -3.0, 1.0, 2.0, 3.0),
            (DeformationSample("P_Model", "A_Bow_Shoot", 12.0, 7, (0.25, 1.5, -0.5)),),
        )
    return CleanReimportExpectation(
        filename,
        STATIC,
        ReimportCounts(1, 1, 1, 0, 0),
        (-1.0, -2.0, -3.0, 1.0, 2.0, 3.0),
    )


def _observation(expectation, process_id: str) -> FreshProcessObservation:
    return FreshProcessObservation(
        process_id,
        True,
        expectation.source_filename,
        expectation.counts,
        expectation.bounds,
        expectation.deformation_samples,
    )


class CleanReimportTests(unittest.TestCase):
    def setUp(self) -> None:
        artifact_root = REPOSITORY_ROOT / "artifacts" / "validation" / "PB-0417"
        artifact_root.mkdir(parents=True, exist_ok=True)
        self.workspace = tempfile.TemporaryDirectory(dir=artifact_root)
        self.root = Path(self.workspace.name)
        (self.root / "Bow.fbx").write_bytes(b"fbx-fixture")
        (self.root / "Bow.glb").write_bytes(b"glb-fixture")

    def tearDown(self) -> None:
        self.workspace.cleanup()

    def test_each_fbx_and_glb_uses_a_distinct_fresh_process_and_matches(self) -> None:
        expectations = (_expectation("Bow.fbx"), _expectation("Bow.glb", animated=True))
        requests = []

        def runner(request):
            requests.append(request)
            return _observation(request.expectation, f"process-{len(requests)}")

        result = validate_clean_reimports(self.root, expectations, runner)
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertEqual(
            ("fbx", "glb"), tuple(item.source_format for item in result.report.artifacts)
        )
        self.assertEqual(2, len({item.process_instance_id for item in result.report.artifacts}))
        self.assertEqual(
            (0.0, 0.0), tuple(item.maximum_bounds_delta for item in result.report.artifacts)
        )

    def test_counts_or_bounds_outside_tolerance_returns_stable_mismatch(self) -> None:
        expectation = _expectation("Bow.fbx")
        observations = (
            replace(_observation(expectation, "one"), counts=ReimportCounts(2, 1, 1, 0, 0)),
            replace(
                _observation(expectation, "two"),
                bounds=(-1.0, -2.0, -3.0, 1.1, 2.0, 3.0),
            ),
        )
        for observation in observations:
            result = validate_clean_reimports(
                self.root, (expectation,), lambda _request, value=observation: value
            )
            self.assertFalse(result.succeeded)
            self.assertEqual("BLENDER_CLEAN_REIMPORT_MISMATCH", result.findings[0].code)

    def test_tolerated_bounds_and_deformation_drift_reports_maximum_delta(self) -> None:
        expectation = _expectation("Bow.glb", animated=True)
        sample = replace(
            expectation.deformation_samples[0],
            position=(0.25001, 1.5, -0.5),
        )
        observation = replace(
            _observation(expectation, "process-1"),
            bounds=(-1.0, -2.0, -3.0, 1.000001, 2.0, 3.0),
            deformation_samples=(sample,),
        )
        result = validate_clean_reimports(
            self.root,
            (expectation,),
            lambda _request: observation,
            bounds_tolerance=1e-5,
            deformation_tolerance=1e-4,
        )
        self.assertTrue(result.succeeded)
        assert result.report is not None
        self.assertAlmostEqual(0.000001, result.report.artifacts[0].maximum_bounds_delta)
        self.assertAlmostEqual(0.00001, result.report.artifacts[0].maximum_deformation_delta)

    def test_missing_or_changed_representative_deformation_blocks(self) -> None:
        expectation = _expectation("Bow.glb", animated=True)
        changed = replace(
            expectation.deformation_samples[0],
            position=(10.0, 10.0, 10.0),
        )
        for samples in ((), (changed,)):
            observation = replace(
                _observation(expectation, "process-1"), deformation_samples=samples
            )
            result = validate_clean_reimports(
                self.root, (expectation,), lambda _request, value=observation: value
            )
            self.assertFalse(result.succeeded)
            self.assertEqual("BLENDER_CLEAN_REIMPORT_MISMATCH", result.findings[0].code)

    def test_nonempty_or_reused_process_and_runner_exception_fail_closed(self) -> None:
        expectations = (_expectation("Bow.fbx"), _expectation("Bow.glb"))
        calls = 0

        def reused(request):
            nonlocal calls
            calls += 1
            return _observation(request.expectation, "same-process")

        result = validate_clean_reimports(self.root, expectations, reused)
        self.assertEqual("BLENDER_CLEAN_REIMPORT_PROCESS_INVALID", result.findings[0].code)

        nonempty = replace(
            _observation(expectations[0], "different"), started_from_empty_scene=False
        )
        result = validate_clean_reimports(self.root, (expectations[0],), lambda _request: nonempty)
        self.assertEqual("BLENDER_CLEAN_REIMPORT_PROCESS_INVALID", result.findings[0].code)

        def failed(_request):
            raise RuntimeError("private executable path")

        result = validate_clean_reimports(self.root, (expectations[0],), failed)
        self.assertEqual("BLENDER_CLEAN_REIMPORT_FAILED", result.findings[0].code)
        self.assertNotIn("private", repr(result.findings))

    def test_unsafe_missing_duplicate_or_semantically_invalid_expectation_blocks(self) -> None:
        invalid = (
            (_expectation("Missing.fbx"),),
            (_expectation("../Bow.fbx"),),
            (_expectation("Bow.fbx"), _expectation("Bow.fbx")),
            (replace(_expectation("Bow.fbx"), bounds=(1.0, 0.0, 0.0, -1.0, 1.0, 1.0)),),
            (
                replace(
                    _expectation("Bow.glb", animated=True),
                    deformation_samples=(),
                ),
            ),
        )
        for expectations in invalid:
            calls = []
            result = validate_clean_reimports(
                self.root,
                expectations,
                lambda request, items=calls: items.append(request),
            )
            self.assertFalse(result.succeeded)
            self.assertEqual("BLENDER_CLEAN_REIMPORT_INPUT_INVALID", result.findings[0].code)
            self.assertEqual([], calls)

        result = validate_clean_reimports(
            self.root,
            (_expectation("Bow.fbx"),),
            lambda _request: self.fail("runner must not execute"),
            bounds_tolerance=True,
        )
        self.assertEqual("BLENDER_CLEAN_REIMPORT_INPUT_INVALID", result.findings[0].code)

    def test_invalid_count_sample_case_and_empty_request_variants_fail_closed(self) -> None:
        invalid = (
            replace(_expectation("Bow.fbx"), counts=ReimportCounts(-1, 1, 1, 0, 0)),
            replace(_expectation("Bow.fbx"), counts=ReimportCounts(0, 0, 0, 0, 0)),
            replace(_expectation("Bow.fbx"), counts=ReimportCounts(2, 1, 1, 1, 0)),
            replace(
                _expectation("Bow.fbx"),
                product_case=RIGGED,
                counts=ReimportCounts(1, 1, 1, 0, 0),
            ),
            replace(
                _expectation("Bow.glb", animated=True),
                counts=ReimportCounts(2, 1, 1, 1, 0),
            ),
            replace(
                _expectation("Bow.glb", animated=True),
                deformation_samples=(
                    DeformationSample("P_Model", "A_Bow_Shoot", 1.0, -1, (0.0, 0.0, 0.0)),
                ),
            ),
            replace(
                _expectation("Bow.glb", animated=True),
                deformation_samples=(
                    DeformationSample("P_Model", "A_Bow_Shoot", 1.0, 0, (0.0, 0.0, 0.0)),
                    DeformationSample("P_Model", "A_Bow_Shoot", 1.0, 0, (0.0, 0.0, 0.0)),
                ),
            ),
            replace(
                _expectation("Bow.fbx"),
                deformation_samples=(
                    DeformationSample("P_Model", "A_Bow_Shoot", 1.0, 0, (0.0, 0.0, 0.0)),
                ),
            ),
            replace(_expectation("Bow.fbx"), source_filename="Bow.obj"),
            replace(_expectation("Bow.fbx"), product_case="collection"),
        )
        (self.root / "Bow.obj").write_bytes(b"fixture")
        for expectation in invalid:
            with self.subTest(expectation=expectation):
                result = validate_clean_reimports(
                    self.root,
                    (expectation,),
                    lambda _request: self.fail("runner must not execute"),
                )
                self.assertEqual("BLENDER_CLEAN_REIMPORT_INPUT_INVALID", result.findings[0].code)

        result = validate_clean_reimports(
            self.root,
            (),
            lambda _request: self.fail("runner must not execute"),
        )
        self.assertEqual("BLENDER_CLEAN_REIMPORT_INPUT_INVALID", result.findings[0].code)


if __name__ == "__main__":
    unittest.main()
