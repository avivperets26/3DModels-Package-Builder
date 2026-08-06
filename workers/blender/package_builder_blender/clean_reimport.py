"""Fresh-process FBX/GLB clean-reimport comparison and deformation validation."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from .case_inference import RIGGED, RIGGED_ANIMATED, STATIC
from .inspection_common import (
    InspectionFinding,
    finite_components,
    finite_number,
    required_name,
)


@dataclass(frozen=True, slots=True)
class ReimportCounts:
    """Expected or observed asset inventory after one clean import."""

    object_count: int
    mesh_count: int
    material_count: int
    skeleton_count: int
    animation_count: int


@dataclass(frozen=True, slots=True)
class DeformationSample:
    """One representative evaluated vertex position for a clip/frame."""

    object_name: str
    clip_name: str
    frame: float
    vertex_index: int
    position: tuple[float, float, float]


@dataclass(frozen=True, slots=True)
class CleanReimportExpectation:
    """Reference metrics for one normalized artifact."""

    source_filename: str
    product_case: str
    counts: ReimportCounts
    bounds: tuple[float, float, float, float, float, float]
    deformation_samples: tuple[DeformationSample, ...] = ()


@dataclass(frozen=True, slots=True)
class FreshProcessObservation:
    """Metrics returned by one independently started empty Blender process."""

    process_instance_id: str
    started_from_empty_scene: bool
    source_filename: str
    counts: ReimportCounts
    bounds: tuple[float, float, float, float, float, float]
    deformation_samples: tuple[DeformationSample, ...] = ()


@dataclass(frozen=True, slots=True)
class CleanReimportRequest:
    """Contained artifact request passed to the fresh-process runner."""

    source_path: Path
    source_format: str
    expectation: CleanReimportExpectation


@dataclass(frozen=True, slots=True)
class ArtifactReimportReport:
    """Successful comparison metrics for one independently imported artifact."""

    source_filename: str
    source_format: str
    process_instance_id: str
    counts: ReimportCounts
    maximum_bounds_delta: float
    maximum_deformation_delta: float


@dataclass(frozen=True, slots=True)
class CleanReimportReport:
    """Deterministic clean-reimport comparison set."""

    artifacts: tuple[ArtifactReimportReport, ...]


@dataclass(frozen=True, slots=True)
class CleanReimportResult:
    """Non-throwing expected result for fresh-process clean reimport."""

    report: CleanReimportReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether every fresh import matched its reference metrics."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-clean-reimport-validator")


def _validate_counts(counts: ReimportCounts, product_case: str) -> None:
    values = (
        counts.object_count,
        counts.mesh_count,
        counts.material_count,
        counts.skeleton_count,
        counts.animation_count,
    )
    if any(type(value) is not int or value < 0 for value in values):
        raise ValueError
    if counts.object_count == 0 or counts.mesh_count == 0:
        raise ValueError
    if product_case == STATIC and (counts.skeleton_count or counts.animation_count):
        raise ValueError
    if product_case == RIGGED and (counts.skeleton_count != 1 or counts.animation_count):
        raise ValueError
    if product_case == RIGGED_ANIMATED and (
        counts.skeleton_count != 1 or counts.animation_count == 0
    ):
        raise ValueError


def _validate_samples(samples: tuple[DeformationSample, ...]) -> None:
    identities: set[tuple[str, str, float, int]] = set()
    for sample in samples:
        object_name = required_name(sample.object_name)
        clip_name = required_name(sample.clip_name)
        frame = finite_number(sample.frame)
        if type(sample.vertex_index) is not int or sample.vertex_index < 0:
            raise ValueError
        finite_components(sample.position, 3)
        identity = (object_name, clip_name, frame, sample.vertex_index)
        if identity in identities:
            raise ValueError
        identities.add(identity)


def _safe_request(root: Path, expectation: CleanReimportExpectation) -> CleanReimportRequest:
    filename = required_name(expectation.source_filename)
    if filename != filename.strip() or "/" in filename or "\\" in filename:
        raise ValueError
    source = root / filename
    resolved_root = root.resolve(strict=True)
    resolved_source = source.resolve(strict=True)
    suffix = resolved_source.suffix.lower()
    if (
        root.is_symlink()
        or not resolved_source.is_relative_to(resolved_root)
        or not resolved_source.is_file()
        or source.is_symlink()
        or source.stat().st_size <= 0
        or suffix not in {".fbx", ".glb"}
    ):
        raise ValueError
    if expectation.product_case not in {STATIC, RIGGED, RIGGED_ANIMATED}:
        raise ValueError
    _validate_counts(expectation.counts, expectation.product_case)
    finite_components(expectation.bounds, 6)
    if any(expectation.bounds[index] > expectation.bounds[index + 3] for index in range(3)):
        raise ValueError
    _validate_samples(expectation.deformation_samples)
    if expectation.product_case == RIGGED_ANIMATED and not expectation.deformation_samples:
        raise ValueError
    if expectation.product_case != RIGGED_ANIMATED and expectation.deformation_samples:
        raise ValueError
    return CleanReimportRequest(resolved_source, suffix[1:], expectation)


def _sample_delta(
    expected: tuple[DeformationSample, ...], observed: tuple[DeformationSample, ...]
) -> float:
    _validate_samples(observed)
    expected_by_id = {
        (item.object_name, item.clip_name, item.frame, item.vertex_index): item for item in expected
    }
    observed_by_id = {
        (item.object_name, item.clip_name, item.frame, item.vertex_index): item for item in observed
    }
    if set(expected_by_id) != set(observed_by_id):
        raise LookupError
    return max(
        (
            abs(expected_component - observed_component)
            for identity, expected_sample in expected_by_id.items()
            for expected_component, observed_component in zip(
                expected_sample.position, observed_by_id[identity].position, strict=True
            )
        ),
        default=0.0,
    )


def validate_clean_reimports(
    artifact_root: Path,
    expectations: tuple[CleanReimportExpectation, ...],
    fresh_process_runner: Callable[[CleanReimportRequest], FreshProcessObservation],
    *,
    bounds_tolerance: float = 1e-5,
    deformation_tolerance: float = 1e-4,
) -> CleanReimportResult:
    """Run every artifact in a distinct empty process and compare deterministic metrics."""

    try:
        root = Path(artifact_root)
        bounds_tolerance = finite_number(bounds_tolerance)
        deformation_tolerance = finite_number(deformation_tolerance)
        if (
            not root.is_absolute()
            or not root.is_dir()
            or root.is_symlink()
            or not expectations
            or bounds_tolerance < 0
            or deformation_tolerance < 0
        ):
            raise ValueError
        requests = tuple(_safe_request(root, item) for item in expectations)
        filenames = tuple(item.expectation.source_filename for item in requests)
        if len(set(filenames)) != len(filenames):
            raise ValueError
    except Exception:
        return CleanReimportResult(
            None,
            (
                _finding(
                    "BLENDER_CLEAN_REIMPORT_INPUT_INVALID",
                    "Clean-reimport expectations, tolerances, or contained artifacts are invalid.",
                    "Repair the normalized artifact set and reference metrics before retrying.",
                ),
            ),
        )

    process_ids: set[str] = set()
    reports: list[ArtifactReimportReport] = []
    for request in requests:
        try:
            observed = fresh_process_runner(request)
            process_id = required_name(observed.process_instance_id)
            if (
                not observed.started_from_empty_scene
                or process_id in process_ids
                or required_name(observed.source_filename) != request.expectation.source_filename
            ):
                raise PermissionError
            process_ids.add(process_id)
            _validate_counts(observed.counts, request.expectation.product_case)
            observed_bounds = finite_components(observed.bounds, 6)
            bounds_delta = max(
                abs(expected - actual)
                for expected, actual in zip(
                    request.expectation.bounds, observed_bounds, strict=True
                )
            )
            deformation_delta = _sample_delta(
                request.expectation.deformation_samples, observed.deformation_samples
            )
            if (
                observed.counts != request.expectation.counts
                or bounds_delta > bounds_tolerance
                or deformation_delta > deformation_tolerance
            ):
                raise LookupError
        except PermissionError:
            return CleanReimportResult(
                None,
                (
                    _finding(
                        "BLENDER_CLEAN_REIMPORT_PROCESS_INVALID",
                        "An artifact was not observed from its own verified empty Blender process.",
                        "Start one new empty Blender process per artifact and retry validation.",
                    ),
                ),
            )
        except LookupError:
            return CleanReimportResult(
                None,
                (
                    _finding(
                        "BLENDER_CLEAN_REIMPORT_MISMATCH",
                        "Reimported counts, bounds, or representative deformation differ from the reference.",
                        "Reject the artifact and review normalization/export diagnostics.",
                    ),
                ),
            )
        except Exception:
            return CleanReimportResult(
                None,
                (
                    _finding(
                        "BLENDER_CLEAN_REIMPORT_FAILED",
                        "A fresh Blender process failed before producing a complete observation.",
                        "Review the sanitized worker log and retry in a new process.",
                    ),
                ),
            )
        reports.append(
            ArtifactReimportReport(
                request.expectation.source_filename,
                request.source_format,
                process_id,
                observed.counts,
                bounds_delta,
                deformation_delta,
            )
        )
    return CleanReimportResult(
        CleanReimportReport(tuple(sorted(reports, key=lambda item: item.source_filename))), ()
    )
