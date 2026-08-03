# PB-0205 Artifact Store Evidence

**Task:** PB-0205 — Implement artifact-store layout and metadata  
**Branch:** `feat/PB-0205-artifact-store`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0204 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `e04d8ffbcf796be18601a79b51855a84c20a56a8` merged through
[pull request #33](https://github.com/avivperets26/3DModels-Package-Builder/pull/33) as
`bc500825c93fbcbe16a65b92728dda2424a248ee`. PR workflow run `30821592450` and required
[main workflow run 30821598836](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30821598836)
succeeded. The user explicitly confirmed completion on 2026-08-03. No exception was used.

PB-0205 final task commit `f8ea3330726ed314db0d51b8cc2d4e87290f4332` merged through
[pull request #34](https://github.com/avivperets26/3DModels-Package-Builder/pull/34) as
`9d8510480fdb5f1497ecc81530d11df8f206143c`.
[PR workflow run 30825282015](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30825282015)
and required
[main workflow run 30825288015](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30825288015)
completed successfully. The user explicitly confirmed completion on 2026-08-03. No exception was
used. This PB-0206 rollover marks PB-0205 `[x]` / 🟢 **DONE**, removes it from Active Work, and
adds exactly one chronological Completion Log row.

## Implemented boundary

- `ArtifactStoreLayoutFactory` converts ordinal typed job/artifact identities to independent
  lowercase SHA-256 keys; raw identifiers never become path segments.
- Every entry uses `Jobs/{job-key}/{artifact-key}/payload` and a bounded, strict version-one
  `artifact.json` document beneath the configured contained artifact root.
- Metadata retains typed job, artifact, step, role, optional target, logical reference, exact byte
  length, canonical SHA-256, lifecycle state, UTC timestamps, and the canonical relative payload
  reference.
- `ArtifactStore` stages with bounded asynchronous copying, then reuses PB-0204 streamed hashing.
  Reads reparse-check the complete path, strictly decode metadata, confirm typed path identity,
  and rehash the payload so tampering fails closed.
- Structured failures cover validation, containment, collision, cancellation, integrity, state
  conflict, and sanitized filesystem errors.
- Lifecycle changes use optimistic expected-state checks and permit only staged-to-validated and
  validated-to-promoted transitions with monotonic UTC timestamps.

PB-0206 retains physical atomic promotion into `Builds`. PB-0214 retains cleanup/recovery and
cache behavior. PB-0215 retains disk quotas, concurrency, and aggregate resource guards. No
deletion, network, database, engine, UI, preview, marketplace, or package-composition behavior is
introduced by PB-0205.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Safe typed deterministic layout | `LayoutUsesDeterministicSafeKeysInsteadOfRawIdentities`, `OrdinallyDifferentIdentitiesCannotCollideOnWindows` |
| Hash, size, role, path, and state metadata | `StagePersistsPayloadTypedMetadataHashSizeRoleAndLifecycle` |
| Integrity-checked reads | `ReadRevalidatesMetadataIdentityAndPayloadIntegrity`, `ReadRejectsChangedPayloadAndMismatchedMetadataIdentity` |
| Strict hostile metadata rejection | `ReadRejectsHostileOrUnsupportedMetadata` |
| Approved lifecycle and optimistic state | `LifecycleAdvancesOnlyThroughApprovedOptimisticTransitions`, `LifecycleRejectsUnapprovedTransitions`, `LifecycleRejectsStateConflictAndInvalidTimes` |
| Containment and reparse defense | `StoreRejectsRootsOutsideProjectAndMissingProject`, `StoreRejectsMissingOutsideAndStoreContainedSources`, `SourceSymbolicLinkIsRejectedAsAReparseBoundary` |
| Cancellation and sanitized I/O | `CancellationReturnsStructuredFailure`, `ReadAndTransitionCancellationReturnStructuredFailures`, `LockedFilesAndBlockedMetadataReplacementReturnIoFailures` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0205 tests | Pass; 44 passed, 0 failed, 0 skipped |
| Critical production coverage | Pass; all 23 instrumented executable artifact-store classes/compiler-generated components report 100% line and branch coverage in the Microsoft Cobertura report beneath `artifacts/PB-0205/coverage-final-formatted` |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,453 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 227, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 2m 54.6s on the exact formatted implementation |
| Formatting and repository safety | Pass; .NET info-level and Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

The first full Core CI attempt exposed only required info-severity .NET style findings. The
formatter was rerun with the repository's explicit `info` severity, the remaining private-static
naming rules were corrected, and formatting verification then passed with zero findings. Focused
tests and 100% line/branch coverage were rerun on the formatted source before the complete
nine-stage pipeline passed. No behavior, test, analyzer, or threshold was weakened. Evidence
remains ignored beneath the single project root.

## Manual and visual testing

PB-0205 has no WPF screen or renderer, so there is no end-user visual test yet. The focused tests
physically create, read, tamper with, and transition disposable artifact entries beneath the
project-contained ignored evidence root. The first supported visual workflow remains the later
WPF vertical slice; no preview or package-build UI is claimed here.

## Completion

No PB-0205 gate remains. PB-0206 owns the physical atomic release-promotion boundary without
reopening the completed artifact-store task.
