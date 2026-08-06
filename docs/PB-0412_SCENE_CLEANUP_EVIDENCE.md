# PB-0412 Helper Cleanup and Selection-Safe Export Sets Evidence

## Status

- Canonical branch: `feat/PB-0412-scene-cleanup`
- Publication branch: `feat/PB-0412-scene-cleanup`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

PB-0412 shares one explicitly user-approved publication cycle with PB-0413 through PB-0415. Each
task retains its independent API, tests, evidence, lifecycle, canonical branch, and eventual
Completion Log row. The exception applies only to this cycle and creates no precedent.

## Implemented Boundary

`package_builder_blender.export_sets.prepare_export_set(...)` requires a complete manifest-owned
set of intended mesh/armature objects plus optional explicitly retained helpers. It classifies and
batch-removes cameras, lights, hidden backups, helpers, and non-intended meshes from the disposable
working scene. It then recursively purges only local orphaned data while retaining linked data.

`SelectionSafeExport` snapshots selection, object hidden state, and the active object; deselects
all scene objects; unhides and selects exactly the requested export set; prefers an armature as the
deterministic active object; and restores the complete snapshot on success or exception.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Exclude camera/light/helper/hidden backup/non-intended objects | exact reason and retained-set assertions |
| Explicitly retained helpers survive | hidden-helper retention test |
| Intended mesh/rig survives | remaining-object identity assertion |
| Purge only local recursive orphans | exact `orphans_purge` argument assertion |
| Export does not depend on prior selection | exact in-context selection/active-object assertion |
| UI state always restores | success and exporter-exception assertions |

Focused PB-0412 tests: 5 passed, 0 failed.

## Combined Validation

- New focused tests: 20 passed; all Blender worker tests: 137 passed.
- Built-in focused executable-line trace: all four new production modules 100%; Python branch
  coverage is not currently measured or claimed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; Core CI: all nine stages passed in 3 minutes 2.498 seconds.
- Ruff lint/format, .NET format, locked restore, task graph, local links, security scans,
  vulnerability audit, and `git diff --check` passed.

## Evidence Limit

The [Blender 5 Object API](https://docs.blender.org/api/5.0/bpy.types.Object.html) defines the
selection, hidden-state, and visibility interfaces used by this boundary. Tests use strict
plain-Python Blender doubles because no approved contained Blender runtime currently exists under
the project root. Real Blender integration and visual output are not claimed.

## Publication Evidence

- Shared task commit: `ba9e617b76b076e58aaa4e2279432b784ec373d7`.
- Pull request: [#61](https://github.com/avivperets26/3DModels-Package-Builder/pull/61).
- Merge commit: `e75f9d41f6091d47b915e1da3be3564f2895839c`.
- Required [main workflow run 31095384477](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31095384477) succeeded.
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-06. No CI or quality exception was used.
