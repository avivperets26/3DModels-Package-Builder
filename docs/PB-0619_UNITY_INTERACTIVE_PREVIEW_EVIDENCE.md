# PB-0619 Unity Interactive Dark-Studio Preview Evidence

## Lifecycle

- Task: PB-0619 — Implement Unity interactive dark-studio preview shell.
- Canonical branch: `feat/PB-0619-unity-interactive-preview`.
- Publication branch: `test/PB-0617-unity-clean-reimport` under the approved combined cycle.
- Status: `[x]` / 🟢 **DONE**.
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
- Press `H` or use the overlay button to hide the main controls; a compact labelled `Show Controls`
  button remains visible so pointer users can always restore the panel.
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
- A user follow-up identified that the original hidden state could only be recovered with the `H`
  key. The PB-0701/PB-0702 publication cycle now adds a compact pointer-accessible restore control,
  excludes its rectangle from orbit capture, and retains the keyboard shortcut. This is a narrowly
  scoped UX enhancement; PB-0619's published completion evidence and contract ownership remain
  unchanged.
- The compact restore-control follow-up passed focused policy checks, the real Unity Editor/Play
  integration beneath `artifacts/u/dee856f4`, clean package reimport, and the full Core CI suite.
- Manual visual review is now available in the retained project
  `artifacts/u/c22a6b24/p`; the user visually accepted the error-free Play-mode result on
  2026-08-09.
- Repository baseline: 32/32 passed. Full nine-stage Core CI passed with all 2,282 tests passing.

## Publication Completion

- Final task commit: `6d82a7d55be66f8db0d63af36070b88ebe616a3b`.
- Published on the PB-0617 branch and merged through
  [PR #74](https://github.com/avivperets26/3DModels-Package-Builder/pull/74) as
  `73261cfdb578de968d8f72aa553742deab75eff8`.
- Required exact-merge [main workflow run 31308970351](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31308970351)
  succeeded.
- The user explicitly confirmed completion and visually accepted the error-free Play-mode result
  on 2026-08-09. No CI or quality exception was used.
