# PB-0112 Worker Contracts Evidence

**Task:** PB-0112 — Define worker request, progress, and result contracts
**Branch:** `feat/PB-0112-worker-contracts`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-25

## Scope and rollover

PB-0112 defines strict, deterministic, offline protocol-version-1 boundaries for a worker request,
an individual JSON Lines event, and a worker result. It adds three Draft 2020-12 schemas, immutable
C# contract values, embedded schema validation, structured non-throwing parse/serialization
results, valid golden fixtures, retained invalid fixtures, and schema, semantic, golden,
round-trip, culture, bounds, and PB-0109 parity tests.

The PB-0111 rollover uses final task commit
`c49cf2755458c409fde41714a29a275f38ce9a92`, pull request #25, merge
`9e2f4f3dbf4cc8e313afdb1374db3af4fc0de653`, successful PR run `30152657016`, successful required
`main` run `30152658843`, and user confirmation dated 2026-07-25. No exception was used. PB-0111
is `[x]` / 🟢 **DONE**, absent from Active Work, and logged exactly once.

PB-0112 was published from final task commit
`a22f913108870b24ead8dde38833ae3e56c02c47`, merged through pull request #26 as
`e8c31a221dedd49daac0e1e35d29fb5df2f43642`, validated by successful PR and required `main`
workflows, and explicitly confirmed complete by the user on 2026-07-25. The PB-0113 rollover
records PB-0112 as `[x]` / 🟢 **DONE**, removes it from Active Work, and adds its single Completion
Log row. No exception was used.

## Version policy and schemas

| Contract | Schema identifier | Limit |
|---|---|---:|
| Request | `https://schemas.packagebuilder.dev/worker-request/v1` | 1,048,576 characters |
| Individual event | `https://schemas.packagebuilder.dev/worker-progress-event/v1` | 65,536 characters |
| Result | `https://schemas.packagebuilder.dev/worker-result/v1` | 1,048,576 characters |

All use JSON Schema Draft 2020-12, maximum nesting depth 64, and
`additionalProperties: false` at every owned object. Every standalone request, event, and result
contains integer `protocolVersion: 1`; version 1 is the only accepted version and unknown versions
fail closed. Duplicate properties are rejected recursively before schema evaluation. Nulls,
missing required properties, wrong types, unknown properties/tokens, malformed JSON, excessive
depth, and oversized input fail without filesystem or network access.

## Request structure

The stable ordered properties are `protocolVersion`, `jobId`, `operation`,
`productManifestReference`, `inputDirectoryReference`, `outputDirectoryReference`,
`resultFileReference`, optional `engineVersion`, and optional `target`. `jobId` reuses PB-0108
`BuildJobId`; `target`, when applicable, reuses `portable`, `unity`, or `unreal`. Operation is an
extensible lowercase single-hyphen identifier.

All four references are explicit logical references. They reject rooted, drive-qualified,
backslash, traversal, empty-segment, colon, control-character, `$`, and `%` forms. They do not
permit command lines, shell fragments, credentials, or environment expansion. This syntax check
does not prove an arbitrary path safe.

## Event structure and tokens

Every event has `protocolVersion`, `eventKind`, and `jobId`.

- `progress` adds required `stage`, optional human `message`, and optional finite `percent` from
  0 through 100. Omitted percent means indeterminate work.
- `finding` adds one PB-0109 `finding` object with the exact existing code, severity, explanation,
  source, optional related artifact, optional suggested action, and blocking semantics.
- `metric` adds `metricId`, finite numeric `value`, and required unit.

Exact event-kind tokens are `progress`, `finding`, and `metric`. Exact metric-unit tokens are
`milliseconds`, `bytes`, `count`, and `percent`. `Utf8JsonWriter` produces one compact object with
no embedded physical newline. PB-0209 owns reading the stream, line framing, and malformed-line
recovery.

## Result structure and semantic rules

The ordered result properties are `protocolVersion`, `jobId`, `status`, `workerVersion`, optional
`engineVersion`, `outputsPromoted`, `artifacts`, `findings`, `metrics`, `logReferences`,
`retrySafety`, and optional `cancellation`.

Exact result-status tokens are `success`, `failure`, and `cancelled`. Exact retry-safety tokens are
`safe`, `unsafe`, and `requires-cleanup`. Exact cancellation outcome tokens are `acknowledged` and
`partial`.

- Success requires `unsafe` retry safety, has no cancellation object, and cannot contain a
  release-blocking finding.
- Failure cannot claim promoted output and has no cancellation object. It may use any retry-safety
  token because failure can be safe, unsafe, or require cleanup.
- Cancelled is first-class, cannot claim promoted output, requires cancellation details, and uses
  `safe` or `requires-cleanup`.

Artifact IDs and metric IDs are unique. Findings are unique by code plus related artifact.
Artifact ownership must match the result job; related artifacts must exist in the result.
Collections are immutable snapshots with ordinal comparisons and deterministic retained order.
Logs are safe logical references, not opened paths. Expected failures use structured results
rather than raw exceptions or stack traces as the primary result error.

Artifacts reuse PB-0108 `BuildArtifactId`, `BuildJobId`, `BuildArtifactRole`, and target identity.
They carry a logical reference plus optional target, lowercase 64-character SHA-256, and
non-negative byte count. Hash and size are caller-supplied facts; PB-0112 reads no artifact and
calculates no hash.

## Responsibility boundaries

| Concern | Owner |
|---|---|
| Filesystem canonicalization, root containment, reparse checks, existence | PB-0201 |
| Streamed hashing and artifact identity verification | PB-0204 |
| External process invocation and arguments | PB-0207 |
| Cancellation signalling, timeout, forced termination, child cleanup | PB-0208 |
| JSON Lines framing, streaming read, malformed-line recovery | PB-0209 |
| Retry, resume, and orchestration execution | PB-0213 |

PB-0112 performs no filesystem access, networking, process execution, cancellation signalling,
cleanup, retry, persistence, engine work, or hashing. A cancellation object records worker result
state; it is not a serialized .NET `CancellationToken` and does not prove how a process was
signalled or terminated.

## Fixtures and tests

Golden fixtures cover a request; progress at 0, 35.5, 100, and indeterminate state; finding and
metric events; and success, failure, and cancellation results. Retained invalid fixtures cover
unknown versions, unknown and duplicate properties, invalid hashes, foreign artifact ownership,
blocking findings in success, and cancelled results claiming promoted completion.

Focused tests additionally cover all required properties at nested levels, forbidden nulls and
wrong types, all tokens, artifact presence/absence of hashes and sizes, hash casing/length/
characters, duplicates, missing artifact references, every retry-safety state and contradiction,
NaN and infinities, physical-newline/control injection, oversized/deep input, deterministic
repeated serialization, Turkish-culture independence, immutable snapshots, schema/C# parity, and
exact PB-0109 finding reuse.

## Dependency impact

No dependency changed. PB-0112 reuses System.Text.Json and centrally pinned JsonSchema.Net 9.3.0
(MIT). No additional schema or transport dependency was added, so third-party notices and package
versions are unchanged.

## Current local validation

| Validation | Result |
|---|---|
| Focused PB-0112 Contracts tests | Pass; 107 passed, 0 failed, 0 skipped |
| New production-file branch coverage | Pass; every branch in all seven new PB-0112 production files reports 100% in `artifacts/PB-0112/coverage-4/0249f11f-7ff3-4862-9c28-2551d3c8ea0f/coverage.cobertura.xml`; the focused suite passed again after repository formatting |
| PB-0109 regression | Pass; 33 Contracts and 78 Domain tests |
| Complete Contracts suite | Pass; 338 passed, 0 failed, 0 skipped |
| Complete Domain suite | Pass; 789 passed, 0 failed, 0 skipped |
| Application and Infrastructure suites | Pass; 1 test each |
| Complete four-project test baseline | Pass; 1,129 passed, 0 failed, 0 skipped |
| Locked restore | Pass with the pinned .NET SDK 10.0.302 |
| Debug and Release solution builds | Pass; 0 warnings and 0 errors |
| Formatting | Pass; .NET formatting, Ruff lint, and Ruff formatting |
| Solution architecture | Pass; 7 checks, 0 failures |
| ADR validation | Pass; 8 checks, 0 failures |
| Quality and release gates | Pass under Windows PowerShell 5.1 and PowerShell 7.6.4; 11 checks, 0 failures |
| Repository baseline | Pass; 29 checks, 0 failures |
| Dependency vulnerability audit | Pass; no vulnerable direct or transitive package in any of 15 projects |
| Complete local Core CI | Pass; all stages in 1 minute 43 seconds |

Coverage tooling note: after the repository formatter made analyzer-directed style-only edits, the
107 focused tests passed again. Repeated follow-up Coverlet/VSTest collection attempts completed
the tests successfully but emitted empty reports. The retained non-empty report above is therefore
the branch-coverage evidence; no coverage claim is derived from the empty follow-up reports.

## Publication and completion evidence

- Final task commit: `a22f913108870b24ead8dde38833ae3e56c02c47`.
- Pull request: [#26](https://github.com/avivperets26/3DModels-Package-Builder/pull/26).
- Successful PR workflow:
  [run 30155442329](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30155442329).
- Merge commit: `e8c31a221dedd49daac0e1e35d29fb5df2f43642`.
- Successful required main workflow:
  [run 30155444972](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30155444972).
- The user explicitly confirmed completion on 2026-07-25.
- No CI, completion, or quality exception was used.

PB-0112 is logically complete. The PB-0113 rollover marks it `[x]` / 🟢 **DONE**, removes it from
Active Work, and records it exactly once in the Completion Log.
