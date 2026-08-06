# PB-0410 Blender Naming Normalization Evidence

## Status

- Canonical branch: `feat/PB-0410-blender-naming-normalization`
- Publication branch: `feat/PB-0409-case-inference`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-05

The shared publication branch was the exact user-approved PB-0409/PB-0410/PB-0411 exception and
created no precedent. Commit `773f776ad3ad9c62d33ecf075715232b2fbaf000` was merged through
[pull request #60](https://github.com/avivperets26/3DModels-Package-Builder/pull/60) as
`8b19fdca393e803ca476669945d5761f7d424cd4`; required
[main workflow run 31086450759](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31086450759)
succeeded, and the user explicitly confirmed completion on 2026-08-06.

## Implemented Boundary

`package_builder_blender.naming_normalization.normalize_blender_names(...)` requires a complete
manifest plan for every supplied object, mesh, armature, material, image, and Action data block,
plus collision-checked exported filenames. Asset IDs, folder names, safe ID names, portable output
extensions, and project naming prefixes are validated before any mutation.

Renaming uses deterministic temporary names, then exact final names. Source identities,
assignments, target names, exported roles, and case-insensitive exported filenames must be unique.
Missing, extra, unsafe, noncanonical, or colliding assignments fail before mutation. If Blender
changes a requested name (for example by appending `.001`) or a setter fails, the complete
transaction is rolled back; an incomplete rollback is disclosed as requiring workspace discard.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_NAMING_PLAN_INVALID` | The manifest plan is incomplete, unsafe, noncanonical, or colliding. |
| `BLENDER_NAMING_APPLY_FAILED` | Blender did not apply the exact plan and rollback was attempted. |

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Every required data-block category follows the plan | all-category success assertion |
| Exported assets follow the plan | FBX/GLB role and filename assertions |
| No collisions | source, desired, role, case-insensitive output, and two-phase swap tests |
| No partial mutation | preflight failure and forced Blender suffix rollback tests |

## Validation

- Focused PB-0410 tests: 8 passed, 0 failed.
- Focused executable-line trace: 96%; Python branch coverage is not currently measured or claimed.
- Combined focused tests: 29 passed; all Blender worker tests: 117 passed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; final Core CI: all nine stages passed in 2 minutes 35.460 seconds.
- Ruff lint/formatting, locked restore, .NET formatting, PowerShell parsing, documentation/task graph,
  secret/prohibited-content, vulnerability, and `git diff --check` checks passed.

## Evidence Limits and Publication

Plain-Python Blender ID doubles cover deterministic transaction behavior; actual contained Blender
execution remains unavailable and is not claimed. This task has no UI or rendered output, so
manual visual testing is not applicable. All publication, required `main` CI, explicit
confirmation, and rollover gates passed as recorded above.
