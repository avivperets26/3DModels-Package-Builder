# PB-0613 Unity Preview Controller Evidence

## Lifecycle

- Task: PB-0613 — Refactor preview controller to frame camera without scaling assets.
- Canonical branch: `feat/PB-0613-unity-preview-controller`.
- Publication branch: `feat/PB-0612-unity-overview-scene` under the approved combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.

## Implemented Controller

`PackageBuilderPreviewController` is product-local runtime code, not an Editor-worker dependency.
The integration moves its source, assembly definition, and metadata into the generated product's
`Scripts` folder before Unity imports the job clone, preserving stable script identity for export.

The controller:

- Computes one world-space bound from all enabled renderers beneath `PreviewTarget`.
- Auto-frames using vertical and horizontal camera field of view, aspect ratio, depth, and padding.
- Orbits the camera around the current bounds centre.
- Zooms by clamped camera distance relative to bounds radius.
- Updates camera look direction and clipping planes where appropriate.
- Never assigns product or `PreviewTarget` position, rotation, or scale.
- Exposes Inspector context-menu actions for manual auto-frame, orbit, and zoom checks without an
  input-system dependency.

Edit-mode integration and the real Play mode smoke test snapshot every product transform before
auto-frame/orbit/zoom and require every local position, rotation, and scale to remain identical while
the camera position changes.

## Validation

- Dependency-free preview-controller policy checks: passed.
- Real Unity edit-mode auto-frame/orbit/zoom: passed.
- Real Unity Play mode auto-frame/orbit/zoom: passed without logged Error, Exception, or Assert.
- Product-transform preservation: passed for the complete instantiated fixture hierarchy.
- User manual inspection on 2026-08-08 confirmed auto-frame, orbit, and zoom move the camera while
  the product hierarchy and transforms remain unchanged, with no red Play mode Console errors.
- Full Core CI: all 9 stages passed; 18 projects built with 0 warnings and 0 errors.
- Complete automated test suite: 2,282 passed, 0 failed, 0 skipped.
- Repository baseline: 32/32 passed.
- Formatting, repository safety, and `git diff --check`: passed.
- Retained evidence: `artifacts/u/1c6667a0`.

## Remaining Gates

The combined PB-0612/PB-0613/PB-0614 publication and completion gates remain.
