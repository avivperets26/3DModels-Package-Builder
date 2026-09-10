# PB-0803 — Individual Unity item prefabs

Current status: 🟢 **DONE**, synchronized in PB-0804 after the user accepted publication and requested the next task. Task commit `13561806bcf2c2c14680dbceab083b2bf018a9e7`, main merge `e540ab270eb5f0c1c14c8391890d333631df5235`, [successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34475266254). The handoff below preserves the historical prepublication record.
Branch: `feat/PB-0803-unity-item-prefabs`.
Base: `fad8adf0805d0a4f2784593a0b580906f32304d5`, verified clean and equal to origin/main after fetch, main checkout and fast-forward pull.

## Implemented boundary

`ItemPrefabPlan.Create` consumes the successful PB-0802 reuse plan and its reviewed PB-0801
ownership. It preserves item IDs, ignores image assignments when selecting model parts, sorts
items and logical sources ordinally, and rejects case-insensitive prefab filename collisions.
The immutable plan retains the reuse aliases and input snapshot identities for import adapters.
It performs no filesystem or engine work and does not modify the manifest.

`SerializeOwnership` emits a version-1 envelope of item IDs, prefab filenames and ordered logical
model sources. `UnityItemPrefabGenerator` verifies every prepared request against that envelope.
Normalization/import adapters supply the original logical reference, normalized FBX reference,
extracted mesh bindings and resolved canonical material references for each part. These inputs
must come from the same immutable snapshot; the envelope is not a substitute for hashing inputs.

Each item produces `Prefabs/P_<ItemId>.prefab`. A single source keeps the existing `P_Model`
hierarchy. Multiple sources become reset `P_Part001`, `P_Part002`, etc. beneath reset `P_Model`.
Mesh bindings must belong to their exact imported FBX. Materials and standalone mesh references
are checked before saving and on the saved prefab. The batch rejects duplicate or missing items,
ownership mismatches, normalized source reuse and existing output collisions. A later failure
removes prefabs created by that batch, preserving existing assets and prepared inputs.

This is the static normalized-model generation boundary. It does not introduce a desktop flow,
infer per-item rig metadata, assemble sets (PB-0804), attach items (PB-0805), or complete production
collection packaging (PB-0806). Import adapters resolve PB-0802 aliases before invocation; this
task does not claim end-to-end shared-texture compilation. The exported acceptance fixture is a
test package used to prove independent clean reimport, not the PB-0806 package pipeline.

## Acceptance-to-tests matrix

Owner: PB-0803. Fixture: `tests/fixtures/manifests/item-prefab-ownership.json` plus the existing
redistributable StoneArch FBX. Application tests live in `ItemPrefabPlanTests.cs`; real engine
checks live in `UnityItemPrefabIntegration.cs`, called by `Invoke-UnityProductIntegration.ps1`.

| Criterion / requirement | Concrete automated evidence |
| --- | --- |
| Every set/collection item has one unique named prefab | `MultipleFilesProduceOnePrefabPerItemAndMatchSharedUnityVector` (both cases), real `Run` and `VerifySaved` |
| Multiple files remain one item, images are not model parts | Shared envelope and `ImageOwnershipDoesNotCreateAnExtraModelPart` |
| Stable identities, deterministic order, immutable plan | Shared JSON exact comparison, read-only list assertion, reversed Unity request order |
| Correct shared and distinct material references | Alpha's two parts use M_Shared; Zed uses M_Distinct; wrong-material batch rejected |
| No cross-item missing mesh/material/script references | Exact source binding rejection, saved references checked by `VerifySaved` before and after clean import |
| Failure recovery and no overwrite (SEC-001 scope) | Late material/mesh failures roll back Alpha; collision preserves original GUID and saved assets |
| Invalid ownership / case collision | `CaseCollidingPrefabNamesFailWithoutRenamingItems`, duplicate requests, source mismatch and unknown envelope version |
| Existing single-item behavior | Full Unity product integration, existing static/rigged/animated generation and clean imports |

## Reuse and changed files

- Application: new `Items/ItemPrefabPlan.cs`, its tests and the shared JSON fixture.
- Unity: new `UnityItemPrefabGenerator.cs` and `UnityItemPrefabIntegration.cs`; extended existing
  `UnityPrefabGenerator.cs`, `UnityProductEditorIntegrationTests.cs`, `UnityCleanReimportIntegration.cs`.
- Scripts: product integration retains item package/reimport evidence; policy checks cover the new boundary; exact template/worker inventories include both new Editor files.
- Documentation: this evidence, plan, architecture, backlog and PB-0802 completion rollover evidence.

Reuse audit: searched PB-0801 ownership, PB-0802 aliases, PB-0611 prefab/mesh/import policies and
existing clean-import harness. Extended the existing generator rather than copying its mesh,
material or save behavior. Reset transforms delegate to `UnityPrefabHierarchyUtility`. Unity's
small DTO mirrors the application envelope because its editor assembly is separate from the
.NET application; the shared golden JSON prevents drift. Test fixture material creation is
deliberately fixture-only and makes no new production material policy. Existing AGENTS and
engineering-skill status/fresh-main instructions already apply; no rule change was needed.

## Validation

- Five PB-0803 application tests passed, with no failures or skips.
- Unity policy checks: 42 passed; template inventory: 8 passed; Editor worker checks: 9 passed.
- Full Unity 6000.3.10f1 product integration passed. Item generation, negative cases, isolated
  package import and saved-reference validation passed; existing static, rigged, animated,
  Play mode and populated-project reopen checks also passed.
- Retained Unity evidence: `artifacts/u/beddf14e`; `item-reimport.json` reports `passed: true`
  with zero findings. The project and prefab paths are recorded in
  `artifacts/PB-0803/unity-result-pointer.json`. All generated files remain ignored.
- Full `Invoke-CoreCI.ps1`: all nine stages passed in 6 minutes 16 seconds; 34 baseline checks,
  locked restore, Release build with zero warnings/errors, .NET formatting and Ruff lint/format.
  All seven test projects passed: **2,410 passed, 0 failed, 0 skipped**, including the five task tests.
  Evidence: `artifacts/PB-0803/core-ci.log`, `core-ci-transcript.log` and `test-summary.json`.
- Public-file/secret safeguards, final whitespace checks and backlog accounting pass.
- Current accounting: 264 total; 115 DONE; 1 IN PROGRESS; 0 BLOCKED; 148 BACKLOG;
  149 remaining; 43.6% complete. Active Work and the Completion Log match this state.
- No implementation blocker remains. Publication, green main CI and user confirmation remain
  lifecycle gates; this is locally validated work, not a published task.

Optional visual inspection: open the project recorded in
`artifacts/PB-0803/unity-result-pointer.json`, then inspect `Assets/PBItemTests/Prefabs`.
`P_Alpha` has two reset parts under `P_Model`, both with the shared blue material; `P_Zed`
has its own red material. The fixture intentionally repeats the same model, so Alpha's parts
overlap at the reset origin; assembly positioning belongs to PB-0804. Meshes and materials should
have no missing references. This supplements the automated saved-reference checks.

## Review and publication

The user explicitly authorized commit, push and merge on 2026-09-10. This is the prepublication evidence snapshot; final task/merge SHAs and main CI will be recorded during the next-task rollover. PB-0802 is recorded DONE once,
with its exact successful main CI and user confirmation. PB-0803 remains the sole active task.
Suggested commit: `feat(PB-0803): generate separate Unity item prefabs`.

Repeat validation from `C:\Dev\PackageBuilder`:

```powershell
. ./scripts/Enter-PackageBuilderEnvironment.ps1
dotnet test tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj --filter 'Task=PB-0803'
& ./scripts/Invoke-UnityProductIntegration.ps1 -ResultPointerPath artifacts/PB-0803/unity-result-pointer.json
& ./scripts/Invoke-CoreCI.ps1
& ./scripts/Update-BacklogStatus.ps1
git diff --check
```

After review and explicit publication authorization, manual Git commands:

```powershell
git add -- src/PackageBuilder.Application/Items/ItemPrefabPlan.cs tests/PackageBuilder.Application.Tests/Items/ItemPrefabPlanTests.cs tests/fixtures/manifests/item-prefab-ownership.json engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityItemPrefabGenerator.cs engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityItemPrefabIntegration.cs engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityPrefabGenerator.cs engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityProductEditorIntegrationTests.cs engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityCleanReimportIntegration.cs scripts/Invoke-UnityProductIntegration.ps1 scripts/Test-UnityProductPolicies.ps1 scripts/Test-UnityProjectTemplate.ps1 scripts/Test-UnityWorkerPackage.ps1 docs/PB-0803_UNITY_ITEM_PREFAB_EVIDENCE.md docs/PB-0802_SHARED_ASSET_DEDUPLICATION_EVIDENCE.md docs/IMPLEMENTATION_BACKLOG.md docs/Package_Builder_Plan.md docs/TECH_STACK_AND_ARCHITECTURE.md
git diff --cached --check
git commit -m "feat(PB-0803): generate separate Unity item prefabs"
git push -u origin feat/PB-0803-unity-item-prefabs
```

Merge/main push require their own user authorization; successful main CI and confirmation precede
the DONE rollover into PB-0804.
