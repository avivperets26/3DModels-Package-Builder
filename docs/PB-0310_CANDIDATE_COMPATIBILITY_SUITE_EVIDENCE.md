# PB-0310 Candidate Compatibility-Suite Runner Evidence

**Task:** PB-0310 — Implement candidate compatibility-suite runner
**Branch:** `test/PB-0310-candidate-promotion-suite`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-05

## Scope

PB-0310 adds an Application-owned runner that executes a configured candidate suite through an
injected check executor and records the final decision through the PB-0307 approval repository.
The configuration must cover the static, rigged, rigged-and-animated, item-set, and item-collection
fixtures plus material/preview comparison, clean export/reimport, and marketplace structure
validation. Multiple target-specific checks of a kind are allowed, while check IDs remain unique.

The runner reads and verifies the exact candidate revision before execution, runs checks in the
configured order, retains every outcome, and computes a deterministic lowercase SHA-256 digest.
The persisted transition contains the run ID, aggregate counts, pass/fail outcome, logical evidence
reference, digest, and UTC completion time. An all-pass run requests `ApprovedLatest`; any failed,
exceptional, or contradictory check requests `Rejected` with a sanitized failure code.

## Safe Fallback and Failure Boundary

- A stale or non-candidate approval fails before any fixture is run.
- Invalid, incomplete, duplicate, traversal, absolute, or otherwise unsafe configuration fails
  before repository reads or executor calls.
- Cancellation propagates and records no partial approval or rejection decision.
- An unexpected executor exception becomes a stable failed-check result without exposing its text.
- Every configured check still runs after an individual failure, producing complete aggregate
  evidence rather than an ambiguous partial pass.
- Candidate rejection never requests `ApprovedLatest` or `LastKnownGood` state. The SQLite
  regression test proves that an existing Approved Latest remains unchanged when a newer candidate
  is rejected.
- Successful promotion continues to use the existing atomic repository transaction that demotes
  the prior Approved Latest to Last Known Good.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Candidate runs configured fixture builds | `CompletePassingSuiteRecordsEvidenceAndPromotesCandidate` verifies all eight required categories execute in configured order. |
| Required suite cannot be weakened or redirected | `MissingRequiredCheckFailsBeforeCandidateReadOrExecution` and `InvalidConfigurationFailsBeforeAnySideEffect`. |
| Results are recorded deterministically | passing/failing assertions inspect persisted counts, outcome, reference, and digest; `EvidenceDigestIsDeterministicForSameCandidateConfigurationAndResults`. |
| Promotion occurs only on complete pass | complete-pass test plus `AnyFailureRunsRemainingChecksRejectsCandidateAndPreservesFallbackBoundary`. |
| Failure falls back safely | runner failure-boundary test plus SQLite `RejectingNewerCandidatePreservesExistingApprovedLatest`. |
| Unexpected/invalid executor behavior fails closed | `UnexpectedExecutorFailureBecomesRecordedFailureWithoutLeakingException` and `InvalidExecutorResultFailsClosed`. |
| Cancellation and concurrency do not create partial decisions | `CancellationPropagatesAndDoesNotRecordPartialDecision` and `StaleOrNonCandidateApprovalNeverRunsOrTransitions`. |
| Persistence errors do not report a promotion | `RepositoryFailureIsSanitizedAndDoesNotReportPromotion`. |

## Current Validation

| Validation | Current result |
|---|---|
| Focused Application tests | Pass; 13 passed, 0 failed, 0 skipped. |
| Focused SQLite approval repository tests | Pass; 13 passed, 0 failed, 0 skipped, including the new fallback-preservation regression. |
| Application build | Pass; 0 warnings and 0 errors. |
| Complete five-project test portfolio | Pass; 2,062 passed, 0 failed, 0 skipped: Domain 857, Application 130, Infrastructure 647, Contracts 413, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 5 minutes 32 seconds. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; Coverlet 10.0.1 passed all 13 focused Application tests but emitted a report with zero instrumented lines and branches, so no coverage percentage is claimed. |

## Deferred Real-Engine Evidence

The five product fixtures and their Blender, Unity, Unreal, rendering, reimport, and marketplace
executors are not yet implemented by their applicable E16 tasks. PB-0310 therefore validates the
runner with deterministic injected executors and the real SQLite approval boundary. It does not
claim that unavailable engine fixture builds have run. PB-1608 and PB-1609 remain responsible for
the contained engine matrix and reviewed candidate-promotion CI workflow.

## Final Publication Evidence

- Final task commit: `539e1434a104a907eb427dc74484440f45512437`.
- Pull request: [#53](https://github.com/avivperets26/3DModels-Package-Builder/pull/53).
- Merge commit on `main`: `b24f5d11b290e85d2b8a91a0e0d6ca3f0e506c0c`.
- Required successful `main` CI: [workflow run 31019836442](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31019836442), completed successfully for that exact merge commit.
- User confirmation: explicit push, merge, green required `main` CI, and completion confirmation on 2026-08-05.
- Exceptions: none. The local coverage-instrumentation gap remains disclosed and no coverage percentage is claimed.

PB-0310 is logically complete and its `[x]` / 🟢 **DONE** status, Active Work removal, and single
Completion Log row are synchronized at the beginning of PB-0401 under the permanent one-merge
rollover workflow.
