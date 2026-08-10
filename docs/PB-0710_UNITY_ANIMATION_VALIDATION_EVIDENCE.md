# PB-0710 Unity Animation Validation Evidence

## Lifecycle

- Task: PB-0710 - Implement animation movement and duration validation.
- Canonical branch: `test/PB-0710-unity-animation-validation`.
- Publication branch: `feat/PB-0709-unity-animation-preview` under the approved combined exception.
- Status: `[ ]` / PROCESS; implemented and validated locally, with publication gates remaining.
- Started: 2026-08-10.

## Implemented Validation

`UnityAnimationMotionValidator` validates extracted AnimationClip assets after real FBX import. Its
typed expectations require the exact clip inventory, names, duration, frames per second, and loop
intent. Each clip must contain a Tip-bone local-rotation binding.

For movement evidence, the validator instantiates the generated animated prefab only in memory,
samples a clip at zero and midpoint with `AnimationMode`, compares the moving bone rotation, and
bakes the SkinnedMeshRenderer to prove vertex deformation. It also requires positive renderer-local
bounds on all three axes, preventing a zero-thickness, back-face-culled fixture from satisfying the
movement check while remaining invisible. It then drives the imported one-shot through
`PackageBuilderAnimationTransport` beyond its duration and requires the Completed state at the exact
clamped end time. The disposable instance is destroyed and no asset is saved.

## Fixture and Results

The existing repository-owned Blender generator creates `AnimatedProp.fbx` beneath the ignored
integration project. The weighted mesh is a closed thin box with non-zero volume. Its sampled Bend
action covers frames 1 through 21 at 30 FPS. Unity extracts:

- `A_AnimatedProp_Attack`, non-looping;
- `A_AnimatedProp_BendLoop`, looping.

Both clips have the expected 0.6667-second duration, 30 FPS, Tip rotation bindings, observable Tip
bone motion, and observable skinned-renderer deformation. The non-looping Attack clip completes
without wrapping.

## Traceability and Validation

| Acceptance criterion | Automated evidence |
|---|---|
| Count and names | Exact sorted two-clip inventory comparison. |
| Duration and FPS | Per-clip tolerance of 0.001 seconds/FPS against the import plan. |
| Bindings | Required non-empty Tip local-rotation curve binding. |
| Renderer and bone movement | Midpoint AnimationMode sample, quaternion delta, and baked-vertex delta. |
| Renderable geometry | Every local-bounds axis must exceed 0.00001 Unity units. |
| Expected non-looping behavior | Imported loop flag plus transport completion at the clamped duration. |

Retained local run `artifacts/u/426dcfb8` passed the complete Unity 6000.3.10f1 integration in
464.6 seconds. The
focused Unity policy validator passed 38/38 and explicitly verifies that metadata, binding,
sampling, renderable volume, deformation, and completion assertions remain present.

## Reuse and Duplication Audit

- Reused PB-0705 clip extraction, PB-0706 loop policy, PB-0707 controller, PB-0708 animated prefab,
  PB-0709 transport, and the existing Blender fixture generator.
- Sampling is centralized in one Editor validator; no motion rule is copied into the importer,
  prefab generator, runtime controller, or PowerShell harness.
- Intentional duplication: none.

## Work Remaining

The task remains PROCESS until the combined publication branch completes the required Git,
exact-merge `main` CI, and explicit user-confirmation gates.
