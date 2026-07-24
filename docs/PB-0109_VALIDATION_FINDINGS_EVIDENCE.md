# PB-0109 Validation Findings Evidence

**Task:** PB-0109 — Implement validation finding and error-code model  
**Branch:** `feat/PB-0109-validation-findings`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-07-24

## Scope

PB-0109 adds immutable, renderer-independent validation finding values in
`PackageBuilder.Domain.Validation` and a stable System.Text.Json boundary in
`PackageBuilder.Contracts.Validation`. Expected invalid values and malformed external JSON return
structured results rather than escaping as unhandled exceptions.

PB-0108 publication is synchronized by this branch: PB-0108 is `[x]` / 🟢 **DONE**, is absent from
Active Work, and appears exactly once in the Completion Log with task commit, pull request,
successful PR workflow, merge commit, successful required `main` workflow, explicit user
confirmation, and no exception. PB-0109 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and
is absent from the Completion Log.

## Public Domain Types

| Type | Purpose |
|---|---|
| `FindingCode` | Validated stable machine identity for one condition. |
| `FindingSeverity` | Closed Info, Warning, Error, and Fatal inventory with stable JSON tokens. |
| `FindingSourceComponent` | Validated extensible identity of the component that produced a finding. |
| `FindingExplanation` | Required human-readable explanation preserving accepted Unicode. |
| `CorrectiveAction` | Optional validated human-readable practical correction. |
| `ValidationFinding` | Immutable aggregate containing every PB-0109 finding fact. |
| `ValidationFindingResult<T>`, `ValidationFindingError` | Structured non-throwing expected-input outcome. |

`ValidationFinding` reuses PB-0108 `BuildArtifactId` for its optional related artifact. It does not
store or resolve a filesystem path, and PB-0109 does not add a second logical-source-reference
field because no approved acceptance requirement needs one.

## Stable Code Grammar and Compatibility

The exact code grammar is:

```text
[A-Z][A-Z0-9]*(?:_[A-Z][A-Z0-9]*)*
```

A code therefore contains one or more non-empty uppercase ASCII alphanumeric segments separated
by exactly one underscore. Every segment begins with an uppercase ASCII letter and may continue
with uppercase ASCII letters or digits; a one-letter segment is valid, while a digit-led segment
is not. Null, empty, whitespace-only, lowercase or mixed-case, leading/trailing/repeated
underscores, controls, Unicode letters, spaces, hyphens, dots, colons, URI/path separators, and
traversal-like forms are rejected. There is no arbitrary length limit.

Codes are ordinal, case-sensitive compatibility identities. They describe a condition, not an
occurrence. Filenames, user data, timestamps, GUIDs, personal paths, secrets, and changing
diagnostic text must remain outside the code.

## Severity and Blocking

| Domain value | JSON token |
|---|---|
| `Info` | `info` |
| `Warning` | `warning` |
| `Error` | `error` |
| `Fatal` | `fatal` |

Release blocking is an explicit Boolean and is never calculated silently from severity. All eight
severity/blocking combinations are valid. No combination is prohibited by an approved
requirement; later release-gate policy may interpret findings without changing their retained
facts.

## Text and Source Invariants

- Source identities use lowercase ASCII words separated by single hyphens, such as
  `unity-material-validator`.
- Explanations are required.
- Corrective actions may be absent only when no safe, practical caller action exists.
- Present explanation/action text preserves Unicode and casing exactly.
- Null, empty, whitespace-only, leading/trailing whitespace, and control characters are rejected.
- No arbitrary text length limit is introduced.
- Tracked examples contain no stack trace, credential, secret, or personal path.

## JSON Contract

The exact property order is:

1. `code`
2. `severity`
3. `explanation`
4. `source`
5. optional `relatedArtifactId`
6. optional `suggestedAction`
7. `blocksRelease`

Absent optional values are omitted, not serialized as `null`. Unknown or duplicate properties,
missing required properties, invalid JSON types, unknown severity tokens, malformed codes/sources,
invalid text, and invalid artifact IDs return typed deserialization failures. Repeated
serialization is deterministic and culture independent.

Golden example with optional values:

```json
{"code":"UNITY_MATERIAL_MISSING_NORMAL","severity":"warning","explanation":"The normal map is missing.","source":"unity-material-validator","relatedArtifactId":"Artifact-01","suggestedAction":"Assign the intended normal map.","blocksRelease":true}
```

Golden example without optional values:

```json
{"code":"UNITY_MATERIAL_MISSING_NORMAL","severity":"warning","explanation":"The normal map is missing.","source":"unity-material-validator","blocksRelease":false}
```

Property names, order, severity tokens, and optional omission behavior are compatibility
commitments. A future change requires an explicitly versioned migration. PB-0910 owns the complete
validation-report schema; PB-0112 owns worker request/progress/result envelopes.

## Scope Boundaries

PB-0109 adds no validator engine, finding catalog, release-gate evaluation, persistence, SQLite
mapping, logging, redaction, support bundle, UI presentation, JSON validation report, worker
protocol envelope, engine behavior, marketplace behavior, filesystem access, or networking. It
does not migrate earlier task-local result types.

## Tests and Coverage

Focused Domain and Contract suites cover the complete code grammar, every malformed category, all
four severities and eight severity/blocking combinations, source/text validation, optional
artifact/action relationships, Unicode/hostile/unusually large text, immutability, ordinal
equality, stable hashing, culture independence, exact golden JSON for every severity, optional
omission, round trips, malformed/unknown JSON, precise structured failures, and deterministic
repeated serialization.

Coverage uses centrally pinned `coverlet.collector` `10.0.1` with
`ExcludeAssembliesWithoutSources=None`. Generated reports remain beneath ignored
`artifacts/PB-0109`.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0109 Domain tests | Pass; 78 passed, 0 failed, 0 skipped. |
| Focused PB-0109 Contract tests | Pass; 33 passed, 0 failed, 0 skipped. |
| Per-file coverage | Pass; all 11 new production files report 100% line and branch coverage. |
| Complete Domain suite | Pass; 739 passed, 0 failed, 0 skipped, preserving the previous 661 tests. |
| Relevant Contracts suite | Pass; 34 passed, 0 failed, 0 skipped. |
| All four core test projects | Pass; 775 discovered, 775 passed, 0 failed, 0 skipped. |
| Solution architecture validator | Pass; 7 checks passed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| ADR validator | Pass; 8 checks passed, 0 failed. |
| Repository baseline with `RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| Full core CI | Pass; all 9 fail-closed stages passed. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and public-repository scans | Pass; .NET and Ruff formatting/lint, `git diff --check`, and candidate secret/personal-path/binary/generated/prohibited-file scans. |

## Remaining Gates

PB-0109 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and remains absent from the
Completion Log. Local implementation and the requested validation matrix pass. User-controlled
staging, task commit, task-branch push, merge into and push of `main`, successful required `main`
CI, explicit completion confirmation, and next-task rollover synchronization remain.
