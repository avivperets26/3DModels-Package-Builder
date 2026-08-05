# PB-0304 Unreal Installation Discovery Evidence

**Task:** PB-0304 — Implement Unreal/Epic installation discovery
**Branch:** `feat/PB-0304-unreal-locator`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-05

## Scope

PB-0304 adds an offline Unreal Engine locator under `PackageBuilder.Infrastructure.Tools` and a
typed consumer contract under `PackageBuilder.Contracts.Tools`. It combines explicit configured
roots, contained cached Epic launcher manifests, explicit source-build roots, caller-supplied
registry and standard-path detections, and direct engines beneath the conventional contained
`tools\unreal` root. Contained candidates are verified through engine layout and strict bounded
`Engine\Build\Build.version` metadata before PB-0301 `ToolInstallation` values are created.

PB-0304 does not launch Unreal Editor or Epic Games Launcher, read the registry directly, scan
external standard directories, download or install Unreal, accept a licence, contact a network,
select or persist an installation, choose an approved version, or add UI. Platform adapters may
supply registry and standard-path roots through the request; external roots stay informational and
their engine files are never opened.

## Public Contract

| Type | Purpose |
|---|---|
| `IUnrealInstallationLocator` | Runs one cancellation-aware deterministic discovery pass. |
| `UnrealInstallationDiscoveryRequest` | Carries the job ID, canonical roots, configured roots, launcher manifests, source builds, and registry/standard detections. |
| `UnrealInstallationDetection` | Records root, editor/version paths, combined provenance, verification status, safe diagnostic, and optional verified installation. |
| `UnrealInstallationDiscoveryReport` | Returns ordered detections, verified installations, and discovery-wide failures. |
| `IUnrealDiscoveryFileSystem` | Isolates the minimal filesystem reads required for deterministic hostile/boundary tests. |
| `PhysicalUnrealDiscoveryFileSystem` | Implements that boundary through `System.IO`. |

Verified detections must contain an unselected contained Unreal `ToolInstallation` whose
executable matches the detection. Every other state forbids an installation value. This makes an
external registry, standard-path, configured, or launcher detection nonselectable by construction.

## Discovery and Verification Policy

- Explicit roots and launcher paths must be canonical fully qualified Windows paths.
- The conventional contained root is `tools\unreal`; it can itself be an engine root or contain
  direct versioned engine roots.
- Discovery is bounded to 32 unique launcher manifests, 1 MiB per manifest, 512 total launcher
  entries, 256 direct contained directories, and 256 unique candidates.
- Launcher manifests must be beneath the project root without reparse boundaries. JSON is strict,
  depth-limited to 16, duplicate-property rejecting, and limited to Unreal entries identified by a
  `UE_` `AppName` or `ArtifactId`.
- Root, editor, and metadata paths cannot cross a reparse-point boundary.
- A contained candidate requires `Engine`,
  `Engine\Binaries\Win64\UnrealEditor-Cmd.exe`, and `Engine\Build\Build.version`.
- Build metadata is limited to 64 KiB and must contain unique nonnegative integral
  `MajorVersion`, `MinorVersion`, and `PatchVersion` fields compatible with PB-0301.
- External roots are checked only for root existence, reported as informational, never opened or
  executed, and never converted to `ToolInstallation`.
- Cancellation stops before the next candidate and returns an explicit safe failure.
- Diagnostics use stable codes and do not copy raw JSON, exceptions, or rejected paths into
  discovery-wide messages.

The engine layout follows Epic's
[Unreal Engine directory structure](https://dev.epicgames.com/documentation/unreal-engine/unreal-engine-directory-structure),
the Windows editor location follows Epic's
[installed-build documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/create-an-installed-build-of-unreal-engine),
and version fields follow the official
[`FBuildVersion` API](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Core/FBuildVersion).

## Licensing Boundary

Unreal Engine use remains subject to Epic's current
[Unreal Engine EULA](https://www.unrealengine.com/eula/unreal), including applicable eligibility,
seat-subscription, and royalty conditions. Package Builder does not bundle Unreal Engine, determine
eligibility, purchase or assign seats, calculate royalties, download an engine, sign in to Epic,
or accept terms for the user. PB-0304 only inspects caller-authorized local discovery inputs.

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| Configured, launcher-manifest, source-build, and conventional contained engines are found | `DiscoversConfiguredSourceLauncherAndContainedInstallationsDeterministically`, `ConventionalRootMayBeTheEngineInstallationItself` |
| Duplicate sources combine deterministically | `DeduplicatesCandidateAndCombinesEveryDiscoverySource` |
| Registry/standard/external detections cannot be selected or opened | `ExternalRegisteredStandardAndConfiguredRootsAreInformationalOnly` |
| Unreal editor layout and `Build.version` identify a verified installation | discovery happy-path tests and `InvalidBuildVersionMetadataFailsClosed` |
| Path/reparse/hostile inputs fail closed (TEST-006, SEC-002) | root, manifest, contained-directory, engine-file, malformed JSON, size, duplicate-property, and unreadable-input tests |
| Work and reads are bounded (PERF-003, PERF-004) | `OversizedBuildVersionIsRejectedBeforeUnboundedRead`, manifest/directory/entry/candidate limit tests, `NonseekableStreamsAreReadWithinTheSameBounds` |
| Discovery is deterministic and offline (TEST-012, INSTALL-005) | ordered discovery/deduplication tests and absence of process/network dependencies |
| Expected failures are typed and sanitized (ENG-004, SEC-007) | expected/unexpected filesystem tests, contract invariant tests, and safe diagnostic assertions |
| Cancellation is explicit | `CancellationStopsCandidateInspectionWithExplicitFailure` |

## Validation Results

| Validation | Current result |
|---|---|
| Focused PB-0304 tests | Pass; 46 passed, 0 failed, 0 skipped. |
| Focused PB-0304 production coverage | Pass; every executable Contracts and Infrastructure class introduced by PB-0304 reports 100% line and branch coverage. |
| Complete Infrastructure suite | Pass; 594 passed, 0 failed, 0 skipped. |
| Complete five-project test portfolio | Pass; 1,941 passed, 0 failed, 0 skipped: Domain 846, Application 84, Infrastructure 594, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline | Pass; 29 checks passed, 0 failed. |
| Full local Core CI | Pass; all 9 fail-closed stages passed in 4 minutes 7 seconds. |
| Formatting, diff, security, and prohibited-content scans | Pass; information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and the repository baseline's prohibited/public-content checks succeeded. |

Generated coverage and test output remains beneath ignored `artifacts/PB-0304`.

The first formatting verification identified only information-level .NET style suggestions in the
new C# sources and tests. The repository's exact formatter was applied, focused tests and 100%
changed-production coverage were reverified, and the complete Core CI script then passed without an
exception or waived gate.

## Manual Visual Test

Not applicable. PB-0304 changes Contracts and Infrastructure only and does not modify WPF. The
PB-1301 shell remains the current visual checkpoint.

## Final Publication Evidence

- Final task commit `e3c62ddf283951e4290746d7577fd542ed2757e0` was pushed on
  `feat/PB-0304-unreal-locator`.
- The task was merged through [pull request #47](https://github.com/avivperets26/3DModels-Package-Builder/pull/47)
  into `main` as `dabbbe74211eaf4ce45ee296712a8d90cae7bda6`.
- Required [main workflow run 30995589319](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30995589319)
  completed successfully for that exact merge commit.
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-05.
- No CI, completion, quality, or workflow exception was used.
- PB-0304 is recorded exactly once in the Completion Log during the PB-0305 rollover.
