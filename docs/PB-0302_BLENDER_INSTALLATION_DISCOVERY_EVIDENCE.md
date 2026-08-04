# PB-0302 Blender Installation Discovery Evidence

**Task:** PB-0302 — Implement Blender installation discovery  
**Branch:** `feat/PB-0302-blender-locator`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-04

## Scope

PB-0302 adds an offline Blender locator under `PackageBuilder.Infrastructure.Tools` and a typed
consumer contract under `PackageBuilder.Contracts.Tools`. It discovers explicit configured
executables plus portable `blender.exe` candidates beneath the approved `tools\blender` root,
verifies contained candidates through PB-0207's direct process runner, and creates PB-0301
`ToolInstallation` values only from canonical vendor output.

PB-0302 does not download or install Blender, inspect the Windows registry, execute an external
detection, accept a Blender licence, select or persist an installation, contact a network, add UI,
or implement Unity/Unreal discovery. Those responsibilities remain with their documented tasks.

## Public Contract

| Type | Purpose |
|---|---|
| `IBlenderInstallationLocator` | Runs one cancellation-aware deterministic discovery pass. |
| `BlenderInstallationDiscoveryRequest` | Carries the job ID, canonical roots, contained process roots, and configured paths. |
| `BlenderInstallationDetection` | Records provenance, verification status, safe diagnostic, and an optional verified installation. |
| `BlenderInstallationDiscoveryReport` | Returns ordered detections, verified installations, and discovery-wide failures. |
| `IBlenderDiscoveryFileSystem` | Isolates the minimal filesystem reads required for deterministic hostile/boundary testing. |
| `PhysicalBlenderDiscoveryFileSystem` | Implements that boundary through `System.IO`. |

Verified detections must contain an unselected, contained Blender `ToolInstallation`. Every other
status forbids an installation value and reports `CanBeSelected == false`. This contract makes it
impossible to represent an external detection as selectable.

## Discovery and Verification Policy

- Configured paths must be canonical fully qualified Windows file paths.
- Existing configured paths outside the approved tools root are informational only and are never
  sent to the process runner.
- Portable discovery scans only `tools\blender`, accepts only `blender.exe`, sorts entries
  ordinally, and deduplicates case-insensitively.
- The portable scan skips reparse-point files/directories and is bounded to depth 4, 1,024 scanned
  directories, and 256 unique candidates.
- Contained candidates run directly with the single literal `--version` argument, no caller
  environment variables, a 4,096-character capture, and bounded 15-second startup/idle,
  30-second total, and 5-second graceful-shutdown intervals.
- Verification requires normal completion, exit code zero, untruncated output, and a first
  non-empty line in the form `Blender <canonical PB-0301 version>`.
- Raw stdout/stderr and rejected physical paths are not copied into discovery-wide diagnostics.
- PB-0207 performs the final existing-path, containment, and reparse checks immediately before
  execution, so a replacement after discovery still fails closed.

The `--version` behavior is documented by the
[official Blender command-line manual](https://docs.blender.org/manual/en/latest/advanced/command_line/arguments.html).

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| Configured contained Blender executables are found and verified | `DiscoversConfiguredAndPortableInstallationsAndVerifiesLiteralVersionOutput` |
| Portable Blender executables beneath the project tools root are found | `DiscoversConfiguredAndPortableInstallationsAndVerifiesLiteralVersionOutput` |
| Duplicate configured/portable paths are deterministic | `DeduplicatesConfiguredPortableCandidateAndCombinesItsSources` |
| External detections are informational and cannot be selected or executed | `ExternalConfiguredDetectionIsInformationalAndIsNeverExecutedOrSelectable` |
| Executable version output is authoritative | `RejectsCandidatesWhoseVersionCommandIsNotAuthoritative`, `EmptyVersionOutputIsRejected` |
| Path/reparse/hostile filesystem inputs fail closed (TEST-006, SEC-002) | `ContainedConfiguredReparsePointIsRejectedBeforeExecution`, `PortableFilesAreFilteredAndUnsafeCandidatesAreReportedWithoutExecution`, `PortableSubdirectoriesSkipReparseAndUnreadableEntries` |
| External execution uses explicit arguments and bounded controls (SEC-005, PERF-004) | `DiscoversConfiguredAndPortableInstallationsAndVerifiesLiteralVersionOutput`, PB-0207 process-runner suites |
| Discovery is bounded and offline (TEST-012, PERF-004, INSTALL-005) | `PortableDepthLimitIsReportedOnlyOnce`, `PortableDirectoryCountIsBounded`, `ConfiguredCandidateCountIsBoundedAndDuplicateLimitFailuresAreSuppressed`, `PortableCandidateCountIsBoundedBeforeVerification` |
| Expected failures are structured and unexpected failures are not mislabeled (ENG-004) | `ExpectedFilesystemFailuresBecomeSafeDiagnostics`, `ConfiguredCandidateInspectionConvertsExpectedFailures`, `UnexpectedFilesystemFailureIsNotMisreportedAsAnExpectedFailure` |
| Cancellation is explicit | `CancellationStopsVerificationWithoutThrowing` |

## Validation Results

| Validation | Current result |
|---|---|
| Focused PB-0302 tests | Pass; 55 passed, 0 failed, 0 skipped. |
| Focused PB-0302 production coverage | Pass; every executable Contracts and Infrastructure class introduced by PB-0302 reports 100% line and branch coverage. |
| Complete Infrastructure suite | Pass; 494 passed, 0 failed, 0 skipped. |
| Complete five-project test portfolio | Pass; 1,841 passed, 0 failed, 0 skipped: Domain 846, Application 84, Infrastructure 494, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline | Pass; 29 checks passed, 0 failed. |
| Full local Core CI | Pass; all 9 fail-closed stages passed in 3 minutes 38 seconds. |
| Formatting, diff, security, and prohibited-content scans | Pass; information-level .NET formatting, `git diff --check`, and the repository baseline's prohibited/public-content checks succeeded. |

Generated coverage and test output remains beneath ignored `artifacts/PB-0302`.

## Manual Visual Test

Not applicable. PB-0302 changes Contracts and Infrastructure only and does not modify WPF. The
PB-1301 shell remains the current visual checkpoint.

## Final Publication Evidence

- Final task commit `35858af4635e8eb1be7d173714870b5c7af8a18e` was pushed on
  `feat/PB-0302-blender-locator`.
- The task was merged through [pull request #45](https://github.com/avivperets26/3DModels-Package-Builder/pull/45)
  into `main` as `7cdd964a582decf77eae093cb41e5f958d8387c0`.
- Required [main workflow run 30941209801](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30941209801)
  completed successfully for that exact merge commit.
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-04.
- No CI, completion, quality, or workflow exception was used.
- PB-0302 is recorded exactly once in the Completion Log during the PB-0303 rollover.
