# PB-0708 Unity Animated Prefab Evidence

## Lifecycle

- Task: PB-0708 — Implement animated prefab flow.
- Canonical branch: `feat/PB-0708-unity-animated-prefab`.
- Publication branch: `feat/PB-0706-unity-clip-settings` under the approved combined exception.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-09.

## Implemented Case-3 Contract

`UnityAnimatedPrefabGenerator` consumes one imported rig, one verified Animator Controller, and an
existing product Prefabs folder. It uses the same `UnityPrefabHierarchyUtility` as the static and
rig-only generators to create a reset `P_<AssetId>` root with exactly one reset `P_Model` child.
This shared helper keeps hierarchy naming, transform reset, and source-reference safety rules in a
single reusable boundary.

Before saving, the generator runs the canonical PB-0703 skin validator, removes legacy Animation
components, creates exactly one Animator, assigns the requested controller, and applies the
explicit root-motion choice. After reloading the prefab, it verifies the reset hierarchy, preserved
skinned renderers and shared meshes, valid skin data, exact controller, one Animator, and no
missing scripts. Failure removes partial output and returns a structured finding.

## Real-Engine Validation

The retained project contains
`Assets/PBAnimationTests/Prefabs/P_AnimatedProp.prefab`. Unity verified the canonical hierarchy,
all skinned-renderer references, one Animator using `AC_AnimatedProp`, and enabled root motion.

The first real-engine attempt exposed a fixture defect: the animated test fixture optimized its
bone hierarchy while exposing only the root, so Unity no longer retained the complete deform-bone
array required by the strict bind-pose validator. The production validator was not weakened. The
fixture now retains the complete hierarchy and validates skin data both before and after clip
import. The corrected run passed prefab creation, reopen, Play mode, exact-package validation, and
clean reimport.

## Validation Checkpoint

- Unity product-policy validator: 35/35 passed.
- Real animated-prefab creation and post-save validation: passed.
- Real populated-project reopen and Play mode: passed with no product errors.
- Exact package validation and clean package reimport: passed.
- Corrected retained run: `artifacts/u/d6c8fd35`.
- Unity project-template validator: 8/8 passed.
- Unity worker-package validator: 9/9 passed.
- Repository baseline with required tracked files: 32/32 passed.
- Full Core CI: all 9 stages passed; 2,282/2,282 tests passed; Release build completed with zero
  warnings and zero errors.
- `git diff --check`: passed.

## Publication Completion

- Final combined task commit: `1e135e426389460162f938c5e8c7ea0e76aa1156`.
- Published on the PB-0706 branch with PB-0706/PB-0707 through
  [PR #78](https://github.com/avivperets26/3DModels-Package-Builder/pull/78) and merged as
  `997e19f2e1837157e0f977b598f941a50c46ea06`.
- Required exact-merge [main workflow run 31335169320](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31335169320)
  succeeded, and the user explicitly confirmed the push, merge, and continuation on 2026-08-09.
- No CI, quality, completion, or workflow exception was used.
