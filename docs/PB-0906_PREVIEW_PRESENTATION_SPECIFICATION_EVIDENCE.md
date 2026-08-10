# PB-0906 Preview Presentation Specification Evidence

## Lifecycle

- Task: PB-0906 — Implement preview presentation specification.
- Canonical branch: `feat/PB-0906-preview-specification`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-10.
- Current blocker: none for local implementation; publication, required `main` CI, explicit user
  confirmation, and next-task rollover remain.

## Implemented Contract

`PackageBuilder.Domain.Preview` now defines an immutable engine-neutral presentation aggregate and
small validated value objects for:

- stable view IDs;
- hero, orthographic front/back/left/right, detail, animation-pose, set-overview, and
  collection-overview roles;
- perspective versus orthographic projection;
- entire-product, assembled-set, all-collection-items, and selected-item visibility;
- normalized radial studio-background colours and shape;
- directional key/fill colour, direction, and intensity;
- approved case-specific default view sets and capture order.

The aggregate reuses `ProductCase`, `ItemSetDefinition`, `ItemCollectionDefinition`, and
`InternalAssetId`. It validates selected item IDs against the supplied set or collection, requires
the correct case-specific overview/pose roles, retains deterministic author-controlled ordering,
supports multiple detail or animation-pose views through distinct IDs, and returns structured
expected-input errors.

## Approved Defaults

The shared defaults codify the values already used by the verified Unity dark-studio shell:

| Token | Value |
|---|---|
| Standard views | Hero, front, back, left, right |
| Perspective field of view / framing padding | 35 degrees / 1.25 |
| Outer / centre RGB | (0.012, 0.014, 0.018) / (0.14, 0.16, 0.20) |
| Centre / radius / horizontal scale | (0.50, 0.58) / 0.70 / 0.82 |
| Key light | yaw -32, pitch 42, intensity 1.15, RGB (1.00, 0.94, 0.84) |
| Fill light | yaw 145, pitch 25, intensity 0.55, RGB (0.62, 0.75, 1.00) |

All colour channels and background coordinates use inclusive normalized unit intervals. Background
radius and horizontal scale must be positive finite values. Light yaw is canonicalized by requiring
[-180, 180], pitch requires [-90, 90], and intensity must be finite and non-negative.

## Requirement-to-Test Mapping

| PB-0906 criterion | Automated evidence |
|---|---|
| Hero, orthographic, detail, and animation-pose views are typed | `ViewRolesHaveStableIdentitiesAndRequiredProjections`, `StableViewIdsAllowMultipleDetailAndAnimationPoseViews` |
| Set and collection overviews are typed and case-correct | `SetAndCollectionDefaultsUseDistinctOverviewAndVisibilitySemantics`, `AggregateRejectsCaseIncompatibleViewsAndVisibility` |
| Visibility is typed and validates item membership | `DetailViewsMaySelectOnlyKnownSetOrCollectionItems`, `AggregateRejectsCaseIncompatibleViewsAndVisibility` |
| Background and lighting are typed, validated, and use approved defaults | `ApprovedStudioDefaultsMatchExistingUnityPresentationValues`, `PrimitiveFactoriesRejectInvalidValuesWithoutThrowing` |
| Case-specific required views and malformed aggregates fail closed | `AggregateRequiresCaseSpecificViews`, `GroupAggregatesRequireTheirDistinctOverviewRoles`, `AggregateRejectsNullEmptyDuplicateAndMissingPresentationParts` |
| Values remain deterministic and engine-neutral | `ValuesAreImmutableOrderedAndCultureIndependent`, `PreviewDomainRemainsEngineRendererFilesystemAndUiIndependent` |

All named tests carry `[Trait("Task", "PB-0906")]` through their containing test class.

## Reuse and Duplication Audit

- Reused the closed `ProductCase` identities instead of introducing preview-specific case enums.
- Reused PB-0106 set/collection definitions and their validated item ordering/membership.
- Reused `InternalAssetId` for view and selected-item identities instead of defining another naming
  grammar.
- Followed existing Domain immutable factories and task-local validation-result conventions.
- A small task-local deterministic hash helper follows existing Domain value-object precedent. The
  repository currently has several namespace-local hash helpers and no approved shared primitive;
  consolidating them is outside PB-0906 and should be owned by a future explicit refactor task.
- No Unity, Unreal, WPF, renderer, filesystem, serialization, input, selection-state, or animation-
  transport logic is duplicated. PB-0913 owns the interactive and serialized contract.

## Validation Checkpoint

- PB-0906 focused Domain tests: 18/18 passed.
- Domain test-project build: passed with zero warnings and zero errors.
- Full local Core CI: all 9 stages passed in 5m43s; 2,300/2,300 tests passed.
- Release solution build: passed with zero warnings and zero errors.
- Repository baseline: 32/32 passed, including public-repository prohibited-content checks.
- Unity product policy: 35/35 passed; template 8/8 and worker package 9/9 passed.
- .NET and Ruff format/lint verification: passed.
- `git diff --check`: passed.

PB-0906 remains PROCESS until the user performs the Git publication sequence, required exact-main
CI succeeds, the user explicitly confirms completion, and its status is synchronized during the
next task rollover.
