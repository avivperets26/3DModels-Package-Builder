# PB-0403 Blender FBX Import Adapter Evidence

## Status

- Task: PB-0403 — Implement FBX import adapter
- Branch: `feat/PB-0403-blender-fbx-import`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-05

PB-0403 is implemented, locally validated, published, and confirmed complete. PB-0404 synchronized
its lifecycle under the permanent one-merge rollover workflow.

## Final Publication Evidence

- Task commit: `b262b3b99bebddc1a8e0410b96dde0ee095bcff2`.
- Pull request: [#56](https://github.com/avivperets26/3DModels-Package-Builder/pull/56).
- Merge commit: `2235ee5f0849fda54763e8ec421afd66a3f8e305`.
- Required successful `main` workflow:
  [31033724099](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31033724099).
- User completion confirmation: 2026-08-05.
- Exception used: none.

## Implemented Boundary

- `FbxImportSettings` records explicit forward/up axes and global scale.
- `import_fbx` accepts only one canonical, non-link, non-empty `.fbx` regular file beneath the
  canonical input root.
- PB-0402 resets the scene before import and after a failed or rejected partial import.
- The adapter calls Blender's standard `bpy.ops.import_scene.fbx` boundary with deterministic
  explicit settings rather than depending on UI selection, active object, editor area, or mode.
- Recursive image search and untrusted custom properties are disabled. Experimental baked-space
  transform is disabled because Blender documents it as broken with armatures/animations.
- Successful reports retain the safe source filename, exact axes/scale, and object, mesh, and
  armature counts. Detailed inspection remains owned by PB-0405 through PB-0408.
- Expected failures map to stable sanitized findings and never contain raw exception text or a
  physical source path.

The operator surface was verified against Blender's official
[FBX add-on source](https://github.com/blender/blender-addons/blob/main/io_scene_fbx/__init__.py)
and [FBX manual](https://docs.blender.org/manual/en/latest/addons/import_export/scene_fbx.html).
The manual notes that the standard importer supports binary FBX 7.1 or newer and describes current
armature/animation limitations; Package Builder does not hide those limits.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_FBX_SOURCE_INVALID` | Source/root containment, type, link, extension, existence, or size failed. |
| `BLENDER_FBX_IMPORT_SETTINGS_INVALID` | Axes or scale cannot be passed safely. |
| `BLENDER_SCENE_RESET_FAILED` | A clean pre-import scene could not be established. |
| `BLENDER_FBX_IMPORT_FAILED` | Blender raised while importing. |
| `BLENDER_FBX_IMPORT_REJECTED` | Blender did not return exactly `FINISHED`. |
| `BLENDER_FBX_IMPORT_EMPTY` | Blender completed without creating a scene object. |
| `BLENDER_FBX_IMPORT_RESULT_INVALID` | Imported scene data could not be read safely. |
| `BLENDER_FBX_IMPORT_CLEANUP_FAILED` | Partial imported state could not be removed. |

All findings are blocking error findings from `blender-fbx-importer` with actionable sanitized
guidance.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Static FBX boundary imports and records axes/units | `test_static_fixture_import_records_exact_axis_and_unit_settings` |
| Skinned FBX boundary imports mesh and armature data | `test_skinned_fixture_import_records_mesh_and_armature_counts` |
| Unsafe sources fail before side effects | `test_invalid_sources_fail_before_scene_or_importer_side_effects`, link test |
| Invalid axes/scales fail before side effects | `test_invalid_axis_and_scale_settings_fail_before_side_effects` |
| Blender failures are sanitized and partial state is removed | exception, rejection, cleanup, and unreadable-result tests |
| Empty imports and reset failures block release | empty-import and scene-reset tests |

## Current Validation

- Focused PB-0403 plus prior Blender worker tests: 28 passed.
- Focused `fbx_import.py` trace coverage: 186/186 executable lines, 100% line execution.
- Debug solution build: 16 projects, 0 warnings, 0 errors.
- Release solution build: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 3 minutes 52.951 seconds.
- Repository-local Ruff lint and formatting: passed.
- Locked restore, .NET formatting, PowerShell parsing, task/dependency/lifecycle checks,
  documentation links, secret/prohibited-content checks, and `git diff --check`: passed.

## Evidence Limits

- No contained Blender executable is present, so real `bpy` execution and real static/skinned FBX
  parsing have not occurred and are not claimed.
- The focused tests use strict deterministic importer/data doubles. They verify arguments,
  containment, result mapping, scene cleanup, and static/skinned object outcomes, but are not a
  substitute for the later contained-engine fixture suite.
- Python branch coverage is not currently measured, so no branch-coverage claim is made.

## Completion

PB-0403 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Its disclosed evidence limits remain accurate and no unsupported real-Blender claim is made.
