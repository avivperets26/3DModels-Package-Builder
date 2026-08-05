"""Safe, deterministic FBX import boundary for the Package Builder Blender worker."""

from __future__ import annotations

import math
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from package_builder_blender.scene_utils import reset_scene

_AXES = frozenset({"X", "Y", "Z", "-X", "-Y", "-Z"})
_FINISHED = frozenset({"FINISHED"})


@dataclass(frozen=True, slots=True)
class FbxImportSettings:
    """Explicit FBX operator settings retained with every successful import report."""

    axis_forward: str = "-Z"
    axis_up: str = "Y"
    global_scale: float = 1.0


@dataclass(frozen=True, slots=True)
class FbxImportReport:
    """Deterministic non-sensitive facts recorded after one successful FBX import."""

    source_name: str
    settings: FbxImportSettings
    object_count: int
    mesh_count: int
    armature_count: int


@dataclass(frozen=True, slots=True)
class FbxImportFinding:
    """Stable sanitized finding that can be mapped to the shared worker protocol."""

    code: str
    explanation: str
    suggested_action: str

    def as_protocol_value(self) -> dict[str, Any]:
        """Return the PB-0109-compatible protocol representation."""

        return {
            "code": self.code,
            "severity": "error",
            "explanation": self.explanation,
            "source": "blender-fbx-importer",
            "suggestedAction": self.suggested_action,
            "blocksRelease": True,
        }


@dataclass(frozen=True, slots=True)
class FbxImportResult:
    """Non-throwing expected result for the FBX import boundary."""

    report: FbxImportReport | None
    findings: tuple[FbxImportFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether the operation produced a complete import report."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, suggested_action: str) -> FbxImportFinding:
    return FbxImportFinding(code, explanation, suggested_action)


def _failure(*findings: FbxImportFinding) -> FbxImportResult:
    return FbxImportResult(None, tuple(findings))


def _validate_settings(settings: FbxImportSettings) -> FbxImportFinding | None:
    if (
        not isinstance(settings.axis_forward, str)
        or not isinstance(settings.axis_up, str)
        or settings.axis_forward not in _AXES
        or settings.axis_up not in _AXES
        or settings.axis_forward.removeprefix("-") == settings.axis_up.removeprefix("-")
        or type(settings.global_scale) not in {float, int}
        or not math.isfinite(settings.global_scale)
        or settings.global_scale < 0.001
        or settings.global_scale > 1000
    ):
        return _finding(
            "BLENDER_FBX_IMPORT_SETTINGS_INVALID",
            "The requested FBX axis or unit settings are invalid.",
            "Choose different forward/up axes and a finite scale from 0.001 through 1000.",
        )
    return None


def _resolve_source(
    source_path: Path, input_root: Path
) -> tuple[Path | None, FbxImportFinding | None]:
    try:
        if (
            not isinstance(source_path, Path)
            or not isinstance(input_root, Path)
            or not source_path.is_absolute()
            or not input_root.is_absolute()
            or input_root.is_symlink()
            or source_path.is_symlink()
        ):
            raise OSError
        resolved_root = input_root.resolve(strict=True)
        resolved_source = source_path.resolve(strict=True)
        if (
            not resolved_root.is_dir()
            or not resolved_source.is_file()
            or str(input_root).casefold() != str(resolved_root).casefold()
            or str(source_path).casefold() != str(resolved_source).casefold()
            or not resolved_source.is_relative_to(resolved_root)
            or resolved_source.suffix.casefold() != ".fbx"
            or resolved_source.stat().st_size <= 0
        ):
            raise OSError
    except (OSError, RuntimeError):
        return None, _finding(
            "BLENDER_FBX_SOURCE_INVALID",
            "The FBX source is missing, empty, linked, outside the input root, or not a regular FBX file.",
            "Provide one non-empty regular .fbx file beneath the contained input directory.",
        )
    return resolved_source, None


def _cleanup_after_failure(data: Any) -> FbxImportFinding | None:
    try:
        reset_scene(data)
    except Exception:
        return _finding(
            "BLENDER_FBX_IMPORT_CLEANUP_FAILED",
            "Blender could not remove partial data after the FBX import failed.",
            "Discard the operation workspace and retry in a new Blender process.",
        )
    return None


def import_fbx(
    data: Any,
    importer: Callable[..., object],
    source_path: Path,
    input_root: Path,
    settings: FbxImportSettings | None = None,
) -> FbxImportResult:
    """Reset the scene and import one contained FBX with explicit reproducible options.

    ``data`` is ``bpy.data`` and ``importer`` is ``bpy.ops.import_scene.fbx``. Accepting only these
    narrow dependencies keeps selection, active-object, editor-area, and mode state outside the
    adapter. Expected input and Blender operator failures become sanitized stable findings.
    """

    selected_settings = settings or FbxImportSettings()
    settings_finding = _validate_settings(selected_settings)
    if settings_finding is not None:
        return _failure(settings_finding)

    source, source_finding = _resolve_source(source_path, input_root)
    if source_finding is not None:
        return _failure(source_finding)

    try:
        reset_scene(data)
    except Exception:
        return _failure(
            _finding(
                "BLENDER_SCENE_RESET_FAILED",
                "Blender could not establish an empty scene before FBX import.",
                "Discard the operation workspace and retry in a new Blender process.",
            )
        )

    try:
        operator_result = importer(
            filepath=str(source),
            global_scale=float(selected_settings.global_scale),
            use_manual_orientation=True,
            axis_forward=selected_settings.axis_forward,
            axis_up=selected_settings.axis_up,
            bake_space_transform=False,
            use_custom_normals=True,
            colors_type="SRGB",
            use_image_search=False,
            use_anim=True,
            anim_offset=1.0,
            use_subsurf=False,
            use_custom_props=False,
            ignore_leaf_bones=False,
            force_connect_children=False,
            automatic_bone_orientation=False,
            use_prepost_rot=True,
        )
    except Exception:
        import_finding = _finding(
            "BLENDER_FBX_IMPORT_FAILED",
            "Blender could not import the FBX source.",
            "Review the retained worker log and retry with a supported binary FBX file.",
        )
        cleanup_finding = _cleanup_after_failure(data)
        return _failure(
            import_finding,
            *(() if cleanup_finding is None else (cleanup_finding,)),
        )

    if operator_result != _FINISHED:
        rejected_finding = _finding(
            "BLENDER_FBX_IMPORT_REJECTED",
            "Blender did not complete the FBX import.",
            "Review the retained worker log and retry with a supported binary FBX file.",
        )
        cleanup_finding = _cleanup_after_failure(data)
        return _failure(
            rejected_finding,
            *(() if cleanup_finding is None else (cleanup_finding,)),
        )

    try:
        objects = tuple(data.objects)
        object_types = tuple(getattr(value, "type", None) for value in objects)
    except Exception:
        invalid_result_finding = _finding(
            "BLENDER_FBX_IMPORT_RESULT_INVALID",
            "Blender returned unreadable scene data after FBX import.",
            "Discard the operation workspace and retry in a new Blender process.",
        )
        cleanup_finding = _cleanup_after_failure(data)
        return _failure(
            invalid_result_finding,
            *(() if cleanup_finding is None else (cleanup_finding,)),
        )
    if not objects:
        return _failure(
            _finding(
                "BLENDER_FBX_IMPORT_EMPTY",
                "The FBX import completed without creating any scene objects.",
                "Verify that the FBX contains supported scene data and retry.",
            )
        )

    return FbxImportResult(
        FbxImportReport(
            source_name=source.name,
            settings=selected_settings,
            object_count=len(objects),
            mesh_count=object_types.count("MESH"),
            armature_count=object_types.count("ARMATURE"),
        ),
        (),
    )
