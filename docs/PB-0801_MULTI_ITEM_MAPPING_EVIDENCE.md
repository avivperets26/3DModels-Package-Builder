# PB-0801 — Multi-item source-to-manifest mapping

Current status: 🟢 **DONE**, reconciled in PB-0802 on 2026-09-10 after user acceptance, PR #91 merge `d7286bd725fb39519e344059394218da66bcf162` and [successful exact-main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34464190458). Task commit: `990a70b23d7fb2c57cea1967eedc777d796f1692`. The handoff below is the historical prepublication record.

Historical prepublication status: 🟡 **IN PROGRESS** — implemented and validated locally; publication gates remained.
Branch: `feat/PB-0801-multi-item-mapper`.
Starting main commit: `599cec4787bff2a0710dc5729dca4e65c85a5c72`. A fresh origin fetch confirmed
local main, origin/main and this branch's base all match. The user's permanent request to fetch,
fast-forward main, verify equality and only then create future branches is recorded in AGENTS.md,
CONTRIBUTING.md and the engineering skill.

## Implemented boundary

`MultiItemSourceMapper.Map` accepts an existing item-set or item-collection manifest and explicit
`ItemSourceAssignment` requests. It delegates to `ProductManifest.WithItemSourceAssignments`,
which reuses canonical manifest validation and snapshots successful assignments. No source file
is opened, renamed, copied, split or grouped by heuristics. Existing `InternalAssetId` values and
user-controlled group order are retained; filename changes never generate replacement item IDs.

The optional schema-version-1 `itemSourceAssignments` property persists exact item IDs and
declared logical source references. Entries serialize in ordinal item-ID/source order. Old v1
documents without this property remain readable as unmapped drafts and retain their previous
canonical output. Explicitly supplied mappings must be complete; consumers of mapped item data
must call the mapper before the later PB-0803/PB-0806 generation steps.

```json
"itemSourceAssignments": [
  { "itemId": "Helmet", "sourceReference": "models/helmet.glb" },
  { "itemId": "Shoulders", "sourceReference": "models/shoulders.fbx" }
]
```

Every item requires a model source; every FBX, GLB or archive source requires exactly one item
owner. Multiple files may belong to one item. A model file assigned to multiple items requires
review/splitting at the file boundary; this task does not invent submesh or archive-member ownership.
Explicit image sharing is allowed, without claiming content deduplication (PB-0802). Image-only
assignments do not satisfy an item's model requirement. Unassigned images may continue to be
referenced by existing material/shared-asset declarations.

## Stable blocking findings

| Code | Meaning |
| --- | --- |
| `MANIFEST_ITEM_SOURCE_CASE_INVALID` | Assignments supplied for a non-group product. |
| `MANIFEST_ITEM_SOURCE_INVALID` | Null/incomplete assignment. |
| `MANIFEST_ITEM_SOURCE_UNKNOWN` | Item ID or source reference does not exactly match the manifest. |
| `MANIFEST_ITEM_SOURCE_DUPLICATE` | Repeated item/source pair. |
| `MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED` | Missing ownership or ambiguous model ownership requires explicit review. |

Failures return blocking findings and no partial manifest. Callers retain the original draft and
request corrected assignments. This is a typed application/contract boundary; a visual review
screen, per-item engine generators and production release readiness are separate backlog work.

## Acceptance and reuse audit

| PB-0801 acceptance / invariant | Automated coverage |
| --- | --- |
| Assign files to stable item IDs | Set and collection mapping, immutable input/output, preserved ID and item order, JSON round-trip. |
| Ambiguous grouping requires review | Empty/partial mapping, extra unassigned models, multiple owners and image-only assignments fail with blocking findings. |
| Deterministic output | Reversed assignment order and Turkish/English cultures produce the same canonical JSON. |
| Reject invalid input | Unknown/wrong-case IDs and references, traversal, duplicates, null entries and malformed persisted assignments. |
| Preserve existing behavior | Legacy draft round-trip and the complete core test suite. |
| Keep canonical rules shared | Application mapper and JSON deserializer use the same Domain validator. |

Before implementation, reviewed PB-0106 `ItemDefinition`/set/collection models, immutable source
assets and logical references, PB-0409's no-guess case-inference boundary, manifest factories,
serializers, JSON Schema and finding creation. Reused those types and the existing finding factory.
No grouping rule is copied into UI, Blender, Unity or portable adapters. The small application entry
point exposes the shared Domain operation for future consumers; PB-0802 deduplication is not added.

## Validation and handoff

Focused tests: `tests/PackageBuilder.Application.Tests/Items/MultiItemSourceMapperTests.cs`:
29 passed, 0 failed, 0 skipped, including FBX/GLB/archive ownership, renamed files and multiple
model files assigned to one stable item. Full `Invoke-CoreCi.ps1` passed all nine stages in
4 minutes 36 seconds: 34 repository checks, locked restore, Release build (0 warnings/errors),
.NET formatting, Ruff lint/format and all seven test projects (2,358 passed, 0 failed, 0 skipped).
The focused tests are included in that total. The fresh-main workflow documentation also passed
the contribution and quality validators (11 checks each); the engineering skill passed its
frontmatter/structure validator. `git diff --check` passed. No UI or engine-generation change is
claimed, and no manual visual check is needed for this mapping boundary.
Validation logs: ignored `artifacts/PB-0801`.

Changed scope: Domain assignment/validator and manifest factory; Application mapper; Contracts
reader/writer; product manifest schema; application integration tests; plan, architecture, backlog
and this evidence. PB-0015's evidence document is marked complete with its original handoff
explicitly labeled historical. Its completion evidence and one Completion Log row are synchronized in
this branch, including task commit `bc5937729f114098d11d195d16f471e66df5d1cb`, main merge
`599cec4787bff2a0710dc5729dca4e65c85a5c72`, successful
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34460554825), and
the user's acceptance and conditional instruction to publish and continue.

Current/suggested branch: `feat/PB-0801-multi-item-mapper`.
Suggested commit: `feat(PB-0801): persist reviewed source-to-item assignments`.
PB-0801 remains unchecked and IN PROGRESS until its own publication, main CI and confirmation.
The user accepted the task validation and explicitly authorized committing and pushing PB-0801. Main merge, main CI and the completion rollover remain separate gates.
No current implementation blocker remains. Backlog totals are 264 tasks: 112 DONE, 2 IN PROGRESS,
0 BLOCKED, 150 BACKLOG, 152 remaining, 42.4% complete. PB-0714's historical reconciliation
state remains unchanged.

After review and authorization, manual commands from the repository root are:

```powershell
git diff --check
git add -- AGENTS.md CONTRIBUTING.md skills/package-builder-engineering/SKILL.md docs/IMPLEMENTATION_BACKLOG.md docs/Package_Builder_Plan.md docs/TECH_STACK_AND_ARCHITECTURE.md docs/PB-0015_JSON_SCHEMA_DISTRIBUTION_EVIDENCE.md docs/PB-0801_MULTI_ITEM_MAPPING_EVIDENCE.md schemas/product-manifest.schema.json src/PackageBuilder.Domain/Manifests/ProductManifest.cs src/PackageBuilder.Domain/Manifests/ItemSourceAssignment.cs src/PackageBuilder.Domain/Manifests/ItemSourceAssignmentValidator.cs src/PackageBuilder.Contracts/Manifests/ProductManifestJson.cs src/PackageBuilder.Application/Items/MultiItemSourceMapper.cs tests/PackageBuilder.Application.Tests/Items/MultiItemSourceMapperTests.cs
git diff --cached --check
git commit -m "feat(PB-0801): persist reviewed source-to-item assignments"
git push -u origin feat/PB-0801-multi-item-mapper
```
