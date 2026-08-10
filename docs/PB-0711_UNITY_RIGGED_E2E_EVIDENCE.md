# PB-0711 Unity Rigged-No-Animation End-to-End Evidence

## Lifecycle

- Task: PB-0711 - Complete Unity rigged-no-animation fixture.
- Canonical branch: `test/PB-0711-unity-rigged-e2e`.
- Publication branch: `feat/PB-0709-unity-animation-preview` under the approved combined exception.
- Status: `[ ]` / PROCESS; implemented and validated locally, with publication gates remaining.
- Started: 2026-08-10.

## Redistribution-Safe Fixture

The task reuses `tests/blender/engine/pb0701_generate_rig_fbx.py` in its no-action mode. The
repository-owned script creates a two-bone deform rig and one weighted mesh, then exports a real FBX
under the ignored integration directory. No third-party model or redistribution licence is needed,
and no generated FBX is tracked. The mesh is a closed thin box rather than a one-sided plane, so it
remains visible from the default overview camera while preserving a minimal rig fixture.

The rigged product package contains the source FBX, one `P_RiggedProp.prefab`, skeleton metadata,
and approved empty base folders. Its exact manifest contains no Animations or Controllers folder
and no animation/controller asset.

## Clean-Reimport Contract

`Invoke-CleanUnityPackageValidation` creates a fresh approved Unity project for each package,
removes the worker and template preview runtime before customer-package import, imports and compiles
the package, and only then adds validation tooling. PB-0711 uses a second isolated clone rather than
reusing the static-product clean project.

The structured `rigged-no-animation` validator requires:

- exactly one imported FBX using Generic rig mode with animation import disabled;
- exactly one valid SkinnedMeshRenderer and two unique deform bones;
- zero Animator and legacy Animation components;
- zero AnimationClip and Animator Controller assets;
- no Animations or Controllers output folders;
- skeleton metadata with `hasAnimationClips: false`;
- no missing scripts or validation findings.

## Traceability and Validation

Retained run `artifacts/u/426dcfb8` produced and clean-imported
`RiggedProp.unitypackage` in `artifacts/u/426dcfb8/g`. The structured result reports:

| Metric | Result |
|---|---:|
| Passed | true |
| Skinned renderers | 1 |
| Unique bones | 2 |
| Animation clips | 0 |
| Animators | 0 |
| Findings | 0 |

The focused Unity policy validator passed 38/38, the project-template validator passed 8/8, and the
full real-engine integration, Play mode, exact package, clean imports, and populated-project reopen
all passed.

## Reuse and Duplication Audit

- Reused the PB-0701 Blender rig generator, PB-0703 skin/skeleton validator, PB-0704 rigged prefab,
  PB-0617 structured clean-reimport validator, and one extracted clean-project orchestration helper.
- Static and rigged clean imports now share the same process isolation, import-log, required-asset,
  worker-injection, and structured-result behavior.
- Intentional duplication: none.

## Work Remaining

The task remains PROCESS until the combined publication branch completes the required Git,
exact-merge `main` CI, and explicit user-confirmation gates.
