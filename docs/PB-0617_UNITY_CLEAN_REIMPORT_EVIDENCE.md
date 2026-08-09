# PB-0617 Clean Unity Package Reimport Evidence

## Lifecycle

- Task: PB-0617 — Implement clean Unity package reimport.
- Canonical and publication branch: `test/PB-0617-unity-clean-reimport`.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-08.
- Combined publication: PB-0618 and PB-0619 under the explicitly approved branch-topology
  exception in the implementation backlog.

## Implemented Boundary

The existing exact-package integration now creates a second short-path Unity project from only the
pinned `6000.3` template. It removes the reusable preview source, verifies that no product root is
present, and imports the exported `.unitypackage` with Unity's command-line package importer.

`UnityCleanReimportIntegration` writes a schema-versioned JSON result atomically. It opens the
imported overview scene and validates the prefab, controller, camera, key light, backdrop, missing
scripts, enabled renderers, product-local materials and textures, camera framing, frustum
intersection, and an actual camera render. Findings use stable `UNITY_REIMPORT_*` codes and logical
asset references rather than local absolute paths.

## Validation Checkpoint

- Dependency-free Unity product policy validation: 26/26 passed.
- PowerShell entrypoint parsing: passed.
- Corrected real-Unity run root: `artifacts/u/8e2af0a6`.
- Exact package export, fresh-template import, scene open, prefab render, material and texture
  reference validation, Play-mode execution, project reopen, and structured-result validation:
  passed.
- Structured clean-reimport result: schema version 1, `passed: true`, one renderer, one material,
  five textures, and zero findings.
- The first real Play-mode attempt exposed use of the legacy `UnityEngine.Input` API while the
  pinned template uses the Input System package. The preview controller now consumes pointer,
  wheel, and keyboard events through `Event.current`; the corrected run contains the
  `PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_PASS` marker and no Play-mode error.
- Generated projects, packages, logs, and JSON results remain beneath ignored repository
  `artifacts` storage.
- Repository baseline: 32/32 passed. Full nine-stage Core CI passed; the Release build completed
  without warnings or errors and all 2,282 tests passed with none failed or skipped.

## Publication Completion

- Final task commit: `6d82a7d55be66f8db0d63af36070b88ebe616a3b`.
- Merged through [PR #74](https://github.com/avivperets26/3DModels-Package-Builder/pull/74)
  as `73261cfdb578de968d8f72aa553742deab75eff8`.
- Required exact-merge [main workflow run 31308970351](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31308970351)
  succeeded.
- The user explicitly confirmed completion on 2026-08-09. No CI or quality exception was used.
