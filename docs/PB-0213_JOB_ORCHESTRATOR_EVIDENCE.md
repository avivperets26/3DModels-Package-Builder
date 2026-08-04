# PB-0213 Job Orchestrator Evidence

**Task:** PB-0213 — Implement job orchestrator and persisted state transitions
**Branch:** `feat/PB-0213-job-orchestrator`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0212 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Final task commit `c4fc812391323ea74c67ea9958a52e9855873ff4` merged through
[pull request #41](https://github.com/avivperets26/3DModels-Package-Builder/pull/41) as
`b2da53e3592c813f34a4e50c5290c3dcd2c003f2`. Required
[main workflow run 30906984461](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30906984461)
completed successfully for that exact merge commit. The user explicitly confirmed the push, merge,
green required `main` CI, and completion on 2026-08-04. No exception was used.

PB-0213 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Final task commit `9b377e4ce7ad9fc750b2b3ff8a6115a5fc5f3fe2` merged through
[pull request #42](https://github.com/avivperets26/3DModels-Package-Builder/pull/42) as
`206d999661a96ebf71ccf3e1dcf87342114ff06a`. Required
[main workflow run 30910471888](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30910471888)
completed successfully for that exact merge commit. The user explicitly confirmed the push, merge,
green required `main` CI, and completion on 2026-08-04. No exception was used.

## Implemented boundary

- `PersistedBuildJobOrchestrator` creates a queued job before worker execution and advances only
  through the approved `BuildJobTransitionPolicy` graph using optimistic persisted transitions.
- `ResumeAsync` reads the exact stored state and continues it without replaying completed stages.
  An interrupted stage remains persisted and resumable across a new orchestrator instance.
- `ContinueAfterReviewAsync` is the only route from `AwaitingReview` back to `Inspecting`; ordinary
  resume leaves review-paused jobs unchanged.
- `IBuildJobStageWorker` executes one stage but cannot mutate job state or promote a release.
  `DeterministicFakeBuildJobWorker` supplies the no-engine vertical slice with deterministic success,
  failure, interruption, and inspection-review outcomes.
- A required structured per-job log is persisted before each stage begins. If logging fails, worker
  execution does not start.
- Worker failures and unexpected worker exceptions use sanitized stable failure codes and transition
  the job to `Failed`. Expected persistence failures return structured orchestration failures.
- Release promotion is attempted only after `CleanReimport` succeeds. A stage failure, cancellation,
  interruption, review pause, invalid worker result, or promotion failure cannot produce a successful
  promotion receipt or a `Completed` job.
- Cancellation follows the existing authoritative graph. It persists `Cancelled` only where the
  graph approves that transition; later stages remain at their exact resumable state.
- UTC timestamps come from an injected clock and are clamped against the persisted update timestamp,
  keeping repository transitions monotonic and deterministic.
- The orchestration contracts are persistence-, engine-, filesystem-, process-, network-, and
  UI-neutral. No new package or licence dependency was introduced.

PB-0213 does not implement retry policy, caching, a real engine worker, a CLI command, or WPF UI.
Those remain with PB-0215, PB-0214, the engine milestones, PB-1201, and PB-1301 respectively.

## Approved execution path

```text
Queued -> Preflight -> Inspecting -> Normalizing -> BuildingTargets
       -> RenderingPreviews -> Validating -> PackagingMarketplace
       -> CleanReimport -> Completed
```

`Inspecting` may pause at `AwaitingReview` and later return to `Inspecting`. Approved failure and
cancellation transitions remain defined exclusively by the domain transition policy.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Full successful state machine | `FakeWorkerJobCompletesEntirePersistedStateMachine` |
| Resume after restart without replay | `NewOrchestratorResumesFromExactPersistedStageAfterInterruption` |
| Every stage fails closed | `FailureAtEveryStagePersistsFailedAndNeverPromotes` |
| Promotion failure and exception safety | `PromotionFailurePersistsFailedAndDoesNotClaimPromotion`, `UnexpectedWorkerAndPromoterExceptionsFailClosedWithoutDiagnosticsLeak` |
| Explicit review continuation | `ReviewRequiresExplicitContinuationAndThenCompletes` |
| Cancellation semantics | `CancellationAtPreflightPersistsCancelledAndNeverPromotes`, `CancellationAtNonCancellableStageLeavesExactStateForResume` |
| Required structured logging | `LoggingFailureStopsBeforeWorkerAndLeavesResumableState` |
| Repository failure handling | creation, read, and transition failure tests in `PersistedBuildJobOrchestratorTests` |
| Worker-result validation | `ContradictoryWorkerResultsFailClosedWithoutTransition` |
| Deterministic clock behavior | `BackwardsClockIsClampedToPersistedTimestamp`, `NonUtcClockIsRejectedBeforeAStageCanRun` |
| Fake-worker boundary | `FakeWorkerValidatesCancellationAndConfiguredBehaviors` |

## Local validation

| Validation | Current result |
|---|---|
| Focused PB-0213 tests | Pass; 31 passed, 0 failed, 0 skipped |
| Changed production coverage | Microsoft `dotnet-coverage` 18.9.0 Cobertura: 96.96% line (319/329) and 86.30% branch (126/146) |
| Complete Application tests | Pass; 84 passed, 0 failed, 0 skipped |
| Debug and Release solution builds | Pass; 15 projects, 0 warnings, 0 errors in both configurations |
| Info-level .NET formatting | Pass; no changes required after the final formatting pass |
| Complete core tests | Pass; 1,714 passed, 0 failed, 0 skipped: Domain 789, Application 84, Infrastructure 439, Contracts 402 |
| Full local Core CI | Pass; all 9 stages completed in 2m 6.272s on the final implementation |
| Repository baseline | Pass; 29 checks, 0 failures |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive package in any of the 15 projects |
| Repository safety and diff checks | Pass; formatting, Ruff, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, ignored files, and history integrity |

Coverage output remains ignored beneath `artifacts/PB-0213`. Coverlet passed the focused tests but
emitted a zero-instrumentation report, matching the already documented collector anomaly. The
repository-local, no-cost Microsoft `dotnet-coverage` 18.9.0 tool produced the retained Cobertura
measurement above without changing tracked dependencies or installing a system tool.

## Manual and visual testing

PB-0213 has no WPF screen or rendered asset, so end-user visual testing is not applicable on this
branch. Its observable boundary is the focused deterministic orchestration suite. Once PB-0213 is
merged and required `main` CI is green, PB-1301's dependencies are satisfied and the next branch can
deliver the first launchable WPF shell for direct visual testing.

## Completion state

All PB-0213 implementation, validation, publication, required `main` CI, confirmation, and rollover
gates are complete. PB-1301 owns the first launchable WPF shell and visual checkpoint.
