# PB-1104–PB-1106 — Unreal cloning, content and textures

Date: 2026-09-16. Branch: `codex/PB-1104-PB-1106-unreal-import`.
Base: freshly fetched/pulled, clean main `10ce0042025a8cd2dd36f9e6f3c65674111f00c5`.
The user approved all three tickets on this one branch and subsequently authorized the
commit/push/merge flow. Tasks stay unchecked / IN PROGRESS until publication and rollover gates.
PB-1101–PB-1103 are recorded DONE once, backed by task `aad9af3`, merge `10ce004`, successful
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/35085945756)
and user acceptance before this branch began.

## Implemented behavior

The Unreal host owns a job-scoped clone and a nonblocking OS file lock. Only the six reviewed
template files are copied; cached/generated inputs are rejected. Existing projects are never
adopted or deleted. Locks remain held through native process completion and guarded cleanup.
Timeout kills the owned process tree; concurrent calls on the same lease and early disposal
are rejected. Source and cleanup traversal checks reparse points before descent. The zero-byte
lock file persists until job disposal, avoiding unlink/reacquire inode races.

The .NET target adapter projects canonical Domain names, product cases and texture assignments
into the immutable `unreal-content-v1` plan. Project and sole Pack root share a bounded ASCII
name, asset names use `T_<InternalAssetId>`, and case-insensitive collisions fail. Base folders
and applicable Skeletons/Animations/Blueprints are sorted deterministically. A maximum of 128
texture operations and 256 MiB per source bound this adapter milestone; larger plans fail rather
than truncate. The worker uses the existing 1 MiB JSON bound and validates source snapshot hashes.

Colour, data and normal policies are supplied explicitly by the .NET plan, not inferred again
inside Python. Albedo/emission are sRGB; data/normal maps are linear. Colour uses TC_DEFAULT,
data TC_MASKS and normals TC_NORMALMAP. Albedo/opacity retain alpha; other roles discard it.
OpenGL normals flip green, DirectX normals do not, and Auto is rejected pending explicit resolution.
All destinations and source identities are checked before engine writes. Imports never overwrite.
Fresh-process verification reads saved settings without repair and rehashes every asset.

The first live run exposed Unreal resetting sRGB after compression changes. Applying compression
first fixed the failure. `FakeTexture` now models this behavior so the import regression test
would fail if property ordering regressed. Failed diagnostic projects were cleaned up.

## Acceptance mapping

| Task / requirement | Concrete automated checks |
| --- | --- |
| PB-1104 isolated clone, exclusive writers | `CloneTests.test_exact_clone_renaming_and_independent_jobs_cleanup`, `test_separate_process_cannot_acquire_same_job_lease`, `test_same_lease_rejects_concurrent_execution_and_early_disposal`; live harness concurrent lease rejection |
| PB-1104 cancellation, timeout and containment / SEC-002, SEC-005 | `CloneTests.test_timeout_stops_owned_process_then_cleans_project`, `test_cancelled_job_cleans_clone`, `test_engine_created_link_blocks_cleanup_without_touching_target`, `test_overlap_and_outside_roots_fail`, `test_existing_clone_is_never_adopted_or_deleted`, `test_failed_copy_cleans_partial_clone_and_releases_lease` |
| PB-1105 project, Pack and optional folders | `UnrealImportPlanTests.ProductCaseControlsOnlyApplicableFolders` (five cases), `CloneTests.test_exact_clone_renaming_and_independent_jobs_cleanup`; live import/reopen checks every static product folder |
| PB-1105 identity and deterministic references | `UnknownNormalOrientationAndCaseCollisionsFailBeforeEngineExecution`, `HostPlanMatchesSharedEngineFixtureAndIsIndependentOfInputOrdering`; import asserts exact engine object reference |
| PB-1106 sRGB, data, normal, compression, alpha and orientation | `CanonicalRolesMapToExplicitEngineSettings` (nine cases); `TextureTests.test_nine_imports_save_reopen_and_no_overwrite`; live import/reopen of all nine cases |
| PB-1106 stale/hostile plans and settings / SEC-002, REL-008 | `TextureTests.test_hostile_plan_fields_are_rejected`, `test_mutated_source_fails_before_any_engine_import`, `test_reopen_detects_saved_setting_corruption_without_repair`, `test_texture_save_failure_never_claims_success` |
| Shared protocol and foundation regression | Existing 13 `UnrealWorkerTests`; 156 Blender Python tests; `.NET WorkerJsonTests`; foundation live harness |
| Test-output cleanup | Context/finally deletion in unit and live harnesses; recorded cleanup receipts and final read-only inventory |

## Reuse and boundaries

Reused Domain `ProductFolderName`, `InternalAssetId`, `ProductCase`, `TextureAssignment`,
`ColourSpace`, `TextureRole` and `NormalConvention`. Shared Python bounded JSON parsing was
extracted from existing request loading; request behavior and contract are unchanged. Reused
logical-reference containment, linked-path rejection, atomic result writes and protocol events.
Both live harnesses now share the exact signed candidate preflight and existing .NET discovery.

The old Unity clone implementation was inspected. Its C# target-specific layout and retention
API cannot be imported by Unreal's Python host. The Unreal lease implements the same necessary
OS isolation concept with stricter pre-descent cleanup and no retention by default; it does not
copy Unity business rules. Cross-language name/path checks at the worker trust boundary are
intentional, backed by the .NET-produced golden plan. Future host integration should reuse this
lease rather than add another clone implementation (owner: E11 orchestration).

This adds adapter APIs and real engine validation, not desktop controls or final Unreal packages.
Meshes, material graphs, ORM packing and product rendering remain later tickets. Engine 5.8.2
is still the explicitly approved development candidate; global production approval is unchanged.
Source fixture `tests/fixtures/unreal/source/texture.png` is an original synthetic 16×16 RGBA test pattern,
not a customer/third-party asset. Native logs remain local and must not be published unredacted.

API sources: Epic's [TextureFactory properties](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/TextureFactory)
and [official import-task example](https://github.com/EpicGames/PythonSamples/blob/main/scripts/ImportExport/Asset_Import_task.py).
Installed 5.8.2 execution validates the actual property behavior beyond documentation.

## Validation and handoff

The full seven-project Release suite passed 2,790 tests with zero failures/skips and verified
that its run did not modify source candidates. The 16 focused .NET planner cases passed again
after final formatting/fixture placement. Python passed 29 Unreal tests and all 156 Blender
regressions. Ruff lint and formatting pass for 62 files. Release build passed with zero warnings
and errors; the repository's full .NET format command passes with its existing `third_party`
exclusion and info severity. An initial broad format invocation included unmodified vendor code;
that is outside the repository-owned formatting gate. No vendor source was changed.

Final live texture receipt: `artifacts/PB-1104/97d644960ab94b50be651b77534fa7b0/receipt.json`.
Nine real Texture2D assets were imported, loaded in a fresh process and checked for unchanged
bytes/settings. The third process rejected duplicate import without changing them. Successful
import/reopen logs contain no warnings or errors. Exact asset identities remain in the receipt.
Hashes are compared within each run; cross-job `.uasset` byte equality is not claimed because
engine import metadata includes the unique job path. Names and policy settings are deterministic.

The original foundation regression also passed all three processes after shared preflight
extraction: `artifacts/PB-1103/2b8bcb60617b4a56bf7e2785d66d6699/receipt.json`.
All four new texture-run projects and this foundation clone were removed, including the initial
failed diagnostic run. Final inventory: zero directories under `artifacts/ue` and zero Python
fixture workspaces. `artifacts/PB-1104/cleanup-verified.json` records the audit.

Automatic approval review rejected removal of 24 empty scratch subdirectories beneath
`artifacts/validation/PB-1104/temp` with `blocked by policy`. The deletion did not execute and
was not retried. There are zero files in that scratch tree. This new exception and the unchanged
historical exceptions are recorded in the cleanup audit; generated engine assets/packages are gone.

Logs and summaries: `artifacts/test-results/PB-1104/summary.json`,
`logs/validation/PB-1104-{build,format,planner-tests,python-tests,blender-regression,live,baseline}.log`.
The final repository baseline passed all 36 checks, including the exact binary-fixture
hash/licence record, secret/prohibited-content scan, branch/status accounting and Git integrity.
Backlog: 265 total; 148 DONE; 3 IN PROGRESS; 0 BLOCKED; 114 BACKLOG; 117 remaining (55.8%).

Changed source groups: Unreal typed plan; Python clone, plan validation, texture adapter and
worker routing; shared bounded JSON; common candidate preflight and live harness; .NET/Python
tests and original hash-pinned fixture; exact branch/binary-fixture validation. Documentation
updates cover AGENTS, engineering skill, backlog rollover/Active Work, plan, architecture, quality
traceability, cleanup audit, fixture licence, worker guide and docs index. No unresolved
implementation dependency remains; publication/main CI/user confirmation are still required.

Suggested commit: `feat: add isolated Unreal jobs and texture import policies`.
The approved publication sequence is shown below. Exact task/merge commits and main CI are
recorded in local publication evidence, then synchronized during the next-task rollover:

```powershell
git add AGENTS.md docs scripts skills/package-builder-engineering/SKILL.md src/PackageBuilder.Targets.Unreal tests/PackageBuilder.App.Wpf.Tests/UnrealImportPlanTests.cs tests/fixtures/unreal tests/unreal workers
git commit -m "feat: add isolated Unreal jobs and texture import policies"
git push -u origin codex/PB-1104-PB-1106-unreal-import
git switch main
git pull --ff-only origin main
git merge --no-ff codex/PB-1104-PB-1106-unreal-import
git push origin main
```

Required main CI and user confirmation follow publication; the next task branch records DONE.
