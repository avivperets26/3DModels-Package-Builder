# PB-0206 Atomic Release Promotion Evidence

**Task:** PB-0206 — Implement atomic release promotion
**Branch:** `feat/PB-0206-atomic-promotion`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0205 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `f8ea3330726ed314db0d51b8cc2d4e87290f4332` merged through
[pull request #34](https://github.com/avivperets26/3DModels-Package-Builder/pull/34) as
`9d8510480fdb5f1497ecc81530d11df8f206143c`.
[PR workflow run 30825282015](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30825282015)
and required
[main workflow run 30825288015](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30825288015)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-03. No exception was used.

PB-0206 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its
user-controlled publication, required `main` CI, explicit confirmation, and next-task rollover.

## Implemented boundary

- `IArtifactPromotionService` accepts typed job/artifact identity, the trusted project,
  artifacts and Builds roots, and an explicit UTC promotion timestamp.
- Promotion first asks PB-0205 to read and rehash the stored artifact. Only a `validated` record
  may begin; an existing `promoted` record may only resume from its matching journal.
- The artifact logical reference maps to a strict portable path beneath Builds. Windows device
  names, trailing dots/spaces, controls, reserved characters, rooted/traversal forms, and
  containment failures are rejected before output is created.
- Source bytes stream through a pooled 64 KiB buffer into the hidden same-volume
  artifact-root `.packagebuilder-promotion/{job-key}/{artifact-key}.partial` file, outside the
  Builds release tree but on the same volume. The job and artifact keys
  prevent same-ID collisions across jobs. The complete partial is rehashed and
  compared with the validated PB-0205 identity before one non-overwriting `File.Move` makes the
  final release visible atomically.
- Existing release paths are never overwritten. The original name is collision version 1;
  subsequent names use `Name (2).ext` through a bounded maximum of 10,000 versions. A collision
  that appears between selection and rename is detected and retried safely.
- A bounded strict version-one `promotion.json` journal is written beside PB-0205 metadata before
  copying. Restart resumes a valid partial, repairs a corrupt partial from the validated payload,
  completes a metadata transition after an already-finished atomic rename, and returns the same
  release instead of creating a duplicate.
- If interruption occurs before rename, no final release exists. If it occurs after rename but
  before metadata transition, the journal and content identity allow the next call to complete
  the transition. Changed promoted output, malformed/inconsistent journals, exhausted names,
  reparse boundaries, invalid roots, cancellation, and I/O fail closed through sanitized
  structured results.

PB-0206 does not delete promoted releases, clean stale job output, compose target packages,
orchestrate jobs, expose UI, or choose publisher/product/version defaults. PB-0214 owns cache
storage rather than release deletion; PB-0215 owns aggregate resource/concurrency policy;
PB-1506 retains the later cross-cutting destructive-target containment suite.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Only validated output is promoted | `OnlyValidatedArtifactPublishesAtomicallyAndTransitionsToPromoted` |
| Complete bytes become visible atomically | `OnlyValidatedArtifactPublishesAtomicallyAndTransitionsToPromoted`, `InterruptedCompletePartialResumesWithoutExposingPartialRelease` |
| Existing releases are never overwritten | `ExistingReleaseIsPreservedAndCollisionGetsDeterministicVersion`, `CollisionAppearingDuringPublishAdvancesJournalAndKeepsBothFiles` |
| Collision search is deterministic and bounded | `RecoveryAtMaximumCollisionVersionFailsWithBoundedDiagnostic`, `RaceAtMaximumCollisionVersionFailsWithoutOverwritingRelease`, `InitialCollisionSearchIsBounded` |
| Interrupted promotion recovers | `InterruptedAfterAtomicRenameRecoversWithoutDuplicateRelease`, `InterruptedCompletePartialResumesWithoutExposingPartialRelease`, `CorruptInterruptedPartialIsRebuiltFromValidatedPayload` |
| Completed release integrity is retained | `CompletedPromotionIsIdempotentAndDetectsChangedRelease`, `MissingJournalForPromotedArtifactFailsClosed` |
| Strict portable paths and contained roots | `NonPortableLogicalReferencesFailBeforeBuildOutput`, `InvalidRootRelationshipsFailClosed`, `MissingProjectAndInvalidCanonicalSyntaxFailBeforeStoreAccess`, `ReparseBuildBoundaryIsRejectedBeforePublication` |
| Journal syntax and semantic facts fail closed | `HostileJournalSyntaxFailsClosed`, `InconsistentJournalFactsFailClosed`, `InvalidOrInconsistentJournalFailsClosed` |
| Interrupted journal replacement recovers | `InterruptedJournalWriteIsReplacedAtomicallyOnRetry` |
| Cancellation/I/O/hash failures expose no partial release | `InterruptedCompletePartialResumesWithoutExposingPartialRelease`, `FilesystemCollisionAtBuildRootReturnsSanitizedIoFailure`, `HashFailureAfterCopyDoesNotExposeARelease` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0206 tests | Pass; 59 passed, 0 failed, 0 skipped |
| Critical production coverage | Pass; all 21 new executable production/compiler-generated components report 100% line and branch coverage in the Microsoft Cobertura report beneath `artifacts/PB-0206/coverage-final` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,512 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 286, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed on the exact final worktree |
| Formatting and repository safety | Pass; .NET info-level and Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

No dependency, engine, network, paid service, telemetry, or quality-threshold change is included.
Generated test and coverage output remains ignored beneath the single project root.

## Manual and visual testing

PB-0206 has no WPF screen or renderer, so there is no end-user visual test yet. Its focused tests
perform real contained filesystem publication, collision, tampering, interruption, resume,
reparse, and bounded-exhaustion scenarios. The first supported visual workflow remains the later
WPF vertical slice; no package preview is claimed here.

## Remaining gates

Final exact-worktree validation, user-controlled commit and branch push, merge into and push of
`main`, successful required `main` CI, explicit completion confirmation, and PB-0207 rollover
remain.
