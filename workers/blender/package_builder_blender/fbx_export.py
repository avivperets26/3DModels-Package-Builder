"""Selection-safe normalized FBX export for static, rigged, and animated products."""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .case_inference import RIGGED, RIGGED_ANIMATED, STATIC
from .export_sets import SelectionSafeExport
from .inspection_common import InspectionFinding, required_name


@dataclass(frozen=True, slots=True)
class NormalizedFbxExportPlan:
    """Complete expected contents and output identity for one normalized FBX."""

    asset_id: str
    product_case: str
    output_root: Path
    output_filename: str
    selected_object_names: tuple[str, ...]
    expected_material_names: tuple[str, ...]
    expected_action_names: tuple[str, ...] = ()
    axis_forward: str = "-Z"
    axis_up: str = "Y"


@dataclass(frozen=True, slots=True)
class NormalizedFbxExportReport:
    """Verified normalized FBX artifact and its exact logical contents."""

    output_filename: str
    byte_count: int
    product_case: str
    object_names: tuple[str, ...]
    material_names: tuple[str, ...]
    action_names: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class NormalizedFbxExportResult:
    """Non-throwing expected result for normalized FBX export."""

    report: NormalizedFbxExportReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether Blender produced the exact requested artifact."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-fbx-exporter")


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


def _safe_output(plan: NormalizedFbxExportPlan) -> tuple[Path, Path]:
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
        or not filename.lower().endswith(".fbx")
        or not filename.startswith(required_name(plan.asset_id))
    ):
        raise ValueError
    resolved_root = root.resolve(strict=True)
    target = root / filename
    resolved_target = target.resolve(strict=False)
    if (
        resolved_target.parent != resolved_root
        or target.exists()
        or target.is_symlink()
        or resolved_target.is_symlink()
    ):
        raise ValueError
    return resolved_root, resolved_target


def _export_options(path: Path, plan: NormalizedFbxExportPlan, animated: bool) -> dict[str, Any]:
    """Return the reviewed non-experimental Blender 5 FBX policy."""

    return {
        "filepath": str(path),
        "check_existing": False,
        "use_selection": True,
        "use_visible": False,
        "use_active_collection": False,
        "collection": "",
        "global_scale": 1.0,
        "apply_unit_scale": True,
        "apply_scale_options": "FBX_SCALE_NONE",
        "use_space_transform": True,
        "bake_space_transform": False,
        "object_types": {"ARMATURE", "MESH"},
        "use_mesh_modifiers": True,
        "use_mesh_modifiers_render": True,
        "mesh_smooth_type": "FACE",
        "colors_type": "SRGB",
        "prioritize_active_color": False,
        "use_subsurf": False,
        "use_mesh_edges": False,
        "use_tspace": True,
        "use_triangles": False,
        "use_custom_props": False,
        "add_leaf_bones": False,
        "primary_bone_axis": "Y",
        "secondary_bone_axis": "X",
        "use_armature_deform_only": True,
        "armature_nodetype": "NULL",
        "bake_anim": animated,
        "bake_anim_use_all_bones": True,
        "bake_anim_use_nla_strips": False,
        "bake_anim_use_all_actions": animated,
        "bake_anim_force_startend_keying": True,
        "bake_anim_step": 1.0,
        "bake_anim_simplify_factor": 0.0,
        "path_mode": "RELATIVE",
        "embed_textures": False,
        "batch_mode": "OFF",
        "use_batch_own_dir": False,
        "use_metadata": False,
        "axis_forward": plan.axis_forward,
        "axis_up": plan.axis_up,
    }


def export_normalized_fbx(
    all_objects: tuple[Any, ...],
    actions: tuple[Any, ...],
    view_layer: Any,
    plan: NormalizedFbxExportPlan,
    export_operator: Callable[..., Any],
) -> NormalizedFbxExportResult:
    """Export exact manifest content and restore the user's Blender selection state."""

    try:
        _root, output_path = _safe_output(plan)
        object_names = tuple(required_name(item.name) for item in all_objects)
        selected_names = tuple(required_name(item) for item in plan.selected_object_names)
        expected_materials = tuple(required_name(item) for item in plan.expected_material_names)
        expected_actions = tuple(required_name(item) for item in plan.expected_action_names)
        if (
            plan.product_case not in {STATIC, RIGGED, RIGGED_ANIMATED}
            or not selected_names
            or len(set(object_names)) != len(object_names)
            or len(set(selected_names)) != len(selected_names)
            or not set(selected_names).issubset(object_names)
            or not expected_materials
            or len(set(expected_materials)) != len(expected_materials)
            or len(set(expected_actions)) != len(expected_actions)
        ):
            raise ValueError
        selected = tuple(item for item in all_objects if item.name in set(selected_names))
        types = tuple(required_name(item.type).upper() for item in selected)
        if any(item not in {"MESH", "ARMATURE"} for item in types) or "MESH" not in types:
            raise ValueError
        armature_count = sum(item == "ARMATURE" for item in types)
        actual_materials = _attached_material_names(selected)
        actual_actions = tuple(sorted(required_name(item.name) for item in actions))
        if actual_materials != tuple(sorted(expected_materials)) or actual_actions != tuple(
            sorted(expected_actions)
        ):
            raise ValueError
        if plan.product_case == STATIC and (armature_count or actual_actions):
            raise ValueError
        if plan.product_case == RIGGED and (armature_count != 1 or actual_actions):
            raise ValueError
        if plan.product_case == RIGGED_ANIMATED and (armature_count != 1 or not actual_actions):
            raise ValueError
        if plan.axis_forward not in {"X", "Y", "Z", "-X", "-Y", "-Z"}:
            raise ValueError
        if plan.axis_up not in {"X", "Y", "Z", "-X", "-Y", "-Z"}:
            raise ValueError
        if plan.axis_forward.lstrip("-") == plan.axis_up.lstrip("-"):
            raise ValueError
    except Exception:
        return NormalizedFbxExportResult(
            None,
            (
                _finding(
                    "BLENDER_FBX_EXPORT_PLAN_INVALID",
                    "The FBX plan, path, case contents, materials, actions, or axes are inconsistent.",
                    "Repair the normalized export plan and retry before invoking Blender export.",
                ),
            ),
        )

    try:
        with SelectionSafeExport(all_objects, view_layer, selected_names):
            result = set(
                export_operator(
                    **_export_options(output_path, plan, plan.product_case == RIGGED_ANIMATED)
                )
            )
        if result != {"FINISHED"}:
            raise RuntimeError
        if not output_path.is_file() or output_path.is_symlink() or output_path.stat().st_size <= 0:
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
                "BLENDER_FBX_EXPORT_FAILED",
                "Blender did not produce a complete normalized FBX artifact.",
                "Review the retained worker log and retry from a fresh normalized workspace.",
            )
        ]
        if cleanup_failed:
            findings.append(
                _finding(
                    "BLENDER_FBX_EXPORT_CLEANUP_FAILED",
                    "The incomplete FBX artifact could not be removed safely.",
                    "Quarantine the build workspace before retrying.",
                )
            )
        return NormalizedFbxExportResult(None, tuple(findings))

    return NormalizedFbxExportResult(
        NormalizedFbxExportReport(
            plan.output_filename,
            output_path.stat().st_size,
            plan.product_case,
            tuple(sorted(selected_names)),
            tuple(sorted(expected_materials)),
            tuple(sorted(expected_actions)),
        ),
        (),
    )
