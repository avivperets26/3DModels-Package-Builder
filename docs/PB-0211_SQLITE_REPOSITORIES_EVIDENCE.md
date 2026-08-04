# PB-0211 SQLite Repository Evidence

**Task:** PB-0211 — Implement job, artifact, tool, and finding repositories
**Branch:** `feat/PB-0211-sqlite-repositories`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0210 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `131342a0eb5adc23e11af4657b33515b51863ac5` merged through
[pull request #39](https://github.com/avivperets26/3DModels-Package-Builder/pull/39) as
`e2ceb8c90162b543413c917f98de23765eb0cbf8`. Required
[main workflow run 30897157981](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30897157981)
completed successfully for that exact merge. The user explicitly confirmed the push, merge,
green required `main` CI, and completion on 2026-08-04. No exception was used.

PB-0211 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its user-controlled
publication, required `main` CI, explicit confirmation, and PB-0212 rollover.

## Implemented repository boundary

- Persistence-neutral interfaces and immutable records live in
  `PackageBuilder.Contracts.Persistence`; Application code never needs SQLite APIs.
- `SqliteBuildMetadataRepository` implements the separate job, artifact, finding, and tool
  interfaces over the PB-0210 version-1 schema.
- Repository creation accepts only an existing, current, complete SQLite database at an absolute
  path contained beneath the approved project root without any existing reparse-point boundary.
  Integrity and the required table inventory are checked before use.
- New jobs must begin queued. Atomic optimistic transitions require the expected state and exact
  previous update timestamp, enforce the PB-0108 transition graph, require a stable failure code
  only for failed jobs, and reject stale or backwards-time updates.
- Resumable queries return only nonterminal jobs in stable creation/identity order. Querying by
  state and identity supports later job history and orchestration without adding PB-0213 behavior.
- Artifact metadata uses typed job/artifact/step/role/target/lifecycle values, safe logical
  references, optional canonical lowercase SHA-256, and nonnegative byte counts. Binary content
  remains outside SQLite in the artifact store.
- Findings reuse the PB-0109 typed finding model and verify that an optional related artifact
  belongs to the same job. Artifact and finding queries remain deterministically ordered.
- Tool metadata supports deterministic approval queries and idempotent updates by stable
  installation identity while preserving the schema's unique physical-tool tuple.
- Every statement is parameterized. Cancellation propagates. Expected input, not-found,
  concurrency, uniqueness/relationship, schema, invalid-data, and storage outcomes return stable,
  sanitized codes without exposing paths, SQL, or stored content.

PB-0211 does not implement orchestration, retries, structured logs, engine-version approval,
binary artifact storage, or UI. PB-0212 owns structured logs, PB-0213 owns orchestration and
persisted execution behavior, and PB-0307 owns engine candidate approval state.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Create, retrieve, query, fail, and cancel jobs | `JobsCanBeCreatedQueriedResumedFailedAndCancelled` |
| Resume only nonterminal work deterministically | `ResumableQueryReturnsOnlyNonTerminalJobsInDeterministicOrder` |
| Atomic optimistic state protection | `ExpectedStateAndTimestampProtectConcurrentTransitions` |
| Approved transition and failure-code policy | `InvalidTransitionsAndFailureCodesAreRejectedBeforeStorage` |
| Artifact/finding job correlation | `ArtifactAndFindingMetadataRemainCorrelatedWithOwningJob`, `CrossJobArtifactFindingCorrelationIsRejected` |
| Artifact step ownership | `ArtifactStepMustBelongToSameJob` |
| Tool upsert, approval filtering, and uniqueness | `ToolUpsertAndApprovalQueriesAreDeterministic`, `ToolUniqueIdentityConflictDoesNotOverwriteAnotherRecord` |
| Safe, bounded metadata input | `ArtifactFindingAndToolInputValidationRejectsUnsafeMetadata`, `JobInputValidationAndMissingJobAreExplicit` |
| Sanitized conflicts and corrupt data | `DuplicateAndMissingReferencesReturnSanitizedFailures`, `CorruptStoredTimestampFailsClosedWithSanitizedDiagnostic` |
| Cancellation and database boundary | `CancelledTokenPropagatesWithoutWriting`, `RepositoryCreationRejectsOutsideMissingAndUnsupportedDatabases` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0211 integration tests | Pass; 17 passed, 0 failed, 0 skipped |
| New production coverage | Microsoft Cobertura: `SqliteBuildMetadataRepository` 95.65% line / 81.13% branch; repository result and persisted record types 100% line/branch beneath ignored `artifacts/PB-0211/coverage-final` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,635 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 391, Contracts 402 |
| Debug and Release builds | Pass; 15 projects, 0 warnings, 0 errors in each configuration |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 3m 17.217s on the final exact implementation |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive package in any of the 15 projects |
| Formatting and repository safety | Pass; info-level .NET formatting, Ruff lint/format, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

## Manual and visual testing

PB-0211 has no WPF screen or renderer, so no end-user visual test is available yet. Its manual
observable boundary is the focused integration suite, which creates only disposable contained
SQLite databases beneath ignored build output and removes them afterward. The first supported
launchable WPF checkpoint remains PB-1301, after PB-0213 supplies the fake-worker vertical slice.

## Remaining gates

User-controlled commit and branch push, merge into and push of `main`, successful required `main`
CI, explicit completion confirmation, and PB-0212 rollover remain.
