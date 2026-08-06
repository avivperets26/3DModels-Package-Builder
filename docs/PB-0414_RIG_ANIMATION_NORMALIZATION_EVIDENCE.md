# PB-0414 Rig, Action, and Baking Normalization Evidence

## Status

- Canonical branch: `feat/PB-0414-rig-animation-normalization`
- Publication branch: `feat/PB-0412-scene-cleanup`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

The shared publication branch is the exact user-approved PB-0412/PB-0413/PB-0414/PB-0415
exception. Task identity and acceptance evidence remain independent; no precedent is created.

## Implemented Boundary

`package_builder_blender.rig_animation_normalization.normalize_rig_animation(...)` validates one
armature, a non-empty deform-bone set, exact complete Action ownership, canonical skeleton/Action
names, non-motionless source clips, inclusive Blender bake ranges, and sample steps from 1 through
120. Requested names are verified exactly so Blender collision suffixes fail closed.

The bake path invokes Blender 5 NLA bake with selected deform bones, visual keying, the current
Action, POSE data, all reviewed transform/B-Bone/property channels, no parent/constraint clearing,
and no curve cleanup. Every output Action is reinspected for exact inclusive start/end keys,
step-conforming samples, and retained transform motion. Scene range, active Action, and pose-bone
selection restore unconditionally. A failed bake requires disposal of the working copy because
baked curve mutations are not claimed to be reversible.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Requested skeleton/Action naming | exact name assertions and invalid-prefix rejection |
| Deterministic baking/sampling | exact operator dictionary and 1/3/5 sample assertion |
| Deform-only policy | selected/exported deform-bone assertions |
| Preserve motion and boundaries | post-bake inspection and inclusive boundary assertions |
| Preserve surrounding state | success/failure range, active Action, and selection assertions |

Focused PB-0414 tests: 5 passed, 0 failed.

## Combined Validation and Limit

- New focused tests: 20 passed; all Blender worker tests: 137 passed.
- Built-in focused executable-line trace: all four new production modules 100%; Python branch
  coverage is not currently measured or claimed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; Core CI: all nine stages passed in 3 minutes 2.498 seconds.
- Ruff lint/format, .NET format, locked restore, task graph, local links, security scans,
  vulnerability audit, and `git diff --check` passed.

The operator contract is aligned with the
[Blender 5 NLA bake API](https://docs.blender.org/api/5.0/bpy.ops.nla.html#bpy.ops.nla.bake).
Strict doubles validate policy; an approved contained Blender runtime is still required before
real evaluated-pose baking or visual playback is claimed.

## Publication Evidence

- Shared task commit: `ba9e617b76b076e58aaa4e2279432b784ec373d7`.
- Pull request: [#61](https://github.com/avivperets26/3DModels-Package-Builder/pull/61).
- Merge commit: `e75f9d41f6091d47b915e1da3be3564f2895839c`.
- Required [main workflow run 31095384477](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31095384477) succeeded.
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-06. No CI or quality exception was used.
