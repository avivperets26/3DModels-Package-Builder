# PB-0101 Product Identity and Naming Evidence

**Task:** PB-0101 — Implement product identity and naming value objects
**Branch:** `feat/PB-0101-product-identity`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-24

## Scope

PB-0101 adds immutable product naming value objects and the smallest canonical texture-name abstraction needed to prevent the historical `Albeado` misspelling. The implementation remains entirely in `PackageBuilder.Domain` and adds no WPF, filesystem implementation, Blender, Unity, Unreal, marketplace, persistence, or networking dependency.

The task also performs the approved PB-0013/E00 rollover and corrects the PB-0013 quality validator for detached HEAD. Final PB-0013 publication and optional PR-workflow failure evidence remains in [PB-0013 quality and release-gate evidence](PB-0013_QUALITY_RELEASE_GATES_EVIDENCE.md).

## Public API

All public APIs are in `PackageBuilder.Domain.Naming` and have XML documentation.

| API | Purpose |
|---|---|
| `ProductDisplayName.Create(string?)` | Validates human-readable display text and returns `NamingValidationResult<ProductDisplayName>`. |
| `InternalAssetId.Create(string?)` | Validates a compact product asset identifier. |
| `ProductFolderName.Create(string?)` | Validates one Windows-safe product folder segment. |
| `PublisherRoot.Create(string?)` | Validates a configurable publisher root used as an identifier and folder segment. |
| `CanonicalTextureNameToken.Albedo` | Exposes the only canonical texture naming token owned by PB-0101. |
| `CanonicalTextureNameToken.Create(string?)` | Accepts exact ordinal `Albedo`; rejects `Albeado`, alternate casing, and unsupported roles. |
| `NamingValidationResult<T>` | Returns `IsValid`, nullable `Value`, and a naming-specific `NamingValidationError` for expected user-input validation. |

No public constructor permits an invalid value. Expected invalid input returns a result rather than requiring exception handling.

## Accepted Naming Contract

PB-0101 applies no trimming, normalization, case folding, separator replacement, or inferred conversion between display name, asset ID, and folder name.

| Type | Accepted grammar | Approved example |
|---|---|---|
| Product display name | Non-empty human-readable Unicode text after common safety validation; internal spaces and punctuation are preserved. | `Silverwing Talonbow` |
| Internal asset ID | `[A-Za-z][A-Za-z0-9]*` | `SilverwingTalonbow` |
| Product folder name | `[A-Za-z0-9][A-Za-z0-9_-]*` | `Silverwing_Talonbow` |
| Publisher root | `[A-Za-z][A-Za-z0-9_]*` | `AvivPeretsFBX` |
| Canonical texture token | Exact ordinal `Albedo` only | `Albedo` |

These are the narrowest rules supported by the approved examples and current cross-engine identifier/folder use. They add no undocumented length limit and do not invent marketplace-specific transformations. A display name remains human-readable, an asset ID remains compact, and a folder name remains an explicit filesystem-safe value.

## Rejected Categories and Validation Semantics

All four product naming types reject:

- Null, empty, and whitespace-only input.
- Leading or trailing whitespace instead of silently trimming it.
- Control characters.
- Windows-rooted and drive-qualified path forms.
- Current-directory and parent-directory traversal segments.
- Forward-slash and backslash directory separators.

Asset IDs reject non-ASCII letters/digits, digit-first values, spaces, punctuation, hyphens, and underscores. Product folder names reject characters outside ASCII letters, digits, underscore, and hyphen. Publisher roots reject characters outside ASCII letters, digits, and underscore and require an ASCII letter first.

Product folder names and publisher roots additionally reject trailing dot/space behavior and case-insensitive Windows device names including `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, and `LPT1`–`LPT9`.

`NamingValidationError` is deliberately local to naming input. It is not the global stable validation-finding/error-code model owned by PB-0109.

## Equality, Hashing, and Culture

Value equality is type-specific, ordinal, and case-sensitive. The same text in two instances of one type is equal; different casing is not equal; values from different naming types are not interchangeable. Hashing uses a stable ordinal FNV-1a implementation over the stored UTF-16 characters, avoiding current-culture behavior and per-process randomized string hashing. Tests repeat equality and hash assertions under `tr-TR`.

## Canonical Albedo Decision

`CanonicalTextureNameToken.Albedo.Value` is exactly `Albedo`. Parsing uses ordinal equality and accepts only that spelling. `Albeado`, `albedo`, `BaseColor`, and path-like or malformed values are rejected. PB-0101 does not define the complete texture-role inventory; PB-0103 will extend the texture model while preserving this canonical spelling.

## Test Matrix

The focused xUnit v3 suite covers:

- Approved display-name, asset-ID, folder-name, publisher-root, and `Albedo` examples.
- Null, empty, whitespace-only, leading/trailing whitespace, and control inputs.
- Rooted, drive-qualified, separator-containing, and traversal-like input.
- Windows-reserved names and trailing dot/space behavior.
- Every accepted grammar edge and invalid-character branch.
- Ordinal equality, type separation, null/other-object equality, stable hashes, and Turkish-culture behavior.
- Multiple configured publisher roots, proving `AvivPeretsFBX` is not the only accepted publisher.
- Exact `Albedo` generation and rejection of `Albeado`.
- Success and failure states of `NamingValidationResult<T>`.

The focused suite passes 107/107 tests. The complete `PackageBuilder.Domain.Tests` project passes 108/108 tests, including its assembly smoke test.

## Coverage Evidence

Coverage is collected with the centrally pinned `coverlet.collector` `10.0.1` beneath ignored `artifacts/PB-0101`. The deterministic build maps source paths to `/_/`; Coverlet's default `ExcludeAssembliesWithoutSources=MissingAll` therefore produced an empty zero-module report and explicitly logged that `PackageBuilder.Domain.dll` was excluded because its PDB had no local source paths. That empty report is not treated as coverage evidence.

The successful run sets `DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeAssembliesWithoutSources=None` so the pinned collector instruments the deterministic assembly. It does not exclude production files or weaken the 100% critical naming threshold.

| Production file | Line coverage | Branch coverage |
|---|---:|---:|
| `CanonicalTextureNameToken.cs` | 100% | 100% |
| `InternalAssetId.cs` | 100% | 100% |
| `NamingValidationResult.cs` | 100% | 100% |
| `NamingValidator.cs` | 100% | 100% |
| `ProductDisplayName.cs` | 100% | 100% |
| `ProductFolderName.cs` | 100% | 100% |
| `PublisherRoot.cs` | 100% | 100% |

The final Cobertura report and collector diagnostics remain ignored beneath the repository-local artifacts root.

## Detached-HEAD Validator Correction

The original PB-0013 changed-file-history check evaluated `@(git branch --show-current)[0]`. A detached checkout intentionally returns an empty array, so indexing element zero produced `Index was outside the bounds of the array.`

The corrected validator:

- Treats zero branch-name lines as detached HEAD without indexing.
- Uses fixed historical PB-0013 commit IDs rather than subject-grep discovery.
- Continues to enforce documentation-only paths for all historical and final PB-0013 task commits.
- Applies live working-tree scope only when the actual branch is `docs/PB-0013-quality-release-gates`.
- Does not classify PB-0101 production/test files as PB-0013 changes.
- Retains concise comments explaining detached-HEAD and successor-history behavior.

Normal-branch and independent detached-clone validation results for PowerShell 7 and Windows PowerShell 5.1 are recorded below.

## Explicitly Deferred Work

- PB-0102 owns product-case and target models.
- PB-0103 owns source assets, the complete texture-role model, colour space, and normal conventions.
- PB-0107 owns the complete publisher and marketplace profile models beyond the publisher-root value.
- PB-0109 owns global validation findings, stable codes, severity, blocking state, explanations, and suggestions.
- PB-0410, PB-0501, PB-0605, PB-1105, and later naming-profile tasks own object/engine/marketplace-specific name composition, collision handling, target prefixes, and path-length policies.

PB-0101 adds no manifest/schema, filesystem operation, automatic conversion from display names, target adapter, or marketplace naming policy.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0101 naming tests | Pass; 107 passed, 0 failed, 0 skipped. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 108 passed, 0 failed, 0 skipped. |
| Critical naming coverage | Pass; every new naming production file reports 100% line and 100% branch coverage. |
| `Test-QualityAndReleaseGates.ps1` — normal branch, PowerShell 7.6.4 | Pass; 11 checks passed, 0 failed. |
| `Test-QualityAndReleaseGates.ps1` — normal branch, Windows PowerShell 5.1 | Pass; 11 checks passed, 0 failed. |
| `Test-QualityAndReleaseGates.ps1` — detached HEAD, PowerShell 7.6.4 | Pass; 11 checks passed, 0 failed. |
| `Test-QualityAndReleaseGates.ps1` — detached HEAD, Windows PowerShell 5.1 | Pass; 11 checks passed, 0 failed. |
| `Test-ArchitectureDecisionRecords.ps1` | Pass; 8 checks passed, 0 failed. |
| `Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 29 checks passed, 0 failed, including in-process and standalone Windows PowerShell validation. |
| `Invoke-CoreCi.ps1` | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| Core test projects | Pass; 111 discovered, 111 passed, 0 failed, 0 skipped. |
| .NET/Ruff formatting and lint | Pass; `dotnet format --verify-no-changes --severity info`, Ruff lint, and Ruff format verification. |
| `git diff --check` | Pass through the repository baseline for working tree and index. |

The initial default Coverlet run produced an empty zero-module report because deterministic PathMap source paths are not local; it was rejected as evidence and rerun with the documented source-availability setting. An initial repository-baseline pass found one changed Markdown line with trailing whitespace, and the first core-CI attempt found repository-style `severity info` formatting changes. Both issues were corrected; the final baseline and core-CI executions above passed without an exception or weakened gate.

## Remaining Gates

PB-0101 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and is absent from the Completion Log. After local validation it still requires the user-controlled task commit, task-branch push, merge into and push of `main`, successful required `main` CI, explicit user completion confirmation, and next-task rollover synchronization.
