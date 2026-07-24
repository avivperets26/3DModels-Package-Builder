# PB-0107 Publisher and Marketplace Profile Domain Evidence

**Task:** PB-0107 — Implement publisher and marketplace profile models  
**Branch:** `feat/PB-0107-profile-domain`  
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope

PB-0107 adds immutable publisher and generic marketplace profile intent in
`PackageBuilder.Domain.Profiles`. It represents a configurable publisher root, publisher display
name, offline-validated support contact, explicit copyright-year policy, explicit AI-assistance
disclosure, optional logo/watermark source declarations, and generic marketplace profile identity.

Publisher and marketplace identities are separate aggregates. `PublisherProfile` has no
marketplace property, and `MarketplaceProfile` has no publisher root, publisher contact, branding,
or listing behavior. Production code contains no hard-coded publisher, marketplace, personal
contact, or identity default.

This branch also performed the approved PB-0106 rollover. PB-0106 is `[x]` / 🟢 **DONE**, is absent
from Active Work, and appears exactly once in the Completion Log.

## Public API

| API | Purpose |
|---|---|
| `PublisherDisplayName.Create(...)` | Creates a validated publisher display name distinct from product identity. |
| `SupportContact.CreateEmail(...)` | Creates an offline syntactically validated support email address. |
| `SupportContact.CreateSecureUrl(...)` | Creates an absolute credential-free HTTPS support URL. |
| `CopyrightHolder.Create(...)` | Creates the validated holder named in a copyright notice. |
| `CopyrightYearPolicy.Create(...)` | Creates explicit single-year, range, or publication-year intent without reading the clock. |
| `CopyrightNotice.Create(...)` | Combines a holder and explicit year policy. |
| `AiDisclosure.Create(...)` | Combines an explicit disclosure state with optional consistent caller-authored text. |
| `BrandingImage.Create(...)` | Associates the `logo` or `watermark` role with an Image `SourceAsset`. |
| `PublisherBranding.Create(...)` | Creates a non-empty immutable unique-role branding snapshot. |
| `PublisherProfile.Create(...)` | Combines the existing `PublisherRoot` with publisher metadata and optional branding. |
| `MarketplaceIdentifier.Create(...)` | Creates an extensible generic marketplace identifier. |
| `MarketplaceProfileIdentifier.Create(...)` | Creates a stable profile identifier within one marketplace. |
| `MarketplaceProfile.Create(...)` | Combines marketplace and profile identity without listing rules. |
| `ProfileValidationResult<T>` | Returns PB-0107 expected-input failures without throwing or pre-empting PB-0109. |

## Validation Invariants

### Publisher identity and text

- `PublisherRoot` is reused directly; changing it changes publisher package-root identity without
  changing production code.
- Publisher display name and copyright holder preserve accepted Unicode and casing exactly.
- Both reject null, empty, whitespace-only, leading/trailing whitespace, control characters, and
  values longer than 256 UTF-16 code units.
- Equality and hashing are ordinal, case-sensitive, stable, and culture-independent.

### Support contact

- A contact is explicitly an email address or secure URL; the two forms remain distinguishable.
- Email validation is syntactic and deterministic, including local-part, domain-label, separator,
  length, whitespace, and control-character checks.
- URL validation accepts only absolute HTTPS URLs with a non-empty host and rejects URI
  credentials, unsafe schemes, malformed values, whitespace, controls, and values over 2048
  UTF-16 code units.
- Validation performs no DNS lookup, HTTP request, email delivery, authentication, or other
  network operation.

### Copyright policy

- Approved states are `single-year`, `year-range`, and `publication-year`.
- Years are explicit integers from 1 through 9999. Publication year is supplied by the caller and
  is never inferred from the system clock.
- A range requires both years and requires the start to precede the ending year.
- Non-range policies reject a supplied start year. Missing, out-of-range, degenerate, reversed,
  and contradictory combinations fail structurally.

### AI disclosure

- Approved states are `undeclared`, `no-ai-assistance`, and `ai-assisted`.
- State and optional prose are separate values; no claim is inferred from the presence or absence
  of branding, source files, or other metadata.
- `undeclared` forbids prose because attaching text would contradict the absence of a declared
  claim.
- Declared states may omit prose or include caller-authored text up to 4096 UTF-16 code units.
  Empty, whitespace-only, edge-whitespace, control-character, and over-limit text is rejected.

### Branding

- Branding is optional at the `PublisherProfile` boundary.
- A present `PublisherBranding` contains at least one `logo` or `watermark` declaration.
- Each declaration reuses an immutable `SourceAsset` and requires `SourceAssetKind.Image`.
- Null declarations, non-image sources, and duplicate roles are rejected.
- Returned images are immutable snapshots ordered `logo`, then `watermark`, independent of input
  order.
- PB-0107 declares source and role only; it does not decode, resize, position, blend, render, or
  otherwise process an image.

### Marketplace identity

- Marketplace and marketplace-profile identifiers are extensible lowercase ASCII identifiers
  beginning with a letter and containing letters, digits, or single hyphen-separated segments.
- Identifiers preserve exact values and use ordinal case-sensitive equality and hashing.
- No marketplace is registered or defaulted in core Domain code, and no marketplace-specific
  listing, packaging, media, engine, or submission rule is embedded.

## Deliberately Deferred Profile Fields

| Deferred field or behavior | Owning future work |
|---|---|
| JSON schemas, converters, examples, and schema-version rules | PB-0111 |
| Profile file loading, saving, and migration | PB-0113 and later persistence work |
| Documentation boilerplate semantics and rendering | PB-0901 |
| Publisher-profile resolution and configured default application | PB-0902 |
| Unity namespace and assembly-name behavior | PB-0602, PB-0605, and PB-0902 |
| Unreal project/pack prefix behavior | PB-1105 and PB-0902 |
| Default render pipeline and engine-version selection/locking | PB-0306, PB-0308, and PB-0902 |
| Preview-theme and presentation semantics | PB-0906 and PB-0902 |
| Marketplace requirements and listing rules | PB-1001 through PB-1009 |
| Branding decoding, resizing, positioning, watermark rendering, and media processing | PB-0906 through PB-0909 |
| Profile editor UI | PB-1303 |

## Test Inventory

The focused xUnit v3 suite covers valid publisher and marketplace profiles; configurable
`PublisherRoot` reuse; display-name and text boundaries; valid email and HTTPS contacts; null,
empty, whitespace, control, malformed, credentialed, unsafe-scheme, and unusually large contacts;
all copyright and AI-disclosure states; contradictory year and disclosure combinations; valid
logo/watermark declarations; non-image, missing, null, duplicate, and conflicting branding data;
publisher/marketplace separation; exact ordinal casing; immutable deterministic collections;
equality and stable hashing; Turkish-culture independence; unusually large inputs; Domain
dependency isolation; and absence of hard-coded personal or marketplace defaults.

## Coverage Evidence

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` with
`ExcludeAssembliesWithoutSources=None`. Generated reports remain beneath ignored
`artifacts/PB-0107`.

All 18 new production files report 100% line coverage and 100% branch coverage in the final
formatted focused run.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0107 tests | Pass; 96 passed, 0 failed, 0 skipped. |
| Per-file coverage | Pass; all 18 new production files report 100% line and 100% branch coverage. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 600 passed, 0 failed, 0 skipped, preserving the previous 504 tests. |
| All four core test projects | Pass; 603 discovered, 603 passed, 0 failed, 0 skipped. |
| Solution architecture validator | Pass; 7 checks passed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| ADR validator | Pass; 8 checks passed, 0 failed. |
| Repository baseline with `RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| Full core CI | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and public-repository scans | Pass; `.NET` formatting, Ruff lint/format, `git diff --check`, and repository secret/personal-path/generated/prohibited-content scans. |

## Publication and Completion Evidence

- Final task commit `7f82d16fe258d7cb81a2c9d3d01c1de8be85c37f` was pushed on
  `feat/PB-0107-profile-domain`.
- [Pull request #21](https://github.com/avivperets26/3DModels-Package-Builder/pull/21)
  merged that exact task commit into `main` as
  `236343156b69714376b5f48ec4267483bb991307`.
- [PR workflow run 30109012948](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30109012948)
  and required [main workflow run 30108791899](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30108791899)
  were reverified on 2026-07-24. Each completed successfully with both `Validate repository
  baseline` and `Validate core application` successful.
- The user explicitly confirmed task commit, push, merge, successful required `main` CI, and
  completion on 2026-07-24.
- No CI, completion, or quality exception was used.

The approved PB-0108 rollover now marks PB-0107 `[x]` / 🟢 **DONE**, removes it from Active Work,
and records it exactly once in the Completion Log.
