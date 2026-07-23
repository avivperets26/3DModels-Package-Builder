# PB-0008 Baseline Unit Tests Evidence

**Task:** PB-0008 — Create baseline unit-test projects  
**Branch:** `test/PB-0008-test-projects`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-23

## Scope and Status

PB-0008 adds one deterministic offline xUnit v3 smoke test to each of the four existing test projects. Production behavior has not been implemented yet, so each test validates a meaningful baseline invariant: the project’s direct production reference produces an assembly that can be loaded at runtime with the expected identity.

The task also adds:

- A dependency-free Windows PowerShell 5.1 structural validator for the exact test-project inventory, direct production references, centrally managed packages, and discoverable categorized smoke-test source.
- A reusable contained test validator that resolves repository-local .NET SDK `10.0.302`, performs a locked solution restore, runs each test project, parses TRX counts, rejects zero discovery, failures, skips, and unclassified outcomes, and returns a nonzero exit code on failure.
- Repository-baseline integration for the structural validator in both in-process and standalone Windows PowerShell 5.1 modes.

PB-0008 does not change production behavior, package versions, lock files, the PB-0002 bootstrap workflow, coverage policy, or the future requirements-to-tests matrix. PB-0009 remains responsible for adding full solution restore, build, format, and test execution to GitHub Actions. PB-1801 owns the complete traceability system, and PB-1806 owns coverage-gate enforcement.

## Smoke Tests

Every test is marked with `[Trait("Category", "Smoke")]`.

| Test project | Test method | Production assembly verified | Invariant |
|---|---|---|---|
| `PackageBuilder.Domain.Tests` | `ReferencedDomainAssemblyLoadsWithExpectedIdentity` | `PackageBuilder.Domain` | The directly referenced Domain assembly loads and reports the expected assembly name. |
| `PackageBuilder.Application.Tests` | `ReferencedApplicationAssemblyLoadsWithExpectedIdentity` | `PackageBuilder.Application` | The directly referenced Application assembly loads and reports the expected assembly name. |
| `PackageBuilder.Infrastructure.Tests` | `ReferencedInfrastructureAssemblyLoadsWithExpectedIdentity` | `PackageBuilder.Infrastructure` | The directly referenced Infrastructure assembly loads and reports the expected assembly name. |
| `PackageBuilder.Contract.Tests` | `ReferencedContractsAssemblyLoadsWithExpectedIdentity` | `PackageBuilder.Contracts` | The directly referenced Contracts assembly loads and reports the expected assembly name. |

The tests do not read the network, clock, locale, user profile, global tools, mutable machine state, or paths outside the repository. No fake production type or behavior was added to make the tests possible.

## Test-Project Configuration

`scripts/Test-TestProjects.ps1` verifies:

1. Exactly the four approved test projects exist beneath `tests`.
2. Each project targets `net10.0`, remains an executable non-packable test project, and imports xUnit once.
3. Each project directly references only its corresponding production project.
4. Each project references exactly the centrally managed test package set without inline versions.
5. `Directory.Packages.props` retains the exact approved stable versions.
6. Each project contains at least one public `[Fact]` with `Category=Smoke` that loads its expected production assembly.

The package versions remain:

| Package | Version |
|---|---:|
| `Microsoft.NET.Test.Sdk` | `18.8.1` |
| `xunit.v3.mtp-off` | `3.2.2` |
| `xunit.runner.visualstudio` | `3.1.5` |
| `coverlet.collector` | `10.0.1` |

All 15 existing `packages.lock.json` files remained unchanged after locked restore. No direct or transitive dependency changed.

## Reusable Test Validator

Omitted-root verification from Windows PowerShell 5.1:

```powershell
& .\scripts\Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges
```

Equivalent explicit-root verification:

```powershell
& .\scripts\Test-BaselineUnitTests.ps1 `
    -RepositoryRoot 'C:\Dev\PackageBuilder' `
    -VerifyNoSourceChanges
```

The validator:

- Requires the Git top level and repository-local SDK pin.
- Sets .NET, NuGet, temporary, and CLI state to contained repository paths.
- Runs `dotnet restore PackageBuilder.sln --locked-mode`.
- Runs each test project with `--no-restore` and the Debug configuration.
- Replaces only the exact prior generated TRX file for each project so stale results cannot satisfy validation.
- Writes TRX files and deterministic `summary.json` beneath `artifacts/test-results/PB-0008`.
- Writes its validation log beneath `logs/validation/PB-0008`.
- Hashes all tracked and reviewable untracked source candidates before and after explicit verification mode and fails if any changes.
- Restores the caller’s process environment before exit.

All generated results, logs, `bin`, `obj`, caches, and temporary files remain ignored and beneath `C:\Dev\PackageBuilder`.

## Measured Test Results

Final omitted-root and explicit-root verification runs produced the same logical result:

| Project | Discovered | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| `PackageBuilder.Domain.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Application.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Infrastructure.Tests` | 1 | 1 | 0 | 0 |
| `PackageBuilder.Contract.Tests` | 1 | 1 | 0 | 0 |
| **Total** | **4** | **4** | **0** | **0** |

Both runs produced deterministic logical-summary SHA-256:

```text
63729354FEAC77390E92D54813BBE9CBF12B5051BB05037AC46FCA3754333619
```

An initial pre-validation run correctly failed at compilation because strict analyzer `CA1707` rejected underscore-separated public method names. The test methods were renamed to behavior-focused PascalCase names; no warning suppression or policy exception was added.

## Validation Evidence

All commands ran on 2026-07-23 from `C:\Dev\PackageBuilder` using repository-contained tools.

| Command or check | Result |
|---|---|
| Repository-local `dotnet --version` | Exit 0; exactly `10.0.302`. |
| `dotnet restore PackageBuilder.sln --locked-mode` | Exit 0; all 15 projects were up to date and accepted their existing lock files. |
| Debug solution build with `--no-restore` | Exit 0; 15 projects built; 0 warnings; 0 errors. |
| Release solution build with `--no-restore` | Exit 0; 15 projects built; 0 warnings; 0 errors. |
| `scripts/Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges` with omitted root | Exit 0 under Windows PowerShell 5.1; 4 discovered, 4 passed, 0 failed, 0 skipped; source hashes unchanged. |
| `scripts/Test-BaselineUnitTests.ps1 -RepositoryRoot 'C:\Dev\PackageBuilder' -VerifyNoSourceChanges` | Exit 0 under Windows PowerShell 5.1 with the same project and total counts; source hashes unchanged. |
| Repeated logical-result comparison | Passed; both validator modes produced summary SHA-256 `63729354FEAC77390E92D54813BBE9CBF12B5051BB05037AC46FCA3754333619`. |
| `scripts/Test-TestProjects.ps1` | Exit 0; 4 projects, 4 pinned packages, 4 checks, 0 failures. |
| `scripts/Test-CentralBuildConfiguration.ps1` | Exit 0; 15 projects, 4 central packages, 8 checks, 0 failures. |
| `scripts/Test-SolutionArchitecture.ps1` | Exit 0; 15 projects, 7 checks, 0 failures. |
| `scripts/Test-FormattingConfiguration.ps1` | Exit 0; 6 checks passed, 0 failed. |
| `scripts/Test-Formatting.ps1` | Exit 0; `dotnet format --verify-no-changes`, Ruff check, Ruff format check, and source-nonmutation validation passed. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Exit 0; 20 checks passed, 0 failed, including in-process and standalone Windows PowerShell 5.1 test-project validation. |
| Windows PowerShell 5.1 parser over every `scripts/*.ps1` | Exit 0; all scripts parsed without errors. |
| `git diff --check` | Exit 0; no whitespace errors. |
| Lock-file diff | No `packages.lock.json` file changed. |
| Prohibited tracked/candidate content | Passed through the repository baseline; no test result, executable, `bin`/`obj`, cache, log, secret, personal path, or generated artifact is reviewable for commit. |
| PB lifecycle validation | Passed; PB-0007 is recorded once in the Completion Log, PB-0008 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log, and PB-0013 is unchanged. |

## Documentation Impact

- Added this PB-0008 evidence record.
- Updated `docs/IMPLEMENTATION_BACKLOG.md` for the verified PB-0007 rollover and active PB-0008 state.
- Updated `docs/PB-0007_FORMATTING_EVIDENCE.md` with final publication and rollover evidence.
- Updated `docs/TECH_STACK_AND_ARCHITECTURE.md` to document the local PB-0008 test workflow and PB-0009 CI boundary.
- `docs/Package_Builder_Plan.md` and `docs/QUALITY_AND_RELEASE_GATES.md` require no change because PB-0008 implements their existing deterministic, offline, contained test policy without changing a normative requirement or claiming future traceability, coverage, or release thresholds.

## Final Publication and Rollover Evidence

| Gate | Final evidence |
|---|---|
| Task commit | `cdf08733edc28d1990b86a4a70b7d59c33fdcbeb` on `test/PB-0008-test-projects` |
| Integration | [PR #9](https://github.com/avivperets26/3DModels-Package-Builder/pull/9) |
| Merge into `main` | `37dbd69690f3397ecf60ef7d96094d9d09221f9a` |
| Required `main` CI | [Repository baseline workflow run 30029052452](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30029052452) — attempt 1 `push` run on `main`, completed successfully for exact merge commit `37dbd69690f3397ecf60ef7d96094d9d09221f9a` |
| User confirmation | The user explicitly confirmed on 2026-07-23 that PB-0008 was merged into `main` and required `main` CI was green. |
| Exception | None used |
| Rollover | Synchronized exactly once at the beginning of `chore/PB-0009-core-ci`; PB-0008 is removed from Active Work and recorded once in the Completion Log. |

PB-0008 is `[x]` and 🟢 **DONE**. No PB-0008 application-test GitHub Actions run is claimed: its required `main` evidence was the PB-0002 repository-baseline workflow that existed before PB-0009. PB-0009 adds the application restore, build, formatting, Ruff, and test CI foundation without rewriting this historical evidence.
