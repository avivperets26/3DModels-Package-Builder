# PB-0209 JSON Lines Progress Reader Evidence

**Task:** PB-0209 — Implement JSON Lines progress reader
**Branch:** `feat/PB-0209-jsonl-progress`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0208 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `0eba2a95ad71e8888e23e4805241bb58c02877c8` merged through
[pull request #37](https://github.com/avivperets26/3DModels-Package-Builder/pull/37) as
`8fca7e0175c6244260156969925d4293059493a6`.
[PR workflow run 30850975774](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30850975774)
and required
[main workflow run 30850980897](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30850980897)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-03. No exception was used.

PB-0209 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `67d8e3027bd16f05d66ee40f67c6faa0082b9291` merged through
[pull request #38](https://github.com/avivperets26/3DModels-Package-Builder/pull/38) as
`ade315b13accf75f65d739d689eed7e5cfa44473`.
[PR workflow run 30892047009](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30892047009)
and required
[main workflow run 30892053123](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30892053123)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-04. No exception was used. This completion state is synchronized during
the approved PB-0210 rollover.

## Implemented contract boundary

- `WorkerProgressJsonLinesReader.ReadAsync` consumes a caller-owned `TextReader` asynchronously.
- A fixed 4,096-character pooled buffer prevents the stream itself from being loaded into memory.
- Retained line content is bounded to the PB-0112 65,536-character event limit plus one possible
  CR delimiter character. Excess content is discarded through the next LF.
- LF and CRLF records are accepted, as is a final valid record without a terminating newline.
- Every physical line returns a `WorkerProgressJsonLineReadResult` with its one-based line number.
- Valid lines reuse `WorkerProgressEventJson` and preserve typed progress, finding, and metric
  events unchanged.
- Empty, whitespace-only, non-object, malformed, duplicate-property, schema-invalid, domain-invalid,
  and oversized lines return stable structured failures. A rejected line does not stop later lines.
- Raw rejected content is never exposed on the result or repeated in line-reader diagnostics.
- Cancellation propagates through asynchronous reads. The reader does not own or dispose the
  caller's stream.

PB-0209 performs no process launch, filesystem access, logging, redaction, persistence, retry,
orchestration, or UI work. PB-0208, PB-0212, PB-0213, and the later desktop tasks retain those
responsibilities.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Progress, finding, and metric records preserve order | `ReadsProgressFindingAndMetricRecordsInPhysicalOrder` |
| LF, CRLF, and final unterminated records | `SupportsCrLfAndFinalUnterminatedRecord` |
| Malformed-line recovery and content non-disclosure | `ReportsMalformedRecordAndRecoversAtNextLine` |
| Specific structured malformed outcomes | `ReturnsSpecificFailureForEveryMalformedPhysicalLine` |
| Bounded oversized-line discard and recovery | `DiscardsOversizedRecordAndRecoversWithoutRetainingItsContent` |
| Exact 65,536-character LF/CRLF boundary | `AcceptsExactMaximumLengthWithLfOrCrLfFraming`, `RejectsMaximumPlusOneWithoutMistakingItForCrLf` |
| Empty stream semantics | `EmptyStreamProducesNoSyntheticRecord` |
| Cancellation and invalid caller input | `CancellationStopsIncrementalConsumption`, `NullReaderIsRejectedAsProgrammingError` |
| Arbitrary asynchronous read boundaries | `HandlesRecordsSplitAcrossSmallAsynchronousReads` |
| Caller-controlled enumeration lifetime | `CallerCanStopAfterOneRecordAndDisposeEnumeration`, `CallerCanDisposeBeforeStartingEnumeration` |
| Physical stream failures remain visible to the caller | `PhysicalReadFailurePropagatesAfterDeliveredRecord` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0209 contract tests | Pass; 18 passed, 0 failed, 0 skipped |
| New production coverage | Pass; both source types report 100% line/branch coverage; the compiler-generated async iterator reports 100% line and 92.86% branch in the Microsoft Cobertura report beneath `artifacts/PB-0209/ms-coverage-complete` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,589 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 345, Contracts 402 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 2m 06.045s on the final exact worktree |
| Formatting and repository safety | Pass; info-level .NET formatting, Ruff lint/format, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive package reported for any of the 15 projects |

No dependency, engine, network, paid service, telemetry, or approved quality-threshold change is
included.

## Manual and visual testing

PB-0209 has no WPF screen, renderer, model import, texture display, or package preview, so there is
no end-user visual test yet. Its behavior is observable through deterministic contract tests that
feed representative JSON Lines streams and inspect each typed line result. The first supported
visual workflow remains the later WPF vertical slice.

## Completion

No PB-0209 implementation, validation, publication, CI, confirmation, or rollover gate remains.
