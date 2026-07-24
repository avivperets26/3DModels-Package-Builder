# Package Builder

Package Builder is a planned local-first Windows desktop application for turning prepared 3D source assets into consistent, validated engine and marketplace deliverables. The design keeps portable outputs, engine-specific targets, and marketplace requirements in separate adapters so each can evolve without being hard-coded into the core.

## Development Status

Package Builder is in the repository-foundation stage. The reviewable implementation currently provides the pinned .NET development environment, solution and project skeleton, formatting and smoke-test baselines, repository validators, core CI entry point, and review-only GitHub governance configuration.

The application does not yet import models, build packages, provide the desktop workflow, or produce marketplace-ready releases. The targets and asset cases below are planned scope, not currently available product functionality. Read the [implementation backlog](docs/IMPLEMENTATION_BACKLOG.md) for task-level status.

## Planned Package Targets

- Portable FBX and GLB deliverables.
- Unity packages created through a dedicated Unity adapter.
- Unreal Engine project archives created through a dedicated Unreal adapter.
- Marketplace packaging through independent, versioned adapters, with Fab planned first.

Unity and Unreal are external products with their own licence, eligibility, seat, and royalty terms. Contributors and operators must review the applicable vendor terms; Package Builder does not grant eligibility, accept licences, or require a paid engine tier.

## Planned Asset Cases

The planned pipeline covers five cases:

1. Static model without a rig or animation.
2. Rigged model without animation.
3. Rigged model with one or more animations.
4. Related item set intended to work together.
5. Collection of independent items packaged together.

## No-Cost Prerequisites

The current repository workflow requires:

- Windows.
- Git.
- Windows PowerShell 5.1 or a compatible later PowerShell.
- [Visual Studio Code](https://code.visualstudio.com/) for the supported editor and terminal workflow.
- The exact .NET SDK `10.0.302` provisioned at `tools\dotnet\10.0.302`.

The .NET SDK, Ruff, build outputs, caches, logs, and other mutable development state stay below the repository root and are ignored by Git. The SDK provisioning and verification record is in [PB-0001 environment baseline](docs/PB-0001_ENVIRONMENT_BASELINE.md). No paid IDE, extension, subscription, hosted service, or software edition is mandatory for the required local workflow.

Blender, Unity, and Unreal are not required for the current foundation checks. Their pinned, licence-aware setup belongs to later adapter tasks.

## Repository-Local .NET 10 SDK

Run all commands from `C:\Dev\PackageBuilder`. Dot-source the environment script so the current terminal selects only the repository-local SDK and redirects CLI state, NuGet caches, and temporary files into ignored directories beneath the project root.

```powershell
Set-Location C:\Dev\PackageBuilder
. .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet --version
```

The final command must print `10.0.302`. `global.json` pins that exact SDK with roll-forward and prerelease selection disabled.

## Visual Studio Code Workflow

Open `C:\Dev\PackageBuilder` as the Visual Studio Code folder, start a PowerShell terminal, and enter the repository environment once per terminal session. Use the commands below from that terminal. The scripts and CLI commands are the supported workflow; paid Visual Studio and IDE-only build actions are not required.

## Local Commands

### Setup

Enter the contained environment and install the pinned Ruff executable from its checksum-verified official archive:

```powershell
Set-Location C:\Dev\PackageBuilder
. .\scripts\Enter-PackageBuilderEnvironment.ps1
& .\scripts\Install-Ruff.ps1
```

### Documentation and Repository Validation

```powershell
& .\scripts\Test-ContributionDocumentation.ps1
& .\scripts\Test-GitHubGovernance.ps1
& .\scripts\Test-RepositoryBaseline.ps1 -RequireTrackedFiles
```

### Restore and Build

```powershell
dotnet restore .\PackageBuilder.sln --locked-mode
dotnet build .\PackageBuilder.sln --configuration Release --no-restore
```

### Formatting Verification

```powershell
dotnet format .\PackageBuilder.sln --no-restore --verify-no-changes --severity info --verbosity minimal
& .\scripts\Test-Formatting.ps1
```

Formatting verification is non-mutating. Use `Test-Formatting.ps1 -Fix` only when intentionally applying formatting changes for review.

### Tests

```powershell
& .\scripts\Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges
```

### Complete Local Core Pipeline

```powershell
& .\scripts\Invoke-CoreCi.ps1
```

The core entry point runs repository validation, exact SDK verification, locked restore, warning-free Release build, .NET formatting, pinned Ruff checks, and all four baseline test projects in fail-closed order.

## Repository Structure

```text
C:\Dev\PackageBuilder\
├── .github\                 # Review templates, ownership, dependency updates, and CI workflow
├── docs\                    # Plans, architecture, backlog, quality rules, and task evidence
├── scripts\                 # Repository setup, validation, formatting, test, and CI entry points
├── src\                     # Application and adapter project skeletons
├── tests\                   # Baseline test projects
├── tools\                   # Ignored repository-local SDKs and tools
├── downloads\               # Ignored verified downloads and metadata
├── logs\                    # Ignored setup and validation logs
├── runtime-data\            # Ignored CLI state, caches, temporary files, and future runtime data
└── artifacts\               # Ignored generated test and build evidence
```

## Workspace Containment

`C:\Dev\PackageBuilder` is the single project root. Source, documentation, local tools, downloads, caches, logs, runtime data, temporary files, and generated artifacts must remain beneath it. Do not use a sibling data directory, a user-profile fallback, or the system temporary directory for project-owned state.

## Version and Identity Boundaries

- A `PB-####` value is a backlog task ID, not a Package Builder product version.
- Pinned SDK, tool, engine, and dependency versions make a build reproducible; they are not Package Builder release versions.
- A Package Builder product release is a separately approved release artifact and Git event.
- Marketplace-requirements profiles are independently versioned because store rules can change without a Package Builder release.
- No final Package Builder release-versioning scheme has been approved yet. Version strings in planning examples do not make that decision.

## Project Documents

- [Contributing workflow](CONTRIBUTING.md)
- [Security reporting policy](SECURITY.md)
- [Project rules](AGENTS.md)
- [Product and implementation plan](docs/Package_Builder_Plan.md)
- [Technology stack and architecture](docs/TECH_STACK_AND_ARCHITECTURE.md)
- [Implementation backlog](docs/IMPLEMENTATION_BACKLOG.md)
- [Quality and release gates](docs/QUALITY_AND_RELEASE_GATES.md)

GitHub issues use the repository's stable Markdown templates. Pull requests remain optional, and Dependabot update pull requests are proposals that require user review and manual merge. The [GitHub governance evidence](docs/PB-0011_GITHUB_GOVERNANCE_EVIDENCE.md) records the current official-documentation basis, configuration limits, and validation results.
