# PB-0811 — Twelve-item collection end-to-end

Status: 🟡 **IN PROGRESS**. Implemented and validated locally; commit, push, merge,
successful main CI and completion confirmation remain publication gates.

Branch: `test/PB-0811-item-collection-e2e`, created from clean synchronized main
`55c4a0213ec20b347bf319804448d9e14a27ad48` after fetch and fast-forward pull on 2026-09-10.

PB-0808, PB-0809 and PB-0810 completion was recorded once at this branch's start.
Their task commit was `4beaa9507205e1937895152415c42ccc6eeecc06`, merged/pushed as
the base above; [required main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34502635125)
passed both jobs, including 2446 tests. The user accepted the publication handoff and
explicitly requested this successor task. No completion-only publication was created.

## Fixture and reuse

`tests/fixtures/portable/twelve-item-collection` contains twelve small original CC0
column models, one shared texture, a reviewed manifest, and independent triangle-count
expectations. Geometry varies by item; declaration order differs from filename order,
making accidental sorting or cross-item geometry substitution observable.

The existing portable fixture binder now accepts an alternate fixture directory.
The test reuses `SharedAssetDeduplicator`, `ItemPrefabPlan`, `MultiItemBuildPlan`,
`PortableMultiItemPlan`, and the existing streaming archive builder/validator. Unity
reuses model preparation, mesh extraction, prefab creation, collection export,
bounds-aware overview composition, selector conformance and isolated import helpers.
No production packaging or selector policy is duplicated or changed. The Blender and
Unity geometry assertions are intentionally independent engine-specific oracles.

## Acceptance traceability

Owner: Package Builder engineering. Source: backlog PB-0811 and product case 5.
All rows use the twelve-item collection fixture and evidence under `artifacts/PB-0811`
plus the run directory recorded by its result pointer.

| Acceptance criterion | Concrete automated test | Evidence |
| --- | --- | --- |
| Twelve unique prefab identities and correct item geometry | `UnityTwelveItemCollectionIntegration.VerifySaved` | Clean import report: per-item GUID, triangles, dimensions, material/texture counts |
| Declaration order, bounds-aware spacing and full camera framing | `VerifySaved`, existing `UnityOverviewSceneComposer.VerifyComposition` | Saved-scene checks, measured overview positions |
| Every item can be selected; others hidden; overview restored | `VerifySaved`, existing `UnityMultiItemIntegration.VerifySelector` | Clean Unity validation and prefab/transform immutability checks |
| Pointer/keyboard picker, wrap, boundaries and hidden controls | `UnitySelectorInteractionTests.Run` against the clean twelve-item project | `selector-ui.log` |
| Exact model/shared/document inventory, bytes, hashes and stable archives | `CollectionEndToEndTests.TwelveItemsHaveExactDeterministicArchiveInventoryAndUnityOwnership`, `pb0811_collection_fixture.py verify-unity` | Core test result, portable checks and actual Unity archive inventory |
| Matching dimensions, triangles, materials and textures across targets | Product harness per-item metrics comparison | Independent Blender/Unity measurements in the two reimport reports |
| Actual portable ZIP clean reimport and contained shared texture | `pb0811_collection_fixture.py verify` | `twelve-portable-reimport.json`, twelve per-item measurements |
| Actual Unity package import without worker/runtime-template dependencies | `Invoke-CleanUnityPackageValidation`, `UnityCleanReimportIntegration` twelve-item mode | `twelve-import.log`, `twelve-reimport.json` |
| Cleanup after success/failure, retain only compact evidence | `Test-UnityTestArtifacts.ps1` and product harness outer `finally` | Cleanup tests and run `cleanup-result.json` |

## Validation

Verified locally on 2026-09-10:

- Final repository baseline: **35 passed, 0 failed**, including all nine cleanup checks.
- Locked restore and Release build: **0 warnings / 0 errors**.
- Full seven-project .NET suite: **2447 passed, 0 failed, 0 skipped**. Evidence:
  `artifacts/test-results/PB-0811/summary.json` and `artifacts/PB-0811/full-suite.log`.
- .NET formatting, pinned Ruff 0.15.22 verification, lint and formatting passed.
  An initial collection-initialization style finding was corrected before the final run.
- Focused portable collection and existing archive tests: **8 passed**.
- Actual portable ZIP: **15 entries**, all twelve models cleanly reimported in Blender.
- Actual Unity archive: **57 asset records, 12 prefabs**, matching the exact export plan
  with no duplicate or foreign paths. The verifier reads records without extraction.

The integration run is `artifacts/u/76b6d8fc`; its full harness passed generation,
all clean imports, selector UI, Play Mode and populated-project reopening. The twelve-item
clean report has `passed: true`, no findings and twelve distinct prefab GUIDs. Every item's
dimensions agree between Blender and Unity within 0.0001 units in Y-up coordinates;
triangle/material/texture counts also match (456 triangles total). The actual UI test
passed pointer navigation, direct selection, keyboard controls and scrolling to the last
item from the all-items state. An intentionally incomplete archive inventory was rejected
with exit code 1 and no success report.

Retained evidence: `twelve-reimport.json`, `twelve-portable-reimport.json`,
`twelve-archive-verification.json`, `cross-target-metrics.json`, `selector-ui.log` and
`cleanup-result.json` in that run. `artifacts/PB-0811/unity-result-pointer.json` identifies
the run; `unity-transcript.log` records the complete harness.

Original generation ran before additive report fields and stricter archive/metric checks
were added. Those checks subsequently validated the same actual exported package with the
final clean-import validator and the final harness's metric-comparison block. No production
generator changed during validation. The post-test process-cleanup refinement passed nine
focused cleanup checks and the final 35-check repository baseline.

## Scope limits and cleanup

This completes the portable/Unity collection acceptance scope only; Unreal integration,
professional media generation and the general documentation engine retain their own tasks.
The fixture is deliberately simple original geometry, not a claim about artistic quality.
Default tests need no external engines; the separately invoked integration needs the
already approved local Blender and Unity installations.

Generated packages, extraction directories and Unity projects are disposable even on
failure. The shared cleanup helper now recognizes this fixture's `twelve`, `twelve-import`
and `v` paths and preserves its portable report. Source goldens, compact logs, measurements
and cleanup receipts remain. No permanent manual-retention exception is requested.

Cleanup completed: **11,184,592,470 bytes (11.18 GB)** of disposable payloads removed.
The run has no remaining directories or generated packages; its compact reports remain.
This is summed file length from the cleanup receipt, not a physical free-space measurement.

The normal Editor UI test launched a Hub process that survived the Editor. The exact owned
Hub tree was closed before cleanup. Future product runs use `Stop-CompletedUnityTestHub`:
it requires an exited parent Editor, an exact quoted contained project path and matching
parent PID; only that Hub's descendants are stopped. Other projects, other parents, active
Editors, invalid roots and reparse points are protected. Nine cleanup checks cover those
boundaries as well as success/failure disposal, retention, evidence and idempotence.

## Publication handoff

Suggested commit: `test: validate twelve-item collection end to end`.

After reviewing final local evidence, the user can publish with:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git add -A
git commit -m "test: validate twelve-item collection end to end"
git push -u origin test/PB-0811-item-collection-e2e
```

Merge and successful main CI follow under separate user authorization. PB-0811 remains
unchecked/IN PROGRESS through publication; its completion is recorded at the next task
branch after the required gates and user confirmation.


## Changed files

- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/Package_Builder_Plan.md`
- `docs/PB-0808_PB-0810_SELECTOR_PORTABLE_EVIDENCE.md`
- `docs/PB-0811_TWELVE_ITEM_COLLECTION_EVIDENCE.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `docs/TEST_ARTIFACT_CLEANUP.md`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityCleanReimportIntegration.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityProductEditorIntegrationTests.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnitySelectorInteractionTests.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityTwelveItemCollectionIntegration.cs`
- `scripts/Invoke-UnityProductIntegration.ps1`
- `scripts/Test-RepositoryBaseline.ps1`
- `scripts/Test-UnityProjectTemplate.ps1`
- `scripts/Test-UnityTestArtifacts.ps1`
- `scripts/Test-UnityWorkerPackage.ps1`
- `scripts/UnityTestArtifacts.Common.ps1`
- `tests/blender/engine/pb0811_collection_fixture.py`
- `tests/fixtures/portable/twelve-item-collection/clean-reimport-evidence.json`
- `tests/fixtures/portable/twelve-item-collection/expectations.json`
- `tests/fixtures/portable/twelve-item-collection/manifest.json`
- `tests/fixtures/portable/twelve-item-collection/README.md`
- `tests/fixtures/portable/twelve-item-collection/source/Column01.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column02.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column03.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column04.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column05.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column06.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column07.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column08.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column09.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column10.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column11.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/Column12.fbx`
- `tests/fixtures/portable/twelve-item-collection/source/T_SharedStone_Albedo.png`
- `tests/PackageBuilder.Targets.Portable.Tests/CollectionEndToEndTests.cs`
- `tests/PackageBuilder.Targets.Portable.Tests/PortableMultiItemTests.cs`

## Completion rollover — 2026-09-10

PR #92 merged task commit f3f37bf79f10f2603b717d30d6a4f2713e666acb into main
as 323e915223bf301b806ec220a42effd01cec150d. Required main CI run 34520660383
succeeded; the user confirmed the merge and requested PB-0901–PB-0903. PB-0811 is
recorded DONE once at that branch start. Publication-pending notes above are historical.
