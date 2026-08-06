"""Validate real PB-0417 process observations through the production contract."""

from __future__ import annotations

import json
import sys
from pathlib import Path


def _arguments() -> tuple[Path, Path, Path, Path]:
    """Read repository, artifact, observation, and report paths."""

    separator = sys.argv.index("--")
    values = sys.argv[separator + 1 :]
    if len(values) != 4:
        raise ValueError("Expected repository, artifact, observation, and report paths.")
    return tuple(Path(value).resolve() for value in values)  # type: ignore[return-value]


REPOSITORY_ROOT, ARTIFACT_ROOT, OBSERVATION_ROOT, REPORT_FILE = _arguments()
sys.path.insert(0, str(REPOSITORY_ROOT / "workers" / "blender"))

from package_builder_blender.clean_reimport import (  # noqa: E402
    CleanReimportExpectation,
    DeformationSample,
    FreshProcessObservation,
    ReimportCounts,
    validate_clean_reimports,
)


def _counts(value: dict[str, int]) -> ReimportCounts:
    """Convert retained JSON counts to the production immutable value."""

    return ReimportCounts(
        value["objectCount"],
        value["meshCount"],
        value["materialCount"],
        value["skeletonCount"],
        value["animationCount"],
    )


def _samples(values: list[dict[str, object]]) -> tuple[DeformationSample, ...]:
    """Convert retained representative positions to production samples."""

    return tuple(
        DeformationSample(
            str(value["objectName"]),
            str(value["clipName"]),
            float(value["frame"]),
            int(value["vertexIndex"]),
            tuple(float(component) for component in value["position"]),  # type: ignore[arg-type]
        )
        for value in values
    )


def main() -> None:
    """Compare every fresh-process result and retain the PB-0417 report."""

    source = json.loads((ARTIFACT_ROOT / "expectations.json").read_text(encoding="utf-8"))
    expectations = tuple(
        CleanReimportExpectation(
            item["sourceFilename"],
            item["productCase"],
            _counts(item["counts"]),
            tuple(float(component) for component in item["bounds"]),  # type: ignore[arg-type]
            _samples(item["deformationSamples"]),
        )
        for item in source["artifacts"]
    )
    observed_by_name: dict[str, FreshProcessObservation] = {}
    for path in sorted(OBSERVATION_ROOT.glob("*.json")):
        item = json.loads(path.read_text(encoding="utf-8"))
        observed_by_name[item["sourceFilename"]] = FreshProcessObservation(
            item["processInstanceId"],
            item["startedFromEmptyScene"],
            item["sourceFilename"],
            _counts(item["counts"]),
            tuple(float(component) for component in item["bounds"]),
            _samples(item["deformationSamples"]),
        )

    result = validate_clean_reimports(
        ARTIFACT_ROOT,
        expectations,
        lambda request: observed_by_name[request.expectation.source_filename],
        bounds_tolerance=1e-4,
        deformation_tolerance=1e-4,
    )
    payload = {
        "succeeded": result.succeeded,
        "findingCodes": [finding.code for finding in result.findings],
        "artifacts": []
        if result.report is None
        else [
            {
                "sourceFilename": item.source_filename,
                "sourceFormat": item.source_format,
                "processInstanceId": item.process_instance_id,
                "maximumBoundsDelta": item.maximum_bounds_delta,
                "maximumDeformationDelta": item.maximum_deformation_delta,
            }
            for item in result.report.artifacts
        ],
    }
    REPORT_FILE.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    if not result.succeeded:
        raise RuntimeError(f"PB-0417 validation failed: {payload['findingCodes']}")
    print(f"PB-0417 real clean reimports passed: {len(payload['artifacts'])}/3")


if __name__ == "__main__":
    main()
