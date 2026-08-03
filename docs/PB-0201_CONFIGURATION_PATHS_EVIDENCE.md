# PB-0201 Configuration and Path-Root Validation Evidence

**Task:** PB-0201 — Implement configuration loading and path-root validation  
**Branch:** `feat/PB-0201-configuration-paths`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0201 implements immutable typed configuration roots, deterministic strict JSON loading,
canonical Windows path containment, approved root hierarchy and collision rules, sanitized
structured failures, and filesystem-specific reparse-point inspection.

The PB-0113 rollover records both publication cycles. Original task commit
`94ba52d85255649f8fd003b31943eef241431263` merged through pull request #27 as
`86a1f33cd33e38ab054eb5e47a41147a849259bd`; PR run `30157194135` and original required `main` run
`30157196092` succeeded. Under the explicitly approved one-time PB-0113-only exception,
corrective commit `db481f0b7af894534f854e7b890a10ed185ffcdb` merged directly into `main` as
`64eddf0b5c8af839567796b87640a0d8119eeef5`. Corrected required
[main workflow run 30162027888](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30162027888)
was independently verified successful for that exact merge. The user confirmed corrected green
CI and PB-0113 completion on 2026-07-25. PB-0113 is `[x]` / 🟢 **DONE**, removed from Active Work,
and recorded exactly once in the Completion Log. The exception creates no precedent.

## GitHub CI corrective cycle

Original PB-0201 task commit `e64ca3a3b249163b96d08554d7ef59fa1f0c44c7` was merged through
[pull request #28](https://github.com/avivperets26/3DModels-Package-Builder/pull/28) as
`c89d5afab69540e640e7f4d1dfd2215c64015ada`. Both
[PR workflow run 30167796737](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30167796737)
and required
[main workflow run 30167799680](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30167799680)
failed in `RelativeResolutionDoesNotDependOnCurrentWorkingDirectory`. The test assigned
`Environment.CurrentDirectory` to `C:\Dev\PackageBuilder\runtime-data`; that directory existed
on the development machine but not in GitHub's `D:\a\...` checkout, so
`Environment.set_CurrentDirectoryCore` threw `DirectoryNotFoundException` before configuration
resolution was exercised. The repository baseline, restore, Release build, formatting, Ruff, 789
Domain tests, 52 other Application tests, 13 Infrastructure tests, and 384 Contract tests passed;
the failure was isolated to this non-portable test precondition.

The correction uses the existing repository/test-contained `AppContext.BaseDirectory`, verifies
that it exists and differs from the approved project root using ordinal case-insensitive Windows
semantics, preserves the nonparallel collection, and restores the original current directory in
`finally`. No production behavior, dependency, workflow, configuration contract, or security
rule changed.

On 2026-08-03 the user explicitly approved a one-time PB-0201 corrective-publication exception
because PR #28 had already merged before its failing checks completed. The exception is limited
to `PackageBuilderPathConfigurationLoaderTests.cs`, this evidence document, and PB-0201 Active
Work synchronization in `IMPLEMENTATION_BACKLOG.md`; it adds no feature scope and creates no
precedent. PB-0201 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until the
correction is published, required corrected `main` CI succeeds, and the user confirms completion.

## Configuration decision

No existing plan, architecture section, ADR, schema, or backlog entry approved a filename,
precedence chain, environment override, or serialization shape. PB-0201 therefore selects the
smallest repository-contained design:

- one file: `C:\Dev\PackageBuilder\packagebuilder.paths.json`;
- strict JSON with exact integer `schemaVersion` 1 and exact `roots` object;
- no environment, command-line, AppData, user-profile, registry, current-directory, or
  machine-level override precedence;
- relative values resolve only against the approved repository root;
- maximum UTF-8 size 65,536 bytes and maximum JSON depth 8;
- comments, trailing commas, duplicate properties, unknown properties, missing properties, wrong
  types, malformed JSON, and unsupported versions fail closed;
- loading is read-only and creates or repairs nothing.

No new NuGet or other third-party dependency was added. `System.Text.Json`, `System.IO`, and
platform APIs from the pinned .NET 10 LTS runtime are sufficient. No ADR inventory change is
required because this fills an explicitly deferred PB-0201 implementation detail without
superseding an accepted architectural decision.

## Root mapping

| Typed root | JSON property | Configured value | Canonical mapping |
|---|---|---|---|
| Repository | `repository` | `C:\Dev\PackageBuilder` | `C:\Dev\PackageBuilder` |
| Tools | `tools` | `tools` | `C:\Dev\PackageBuilder\tools` |
| Downloads | `downloads` | `downloads` | `C:\Dev\PackageBuilder\downloads` |
| Data | `data` | `runtime-data` | `C:\Dev\PackageBuilder\runtime-data` |
| Source assets | `sourceAssets` | `runtime-data\source-assets` | `C:\Dev\PackageBuilder\runtime-data\source-assets` |
| Jobs | `jobs` | `runtime-data\jobs` | `C:\Dev\PackageBuilder\runtime-data\jobs` |
| Cache | `cache` | `runtime-data\engine-caches` | `C:\Dev\PackageBuilder\runtime-data\engine-caches` |
| Temp | `temp` | `runtime-data\temp` | `C:\Dev\PackageBuilder\runtime-data\temp` |
| Templates | `templates` | `runtime-data\engine-templates` | `C:\Dev\PackageBuilder\runtime-data\engine-templates` |
| Builds | `builds` | `artifacts\Builds` | `C:\Dev\PackageBuilder\artifacts\Builds` |
| Artifacts | `artifacts` | `artifacts` | `C:\Dev\PackageBuilder\artifacts` |
| Logs | `logs` | `logs` | `C:\Dev\PackageBuilder\logs` |

The data children must be strict, distinct descendants of Data; Builds must be a strict
descendant of Artifacts. Tools, Downloads, Data, Artifacts, and Logs must be distinct siblings
without ancestor relationships. All non-repository roots require a dedicated child directory.

## Security behavior

- Converts `/` to the Windows separator and resolves full paths against the approved root, never
  the process current directory.
- Uses `StringComparison.OrdinalIgnoreCase` and an explicit separator boundary rather than a
  vulnerable string-prefix containment test.
- Rejects `.`/`..` escapes, sibling-prefix attacks, other drives, drive-relative syntax, UNC,
  device and extended-length prefixes, rooted separator-only values, alternate data streams,
  invalid/control characters, unresolved placeholders, whitespace-only values, and paths outside
  the approved project root.
- Uses immutable `ConfiguredPathRoot` values with typed `PathRootKind`, ordinal case-insensitive
  equality, and stable culture-independent hashing.
- Returns stable failure codes, logical property names, and sanitized actionable diagnostics
  without echoing untrusted path values.
- Keeps cross-layer read/reparse results and interfaces in Contracts, physical attribute access
  behind Infrastructure `IFileAttributeReader`, and reparse traversal behind
  `IReparsePointInspector`; pure parsing and canonical containment have no UI, Blender, Unity,
  Unreal, marketplace, persistence, or worker dependency.
- Rejects any existing reparse point crossed by a configured root. This also rejects a
  nonexistent descendant when its nearest existing ancestor is a link or junction.

### Residual limitation

Validation is a point-in-time check. It does not provide complete protection against a privileged
or concurrent actor replacing a validated directory with a junction, symbolic link, or other
reparse point between validation and a later filesystem operation. Later PB owners must revalidate
the exact operation path immediately before use and apply handle/operation-specific controls.
PB-0201 does not claim elimination of filesystem time-of-check/time-of-use races.

## Requirements-to-tests traceability

| PB-0201 criterion | Automated evidence |
|---|---|
| Typed normalized mapping and immutable values | `ValidConfigurationCreatesEveryTypedCanonicalRoot`, `ResultAndRootCollectionsAreImmutableSnapshots` |
| Relative/absolute, dot segments, case, separators, CWD independence | `AbsoluteRelativeDotSegmentsCaseAndTrailingSeparatorsNormalizeDeterministically`, `RelativeResolutionDoesNotDependOnCurrentWorkingDirectory` |
| Boundary containment and sibling-prefix defense | `TraversalSiblingPrefixSystemAndOtherDriveEscapesAreRejected` |
| Windows unsafe syntax matrix | `DeviceAndExtendedPrefixesAreRejected`, `UncPathsAreRejected`, `SeparatorOnlyRootIsRejected`, `RootedSeparatorPathIsRejected`, `DriveRelativePathIsRejected`, `AlternateDataStreamSyntaxIsRejected`, `InvalidAndControlCharactersAreRejected` |
| Missing, malformed, duplicate, unknown, wrong-type, size/depth input | configuration structure and oversized-content tests in `PackageBuilderPathConfigurationLoaderTests` |
| Approved hierarchy, collisions, project-root child rejection | `DuplicateCanonicalRootsAreRejected`, `RequiredChildHierarchyIsEnforced`, `UnapprovedParentAndSiblingNestingIsRejected`, `DedicatedWritableRootCannotBeProjectRoot` |
| Culture-independent equality and hashing | `ConfiguredRootEqualityAndHashingAreOrdinalCaseInsensitiveAndKindSensitive`, `EqualityHashingAndNormalizationAreCultureIndependent` |
| Read-only loading and structured read failures | `LoadReadsOnlyTheFixedRepositoryConfigurationFile`, `RepositoryConfigurationLoadsThroughPhysicalImplementationsWithoutWrites`, `FileConfigurationTextReaderTests` |
| Existing and ancestor reparse behavior | `ExistingReparsePointAndItsNonexistentDescendantAreRejected`, `ExistingPhysicalAndNonexistentDescendantPathsAreAccepted`, `AttributeInspectionFailureReturnsSanitizedFailure` |

## Explicit non-goals

PB-0201 performs no directory creation, repair, deletion, cleanup service, archive inspection or
extraction, source snapshot, hashing, caching, process execution, persistence, package building,
engine integration, marketplace behavior, UI, or PB-0202 work.

## Validation results

| Validation | Result |
|---|---|
| Focused PB-0201 Application tests | Pass; 52 passed, 0 failed, 0 skipped in Release |
| Focused PB-0201 Infrastructure tests | Pass; 12 passed, 0 failed, 0 skipped in Release, including physical symbolic-link behavior |
| Complete affected Application suite | Pass; 53 passed, 0 failed, 0 skipped |
| Complete affected Infrastructure suite | Pass; 13 passed, 0 failed, 0 skipped |
| Complete core test suite | Pass through `Invoke-CoreCi.ps1`; 1,239 discovered and passed: Domain 789, Application 53, Infrastructure 13, Contracts 384; 0 failed, 0 skipped |
| New production-file coverage | Pass; every executable PB-0201 Application, Contracts, and Infrastructure configuration file reports 100% line and 100% branch coverage in `artifacts/PB-0201/coverage-final10-application` and `artifacts/PB-0201/coverage-final6-infrastructure` |
| Debug and Release builds | Pass; exact 15-project solution, 0 warnings and 0 errors in both configurations |
| Solution architecture | Pass; 15 projects, 7 checks, 0 failures; inward-only graph preserved |
| ADR validation | Pass; 8 checks, 0 failures; no ADR inventory change required |
| Quality/release gates | Pass under Windows PowerShell 5.1 and repository-local PowerShell 7.6.4; 11 checks, 0 failures in each |
| Repository baseline with `RequireTrackedFiles` | Pass through final Core CI; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages, including locked restore, Release build, .NET/Ruff formatting, and all four test projects |
| Locked restore | Pass for all 15 projects |
| Vulnerability audit | Pass; no vulnerable direct or transitive packages reported for any of the 15 projects |
| Formatting | Pass; `dotnet format --verify-no-changes --severity info`; Ruff lint/format pass through Core CI |
| Dependency change | None; no package version, direct dependency, or transitive dependency changed |

### Corrective validation

| Validation | Result |
|---|---|
| Original PR and `main` CI | Failed; runs `30167796737` and `30167799680` each reached the test stage and failed only the hard-coded CWD precondition |
| Previously failing test after correction | Pass; 1 passed, 0 failed, 0 skipped |
| Complete Application suite after correction | Pass; 53 passed, 0 failed, 0 skipped |
| Complete core suite after correction | Pass; 1,239 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 13, Contracts 384 |
| Full local Core CI after correction | Pass; all 9 stages in 3m 39s, including repository baseline, locked restore, Release build, .NET/Ruff formatting, and all four test projects |
| Debug and Release builds after correction | Pass; exact 15-project solution, 0 warnings and 0 errors in each configuration |
| Relevant corrective coverage | Pass; the four executable Application configuration files remain at 100% line and 100% branch coverage in `artifacts/PB-0201/coverage-corrective-application` |
| Repository and content validation | Pass through Core CI; repository baseline, Git diff, secrets, personal paths, prohibited/generated content, lifecycle, links, and history checks succeeded |
| Vulnerability audit after correction | Pass; no vulnerable direct or transitive package reported for any of the 15 projects |
| Dependency and production-code change | None; the correction changes only one test and two synchronized PB-0201 documents |

## Remaining gates

- User-controlled corrective staging and commit, task-branch push, corrective merge into and push
  of `main`, successful required corrected `main` CI, and explicit completion confirmation.

## Manual visual testing

PB-0201 is configuration and path-security infrastructure and does not yet provide a meaningful
visual workflow to test manually.
