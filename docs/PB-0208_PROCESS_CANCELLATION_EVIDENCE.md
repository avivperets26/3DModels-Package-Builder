# PB-0208 Process Cancellation and Cleanup Evidence

**Task:** PB-0208 — Implement process timeout, cancellation, and cleanup
**Branch:** `feat/PB-0208-process-cancellation`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0207 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `3b49e6e0b5aa390680ce18d93a5a4f15eb988d94` merged through
[pull request #36](https://github.com/avivperets26/3DModels-Package-Builder/pull/36) as
`9ebc50c5bceab42e79201c6cf9c898150d270669`.
[PR workflow run 30844336598](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30844336598)
and required
[main workflow run 30845827814](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30845827814)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-03. No exception was used.

PB-0208 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its one
user-controlled publication, required `main` CI, explicit confirmation, and PB-0209 rollover.

## Implemented lifecycle boundary

- `IExternalProcessRunner` accepts a caller cancellation token while retaining a convenience
  overload for callers that do not yet propagate one.
- Every request owns explicit positive startup, idle, total, and graceful-termination intervals.
  No interval may exceed seven days; startup and idle intervals may not exceed the total interval.
- The startup timer begins after launch and requires the first stdout or stderr activity before its
  deadline. Thereafter, either stream resets the idle timer. The total timer remains absolute.
- External cancellation and any deadline first create one unpredictable contained marker beneath
  the request's temporary root. The exact marker path is passed to the child only through the
  runner-owned `PACKAGEBUILDER_CANCELLATION_FILE` environment variable.
- A cooperative worker can observe the marker and exit during the bounded grace period. Otherwise
  the runner calls whole-process-tree termination and waits for confirmed process exit.
- The runner deletes a marker only when it created that exact file. A pre-existing collision is
  never treated as runner-owned and is never deleted.
- The receipt distinguishes normal exit, external cancellation, startup timeout, idle timeout,
  and total timeout. It also records signal creation, graceful acknowledgement, forced
  termination, and control-file cleanup.
- Complete stdout and stderr are streamed independently into unique UTF-8 files beneath the
  validated contained log root while the existing in-memory captures remain bounded. The receipt
  exposes only safe project-relative log references.
- Cancellation before launch returns a sanitized structured failure without creating process,
  control, or log state. Cancellation during executable inspection is handled by the same
  pre-launch boundary.

PB-0208 does not add JSON Lines framing, persisted orchestration, retry/resume policy, structured
redaction, or UI controls. PB-0209, PB-0212, PB-0213, and PB-1314 retain those responsibilities.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Cooperative cancellation | `ExternalCancellationIsAcknowledgedGracefullyAndPreservesCompleteLogs` |
| Startup timeout | `StartupTimeoutEscalatesAndPreservesEmptyLogs` |
| Idle timeout after activity | `IdleTimeoutEscalatesAfterObservedActivity` |
| Absolute total timeout despite heartbeat activity | `TotalTimeoutWinsWhileHeartbeatPreventsIdleTimeout` |
| Forced whole-tree termination and child cleanup | `ForcedCancellationTerminatesChildTreeAndReleasesProcessHandles` |
| Pre-launch cancellation leaves no state | `CancellationBeforeLaunchCreatesNoProcessState` |
| Strict timeout policy | `InvalidLifecyclePoliciesFailBeforeLaunch` |
| Complete preserved stream logs | all four started-process lifecycle tests and existing bounded-capture tests |
| Marker ownership and locked-file failure handling | `CancellationSignalOperationsReportExistingAndLockedFiles`, `TerminationPreservesUnownedSignalWhenExitPrecedesEscalation` |
| Strict lifecycle/log contracts | `LifecycleContractsExposeDefaultsAndExplicitReceiptMetadata`, `LogReferencesRejectUnsafeValues` |

The real process probe is built from tracked source beneath `tests/fixtures/processes`. It supports
cooperative exit, silent startup, sustained heartbeat output, ignored cancellation, and a spawned
child whose PID is verified absent after escalation. Generated probes, logs, control state, and
coverage remain ignored beneath `artifacts/PB-0208`.

## Local validation

| Validation | Result |
|---|---|
| Focused process tests | Pass; 59 passed, 0 failed, 0 skipped |
| Critical production coverage | Pass; all 18 changed production/compiler-generated components report 100% line and branch coverage in the Microsoft Cobertura report beneath `artifacts/PB-0208/coverage-exact` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,571 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 345, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 3m 25.856s on the exact implementation worktree |
| Formatting and repository safety | Pass; .NET info-level and Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

No dependency, engine, network, paid service, telemetry, or approved quality-threshold change is
included.

## Manual and visual testing

PB-0208 has no WPF screen, renderer, model import, or package preview, so no end-user visual test is
available yet. The focused suite does perform real child-process cancellation and timeout tests,
including cooperative exit and forced cleanup of a spawned descendant. The first supported visual
workflow remains the later WPF vertical slice.

## Remaining gates

Final exact-worktree validation, user-controlled commit and branch push, merge into and push of
`main`, successful required `main` CI, explicit completion confirmation, and PB-0209 rollover
remain.
