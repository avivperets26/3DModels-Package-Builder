# PB-0006 Central Build Configuration Evidence

**Task:** PB-0006 — Add centralized SDK, build, and NuGet configuration  
**Branch:** `chore/PB-0006-central-build-config`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-23

## Scope and Status

PB-0006 centralizes the existing solution's SDK, compiler, analyzer, package-version, NuGet-source, cache-containment, and dependency-lock policy. It adds no business functionality and does not satisfy PB-0008's requirement for meaningful passing smoke tests or PB-0009's application CI scope.

Local implementation and validation evidence are recorded below. Final publication evidence confirms that every PB-0006 completion gate passed without a CI or quality exception. The approved one-merge rollover was recorded at the beginning of PB-0007 on `chore/PB-0007-formatting`.

## SDK Policy

`global.json` retains the approved repository-local SDK policy:

- SDK: `10.0.302`.
- `rollForward`: `disable`.
- `allowPrerelease`: `false`.
- No SDK installation or upgrade was performed by PB-0006.

The environment entry script resolves `dotnet.exe` from `tools/dotnet/10.0.302` and disables multilevel lookup.

## Central Build Policy

`Directory.Build.props` applies to all 15 projects and enables:

- Nullable reference types and implicit usings.
- Deterministic output and deterministic source paths.
- Portable PDBs and compiler path mapping that removes workstation paths.
- Supported .NET analyzers during builds at `latest-recommended`.
- Build-time code-style enforcement.
- Compiler and analyzer warnings as errors.
- `ContinuousIntegrationBuild` when `CI`, `GITHUB_ACTIONS`, or `TF_BUILD` is active.

Project-specific target frameworks, output types, WPF settings, test settings, and project references remain in the individual project files. The four xUnit v3 projects use the required executable shape: `OutputType=Exe`, `IsTestProject=true`, and `IsPackable=false`.

## Central Package Inventory

Official NuGet Gallery metadata was reviewed on 2026-07-23. The selected versions are exact stable releases with no wildcard, range, or prerelease identifiers.

| Package | Selected stable version | Official verification source | Notes |
|---|---:|---|---|
| `Microsoft.NET.Test.Sdk` | `18.8.1` | [NuGet Gallery](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk) | Gallery showed `18.8.1` as the current stable package, last updated 2026-07-14. |
| `xunit.v3.mtp-off` | `3.2.2` | [NuGet Gallery](https://www.nuget.org/packages/xunit.v3.mtp-off/3.2.2) and [xUnit runner guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) | Stable xUnit v3 meta-package with Microsoft Testing Platform support explicitly disabled; the newer xUnit 4 packages are prereleases and were not selected. |
| `xunit.runner.visualstudio` | `3.1.5` | [NuGet Gallery](https://www.nuget.org/packages/xunit.runner.visualstudio) | `4.0.0-pre.*` entries were excluded because they are prereleases. |
| `coverlet.collector` | `10.0.1` | [NuGet Gallery](https://www.nuget.org/packages/coverlet.collector) | Gallery showed `10.0.1` as the current stable package, last updated 2026-05-18. |

`Directory.Packages.props` enables NuGet Central Package Management, disables `VersionOverride`, leaves transitive pinning disabled, and enables lock-file generation. The four approved test packages are the only direct NuGet dependencies. Production projects have no direct package references.

VSTest remains the selected `dotnet test` runner: `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` remain referenced, `global.json` contains no Microsoft Testing Platform runner selection, and no test project enables a Microsoft Testing Platform runner property. `coverlet.collector` and `xunit.runner.visualstudio` retain `PrivateAssets=all` and the approved restricted `IncludeAssets` list.

## Resolved Test Package Graph

All four test lock files resolve the same package graph for `net10.0`:

| Package | Resolved version | Relationship |
|---|---:|---|
| `coverlet.collector` | `10.0.1` | Direct |
| `Microsoft.NET.Test.Sdk` | `18.8.1` | Direct; depends on `Microsoft.CodeCoverage` and `Microsoft.TestPlatform.TestHost` 18.8.1 |
| `xunit.runner.visualstudio` | `3.1.5` | Direct |
| `xunit.v3.mtp-off` | `3.2.2` | Direct; depends on `xunit.analyzers` 1.27.0, `xunit.v3.assert` 3.2.2, and `xunit.v3.core.mtp-off` 3.2.2 |
| `Microsoft.Bcl.AsyncInterfaces` | `6.0.0` | Transitive |
| `Microsoft.CodeCoverage` | `18.8.1` | Transitive |
| `Microsoft.TestPlatform.ObjectModel` | `18.8.1` | Transitive |
| `Microsoft.TestPlatform.TestHost` | `18.8.1` | Transitive; depends on `Microsoft.TestPlatform.ObjectModel` 18.8.1 |
| `Microsoft.Win32.Registry` | `5.0.0` | Transitive |
| `xunit.analyzers` | `1.27.0` | Transitive |
| `xunit.v3.assert` | `3.2.2` | Transitive |
| `xunit.v3.common` | `3.2.2` | Transitive; depends on `Microsoft.Bcl.AsyncInterfaces` 6.0.0 |
| `xunit.v3.core.mtp-off` | `3.2.2` | Transitive; depends on `xunit.v3.extensibility.core` and `xunit.v3.runner.inproc.console` 3.2.2 |
| `xunit.v3.extensibility.core` | `3.2.2` | Transitive; depends on `xunit.v3.common` 3.2.2 |
| `xunit.v3.runner.common` | `3.2.2` | Transitive; depends on `Microsoft.Win32.Registry` 5.0.0 and `xunit.v3.common` 3.2.2 |
| `xunit.v3.runner.inproc.console` | `3.2.2` | Transitive; depends on `xunit.v3.extensibility.core` and `xunit.v3.runner.common` 3.2.2 |

No test lock file contains the xUnit v2 meta-package or the legacy `xunit.abstractions`, `xunit.assert`, `xunit.core`, `xunit.extensibility.core`, or `xunit.extensibility.execution` dependencies.

## NuGet Source and Containment

Root `NuGet.config`:

- Clears inherited package sources.
- Approves only `https://api.nuget.org/v3/index.json` as `nuget.org`.
- Contains no credentials or private feeds.
- Stores global packages at the portable repository-relative path `runtime-data/nuget-packages`.

`scripts/Enter-PackageBuilderEnvironment.ps1` contains all remaining CLI and NuGet mutable state beneath `runtime-data`:

- .NET CLI home.
- NuGet global packages.
- NuGet HTTP cache.
- NuGet scratch space.
- NuGet plugin cache.
- Process temporary directories.
- .NET CLI telemetry opt-out.
- Microsoft Testing Platform telemetry opt-out (`TESTINGPLATFORM_TELEMETRY_OPTOUT=1`) as defense in depth.

No telemetry is enabled, and no paid service or hosted dependency was introduced.

## Lock-File Policy

`RestorePackagesWithLockFile` is enabled centrally. Each of the 15 projects has a format-version-2 `packages.lock.json` so solution-wide `dotnet restore --locked-mode` can validate the complete project and transitive package graph.

Normal restore may update lock files after an explicitly reviewed dependency change. CI and repeatability checks use locked mode and fail when project/package inputs differ from the committed locks. Package caches and generated `bin`/`obj` output remain ignored.

## Dependency-Free Validator

`scripts/Test-CentralBuildConfiguration.ps1` uses only Windows PowerShell/.NET framework capabilities and validates:

- Exact SDK policy.
- Required central build and analyzer properties.
- Central Package Management and approved stable versions.
- Absence of project-level, floating, wildcard, range, or prerelease versions.
- Exact 15-project inventory and four test-package inventory.
- Executable xUnit v3 test-project shape with VSTest retained and Microsoft Testing Platform disabled.
- Runner and Coverlet private/build asset restrictions.
- Both .NET CLI and Microsoft Testing Platform telemetry opt-outs.
- Explicit NuGet source and contained mutable state.
- One consistent lock file per project with legacy xUnit v2 packages rejected.
- Contained project references and absence of explicit import/configuration escapes.

`scripts/Test-RepositoryBaseline.ps1` invokes the validator directly and through standalone Windows PowerShell 5.1. The bootstrap GitHub workflow remains dependency-free and does not install .NET.

## Measured Validation

All commands ran on 2026-07-23 from `C:\Dev\PackageBuilder`. The .NET commands ran after entering the repository environment.

| Command | Result |
|---|---|
| `dotnet --version` | Exit 0; `10.0.302`; 0.25 seconds measured wall time. |
| `dotnet nuget locals all --list` | HTTP cache, global packages, scratch/temp, and plugin cache all resolved beneath `C:\Dev\PackageBuilder\runtime-data`. |
| Runtime telemetry opt-out check after `scripts/Enter-PackageBuilderEnvironment.ps1` | `DOTNET_CLI_TELEMETRY_OPTOUT=1` and `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`; exit 0. |
| `dotnet restore PackageBuilder.sln` | Exit 0; all 15 project lock graphs regenerated or confirmed; no warnings or errors; 7.95 seconds measured wall time. |
| `dotnet restore PackageBuilder.sln --locked-mode` | Exit 0; all 15 project graphs accepted their lock files; no warnings or errors; 3.25 seconds measured wall time. |
| `dotnet build PackageBuilder.sln --configuration Debug --no-restore` | Exit 0; 15 projects built; 0 warnings; 0 errors; .NET reported 16.16 seconds. |
| `dotnet build PackageBuilder.sln --configuration Release --no-restore` | Exit 0; 15 projects built; 0 warnings; 0 errors; .NET reported 6.97 seconds. |
| `dotnet test PackageBuilder.sln --configuration Release --no-build --no-restore` | Exit 0 through VSTest; four test assemblies discovered; 0 warnings; 0 errors; zero meaningful tests available; 6.15 seconds measured wall time. PB-0008 owns the missing smoke tests. |
| `scripts/Test-CentralBuildConfiguration.ps1` | Governance revalidation exit 0; 8 checks passed; 0 failed; 15 projects, 4 central packages, 15 lock files, the VSTest/xUnit v3 shape, telemetry opt-outs, and legacy dependency denial validated; 1.26 seconds measured wall time. |
| Standalone Windows PowerShell 5.1 central validator | Exit 0; the same 8 checks passed with 0 failures. |
| `scripts/Test-SolutionArchitecture.ps1` | Governance revalidation exit 0; 7 checks passed; 0 failed; exact executable xUnit v3 package shape and 15-project architecture validated; 0.92 seconds measured wall time. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Governance revalidation exit 0; 16 checks passed; 0 failed, including dependency-free central validation and standalone Windows PowerShell 5.1 execution; 24.12 seconds measured wall time. |
| `git diff --check` | Exit 0; no whitespace errors. |
| Legacy dependency search over `Directory.Packages.props`, project files, and lock files | Exit 0; no xUnit v2 dependency declaration or lock entry found. |

The Release VSTest run intentionally reports no available tests in each of the four assemblies. This is the verified current skeleton state, not meaningful test coverage: PB-0008 remains unchecked and owns adding at least one passing smoke test to each project. PB-0009 remains unchecked and owns full application restore/build/test CI.

## Documentation Impact

- Updated `AGENTS.md` and `docs/IMPLEMENTATION_BACKLOG.md` with the permanent one-merge rollover workflow while keeping PB-0006 unchecked and 🟡 **PROCESS**.
- Added and updated this PB-0006 evidence record.
- `docs/Package_Builder_Plan.md`, `docs/TECH_STACK_AND_ARCHITECTURE.md`, and `docs/QUALITY_AND_RELEASE_GATES.md` required no change because they contain no conflicting completion-bookkeeping location or mandatory pull-request/branch-CI workflow.
- The Completion Log is unchanged.

## Final Publication and Completion Evidence

| Gate | Evidence |
|---|---|
| Task commit | `41255c6f5953fc7d2dfe96530617484a1e3f87d9` |
| Task branch | `chore/PB-0006-central-build-config` |
| Pull request | [#7](https://github.com/avivperets26/3DModels-Package-Builder/pull/7) |
| Merge into `main` | `9de260b0e02d201cf539fdfd154224fe99a3122b` |
| Pull-request workflow | [Run 30022944913](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30022944913), successful |
| Required `main` workflow | [Run 30022954605](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30022954605), successful |
| User confirmation | User confirmed the merge and green `main` CI on 2026-07-23 |
| Exceptions | None; no CI or quality exception was used |

PB-0006 is logically complete and is synchronized exactly once in the backlog Completion Log. PB-0006 is no longer in Active Work. PB-0007 owns the branch containing this rollover synchronization; no completion-only PB-0006 branch or commit cycle was created.

## Remaining Gates

None for PB-0006.
