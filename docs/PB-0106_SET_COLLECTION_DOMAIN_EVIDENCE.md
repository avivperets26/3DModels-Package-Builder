# PB-0106 Set and Collection Domain Evidence

**Task:** PB-0106 — Implement set and collection item definitions
**Branch:** `feat/PB-0106-set-collection-domain`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-24

## Scope

PB-0106 adds immutable renderer-, engine-, filesystem-, and marketplace-independent item definitions in `PackageBuilder.Domain.Items`. It represents stable item IDs, user-controlled ordering, extensible categories and logical attachment slots, item relationships, declared shared source assets, set assembly membership and slot invariants, compatibility metadata, and the semantic separation between related sets and independent collections.

It adds no Unity prefabs or scenes, Unreal Blueprints or maps, material/texture deduplication, source-file grouping, manifest mapping, attachment transforms, skeleton retargeting, package folders, preview selection/rendering, CSV generation, UI, marketplace category identifiers, publisher data, persistence, networking, or JSON contracts.

This branch also performs the approved PB-0105 rollover. PB-0105 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion Log with its supplied publication evidence. PB-0106 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and is absent from the Completion Log.

## PB-0105 Final Publication Evidence

- Final task commit: `94a3caa147d1c196a9f21a54c2b47230d34c8753`.
- Pull request: [#19](https://github.com/avivperets26/3DModels-Package-Builder/pull/19).
- Successful PR workflow: [run 30102421376](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30102421376).
- Merge commit: `67aff0d6c4a1c7f19b7d88b13cd64bd7da998aab`.
- Successful required main workflow: [run 30102701368](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30102701368).
- The user explicitly confirmed completion on 2026-07-24.
- No CI, completion, or quality exception was used.

## Public API

| API | Purpose |
|---|---|
| `ItemCategory.Create(...)` | Creates a validated extensible semantic category without a hard-coded registry. |
| `AttachmentSlot.Create(...)` | Creates a validated extensible logical body/attachment slot without engine behavior. |
| `SharedAssetDefinition.Create(...)` | Associates an `InternalAssetId` with an existing immutable `SourceAsset`. |
| `ItemDefinition.Create(...)` | Creates one stable item with categories, optional slot, and shared-asset references. |
| `ItemRelationship.Create(...)` | Creates one canonical undirected relationship between distinct item IDs. |
| `CompatibilityMetadataEntry.Create(...)` | Creates one extensible exact-value compatibility entry. |
| `AssembledSetMember.Create(...)` | Declares one assembled member and its expected logical slot. |
| `AssembledSetRules.Create(...)` | Creates deterministic complete-membership, slot-uniqueness, and compatibility intent. |
| `ItemSetDefinition.Create(...)` | Creates a related `ProductCase.ItemSet` aggregate with optional assembly behavior. |
| `ItemCollectionDefinition.Create(...)` | Creates an independent `ProductCase.ItemCollection` aggregate and rejects assembly behavior. |
| `ItemValidationResult<T>` | Returns task-local structured expected-input failures without throwing or pre-empting PB-0109. |

## Identity, Category, and Ordering Invariants

- Item and shared-asset identities reuse the existing validated `InternalAssetId`; PB-0106 does not duplicate its grammar or validation.
- IDs preserve casing and compare with exact ordinal, case-sensitive semantics. `Item` and `item` are distinct; exact duplicates are rejected.
- Categories and slots accept extensible lowercase ASCII words separated by single hyphens. No armor, weapon, body-slot, engine, or marketplace registry is embedded.
- Group item order is the exact caller-supplied user order and is part of equality and hashing.
- Categories, item-level shared references, relationships, shared declarations, assembled members, and compatibility entries use deterministic ordinal returned order.
- All retained collections are copied into immutable read-only snapshots.
- Empty and single-item sets and collections are valid because the approved requirements establish no arbitrary minimum size.

## Relationship and Shared-Asset Invariants

- Relationships are undirected and require two distinct non-null IDs.
- Relationship endpoints are canonicalized ordinally, so exact and reversed duplicates are one logical relationship.
- A group rejects unknown endpoints and duplicate relationships.
- Shared declarations bind a stable ID to an existing immutable `SourceAsset`; they do not deduplicate, hash, copy, or transform content.
- Items may reference a shared declaration, and multiple items may reference the same declaration.
- Duplicate declarations, null/missing references, unknown references, duplicate item-level references, and declarations referenced by no item are rejected.

## Set and Collection Invariants

- `ItemSetDefinition.ProductCase` is `ProductCase.ItemSet`; `ItemCollectionDefinition.ProductCase` is `ProductCase.ItemCollection`.
- Sets may omit assembled rules or declare complete assembled membership, logical slots, a unique-slot requirement, and extensible compatibility metadata.
- Assembled members must identify every declared item exactly once. Null, duplicate, unknown, missing, and slot-contradictory members are rejected.
- An assembled member cannot silently add, remove, or override the slot declared by its item.
- When the rules require unique attachment slots, repeated non-null slots are rejected; without that rule, shared logical slots remain valid.
- Collections remain independently usable, reject any assembled-set rules, and expose no combined runtime object.

## Determinism and Equality

All PB-0106 values use immutable value-style equality. Stable FNV-based hashes use ordinal UTF-16 text and nested stable value hashes. Equality, duplicate detection, ordering, and hashing do not depend on current culture, UI culture, input order where canonical ordering is required, or process-randomized string hashing.

## Test Inventory

The xUnit v3 PB-0106 suite covers:

- Valid and invalid extensible categories and attachment slots.
- Valid Item Set and Item Collection definitions, including empty and single-item groups.
- Exact duplicate and case-different item IDs.
- User-controlled item order and deterministic returned order for every canonicalized collection.
- Valid relationships plus null, self, unknown, exact-duplicate, and reversed-duplicate relationships.
- Optional slots, contradictory assembled slots, and conditional conflicting-slot rejection.
- Shared assets used by multiple items plus null, duplicate, unknown, duplicate-reference, and unreferenced declarations.
- Complete assembled membership plus null, duplicate, unknown, missing, and contradictory members.
- Explicit rejection of assembled rules for collections and absence of a collection combined-runtime-object API.
- Immutable snapshots, ordinal casing, complete equality/hashing fields, current-culture and UI-culture independence, and Domain dependency isolation.

## Coverage Evidence

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` with `ExcludeAssembliesWithoutSources=None`. Generated reports remain beneath ignored `artifacts/PB-0106`.

All 13 new production source files and all 14 generated coverage class entries report 100% line and 100% branch coverage in the final focused PB-0106 run.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0106 tests | Pass; 36 passed, 0 failed, 0 skipped. |
| Per-file coverage | Pass; all 13 new production files and 14 coverage class entries report 100% line and 100% branch coverage. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 504 passed, 0 failed, 0 skipped, preserving the previous 468 tests. |
| All four core test projects | Pass; 507 discovered, 507 passed, 0 failed, 0 skipped. |
| Solution architecture validator | Pass; 7 checks passed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| ADR validator | Pass; 8 checks passed, 0 failed. |
| Repository baseline with `RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| Full core CI | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and lint | Pass; `.NET` formatting plus Ruff lint/format checks. |
| Diff and public-repository scans | Pass; `git diff --check` plus 25-candidate secret, personal/out-of-root path, binary, large/generated-content, prohibited-extension, placeholder, and production-coupling scans. |

## Remaining Gates

PB-0106 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and remains absent from the Completion Log. Local implementation and the requested validation matrix pass. User-controlled staging, task commit, task-branch push, merge into and push of `main`, successful required `main` CI, explicit completion confirmation, and next-task rollover synchronization remain.
