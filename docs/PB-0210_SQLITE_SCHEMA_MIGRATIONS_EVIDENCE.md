# PB-0210 SQLite Schema and Migration Evidence

**Task:** PB-0210 — Implement SQLite schema and migrations
**Branch:** `feat/PB-0210-sqlite-schema`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0209 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `67d8e3027bd16f05d66ee40f67c6faa0082b9291` merged through
[pull request #38](https://github.com/avivperets26/3DModels-Package-Builder/pull/38) as
`ade315b13accf75f65d739d689eed7e5cfa44473`.
[PR workflow run 30892047009](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30892047009)
and required
[main workflow run 30892053123](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30892053123)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-04. No exception was used.

PB-0210 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log by the PB-0211 rollover. Final task commit
`131342a0eb5adc23e11af4657b33515b51863ac5` merged through
[pull request #39](https://github.com/avivperets26/3DModels-Package-Builder/pull/39) as
`e2ceb8c90162b543413c917f98de23765eb0cbf8`. Required
[main workflow run 30897157981](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30897157981)
completed successfully for that exact merge, and the user explicitly confirmed completion on
2026-08-04. No exception was used.

## Implemented persistence boundary

- `SqliteDatabaseMigrator` owns initialization and schema upgrades only. PB-0211 remains the
  owner of job, artifact, tool, and finding CRUD repositories.
- SQLite `PRAGMA user_version` is the single schema-version source. Version 1 creates exactly the
  eleven table families approved by the architecture: Products, ProductVersions,
  PublisherProfiles, BuildJobs, BuildSteps, Artifacts, ValidationFindings, ToolInstallations,
  EngineVersions, RequirementsProfiles, and Settings.
- The schema uses SQLite `STRICT` tables, foreign keys, canonical state checks, uniqueness checks,
  nonnegative size/order checks, release-blocking booleans, and indexes for relationship/state
  queries.
- Every table, index, and the version promotion are committed in one transaction. A conflicting
  partial schema demonstrates that failure rolls all new objects back and leaves `user_version`
  unchanged.
- Existing version-0 databases receive a consistent SQLite online backup before migration. The
  backup is integrity checked, uses a contained logical reference, never overwrites an existing
  backup, and remains available when migration fails.
- Current version-1 databases are idempotent and create no backup. A mismatched version-1 table
  inventory or a database newer than version 1 fails closed without modification.
- Project, database, and backup paths must be absolute and physically contained beneath the
  approved project root. Missing roots, outside paths, root aliases, and existing reparse-point
  boundaries are rejected before database access.
- Physical paths, SQL text, and database content are omitted from stable failure diagnostics.
  Cancellation propagates and does not create a database when already requested.

The runtime database remains `runtime-data/packagebuilder.db`. Large files remain in the artifact
store and are referenced by logical path and SHA-256; SQLite stores metadata only.

## Dependency and security decision

The approved stable managed provider is `Microsoft.Data.Sqlite` 10.0.10. Its original dependency
graph selected `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which NuGet rejected with high-severity
advisory `GHSA-2m69-gcr7-jv3q`. The advisory was not suppressed. The native package is directly
pinned to patched stable 2.1.12, all affected lock files are regenerated, and locked restore plus
the vulnerability audit must remain clean. Both dependencies are free and require no server,
telemetry, paid IDE, hosted service, or runtime network access.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Transactional creation of all approved tables | `NewDatabaseCreatesEveryApprovedTableTransactionally` |
| Consistent pre-upgrade backup | `ExistingVersionZeroDatabaseIsBackedUpBeforeUpgrade` |
| Rollback with preserved backup | `MigrationFailureRollsBackEverySchemaChangeAndPreservesBackup` |
| Idempotent current database | `CurrentDatabaseIsIdempotentAndCreatesNoBackup` |
| Newer/mismatched schemas fail closed | `NewerDatabaseIsRejectedWithoutModificationOrBackup`, `CurrentVersionWithIncompleteTableInventoryFailsClosed` |
| Foreign keys, canonical states, canonical hashes, and indexes | `SchemaEnforcesForeignKeysAndCanonicalStateValues`, `SchemaRejectsNonCanonicalSha256Metadata`, `SchemaContainsExpectedIndexes` |
| Containment and reparse boundaries | `UncontainedOrRelativePathsAreRejected`, `ReparsePointInsideDatabasePathIsRejected`, `ReparsePointProjectRootIsRejected` |
| Backup collision and exhaustion safety | `BackupNamesNeverOverwriteExistingEvidence`, `ExhaustedBackupNamesReturnSanitizedStorageFailure` |
| Cancellation and sanitized failures | `CancellationBeforeOpeningDoesNotCreateDatabase`, `InvalidSqliteFileReturnsSanitizedMigrationFailure`, `FailuresDoNotExposePhysicalPathsOrSql` |

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0210 integration tests | Pass; 29 passed, 0 failed, 0 skipped, including real Windows reparse-point boundaries |
| New production coverage | `SqliteDatabaseMigrator` 95.05% line / 98.44% branch; schema, result, error, and internal result model 100% line/branch in the Microsoft Cobertura report beneath `artifacts/PB-0210/coverage-final4` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,618 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 374, Contracts 402 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Central dependency validation | Pass; 7 pinned central packages, 8 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 3m 26.950s on the final exact implementation |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive package in any of the 15 projects |
| Formatting and repository safety | Pass; info-level .NET formatting, Ruff lint/format, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

The remaining migrator coverage consists of defensive access-denied handling and a non-`ok`
`PRAGMA quick_check` result. Exercising those branches would require changing machine ACLs or
deliberately corrupting SQLite internals; neither is safe or deterministic in the shared local and
GitHub environments. The measured coverage is reported without exclusion or waiver, while the
observable invalid-file, rollback, backup-exhaustion, containment, and reparse failure paths remain
covered by integration tests.

The first full Core CI attempt correctly stopped on info-severity C# style diagnostics. The
repository formatter applied only mechanical fixes, focused tests were rerun, and the complete
nine-stage pipeline then passed. No test, analyzer, vulnerability warning, or release threshold was
disabled.

## Manual and visual testing

PB-0210 has no WPF screen, renderer, model import, texture display, or package preview, so there is
no end-user visual test yet. Its observable manual boundary is the focused integration test suite,
which creates only contained disposable SQLite databases beneath ignored build output, verifies
their schema and backups, and removes them. The first supported end-user visual workflow remains
the later WPF vertical slice.

## Completion

All PB-0210 implementation, validation, publication, required `main` CI, confirmation, and
rollover gates are complete. Typed repository operations remain correctly owned by PB-0211.
