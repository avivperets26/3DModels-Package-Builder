# PB-0113 Manifest and Profile Migration Evidence

**Task:** PB-0113 — Add manifest/profile migration framework  
**Branch:** `feat/PB-0113-schema-migrations`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-07-25

## Scope and rollover

PB-0113 adds a renderer-, filesystem-, persistence-, UI-, and network-independent JSON migration
boundary for product manifests, publisher profiles, and generic marketplace identity profiles. It
detects a document family and validated positive schema version, reports compatibility without
side effects, executes only explicitly registered forward migrations, audits every structural
change, and finalizes through the existing strict current schema, Domain reconstruction, semantic
validation, and canonical serializer.

The PB-0112 rollover uses final task commit
`a22f913108870b24ead8dde38833ae3e56c02c47`, pull request #26, merge
`e8c31a221dedd49daac0e1e35d29fb5df2f43642`, successful PR run `30155442329`, successful required
`main` run `30155444972`, and user confirmation dated 2026-07-25. No exception was used. PB-0112
is `[x]` / 🟢 **DONE**, absent from Active Work, and logged exactly once. PB-0113 remains `[ ]` /
🟡 **PROCESS**, active, and absent from the Completion Log.

## Production schema history

No approved legacy production format exists.

- Repository history first adds the product-manifest contract in PB-0110 commit
  `a88ed992002b34ffdb96a8b1e7b7b596609d6891`, already using schema version 1.
- Repository history first adds publisher and marketplace profile contracts in PB-0111 commit
  `c49cf2755458c409fde41714a29a275f38ce9a92`, already using schema version 1.
- The retained schemas, fixtures, examples, plan, architecture, ADRs, PB-0110 evidence, and PB-0111
  evidence contain no pre-version-1 public contract or approved legacy fixture.

The production registry is therefore intentionally empty and all three current versions remain 1.
Version-1 documents report `CurrentDocument` / `MIGRATION_NOT_REQUIRED`. Positive future versions
fail as `UnsupportedNewerVersion`; zero, negative, fractional, overflow, missing, null, wrong-type,
and duplicate `schemaVersion` values are invalid. PB-0113 does not change a public schema to
version 2 and does not fabricate a version-0 production contract.

The generic framework is demonstrated with three retained internal-only representative fixtures
under `tests/fixtures/migrations/internal`. That controlled chain exercises versions 1 → 2 → 3,
rename, default, conversion, warning, review, single-step, and multi-step behavior. It is not a
public manifest/profile schema and cannot be selected by the production registry.

## Public migration types

| Type | Purpose |
|---|---|
| `SchemaVersion` | Positive `int`-bounded, culture-independent typed schema version. |
| `MigrationDocumentFamily` | Closed product-manifest, publisher-profile, and marketplace-profile families. |
| `MigrationStatus` | Current, available, migrated, invalid, unsupported older/newer, missing, ambiguous, and failed outcomes. |
| `MigrationChange` / `MigrationChangeKind` | Explicit immutable addition, removal, rename, default, conversion, warning, or review ledger entry. |
| `IJsonMigrationStep` / `DelegateJsonMigrationStep` | One explicit family-specific forward step. |
| `MigrationStepResult` | Structured step output or expected failure without raw-input diagnostics. |
| `MigrationRegistry` / `MigrationRegistryResult` | Immutable explicit registrations and structured registry validation. |
| `ManifestProfileMigrationEngine` | Side-effect-free inspect and migrate execution for an explicit registry/finalizer. |
| `ManifestProfileMigration` | Production version-1 facade for the three approved document families. |
| `MigrationFinalDocument` | Canonical JSON plus exactly one typed current Domain document for production results. |
| `MigrationResult` | Immutable status, family, source/target versions, original audit input, final document, ledger, and stable diagnostic code. |

## Invariants and failure behavior

- Strict parsing uses the shared 1,048,576-character input limit, depth 64, comment/trailing-comma
  rejection, object-root requirement, and recursive ordinal duplicate-property detection.
- Family detection uses mutually exclusive approved family signatures. Unknown or cross-family
  shapes fail; no family is guessed.
- `schemaVersion` must be an exact positive integer representable by `int`.
- Registrations are explicit, immutable, one-version forward steps. Duplicate registrations,
  non-contiguous edges, gaps, cycles, downgrades, and multiple outgoing paths are rejected.
- Inspection never executes a step. Migration never downgrades and never chooses among paths.
- Steps receive a read-only `JsonElement` and return new JSON; caller-owned data is not mutated.
- Every removed leaf, added leaf, or changed leaf must be covered by a compatible change entry.
  Silent removal, rename, default, or conversion fails with
  `MIGRATION_CHANGE_LEDGER_INCOMPLETE`.
- The exact original string is retained as immutable audit evidence but is never interpolated into
  diagnostics. Expected invalid input uses stable non-throwing status and diagnostic values.
- Each step output is reparsed with the same strict safeguards and must identify the same family
  and its exact registered target version.
- Final production JSON must pass the current embedded Draft 2020-12 schema, reconstruct and
  semantically validate the current Domain aggregate, and serialize through the existing
  canonical deterministic writer.
- Equality, ordering, path comparison, diagnostics, and version formatting use ordinal or
  invariant behavior.

## Requirements-to-tests traceability

| PB-0113 criterion | Automated evidence |
|---|---|
| Current detection for all three families and fixture regression | `CurrentVersionIsDetectedTypedValidatedAndCanonical` |
| Invalid version and unknown-family matrix | `MissingNullWrongMalformedAndOutOfRangeVersionsAreInvalid`, `InvalidJsonAndUnknownOrAmbiguousFamiliesFailClosed` |
| Older/newer and current distinction | `ProductionVersionOneDocumentsNeverInventALegacyMigration`, `ControlledRegistryDistinguishesUnsupportedOlderAndMissingStep` |
| Single- and multi-step migrations | `InspectReportsAvailableWithoutExecutingAndMigrationRunsSingleOrMultipleSteps` |
| Registry invariants | `RegistryRejectsDuplicatesGapsCyclesDowngradesAndAmbiguousPaths`, `InvalidRegistryIsReportedWithoutSelectingAPath` |
| Explicit change ledger and no silent loss | `ChangeLedgerExplicitlyRecordsEverySupportedChangeCategory`, `RemovedRenamedOrTransformedDataCannotDisappearWithoutLedgerEvidence` |
| Step/output/final validation failures | `StepFailureExceptionMalformedOutputAndDuplicateOutputFailClosed`, `InvalidChangeShapeAndWrongFamilyOrVersionOutputAreRejected`, `FinalSchemaAndSemanticValidationFailureRejectsMigratedOutput` |
| Current strict schema and semantic validation | `CurrentProductionDocumentMustPassStrictSchemaAndSemanticValidation` |
| Immutability and audit retention | `CallerInputAndOriginalAuditEvidenceRemainUnchanged` |
| Size, depth, duplicate, culture, and determinism boundaries | `MaximumInputSizeAndDepthBoundariesAreEnforced`, `DuplicateVersionAtEveryRepresentativeNestingLevelIsRejected`, `OutputAndDiagnosticsRemainDeterministicAndCultureIndependent` |
| Typed schema-version behavior | `SchemaVersionValueHasValidatedOrdinalValueSemantics` |
| Structured invalid registry input | `InvalidRegistryInputsFailWithoutThrowing` |

## Scope boundaries

PB-0113 performs no file discovery, file read/write, backup, persistence, SQLite migration,
engine-project or template migration, marketplace-requirements-profile migration, schema download,
network request, telemetry, UI dialog, best-effort recovery, or in-place mutation. PB-0210 owns
SQLite migrations. Engine-template evolution and marketplace-requirements profiles retain their
existing owners.

## Current local validation

| Validation | Result |
|---|---|
| Focused PB-0113 tests | Pass; 43 passed, 0 failed, 0 skipped |
| Complete Contracts suite | Pass; 381 passed, 0 failed, 0 skipped |
| All four core test projects | Pass; 1,172 passed, 0 failed, 0 skipped: Domain 789, Application 1, Infrastructure 1, Contracts 381 |
| Migration-critical coverage | Pass; all seven files report 100% line and 100% branch coverage in `artifacts/PB-0113/coverage-final-2/fabdfff9-6287-4b7a-b587-88cb51322d08/coverage.cobertura.xml` |
| Repository baseline | Pass; 29 checks, 0 failures |
| Architecture validator | Pass; 7 checks, 0 failures |
| ADR validator | Pass; 8 checks, 0 failures |
| Quality validator | Pass in Windows PowerShell 5.1 and repository-local PowerShell 7.6.4; 11 checks, 0 failures in each |
| Full local Core CI | Pass; all 9 stages, including locked restore, formatter, Ruff, Release build, and 1,172 tests |
| Debug and Release builds | Pass; 15 projects, 0 warnings, 0 errors in each configuration |
| Vulnerability audit | Pass; no vulnerable direct or transitive packages reported across all 15 projects |
| Formatting and repository checks | Pass; `dotnet format --verify-no-changes --severity info`, Ruff through Core CI, and `git diff --check` |

## Remaining gates

- User-controlled staging, task commit, branch push, merge, and `main` push.
- Successful required `main` CI and explicit user completion confirmation.
