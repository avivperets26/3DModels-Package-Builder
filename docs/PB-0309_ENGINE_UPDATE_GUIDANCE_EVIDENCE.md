# PB-0309 Engine Update and Installation Guidance Evidence

**Task:** PB-0309 — Implement engine update checks and installation guidance
**Branch:** `feat/PB-0309-engine-update-guidance`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-05

## Scope

PB-0309 adds an Application service that checks the official PB-0305 release catalogs for Blender,
Unity, and Unreal, compares their newest stable version with verified contained installations, and
returns deterministic per-engine status. It also creates and launches official installation
guidance without downloading software or running an installer.

The check is cache-only unless the caller records an explicit user-initiated network refresh.
Individual catalog failures are visible without hiding valid results for the other engines. Release
metadata provenance, fallback failure codes, exact candidate versions, current contained versions,
and any matching PB-0307 approval state remain available to future UI and CLI consumers.

## Consent, Containment, and Licensing Boundary

- A guidance plan accepts only a stable Blender, Unity, or Unreal version.
- The planned destination is deterministically confined to `tools/<engine>/<version>` beneath the
  canonical project root.
- Guidance opens only a code-owned official HTTPS vendor page and requires explicit confirmation
  immediately before external navigation.
- Package Builder does not download engines, start installers, accept terms, choose modules, select
  a subscription, determine eligibility, or claim that a free/paid tier applies.
- Blender guidance discloses the GNU GPL and links the official Blender licence page.
- Unity guidance links the current Unity Editor Software Terms and warns that plan eligibility,
  authorized-user, and seat conditions must be reviewed by the user.
- Unreal guidance links Epic's installation documentation and current Unreal Engine EULA, warning
  that eligibility, seat-subscription, and royalty conditions must be reviewed by the user.
- Existing contained locators remain the authority that verifies an installation after the user
  completes the vendor-controlled process.

Official references:

- [Blender download](https://www.blender.org/download/) and [Blender licence](https://www.blender.org/about/license/)
- [Unity installation manual](https://docs.unity3d.com/Manual/GettingStartedInstallingUnity.html) and [Unity Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software)
- [Unreal Engine installation](https://dev.epicgames.com/documentation/en-us/unreal-engine/install-unreal-engine) and [Unreal Engine EULA](https://www.unrealengine.com/eula/unreal)

## Acceptance Mapping

| Requirement | Automated evidence |
|---|---|
| Users can explicitly check official engine updates | `ExplicitCheckReportsMissingAndNewerStableCandidatesForAllEngines` |
| Startup/default behavior remains offline and cache-only | `DefaultCheckIsCacheOnlyAndReportsPerEngineCatalogFailure` |
| Missing and newer stable candidates are visible; previews are excluded | explicit-check test plus typed `EngineUpdateAvailability` assertions |
| Installation flow targets only the contained project tool root | `GuidanceUsesOfficialLinksContainedDestinationAndNoSilentActions`, unsafe-guidance theory |
| No silent download, installer, licence acceptance, or paid-tier assumption | guidance flag assertions for all three engines |
| External guidance launch requires explicit consent | `LaunchRequiresConfirmationAndOnlyOpensApprovedOfficialGuide` |
| Production launcher rejects non-allowlisted URLs and cancellation | `SystemEngineInstallationGuidanceLauncherTests` |
| Invalid or duplicate input fails before network or navigation | invalid-collection/root tests and unsafe-guidance theory |

## Current Validation

| Validation | Current result |
|---|---|
| Focused PB-0309 tests | Pass; Application 14 and Infrastructure 5, with 0 failed and 0 skipped. |
| Complete five-project test portfolio | Pass; 2,048 passed, 0 failed, 0 skipped: Domain 857, Application 117, Infrastructure 646, Contracts 413, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 4 minutes 33 seconds. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; Coverlet 10.0.1 passed both focused suites but emitted reports with zero instrumented points, so no coverage percentage is claimed. |

## Remaining Gates

- User stages, commits, pushes, and merges the task branch.
- Required `main` CI succeeds and the user explicitly confirms completion.
