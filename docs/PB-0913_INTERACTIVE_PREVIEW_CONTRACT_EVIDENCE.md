# PB-0913 Interactive Preview Contract Evidence

## Lifecycle

- Task: PB-0913 — Define shared interactive preview experience contract.
- Canonical branch: `feat/PB-0913-interactive-preview-contract`.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-10.
- Completed: 2026-08-10.

## Implemented Contract

Contract version 1 is divided by responsibility:

- `PackageBuilder.Domain.Preview` owns bounded camera and key-light state, overlay recovery,
  deterministic item selection, animation transport, normalized actions/bindings, and accessibility
  labels/focus semantics.
- PB-0906 `PreviewBackground` and `PreviewLighting` remain the sole source of approved dark-studio
  presentation values.
- `AnimationDefinition` remains the sole source of clip duration and source loop metadata. Preview
  loop changes are transient and cannot mutate it.
- `PackageBuilder.Contracts.Preview` owns strict deterministic JSON, the embedded Draft 2020-12
  schema, input safeguards, and Domain revalidation.
- `tests/fixtures/preview/preview-experience-v1-vectors.json` is the portable adapter-conformance
  fixture.

Camera state changes yaw, pitch, and distance multiplier only. It exposes no product transform or
scale. Item state supports empty, all-items, or exactly-one-item visibility and defines wrapping.
Animation state supports unavailable, stopped, playing, paused, and completed modes plus selection,
replay, bounded scrub, current time/duration, and preview-only loop override. Hiding the overlay
moves focus to the labelled `show-controls` recovery control.

## Requirement-to-Test Mapping

| PB-0913 criterion | Automated evidence |
|---|---|
| Versioned engine-neutral typed contract and approved presentation tokens | `DefaultsReuseApprovedPresentationAndCoverEverySharedAction`, `PreviewContractRemainsEngineRendererFilesystemAndUiIndependent` |
| Bounded orbit, camera-distance zoom, reset, and light direction/reset | `CameraAndLightStatesClampWrapAndResetWithoutProductTransforms`, shared camera vectors |
| Pointer and keyboard bindings | `DefaultsReuseApprovedPresentationAndCoverEverySharedAction` |
| Overlay visibility and accessible recovery | `AccessibilityHasUniquePredictableFocusAndHiddenRestoreSemantics`, shared overlay vector |
| Previous/Next/direct/all item states and edge cases | `ItemSelectionHandlesEmptySingleDirectAllAndWrapStates`, shared item vectors |
| Animation list/select/play/pause/replay/scrub/loop/time/duration | `AnimationTransportUsesCanonicalDurationAndKeepsLoopOverridePreviewOnly`, `AnimationTransportHandlesEmptyDuplicateAndUnknownStates`, shared animation vectors |
| Accessibility labels, visible focus, and deterministic focus order | `AccessibilityHasUniquePredictableFocusAndHiddenRestoreSemantics`, `ContractValidationRejectsBadRangesDuplicateBindingsAndFocus` |
| Deterministic serialization and validation | `DefaultContractRoundTripsToCanonicalDeterministicJson`, `SchemaIsPinnedDraft202012VersionOne`, fail-closed JSON tests |
| Shared adapter test vectors | `SharedVectorsAreVersionedAndExerciseCrossEngineBoundaryCases` |
| ADR | `scripts/Test-ArchitectureDecisionRecords.ps1` validates ADR-0014 and all indexes |

All named code tests carry `[Trait("Task", "PB-0913")]` through their containing test class.

## Reuse and Duplication Audit

- Reused PB-0906 background, lighting, colour, and directional-light types and defaults.
- Reused `AnimationDefinition.DurationSeconds` and `LoopBehavior`; transport does not recalculate
  frame duration or alter source loop metadata.
- Reused `InternalAssetId` for item identities and the repository's task-local explicit-result
  pattern for expected input failures.
- Reused `JsonInputSafeguards`, embedded schema conventions, Draft 2020-12 validation, and canonical
  writer pattern from existing Contracts serializers.
- State transitions are implemented once in Domain and are intended for Unity, Unreal, and desktop
  adapters. No engine, WPF, renderer, filesystem, or packaged-asset mutation logic was added.
- Intentional duplication: camera and light yaw canonicalization are each private to small cohesive
  state types. Their lifecycle and policies differ; extracting a general angle utility would be a
  speculative cross-domain abstraction. PB-0913 owns review if later consumers prove identical
  semantics.

## Validation Checkpoint

- PB-0913 Domain tests: 9/9 passed.
- PB-0913 Contracts tests: 11/11 passed.
- Domain and Contracts builds: passed with zero warnings and zero errors.
- Full local Core CI: all 9 stages passed in 6m40s; 2,320/2,320 tests passed.
- Release solution build: passed with zero warnings and zero errors.
- Repository baseline: 32/32 passed, including ADR indexes, local links, Git integrity, and
  public-repository prohibited-content checks.
- .NET and Ruff format/lint verification: passed.
- Markdownlint CLI 0.23.2: 137/137 Markdown files passed with zero issues. The shared
  `.markdownlint.jsonc` disables only the repository-incompatible line-length, single-H1 backlog,
  and table-column-alignment rules.

## Publication Completion

- Final task commit: `f6b38c22b3c50b6e69d13a32d15d9cc323fcf1fc`.
- Published through [pull request #82](https://github.com/avivperets26/3DModels-Package-Builder/pull/82)
  and merged into `main` as `34f6b06d308dcf5c363999eef5aeb48b80025f14`.
- Required exact-merge [main workflow run 31387407667](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31387407667)
  completed successfully.
- The user explicitly confirmed the push, merge, green required `main` CI, and continuation on
  2026-08-10. No CI, quality, completion, or workflow exception was used.
