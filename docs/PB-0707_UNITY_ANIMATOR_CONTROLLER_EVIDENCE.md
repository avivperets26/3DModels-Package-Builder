# PB-0707 Unity Animator Controller Evidence

## Lifecycle

- Task: PB-0707 — Implement Animator Controller generator.
- Canonical branch: `feat/PB-0707-unity-animator-controller`.
- Publication branch: `feat/PB-0706-unity-clip-settings` under the approved combined exception.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-09.

## Implemented Controller Contract

`UnityAnimatorControllerGenerator` creates deterministic `AC_<AssetId>.controller` assets from a
validated clip set. Clip identifiers are sorted ordinally and become stable state names. The
manifest-selected default clip becomes the default state. Each clip receives a stable
`Replay_<ClipId>` trigger and an Any State transition that can replay that motion.

The generator rejects unsafe paths, invalid or duplicate identifiers, duplicate clip references,
missing motions, an unknown default clip, and occupied output paths. The saved controller is
reloaded and verified for exact states, default state, replay parameters, transitions, and motion
references. Any failure removes the partial controller and returns a structured finding.

## Real-Engine Validation

The retained project contains
`Assets/PBAnimationTests/Controllers/AC_AnimatedProp.controller`. Unity verified:

- exactly two states, `Attack` and `BendLoop`;
- `Attack` as the declared default;
- one `Replay_Attack` and one `Replay_BendLoop` trigger;
- one replay transition per clip; and
- no state with a missing motion.

Repeated generation into the occupied path failed closed instead of overwriting the controller.

## Validation Checkpoint

- Unity product-policy validator: 35/35 passed.
- Real controller creation and post-save verification: passed.
- Real Unity populated-project reopen, Play mode, exact-package validation, and clean package
  reimport: passed.
- Unity project-template validator: 8/8 passed.
- Unity worker-package validator: 9/9 passed.
- Repository baseline with required tracked files: 32/32 passed.
- Full Core CI: all 9 stages passed; 2,282/2,282 tests passed; Release build completed with zero
  warnings and zero errors.
- `git diff --check`: passed.

## Remaining Gates

PB-0707 remains PROCESS until the combined change is committed, pushed, merged into and pushed on
`main`, required `main` CI succeeds, the user explicitly confirms completion, and rollover records
its independent Completion Log entry.
