# PB-0108 Build Job Domain Evidence

**Task:** PB-0108 — Implement build job, step, artifact, and state models  
**Branch:** `feat/PB-0108-build-job-domain`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-07-24

## Scope

PB-0108 adds typed immutable build execution intent in `PackageBuilder.Domain.BuildJobs`. It models
job identity, current state, exact transition authorization and history; step identity, operation
type, execution stage, recorded status, ordering, UTC timing, and completion references; and
artifact identity, ownership, role, target association, logical reference, and lifecycle facts.

The Domain reads no clock and performs no filesystem, persistence, process, engine, marketplace,
network, serialization, hashing, orchestration, retry, or resume behavior. Expected invalid input
and transitions return task-local structured results. PB-0109 validation findings and PB-0204
streamed hashing remain deferred.

This branch performs the approved PB-0107 rollover. PB-0107 is `[x]` / 🟢 **DONE**, is absent from
Active Work, and appears exactly once in the Completion Log with reverified task commit, pull
request, successful PR workflow, merge commit, successful required `main` workflow, user
confirmation, and no exception. PB-0108 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and
is absent from the Completion Log.

## Public Types

| Type | Purpose |
|---|---|
| `BuildJobId`, `BuildStepId`, `BuildArtifactId` | Distinct stable ordinal identities. |
| `BuildJobState`, `BuildJobStateCategory` | Every architecture job state and active/review/terminal classification. |
| `BuildJobTransitionPolicy` | Single authoritative approved-edge table. |
| `BuildJobTransition`, `BuildJobTransitionResult`, `BuildJobTransitionError` | Immutable ordinal history and structured non-throwing transition outcomes. |
| `BuildJob` | Immutable identity, Queued initial state, current state, history, steps, and artifacts. |
| `BuildStepType` | Extensible canonical logical operation identity. |
| `BuildStepStatus` | Pending, running, completed, failed, or cancelled retained step fact. |
| `BuildStepCompletionMetadata` | Deterministic logical input, output, tool-version, and log references for completed steps only. |
| `BuildStep` | Typed ownership, operation, execution stage, status, order, timing, and completion data. |
| `BuildArtifactRole` | Extensible canonical logical artifact role. |
| `BuildArtifactLifecycleState` | Staged, validated, or promoted artifact fact. |
| `BuildArtifact` | Typed ownership, role, optional target, logical reference, lifecycle, and UTC timestamps. |
| `BuildModelValidationResult<T>`, `BuildModelValidationError` | Task-local expected PB-0108 validation results. |

## Approved Transition Table

| From | Approved targets |
|---|---|
| Queued | Preflight, Cancelled |
| Preflight | Inspecting, Failed, Cancelled |
| Inspecting | AwaitingReview, Normalizing, Failed |
| AwaitingReview | Inspecting, Cancelled |
| Normalizing | BuildingTargets, Failed |
| BuildingTargets | RenderingPreviews, Failed |
| RenderingPreviews | Validating, Failed |
| Validating | PackagingMarketplace, Failed |
| PackagingMarketplace | CleanReimport, Failed |
| CleanReimport | Completed, Failed |
| Completed | none |
| Failed | none |
| Cancelled | none |

Every same-state edge, every edge absent from this table, and every transition from Completed,
Failed, or Cancelled returns a failed `BuildJobTransitionResult` with a typed reason and no new
job value.

## Invariants and Boundaries

- Jobs always begin Queued with empty immutable history.
- Successful transitions create a new job value and append one ordinal history entry; the prior
  value remains unchanged.
- Job, step, and artifact identities preserve accepted Unicode/casing and compare ordinally.
- Step types and artifact roles use extensible lowercase single-hyphen-separated identifiers.
- Steps use only execution stages; Queued, AwaitingReview, and terminal job states are not steps.
- Pending steps have no timing; running steps have only a start; terminal step records have start
  and end. Only Completed requires and permits completion metadata.
- Every represented timestamp is caller-supplied UTC. Reversed ranges, state times before artifact
  creation, and step/artifact times before job creation fail.
- Job construction rejects nulls, ordinal duplicate step/artifact IDs, duplicate step ordering,
  foreign job references, and unknown owning-step references.
- Steps are retained by numeric order; artifacts and completion references use ordinal ordering.
  All returned collections are immutable snapshots.
- Artifact logical references reject rooted, drive/URI-like, backslash, empty-segment, traversal,
  segment-edge-whitespace, and control-character forms without resolving or opening a path.
- Step cancelled status records a completed fact only. PB-0108 defines no step transition graph,
  retry, resume, pause, cancellation execution, or orchestration behavior.

## Normative Review

No contradiction was found among the product plan, architecture state diagram, quality gates,
backlog, and relevant accepted ADRs. The implementation does not add cancellation from Inspecting,
Normalizing, BuildingTargets, RenderingPreviews, Validating, PackagingMarketplace, or
CleanReimport, and does not add retry, pause, or resume edges.

## Tests and Coverage

The focused xUnit v3 suite tests each approved edge individually and the complete 13-by-13 matrix
for every unapproved edge; self and terminal rejection; null/UTC/chronology errors; initial Queued
state; history determinism and immutability; hostile identities, roles, and references; every step
timing/status combination; completed-only metadata; duplicate ordering and ownership rejection;
unknown job/step references; artifact lifecycle/target data; ordinal casing; stable equality and
hashing; Turkish-culture independence; and forbidden Domain dependencies/hidden clock or I/O.

Focused coverage uses centrally pinned `coverlet.collector` `10.0.1` with
`ExcludeAssembliesWithoutSources=None`. All 17 new production files report 100% line
and 100% branch coverage. Generated reports remain beneath ignored `artifacts/PB-0108`.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0108 tests | Pass; 61 passed, 0 failed, 0 skipped. |
| Per-file coverage | Pass; all 17 new production files report 100% line and branch coverage. |
| Complete Domain suite | Pass; 661 passed, 0 failed, 0 skipped, preserving all previous 600 tests. |
| All four core test projects | Pass; 664 discovered, 664 passed, 0 failed, 0 skipped. |
| Solution architecture validator | Pass; 7 checks passed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| ADR validator | Pass; 8 checks passed, 0 failed. |
| Repository baseline with `RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| Full core CI | Pass; all 9 fail-closed stages passed. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and public-repository scans | Pass; .NET and Ruff formatting/lint, `git diff --check`, and repository secret/personal-path/generated/prohibited-content scans. |

## Remaining Gates

PB-0108 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and remains absent from the
Completion Log. Local implementation and the requested validation matrix pass. User-controlled
staging, task commit, task-branch push, merge into and push of `main`, successful required `main`
CI, explicit completion confirmation, and next-task rollover synchronization remain.
