# PB-0615 Exact Unity Package Export Evidence

## Lifecycle

- Task: PB-0615 — Implement exact Unity package export.
- Canonical and publication branch: `feat/PB-0615-unitypackage-export`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-08.
- Combined publication: PB-0616 under the explicitly approved, no-precedent branch-topology
  exception recorded in the implementation backlog.

## Implemented Export Boundary

`UnityPackageExporter` creates an exact, ordinally ordered plan from one validated product root.
Only canonical product folders and their contents may enter the plan: `Animations`, `Controllers`,
`Documentation`, `Materials`, `Meshes`, `Prefabs`, `Scenes`, `Scripts`, `Source`, and `Textures`.
Case-insensitive collisions, unsafe paths, `_Template`, unexpected top-level content, dependencies
outside the product root, missing assets, and existing output files fail closed.

Export calls `AssetDatabase.ExportPackage` with the explicit planned inventory and
`ExportPackageOptions.Default`. It never enables recursive or implicit dependency expansion. The
output must be a new `.unitypackage` path inside the isolated project clone but outside Unity's
`Assets`, `Packages`, `ProjectSettings`, and `Library` trees.

The overview composer now copies its background material into the product `Materials` folder, and
the real fixture compiles its material using product-local textures. Therefore the scene, prefab,
material, texture, mesh, source, documentation, script, and metadata dependency closure is wholly
inside the product root.

## Real Unity and Archive Validation

- Unity: 6000.3.10f1.
- Exact export completed successfully for `Assets/PBModelTests`.
- Every archive `pathname` matched the export plan exactly.
- Every archive record contained `pathname` and `asset.meta`; every non-folder record also
  contained an `asset` payload.
- No `_Template`, reusable overview template, unrelated test fixture, or worker-package asset was
  present.
- Existing output collision: rejected without overwriting.
- Incorrectly placed product file: rejected before export.
- Real Play mode cycle and populated-project reopen: passed.
- Manual review of the first retained run exposed a diagonal-camera framing edge case before
  publication. The controller now fits all eight product-bound corners in camera space, and the
  real Unity regression assertion requires every corner to remain inside the viewport.
- Corrected retained ignored evidence: `artifacts/u/a7c04c07`.
- Corrected retained manual project: `artifacts/u/a7c04c07/p`.
- User manual Play-mode verification on 2026-08-08 confirmed the corrected full-product framing
  with visible viewport margin and no lower-edge clipping.

## Focused Validation

- Unity product policy validator: 23/23 passed.
- Unity worker package validator: 9/9 passed.
- Real Unity exact-package integration: passed.
- Repository baseline: 32/32 passed.
- Full Core CI: all 9 stages passed in 8 minutes 47.2 seconds.
- Release build: 18 projects, 0 warnings, 0 errors.
- Complete automated .NET suite: 2,282 passed, 0 failed, 0 skipped.
- Locked restore, .NET formatting, Ruff lint/formatting, security, history, and diff checks: passed.
- Initial archive-verifier run correctly exposed that Unity folder records omit `asset` payloads;
  the verifier was corrected to distinguish planned folders from files while retaining strict
  payload checks for every file record. The complete rerun passed.
- Generated evidence remains ignored beneath the repository `artifacts` root.

## Remaining Gates

User-controlled commit, branch push, merge into and push of `main`, successful required `main` CI,
explicit user completion confirmation, and next-task rollover remain.
