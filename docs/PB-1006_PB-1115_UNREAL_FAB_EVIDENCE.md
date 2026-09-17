# PB-1006 / PB-1115 — Fab Unreal validation and static release

## Scope and lifecycle

The user approved both tasks on `feat/PB-1006-PB-1115-unreal-fab-e2e`, created after
fetch/fast-forward synchronization and clean equality with origin/main at
`bfc03c6a6c974bc8ab7f3be74ae317c7c07f04fb` on 2026-09-17. PB-1113/PB-1114/PB-1116
rolled over once using their published commit, successful main CI and user acceptance.
The current two tasks remain unchecked and IN PROGRESS until publication and acceptance.

## Implementation and reuse

- `FabUnrealProjectValidator` checks the complete content-only static project observation,
  required descriptor/config/Pack/map/docs, safe naming, duplicate/unplanned paths, unused or
  redirector assets and uncompressed size. Native findings remain bound to exact ZIP/job/product.
- `UnrealContentDeliveryPolicy` was extracted from the target writer into Contracts and is
  reused by the writer and Fab validator. No engine API or filesystem dependency crosses into it.
- `FabReleaseComposer` uses the existing profile updater, target evidence and verified archive
  writer. Selected Unreal ZIPs require matching inspection and pinned engine versions;
  missing/unselected/stale outputs fail before archive writing.
- Listing metadata now includes selected Unreal versions and the manual Project File Link step.
- The existing Fab and Unreal live harnesses are composed together. They use the same original
  StoneArch static FBX, verify the common original source identity and Unreal normalized-input
  identity, and check every final
  archive entry. Unreal's native negative cases, fresh helper-free PIE and cleanup are retained.
- Native checks remain in the Unreal adapter; the Fab facade does not reimplement map/lighting,
  redirector repair, dependency traversal, import or engine diagnostics. Native log classification
  reuses the narrowly evidenced candidate-engine baseline notice from PB-1116.

No new third-party library, engine installation, runtime plugin or automatic upload is introduced.

## Requirements profile

Candidate `fab-2026-09-17.1` extends only static content-only Unreal support; old profile files
and approvals are unchanged. The exact candidate is cached, compatibility-tested and approved
only in an isolated test database before release composition. This does not change global engine
approval or the user's current requirements profile, and does not certify Fab editorial acceptance.
Rigged/animated products, code plugins and GLB remain outside the scoped static review.

Reviewed 2026-09-17: [Fab file and structure requirements](https://dev.epicgames.com/documentation/en-us/fab/asset-file-format-and-structure-requirements-in-fab).
The limit uses project bytes before compression; the current profile keeps the existing
15,000,000,000-byte boundary. Project organization and native validation stay separate checks.

## Acceptance mapping

| Task / criterion | Concrete verification |
| --- | --- |
| PB-1006: one project, matching Pack, safe organization/naming | `ExactNativeObservationPassesAndPinsProfile`, `UnplannedOrForbiddenPathsBlockEvenWhenDeclaredInInventory`, shared writer tests |
| PB-1006: descriptor, config, overview and documentation | `MissingRequiredComponentsBlock`; native `fresh-reopen` and `reject-missing-map` |
| PB-1006: redirectors and unused/generated content | `UnusedAssetsAndRedirectorsBlock`, native `validate_overview` package/reference/physical inventory gates |
| PB-1006: logs, stale or missing native results | `StaleMissingAndFailedNativeGatesBlock`; independent native log scan and helper-free PIE |
| PB-1006: uncompressed project limit and cancellation | `LimitUsesUncompressedProjectSizeAndUnknownRulesBlock` |
| PB-1115: all selected outputs, exact profile/engine pins | `ComposerIncludesOnlyInspectedUnrealAndPinnedEngineMetadata`, `UnrealCandidateRequiresCompatibilityAndApprovalBeforeRelease` |
| PB-1115: one static source, portable reimport, clean Unity import | `Invoke-FabStaticReleaseIntegration.ps1 -IncludeUnreal`, `FabRealReleaseValidation` source-hash comparisons |
| PB-1115: actual Unreal render, ZIP and clean reopening | `unreal_delivery_integration.py --preview --fab`, `ValidateLiveOverviewGalleryWhenRequested`, `FabUnrealLiveObservation` |
| PB-1115: exact final release contents and cleanup | Final manifest hash loop, owned-run finally cleanup and post-validation process shutdown |

## Validation record

The final Release build passed with zero warnings/errors. Final focused Fab/Unreal tests passed
(248). The full .NET suite ran 2,837 tests: 2,836 initially passed;
`TotalTimeoutWinsWhileHeartbeatPreventsIdleTimeout` hit its existing 250 ms startup budget
under concurrent engine/test load. The entire Infrastructure suite was rerun after engines
exited: all 653 passed, including that test. No timing threshold was weakened. All 54 Unreal
Python tests passed. Repository baseline passed 36/36, including the 11 quality checks.
Final .NET and Ruff formatting verification passed. The .NET check uses the repository's
required `--exclude third_party`; an initial
over-broad read-only formatting scan included unchanged upstream vendor sources. The final
test-code formatting fixes only deconstruct/rename local variables and normalize line endings.

The first live run `f9dbcc6d4df74424b93e2fcddc467360` correctly failed on the original
FBX missing smoothing groups, despite the native worker returning success. The host log gate
blocked the release. Unreal now normalizes that immutable original through the existing Blender
FBX exporter (`mesh_smooth_type=FACE`) and records original and normalized hashes separately.
No warning suppression was added. The original Unity regression run passed; its owned
`artifacts/u/4ffbd106` outputs were cleaned (12,804,402,976 bytes). The static retry uses the
new `-StaticOnly` handoff, preserving the full harness default and automatic finally cleanup.

The corrected live run `b8ccb09ffc1541028e2feb01ef071b72` passed all 14 native operations on
Unreal 5.8.2 / worker 0.1.0, including fresh helper-free PIE, restored reopening and rejection
of missing preview/map assets, an absolute source path and an extra descriptor field.
The original source and all delivered file hashes stayed unchanged. The clean Unreal ZIP had
15 files. Five native 1920×1080 views passed measured image validation; the hero and interactive
controls screenshots were also visually inspected.

The final Fab release passed with no findings: portable FBX, Unity (18 inspected assets), Unreal,
documentation and five Unity gallery images, in a 15-entry final envelope. Native Unreal images
were separately validated; the final gallery does not contain ten images. Exact profile
`fab-2026-09-17.1` SHA-256:
`02a061c4337da00e22bde83ffec687a014677b5686ee3b9e982b486a8fed38ba`.
The final envelope was 1,525,348 bytes, SHA-256
`b8b241af9ae9f95eaa9ec84b0fcef04ce28ed1f2798f86fad02148478c6da75e`.
This is a small owned static integration fixture, not a claim about arbitrary customer assets.

Receipts and compact logs remain in ignored `artifacts/PB-1115/<run-id>`. Native cleanup
reported success and was independently checked; both native job roots and both portable run
roots are absent. Unity retry `b77c938b` removed 4,821,815,660 bytes and retains only compact
evidence. Its original source hash still matches
`9d3cc82c22918c15fa20c674b26df60d5b86a808bc7b1e9a1962eb33916fd9a9`.
The retry began before the parent evidence-root adjustment, so its summary was copied from
PB-1010 to PB-1115 and the saved historical PB-1010 summary restored. Future runs use PB-1115
directly. No publication or main CI is claimed for this branch.

After the final validation command, ownership inspection identified 15 contained MSBuild
nodes and one compiler server from this task's recorded builds. `dotnet build-server shutdown`
reported successful MSBuild/compiler shutdown. A fresh process scan found zero remaining
project-tool, Unity, Unreal, Blender, shader-compiler or crash-reporter processes. No unrelated
user process was stopped. See `artifacts/PB-1115-shutdown.log` and process inventories.

Automatic approval review rejected an optional extra normalization smoke/temporary-cleanup
command as "blocked by policy" before execution. Nothing ran and no folder was created by
that command; it was not retried or bypassed. The maintained integration harness above
independently validated normalization and performed its normal owned-run cleanup.

## Repeat locally

```powershell
. .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet build PackageBuilder.sln -c Release --no-restore
dotnet test tests/PackageBuilder.App.Wpf.Tests -c Release --no-build --filter 'FullyQualifiedName~Fab|FullyQualifiedName~UnrealDelivery'
& .\scripts\Invoke-FabStaticReleaseIntegration.ps1 -IncludeUnreal
dotnet build-server shutdown
```

Use the signed user-approved engine installations. Generated packages and engine projects
are disposable; compact logs/receipts are retained under ignored artifacts. Previously denied
cleanup exceptions documented in `TEST_ARTIFACT_CLEANUP.md` are not retried.

## Review and publication handoff

Changed implementation areas are Contracts (shared Unreal delivery layout), the Fab adapter
(validator, profile, target resolution, listing and composition), the Unreal archive writer,
native acceptance scripts and WPF integration tests. The plan, architecture, quality traceability,
backlog, engineering skill and branch-policy checks document the approved combined scope.
The extraction of the shared layout removes duplicated policy; native observations continue
through existing adapters. No intentional business-rule duplication was introduced.

Suggested commit: `feat: validate Unreal Fab packages and static cross-target releases`.
The following commands are a manual publication guide, not evidence that publication occurred.
Review the complete worktree and stage only this scope's reviewed files before committing:

```powershell
git status --short
git diff --check
git diff
# Stage the reviewed changed/new files from this scope, then:
git commit -m "feat: validate Unreal Fab packages and static cross-target releases"
git push -u origin feat/PB-1006-PB-1115-unreal-fab-e2e
git switch main
git pull --ff-only origin main
git merge --no-ff feat/PB-1006-PB-1115-unreal-fab-e2e
git push origin main
```

Verify required main CI after publication. Record DONE/Completion Log rollover only after
that succeeds and the user confirms completion, at the start of the next task branch.
