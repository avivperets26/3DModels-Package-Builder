"""Stable preflight findings for the Blender failure and regression fixture portfolio."""

from __future__ import annotations

from dataclasses import dataclass

from .inspection_common import InspectionFinding, required_name


@dataclass(frozen=True, slots=True)
class RegressionObservation:
    """Bounded failure facts collected by import and inspection adapters."""

    fixture_id: str
    source_format: str
    parser_succeeded: bool
    mesh_count: int
    missing_image_count: int = 0
    skeleton_count: int = 0
    mesh_without_uv_count: int = 0
    unsupported_data_types: tuple[str, ...] = ()
    invalid_animation_count: int = 0


@dataclass(frozen=True, slots=True)
class RegressionValidationReport:
    """Successful fixture preflight without a release-blocking condition."""

    fixture_id: str
    source_format: str
    mesh_count: int


@dataclass(frozen=True, slots=True)
class RegressionValidationResult:
    """Non-throwing regression validation result with deterministic findings."""

    report: RegressionValidationReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether the observation contains no known blocking regression."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-regression-validator")


def validate_regression_observation(
    observation: RegressionObservation,
) -> RegressionValidationResult:
    """Map bounded inspection facts to stable findings without leaking parser details."""

    try:
        fixture_id = required_name(observation.fixture_id)
        if fixture_id != fixture_id.strip() or "/" in fixture_id or "\\" in fixture_id:
            raise ValueError
        source_format = required_name(observation.source_format).lower()
        if source_format not in {"fbx", "glb"}:
            raise ValueError
        if type(observation.parser_succeeded) is not bool:
            raise ValueError
        numeric_values = (
            observation.mesh_count,
            observation.missing_image_count,
            observation.skeleton_count,
            observation.mesh_without_uv_count,
            observation.invalid_animation_count,
        )
        if any(type(value) is not int or value < 0 for value in numeric_values):
            raise ValueError
        unsupported = tuple(required_name(item) for item in observation.unsupported_data_types)
        if len(set(unsupported)) != len(unsupported):
            raise ValueError
    except Exception:
        return RegressionValidationResult(
            None,
            (
                _finding(
                    "BLENDER_REGRESSION_INPUT_INVALID",
                    "Regression preflight facts are incomplete, malformed, or unsupported.",
                    "Discard the observation and rerun bounded source inspection.",
                ),
            ),
        )

    findings: list[InspectionFinding] = []
    if not observation.parser_succeeded:
        findings.append(
            _finding(
                "BLENDER_SOURCE_CORRUPT",
                "The source could not be parsed as its declared 3D format.",
                "Replace or repair the source file before building a package.",
            )
        )
    if observation.missing_image_count:
        findings.append(
            _finding(
                "BLENDER_TEXTURE_REFERENCE_MISSING",
                "One or more material image references are missing from the bounded source set.",
                "Supply each referenced image beneath the approved input root and retry.",
            )
        )
    if observation.skeleton_count > 1:
        findings.append(
            _finding(
                "BLENDER_MULTIPLE_RIGS_UNSUPPORTED",
                "A single product contains multiple skeleton roots and cannot be normalized safely.",
                "Select one intended rig in the manifest or split the source into separate products.",
            )
        )
    if observation.mesh_without_uv_count:
        findings.append(
            _finding(
                "BLENDER_UV_REQUIRED",
                "One or more intended textured meshes have no UV layer.",
                "Create and review UVs for every intended textured mesh before export.",
            )
        )
    if unsupported:
        findings.append(
            _finding(
                "BLENDER_DATA_UNSUPPORTED",
                "The source contains data types outside the normalized mesh/armature boundary.",
                "Convert or explicitly remove unsupported source data before retrying.",
            )
        )
    if observation.invalid_animation_count:
        findings.append(
            _finding(
                "BLENDER_ANIMATION_INVALID",
                "One or more Actions have invalid ranges, channels, samples, or motion data.",
                "Repair the affected Action and rerun animation inspection and baking.",
            )
        )
    if findings:
        return RegressionValidationResult(None, tuple(findings))
    if observation.mesh_count == 0:
        return RegressionValidationResult(
            None,
            (
                _finding(
                    "BLENDER_MESH_REQUIRED",
                    "The source contains no intended mesh for normalized export.",
                    "Select a source containing at least one intended mesh.",
                ),
            ),
        )
    return RegressionValidationResult(
        RegressionValidationReport(fixture_id, source_format, observation.mesh_count), ()
    )
