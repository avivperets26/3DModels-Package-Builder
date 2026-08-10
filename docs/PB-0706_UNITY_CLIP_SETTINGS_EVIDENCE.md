# PB-0706 Unity Clip Settings Evidence

## Lifecycle

- Task: PB-0706 — Implement clip loop, compression, and root-motion policy.
- Canonical and publication branch: `feat/PB-0706-unity-clip-settings`.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-09.
- Publication is combined with PB-0707 and PB-0708 under the explicitly approved exception in the
  implementation backlog. The exception creates no precedent.

## Implemented Policy

`UnityAnimationClipImporter` now requires explicit task-owned policies instead of relying on Unity
defaults:

- each clip declares whether it loops;
- each clip declares whether root motion is preserved or baked into the pose; and
- each import request selects Off, Keyframe Reduction, Keyframe Reduction and Compression, or
  Optimal compression.

One-shot clips set `loopTime=false` and `loopPose=false`. Declared loops set both values true.
Bake-into-pose locks root rotation and position while preservation retains the original root
curves. Unspecified or unknown policies fail with structured findings. Importer state is restored
and partial outputs are deleted when a transaction fails.

## Real-Engine Validation

The retained Unity project is `artifacts/u/d6c8fd35/p`. It contains:

- one-shot clip `Assets/PBAnimationTests/Animations/A_AnimatedProp_Attack.anim` from frames 1–11;
- looping clip `Assets/PBAnimationTests/Animations/A_AnimatedProp_BendLoop.anim` from frames 1–21;
- both clips at 30 FPS; and
- importer compression set to Optimal.

The one-shot clip was verified not to loop and to bake root motion into the pose. The looping clip
was verified to loop and preserve root motion. Invalid unspecified compression and root-motion
requests were rejected without modifying the source.

## Validation Checkpoint

- Unity product-policy validator: 35/35 passed.
- Real contained-Blender fixture generation: passed.
- Real Unity Edit-mode integration, populated-project reopen, Play mode, exact-package validation,
  and clean package reimport: passed.
- The strict skin validator passed both before and after clip import, proving the import transaction
  preserved bind data and skinned-renderer integrity.
- Unity project-template validator: 8/8 passed.
- Unity worker-package validator: 9/9 passed.
- Repository baseline with required tracked files: 32/32 passed.
- Full Core CI: all 9 stages passed; 2,282/2,282 tests passed; Release build completed with zero
  warnings and zero errors.
- `git diff --check`: passed.

## Publication Completion

- Final combined task commit: `1e135e426389460162f938c5e8c7ea0e76aa1156`.
- Published with PB-0707/PB-0708 through
  [PR #78](https://github.com/avivperets26/3DModels-Package-Builder/pull/78) and merged as
  `997e19f2e1837157e0f977b598f941a50c46ea06`.
- Required exact-merge [main workflow run 31335169320](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31335169320)
  succeeded, and the user explicitly confirmed the push, merge, and continuation on 2026-08-09.
- No CI, quality, completion, or workflow exception was used.
