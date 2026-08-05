"""Safe, deterministic GLB import boundary for the Package Builder Blender worker."""

from __future__ import annotations

import math
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from package_builder_blender.scene_utils import reset_scene

_FINISHED = frozenset({"FINISHED"})
_SHADING_MODES = frozenset({"NORMALS", "FLAT", "SMOOTH"})
_BONE_HEURISTICS = frozenset({"BLENDER", "TEMPERANCE", "FORTUNE"})
_LIGHTING_MODES = frozenset({"SPEC", "COMPAT", "RAW"})


@dataclass(frozen=True, slots=True)
class GlbImportSettings:
    """Explicit Blender 5.0 glTF operator settings retained in the import report."""

    pack_images: bool = True
    merge_vertices: bool = False
    shading: str = "NORMALS"
    bone_heuristic: str = "BLENDER"
    guess_original_bind_pose: bool = True
    disable_bone_shape: bool = True
    bone_shape_scale_factor: float = 1.0
    import_webp_texture: bool = False
    import_unused_materials: bool = True
    select_created_objects: bool = False
    import_scene_extras: bool = False
    import_scene_as_collection: bool = False
    merge_material_slots: bool = False
    lighting_mode: str = "SPEC"


@dataclass(frozen=True, slots=True)
class GlbImportReport:
    """Deterministic non-sensitive facts recorded after one successful GLB import."""

    source_name: str
    settings: GlbImportSettings
    object_count: int
    mesh_count: int
    material_count: int
    image_count: int
    packed_image_count: int
    armature_count: int
    skinned_mesh_count: int
    animation_count: int


@dataclass(frozen=True, slots=True)
class GlbImportFinding:
    """Stable sanitized finding that maps directly to the shared worker protocol."""

    code: str
    explanation: str
    suggested_action: str

    def as_protocol_value(self) -> dict[str, Any]:
        """Return the PB-0109-compatible protocol representation."""

        return {
            "code": self.code,
            "severity": "error",
            "explanation": self.explanation,
            "source": "blender-glb-importer",
            "suggestedAction": self.suggested_action,
            "blocksRelease": True,
        }


@dataclass(frozen=True, slots=True)
class GlbImportResult:
    """Non-throwing expected result for the GLB import boundary."""

    report: GlbImportReport | None
    findings: tuple[GlbImportFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether the operation produced a complete import report."""

        return self.report is not None and not self.findings


@dataclass(frozen=True, slots=True)
class _ImportBaseline:
    """Identity snapshot used to exclude Blender data that predates the import."""

    objects: frozenset[int]
    materials: frozenset[int]
    images: frozenset[int]
    actions: frozenset[int]


def _finding(code: str, explanation: str, suggested_action: str) -> GlbImportFinding:
    return GlbImportFinding(code, explanation, suggested_action)


def _failure(*findings: GlbImportFinding) -> GlbImportResult:
    return GlbImportResult(None, tuple(findings))


def _validate_settings(settings: GlbImportSettings) -> GlbImportFinding | None:
    if not isinstance(settings, GlbImportSettings):
        return _finding(
            "BLENDER_GLB_IMPORT_SETTINGS_INVALID",
            "The requested GLB import settings are invalid.",
            "Use supported shading, bone, lighting, image, and scene-import settings.",
        )
    boolean_values = (
        settings.pack_images,
        settings.merge_vertices,
        settings.guess_original_bind_pose,
        settings.disable_bone_shape,
        settings.import_webp_texture,
        settings.import_unused_materials,
        settings.select_created_objects,
        settings.import_scene_extras,
        settings.import_scene_as_collection,
        settings.merge_material_slots,
    )
    if (
        any(type(value) is not bool for value in boolean_values)
        or not isinstance(settings.shading, str)
        or not isinstance(settings.bone_heuristic, str)
        or not isinstance(settings.lighting_mode, str)
        or settings.shading not in _SHADING_MODES
        or settings.bone_heuristic not in _BONE_HEURISTICS
        or settings.lighting_mode not in _LIGHTING_MODES
        or type(settings.bone_shape_scale_factor) not in {float, int}
        or not math.isfinite(settings.bone_shape_scale_factor)
        or settings.bone_shape_scale_factor <= 0
    ):
        return _finding(
            "BLENDER_GLB_IMPORT_SETTINGS_INVALID",
            "The requested GLB import settings are invalid.",
            "Use supported shading, bone, lighting, image, and scene-import settings.",
        )
    return None


def _resolve_source(
    source_path: Path, input_root: Path
) -> tuple[Path | None, GlbImportFinding | None]:
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
            or resolved_source.suffix.casefold() != ".glb"
            or resolved_source.stat().st_size <= 0
        ):
            raise OSError
    except (OSError, RuntimeError):
        return None, _finding(
            "BLENDER_GLB_SOURCE_INVALID",
            "The GLB source is missing, empty, linked, outside the input root, or not a regular GLB file.",
            "Provide one non-empty regular .glb file beneath the contained input directory.",
        )
    return resolved_source, None


def _capture_baseline(data: Any) -> _ImportBaseline:
    """Capture direct-data identities after reset so linked retained data is not misreported."""

    return _ImportBaseline(
        frozenset(id(value) for value in data.objects),
        frozenset(id(value) for value in data.materials),
        frozenset(id(value) for value in data.images),
        frozenset(id(value) for value in data.actions),
    )


def _created(values: Iterable[Any], baseline: frozenset[int]) -> tuple[Any, ...]:
    """Return only data-block identities created after the import baseline."""

    return tuple(value for value in values if id(value) not in baseline)


def _is_packed_image(image: Any) -> bool:
    """Recognize Blender's single or tiled packed-image representations."""

    return getattr(image, "packed_file", None) is not None or bool(
        tuple(getattr(image, "packed_files", ()))
    )


def _is_skinned_mesh(value: Any) -> bool:
    """Recognize a mesh connected to an imported armature without using UI context."""

    if getattr(value, "type", None) != "MESH":
        return False
    parent = getattr(value, "parent", None)
    if getattr(parent, "type", None) == "ARMATURE":
        return True
    return any(
        getattr(modifier, "type", None) == "ARMATURE"
        for modifier in tuple(getattr(value, "modifiers", ()))
    )


def _cleanup_after_failure(data: Any) -> GlbImportFinding | None:
    try:
        reset_scene(data)
    except Exception:
        return _finding(
            "BLENDER_GLB_IMPORT_CLEANUP_FAILED",
            "Blender could not remove partial data after the GLB import failed.",
            "Discard the operation workspace and retry in a new Blender process.",
        )
    return None


def import_glb(
    data: Any,
    importer: Callable[..., object],
    source_path: Path,
    input_root: Path,
    settings: GlbImportSettings | None = None,
) -> GlbImportResult:
    """Reset the scene and import one contained GLB with explicit reproducible options.

    ``data`` is ``bpy.data`` and ``importer`` is ``bpy.ops.import_scene.gltf``. The boundary
    deliberately accepts canonical single-file GLB input only. Separate ``.gltf`` dependency
    graphs require a later contained-reference preflight and are not silently followed here.
    """

    selected_settings = GlbImportSettings() if settings is None else settings
    settings_finding = _validate_settings(selected_settings)
    if settings_finding is not None:
        return _failure(settings_finding)

    source, source_finding = _resolve_source(source_path, input_root)
    if source_finding is not None:
        return _failure(source_finding)

    try:
        reset_scene(data)
        baseline = _capture_baseline(data)
    except Exception:
        return _failure(
            _finding(
                "BLENDER_SCENE_RESET_FAILED",
                "Blender could not establish an empty scene before GLB import.",
                "Discard the operation workspace and retry in a new Blender process.",
            )
        )

    try:
        operator_result = importer(
            filepath=str(source),
            import_pack_images=selected_settings.pack_images,
            merge_vertices=selected_settings.merge_vertices,
            import_shading=selected_settings.shading,
            bone_heuristic=selected_settings.bone_heuristic,
            guess_original_bind_pose=selected_settings.guess_original_bind_pose,
            disable_bone_shape=selected_settings.disable_bone_shape,
            bone_shape_scale_factor=float(selected_settings.bone_shape_scale_factor),
            import_webp_texture=selected_settings.import_webp_texture,
            import_unused_materials=selected_settings.import_unused_materials,
            import_select_created_objects=selected_settings.select_created_objects,
            import_scene_extras=selected_settings.import_scene_extras,
            import_scene_as_collection=selected_settings.import_scene_as_collection,
            import_merge_material_slots=selected_settings.merge_material_slots,
            export_import_convert_lighting_mode=selected_settings.lighting_mode,
        )
    except Exception:
        import_finding = _finding(
            "BLENDER_GLB_IMPORT_FAILED",
            "Blender could not import the GLB source.",
            "Review the retained worker log and retry with a supported glTF 2.0 binary file.",
        )
        cleanup_finding = _cleanup_after_failure(data)
        return _failure(
            import_finding,
            *(() if cleanup_finding is None else (cleanup_finding,)),
        )

    if operator_result != _FINISHED:
        rejected_finding = _finding(
            "BLENDER_GLB_IMPORT_REJECTED",
            "Blender did not complete the GLB import.",
            "Review the retained worker log and retry with a supported glTF 2.0 binary file.",
        )
        cleanup_finding = _cleanup_after_failure(data)
        return _failure(
            rejected_finding,
            *(() if cleanup_finding is None else (cleanup_finding,)),
        )

    try:
        objects = _created(tuple(data.objects), baseline.objects)
        materials = _created(tuple(data.materials), baseline.materials)
        images = _created(tuple(data.images), baseline.images)
        actions = _created(tuple(data.actions), baseline.actions)
        object_types = tuple(getattr(value, "type", None) for value in objects)
        packed_image_count = sum(_is_packed_image(value) for value in images)
        skinned_mesh_count = sum(_is_skinned_mesh(value) for value in objects)
    except Exception:
        invalid_result_finding = _finding(
            "BLENDER_GLB_IMPORT_RESULT_INVALID",
            "Blender returned unreadable scene data after GLB import.",
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
                "BLENDER_GLB_IMPORT_EMPTY",
                "The GLB import completed without creating any scene objects.",
                "Verify that the GLB contains supported scene data and retry.",
            )
        )

    return GlbImportResult(
        GlbImportReport(
            source_name=source.name,
            settings=selected_settings,
            object_count=len(objects),
            mesh_count=object_types.count("MESH"),
            material_count=len(materials),
            image_count=len(images),
            packed_image_count=packed_image_count,
            armature_count=object_types.count("ARMATURE"),
            skinned_mesh_count=skinned_mesh_count,
            animation_count=len(actions),
        ),
        (),
    )
