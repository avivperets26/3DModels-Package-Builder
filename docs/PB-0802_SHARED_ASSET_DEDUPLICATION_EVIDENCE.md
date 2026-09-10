# PB-0802 — Shared texture and material deduplication

Current status: 🟢 **DONE**, recorded in PB-0803 after user confirmation. Task commit `b9c17f6b7778335d724349ccdf4cc0709dc77215`, main merge `fad8adf0805d0a4f2784593a0b580906f32304d5`, [successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34468230022). The handoff below preserves the historical prepublication record.
Branch: `feat/PB-0802-shared-asset-deduplication`.
Base: `d7286bd725fb39519e344059394218da66bcf162`, verified clean and equal to origin/main after
`git fetch origin`, `git switch main`, and `git pull --ff-only origin main` on 2026-09-10.

## Boundary and reuse rules

`SharedAssetDeduplicator.Plan` consumes a PB-0801 reviewed set/collection manifest and exact
`SourceContentIdentity` bindings from the current immutable input snapshot. PB-0204's existing
`ArtifactHashService` streams the files and supplies SHA-256 plus byte length. The planner performs
no file reads, writes, copying, network activity, cache persistence, engine import or manifest edits.
The caller must retain the immutable snapshot and verify the returned identities before emission;
supplying a manually invented or stale identity does not verify file content. Failed hashing must
stop the caller before planning. This plan is build-local and must be recomputed when inputs change.

- Texture equivalence requires equal SHA-256 **and** byte length, role, colour space and resolved
  normal orientation. Different paths and original filenames do not prevent genuine reuse.
- Each equivalence class chooses its ordinally first logical source. An image used in different
  roles keeps separate interpretations. No filename, approximate colour, perceptual similarity,
  decoder output or engine heuristic can establish equivalence.
- `NormalConvention.Auto` requires review rather than assuming an orientation.
- Materials first resolve texture references to those canonical interpretations, then use the
  existing `MaterialDefinition.Equals` rule. Every approved scalar, emission component, UV
  transform, surface/opacity/cutout setting, double-sided flag and texture role remains significant.
- Equal materials choose the ordinally first existing material ID. Aliases include self-references
  and resolve directly to canonical assets; consumers need no recursive alias traversal.
- The plan retains immutable, ordered source identities, texture aliases and material aliases.
  Item IDs, order, relationships, shared declarations and reviewed source ownership stay in the
  original manifest, unchanged. A content match never combines items or model files.
- Only material-assigned images have sufficiently specified interpretation for this task. Bare
  shared-asset declarations and unassigned images are preserved, not guessed into material roles.
- Missing, unknown, wrong-case, metadata-mismatched, null or duplicated identity bindings fail
  with blocking findings and no partial plan. Cancellation is propagated through planning.

PB-0803/PB-0806 and subsequent target generators own emission: resolve each original material ID
and texture assignment through this plan and emit each distinct canonical asset once. They must
not rewrite the manifest or merge items. No existing single-item target behavior or schema changes
in this task. Target-specific settings outside the approved Domain model are not part of this
equivalence contract and cannot be silently ignored by future consumers.

## Findings

| Code | Corrective action |
| --- | --- |
| `SHARED_ASSET_MAPPING_REQUIRED` | Complete reviewed multi-item source ownership. |
| `SHARED_ASSET_IDENTITY_INVALID` | Supply an exact declared image and its current snapshot identity. |
| `SHARED_ASSET_IDENTITY_DUPLICATE` | Supply one identity per source, even if duplicates agree. |
| `SHARED_ASSET_IDENTITY_MISSING` | Stream-hash every material-assigned image. |
| `SHARED_ASSET_REVIEW_REQUIRED` | Resolve Auto normal orientation before reuse. |

## Acceptance-to-tests matrix

Application tests: `tests/PackageBuilder.Application.Tests/Items/SharedAssetDeduplicatorTests.cs`.
Infrastructure integration: `tests/PackageBuilder.Infrastructure.Tests/Artifacts/SharedAssetHashIntegrationTests.cs`.

| Criterion / requirement | Concrete automated evidence |
| --- | --- |
| Reuse identical texture/material inputs, PB-0802 | `IdenticalContentAndIntentShareOneCanonicalAssetWithoutChangingOwnership`, both set and collection; `PhysicalSnapshotHashesDetermineReuseWithoutChangingFiles`, real streamed receipts. |
| Keep merely similar inputs separate, PB-0802 | `DifferentBytesOrLengthNeverShareEvenWhenNamesAndSettingsLookSimilar`, `EqualBytesWithDifferentInterpretationsStaySeparate`, `EveryMaterialParameterPreventsUnsafeReuseAndSurvivesCanonicalization`, `MissingTextureRoleDiffersFromAssignedTexture`. |
| Safe cross-role and texture-free behavior | `OneSourceCanHaveSeveralDistinctInterpretations`, `TextureFreeEqualMaterialsNeedNoHashes`. |
| Fail closed / hostile inputs, TEST-006 | `InvalidIdentitySetsFailWithoutPartialPlans`, `UnresolvedNormalConventionRequiresReview`, `DraftOrSingleProductCannotBypassReviewedMapping`. |
| Determinism and ownership, TEST-012 / ENG-003 | `OrderCultureAndManifestRoundTripDoNotChangeAliases`, `DeclaredSharedReferencesRemainDistinctWhenTheirMaterialTexturesAreReused`, immutable snapshot/collection checks and unchanged manifest serialization. |
| Cancellation / bounded hashing, PERF-003–004 | `CancellationAndNullProgrammingArgumentsDoNotReturnPlans`; existing `ArtifactHashServiceTests.LargeInputUsesBoundedStreamingReads`; physical integration spans multiple 64-KiB reads. |
| Canonical validation reuse, ENG-004 | `ReplacementTexturesReuseDomainValidation`; all material-parameter vectors verify replacement preserves intent. |

This task makes no measured throughput, memory-budget, coverage/mutation threshold, engine-export
or production-release claim. The broader PB-180x quality and release evidence remains required.

## Reuse audit and changed scope

Reviewed existing Domain material/texture equality, source identities, manifest references and
item mappings; Contracts artifact content identity and byte-duplicate detector; Infrastructure
streamed hashing; and current target compiler boundaries before adding code.

Reused `ArtifactContentIdentity` equality (length plus SHA-256) rather than duplicating hashing.
`ArtifactDuplicateDetector` groups content alone and cannot decide texture interpretation, so the
planner composes that existing identity type with Domain semantics. Dictionary hashes are indexes;
full equality decides membership. `MaterialDefinition.WithTextureAssignments` delegates to its
existing factory, allowing semantic normalization without copying material equality or validation.
No business rule is duplicated into engine/UI adapters. Test fixtures exercise the same canonical
types; no new dependencies, binaries, customer assets or public configuration are introduced.

Changed files: Application planner and immutable plan/result contracts; Domain material replacement
method; two focused test files and an Infrastructure test-only Application project reference/lock
entry and its exact allowlist in `scripts/Test-TestProjects.ps1`; this evidence, plan, architecture
and backlog. PB-0714 and PB-0801
evidence documents now identify their verified completion and preserve historical validation.
Their two completion-log rows are recorded once in this task branch at the user's reconciliation
request. Existing AGENTS/engineering-skill status and fresh-main rules already cover this work.

## Validation and handoff

- Application acceptance tests: 45 passed, 0 failed, 0 skipped.
- Physical hash integration: 2 passed, 0 failed, 0 skipped.
- Full `Invoke-CoreCI.ps1`: all nine stages passed in 3 minutes 58 seconds; 34 repository checks,
  locked restore, Release build with 0 warnings/errors, .NET format, Ruff lint/format and all seven
  test projects: **2,405 passed, 0 failed, 0 skipped**. The 47 task tests are included in this total.
  Evidence: ignored `artifacts/PB-0802/core-ci.log` and `artifacts/PB-0802/test-summary.json`.
- Final backlog accounting and whitespace checks passed. Active Work matches the sole active task;
  all 114 DONE tasks have individual Completion Log rows, with no duplicate IDs.
- No manual visual check is required: this is an application planning boundary without UI changes.
- The user accepted the validated task and explicitly authorized commit, push and merge on
  2026-09-10. This is the prepublication evidence snapshot; final task/merge SHAs and main CI
  are recorded during the next-task completion rollover.

Suggested commit: `feat(PB-0802): plan exact shared texture and material reuse`.
After review and explicit publication authorization, manual commands from the repository root:

```powershell
git diff --check
git add -- src/PackageBuilder.Application/Items/SharedAssetDeduplicator.cs src/PackageBuilder.Application/Items/SharedAssetReusePlan.cs src/PackageBuilder.Domain/Materials/MaterialDefinition.cs tests/PackageBuilder.Application.Tests/Items/SharedAssetDeduplicatorTests.cs tests/PackageBuilder.Infrastructure.Tests/Artifacts/SharedAssetHashIntegrationTests.cs tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj tests/PackageBuilder.Infrastructure.Tests/packages.lock.json scripts/Test-TestProjects.ps1 docs/PB-0802_SHARED_ASSET_DEDUPLICATION_EVIDENCE.md docs/IMPLEMENTATION_BACKLOG.md docs/Package_Builder_Plan.md docs/TECH_STACK_AND_ARCHITECTURE.md docs/PB-0714_UNITY_GENERIC_TOPOLOGY_EVIDENCE.md docs/PB-0801_MULTI_ITEM_MAPPING_EVIDENCE.md
git diff --cached --check
git commit -m "feat(PB-0802): plan exact shared texture and material reuse"
git push -u origin feat/PB-0802-shared-asset-deduplication
```

PB-0802 remains unchecked and IN PROGRESS until its publication, required main CI, user acceptance
and next-task completion rollover. Current totals: 264 tasks; 114 DONE, 1 IN PROGRESS, 0 BLOCKED,
149 BACKLOG; 150 remaining; 43.2% complete.
