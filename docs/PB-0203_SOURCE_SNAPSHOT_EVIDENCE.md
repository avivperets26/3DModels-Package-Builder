# PB-0203 Immutable Source Snapshot Evidence

**Task:** PB-0203 — Implement immutable source snapshots  
**Branch:** `feat/PB-0203-source-snapshots`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0202 is `[x]` / 🟢 **DONE**, removed from Active Work, and recorded exactly once in the
Completion Log. Task commit `8fb5730fa345e6e535167ea3abe725ed348cd361` merged through pull
request #31 as `69e667c948ff8b9bf56ea5b26e814fbc9dd03343`; required `main` workflow
run `30810800726` succeeded, and the user explicitly confirmed completion on 2026-08-03. No
exception was used.

PB-0203 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its local,
user-controlled publication, required `main` CI, confirmation, and next-task rollover gates pass.

## Implemented boundary

The snapshot API accepts an existing trusted project root, accepted source directory, dedicated
job root, new snapshot destination, and caller-supplied limits. It returns structured expected
failures or an immutable receipt containing deterministic portable paths, byte counts, and
lowercase SHA-256 digests.

The physical implementation:

- preflights the complete tree before destination creation;
- requires canonical strict containment and rejects source/job overlap;
- rejects reparse points, unsafe portable names, missing roots, reused destinations, and quota
  violations;
- rechecks the new destination and each source as close to use as practical;
- opens sources without write/delete sharing and creates outputs with create-new semantics;
- copies and hashes through one bounded 64 KiB asynchronous stream;
- detects size changes between preflight and copy;
- marks successful snapshot files read-only; and
- never writes to the accepted source directory.

Hard links are intentionally not used. Generic streamed artifact identity and duplicate-content
detection remain PB-0204. Artifact-store metadata, cleanup/recovery, and product quota defaults
remain PB-0205, PB-0214, and PB-0215 respectively.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Nested safe copy, deterministic receipt, and hashes | `CopiesNestedSourcesRecordsHashesAndLeavesOriginalsUnchanged` |
| Originals remain byte-for-byte unchanged | `CopiesNestedSourcesRecordsHashesAndLeavesOriginalsUnchanged` |
| Successful snapshot is read-only | `PhysicalSnapshotCannotBeOverwrittenAfterSuccess` |
| Canonical project/job containment and no overlap | `RootsOutsideApprovedBoundariesAreRejected`, `SourceAndJobOverlapIsRejected` |
| Dedicated destination and race protection | `ExistingDestinationIsRejectedBeforeWrites`, `DestinationAppearingAfterPreflightIsRejected` |
| Explicit bounded resource policy | `ExplicitLimitsAreEnforcedBeforeDestinationCreation`, `SourceSnapshotPolicyTests` |
| Source mutation, cancellation, and I/O failures fail closed | `SourceLengthChangeAfterPreflightIsRejected`, `CancellationReturnsStructuredFailure`, `IoFailureReturnsSanitizedFailure` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0203 tests | Pass; 62 passed, 0 failed, 0 skipped |
| Critical snapshot coverage | Pass; every new contract and infrastructure class reports 100% line and branch coverage in the generated Cobertura report beneath `artifacts/PB-0203/coverage-final-7` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,374 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 148, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 2m 24.7s on the exact final implementation |
| Formatting and repository safety | Pass; .NET/Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

The first two full-pipeline attempts correctly stopped at informational .NET analyzer findings in
new tests. The repository formatter was rerun at the CI-required `info` severity, the remaining
xUnit enum assertion was corrected manually, and the exact complete pipeline then passed. No
production behavior or gate was weakened. Generated reports remain beneath ignored
`artifacts/PB-0203`.

## Manual and visual testing

PB-0203 has no UI or renderer. A developer can inspect a test-created job snapshot, its read-only
files, and recorded hashes at the filesystem level, but there is not yet a supported end-user
manual command or visual 3D preview. The first genuinely visual test remains the desktop/preview
slice; this task must not claim that milestone early.

## Remaining gates

Final local validation, user-controlled commit and branch push, merge into and push of `main`,
successful required `main` CI, explicit user confirmation, and PB-0204 rollover remain.
