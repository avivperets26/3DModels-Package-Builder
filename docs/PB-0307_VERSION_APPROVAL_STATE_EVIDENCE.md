# PB-0307 Version Approval State Evidence

**Task:** PB-0307 — Implement candidate approval and compatibility-state persistence
**Branch:** `feat/PB-0307-version-approval-state`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-05

## Scope

PB-0307 implements the documented Discovered, Installed, Candidate, Approved Latest, Rejected,
and Last Known Good lifecycle as a pure Domain transition policy and a typed SQLite persistence
boundary. SQLite schema version 2 adds checked engine state, optimistic revision, UTC lifecycle
timestamps, immutable installed-module snapshots, and append-only transition rows containing the
exact compatibility-suite result used for candidate approval or rejection.

The repository does not discover, download, install, launch, or select an engine; run the five-case
compatibility suite; accept third-party terms; determine licence eligibility; or fetch marketplace
rules. PB-0306 owns selection, PB-0308 owns build locks, PB-0309 owns update guidance, and PB-0310
owns compatibility-suite execution.

## Lifecycle and Evidence Rules

- New records begin only as `Discovered` at revision zero with one initial history row.
- The allowed edges exactly match architecture section 14.2. Skips, reverse promotion, and
  transitions out of Last Known Good fail before storage.
- Candidate → Approved Latest requires a passed result whose positive total equals passed tests
  and has zero failures.
- Candidate → Rejected requires a failed result with at least one failure and internally
  consistent totals.
- Both outcomes require a stable run identity, contained logical evidence reference, lowercase
  SHA-256, and UTC completion time. Other lifecycle edges reject attached suite evidence.
- Expected state and non-negative revision provide optimistic concurrency protection.
- Promoting a candidate atomically demotes the prior Approved Latest for the same engine to Last
  Known Good and appends both history records in the same transaction.
- Reads revalidate canonical tool versions, state tokens, timestamps, revisions, modules, and
  compatibility results and return sanitized failures for corrupt or incompatible stored data.

## Schema Migration

Schema version 2 is applied in one transaction after a consistent contained pre-upgrade backup.
Valid version-1 engine rows are preserved with revision zero and deterministic legacy timestamps.
Invalid legacy state values violate the new checked table and roll back without replacing the
source database. Current databases are idempotent; future or incomplete schemas fail closed.

New tables are `EngineVersionModules` and `EngineVersionTransitions`. The rebuilt
`EngineVersions` table enforces canonical engine/state values and supports only one Approved
Latest row per engine through a partial unique index.

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| All six states and documented transitions persist | `CompletePassingLifecyclePersistsModulesAndCompatibilityEvidence`, `AllowsOnlyDocumentedLifecycleEdges` |
| Passed and failed compatibility results are retained | `CompletePassingLifecyclePersistsModulesAndCompatibilityEvidence`, `FailedCandidateCanBeRetriedWithBothResultsRetained` |
| Promotion retains Last Known Good atomically | `PromotingNewerVersionAtomicallyDemotesPreviousApproval` |
| State survives restart and queries are deterministic | `StateAndHistorySurviveRepositoryRestartAndQueryDeterministically` |
| Concurrent/stale changes fail closed | `ExpectedStateAndRevisionRejectStaleConcurrentTransition` |
| Invalid, duplicate, or corrupt data returns typed failures (ENG-004) | invalid-transition theory, duplicate-version/module test, corrupt-timestamp test |
| Version-1 data upgrades transactionally with backup | `VersionOneDatabaseUpgradesWithApprovalHistoryTablesAndBackup` plus existing migration rollback/idempotence tests |
| Default tests remain deterministic and offline (TEST-012) | Domain and SQLite focused suites use only repository-contained temporary workspaces |

## Current Validation Results

| Validation | Current result |
|---|---|
| Focused lifecycle Domain tests | Pass; 11 passed, 0 failed, 0 skipped. |
| Focused SQLite migration and approval tests | Pass; 42 passed, 0 failed, 0 skipped. |
| Complete affected suites | Pass; Domain 857, Application 98, and Infrastructure 641 passed with 0 failed and 0 skipped. |
| Complete five-project test portfolio | Pass; 2,013 passed, 0 failed, 0 skipped: Domain 857, Application 98, Infrastructure 641, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 3 minutes 32 seconds against the final source state. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; Coverlet 10.0.1 passed all 641 Infrastructure tests but emitted a report with zero instrumented points, so no coverage percentage is claimed. |

## Licensing Boundary

Persisting approval evidence does not grant rights to use a tool. Blender remains GPL software;
.NET remains subject to Microsoft's licensing; Unity remains subject to Unity's current legal,
eligibility, plan, and seat conditions; and Unreal remains subject to the Unreal Engine EULA's
applicable eligibility, seat-subscription, and royalty terms. Package Builder records technical
compatibility state only and does not accept terms or determine operator eligibility.

## Manual Visual Test

Not applicable. PB-0307 changes Domain, Contracts, Infrastructure, SQLite migration, and automated
tests only; it does not modify WPF. The PB-1301 shell remains the current visual checkpoint.

## Remaining Gates

- Resolve or explicitly disposition the invalid coverage-instrumentation evidence before claiming
  the detailed coverage gate.
- User stages and commits PB-0307 on `feat/PB-0307-version-approval-state`.
- User pushes the task branch and merges it into `main` through an optional PR or approved direct
  merge.
- Required `main` CI succeeds for the merge commit.
- User explicitly confirms completion.
- PB-0307 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until the PB-0308
  rollover records those gates.
