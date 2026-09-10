# PB-0804 — Assembled Unity item-set prefab

Status: 🟡 **IN PROGRESS**. Branch: `feat/PB-0804-unity-assembled-set`.
Base: `e540ab270eb5f0c1c14c8391890d333631df5235`, clean and equal to origin/main after
fetch, main checkout and fast-forward pull on 2026-09-10.

## Contract and scope

`AssembledSetPlan.Create` consumes the successful PB-0803 item-prefab plan. It requires a nonempty
item set with explicit PB-0106 assembled-set rules and complete prefab membership. Existing Domain
validation owns membership completeness, matching item/member slots and optional slot uniqueness.
Collections and sets with missing assembly declarations return `SET_ASSEMBLY_DECLARATION_REQUIRED`;
they never infer a runtime assembly. Missing prefab membership returns `SET_ASSEMBLY_ITEM_PREFAB_MISSING`.

The planner uses `ItemSetDefinition.Items` order, not the ordinal ordering of either prefab plans
or assembled membership lookup records. Each member retains its original ID, prefab filename and
logical slot. Slotted items use `Slot_<slot>_<itemId>` containers; unslotted items use `Item_<itemId>`.
Repeated slots remain separate when the Domain rules permit them. The original item/reuse plan and
manifest are unchanged. No schema or Domain rule changes are introduced.

The application emits the version-1 assembly contract with ordered members and compatibility
entries. Unity consumes that contract plus exact generated item-prefab paths. It produces:

- `Prefabs/P_<setId>_Assembled.prefab`, with reset root and ordered reset member containers.
- One reset nested instance of the existing item prefab beneath each member container.
- `Documentation/SET_<setId>.json`, preserving the exact contract, slots and compatibility metadata.

Nested source references, descendants, meshes and materials are preserved; assembly never rebuilds
or combines meshes/materials. Existing outputs, including unimported document files, are rejected.
The document uses exclusive new-file creation. Failures clean owned outputs; source prefabs remain
unchanged. Shared package hierarchy validation now detects missing scripts throughout descendants
and missing MeshFilter/SkinnedMeshRenderer mesh references, and is reused before/after composition.

**Placement is explicitly `logical-slots-at-origin`; attachment validation is `not-performed`.**
The existing manifest has no per-item placement transforms, socket mapping or target skeleton.
This task groups the parts at their authored/reset origin and documents compatibility claims; it
does not validate those claims, retarget skin, place objects onto bones or infer attachment points.
Actual target/socket/bone validation belongs to PB-0805. Collection packaging remains PB-0806.
No new desktop flow, runtime script, online service, paid dependency or licensing change is added.
The existing pinned Unity Editor and its licensing/eligibility requirements still apply.

## Acceptance-to-tests matrix

Owner: PB-0804. Application tests: `AssembledSetPlanTests.cs`; shared fixtures:
`tests/fixtures/manifests/assembled-set-manifest.json` and `assembled-set-plan.json`.
Engine acceptance: `UnityAssembledSetIntegration.Run` and `VerifySaved`, included in the existing
product integration and isolated `item-and-set-prefabs` clean reimport mode.

| Criterion / requirement | Concrete automated evidence |
| --- | --- |
| Declared order instead of ID sorting | `DeclaredOrderOverridesCanonicalPrefabAndMemberOrder`, reversed Unity input path order, saved Zed-before-Alpha hierarchy |
| Declared slots and compatibility | Shared exact JSON comparison; `OptionalSlotsAndCompatibilityRemainExplicit`; `RepeatedLogicalSlotsKeepUniqueMemberContainersWhenPermitted`; real saved document checks |
| Reset set root and member placement | `VerifySaved` checks root, member containers and nested item transforms before and after clean import |
| Single-item boundary and collision-free naming | `SingleItemSetHasADistinctAssemblyFilename` |
| Complete valid inputs without guessed assembly | Missing declaration/collection tests; null test; duplicate/missing Unity bindings; unknown version and traversal rejection |
| No broken dependencies | Existing shared validator rejects a deliberately broken descendant mesh; exact nested source paths and saved references verified |
| No overwrite or source mutation | Unimported document collision, existing set GUID preservation, byte-identical original item prefab files and unchanged GUIDs |
| Clean customer import and regression | Item/set package imports without worker code; validation dependencies are then supplied by the harness; PB-0803 item checks plus existing static/rigged/animated, Play mode and reopen suite |

These are task-specific correctness checks. They do not claim broad coverage, mutation, performance,
UI or public-release certification beyond the repository's existing measured gates.

## Reuse audit and files

Searched and reused `AssembledSetRules`, `ItemSetDefinition`, `AssembledSetMember`, logical
`AttachmentSlot`, compatibility entries, PB-0803 prefab plans/generation and existing Unity
hierarchy, package validation and clean-import harnesses. Canonical membership and slot rules stay
in Domain; ordering and document intent stay in Application; nested prefab operations stay in
Unity. Small Unity DTOs mirror the application contract because the Unity Editor assembly cannot
reference the .NET 10 application; the shared golden fixture checks this intentional duplication.

Changed scope: new Application planner/tests, two shared JSON fixtures, Unity assembly generator
and acceptance fixture; shared package validator, item/product/clean-import integration; product
integration script and exact template/worker/policy inventories. Documentation updates include
this evidence, plan, architecture, backlog and the verified PB-0803 completion rollover. Existing
AGENTS and engineering-skill fresh-main/status guidance already covers this task.

## Validation and status

Seven focused application tests pass. Full core CI passed all nine stages in 10 minutes 12 seconds: 34 repository checks, locked restore, Release build with zero warnings/errors, .NET formatting, Ruff lint/format and 2,417 tests with zero failures/skips. Evidence: `artifacts/PB-0804/core-ci.log`, `core-ci-transcript.log` and `test-summary.json`. Real Unity generation and isolated item/set clean reimport pass with zero findings in `artifacts/u/ee6df4c0/item-reimport.json`. Full Unity 6000.3.10f1 product integration passed, including existing static/rigged/animated generation, clean imports, Play mode and populated-project reopen. Unity policy checks: 43 passed. Retained project `artifacts/u/ee6df4c0/p`; exact project/prefab/document paths are in `artifacts/PB-0804/unity-result-pointer.json`. Final backlog accounting, PowerShell parsing and whitespace checks pass; generated artifacts remain ignored.
PB-0803 is recorded DONE once with task commit `1356180`, main merge `e540ab2`, successful
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34475266254) and the
user's acceptance of publication/request to start PB-0804.

Current backlog: 264 total; 116 DONE; 1 IN PROGRESS; 0 BLOCKED; 147 BACKLOG;
148 remaining; 43.9% complete. PB-0804 remains unchecked until publication, green main CI,
user confirmation and completion rollover. No implementation blocker is currently known. The Unity harness now waits on the Editor process itself instead of waiting on persistent compiler descendants; this resolved a wait after a successful Editor exit. The shared clean-import helper and Play mode/reopen paths use the same direct process wait.

Optional visual inspection: use the retained project path in
`artifacts/PB-0804/unity-result-pointer.json`; open `Assets/PBSetTests/Prefabs/P_ExampleSet_Assembled.prefab`.
Verify `Slot_head_Zed` appears before `Slot_body_Alpha`, their nested prefabs retain the red/blue
materials, and `Assets/PBSetTests/Documentation/SET_ExampleSet.json` contains Generation=One.
The repeated model fixture intentionally overlaps at the origin; this is not socket placement.

## Manual handoff

Suggested commit: `feat(PB-0804): assemble ordered Unity item sets`.
The user explicitly authorized commit, push and merge on 2026-09-10. This is the prepublication evidence snapshot; final task/merge SHAs and main CI are recorded during the next-task completion rollover.
From `C:\Dev\PackageBuilder`:

```powershell
. ./scripts/Enter-PackageBuilderEnvironment.ps1
dotnet test tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj --filter 'Task=PB-0804'
& ./scripts/Invoke-UnityProductIntegration.ps1 -ResultPointerPath artifacts/PB-0804/unity-result-pointer.json
& ./scripts/Invoke-CoreCI.ps1
& ./scripts/Update-BacklogStatus.ps1
git diff --check
```

After explicit publication authorization, stage only the reviewed PB-0804 files, then:

```powershell
git diff --cached --check
git commit -m "feat(PB-0804): assemble ordered Unity item sets"
git push -u origin feat/PB-0804-unity-assembled-set
```

Merging and pushing main require their own explicit authorization. Record final completion only
after main CI and user confirmation through the next-task rollover.
