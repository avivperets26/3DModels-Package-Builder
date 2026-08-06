"""Transactional manifest-driven Blender material and image normalization."""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any

from .inspection_common import InspectionFinding, required_name
from .texture_inspection import TextureInspectionReport

_ROLE_LABELS = {
    "albedo": "Albedo",
    "normal": "Normal",
    "metallic": "Metallic",
    "roughness": "Roughness",
    "emission": "Emission",
    "ambient_occlusion": "AmbientOcclusion",
    "opacity": "Opacity",
    "height": "Height",
}
_SRGB_ROLES = {"albedo", "emission"}
_ASSET_ID = re.compile(r"[A-Za-z][A-Za-z0-9]*\Z")


@dataclass(frozen=True, slots=True)
class TextureNormalizationAssignment:
    """One image's canonical separate-map role and safe package filename."""

    image_name: str
    canonical_role: str
    target_filename: str
    explicit_role_override: bool = False


@dataclass(frozen=True, slots=True)
class MaterialImageNormalizationPlan:
    """Complete manifest-owned image assignment plan for one asset."""

    asset_id: str
    assignments: tuple[TextureNormalizationAssignment, ...]


@dataclass(frozen=True, slots=True)
class NormalizedImageReference:
    """One normalized Blender-relative texture reference."""

    image_name: str
    canonical_role: str
    blender_relative_path: str
    color_space: str


@dataclass(frozen=True, slots=True)
class MaterialImageNormalizationReport:
    """Deterministic normalized references without image pixel conversion."""

    images: tuple[NormalizedImageReference, ...]


@dataclass(frozen=True, slots=True)
class MaterialImageNormalizationResult:
    """Non-throwing expected result for material/image normalization."""

    report: MaterialImageNormalizationReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether every image was normalized exactly."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-material-normalizer")


def _safe_filename(value: Any) -> str:
    filename = required_name(value)
    if (
        filename != filename.strip()
        or filename in {".", ".."}
        or "/" in filename
        or "\\" in filename
        or not filename.lower().endswith((".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".exr"))
    ):
        raise ValueError
    return filename


def _inferred_roles(report: TextureInspectionReport, image_name: str) -> set[str]:
    return {item.probable_role for item in report.connections if item.image_name == image_name}


def normalize_material_images(
    images: tuple[Any, ...],
    inspection: TextureInspectionReport,
    plan: MaterialImageNormalizationPlan,
) -> MaterialImageNormalizationResult:
    """Normalize safe image paths and color spaces while retaining separate source maps."""

    try:
        asset_id = required_name(plan.asset_id)
        if not _ASSET_ID.fullmatch(asset_id):
            raise ValueError
        image_names = tuple(required_name(item.name) for item in images)
        inspected_names = tuple(item.name for item in inspection.images)
        assignments = plan.assignments
        assigned_names = tuple(required_name(item.image_name) for item in assignments)
        if (
            len(set(image_names)) != len(image_names)
            or set(image_names) != set(inspected_names)
            or set(assigned_names) != set(image_names)
            or len(assigned_names) != len(set(assigned_names))
        ):
            raise ValueError
        filenames: list[str] = []
        for assignment in assignments:
            role = required_name(assignment.canonical_role)
            if role not in _ROLE_LABELS:
                raise ValueError
            filename = _safe_filename(assignment.target_filename)
            stem = path_like_stem(filename)
            expected = f"T_{asset_id}_{_ROLE_LABELS[role]}"
            if stem != expected and not stem.startswith(expected + "_"):
                raise ValueError
            tokens = {
                "".join(character.lower() for character in segment if character.isalnum())
                for segment in stem.split("_")
            }
            if tokens.intersection({"orm", "metallicroughness"}):
                raise ValueError
            filenames.append(filename)
            inferred = _inferred_roles(inspection, assignment.image_name)
            unambiguous = len(inferred) == 1 and inferred.issubset(_ROLE_LABELS)
            if not assignment.explicit_role_override and (not unambiguous or role not in inferred):
                raise LookupError
        if len({item.casefold() for item in filenames}) != len(filenames):
            raise ValueError
    except LookupError:
        return MaterialImageNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_TEXTURE_ROLE_AMBIGUOUS",
                    "At least one image role is unknown, ambiguous, or contradicts the requested assignment.",
                    "Provide an explicit manifest role override after visually reviewing the source map.",
                ),
            ),
        )
    except Exception:
        return MaterialImageNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_MATERIAL_NORMALIZATION_PLAN_INVALID",
                    "The image plan is incomplete, unsafe, non-canonical, or combines separate maps.",
                    "Regenerate a complete collision-free canonical texture plan and retry.",
                ),
            ),
        )

    by_name = {item.name: item for item in images}
    inspected_by_name = {item.name: item for item in inspection.images}
    snapshots = {
        name: (item.filepath_raw, item.colorspace_settings.name) for name, item in by_name.items()
    }
    normalized: list[NormalizedImageReference] = []
    try:
        for assignment in sorted(assignments, key=lambda item: item.image_name):
            image = by_name[assignment.image_name]
            path = f"//Textures/{assignment.target_filename}"
            color_space = "sRGB" if assignment.canonical_role in _SRGB_ROLES else "Non-Color"
            image.filepath_raw = path
            image.colorspace_settings.name = color_space
            if image.filepath_raw != path or image.colorspace_settings.name != color_space:
                raise ValueError
            inspected = inspected_by_name[assignment.image_name]
            if (
                tuple(image.size) != (inspected.width, inspected.height)
                or image.file_format != inspected.file_format
            ):
                raise ValueError
            normalized.append(
                NormalizedImageReference(
                    assignment.image_name,
                    assignment.canonical_role,
                    path,
                    color_space,
                )
            )
    except Exception:
        rollback_failed = False
        for name, (path, color_space) in snapshots.items():
            try:
                by_name[name].filepath_raw = path
                by_name[name].colorspace_settings.name = color_space
            except Exception:
                rollback_failed = True
        return MaterialImageNormalizationResult(
            None,
            (
                _finding(
                    "BLENDER_MATERIAL_NORMALIZATION_FAILED",
                    "Blender rejected a normalized image reference or color-space assignment.",
                    (
                        "Discard the workspace and retry from the retained source."
                        if rollback_failed
                        else "Review the restored image data and retry."
                    ),
                ),
            ),
        )
    return MaterialImageNormalizationResult(MaterialImageNormalizationReport(tuple(normalized)), ())


def path_like_stem(filename: str) -> str:
    """Return a final filename stem after the caller has rejected every directory separator."""

    return filename.rsplit(".", 1)[0]
