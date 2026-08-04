# PB-0212 Structured Logging and Correlation Evidence

**Task:** PB-0212 — Implement structured logging and correlation IDs  
**Branch:** `feat/PB-0212-structured-logging`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0211 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Final task commit `a94570587634509c388502abce0768c622674634` merged through
[pull request #40](https://github.com/avivperets26/3DModels-Package-Builder/pull/40) as
`50c9caff34e7b48676cfa931aa4ce85dc3fc8b0e`. Required
[main workflow run 30901236339](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30901236339)
completed successfully for that exact merge commit. The user explicitly confirmed the push, merge,
green required `main` CI, and completion on 2026-08-04. No exception was used.

PB-0212 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Final task commit `c4fc812391323ea74c67ea9958a52e9855873ff4` merged through
[pull request #41](https://github.com/avivperets26/3DModels-Package-Builder/pull/41) as
`b2da53e3592c813f34a4e50c5290c3dcd2c003f2`. Required
[main workflow run 30906984461](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30906984461)
completed successfully for that exact merge. The user explicitly confirmed the push, merge, green
required `main` CI, and completion on 2026-08-04. No exception was used.

## Implemented boundary

- `StructuredLogEvent` is an immutable persistence-neutral contract with a UTC timestamp,
  correlation ID, component, optional step, closed severity, bounded single-line message, and no
  more than 64 bounded uniquely named properties.
- Correlation, component, step, and property names use a small ASCII token grammar. Expected input
  failures return stable sanitized `LOG_*` results instead of throwing.
- `IStructuredLogWriter` separates application and typed per-job writes from the physical sink.
- `ContainedJsonLinesLogWriter` derives only `logs/application.log` and
  `logs/jobs/<sha256-job-id>/job.log` from an existing absolute project root. Job identities cannot
  become filesystem segments; the original job identity remains in the structured record.
- Records are deterministic compact UTF-8 JSON followed by one LF, without a BOM. Properties are
  serialized in ordinal order and every record includes correlation, component, step, and severity.
- One writer serializes concurrent appends. Existing records are preserved, pre-cancelled work
  creates no state, queued cancellation returns a stable result, and expected I/O/access failures
  do not expose paths or content.
- Existing and newly created log directories are inspected for reparse boundaries. An existing log
  file with reparse attributes is rejected before opening. No user-profile, system log, system
  temporary, telemetry, remote sink, or network fallback exists.
- Sensitive property names are redacted wholesale. Common Bearer/Basic authorization values,
  inline key/password/secret/token assignments, and Windows user-profile prefixes are redacted in
  remaining values before serialization. Control characters are rejected to prevent multiline log
  injection.
- The earlier aspirational Serilog selection was replaced with the built-in `System.Text.Json`
  sink for this approved version-1 boundary. This avoids an unnecessary third-party runtime package
  while retaining the required structured JSON Lines output. No dependency or licence record changed.

PB-0212 does not implement job orchestration, retry/resume, log rotation/quotas, support-bundle
assembly, final cross-system secret scanning, or UI. Those remain with PB-0213, PB-0215, PB-0912,
PB-1811, and PB-1301/PB-1313 respectively.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Deterministic contained application log | `ApplicationLogUsesDeterministicJsonLinesAndRedactsSensitiveContent` |
| Safe per-job log path and identity | `JobLogUsesHashedFolderAndKeepsTypedJobIdentityInRecord` |
| Correlation/component/step/severity fields | application-record assertions and `EverySeverityHasStableLowercaseToken` |
| Credential and profile-path redaction | application record plus `CommonInlineCredentialFormsAreRedacted` |
| Concurrent complete JSON Lines | `ConcurrentWritesProduceCompleteParseableRecordsWithoutLoss` |
| Append behavior | `ExistingLogIsAppendedWithoutReplacingEarlierRecords` |
| Cancellation before/while queued | `PreCancelledWriteCreatesNoLogState`, `CancellationWhileWaitingForWriterReturnsStructuredFailure` |
| Reparse and containment failures | `UnsafeLogRootReturnsSanitizedFailure`, `PostCreationInspectionFailureProducesNoLogRecord`, `ExistingReparseLogFileIsRejectedBeforeOpening` |
| Sanitized storage failures | `ExistingFileAtLogRootIsRejectedAsUnsafe`, `LockedLogFileReturnsSanitizedWriteFailure` |
| Contract bounds and immutable ordering | `StructuredLogContractTests` |

## Local validation

| Validation | Current result |
|---|---|
| Focused PB-0212 tests | Pass; 48 passed, 0 failed, 0 skipped |
| Changed production coverage | Microsoft Cobertura: 100% branches; all executable lines covered (269/270 instrumented lines, with the sole uncovered sequence point on a compiler-generated closing brace) |
| Complete core tests | Pass; 1,683 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 439, Contracts 402 |
| Debug and Release solution builds | Pass; 15 projects, 0 warnings, 0 errors in both configurations |
| Full local Core CI | Pass; all 9 stages completed in 4m 48.649s on the final exact implementation |
| Repository baseline | Pass; 29 checks, 0 failures |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive package in any of the 15 projects |
| Formatting and repository safety | Pass; info-level .NET formatting, Ruff lint/format, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

Coverage remains ignored beneath `artifacts/PB-0212`.

The first Core CI attempt reached the formatting stage after a successful baseline, restore, and
warning-free Release build, then correctly stopped on one private-field naming rule and mechanical
info-level style findings. Those findings were corrected without behavior changes; the standalone
format verifier, focused tests, and complete nine-stage Core CI rerun then passed.

## Manual and visual testing

PB-0212 has no WPF screen or renderer, so there is no end-user visual test. Its observable manual
boundary is the focused test suite, which writes and parses disposable contained log files beneath
ignored `artifacts/PB-0212` and removes them afterward. The first supported visual checkpoint
remains PB-1301 after PB-0213 supplies the fake-worker vertical slice.

## Completion state

All PB-0212 implementation, validation, publication, required `main` CI, confirmation, and rollover
gates are complete. PB-0213 owns persisted orchestration and the fake-worker vertical slice.
