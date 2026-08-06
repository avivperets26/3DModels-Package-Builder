"""Selection-safe normalized GLB export with embedded texture and animation policy."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .case_inference import RIGGED, RIGGED_ANIMATED, STATIC
from .export_sets import SelectionSafeExport
from .inspection_common import InspectionFinding, required_name
from .texture_inspection import TextureInspectionReport


@dataclass(frozen=True, slots=True)
class NormalizedGlbExportPlan:
    """Complete expected content and output identity for one normalized GLB."""

    asset_id: str
    product_case: str
    output_root: Path
    output_filename: str
    selected_object_names: tuple[str, ...]
    expected_material_names: tuple[str, ...]
    expected_image_names: tuple[str, ...]
    expected_action_names: tuple[str, ...] = ()
    copyright_notice: str = ""


@dataclass(frozen=True, slots=True)
class NormalizedGlbExportReport:
    """Verified GLB artifact and exact logical contents supplied to Blender."""

    output_filename: str
    byte_count: int
    product_case: str
    object_names: tuple[str, ...]
    material_names: tuple[str, ...]
    image_names: tuple[str, ...]
    action_names: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class NormalizedGlbExportResult:
    """Non-throwing expected result for normalized GLB export."""

    report: NormalizedGlbExportReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether Blender produced the exact requested GLB artifact."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-glb-exporter")


def _attached_material_names(objects: tuple[Any, ...]) -> tuple[str, ...]:
    names: set[str] = set()
    for scene_object in objects:
        if required_name(scene_object.type).upper() != "MESH":
            continue
        for slot in tuple(scene_object.material_slots):
            if slot.material is None:
                raise ValueError
            names.add(required_name(slot.material.name))
    return tuple(sorted(names))


def _safe_output(plan: NormalizedGlbExportPlan) -> Path:
    root = Path(plan.output_root)
    filename = required_name(plan.output_filename)
    if (
        not root.is_absolute()
        or not root.is_dir()
        or root.is_symlink()
        or filename != filename.strip()
        or filename in {".", ".."}
        or "/" in filename
        or "\\" in filename
        or not filename.lower().endswith(".glb")
        or not filename.startswith(required_name(plan.asset_id))
    ):
        raise ValueError
    resolved_root = root.resolve(strict=True)
    target = root / filename
    resolved_target = target.resolve(strict=False)
    if resolved_target.parent != resolved_root or target.exists() or target.is_symlink():
        raise ValueError
    return resolved_target


def _export_options(path: Path, plan: NormalizedGlbExportPlan, animated: bool) -> dict[str, Any]:
    """Return the reviewed Blender 5 glTF Binary export policy."""

    # Do not pass export_action_filter=False explicitly. Blender 5.0.0's background operator
    # callback dereferences a UI-only Scene collection when that property is assigned. Its official
    # default is already false, so omission keeps filtering disabled without emitting a traceback.
    return {
        "filepath": str(path),
        "check_existing": False,
        "export_import_convert_lighting_mode": "SPEC",
        "gltf_export_id": "package-builder",
        "export_use_gltfpack": False,
        "export_format": "GLB",
        "export_copyright": plan.copyright_notice,
        "export_image_format": "AUTO",
        "export_image_add_webp": False,
        "export_image_webp_fallback": False,
        "export_texture_dir": "",
        "export_jpeg_quality": 100,
        "export_image_quality": 100,
        "export_keep_originals": False,
        "export_texcoords": True,
        "export_normals": True,
        "export_gn_mesh": False,
        "export_draco_mesh_compression_enable": False,
        "export_tangents": True,
        "export_materials": "EXPORT",
        "export_unused_images": False,
        "export_unused_textures": False,
        "export_vertex_color": "MATERIAL",
        "export_all_vertex_colors": True,
        "export_active_vertex_color_when_no_material": True,
        "export_attributes": False,
        "use_mesh_edges": False,
        "use_mesh_vertices": False,
        "export_cameras": False,
        "use_selection": True,
        "use_visible": False,
        "use_renderable": False,
        "use_active_collection_with_nested": False,
        "use_active_collection": False,
        "use_active_scene": False,
        "collection": "",
        "at_collection_center": False,
        "export_extras": False,
        "export_yup": True,
        "export_apply": False,
        "export_shared_accessors": False,
        "export_animations": animated,
        "export_frame_range": False,
        "export_frame_step": 1,
        "export_force_sampling": True,
        "export_sampling_interpolation_fallback": "LINEAR",
        "export_pointer_animation": False,
        "export_animation_mode": "ACTIONS",
        "export_def_bones": True,
        "export_hierarchy_flatten_bones": False,
        "export_hierarchy_flatten_objs": False,
        "export_armature_object_remove": False,
        "export_leaf_bone": False,
        "export_optimize_animation_size": False,
        "export_negative_frame": "SLIDE",
        "export_anim_slide_to_zero": False,
        "export_bake_animation": False,
        "export_merge_animation": "ACTION",
        "export_anim_single_armature": True,
        "export_reset_pose_bones": True,
        "export_current_frame": False,
        "export_rest_position_armature": True,
        "export_anim_scene_split_object": True,
        "export_skins": True,
        "export_influence_nb": 4,
        "export_all_influences": False,
        "export_morph": True,
        "export_morph_normal": True,
        "export_morph_tangent": False,
        "export_morph_animation": True,
        "export_morph_reset_sk_data": True,
        "export_lights": False,
        "export_try_sparse_sk": True,
        "export_try_omit_sparse_sk": False,
        "export_gpu_instances": False,
        "export_convert_animation_pointer": False,
        "export_nla_strips": False,
        "export_original_specular": False,
        "will_save_settings": False,
        "export_hierarchy_full_collections": False,
        "export_extra_animations": False,
        "export_loglevel": -1,
    }


def _valid_glb_container(path: Path) -> bool:
    if path.is_symlink() or not path.is_file():
        return False
    byte_count = path.stat().st_size
    if byte_count < 20:
        return False
    with path.open("rb") as stream:
        header = stream.read(20)
    if len(header) != 20 or header[:4] != b"glTF":
        return False
    version = int.from_bytes(header[4:8], "little")
    declared_length = int.from_bytes(header[8:12], "little")
    json_length = int.from_bytes(header[12:16], "little")
    json_type = header[16:20]
    return (
        version == 2
        and declared_length == byte_count
        and declared_length % 4 == 0
        and json_type == b"JSON"
        and json_length > 0
        and json_length % 4 == 0
        and 20 + json_length <= byte_count
    )


def export_normalized_glb(
    all_objects: tuple[Any, ...],
    images: tuple[Any, ...],
    actions: tuple[Any, ...],
    texture_report: TextureInspectionReport,
    view_layer: Any,
    plan: NormalizedGlbExportPlan,
    export_operator: Callable[..., Any],
) -> NormalizedGlbExportResult:
    """Export exact manifest content to one embedded-texture GLB and restore UI state."""

    try:
        output_path = _safe_output(plan)
        object_names = tuple(required_name(item.name) for item in all_objects)
        selected_names = tuple(required_name(item) for item in plan.selected_object_names)
        expected_materials = tuple(required_name(item) for item in plan.expected_material_names)
        expected_images = tuple(required_name(item) for item in plan.expected_image_names)
        expected_actions = tuple(required_name(item) for item in plan.expected_action_names)
        if (
            plan.product_case not in {STATIC, RIGGED, RIGGED_ANIMATED}
            or not selected_names
            or len(set(object_names)) != len(object_names)
            or len(set(selected_names)) != len(selected_names)
            or not set(selected_names).issubset(object_names)
            or not expected_materials
            or not expected_images
            or any(
                len(values) != len(set(values))
                for values in (expected_materials, expected_images, expected_actions)
            )
            or any(ord(character) < 32 for character in plan.copyright_notice)
        ):
            raise ValueError
        selected_set = set(selected_names)
        selected = tuple(item for item in all_objects if item.name in selected_set)
        types = tuple(required_name(item.type).upper() for item in selected)
        if any(item not in {"MESH", "ARMATURE"} for item in types) or "MESH" not in types:
            raise ValueError
        armature_count = sum(item == "ARMATURE" for item in types)
        actual_materials = _attached_material_names(selected)
        actual_images = tuple(sorted(required_name(item.name) for item in images))
        inspected_images = tuple(sorted(item.name for item in texture_report.images))
        connected_images = tuple(
            sorted(
                item.name for item in texture_report.images if item.material_connection_count > 0
            )
        )
        actual_actions = tuple(sorted(required_name(item.name) for item in actions))
        if (
            actual_materials != tuple(sorted(expected_materials))
            or actual_images != tuple(sorted(expected_images))
            or inspected_images != actual_images
            or connected_images != actual_images
            or actual_actions != tuple(sorted(expected_actions))
        ):
            raise ValueError
        if plan.product_case == STATIC and (armature_count or actual_actions):
            raise ValueError
        if plan.product_case == RIGGED and (armature_count != 1 or actual_actions):
            raise ValueError
        if plan.product_case == RIGGED_ANIMATED and (armature_count != 1 or not actual_actions):
            raise ValueError
    except Exception:
        return NormalizedGlbExportResult(
            None,
            (
                _finding(
                    "BLENDER_GLB_EXPORT_PLAN_INVALID",
                    "The GLB path, case contents, materials, connected textures, or Actions are inconsistent.",
                    "Repair the normalized export plan and retry before invoking Blender export.",
                ),
            ),
        )

    try:
        with SelectionSafeExport(all_objects, view_layer, selected_names):
            operator_result = set(
                export_operator(
                    **_export_options(output_path, plan, plan.product_case == RIGGED_ANIMATED)
                )
            )
        if operator_result != {"FINISHED"} or not _valid_glb_container(output_path):
            raise RuntimeError
    except Exception:
        cleanup_failed = False
        try:
            if output_path.exists() or output_path.is_symlink():
                output_path.unlink()
        except Exception:
            cleanup_failed = True
        findings = [
            _finding(
                "BLENDER_GLB_EXPORT_FAILED",
                "Blender did not produce a complete glTF 2.0 Binary artifact.",
                "Review the retained worker log and retry from a fresh normalized workspace.",
            )
        ]
        if cleanup_failed:
            findings.append(
                _finding(
                    "BLENDER_GLB_EXPORT_CLEANUP_FAILED",
                    "The incomplete GLB artifact could not be removed safely.",
                    "Quarantine the build workspace before retrying.",
                )
            )
        return NormalizedGlbExportResult(None, tuple(findings))

    return NormalizedGlbExportResult(
        NormalizedGlbExportReport(
            plan.output_filename,
            output_path.stat().st_size,
            plan.product_case,
            tuple(sorted(selected_names)),
            tuple(sorted(expected_materials)),
            tuple(sorted(expected_images)),
            tuple(sorted(expected_actions)),
        ),
        (),
    )
