# PB-0202 Safe Archive Inspection and Extraction Evidence

**Task:** PB-0202 — Implement safe archive inspection and extraction
**Branch:** `security/PB-0202-safe-archive-extraction`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0202 implements a typed, dependency-free ZIP preflight and extraction boundary. PB-0201 is
`[x]` / 🟢 **DONE**, removed from Active Work, and recorded exactly once in the Completion Log
using corrective commit `ada453196578c7233e5545451e5a44f792d0ec9c`, pull request #30, merge
`dccb1696baa742c5024344ad8802b82a25a1b342`, and successful required `main` workflow
`30807459397`.

PB-0202 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until the user-controlled
publication, required `main` CI, confirmation, and next-task rollover gates pass.

## Design

`ArchiveSafetyPolicy` requires every numeric quota and extension rule from the caller. The archive
service does not invent product-type limits. It validates the project root, source ZIP,
destination containment root, and new dedicated destination before opening the archive. Existing
path components are inspected for reparse points, and the exact destination is checked again
after preflight and immediately while its directory tree is created.

Inspection completes before the destination directory is created. It rejects:

- traversal, absolute/rooted paths, empty segments, Windows-ambiguous aliases, control or unsafe
  characters, and reserved device names;
- Unix links/special files, Windows reparse metadata, and path/type metadata conflicts;
- case-insensitive or separator-equivalent duplicate targets and file/directory prefix collisions;
- unapproved or extensionless content unless the active policy explicitly allows it;
- entry-count, path-depth, archive-size, per-entry expanded-size, total expanded-size, compressed
  size, and projected expansion-ratio violations;
- corrupt or unsupported archive structures and inconsistent content-size metadata.

Every file destination is canonicalized beneath the new dedicated destination. Extraction reuses
the same locked source stream and preflight plan, creates files with create-new semantics, and
copies through one bounded 65,536-byte buffer. It never executes extracted content, never writes
to the source, never uses a user-profile or system-temp fallback, and never deletes unrelated
content. A failed partial dedicated destination is reported for later job-owned cleanup; PB-0206
and PB-0214 retain cleanup and atomic-promotion ownership.

The source handle denies write and delete sharing while the operation is active. This narrows
same-file replacement risk, but it does not claim protection from a privileged concurrent actor
that can replace an ancestor after validation. PB-1501, PB-1502, and PB-1506 retain the broader
adversarial and destructive-target suites.

## API and implementation

- Contracts: `PackageBuilder.Contracts.Archives` provides the request, explicit safety policy,
  immutable inspection plan, extraction receipt, structured failures, service boundary, and
  filesystem boundary.
- Infrastructure: `SafeZipArchiveService` provides preflight and extraction;
  `PhysicalArchiveFileSystem` provides locked asynchronous physical I/O.
- Dependencies: none added; implementation uses pinned .NET 10 LTS platform APIs.
- Result semantics: expected invalid or unsafe input returns stable structured failure codes with
  logical locations and sanitized diagnostics.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Valid streaming inspection/extraction and unchanged source | `ValidArchiveIsInspectedThenStreamedIntoDedicatedDestination` |
| Traversal, rooted, alias, reserved, and unsafe names | `UnsafeEntryPathsFailBeforeDestinationCreation`, `EmptyAndWindowsUnsafeEntryNamesAreRejected` |
| Link, reparse, special, and conflicting metadata | `UnixLinkSpecialAndConflictingTypeMetadataAreRejected`, `WindowsReparseMetadataIsRejected`, `ExistingDestinationAncestorReparsePointIsRejected` |
| Duplicate and file/directory collisions | `CaseAndSeparatorEquivalentTargetsAreRejected`, `FileDirectoryPrefixCollisionsAreRejectedInEitherOrder` |
| Explicit extension policy | `ExtensionPolicyRejectsUnexpectedContentBeforeWrites`, `ArchiveSafetyPolicyTests` |
| Count, depth, byte, compressed-size, and ratio quotas | `EntryCountDepthEntrySizeTotalSizeAndRatioLimitsFailClosed`, `ImpossibleCompressedSizeMetadataFailsPreflight` |
| Corrupt or inconsistent ZIP content | `CorruptArchiveReturnsStructuredFailureWithoutCreatingDestination`, `CentralDirectorySizeTamperingFailsPreflight` |
| Boundary containment and destination non-reuse | `RequestValidationRejectsUnsafeBoundariesAndNonZipSources`, `DestinationAppearingAfterPreflightIsRejectedBeforeWrites` |
| Filesystem races and failures fail closed | `SafeZipArchiveServiceFailureTests` |
| Bounded-copy independent limits | `BoundedReaderEnforcesMetadataEntryAndTotalLimitsIndependently` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0202 tests | Pass; 73 passed, 0 failed, 0 skipped |
| Critical archive production coverage | Pass; every one of the 19 instrumented archive contract/infrastructure classes reports 100% branch coverage in `artifacts/PB-0202/coverage-debug-final-3/7cc91a09-9ce7-45d9-a7c8-9a79f0bec9a6/coverage.cobertura.xml` |
| Repository baseline | Pass; 29 checks, 0 failures |
| Locked restore | Pass for all 15 projects |
| Release solution build | Pass; 0 warnings and 0 errors |
| Complete core tests | Pass; 1,312 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 86, Contracts 384 |
| Full local Core CI | Pass; all 9 stages completed in 2m 51s on the exact final implementation |
| Formatting | Pass for .NET and Ruff; verification is non-mutating |
| Repository/public-content checks | Pass; lifecycle, task graph, links, Git diff, history, secrets, personal paths, prohibited/generated content, and ignore policy |
| Source immutability | Pass; the valid extraction test compares the source SHA-256 before and after extraction |

Focused reports, logs, and generated outputs remain beneath ignored `artifacts/PB-0202` or
existing ignored build/runtime directories. No dependency or lock-file change was required.

## Manual visual testing

Not applicable. PB-0202 is filesystem/security infrastructure and adds no UI or rendered output.
The first useful visual manual workflow remains deferred until the desktop workflow and preview
pipeline exist.

## Remaining gates

User-controlled commit and task-branch push, merge into and push of `main`, successful required
`main` CI, explicit user confirmation, and PB-0203 rollover remain.
