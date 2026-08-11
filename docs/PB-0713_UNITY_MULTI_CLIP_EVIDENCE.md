# PB-0713 Unity Multi-Clip Evidence

## Lifecycle

- Task: PB-0713 - Add multi-clip animated fixture.
- Branch: `test/PB-0713-unity-multi-clip`.
- Status: `[ ]` / PROCESS; implemented and validated locally, with publication gates remaining.
- Started: 2026-08-11.

## Fixture and Product Contract

The harness generates a redistribution-safe two-bone skinned FBX with the maintained Blender
fixture generator. No private or third-party model is required or tracked. The Unity integration
discovers the one 30 FPS source take and derives two exact product clips from it:

| Clip | Loop setting | Controller role |
|---|---:|---|
| `A_MultiClipProp_Attack` | false | Default state |
| `A_MultiClipProp_BendLoop` | true | Secondary state |

Both clips pass through the existing clip importer, mixed-loop policy, deterministic Animator
Controller generator, animated-prefab generator, motion validator, package exporter, and shared
clean-reimport helper. The fixture therefore tests composition of production behavior rather than
introducing a second implementation of clip or controller rules.

## Automated Evidence

Retained run `artifacts/u/8396c00f` passed Blender 5.0.0 generation, Unity 6000.3.10f1 product
generation, exact package inspection, and isolated clean reimport. The clean structured result
reports:

| Acceptance evidence | Result |
|---|---:|
| Validation findings | 0 |
| Generic skin renderers / bones | 1 / 2 |
| Animation clips | 2 |
| Looping / non-looping clips | 1 / 1 |
| Animator Controller states | 2 |
| Animators | 1 |
| Bone and renderer motion verified | true |
| Non-looping completion verified | true |

The exact exported package contains only `Assets/PBMultiClipTests`. A second fresh Unity project
reimports the package and verifies the two clip names, 30 FPS metadata, mixed loop intent, exact
two-state controller with `Attack` as the default motion, one animated prefab, and sampled bone and
renderer motion. Focused static validation passed 40/40 Unity product policy checks.

Repository regression validation also passed:

- Unity template inventory: 8/8;
- Unity worker inventory: 9/9;
- repository baseline: 32/32;
- Markdown lint: 142 files, 0 errors;
- Blender/Python unit tests: 156/156;
- maintained sequential .NET test harness: 2,320/2,320.

Two unrelated external-process timing cases each failed once during earlier heavily concurrent
test attempts. Both passed when rerun in isolation, the complete Infrastructure project passed
647/647, and the final maintained sequential run passed all 2,320 tests. No product source was
changed in response to those transient scheduling results.

On 2026-08-11, the user manually inspected the retained generated project and supplied Unity
screenshots confirming that `Attack` is 0.667 seconds at 30 FPS with Loop Time disabled,
`BendLoop` is 0.667 seconds at 30 FPS with Loop Time and Loop Pose enabled, the controller contains
exactly both states with `Attack` as the default state, and the prefab has one Animator referencing
`AC_MultiClipProp`. Unity's standalone extracted-clip preview reported that no preview model was
assigned; this is expected for the `.anim` asset and does not indicate an import or validation
error.

Retained evidence:

- generated project: `artifacts/u/8396c00f/p`;
- clean reimport project: `artifacts/u/8396c00f/m`;
- exact package: `artifacts/u/8396c00f/p/PackageBuilderExports/MultiClipProp.unitypackage`;
- structured clean result: `artifacts/u/8396c00f/unity-multi-clip-clean-result.json`.

## Reuse and Duplication Audit

- Reused the repository-owned procedural FBX generator and the existing Generic-rig, skin, clip,
  loop-policy, controller, animated-prefab, motion-validation, exact-package, and clean-reimport
  implementations.
- Extended the shared clean-reimport result with controller-state, loop-inventory, and motion
  fields that are also useful to later animated-product fixtures.
- Intentional duplication: exact names, counts, and loop expectations appear in both the build and
  clean-reimport assertions because they are fixture-specific acceptance data on opposite sides of
  the package boundary. Owner: PB-0713 Unity Fixture Engineering.

## Work Remaining

PB-0713 remains PROCESS until the user commits and pushes the task branch, merges and pushes
`main`, required exact-merge `main` CI succeeds, and the user explicitly confirms completion.
