# PB-0305 Official Release Catalog Evidence

**Task:** PB-0305 — Implement official release-catalog providers
**Branch:** `feat/PB-0305-release-catalogs`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-05

## Scope

PB-0305 adds typed official release catalogs under `PackageBuilder.Contracts.Tools` and a
cache-first provider plus bounded HTTP transport under `PackageBuilder.Infrastructure.Tools`.
Catalogs normalize current stable, LTS where the vendor publishes that classification, and preview
metadata into PB-0301 canonical versions with an official source URI and UTC refresh timestamp.

Network refresh is explicitly caller-controlled. The default request is cache-only and makes no
outbound call. A successful opted-in refresh stores the bounded raw response beneath
`downloads\release-catalogs\<tool>` and atomically replaces the normalized schema-versioned cache
beneath `runtime-data\cache\release-catalogs\v1`. Transport, parsing, response-validation, or
persistence failure returns the last valid cache with distinct `LastKnownCache` provenance and a
sanitized failure code. A missing, corrupt, incompatible, wrong-tool, wrong-source, or malformed
cache fails closed.

PB-0305 does not schedule background updates, download or install tools, verify installers, select
the approved default, accept licence terms, determine vendor eligibility, or add UI. PB-0306 owns
selection; PB-0309 owns user-facing update checks and installation guidance; PB-1811 owns the
complete download-verification and network-consent security gate.

## Public Contract

| Type | Purpose |
|---|---|
| `IOfficialReleaseCatalogProvider` | Performs one cancellation-aware cache lookup or explicitly consented refresh. |
| `OfficialReleaseCatalogRequest` | Carries the tool, canonical project/download/cache roots, and explicit refresh consent. |
| `OfficialReleaseCatalog` | Immutable validated tool/source/refresh metadata with deterministic newest-first releases. |
| `OfficialToolRelease` | Carries a canonical vendor version, stable/preview channel through `ToolVersion`, LTS/standard support, and optional official release time. |
| `OfficialReleaseCatalogSnapshot` | Returns catalog origin, contained raw/cache paths, and optional refresh-failure provenance. |
| `IOfficialReleaseMetadataTransport` | Isolates bounded network access for deterministic offline tests and future composition. |
| `HttpReleaseMetadataTransport` | Streams HTTPS responses with a 4 MiB bound, cancellation, status checks, and same-authority redirect enforcement. |

Expected validation, network, parsing, cache, and cancellation failures use typed sanitized
`ReleaseCatalogResult<T>` values. Raw response bodies, exception messages, credentials, and local
paths are not copied into user-facing errors.

## Official Sources and Normalization

| Tool | Official source | Normalization |
|---|---|---|
| .NET | [Microsoft .NET release index](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) | Current non-EOL channels use `latest-sdk`; `release-type=lts` marks LTS and the SDK version determines stable/preview. |
| Unity | [Unity Release API](https://services.docs.unity.com/release/v1/) | Published `version` and `releaseDate` values are validated; `stream=LTS` marks LTS and Unity `a`/`b` versus `f`/`p` determines preview/stable. |
| Blender | [Blender releases](https://www.blender.org/download/releases/) | Explicit release labels are parsed; `LTS` marks LTS and Alpha/Beta/RC marks preview. Two-part families normalize to patch `.0`. |
| Unreal | [Epic Unreal What's New](https://dev.epicgames.com/documentation/unreal-engine/whats-new?lang=en-US) | Explicit Unreal release-note and preview labels are parsed. Two-part families normalize to patch `.0`; Epic does not publish an LTS label here. |

The .NET and Unity JSON adapters reject duplicate properties, invalid structure, invalid canonical
versions, and excessive release counts. Blender and Unreal are bounded official-page adapters
because no documented public machine-readable catalog was identified for those vendor indexes.
If their markup changes, refresh fails and last-known metadata remains available; the provider does
not scrape arbitrary pages or infer an unannounced release.

## Containment, Cache, and Network Policy

- Project, downloads, and cache roots must be canonical fully qualified paths. Downloads and cache
  must be strict descendants of the project root with final segments `downloads` and `cache`.
- Existing reparse-point boundaries from the project root to either storage root are rejected.
- Raw and normalized metadata inputs are limited to 4 MiB and normalized catalogs to 512 releases.
- Cache schema, tool, source URI, UTC timestamps, support values, versions, duplicates, and release
  count are revalidated on every read.
- Cache writes use same-directory temporary files, durable flush, and atomic replacement; a
  per-cache asynchronous gate prevents concurrent writers.
- Redirects outside the configured source's HTTPS authority are rejected. The transport sends no
  authentication, personal data, source assets, or telemetry.
- Cancellation propagates through the gate and transport and remains an explicit cancelled result.
- The default test suite uses stub transports only and requires no internet access.

## Licensing Boundary

Catalog discovery does not grant rights to use any listed tool. .NET remains subject to
[Microsoft's .NET licensing](https://dotnet.microsoft.com/platform/free); Blender is GPL software
under the [Blender licence](https://www.blender.org/about/license/). Unity use remains subject to
Unity's current [legal terms and eligibility conditions](https://unity.com/legal), including any
applicable plan or seat requirements. Unreal use remains subject to Epic's current
[Unreal Engine EULA](https://www.unrealengine.com/eula/unreal), including applicable eligibility,
seat-subscription, and royalty conditions. Package Builder does not determine eligibility, buy or
assign seats, calculate royalties, download an engine, sign in, or accept terms for the user.

## Acceptance and Requirements Mapping

| Criterion or requirement | Automated evidence |
|---|---|
| Provider abstraction returns source and UTC refresh metadata | `CatalogSnapshotsAndSortsCanonicalVersionsNewestFirst`, `RefreshNormalizesAndAtomicallyStoresOfficialDotNetMetadata` |
| Stable/LTS/preview metadata is normalized for all four tools | `.NET` refresh test, `VendorAdaptersReturnStableLtsAndPreviewMetadata`, `UnityAdapterPreservesLtsClassificationAndReleaseTimestamp` |
| Raw and normalized data stay beneath repository-local download/cache roots | `RefreshNormalizesAndAtomicallyStoresOfficialDotNetMetadata`, `RejectsUnsafeOrMisconfiguredRootsBeforeTransport` |
| Network access requires explicit consent; default operation is offline (TEST-012, SEC-012) | `CacheOnlyIsOfflineByDefault`, `CacheOnlyReturnsPersistedMetadataWithoutTransportAccess`, `RejectsInvalidRequestsWithoutSending` |
| Network or parsing failure uses last known metadata | `NetworkFailureReturnsLastKnownValidMetadataWithFailureProvenance`, `InvalidFreshMetadataDoesNotReplaceLastKnownCache` |
| Cache identity, compatibility, corruption, concurrency, and atomicity fail closed (PERF-005) | cache-only round trip, incompatible-cache test, fresh-invalid preservation test, `ConcurrentRefreshesSerializeWritesAndLeaveAReadableCache`, and no-temporary-file assertions |
| Work, response size, redirects, and cancellation are bounded (TEST-006, PERF-003, PERF-004) | `RejectsDeclaredAndStreamedOversizedResponses`, `OversizedOrCrossAuthorityPayloadCannotPopulateCache`, `CancellationStopsBeforeNetworkOrCacheMutation` |
| Expected failures are typed and sanitized (ENG-004, SEC-007) | contract-invalid scenarios, provider error assertions, and HTTP status/redirect tests |

## Current Validation Results

| Validation | Current result |
|---|---|
| Focused PB-0305 production build | Pass; 0 warnings, 0 errors. |
| Focused PB-0305 tests | Pass; 34 passed, 0 failed, 0 skipped. |
| Complete Infrastructure suite | Pass; 628 passed, 0 failed, 0 skipped in the final standalone and Core CI runs. |
| Complete five-project test portfolio | Pass; 1,975 passed, 0 failed, 0 skipped: Domain 846, Application 84, Infrastructure 628, Contracts 402, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline | Pass; 29 checks passed, 0 failed. |
| Full local Core CI | Pass; all 9 fail-closed stages passed in 3 minutes 18 seconds against the final source state. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; repeated Coverlet 10.0.1 runs passed all 627 tests but emitted an empty report with zero instrumented points, so no coverage percentage is claimed. |

Generated metadata and test output remain beneath ignored `downloads`, `runtime-data`, and
`artifacts/PB-0305` roots.

One earlier Core CI attempt observed the pre-existing PB-0208
`TotalTimeoutWinsWhileHeartbeatPreventsIdleTimeout` test classify a constrained process start as
`StartupTimedOut` instead of `TotalTimedOut`. Isolated repetition passed three times and reproduced
once; the next complete Infrastructure suite and the final complete Core CI run both passed. No
PB-0208 source or test was changed on this PB-0305 branch. The intermittent result remains
disclosed for a separately scoped follow-up rather than being hidden or repaired across task scope.

## Manual Visual Test

Not applicable. PB-0305 changes Contracts and Infrastructure only and does not modify WPF. The
PB-1301 shell remains the current visual checkpoint.

## Remaining Gates

- User stages and commits PB-0305 on `feat/PB-0305-release-catalogs`.
- User pushes the task branch and merges it into `main` through an optional PR or approved direct
  merge.
- Required `main` CI succeeds for the merge commit.
- User explicitly confirms completion.
- Resolve or explicitly disposition the invalid coverage-instrumentation evidence before claiming
  the detailed coverage gate for PB-0305.
- Track the unrelated PB-0208 timeout-classification flake in a separately scoped task if it
  reproduces in required branch or `main` CI.
- PB-0305 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until the PB-0306
  rollover records those gates.
