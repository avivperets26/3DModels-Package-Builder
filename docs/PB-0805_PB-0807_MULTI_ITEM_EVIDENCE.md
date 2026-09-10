# PB-0805–PB-0807 — Attachments, collection export and overview layout

Current status: 🟢 **DONE** for PB-0805, PB-0806 and PB-0807. Recorded during the PB-0808–PB-0810 rollover after task commit `88cba0209176fce4a4f84d4307a9a171c3a2c80f`, main merge `ee5108fe3ee66663fad717595cbbc419da3744e0`, [successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34487441758), and user acceptance/request for the next scope. The following preserves the historical handoff. Branch: `feat/PB-0805-PB-0807-multi-item-flow`.
Base: `d7f3d1c7a3fc222d26d63c7f40256f1508fafa58`, verified clean and equal to freshly fetched/pulled
origin/main before branch creation. The user explicitly approved all three tasks in one branch
on 2026-09-10; AGENTS.md records this narrow exception. Each task retains its own acceptance and status.

## PB-0805: attachment validation

`MultiItemBuildPlan.Attachments` maps the immutable assembled-set plan plus explicitly supplied
bindings into version-1 JSON. Every slotted member requires exactly one binding containing its
item ID, logical slot, target ID, kind (`socket`, `bone`, `body-slot`) and exact hierarchy path
relative to the target root. Unslotted members do not acquire inferred attachments.

Unity resolves target IDs using the caller's explicit asset-reference map. Paths must identify
exactly one transform in the current imported target. A bone must also appear in an imported
SkinnedMeshRenderer bone list. Socket/body-slot paths are explicit declarations, with no fuzzy
matching or inference from names. Missing targets, ambiguous paths, wrong slots/kinds, duplicate
bindings and unknown members fail before assembled outputs are created.

The existing assembled-set generator now gates applicable builds through that validation and
records `attachmentValidation: validated` plus the validated bindings in `SET_<id>.json`.
It still places nested items at their authored/reset origin. Target validation does not retarget
skin, change geometry or claim physical socket placement. Target assets are build inputs and are
not implicitly copied into the customer package. Validation describes the target supplied to
that build; a subsequent build must validate its current targets again.

## PB-0806: independent collection package

`MultiItemBuildPlan.Collection` requires an explicit reviewed collection and preserves its
declared order. `UnityCollectionPackageFlow` accepts the already prepared PB-0802/PB-0803 outputs,
requires exactly one uniquely named `P_<itemId>.prefab` per declared item, rejects additional
prefabs (including an implicit combined prefab), validates the product, and delegates to the
existing dependency-closed, collision-safe package exporter. Separate items keep their original
meshes and materials. JSON metadata is permitted only beneath Documentation, with misplaced JSON blocked.
A runtime assembly is a separate explicit set operation, never inferred
from a collection's size. No new material compiler, asset deduplicator or archive writer is added.

## PB-0807: deterministic overview

The existing `UnityOverviewSceneComposer` accepts ordered `ItemPrefabReferences` and a nonnegative
`ItemGap` (default 0.25 metres). Set callers use `AssembledSetPlan.Members` order; collection callers
use the collection plan order. Every item remains a linked prefab instance under PreviewTarget.
The shared layout policy uses aggregate renderer bounds, grounds each item, centers its depth,
spaces adjacent X bounds by the gap, and centers the complete row. Source prefabs remain unchanged.
Empty/missing bounds, duplicate IDs, nonfinite/reversed/excessive measurements and invalid gaps fail.
Limits are 10,000 items, absolute bounds/gap up to 1,000,000 and total row extent up to 10,000,000.

The existing preview controller frames all item bounds and keeps the studio controls. Single-product
composition retains its original behavior. Multi-item animation selection remains PB-0808 and
related selector work; the scene does not arbitrarily bind its shared transport to one member.

## Reuse and platform boundary

Existing Domain membership/slot rules, Application ownership/reuse/prefab plans, Unity prefab
generator, overview template/controller, package validator/exporter and isolated clean-import
harness were searched and reused. No business policy is duplicated into a second engine adapter. Repository and quality validators also use one shared task-branch policy, with regressions for approved IDs and rejected scope expansion.

The canonical CLR-only `AttachmentPolicy` and `OverviewLayoutPolicy` sources live in the embedded
worker's `Editor/Shared` directory and are linked into the Application project. This physical
location lets the distributable Unity template compile the exact same source without referencing
the .NET 10 application assembly. The files must remain engine-free and C# 9 compatible. A scoped
EditorConfig preserves block namespaces and older collection syntax; the attachment wire DTOs
retain fields because Unity JsonUtility cannot serialize properties. This is a serializer/compiler
constraint, not a coverage exemption. Layout measurements use ordinary properties.

Versioned adapter envelopes have shared golden JSON fixtures. The Unity integration temporarily
relocates the fixture runtime scripts into the collection root during export, then restores them,
avoiding duplicate runtime assemblies. The exported collection contains its own runtime scripts;
the fresh collection project imports that package before any worker validation code is supplied.
Use the clean collection project for inspecting the self-contained result.

## Criterion-to-test traceability

Owner: each PB ID below. Application checks are in `MultiItemPolicyTests`; shared vectors are
`set-attachments.json`, `collection-plan.json` and the existing assembled-set manifest/plan.
Engine checks run in the existing Unity product integration harness.

| Task / criterion | Concrete automated evidence |
| --- | --- |
| PB-0805: exact sockets/bones/body slots | `AttachmentsRequireExactCurrentTargetAndUniqueBinding`; `UnityAssembledSetIntegration.Run` validates socket/body paths, real skin bone membership and non-bone rejection |
| PB-0805: applicable missing attachments block output | Missing target/binding tests; generator negative case verifies no prefab/document exists |
| PB-0805: metadata survives import | Shared exact JSON comparison; assembled `VerifySaved` checks validated bindings after isolated import |
| PB-0806: separate uniquely named outputs | Application collection vector; `UnityMultiItemIntegration.Run` exports the existing Alpha/Zed prefabs and rejects an extra combined prefab |
| PB-0806: correct references and clean package | Existing item verification plus product validation/export dependency checks; isolated `collection-overview` import runs item verification again |
| PB-0806: no overwrite | Export collision test reuses the existing exporter and rejects replacement |
| PB-0807: bounds spacing/order | `LayoutPreservesOrderGroundsAndCentersUnequalOffsetBounds`; real scene verification checks ordered linked prefabs and rejects overlapped positions |
| PB-0807: boundaries and repeatability | `InvalidGapsReturnNoPartialLayout`, `InvalidBoundsAndExcessiveInputsAreRejected`, repeated/single layout checks |
| PB-0807: saved scene, source preservation and camera | Unity scene save/reopen, source-byte comparison, all eight bounds corners in camera viewport, isolated collection scene reimport |
| Regression | Full core CI; existing Unity static/rigged/animated generation, clean imports, preview Play mode and populated-project reopen |

These checks measure this task scope. They do not claim unmeasured performance, mutation, coverage,
accessibility or public-release certification.

## Validation and lifecycle

Real Unity 6000.3.10f1 integration passed in `artifacts/u/70875484`: generation, attachment failures and saved metadata, independent collection export, bounds-aware overview, isolated static/item-set/collection/rigged imports (zero findings), Play mode and populated-project reopen. The pointer is `artifacts/PB-0805-PB-0807/unity-result-pointer.json`. Unity guards: 8 template, 9 worker and 44 product-policy checks passed. The 11 focused Application checks passed. Full core CI passed all 9 stages in 3 minutes 57 seconds: 34 repository checks, locked restore, Release build with zero warnings/errors, .NET formatting, Ruff lint/format, and 2,428 tests with zero failures/skips. Evidence: `artifacts/PB-0805-PB-0807/core-ci.log`, `core-ci-transcript.log` and `test-summary.json`. No implementation blocker remains; publication and successful main CI/user confirmation are still pending.

PB-0804 completion is recorded once in this branch using its task commit, main merge, successful
main CI and user confirmation. Backlog: 264 total; 117 DONE; 3 IN PROGRESS; 0 BLOCKED; 144 BACKLOG;
147 remaining; 44.3% complete. All three current task checkboxes remain unchecked pending the
existing publication/main-CI/user-confirmation gates and subsequent completion rollover.

## Handoff

Changed areas: Application plan and shared-policy compile link; attachment, collection and overview
Unity adapters/integration; shared JSON fixtures and application tests; exact Unity inventories,
policy and integration scripts; shared task-branch policy and its narrow-exception regressions; compiler-style compatibility; backlog, engineering rules/skill,
plan, architecture and task evidence. Generated engines, projects, packages and logs remain ignored.

Suggested commit: `feat(PB-0805-PB-0807): validate attachments and export collection overviews`.
The user explicitly authorized staging, commit, push and merge for this combined scope on 2026-09-10. This is the prepublication evidence snapshot; final task/main SHAs and main CI are recorded at the next-task rollover. From `C:\Dev\PackageBuilder`:

```powershell
git diff --check
& ./scripts/Update-BacklogStatus.ps1
git status --short
# After reviewing and staging the listed scope:
git commit -m "feat(PB-0805-PB-0807): validate attachments and export collection overviews"
git push -u origin feat/PB-0805-PB-0807-multi-item-flow
```

## Optional visual review

Open the clean collection project `artifacts/u/70875484/c` in the pinned Unity Editor and load
`Assets/PBItemTests/Scenes/S_ExampleCollection_Overview.unity`. Zed (red material) precedes Alpha
(blue material) from left to right. Both remain linked individual prefabs. The fixture deliberately
reuses the same simple source model; it tests ownership and layout, not finished customer artwork.
The set document in `artifacts/u/70875484/i/Assets/PBSetTests/Documentation/SET_ExampleSet.json`
retains the validated Socket/Body bindings and Generation=One compatibility declaration.

## Exact reviewable file scope

```text
.editorconfig
AGENTS.md
docs/IMPLEMENTATION_BACKLOG.md
docs/Package_Builder_Plan.md
docs/PB-0804_UNITY_ASSEMBLED_SET_EVIDENCE.md
docs/PB-0805_PB-0807_MULTI_ITEM_EVIDENCE.md
docs/TECH_STACK_AND_ARCHITECTURE.md
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/Shared/AttachmentPolicy.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/Shared/OverviewLayoutPolicy.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityAssembledSetGenerator.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityAssembledSetIntegration.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityCleanReimportIntegration.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityCollectionPackageFlow.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityMultiItemIntegration.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityMultiItemLayout.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityOverviewScenePipeline.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityPackageValidator.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityProductEditorIntegrationTests.cs
engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnitySetAttachmentValidator.cs
scripts/Invoke-UnityProductIntegration.ps1
scripts/TaskBranchPolicy.Common.ps1
scripts/Test-QualityAndReleaseGates.ps1
scripts/Test-RepositoryBaseline.ps1
scripts/Test-UnityProductPolicies.ps1
scripts/Test-UnityProjectTemplate.ps1
scripts/Test-UnityWorkerPackage.ps1
skills/package-builder-engineering/SKILL.md
src/PackageBuilder.Application/Items/MultiItemBuildPlan.cs
src/PackageBuilder.Application/PackageBuilder.Application.csproj
tests/fixtures/manifests/collection-plan.json
tests/fixtures/manifests/set-attachments.json
tests/PackageBuilder.Application.Tests/Items/MultiItemPolicyTests.cs
```
