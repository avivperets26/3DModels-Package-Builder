# PB-0619 Unity Interactive Dark-Studio Preview Evidence

## Lifecycle

- Task: PB-0619 — Implement Unity interactive dark-studio preview shell.
- Canonical branch: `feat/PB-0619-unity-interactive-preview`.
- Publication branch: `test/PB-0617-unity-clean-reimport` under the approved combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-08.

## Runtime Interaction and Studio Contract

The product-local `PackageBuilderPreviewController` exposes contract version `1` and keeps the
product hierarchy immutable while controlling only the camera, studio backdrop, and key light.
Runtime controls are:

- Hold left mouse and drag for bounded yaw/pitch orbit.
- Use the mouse wheel or `+`/`-`/Page Up/Page Down for distance zoom.
- Use arrow keys for keyboard orbit.
- Press `R` to restore automatic bounds framing.
- Press `L` to restore the approved key-light direction.
- Press `H` or use the overlay button to hide/show all controls for clean capture.
- Use labelled yaw/pitch sliders and a reset button to adjust the key light.

The generic overview template generates a deterministic radial near-black image with a subtly
brighter centre, imports it without compression or mipmaps, and displays it on a camera-facing
URP/Unlit quad. Product composition copies both the background material and texture beneath the
publisher product root so the exported scene has no template dependency or horizon line.

## Reuse Boundary

PB-0619 implements Unity runtime conformance and a versioned contract surface. PB-0913 remains the
owner of the engine-neutral serialized preview contract, cross-engine test vectors, and Unreal
conformance. This preserves the dependency and avoids copying Unity-specific behavior into future
engine integrations.

## Validation Checkpoint

- Dependency-free interactive-preview policy: passed as part of 26/26 Unity policy checks.
- Integration assertions cover bounded pitch, key-light adjustment/reset, overlay visibility,
  background references, and unchanged product transforms.
- A first real Play-mode run exposed an incompatible legacy-input read with the pinned Input System
  package. The runtime controller was corrected to process mouse and keyboard input through the
  active IMGUI event, without adding an editor-only or input-package dependency.
- Corrected real Unity Editor, Play mode, exact-package clean reimport, actual camera render, and
  project reopen: passed. The retained Play log contains
  `PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_PASS` and no Play-mode error.
- Automated tests verify that orbit, zoom, reset, and light controls do not mutate product position,
  rotation, or scale; clean reimport reports one renderer, one material, five textures, and zero
  findings.
- Manual visual review is now available in the retained project
  `artifacts/u/c22a6b24/p`; user visual acceptance remains pending.
- Repository baseline: 32/32 passed. Full nine-stage Core CI passed with all 2,282 tests passing.

## Remaining Gates

Manual visual acceptance, user-controlled publication, successful required `main` CI, explicit
confirmation, and next-task rollover remain.
