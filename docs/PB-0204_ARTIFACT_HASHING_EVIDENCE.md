# PB-0204 Streamed Artifact Identity Evidence

**Task:** PB-0204 — Implement streamed hashing and artifact identity
**Branch:** `feat/PB-0204-artifact-hashing`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0203 is `[x]` / 🟢 **DONE**, removed from Active Work, and recorded exactly once in the
Completion Log. Final task commit `0cb2b033ffe3f35cad411aea8d79023d7d8e1123` merged through pull
request #32 as `f9d99c9f9f400c46c2c0ba3e25a8983b946b5c3`; corrected pull-request
workflow run `30817505318` and required `main` workflow run `30817611922` succeeded. The user
explicitly confirmed completion on 2026-08-03. No exception was used.

PB-0204 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its implementation,
user-controlled publication, required `main` CI, confirmation, and next-task rollover gates pass.

## Implemented boundary

The contracts define:

- canonical lowercase 64-character SHA-256 digests;
- immutable content identities combining exact byte length and digest;
- logical artifact hash requests and receipts;
- structured, sanitized expected-operation failures; and
- deterministic duplicate groups that keep logical artifact identity separate from content.

The physical service validates absolute canonical paths and strict project containment, rejects
reparse-point boundaries, opens the file without write/delete sharing, and hashes asynchronously
with one pooled 64 KiB buffer. Reported length is checked before and throughout the stream. The
service never loads the complete file, never modifies source bytes, and returns safe failures for
cancellation, access, I/O, size overflow, and detected source changes.

Duplicate detection operates only on completed receipts. Equal size and SHA-256 values group as
duplicates even when physical paths or logical artifact IDs differ. Duplicate logical IDs are
rejected, unique identities are omitted, and groups/members use deterministic ordinal ordering.

Artifact-store layout and metadata remain PB-0205. Cleanup/recovery remains PB-0214 and aggregate
resource defaults remain PB-0215. No cache, persistence, UI, engine, marketplace, or network
behavior is introduced here.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Known SHA-256 and unchanged source | `PhysicalHashMatchesSha256AndDoesNotModifySource` |
| Large files use bounded streaming | `LargeInputUsesBoundedStreamingReads` |
| Same bytes at different paths are duplicates | `SameBytesAtDifferentPathsProduceDuplicateIdentity` |
| Canonical content identity and stable equality/hash | `Sha256RequiresCanonicalLowercaseHex`, `IdentityEqualityAndHashingAreDeterministicAndTypeSafe` |
| Deterministic duplicate groups | `EqualContentIsGroupedDeterministicallyWhileDifferentContentRemainsSeparate` |
| Containment and reparse boundaries | `FileOutsideProjectIsRejected`, `ReparsePointsAreRejectedAcrossTheCompletePath` |
| Changes, cancellation, and physical failures fail closed | `LengthChangesDuringStreamingAreRejected`, `CancellationReturnsStructuredFailure`, `PhysicalFailuresAreSanitized` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0204 tests | Pass; 35 passed, 0 failed, 0 skipped |
| Critical production coverage | Pass; all 17 instrumented executable artifact classes/compiler-generated components report 100% line and branch coverage in the Microsoft Cobertura report beneath `artifacts/PB-0204/ms-coverage-formatted` |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,409 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 183, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 3m 18.0s on the exact formatted implementation |
| Formatting and repository safety | Pass; .NET/Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

Coverlet Collector 10.0.1 emitted an empty report for this focused .NET 10 run. The repository's
already available Microsoft `Code Coverage;Format=Cobertura` collector produced the complete
instrumented report without adding or changing a dependency. No coverage threshold was waived.

The first full Core CI attempt stopped at required info-severity .NET style findings in the new
code and tests. The repository formatter applied only those mechanical corrections; focused tests
and coverage were rerun unchanged, and the exact complete nine-stage pipeline then passed. No
behavior, test, analyzer, or release threshold was weakened.

## Manual and visual testing

PB-0204 has no UI or renderer, so there is no end-user visual test yet. The automated large-file
test proves bounded reads and duplicate detection. A developer may also run the focused test suite
and inspect its ignored evidence beneath `artifacts/PB-0204`; no supported packaging preview is
claimed. The first genuinely visual test remains the later WPF/preview vertical slice.

## Remaining gates

Final exact-worktree validation, user-controlled commit and branch push, merge into and push of
`main`, successful required `main` CI, explicit user confirmation, and PB-0205 rollover remain.
