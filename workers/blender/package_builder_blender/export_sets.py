"""Deterministic scene cleanup and selection-safe Blender export sets."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from .inspection_common import InspectionFinding, required_name


@dataclass(frozen=True, slots=True)
class ExportSetPlan:
    """Manifest-owned object identities and optional explicitly retained helpers."""

    intended_object_names: tuple[str, ...]
    explicitly_retained_names: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class ExcludedSceneObject:
    """One object excluded from a normalized export and the stable reason."""

    name: str
    reason: str


@dataclass(frozen=True, slots=True)
class ExportSetReport:
    """Exact selected and excluded object inventory produced before cleanup."""

    selected_object_names: tuple[str, ...]
    retained_helper_names: tuple[str, ...]
    excluded_objects: tuple[ExcludedSceneObject, ...]
    removed_object_count: int
    orphaned_data_block_count: int


@dataclass(frozen=True, slots=True)
class ExportSetResult:
    """Non-throwing expected result for export-set planning and cleanup."""

    report: ExportSetReport | None
    findings: tuple[InspectionFinding, ...]

    @property
    def succeeded(self) -> bool:
        """Return whether an exact export set was produced."""

        return self.report is not None and not self.findings


def _finding(code: str, explanation: str, action: str) -> InspectionFinding:
    return InspectionFinding(code, explanation, action, "blender-export-set-builder")


def _hidden(scene_object: Any) -> bool:
    hide_get = getattr(scene_object, "hide_get", None)
    hidden = bool(hide_get()) if callable(hide_get) else False
    return (
        hidden
        or bool(getattr(scene_object, "hide_viewport", False))
        or bool(getattr(scene_object, "hide_render", False))
    )


def _exclusion_reason(scene_object: Any) -> str:
    object_type = required_name(scene_object.type).upper()
    if object_type == "CAMERA":
        return "camera"
    if object_type == "LIGHT":
        return "light"
    if _hidden(scene_object):
        return "hidden_or_backup"
    if object_type not in {"MESH", "ARMATURE"}:
        return "helper"
    return "not_intended"


def prepare_export_set(data: Any, plan: ExportSetPlan) -> ExportSetResult:
    """Remove non-retained objects and local orphans without consulting UI selection."""

    try:
        objects = tuple(data.objects)
        names = tuple(required_name(item.name) for item in objects)
        intended = tuple(required_name(item) for item in plan.intended_object_names)
        retained = tuple(required_name(item) for item in plan.explicitly_retained_names)
        if (
            len(set(names)) != len(names)
            or not intended
            or len(set(intended)) != len(intended)
            or len(set(retained)) != len(retained)
            or set(intended).intersection(retained)
            or not set(intended + retained).issubset(names)
        ):
            raise ValueError
        by_name = dict(zip(names, objects, strict=True))
        if any(
            required_name(by_name[name].type).upper() not in {"MESH", "ARMATURE"}
            for name in intended
        ):
            raise ValueError
    except Exception:
        return ExportSetResult(
            None,
            (
                _finding(
                    "BLENDER_EXPORT_SET_INVALID",
                    "The manifest export set is empty, ambiguous, missing, or selects a non-mesh/non-rig object as intended content.",
                    "Repair the manifest object identities and retry from a fresh working copy.",
                ),
            ),
        )

    selected_names = tuple(sorted((*intended, *retained)))
    selected = set(selected_names)
    excluded = tuple(
        sorted(
            (
                ExcludedSceneObject(name, _exclusion_reason(scene_object))
                for name, scene_object in zip(names, objects, strict=True)
                if name not in selected
            ),
            key=lambda item: item.name,
        )
    )
    removed_objects = tuple(by_name[item.name] for item in excluded)
    try:
        if removed_objects:
            data.batch_remove(ids=removed_objects)
        orphan_count = data.orphans_purge(
            do_local_ids=True,
            do_linked_ids=False,
            do_recursive=True,
        )
        if type(orphan_count) is not int or orphan_count < 0:
            raise ValueError
        remaining = {required_name(item.name) for item in tuple(data.objects)}
        if remaining != selected:
            raise ValueError
    except Exception:
        return ExportSetResult(
            None,
            (
                _finding(
                    "BLENDER_EXPORT_SET_CLEANUP_FAILED",
                    "Blender did not produce the exact manifest-owned working export set.",
                    "Discard the disposable workspace and retry from the retained source snapshot.",
                ),
            ),
        )

    return ExportSetResult(
        ExportSetReport(
            selected_names,
            tuple(sorted(retained)),
            excluded,
            len(removed_objects),
            orphan_count,
        ),
        (),
    )


class SelectionSafeExport:
    """Temporarily select exactly one export set and restore all UI state afterward."""

    def __init__(self, objects: tuple[Any, ...], view_layer: Any, selected_names: tuple[str, ...]):
        self._objects = objects
        self._view_layer = view_layer
        self._selected_names = selected_names
        self._snapshots: tuple[tuple[Any, bool, bool, bool, bool], ...] = ()
        self._active: Any = None

    def __enter__(self) -> tuple[Any, ...]:
        names = tuple(required_name(item.name) for item in self._objects)
        selected_names = tuple(required_name(item) for item in self._selected_names)
        if (
            len(set(names)) != len(names)
            or not selected_names
            or len(set(selected_names)) != len(selected_names)
            or not set(selected_names).issubset(names)
        ):
            raise ValueError("Export selection must contain each planned object exactly once.")
        self._active = getattr(self._view_layer.objects, "active", None)
        snapshots: list[tuple[Any, bool, bool, bool, bool]] = []
        selected_set = set(selected_names)
        selected_objects = tuple(item for item in self._objects if item.name in selected_set)
        for item in self._objects:
            hide_get = getattr(item, "hide_get", None)
            hidden = bool(hide_get()) if callable(hide_get) else False
            snapshots.append(
                (
                    item,
                    bool(item.select_get()),
                    hidden,
                    bool(getattr(item, "hide_viewport", False)),
                    bool(getattr(item, "hide_render", False)),
                )
            )
            item.select_set(False)
        self._snapshots = tuple(snapshots)
        for item in selected_objects:
            hide_set = getattr(item, "hide_set", None)
            if callable(hide_set):
                hide_set(False)
            if hasattr(item, "hide_viewport"):
                item.hide_viewport = False
            if hasattr(item, "hide_render"):
                item.hide_render = False
            item.select_set(True)
        ordered = tuple(
            sorted(
                selected_objects,
                key=lambda item: (
                    0 if required_name(item.type).upper() == "ARMATURE" else 1,
                    item.name,
                ),
            )
        )
        self._view_layer.objects.active = ordered[0]
        return ordered

    def __exit__(self, _exception_type: object, _exception: object, _traceback: object) -> bool:
        restore_error: Exception | None = None
        for item, selected, hidden, hide_viewport, hide_render in self._snapshots:
            try:
                item.select_set(selected)
                hide_set = getattr(item, "hide_set", None)
                if callable(hide_set):
                    hide_set(hidden)
                if hasattr(item, "hide_viewport"):
                    item.hide_viewport = hide_viewport
                if hasattr(item, "hide_render"):
                    item.hide_render = hide_render
            except Exception as exception:  # pragma: no cover - Blender integration guard
                restore_error = exception
        try:
            self._view_layer.objects.active = self._active
        except Exception as exception:  # pragma: no cover - Blender integration guard
            restore_error = exception
        if restore_error is not None and _exception_type is None:
            raise RuntimeError("Blender selection state could not be restored.") from restore_error
        return False
