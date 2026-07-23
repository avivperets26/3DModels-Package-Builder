# PB-0007 Formatting Enforcement Evidence

**Task:** PB-0007 — Add coding style and formatting enforcement
**Branch:** `chore/PB-0007-formatting`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-23

## Scope and Status

PB-0007 establishes a free, repository-contained formatting baseline for .NET, Python, Blender/Unreal worker code, PowerShell, XML, XAML, JSON, YAML, and Markdown. The task does not add application behavior and does not add full application GitHub CI; PB-0009 remains the owner of restore, build, format, and test CI.

The default validation path is read-only for source files. An explicit `-Fix` switch is the only supported mutation path, and it is followed immediately by the same verification checks used in normal mode.

## Approved Versions and Official Sources

| Tool | Approved version | Official source | Integrity and installation |
|---|---:|---|---|
| .NET SDK and `dotnet format` | `10.0.302` | Existing approved repository SDK pin in `global.json` | Uses `tools/dotnet/10.0.302/dotnet.exe`; no separate formatter tool is installed. |
| Ruff | `0.15.22` | [Official immutable Ruff 0.15.22 release](https://github.com/astral-sh/ruff/releases/tag/0.15.22), published 2026-07-16 and marked Latest when reviewed on 2026-07-23 | Official x64 Windows archive and checksum are retained beneath `downloads/ruff/0.15.22`; the verified executable is installed at `tools/ruff/0.15.22/ruff.exe`. |

The approved official archive is:

```text
https://releases.astral.sh/github/ruff/releases/download/0.15.22/ruff-x86_64-pc-windows-msvc.zip
```

The official checksum source is:

```text
https://releases.astral.sh/github/ruff/releases/download/0.15.22/ruff-x86_64-pc-windows-msvc.zip.sha256
```

The tracked and independently verified SHA-256 is:

```text
6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d
```

Ruff is the official free distribution from Astral. No global install, package-manager environment, paid service, or remote-script pipeline is used. The release also publishes GitHub artifact attestations; the repository-contained setup uses the official per-asset checksum because it is independently downloadable and directly pinned.

## Python Compatibility Policy

`ruff.toml` uses `target-version = "py311"`. This is selected for the planned Blender 5.0 worker runtime, not because Python 3.11 is the newest available syntax. Blender bundles one compatible Python runtime, and the official Blender 5.0 Python-wheel guidance uses Python 3.11 wheel targets. The target must be reviewed when the approved Blender family changes.

The configuration uses `required-version = "==0.15.22"` so Ruff itself rejects a mismatched executable at runtime.

## Repository Formatting Policy

Root `.editorconfig` enforces:

- UTF-8 text.
- LF line endings where the editor or formatter supports them.
- A final newline and trailing-whitespace removal by default.
- Four-space indentation by default, with two-space JSON/YAML and native solution-file exceptions.
- Modern C# layout, expression, namespace, accessibility, and naming conventions.
- Dedicated XML, XAML, JSON, YAML, Markdown, PowerShell, and Python sections.
- Intentional Markdown hard-line-break preservation.
- Generated-source analyzer exemptions and producer-owned lock/minified-file treatment.
- Compatibility with nullable references, supported analyzers, warnings-as-errors, and deterministic builds.

Root `ruff.toml` configures:

- Pyflakes and selected pycodestyle errors.
- Import sorting (`I`).
- Python upgrade checks (`UP`).
- Bugbear bug checks (`B`).
- Comprehension improvements (`C4`).
- Bandit-derived security checks (`S`).
- Simplification checks (`SIM`).
- Ruff-specific checks (`RUF`).
- Safe automatic fixes only; security findings remain non-fixable for review.
- LF formatting, double quotes, four-space indentation, and formatted docstring code examples.
- Future `workers/blender`, `workers/unreal`, repository `scripts`, and Python tests as first-party locations.

The rule set is deliberately selected rather than using `ALL`. Test files receive only the narrow `S101` exception required for assertion-based tests.

## Exclusions and Containment

Ruff force-excludes:

- `tools`, `downloads`, `logs`, `runtime-data`, and `artifacts`.
- Python caches and local environments.
- .NET `bin` and `obj`.
- Unity generated `Library`, `Temp`, `Logs`, and `UserSettings`.
- Unreal generated `Binaries`, `DerivedDataCache`, `Intermediate`, and `Saved`.

Ruff cache data is contained beneath `runtime-data/ruff-cache`. Formatting validation logs are written beneath `logs/validation/PB-0007`, and temporary validation output is written beneath `artifacts/validation/PB-0007`. All locations are ignored by Git.

## Installation

From a Windows PowerShell 5.1 prompt:

```powershell
& .\scripts\Install-Ruff.ps1
```

The setup script:

1. Resolves and validates the Git repository root.
2. Downloads only the exact official archive and checksum into `downloads/ruff/0.15.22`.
3. Confirms the downloaded checksum file matches the tracked SHA-256.
4. Hashes the archive before extraction.
5. Extracts through a contained temporary directory under `artifacts/setup/PB-0007`.
6. Installs only `ruff.exe` under `tools/ruff/0.15.22`.
7. Verifies `ruff --version`.
8. Writes setup evidence under `logs/setup/PB-0007`.

`-Force` explicitly re-downloads and replaces only the pinned, contained Ruff archive, checksum, temporary extraction directory, and tool-version directory. The script never evaluates or pipes downloaded script content.

## Validation and Fix Commands

Normal verification, with repository root inferred safely:

```powershell
& .\scripts\Test-Formatting.ps1
```

Equivalent explicit-root verification:

```powershell
& .\scripts\Test-Formatting.ps1 -RepositoryRoot 'C:\Dev\PackageBuilder'
```

Normal mode performs:

```text
dotnet format PackageBuilder.sln --no-restore --verify-no-changes --severity info
ruff check --config ruff.toml --no-fix .
ruff format --config ruff.toml --check .
```

The script calls the exact executables beneath `tools`, verifies their versions, uses `--no-restore`, hashes source candidates before and after validation, and fails if normal mode changes a source file.

Explicit fix mode:

```powershell
& .\scripts\Test-Formatting.ps1 -Fix
```

Fix mode may update C# and Python source. It runs `dotnet format --no-restore`, Ruff safe lint fixes, and Ruff formatting, then repeats the non-mutating checks. Unsafe Ruff fixes remain disabled. Review the resulting diff before any user-controlled staging or commit.

## Troubleshooting

- If the local .NET SDK is missing, restore the approved `10.0.302` SDK using the PB-0001 environment procedure; do not use a global fallback.
- If Ruff is missing or mismatched, rerun `scripts/Install-Ruff.ps1`; use `-Force` only when replacing the exact contained PB-0007 files is intended.
- If checksum verification fails, stop. Do not extract or execute the archive; compare the official release and checksum URLs with this evidence record.
- If `dotnet format --no-restore` reports missing assets, run the controlled locked restore before validation.
- If verification reports changes, run explicit fix mode, inspect the diff, then rerun normal verification.
- Validation details are retained in `logs/validation/PB-0007/formatting-validation.log`.

## Measured Validation

All commands ran on 2026-07-23 from `C:\Dev\PackageBuilder`. .NET commands used the contained environment and the exact executable beneath `tools/dotnet/10.0.302`. Ruff commands used `tools/ruff/0.15.22/ruff.exe`.

| Command or check | Result |
|---|---|
| Official Ruff archive and checksum download through `scripts/Install-Ruff.ps1` under Windows PowerShell 5.1 | Exit 0; archive and checksum retained beneath `downloads/ruff/0.15.22`; SHA-256 `6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d` matched both the tracked pin and official checksum file. |
| Installed Ruff version | Exit 0; `ruff 0.15.22`; executable resolved at `tools/ruff/0.15.22/ruff.exe`. |
| Repository-local .NET SDK version | Exit 0; exactly `10.0.302`. |
| `dotnet restore PackageBuilder.sln --locked-mode` | Exit 0; all 15 projects were up to date and accepted their lock files. |
| Initial default `scripts/Test-Formatting.ps1` | Correctly exited nonzero without applying fixes because `AssemblyInfo.cs` required standard attribute whitespace. |
| Explicit `scripts/Test-Formatting.ps1 -Fix` | Exit 0; .NET fix, Ruff safe lint fix, Ruff format, and all post-fix checks passed. |
| Final `dotnet format PackageBuilder.sln --no-restore --verify-no-changes --severity info` | Exit 0; no changes required. |
| Final `ruff check --config ruff.toml --no-fix .` | Exit 0; all checks passed. |
| Final `ruff format --config ruff.toml --check .` | Exit 0; no changes required. |
| Final `scripts/Test-Formatting.ps1` with omitted `RepositoryRoot` | Exit 0 under Windows PowerShell 5.1; all version, .NET, Ruff, and source-nonmutation checks passed. |
| Final `scripts/Test-Formatting.ps1 -RepositoryRoot 'C:\Dev\PackageBuilder'` | Exit 0 under Windows PowerShell 5.1; all version, .NET, Ruff, and source-nonmutation checks passed. |
| `dotnet build PackageBuilder.sln --configuration Debug --no-restore` | Final exit 0; 15 projects built; 0 warnings; 0 errors; .NET reported 2.44 seconds. |
| `dotnet build PackageBuilder.sln --configuration Release --no-restore` | Final exit 0; 15 projects built; 0 warnings; 0 errors; .NET reported 2.54 seconds. |
| `scripts/Test-FormattingConfiguration.ps1` | Exit 0; 6 checks passed; 0 failed. |
| `scripts/Test-CentralBuildConfiguration.ps1` | Exit 0; 8 checks passed; 0 failed. |
| `scripts/Test-SolutionArchitecture.ps1` | Exit 0; 7 checks passed; 0 failed. |
| `scripts/Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Exit 0; 18 checks passed; 0 failed, including formatting configuration validation in-process and through standalone Windows PowerShell 5.1. |
| Windows PowerShell 5.1 parser over every `scripts/*.ps1` | Exit 0; all 8 scripts parsed with no errors. |
| `git diff --check` | Exit 0; no whitespace errors. |
| Tracked/candidate prohibited-content, secret, personal-path, binary, and generated-file validation | Passed through repository baseline; downloaded archives, executables, logs, caches, and artifacts remain ignored. |
| PB lifecycle validation | Passed; PB-0006 appears exactly once in the Completion Log, PB-0007 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log, and PB-0013 is unchanged. |

### Existing Source Formatting Changes

Explicit fix mode changed one existing source file:

- `src/PackageBuilder.App.Wpf/AssemblyInfo.cs` — normalized CRLF to LF and inserted the standard space in `[assembly: ThemeInfo(...)]`.

Ruff changed no existing source because the repository currently contains no Python worker files. The configuration is active for future `.py`, `.pyi`, and `.pyw` files and validates its own `ruff.toml` input today.

No PB-0007 GitHub CI is claimed because the task changes have not been published.

## Documentation Impact

- Added this PB-0007 evidence record.
- Updated `docs/IMPLEMENTATION_BACKLOG.md` for the PB-0006 rollover and active PB-0007 state.
- Updated `docs/TECH_STACK_AND_ARCHITECTURE.md` to record the exact formatting baseline and Python compatibility rationale.
- Updated `docs/PB-0006_CENTRAL_BUILD_CONFIGURATION_EVIDENCE.md` with final completion evidence.
- `docs/Package_Builder_Plan.md` and `docs/QUALITY_AND_RELEASE_GATES.md` require no policy change because PB-0007 implements their existing UTF-8, free-tooling, containment, warning, download-integrity, and evidence requirements without changing a normative requirement.

## Remaining Gates

- User stages and commits PB-0007 on `chore/PB-0007-formatting`.
- User pushes the task branch, merges it into `main`, pushes `main`, receives successful required `main` CI, and explicitly confirms completion.
- Synchronize PB-0007 completion only at the beginning of the next task branch under the one-merge rollover workflow.
