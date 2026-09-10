# PB-0808–PB-0810 — Item selector, portable inventory and equipment fixture

Current lifecycle: all three tasks remain 🟡 **IN PROGRESS**, unchecked, after successful local
implementation and validation; publication gates remain. No task commit, push or merge
has been performed for this scope.

Approved combined branch: `feat/PB-0808-PB-0810-selector-portable-e2e`.
Base: `ee5108fe3ee66663fad717595cbbc419da3744e0`, verified clean and equal to
freshly fetched and fast-forward-pulled main before branch creation. The user approved
these three tasks together on 2026-09-10. PB-0805–PB-0807 completion was rolled over
once with its existing task commit, main merge, successful main CI and user acceptance.

## PB-0808 — Preview selection

`PreviewSelectionPolicy` is the canonical engine-free index policy, compiled verbatim
into Domain and the customer Unity runtime. Domain retains typed IDs and immutable
state; Unity adapts scene-instance visibility. The template owns the physical source
so exported runtime code does not need the .NET 10 application or Editor worker.
Its block namespace and explicit argument exceptions support Unity C# 9/.NET Standard;
the scoped CA1512 exception is a runtime-API constraint, not a coverage exclusion.

Multi-item overview composition serializes references in declaration order. Previous
and Next wrap, direct selection accepts only valid indices, and All items restores
the overview. Labels show the current name and one-based index/count. Empty/single
states are safe. The scrollable direct picker and named buttons share the same state
operations as keyboard input. Tab/Shift+Tab uses the controller's existing focus cycle;
Enter/Space activates buttons, arrows select items, and Home/End select boundaries.
Focused buttons have a visible highlight. Scrolling over the overlay is reserved for
the picker. Hidden controls do not consume focused item commands.

Only scene root visibility changes. Prefab bytes, GUID references and layout transforms
remain unchanged. Camera framing uses active renderers, so Reset View frames the visible
selection. Animation transport remains its existing separate adapter; selecting an
item does not create an animation binding that the product did not declare.

## PB-0809 — Portable archives and inventories

`PortableMultiItemPlan` consumes a reviewed typed manifest and validated normalized
artifact bindings. It requires exact declared item/shared IDs, one FBX per item,
compatible shared asset purposes, safe canonical names and unique artifact identities.
Case-insensitive filename collisions, missing/extra/duplicate bindings and draft
ownership are rejected. Existing `PortableCompositionArtifact` enforces lifecycle,
target, role and format qualifiers.

Shared aliases already bound to the same canonical artifact record emit one file and
retain all declared IDs in the inventory. Alias content identity, texture role and
extension must agree. This honors upstream reuse decisions; it does not introduce
another material or texture deduplicator.

The generated archive contains one README and one `INVENTORY.md`, each bound to its
exact UTF-8 hash before layout creation. Inventory rows preserve item declaration
order and report names, shared references, byte counts and SHA-256 identities. Archive
entries are sorted ordinally. The existing `PortableFbxArchiveBuilder` supplies fixed
ZIP metadata, streamed hashing, exact source checks, cancellation and failed-output
cleanup. The existing archive validator is exposed for both single and multi-item
layouts; no second ZIP writer or archive integrity algorithm was introduced.

Materials carried by the FBX remain there, with external shared files alongside the
models. The archive planner does not normalize geometry or rewrite FBX references;
normalization supplies those validated inputs and actual clean reimport verifies them.

## PB-0810 — One equipment fixture across both targets

`tests/fixtures/portable/equipment-set/manifest.json` declares Helmet then Armour,
head/body slots, and one shared steel texture. Original CC0 procedural FBX/PNG source
fixtures are committed as stable test inputs. The Blender generator is retained for
reproducibility of geometry; FBX metadata is not claimed byte-identical on regeneration.

`EquipmentSetEndToEndTests` produces the portable archive and existing Application
ownership, assembly and attachment envelopes from that one manifest. The Unity harness
uses those envelopes with real model import, mesh extraction, item generation,
attachment validation, nested set assembly, overview composition and exact package
export. It then imports the package into a separate clean project before installing
the Editor validation worker. Customer runtime scripts travel with the package.

The same harness extracts only the portable ZIP's exact bounded inventory and imports
each FBX into a fresh Blender scene, verifying geometry, materials and resolved images.
Unity checks the individual prefabs, assembled prefab, shared material/texture,
declaration order, selector state and references after clean import. This is a primitive
packaging fixture, not a character-fitting or artistic-quality assessment. The broader
twelve-item collection end-to-end matrix remains PB-0811.

## Criterion-to-test traceability

| Task / acceptance criterion | Automated evidence |
| --- | --- |
| PB-0808: shared state, wrapping, boundaries, empty/single | Existing `PreviewExperienceContractTests`, new `PreviewSelectionPolicyTests`, Unity `VerifySelector` |
| PB-0808: pointer Previous/Next/direct/overview | `UnitySelectorInteractionTests` runs real IMGUI mouse/key events on Editor updates after clean equipment import |
| PB-0808: keyboard, name/index/order, only selected visible | `UnityMultiItemIntegration.VerifySelector`, named keyboard routing and controller focus integration |
| PB-0808: no prefab mutation, survives import | Prefab byte/transform comparisons and selector checks in clean collection/equipment projects |
| PB-0809: exact names, items, shared assets and documents | `PortableMultiItemTests.SetAndCollectionArchiveHaveExactStableInventoryAndDocuments` |
| PB-0809: reuse, invalid inputs, collision, stale inventory | Alias, draft, case collision, duplicate binding and document-record tests |
| PB-0809: deterministic bytes, cancellation, tamper | Repeat archive comparison and cancellation/tamper/missing-source tests; existing archive regression suite |
| PB-0810: single reviewed fixture feeds both targets | `EquipmentSetEndToEndTests.EquipmentFixtureBuildsPortableArchiveAndUnityPlansFromOneManifest` |
| PB-0810: portable package actually reimports | `pb0810_equipment_fixture.py verify`, run by `Invoke-UnityProductIntegration.ps1` |
| PB-0810: Unity package actually reimports | `UnityEquipmentSetIntegration.Run/VerifySaved`, isolated `equipment-set` validation mode |

## Validation and handoff

Validated locally on 2026-09-10:

- Full `Invoke-CoreCI.ps1`: **9/9 stages passed**, 34 repository checks passed,
  Release build with **0 warnings / 0 errors**, .NET formatting and Ruff checks passed.
- Full .NET suite: **2,446 passed, 0 failed, 0 skipped** across all seven test projects.
  Includes 10 new shared-selector cases and 8 portable/equipment cases.
- Final Unity integration: **passed**, run root `artifacts/u/11115fda`.
  Product generation/export, static/item-set/collection/equipment/rig isolated imports,
  pointer/keyboard UI, Play Mode and populated-project reopen all passed.
- Every clean Unity report has `passed: true` and **zero findings**.
- Portable equipment ZIP clean Blender import passed with Helmet (62 vertices), Armour
  (8 vertices), materials and the shared texture. The stricter supplemental run at
  `artifacts/u/11115fda/equipment-contained-reimport.json` also proves
  every imported image resolves inside the extracted archive directory.

Retained core evidence: `artifacts/PB-0808-PB-0810/core-ci.log`,
`core-ci-complete-transcript.log` and `test-summary.json` in that same scope directory.
Engine evidence: `artifacts/PB-0808-PB-0810/unity-result-pointer.json`,
`unity-complete-transcript.log`, and the reports/logs beneath `artifacts/u/11115fda`.
The clean equipment and collection projects were `artifacts/u/11115fda/e` and
`artifacts/u/11115fda/c`. Generated projects/packages are disposable; the cleanup
receipt records their removal and the pointer's `artifactsRetained` is false.

The UI test uses a normal Editor process because batch mode does not render IMGUI.
It waits for the Editor event loop, sends actual pointer/key events, checks their effects
on later updates, has a 30-second test deadline, restores the overview and exits Unity.
Earlier failed attempt logs remain as diagnostic evidence only: one fixture
assertion initially rejected the package's own root; an initial synchronous/batch UI
harness could not render controls; the fixture inventory needed its three explicit,
size-bounded, hash-checked CC0 binary source entries. Final passing runs include the fixes.

No implementation blockers remain. Commit, push, merge, main CI and user completion
confirmation are outstanding publication gates. Active Work reflects local validation;
PB-0805–PB-0807 Completion Log rollover is recorded once, while these three tasks remain
unchecked/IN PROGRESS. Summary: 120 DONE, 3 IN PROGRESS, 0 BLOCKED, 141 BACKLOG;
144 remaining out of 264 (45.5% complete).

Reuse audit: existing preview contract/focus controller, normalized artifact qualifier,
naming profile, streaming archive writer/validator, Application item/reuse/assembly
plans, Unity model/mesh/prefab generators, overview composer, exporter and clean-import
harness were inspected and extended. The item fixture import helper is shared by both
old and equipment tests. No new runtime/editor dependency crosses the customer package
boundary. Engine API adaptation and fixture assertions remain engine-specific.

Suggested commit: `feat: add multi-item selector and portable equipment packaging`.
Publication remains a separate user-authorized step; all three task statuses and the
backlog summary must remain synchronized through the existing one-merge rollover.

## Test artifact cleanup follow-up (2026-09-10)

The user requested that disposable test packages be deleted after testing. AGENTS.md,
the engineering skill and the quality gates now require cleanup after success/failure,
with source fixtures, intended releases, shared tools/caches and compact evidence preserved.

Product, multi-clip, topology and Silverwing integration scripts share
`UnityTestArtifacts.Common.ps1` inside outer `finally` blocks. The helper removes only
known payload names beneath a selected `artifacts/u/<8-hex-id>` directory, checks resolved
containment, refuses reparse points and active engine processes, copies nested diagnostic
reports out, verifies deletion and writes `cleanup-result.json`. Unrelated run entries stay.
Result pointers retain historical generation paths and explicitly report `artifactsRetained`.

`-KeepArtifacts` retains a successful run for a specific manual inspection; failures still
clean up. The static vertical-slice composer keeps its child outputs only until composition
finishes and then cleans the temporary Unity run. Its intended promoted release remains.
Other legacy/manual harnesses are covered by the standing operator cleanup rule and must
have disposable outputs cleaned before handoff; they have not all been retrofitted here.

Focused validation: `Test-UnityTestArtifacts.ps1` passes 8 checks covering invalid roots,
retention, active Hub process refusal, junction refusal before any deletion, cleanup after failure, compact evidence and
unrelated-file preservation, idempotence, and executing all four actual harness `finally`
blocks for success/failure retention. The repository baseline now runs these checks.
`Test-UnityProductPolicies.ps1` passes 44 checks; the repository baseline passes 35 checks
with no failures. `git diff --check` and backlog accounting pass.
Logs: `artifacts/PB-0808-PB-0810/cleanup-tests.log`, `cleanup-unity-policies.log`,
and `cleanup-repository-baseline.log`. Earlier full .NET/engine results above predate this
PowerShell/documentation follow-up; those engine/package implementations were unchanged here.
The skill's unchanged frontmatter and new cleanup reference were manually checked; the
optional Python quick validator could not start because the local Blender Python lacks PyYAML.

Current-scope runs `11115fda`, `f4d7c422`, `eca4f8bb` and `5fb84d0f` were cleaned and verified
to contain no generated project directories or packages. Receipts account for at least
11,187,448,327 bytes removed; the partial deletion preceding the resolved Hub lock is excluded
from that lower bound. Logs, inventories and reports remain, including the portable reimport
and containment reports copied to the run root. See `cleanup-summary.json` in the scope evidence
directory and each run's `cleanup-result.json`. That initial cleanup covered only these four runs;
the user's subsequent request extends cleanup to prior tasks as recorded in
[the retrospective audit](TEST_ARTIFACT_CLEANUP.md).

That retrospective follow-up cleaned 63 older Unity runs and 19 legacy test locations, removing
an additional 164.4 GB of generated files. Its 8 cleanup checks, 35 repository checks and skill
validation passed. The audit records preserved release/source exceptions and the permanent
past/current/future task rule; historical DONE statuses and test results remain unchanged.

One failed run had a Hub instance launched by the earlier selector UI test holding its empty
project directory open. That specifically identified test process was closed and cleanup retried
successfully. The shared process guard now includes Unity Hub; all 8 focused checks were rerun
after that guard change. The 35-check baseline pass preceded this final guard refinement.

Reuse audit: existing test workspace disposal and Unity clean-import lifecycle were inspected.
The four harnesses now share one cleanup implementation; release composition defers cleanup
until its existing consumer finishes. No application packaging or domain policy was duplicated.

## Manual publication commands

After reviewing local validation, the user can publish this existing branch with:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git add -A
git commit -m "feat: add multi-item selector and portable equipment packaging"
git push -u origin feat/PB-0808-PB-0810-selector-portable-e2e
```

Merge and main CI are subsequent publication gates. Keep PB-0808, PB-0809 and PB-0810
unchecked until successful main CI and user confirmation allow their next-branch rollover.

## Changed files

- `.editorconfig`
- `AGENTS.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/Package_Builder_Plan.md`
- `docs/PB-0805_PB-0807_MULTI_ITEM_EVIDENCE.md`
- `docs/PB-0808_PB-0810_SELECTOR_PORTABLE_EVIDENCE.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `docs/TEST_ARTIFACT_CLEANUP.md`
- `engine-templates/unity/6000.3/Assets/PackageBuilder/Preview/PackageBuilderItemSelector.cs`
- `engine-templates/unity/6000.3/Assets/PackageBuilder/Preview/PackageBuilderItemSelector.cs.meta`
- `engine-templates/unity/6000.3/Assets/PackageBuilder/Preview/PackageBuilderPreviewController.cs`
- `engine-templates/unity/6000.3/Assets/PackageBuilder/Preview/PreviewSelectionPolicy.cs`
- `engine-templates/unity/6000.3/Assets/PackageBuilder/Preview/PreviewSelectionPolicy.cs.meta`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityCleanReimportIntegration.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityEquipmentSetIntegration.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityItemPrefabIntegration.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityMultiItemIntegration.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityOverviewScenePipeline.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityProductEditorIntegrationTests.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnitySelectorInteractionTests.cs`
- `scripts/Invoke-UnityProductIntegration.ps1`
- `scripts/Invoke-UnityMultiClipIntegration.ps1`
- `scripts/Invoke-UnitySilverwingIntegration.ps1`
- `scripts/Invoke-UnityStaticVerticalSlice.ps1`
- `scripts/Invoke-UnityTopologyMatrixIntegration.ps1`
- `scripts/UnityTestArtifacts.Common.ps1`
- `scripts/Test-UnityTestArtifacts.ps1`
- `scripts/TaskBranchPolicy.Common.ps1`
- `scripts/Test-RepositoryBaseline.ps1`
- `scripts/Test-UnityProjectTemplate.ps1`
- `scripts/Test-UnityWorkerPackage.ps1`
- `skills/package-builder-engineering/SKILL.md`
- `src/PackageBuilder.Domain/PackageBuilder.Domain.csproj`
- `src/PackageBuilder.Domain/Preview/PreviewExperienceDefaults.cs`
- `src/PackageBuilder.Domain/Preview/PreviewItemSelectionState.cs`
- `src/PackageBuilder.Targets.Portable/PortableMultiItemPlan.cs`
- `src/PackageBuilder.Targets.Portable/PortableTargetValidator.cs`
- `tests/blender/engine/pb0810_equipment_fixture.py`
- `tests/fixtures/portable/equipment-set/clean-reimport-evidence.json`
- `tests/fixtures/portable/equipment-set/manifest.json`
- `tests/fixtures/portable/equipment-set/README.md`
- `tests/fixtures/portable/equipment-set/source/Armour.fbx`
- `tests/fixtures/portable/equipment-set/source/Helmet.fbx`
- `tests/fixtures/portable/equipment-set/source/T_SharedSteel_Albedo.png`
- `tests/PackageBuilder.Domain.Tests/Preview/PreviewSelectionPolicyTests.cs`
- `tests/PackageBuilder.Targets.Portable.Tests/PortableMultiItemTests.cs`
