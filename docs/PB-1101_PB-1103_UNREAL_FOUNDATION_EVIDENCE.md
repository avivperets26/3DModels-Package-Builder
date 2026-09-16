# PB-1101–PB-1103 — Unreal foundation

Branch `codex/PB-1101-PB-1103-unreal-foundation` starts from freshly fetched, fast-forwarded,
clean main `bcd696b1d9aa61ce97228849fbbd120cea34e844`. The user approved the combined scope
on 2026-09-16. PB-1008–PB-1010 were rolled to DONE once using their successful main CI and
user confirmation. The user authorized staging, committing, pushing, merging into main and verifying main CI
on 2026-09-16. Publication results will be retained locally and recorded in the next task rollover.

## Current state

The user installed Unreal 5.8.2 at `C:\Program Files\Epic Games\UE_5.8`, explicitly approving
that location. Its Epic Games Authenticode signature is valid and vendor metadata reports
5.8.2 / changelist 56702186. AGENTS records the narrow external-engine exception; project
outputs remain repository-contained. All three tasks are IN PROGRESS: live foundation checks
now pass; publication and final completion gates remain. The version remains a candidate in
the global production approval system; foundation smoke does not establish full product compatibility.

Candidate 5.8.2 is pinned in `profiles/engines/unreal-5.8.2-candidate.json`, independently of the
approved tool-version database. Source review on 2026-09-16 used the official
[5.8 release announcement](https://www.unrealengine.com/news/unreal-engine-5-8-is-now-available),
[5.8.2 hotfix announcement](https://forums.unrealengine.com/t/5-8-2-hotfix-released/2746335), and
[Epic installation flow](https://www.unrealengine.com/download). No engine is approved, rejected
as incompatible, or claimed installed merely from this source review. A missing prerequisite
is not a compatibility rejection.

## Acceptance mapping

| Owner / criterion | Automated check / fixture | Evidence and status |
| --- | --- | --- |
| PB-1101: detect exact installation with explicit external-root exception | `UnrealFoundationInstallationTests.FoundationCandidateUsesVerifiedContainedDiscovery`; existing locator fixtures, optional explicit live engine input | PASS: exact external metadata parsed by the shared bounded reader; signed engine; `artifacts/PB-1101/installation-discovery.json` |
| PB-1101: smoke and approval decision | `Invoke-UnrealFoundationIntegration.ps1`, real candidate then existing compatibility approval flow | PASS for foundation smoke; user-selected engine accepted for foundation development. Global ApprovedLatest promotion remains gated by full compatibility evidence |
| PB-1102: minimal plugins, Pack root, no generated state | Repository baseline `Unreal foundation template contains only reviewed source and required plugins` | PASS: source inventory and real plugin load; default animation settings require no additional plugin |
| PB-1103: requests, progress, results | `tests/unreal/test_worker.py`; `WorkerJsonTests` with `unreal-probe-result.json` | Shared v1 golden, invalid/oversized/duplicate requests, unsafe paths, version and operation failures |
| PB-1103: saved asset, restart and command-line exit | `Invoke-UnrealFoundationIntegration.ps1` probe/save and fresh-process verify | PASS: live save, separate-process reopen with unchanged hash, rejected-operation exit and cleanup receipt |
| SEC-004/SEC-005: reject unsafe output and errors | Unreal worker tests for symlinks, overlap, foreign project, failure sanitization | Local adapter tests; native engine diagnostics remain local |
| Workspace cleanup | TemporaryDirectory disposal; live harness guarded `finally` | PASS: all three live project clones removed. First-run Zen helper cleanup was rejected by automatic approval review; see audit |

## Reuse and scope

Extracted the existing Blender Python v1 protocol into `workers/shared/package_builder_protocol.py`;
the Blender import module is a compatibility facade. Both engines now use identical bounded parsing,
logical references, atomic output and event serialization. Link/reparse rejection was strengthened
at that shared boundary. The .NET contract and JSON schema remain canonical; the new Unreal result
golden is exercised by both languages. This avoids another Unreal-specific protocol implementation.

The harness invokes the production .NET installation locator and shared bounded vendor-version parser.
External discovery remains informational; only the exact user-approved, signed engine is executable by
this development harness. No production selection-containment rule is relaxed. Engine-specific asset creation/loading remains in the Unreal adapter.
The harness's fixed probe assertions are acceptance checks, not duplicated product validation.
No production cloning, naming generator, full imports, preview UI, marketplace approval or customer
release export is included. PB-1104 onward owns those capabilities.

## Validation and remaining work

Local validation on 2026-09-16: Release build passed with zero warnings/errors; all 2,774 .NET
tests passed with no skips and no source changes during verification; 13 Unreal adapter tests
and all 156 Blender Python tests passed. Full .NET format verification and Ruff checks passed.
The earlier missing-engine preflight failed as expected; the installed-engine continuation now
passes all three real commandlet checks described below. All 36 repository baseline checks passed, including the new template inventory, branch policy,
public-repository safeguards and cleanup checks. Logs: `logs/validation/PB-1101-repository-live-run.log`,
`logs/validation/PB-1101-unit-live-run.log`; .NET summary: `artifacts/test-results/PB-1101/summary.json`.
Engine acceptance remains
pending independently of any green .NET/Python/baseline result. Run instructions and cleanup
behavior are in [the worker guide](../workers/unreal/README.md).

## Live verification — 2026-09-16

Final receipt: `artifacts/PB-1103/ff4fbb94456f41d89ac6e00a74426e5a/receipt.json`.
Three fresh commandlet processes performed save, reopen and unsupported-operation rejection.
The saved material was 2,040 bytes, SHA-256
`d05e4e5c2163412a0fabb2eea630ae21696f7342227d2af4bc674df204a8c62e`.
Successful process logs contain no warnings/errors. Progress begins at 0 and completes at 100;
results use protocol v1 and never promote output. The rejected operation exits unsuccessfully,
returns `UNREAL_OPERATION_UNSUPPORTED`, and preserves the saved asset bytes.
All three owned `artifacts/ue/<guid>` clones, including diagnostic attempts, are gone.

The first diagnostic run revealed missing ACL default compression assets and Zen helper installation
outside the repository. The source template now selects the built-in animation defaults under
`Animation.DefaultObjectSettings`. The harness selects the filesystem-only `-ddc=(Local)` graph
and disables Zen auto-launch; final logs confirm the writable cache in `runtime-data/unreal/5.8.2/ddc`
and contain no user-profile paths or Zen auto-launch. These settings were checked against the
installed 5.8.2 source/config and real logs.

The first run created ten helper files (68,101,924 bytes) in
`%LOCALAPPDATA%/UnrealEngine/Common/Zen/Install`. Timestamps and logs attribute them to that run;
no Zen process remained. Automatic approval review rejected bounded deletion with `blocked by policy`.
The deletion did not execute and was not retried. This is a recorded cleanup exception, distinct
from project/package cleanup, which succeeded. The original Epic installation is preserved.

The user-selected version is accepted for these foundation smoke operations only. Global production
approval remains unchanged: the existing eight-kind compatibility suite requires later E11 product
adapters and cannot honestly be declared passed by this probe. Future engine selection must honor
the user's explicit installation exception through an equally explicit adapter policy.

Suggested eventual commit: `feat: add Unreal candidate template and worker foundation`.
Commit/push/merge are now explicitly authorized. Keep the tasks unchecked in this branch; record
final Git/main-CI evidence during the normal next-task completion rollover.

Configuration references: the harness uses Epic's documented
[EditorSettings analytics preference](https://dev.epicgames.com/documentation/unreal-engine/API/Editor/UnrealEd/UAnalyticsPrivacySettings)
through a [command-line config override](https://dev.epicgames.com/documentation/unreal-engine/configuration-files-in-unreal-engine).
Local DDC and disabled shared DDC use the [documented cache settings](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-derived-data-cache-in-unreal-engine).
The final logs confirm the local filesystem DDC and clean template startup. This does not claim
operating-system sandbox isolation or a full network-activity audit.

Changed source groups: Unreal template/config/plugin; candidate profile; worker and shared Python
protocol; live integration harness; .NET discovery/contract fixtures; Python worker tests; repository
baseline/branch policy. Updated documentation includes AGENTS, engineering skill, backlog,
plan, architecture, quality gates, environment baseline, docs index and worker guides. Previous-task
publication evidence was synchronized as part of the authorized completion rollover.
