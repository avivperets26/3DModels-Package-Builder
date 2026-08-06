# PB-0415 Normalized FBX Export Evidence

## Status

- Canonical branch: `feat/PB-0415-normalized-fbx-export`
- Publication branch: `feat/PB-0412-scene-cleanup`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-06

The shared publication branch is the exact user-approved PB-0412/PB-0413/PB-0414/PB-0415
exception. Each task remains independently reviewable and the exception creates no precedent.

## Implemented Boundary

`package_builder_blender.fbx_export.export_normalized_fbx(...)` validates a canonical non-link
output root and new direct-child `.fbx` path, exact selected objects, attached materials, complete
Action inventory, and the static/rigged/rigged-animated case shape before the exporter can run.
Existing artifacts are never overwritten.

The reviewed Blender 5 policy exports selected meshes/armatures only; applies unit and axis-space
metadata without the experimental armature-breaking baked-space transform; applies modifiers;
exports tangents and face smoothing; excludes leaf bones, custom properties, metadata, NLA strips,
and texture embedding; uses deform bones; preserves start/end animation keys; disables curve
simplification; and keeps normalized texture references relative. PB-0412's selection guard
restores UI state on every path. Cancelled, exceptional, missing, linked, or empty artifacts fail
closed and trigger exact partial-file removal.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Static FBX selection/material policy | static fixture/operator test |
| Rigged FBX mesh/armature policy | rigged fixture/operator test |
| Animated FBX Action/bake policy | animated fixture/operator test |
| Exact selected assets and state restoration | in-operator and post-export assertions |
| Safe output and partial cleanup | overwrite refusal and cancelled-export cleanup tests |

Focused PB-0415 tests: 5 passed, 0 failed.

## Combined Validation and Limit

- New focused tests: 20 passed; all Blender worker tests: 137 passed.
- Built-in focused executable-line trace: all four new production modules 100%; Python branch
  coverage is not currently measured or claimed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; Core CI: all nine stages passed in 3 minutes 2.498 seconds.
- Ruff lint/format, .NET format, locked restore, task graph, local links, security scans,
  vulnerability audit, and `git diff --check` passed.

The exact options match the
[Blender 5 FBX export API](https://docs.blender.org/api/5.0/bpy.ops.export_scene.html#bpy.ops.export_scene.fbx),
including its warning that baked space transform is experimental and known to be broken with
armatures/animations. Fixture tests use a strict exporter double that writes contained non-empty
artifacts; no real FBX binary validity or visual reimport is claimed until an approved contained
Blender runtime exists and PB-0417 performs clean reimport validation.
