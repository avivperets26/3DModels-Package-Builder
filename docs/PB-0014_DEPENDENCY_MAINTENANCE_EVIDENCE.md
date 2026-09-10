# PB-0014 — September 2026 dependency maintenance

Reviewed on 2026-09-10 from `main` commit `2a773cb`, on
`chore/PB-0014-dependency-maintenance`. Status: IN PROGRESS.

## Failure diagnosis

The failed repository-baseline jobs for Dependabot PRs #79, #80, #87, #88, and #89
reject new versions against the approved-version maps in
`scripts/Test-CentralBuildConfiguration.ps1` and `scripts/Test-TestProjects.ps1`.
The lockfile checks use the same older approval values. The core application job
was skipped, so these failures do not establish an application regression.
Downloaded job logs are retained locally under ignored `artifacts/dependency-review/pr-*.log`.

The maintenance change updates the reviewed approval values along with central
package pins and NuGet-generated lockfiles. Exact-version, package inventory,
lockfile consistency, warnings-as-errors, and all existing test gates remain enabled.

## Review decisions

| PR | Package | Before | Candidate | Decision and evidence |
| --- | --- | --- | --- | --- |
| [#88](https://github.com/avivperets26/3DModels-Package-Builder/pull/88) | Microsoft.Extensions.Hosting | 10.0.10 | 10.0.11 | Patch update; MIT; validate WPF host composition and lifetime tests. [Package](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.11), [runtime release](https://github.com/dotnet/runtime/releases/tag/v10.0.11). |
| [#87](https://github.com/avivperets26/3DModels-Package-Builder/pull/87) | Microsoft.Data.Sqlite | 10.0.10 | 10.0.11 | Patch update; MIT; validate migrations, backup, transactions, repositories, and orchestration. [Package](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.11), [EF Core release](https://github.com/dotnet/efcore/releases/tag/v10.0.11). |
| [#89](https://github.com/avivperets26/3DModels-Package-Builder/pull/89) | Microsoft.NET.Test.Sdk | 18.8.1 | 18.9.0 | Test runner update; review discovery, execution, TRX results, and unchanged test inventory. [Upstream release notes](https://github.com/microsoft/vstest/releases/tag/v18.9.0). |
| [#80](https://github.com/avivperets26/3DModels-Package-Builder/pull/80) | SQLitePCLRaw.lib.e_sqlite3 | 2.1.12 | 3.53.3 | Review separately as a native binary change. The package now follows SQLite version numbering and retains the `e_sqlite3` binary name; this does not require upgrading the managed SQLitePCLRaw bundle/provider. [Package metadata](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3), [public-domain notice](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/3.53.3/License), [SQLite release notes](https://sqlite.org/releaselog/3_53_3.html). |
| [#79](https://github.com/avivperets26/3DModels-Package-Builder/pull/79) | JsonSchema.Net | 9.3.0 | 9.4.0 | Deferred to PB-0015. The existing binary already carries conditional maintenance-fee terms, so retaining it is not evidence of an unconditional free distribution path. [Release notes](https://docs.json-everything.net/rn-json-schema/), [maintainer explanation](https://blog.json-everything.net/posts/expensive/). |

## JSON Schema distribution prerequisite

The installed 9.3.0 package's `.nuspec` declares `OSMFEULA.txt`, rather than a plain
MIT package licence. That agreement describes fees for binary use in revenue-generating
activities at annual gross revenue of at least USD 10,000, with stated exemptions.
It also preserves access to MIT source and independently compiled binaries.
No revenue eligibility, fee acceptance, or subscription authorization is inferred.

The old notices incorrectly described all these binaries as unconditionally requiring
no subscription. This task corrects that claim. PB-0015 owns a reproducible source-build
or compatible permissive replacement path, including transitive packages and conformance
tests. PR #79 stays deferred until that prerequisite is resolved. This is a known
free-distribution limitation in the existing baseline, not a new fee introduced here.

## Validation

- Hosting and Microsoft.Data.Sqlite patch stage: restore succeeded; 647 infrastructure
  tests passed, zero failures and skips, against the prior native SQLite and test SDK.
- Test SDK stage: restore succeeded and all 15 WPF tests passed, with zero failures and skips.
- Native smoke check: database creation/reopen, integrity, schema version, commit, rollback,
  and reopening new writes with the previously pinned package passed. Both restored package
  archives contain the same Windows x64 DLL, SHA-256
  `B7385D722C83FB52142A00477A726723745916D22A555711EE89834C1111FB2E`, reporting SQLite 3.53.3.
  Therefore this is package-resolution compatibility evidence, not proof of an engine-version
  migration or an additional Windows security fix. Other native platforms were not executed.
- NuGet vulnerability report: all 18 solution projects checked, including transitive packages;
  no known vulnerabilities reported by the configured NuGet source on 2026-09-10.
- Candidate package signatures: `dotnet nuget verify --all` succeeded for all four changed direct packages.
- Full `scripts/Invoke-CoreCi.ps1`: PASS. Repository baseline 33/33; locked restore; Release build
  with zero warnings/errors; .NET formatting; Ruff lint and formatting; all 2,320 tests across
  seven projects passed, with zero failures or skips. Source-change verification passed.
- The initial full run rejected mixed line endings in regenerated lockfiles and the summary;
  repository line endings and summary generation were corrected before the successful full rerun.
- Local final evidence: `artifacts/dependency-review/core-ci-final.log`,
  `artifacts/test-results/PB-0009/summary.json`, `artifacts/dependency-review/vulnerabilities.json`,
  `artifacts/dependency-review/signatures.log`, and `artifacts/dependency-review/native-upgrade.log`.

### Acceptance mapping

| Acceptance area | Existing validation |
| --- | --- |
| Reviewed pins and lockfiles | `Test-CentralBuildConfiguration.ps1`, `Test-TestProjects.ps1`, locked solution restore |
| Database/provider compatibility | Infrastructure suite, including `SqliteDatabaseMigratorTests`, `SqliteBuildMetadataRepositoryTests`, and `SqliteToolVersionApprovalRepositoryTests`; native smoke checks described above |
| Hosting and test SDK compatibility | WPF suite and all seven test projects through `Test-BaselineUnitTests.ps1` |
| Build and formatting | `Invoke-CoreCi.ps1` Release, .NET formatting, and Ruff stages |
| Package provenance and vulnerabilities | `dotnet nuget verify --all`; `dotnet list PackageBuilder.sln package --vulnerable --include-transitive --format json --no-restore` |
| Licence decisions and deferred work | Package metadata/licence review above; PB-0015; dependency notices |

## Reuse and publication

Reused the existing central-build and test-project validators, NuGet lockfile workflow,
core-CI entry point, persistence fixtures, and WPF tests. No production logic or new
parallel validator was introduced. The two existing approval maps retain their current
roles in independent configuration checks.

No completion marker or Completion Log entry is added for PB-0014 before the repository's
publication, exact-main-CI, confirmation, and rollover gates. PB-0714's prior completion
evidence remains separately unresolved. The backlog summary is regenerated at each transition.
