# PB-0308 Exact Build Lock Evidence

**Task:** PB-0308 — Implement exact build lock generation
**Branch:** `feat/PB-0308-build-lock`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-05

## Scope

PB-0308 adds strict build-lock schema version 1 and deterministic generation for one build-job
identity. Each lock records the exact Package Builder, .NET SDK, Blender, Unity, Unreal, product
manifest schema, worker, and marketplace-profile versions required by the acceptance criterion.

Generation requires stable .NET and engine versions. Each of Blender, Unity, and Unreal must occur
exactly once and carry either Approved Latest or Last Known Good evidence. Worker identifiers and
marketplace/profile identities must be unique, their version sets must be non-empty, and all input
collections are snapshotted before canonical serialization.

## Determinism and Security

- Contract JSON uses a pinned embedded JSON Schema Draft 2020-12 definition with
  `additionalProperties: false`.
- JSON input is bounded, depth limited, comment and trailing-comma rejecting, object-only, and
  duplicate-property rejecting.
- Vendor-specific tool versions are reparsed through the typed domain grammar on deserialization.
- Worker entries sort by ordinal identifier; marketplace entries sort by ordinal marketplace then
  profile identity.
- Canonical compact UTF-8 JSON is hashed with SHA-256 and exposed as lowercase hexadecimal.
- The contract contains versions and logical identities only; it does not expose local paths,
  credentials, vendor assets, or installation data.

## Acceptance Mapping

| Requirement | Automated evidence |
|---|---|
| Record Package Builder, SDK, Blender, Unity, Unreal, and manifest-schema versions | `BuildLockJsonTests.CanonicalRoundTripRecordsEveryRequiredVersionAndSortsCollections` |
| Record every worker and marketplace-profile version deterministically | Canonical round-trip test and `DeterministicBuildLockGeneratorTests.GeneratesIdenticalCanonicalLockAndDigestRegardlessOfInputOrder` |
| Use only production-approved stable engines | Unapproved, incomplete, duplicate, and prerelease generator tests |
| Fail closed on hostile or incompatible contract input | Null, empty, oversized infrastructure, malformed, non-object, duplicate-property, unknown-property, vendor-invalid version, and duplicate-identity tests |

## Current Validation

| Validation | Current result |
|---|---|
| Focused PB-0308 tests | Pass; 11 contract and 5 application tests, 0 failed and 0 skipped. |
| Complete five-project test portfolio | Pass; 2,029 passed, 0 failed, 0 skipped: Domain 857, Application 103, Infrastructure 641, Contracts 413, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 2 minutes 39 seconds against the final source state. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Coverage instrumentation | Unresolved evidence gap; Coverlet 10.0.1 passed both focused suites but emitted reports with zero instrumented points, so no coverage percentage is claimed. |

One local Core CI rehearsal observed two unrelated existing process-lifecycle timing failures after
those same 641 Infrastructure tests had passed. Both failing tests passed immediately when retried,
and the subsequent complete final Core CI run passed all 641 Infrastructure tests. No production
or test change was made in response.

## Final Publication Evidence

- Final task commit: `466b913175af2902e8c20a32f1980418ecaf1bff`.
- Pull request: [#51](https://github.com/avivperets26/3DModels-Package-Builder/pull/51).
- Merge commit on `main`: `1454a5cbab08089042b7b2cd059b16a9a57a02d0`.
- Required successful `main` CI: [run 31013208586](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31013208586).
- User confirmation: 2026-08-05.
- No CI, completion, quality, or workflow exception was used. The invalid local coverage reports
  remain disclosed and no coverage percentage is claimed.

PB-0308 is synchronized `[x]` / 🟢 **DONE**, is absent from Active Work, and appears exactly once
in the Completion Log during the PB-0309 rollover.
