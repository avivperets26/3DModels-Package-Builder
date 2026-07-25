# PB-0111 Publisher and Marketplace Profile Schema Evidence

**Task:** PB-0111 — Define publisher and marketplace profile schemas  
**Branch:** `feat/PB-0111-profile-schemas`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-07-24

## Scope and Rollover

PB-0111 defines strict schema-version-1 JSON boundaries for the PB-0107 `PublisherProfile` and
generic `MarketplaceProfile` Domain aggregates. It adds Draft 2020-12 schemas, embedded offline
validation, deterministic C# serialization/deserialization, public-safe examples, retained invalid
fixtures, exact golden tests, parity tests, round-trip tests, and PB-0110 manifest-reference
compatibility.

The PB-0110 rollover uses final task commit
`a88ed992002b34ffdb96a8b1e7b7b596609d6891`, pull request #24, merge
`1980b801147e673df23b8d741fe8b5f3bc59832b`, successful PR run `30118538985`, successful
required `main` run `30118545085`, and user confirmation dated 2026-07-24. No exception was used.
PB-0110 is `[x]` / 🟢 **DONE**, absent from Active Work, and logged exactly once. PB-0111 remains
`[ ]` / 🟡 **PROCESS**, active, and absent from the Completion Log.

## Versioned Schemas

| Contract | Schema | Identifier | Version |
|---|---|---|---:|
| Publisher profile | `schemas/publisher-profile.schema.json` | `https://schemas.packagebuilder.dev/publisher-profile/v1` | 1 |
| Marketplace profile | `schemas/marketplace-profile.schema.json` | `https://schemas.packagebuilder.dev/marketplace-profile/v1` | 1 |

Both schemas use JSON Schema Draft 2020-12 and `additionalProperties: false` at every owned object
level. Runtime parsing rejects unknown versions and tokens, missing/unknown/null/wrongly typed
properties, recursive duplicate properties, comments, trailing commas, non-object roots, malformed
JSON, depth over 64, and input over 1,048,576 UTF-16 characters. Validation is deterministic,
offline, and performs no filesystem or network operation.

## Exact JSON Structures

Publisher version 1 uses this ordered structure:

```json
{
  "schemaVersion": 1,
  "root": "PublisherRoot",
  "displayName": "Publisher display name",
  "supportContact": {
    "kind": "email | secure-url",
    "value": "validated contact"
  },
  "copyright": {
    "holder": "Copyright holder",
    "yearPolicy": {
      "kind": "single-year | year-range | publication-year",
      "startYear": 2024,
      "year": 2026
    }
  },
  "aiDisclosure": {
    "state": "undeclared | no-ai-assistance | ai-assisted",
    "text": "optional caller-authored text"
  },
  "branding": {
    "images": [
      {
        "role": "logo | watermark",
        "source": {
          "kind": "image",
          "logicalReference": "safe/source/reference.png",
          "originalFileName": "optional-source-name.png"
        }
      }
    ]
  }
}
```

`startYear` exists only for `year-range`. Disclosure `text` is forbidden for `undeclared` and is
optional for declared states. `branding` is optional; when present it is non-empty, roles are
unique and serialized logo before watermark, and every source is an image. Optional values are
omitted rather than written as JSON `null`.

Marketplace version 1 is deliberately identity-only:

```json
{
  "schemaVersion": 1,
  "marketplace": "fab",
  "profile": "default"
}
```

Publisher and marketplace identities remain separate. Full profiles are not embedded in product
manifests.

## Public Examples and Placeholders

`profiles/publishers/AvivPeretsFBX.example.json` uses root/display name `AvivPeretsFBX`, reserved
non-production URL `https://example.com/package-builder-support`, placeholder holder
`Publisher Name (review before publication)`, placeholder publication year `2026`, AI state
`undeclared`, and no branding. Support, copyright, and disclosure values require user review
before real publication. PB-0111 does not finalize them or replace placeholders automatically.

`profiles/marketplaces/fab.identity.example.json` uses `fab` / `default`. It proves identity
serialization only and is not a Fab requirements profile. It contains no Fab packaging, media,
listing, engine, documentation, upload, submission, or authentication rule. PB-1001 owns the
versioned Fab requirements profile; PB-1002 through PB-1009 own Fab behavior.

## Domain and Manifest Compatibility

`PublisherProfileJson` reconstructs the exact PB-0107 aggregate and preserves support kind,
copyright invariants, AI state/text consistency, branding source safety, Unicode text, and ordinal
identity. `MarketplaceProfileJson` reconstructs only the marketplace/profile pair.

The PB-0110 static fixture references publisher `AvivPeretsFBX` and marketplace/profile
`fab` / `default`. Cross-contract tests prove equality with both retained examples without
weakening PB-0110 parsing.

## Dependency Impact

No dependency changed. PB-0111 reuses centrally pinned JsonSchema.Net 9.3.0 (MIT). Both profile
contracts share one internal utility for limits, strict parsing, recursive duplicate detection,
Draft 2020-12 construction, embedded schema validation, and offline instance evaluation.
Third-party notices therefore require no change.

## Retained Test Matrix

| Area | Automated evidence |
|---|---|
| Exact examples | Golden parse, serialize twice, byte comparison, Domain equality round trip |
| Strict structure | Missing, null, wrong type, unknown token/version, unknown property at every object level |
| Parser safety | Duplicate top-level/nested/array-object properties, malformed/non-object/deep/oversized input |
| Support | Email, secure URL, contradictions, malformed forms, HTTP, credentialed HTTPS |
| Copyright | All policies, policy-property exclusion, reversed/degenerate range, explicit years |
| AI disclosure | All states, optional declared text, forbidden undeclared text |
| Branding | Absent, logo, watermark, both, duplicates, non-image, unsafe reference, invalid original name |
| Compatibility | Publisher root and marketplace/profile equality against PB-0110 static manifest |
| Determinism | Repeated byte identity, Unicode, Turkish culture, ordinal identities |

Invalid fixtures live beneath `tests/fixtures/profiles/invalid`. The two public examples are the
retained valid golden fixtures and are copied directly into test output, avoiding divergent valid
copies.

## Deferred Work

| Concern | Owner |
|---|---|
| Migration | PB-0113 |
| Loading, saving, discovery, defaults, resolution | PB-0113, PB-0902 |
| Documentation boilerplate/rendering | PB-0901 through PB-0905 |
| Branding processing and watermark rendering | PB-0906 through PB-0909 |
| Unity namespace/assembly generation | PB-0602, PB-0605, PB-0902 |
| Unreal project/pack prefix | PB-1105, PB-0902 |
| Engine and preview defaults | PB-0306, PB-0308, PB-0902, PB-0906 |
| Fab requirements and behavior | PB-1001 through PB-1009 |
| Profile editor UI | PB-1303 |

## Current Validation

| Validation | Result |
|---|---|
| Focused Contracts suite | Pass; 231 passed, 0 failed, 0 skipped |
| New Contracts coverage | Pass; 100% line and 100% branch coverage for all new profile-contract source files |
| Debug Contracts build | Pass; 0 warnings, 0 errors |
| Locked restore | Pass |
| Debug and Release solution builds | Pass; 0 warnings, 0 errors |
| Full local core CI | Pass; all 9 stages in 1 minute 14.6 seconds |
| Release test portfolio | Pass; 1,022 passed, 0 failed, 0 skipped (Domain 789, Contracts 231, Application 1, Infrastructure 1) |
| Architecture, ADR, and quality validators | Pass; 7/7, 8/8, and 11/11 respectively; quality validator also passes under Windows PowerShell 5.1 |
| Dependency vulnerability audit | Pass; no known vulnerable direct or transitive packages |
| Formatting and repository safeguards | Pass; .NET format, Ruff, `git diff --check`, repository baseline, and prohibited-content checks |

## Remaining Gates

- User-controlled staging, task commit, branch push, merge, and `main` push.
- Successful required `main` CI.
- Explicit user completion confirmation after required `main` CI.
