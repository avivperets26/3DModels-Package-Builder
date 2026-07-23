# PB-0009 Core CI Evidence

**Task:** PB-0009 — Add core GitHub Actions CI
**Branch:** `chore/PB-0009-core-ci`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-23

## Scope and Status

PB-0009 expands `.github/workflows/repository-baseline.yml` in place. The PB-0002 `validate-repository-baseline` job remains the first independent job, and a new `core-ci` job depends on it. The workflow runs on pull requests targeting `main` and pushes to `main`, uses GitHub Free `windows-latest` runners, and grants only `contents: read`.

The new reusable `scripts/Invoke-CoreCi.ps1` entry point runs the same logical pipeline from Windows PowerShell 5.1 locally and `pwsh` in GitHub Actions. Local execution only selects `tools/dotnet/10.0.302/dotnet.exe`. An external action-managed SDK is accepted only with explicit `-GitHubActions`, `GITHUB_ACTIONS=true`, an exact `GITHUB_WORKSPACE` match, and successful `dotnet --version` verification for `10.0.302`.

PB-0009 does not add coverage thresholds, supply-chain enforcement, caching, uploaded artifacts, engines, telemetry, publishing, deployments, secrets, paid services, or marketplace operations. PB-1806 owns coverage gates, and PB-1611 owns dependency, licence, vulnerability, and secret CI.

## PB-0008 Rollover

The required previous-task synchronization was completed before PB-0009 implementation:

| Gate | PB-0008 evidence |
|---|---|
| Task commit | `cdf08733edc28d1990b86a4a70b7d59c33fdcbeb` |
| Pull request | [PR #9](https://github.com/avivperets26/3DModels-Package-Builder/pull/9) |
| Merge commit | `37dbd69690f3397ecf60ef7d96094d9d09221f9a` |
| Exact required `main` workflow | [Run 30029052452](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30029052452) — attempt 1 `push` run on `main`, completed successfully for the exact merge commit |
| User confirmation | Explicitly provided on 2026-07-23 |
| Exception | None used |

PB-0008 is now `[x]` / 🟢 **DONE**, removed from Active Work, and recorded exactly once in the Completion Log. PB-0013 is unchanged.

## Reviewed Immutable Actions

GitHub release metadata, tag refs, and exact commit metadata were independently checked on 2026-07-23. Both releases were current, non-draft, non-prerelease releases; both exact commits had valid GitHub verification.

| Action | Reviewed stable release | Immutable commit | Evidence |
|---|---|---|---|
| `actions/checkout` | `v7.0.1`, published 2026-07-20 | `3d3c42e5aac5ba805825da76410c181273ba90b1` | [Release](https://github.com/actions/checkout/releases/tag/v7.0.1), [commit](https://github.com/actions/checkout/commit/3d3c42e5aac5ba805825da76410c181273ba90b1) |
| `actions/setup-dotnet` | `v6.0.0`, published 2026-07-16 | `a98b56852c35b8e3190ac28c8c2271da59106c68` | [Release](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0), [commit](https://github.com/actions/setup-dotnet/commit/a98b56852c35b8e3190ac28c8c2271da59106c68) |

No mutable action tag such as `@main`, `@v7`, or `@latest` appears in the workflow.

## Workflow Design

| Setting | Value |
|---|---|
| Workflow file | `.github/workflows/repository-baseline.yml` |
| Triggers | Pull requests targeting `main`; pushes to `main` |
| Permissions | `contents: read` only |
| Baseline job | `validate-repository-baseline`, `windows-latest`, 10-minute timeout |
| Core job | `core-ci`, depends on `validate-repository-baseline`, `windows-latest`, 30-minute timeout |
| Checkout | Full history with `fetch-depth: 0`; `persist-credentials: false` in both jobs |
| SDK setup | SHA-pinned `actions/setup-dotnet` with exact `dotnet-version: '10.0.302'` |
| Core command | `scripts/Invoke-CoreCi.ps1 -RepositoryRoot $env:GITHUB_WORKSPACE -GitHubActions` |
| Artifact upload/cache | None |

The baseline job remains dependency-free and does not install .NET. It validates the workflow and scripts before the dependent core job can start.

## Core Pipeline

`scripts/Invoke-CoreCi.ps1` runs these fail-closed stages in order:

1. Repository baseline validation with `-RequireTrackedFiles`.
2. Exact .NET SDK `10.0.302` verification.
3. One `dotnet restore PackageBuilder.sln --locked-mode`.
4. Complete Release solution build with `--no-restore`.
5. `dotnet format --verify-no-changes` with `--no-restore`.
6. Ruff `0.15.22` installation and verification through `scripts/Install-Ruff.ps1`, including the existing official SHA-256 pin `6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d`.
7. Ruff lint verification with `--no-fix`.
8. Ruff formatting verification with `--check`.
9. All four baseline test projects in Release with `--no-restore --no-build`.

`scripts/Test-BaselineUnitTests.ps1` retains its PB-0008 defaults while adding explicit Release, no-restore, no-build, result-set, and verified GitHub Actions SDK options. It deletes each exact prior TRX before running, rejects missing results, zero discovery, nonzero test exits, failures, skips, unclassified outcomes, and fewer than four total passes, then writes clear per-project and total counts.

Project-owned CLI state, NuGet packages and caches, Ruff cache, temporary data, logs, and test results stay under the repository workspace. .NET CLI and test-platform telemetry are disabled. Commands are noninteractive and use explicit executable paths plus argument arrays.

## Measured Local Results

Both the omitted-root and explicit-root core-CI runs passed. They used repository-local SDK `10.0.302` and produced the same deterministic logical-summary SHA-256:

```text
7D4AAF1C94D7E865099F397A2CD47E6C377B94608BA149467E9DBD1CBB14932D
```

| Project | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| `PackageBuilder.Domain.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Application.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Infrastructure.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Contract.Tests` | 1 | 1 | 0 | 0 |
| **Total** | **4** | **4** | **0** | **0** |

The controlled restore accepted all existing lock files. The complete 15-project Release build succeeded with zero warnings and zero errors. .NET formatting, Ruff lint, and Ruff formatting verification made no source changes.

## Validation Evidence

| Command or check | Result |
|---|---|
| `scripts/Invoke-CoreCi.ps1` with omitted `RepositoryRoot` | Pass; all nine stages passed; 4 discovered, 4 passed, 0 failed, 0 skipped. |
| `scripts/Invoke-CoreCi.ps1 -RepositoryRoot 'C:\Dev\PackageBuilder'` | Pass; the same stage and test result. |
| Repeated logical-result comparison | Pass; both runs produced SHA-256 `7D4AAF1C94D7E865099F397A2CD47E6C377B94608BA149467E9DBD1CBB14932D`. |
| Exact SDK | Pass; `10.0.302`. |
| Locked restore | Pass; all projects up to date; no lock-file change. |
| Release build | Pass; 15 projects, 0 warnings, 0 errors. |
| `dotnet format --verify-no-changes` | Pass. |
| Ruff install/check/format | Pass; `0.15.22`, official SHA-256 verified, lint and formatting clean. |
| `scripts/Test-CoreCiConfiguration.ps1` | Pass; 8 checks, 0 failures. |
| `scripts/Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges` with PB-0008 defaults | Pass; locked restore, Debug configuration, 4 discovered, 4 passed, 0 failed, 0 skipped; standalone behavior preserved. |
| `scripts/Test-TestProjects.ps1` | Pass; 4 projects, 4 pinned packages, 4 checks, 0 failures. |
| `scripts/Test-CentralBuildConfiguration.ps1` | Pass; 15 projects, 4 packages, 8 checks, 0 failures. |
| `scripts/Test-SolutionArchitecture.ps1` | Pass; 15 projects, 7 checks, 0 failures. |
| `scripts/Test-FormattingConfiguration.ps1` | Pass through repository baseline; 6 checks, 0 failures. |
| `scripts/Test-Formatting.ps1` | Pass; .NET format, Ruff lint, Ruff format, and source-nonmutation checks succeeded. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass inside both core runs; 21 checks, 0 failures. |
| Windows PowerShell 5.1 parsing | Pass under `5.1.26100.8894`; every `scripts/*.ps1` file parsed without errors. |
| `git diff --check` | Pass inside both repository-baseline runs. |
| Lock-file, changed-file, content, and lifecycle audit | Pass; lock files are unchanged; exactly nine expected PB-0009 files changed; prohibited generated/runtime content is not reviewable; PB-0008 is logged once; PB-0009 remains PROCESS and absent from the Completion Log; PB-0013 is unchanged. |

## Documentation Impact

- Added this PB-0009 evidence record.
- Updated `docs/IMPLEMENTATION_BACKLOG.md` for the PB-0008 rollover and active PB-0009 state.
- Updated `docs/PB-0008_BASELINE_UNIT_TESTS_EVIDENCE.md` with final publication and rollover evidence.
- Updated `docs/TECH_STACK_AND_ARCHITECTURE.md` with the current two-job core CI design and local/GitHub SDK boundary.
- `docs/PB-0002_REPOSITORY_BASELINE.md` remains unchanged because it accurately records historical PB-0002 evidence; PB-0009 preserves that job and documents the later expansion here.
- `docs/Package_Builder_Plan.md` and `docs/QUALITY_AND_RELEASE_GATES.md` require no change because PB-0009 implements their existing CI, containment, warning, deterministic-test, free-tooling, and evidence policies without changing a normative requirement or introducing future coverage/supply-chain gates.

## Remaining Gates

PB-0009 remains locally implemented, `[ ]`, and 🟡 **PROCESS**. Local configuration validation is not a GitHub Actions execution, and no PB-0009 GitHub CI pass is claimed.

The user-controlled task commit, branch push, merge into and push of `main`, successful required `main` CI, and explicit user completion confirmation remain. PB-0009 completion bookkeeping must be synchronized only at the beginning of the next task branch after those gates pass.
