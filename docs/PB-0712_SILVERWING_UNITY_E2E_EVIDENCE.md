# PB-0712 Silverwing Unity End-to-End Evidence

## Lifecycle

- Task: PB-0712 - Complete Silverwing Talonbow animated fixture.
- Branch: `test/PB-0712-silverwing-unity-e2e`.
- Status: `[ ]` / PROCESS; implemented and validated locally, with publication gates remaining.
- Started: 2026-08-11.

## Private Fixture Boundary

The user confirmed permission to use Silverwing and required the source to remain private. All
model, texture, reference-image, and Blender files stay beneath
`runtime-data/fixtures/private/Silverwing_Talonbow`, which is excluded by the repository-level
`/runtime-data/` ignore rule. The integration harness rejects sources outside the private fixture
root, ignored-policy failures, and any reparse point in the source tree.

`tests/blender/engine/pb0712_prepare_silverwing.py` opens the approved `.blend` with Blender
automatic Python execution disabled. In memory, it samples the separate bow and string Actions,
consolidates the two authoring armatures into one 38-bone hierarchy with `Bone_000` as its only
root, bakes one `Bow_Shot` Action, and exports only the bow body, visible string, and consolidated
armature. It never saves the source `.blend`. The harness compares SHA-256 hashes for the consumed
`.blend` and five texture maps before and after validation; all were identical in the passing run.
No private or generated binary asset is tracked.

## Unity Product Contract

The isolated Unity integration reuses the maintained production adapters for:

- Generic rig import and skin/skeleton validation;
- texture-role import, metallic-smoothness packing, and URP/Lit material compilation;
- exact non-looping clip extraction and one-state Animator Controller generation;
- animated-prefab creation with both skinned renderers preserved;
- overview composition and the shared animation-transport controls;
- dependency-closed package validation/export and clean-project reimport.

The synchronized-motion validator samples from the child object that owns the Animator, uses a
scale-relative deformation threshold, and requires both
`P_SilverwingTalonbow_Body` and `P_SilverwingTalonbow_String` to deform during the same sampled
pose. Sampling uses disposable prefab instances and never writes to the source FBX, extracted clip,
controller, prefab, or private input.

## Traceability and Validation

Retained run `artifacts/u/38e40ad3` passed Blender 5.0.0 normalization, Unity 6000.3.10f1 product
generation, exact package inspection, and isolated clean reimport. The clean structured result
reports:

| Acceptance evidence | Result |
|---|---:|
| Validation findings | 0 |
| Generic, non-Humanoid skeleton bones | 38 |
| Skinned renderers | 2 |
| Animation clips | 1 |
| Clip name | `A_SilverwingTalonbow_Bow_Shot` |
| Clip loop state | false |
| Animators / controller states | 1 / 1 |
| Product materials / referenced textures | 1 / 4 |
| Body and string synchronized deformation | true |
| Private consumed inputs unchanged | true |

The generated overview scene auto-framed and rendered the textured product, initialized the shared
transport with only `Bow_Shot`, and reported source-derived loop state as disabled. The exported
package inventory matched its manifest exactly and contained only `Assets/PBSilverwingTests`.

On 2026-08-11, the user opened the retained generated project and explicitly confirmed that the
fixture works perfectly, the animation and preview behave correctly, and Unity reports no errors.
This records the local visual/manual acceptance separately from the remaining Git, merge, and
required `main` CI gates.

Focused static validation passed 39/39 Unity product policy checks. The real-engine run compiled
without C# errors or package-caused warnings and retained:

- generated project: `artifacts/u/38e40ad3/p`;
- clean reimport project: `artifacts/u/38e40ad3/s`;
- exact package: `artifacts/u/38e40ad3/p/PackageBuilderExports/SilverwingTalonbow.unitypackage`;
- structured clean result: `artifacts/u/38e40ad3/unity-silverwing-clean-result.json`.

## Reuse and Duplication Audit

- Reused the normalized Blender FBX exporter and existing Generic-rig, skin, texture, material,
  clip, controller, animated-prefab, overview, package, and clean-reimport implementations.
- Extracted clean Unity package import/validation process orchestration into
  `scripts/UnityCleanReimport.Common.ps1` for both the maintained product integration and
  Silverwing fixture.
- Extended the shared package path validator for the already-approved `Animations/*.anim` and
  `Controllers/*.controller` layout rather than adding fixture-specific exceptions.
- Intentional duplication: the private fixture's exact object, action, and topology assertions are
  fixture-specific acceptance data; they are not copied product-domain rules. Owner: PB-0712 Unity
  Fixture Engineering.

## Work Remaining

PB-0712 remains PROCESS until the user commits and pushes the task branch, merges and pushes
`main`, required exact-merge `main` CI succeeds, and the user explicitly confirms completion.
