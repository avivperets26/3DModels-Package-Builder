# PB-0709 Unity Animation Preview Evidence

## Lifecycle

- Task: PB-0709 - Implement Unity animation preview controls.
- Branch: `feat/PB-0709-unity-animation-preview`.
- Status: `[ ]` / PROCESS; implemented and validated locally, with publication gates remaining.
- Started: 2026-08-10.

## Implemented Contract

`PackageBuilderAnimationTransport` is a product-local Unity adapter for the PB-0913 version-one
transport contract. It discovers unique generated-controller clips in stable order, selects the
first clip at time zero, and exposes select, play, pause/resume, replay, scrub, preview-loop,
current-time, and duration state. It manually samples the assigned Animator, so a preview loop
override remains serialized scene state and never changes an AnimationClip, ModelImporter,
Animator Controller, or source FBX.

`PackageBuilderPreviewController` delegates transport state to that component. Its overlay provides
clip selection, play/pause, replay, timeline, loop, and time/duration feedback. Pointer controls have
a visible Tab focus cycle; Enter/Space activates buttons and loop, while arrows operate the focused
clip selector or timeline. Focused animation controls consume their events before camera orbit.

The overview composer configures zero or one product Animator, rejects multiple Animators, persists
the transport in the saved scene, and verifies it again after reopening. Static products retain an
unavailable transport without displaying irrelevant animation controls.

## Traceability and Validation

| Acceptance criterion | Automated evidence |
|---|---|
| List and select clips | `TestOverviewTemplateControllerAndComposition` verifies the exact two-name order and selection reset. |
| Play, pause/resume, replay | Real Unity assertions exercise every transition. |
| Scrub, loop, time/duration | Real Unity assertions verify end clamping, completion, loop override, and exposed feedback state. |
| Pointer and keyboard operation | `Test-UnityProductPolicies.ps1` verifies the concrete IMGUI controls, focus cycle, activation keys, and arrow handling. |
| Do not modify packaged animation | Dependency hashes for both source clips are identical before and after composition and transport operations. |

Retained local run `artifacts/u/426dcfb8` passed the real Unity 6000.3.10f1 Editor suite, scene
save/reopen, Play mode, exact package checks, clean import, and populated-project reopen. The focused
Unity policy validator passed 38/38 and the template validator passed 8/8.

The first manual visual inspection exposed a zero-thickness test mesh whose back face was culled by
the Game camera. The repository-owned fixture now generates a closed thin box, and PB-0710 rejects
an animated renderer with a degenerate local-bounds axis. The user then confirmed that the corrected
object is visible and animating in the Game view of the retained `426dcfb8` project. The same manual
run confirmed both clip selections, play, pause/resume, replay, timeline scrubbing, loop toggling,
time feedback, Tab focus, Enter/Space activation, and arrow-key operation.

## Reuse and Duplication Audit

- Reused PB-0913 action names, defaults, state semantics, and preview-only loop invariant.
- Reused the existing overview controller, composer, generated Animator Controller, and animated
  fixture instead of introducing a model or a second UI framework.
- Kept animation state in a cohesive component rather than expanding the camera controller into a
  combined state machine.
- Intentional duplication: Unity maps the shared state transitions locally because exported Unity
  customer assemblies cannot reference the repository's .NET 10 Domain assembly. Unity Preview
  Engineering owns conformance through shared defaults, policy checks, and real-engine assertions.

## Work Remaining

The task remains PROCESS until the user commits and pushes the combined branch, merges it into
`main`, required exact-merge `main` CI succeeds, and the user explicitly confirms completion.
