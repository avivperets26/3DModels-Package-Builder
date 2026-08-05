# PB-0402 Blender Scene Utilities Evidence

## Status

- Task: PB-0402 — Implement Blender scene reset and context-safe utilities
- Branch: `feat/PB-0402-blender-scene-utils`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

The utilities and deterministic boundary tests are implemented and locally validated. This task
remains active until the user-controlled commit, push, merge, required `main` CI, and explicit
completion-confirmation gates succeed.

## Implemented Boundary

- `reset_scene(bpy.data)` snapshots and removes every object through
  `BlendData.batch_remove(ids=...)`, then recursively purges local orphaned data while retaining
  linked orphaned data.
- `TemporaryDataBlocks(bpy.data)` owns exact operation-created data-block identities, rejects null
  registrations, deduplicates repeated identities, and disposes them as one batch in reverse
  registration order.
- Context-manager exit performs cleanup after successful or failed processing. A Blender cleanup
  failure propagates and retains ownership so the caller can retry; successful close is idempotent.
- The boundary receives `bpy.data`, not the full `bpy` module, and never reads or writes selection,
  active object, editor area, or interaction mode.
- Callers must not retain or access removed Blender references.

This follows Blender's official API guidance that operators consume UI context and can fail when
the active area, selection, object, or mode is unsuitable. Direct data access avoids that implicit
state. Relevant primary references are the official
[operator gotchas](https://docs.blender.org/api/current/info_gotchas_operators.html),
[BlendData API](https://docs.blender.org/api/current/bpy.types.BlendData.html), and
[data access overview](https://docs.blender.org/api/current/bpy.data.html).

## Acceptance Mapping

| Acceptance condition | Automated evidence | Result |
|---|---|---|
| Scene reset is independent of UI selection and mode | `BlenderSceneUtilityTests.test_reset_removes_all_objects_and_local_orphans_without_ui_state`; forbidden UI sentinels fail on access | Pass |
| Empty scenes reset deterministically | `BlenderSceneUtilityTests.test_empty_reset_still_purges_orphans_without_empty_batch_call` | Pass |
| Invalid Blender cleanup results fail closed | `BlenderSceneUtilityTests.test_invalid_orphan_count_fails_closed` | Pass |
| Temporary data is disposed deterministically and once | `BlenderSceneUtilityTests.test_temporary_data_is_deduplicated_and_removed_in_reverse_order` | Pass |
| Temporary data is disposed after processing failure | `BlenderSceneUtilityTests.test_context_manager_disposes_temporary_data_after_body_failure` | Pass |
| Cleanup failure is visible and retryable | `BlenderSceneUtilityTests.test_failed_cleanup_keeps_ownership_retryable` | Pass |
| Invalid temporary ownership is rejected | `BlenderSceneUtilityTests.test_none_cannot_be_registered` | Pass |
| PB-0401 worker behavior does not regress | `BlenderWorkerEntrypointTests` | Pass (10 tests) |

## Current Validation

- `python -m unittest discover -s tests/blender -p "test_*.py" -v`: 17 passed.
- Repository-local Ruff 0.15.22 lint: passed.
- Repository-local Ruff 0.15.22 format check: passed; 8 files already formatted.
- `scripts/Invoke-CoreCi.ps1`: all 9 stages passed in 00:03:51.8362189, including repository
  baseline 29/29, warning-free Release build, .NET formatting, and 2,067/2,067 .NET tests.
- .NET 10.0.302 Debug solution build: passed with 0 warnings and 0 errors.
- Python standard-library trace with missing-line reporting: `scene_utils.py` 57/57 executable
  lines; complete worker package 305/332 executable lines (91.87%). This is line-execution evidence
  only and does not measure branches.
- `git diff --check`: passed.

## Evidence Limits

- No contained Blender executable is present, so real `bpy` execution has not occurred and Blender
  integration is not claimed.
- Tests use a strict deterministic `bpy.data` double whose UI sentinels fail if accessed. This is
  unit/contract evidence, not a substitute for later contained-engine integration tests.
- Python branch coverage is not measured by the standard-library trace, so no branch-coverage or
  full approved coverage-gate claim is made.

## Remaining Publication Gates

- User reviews and commits this branch.
- User pushes the task branch and merges it into `main`.
- Required `main` CI succeeds for the exact merge commit.
- User explicitly confirms completion. PB-0402 remains `[ ]` / 🟡 **PROCESS** until its rollover at
  the beginning of PB-0403.
