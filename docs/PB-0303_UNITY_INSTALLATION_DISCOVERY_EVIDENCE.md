# PB-0303 Unity Installation Discovery Evidence

**Task:** PB-0303 — Implement Unity Hub editor discovery
**Branch:** `feat/PB-0303-unity-locator`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-04

## Scope

PB-0303 adds an offline Unity Editor locator under `PackageBuilder.Infrastructure.Tools` and a
typed consumer contract under `PackageBuilder.Contracts.Tools`. It discovers explicit configured
editor executables plus direct version directories in the conventional contained
`tools\unity\Hub\Editor` root and additional explicitly configured contained Hub roots. It verifies
caller-required module markers before running the contained editor's official `-version` command,
and it creates PB-0301 `ToolInstallation` values only from canonical Unity version output.

PB-0303 does not execute Unity Hub, parse external Hub state, download or install Unity or its
modules, inspect the Windows registry, execute an external editor, activate Unity, accept a Unity
licence, contact a network, select or persist an installation, or add UI. Release catalogs,
approval, persistence, first-run UI, and Unreal discovery remain with their documented tasks.

## Public Contract

| Type | Purpose |
|---|---|
| `IUnityInstallationLocator` | Runs one cancellation-aware deterministic discovery pass. |
| `UnityInstallationDiscoveryRequest` | Carries the job ID, canonical roots, contained process roots, configured executables, contained Hub roots, and required modules. |
| `UnityEditorModuleRequirement` | Defines one bounded safe ID and traversal-free relative file/directory marker beneath an editor installation. |
| `UnityEditorModuleDetection` | Records installed, missing, reparse-rejected, or unreadable state for one required marker. |
| `UnityInstallationDetection` | Records provenance, editor/module verification status, safe diagnostic, and an optional verified installation. |
| `UnityInstallationDiscoveryReport` | Returns ordered detections, verified installations, and discovery-wide failures. |
| `IUnityDiscoveryFileSystem` | Isolates the minimal filesystem reads required for deterministic hostile/boundary testing. |
| `PhysicalUnityDiscoveryFileSystem` | Implements that boundary through `System.IO`. |

Verified detections must contain an unselected, contained Unity `ToolInstallation`, and all their
module detections must be installed. Every other status forbids an installation value. External
configured editors therefore cannot become selectable.

## Discovery and Verification Policy

- Configured executable and Hub root paths must be canonical fully qualified Windows paths.
- Existing configured editors outside the approved tools root are informational only and are
  never sent to the process runner. External Hub roots are never enumerated.
- The default Hub editor root is `tools\unity\Hub\Editor`; additional roots must also be contained.
- Hub discovery inspects only direct version children and the documented
  `<version>\Editor\Unity.exe` layout, sorts paths ordinally, and deduplicates case-insensitively.
- Discovery is bounded to 32 unique Hub roots, 256 Hub editor directories, and 256 executable
  candidates.
- Project/tools roots, Hub roots, editor directories, editor executables, and installed module
  markers cannot cross reparse-point boundaries.
- Module IDs accept only a bounded ASCII identifier alphabet. Marker paths are canonical relative
  Windows paths without rooting, empty segments, `.`/`..`, alternate separators, or traversal.
- A candidate missing any required marker is rejected before process execution. Markers may be
  files or directories so a future target can identify the vendor artifact that proves its module.
- Contained candidates run directly with the single literal `-version` argument, no caller
  environment variables, a 4,096-character capture, and bounded 15-second startup/idle,
  30-second total, and 5-second graceful-shutdown intervals.
- Verification requires normal completion, exit code zero, untruncated output, and a first
  non-empty line matching the canonical PB-0301 Unity grammar `<major>.<minor>.<patch><a|b|f|p><revision>`.
- Raw stdout/stderr and rejected untrusted paths are not copied into discovery-wide diagnostics.
- PB-0207 performs the final existing-path, containment, and reparse checks immediately before
  execution, so a replacement after discovery still fails closed.

The layout and component model follow Unity's official
[Hub installation guidance](https://docs.unity3d.com/Manual/GettingStartedInstallingHub.html), and
the executable probe follows the official
[Unity Editor command-line `-version` argument](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html).

## Licensing Boundary

Unity Editor and Hub use remains subject to Unity's current plan eligibility, seat, subscription,
and other [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software).
Package Builder does not bundle Unity, determine eligibility, purchase a seat, activate a licence,
or accept terms for the user. PB-0303 only inspects configured local paths and runs the contained
editor's non-project version probe.

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| Contained configured and Hub editors are discovered and verified | `DiscoversHubAndConfiguredEditorsWithRequiredModulesAndLiteralVersionOutput` |
| Duplicate configured/Hub paths are deterministic | `DeduplicatesConfiguredAndHubCandidateAndCombinesSources` |
| Required directory and file module markers are verified | `DiscoversHubAndConfiguredEditorsWithRequiredModulesAndLiteralVersionOutput`, `FileModuleMarkerCanBeVerified` |
| Missing required modules reject an editor before execution | `MissingRequiredModuleRejectsCandidateBeforeExecution` |
| External editor detections are informational and cannot be selected or executed | `ExternalConfiguredEditorIsInformationalAndNeverExecutedOrSelectable` |
| External Hub roots are never enumerated | `ConfiguredHubRootFailuresAreSafeAndExternalRootsAreNeverEnumerated` |
| Unity layout and executable version output are authoritative | `ContainedConfiguredEditorMustUseVendorLayout`, `ContainedConfiguredExecutableWithWrongFilenameIsInvalidLayout`, `RejectsCandidatesWhoseVersionCommandIsNotAuthoritative` |
| Path/reparse/hostile filesystem inputs fail closed (TEST-006, SEC-002) | `InvalidDiscoveryRootsFailClosed`, `ConfiguredReparseAndExpectedInspectionFailureAreRejectedWithoutExecution`, `HubRootReparseAndUnreadableEnumerationAreReported`, `HubChildrenReportMissingReparseAndExecutableReparseOutcomes`, `RequiredModuleReparseAndUnreadableMarkersRejectBeforeExecution` |
| External execution uses explicit arguments and bounded controls (SEC-005, PERF-004) | `DiscoversHubAndConfiguredEditorsWithRequiredModulesAndLiteralVersionOutput`, PB-0207 process-runner suites |
| Discovery is bounded and offline (TEST-012, PERF-004, INSTALL-005) | `HubRootCountIsBounded`, `HubEditorDirectoryCountIsBoundedBeforeCandidateExecution`, `ConfiguredCandidateCountIsBoundedAndFailureIsReportedOnce` |
| Expected failures are structured and unexpected failures are not mislabeled (ENG-004) | `UnexpectedConfiguredInspectionFailureIsNotMisreported` and the root/Hub/module failure tests above |
| Cancellation is explicit | `CancellationStopsVerificationWithoutThrowing` |

## Validation Results

| Validation | Current result |
|---|---|
| Focused PB-0303 tests | Pass; 54 passed, 0 failed, 0 skipped. |
| Focused PB-0303 production coverage | Pass; every executable Contracts and Infrastructure class introduced by PB-0303 reports 100% line and branch coverage. |
| Complete Infrastructure suite | Pass; 548 passed, 0 failed, 0 skipped. |
| Complete five-project test portfolio | Pass; 1,895 passed, 0 failed, 0 skipped: Domain 846, Application 84, Infrastructure 548, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline | Pass; 29 checks passed, 0 failed. |
| Full local Core CI | Pass; after applying the script's information-level formatting corrections, the final implementation worktree passed all 9 fail-closed stages in 3 minutes 20 seconds. |
| Formatting, diff, security, and prohibited-content scans | Pass; information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and the repository baseline's prohibited/public-content checks succeeded. |

Generated coverage and test output remains beneath ignored `artifacts/PB-0303`.

The first complete Core CI attempt identified only information-level .NET formatting suggestions
in the new test sources and stopped at that fail-closed stage. The repository's exact formatter was
applied, the focused suite and 100% changed-production coverage were reverified, and the complete
Core CI script then passed without an exception or waived gate.

## Manual Visual Test

Not applicable. PB-0303 changes Contracts and Infrastructure only and does not modify WPF. The
PB-1301 shell remains the current visual checkpoint.

## Remaining Gates

PB-0303 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log. All local implementation
and validation gates pass. User-controlled commit and branch push, merge into and push of `main`,
successful required `main` CI, explicit user completion confirmation, and next-task rollover
remain. No exception is used.
