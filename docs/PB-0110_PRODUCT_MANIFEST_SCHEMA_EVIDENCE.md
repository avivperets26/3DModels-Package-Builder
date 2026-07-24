# PB-0110 Product Manifest Schema Evidence

**Task:** PB-0110 — Define and validate the product manifest schema
**Branch:** `feat/PB-0110-product-manifest-schema`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-24

## Scope

PB-0110 implements schema version 1 as a strict, offline, deterministic boundary shared by the
Domain and Contracts projects. It composes the approved PB-0103 source/texture, PB-0104 material,
PB-0105 rig/animation, PB-0106 item-group, PB-0107 profile-identity, and PB-0109 validation-finding
types. No filesystem loading, engine work, profile resolution, migration, or marketplace listing
policy is introduced.

PB-0109 publication was synchronized at the start of this branch from the supplied final task
commit, pull request, merge, successful required `main` CI, and explicit user confirmation.
PB-0110 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and is absent from the Completion
Log while its Git and completion gates remain.

## Versioned Contract

| Item | Approved value |
|---|---|
| Schema file | `schemas/product-manifest.schema.json` |
| Schema identity | `https://schemas.packagebuilder.dev/product-manifest/v1` |
| Dialect | JSON Schema Draft 2020-12 |
| Manifest schema version | integer `1` |
| Product version grammar | `major.minor.patch`, three non-negative decimal components without leading zeroes |
| Cases | `static`, `rigged`, `rigged-animated`, `item-set`, `item-collection` |
| Targets | `portable`, `unity`, `unreal` |
| Maximum JSON input | 1,048,576 UTF-16 characters |
| Maximum JSON nesting | 64 |

All owned objects use `additionalProperties: false`. Required fields reject missing values,
`null`, and wrong JSON types. Duplicate JSON property names are rejected before schema
evaluation. Conditional schema rules reject missing or contradictory case sections. Domain
validation rejects duplicate target, source, material, or animation identities; missing model
sources; undeclared texture/shared-asset sources; mismatched animation rigs; and remaining
case-level contradictions through blocking PB-0109 findings.

## Typed Boundaries

| Area | Implementation |
|---|---|
| Domain aggregate | `ProductManifest`, `ProductVersion`, and `ManifestMaterial` |
| Semantic result | `ProductManifestValidationResult` with immutable `ValidationFinding` values |
| JSON boundary | `ProductManifestJson` |
| Structured JSON results | `ProductManifestSerializationResult`, `ProductManifestDeserializationResult`, and `ProductManifestSchemaValidationResult` |
| Schema runtime | JsonSchema.Net 9.3.0, pinned centrally and in lock files |

Serialization writes one stable property order and canonical ordering for targets, sources,
materials, animations, and the PB-0103 through PB-0107 nested values. Optional sections are
omitted rather than serialized as `null`. Deserialization does not silently trim, normalize,
default, upgrade, or discard invalid input.

## Intentionally Deferred Sections

| Deferred concern | Owning task(s) |
|---|---|
| Publisher and marketplace profile schema bodies and resolution | PB-0111 and PB-0902 |
| Worker request, progress, and result envelopes | PB-0112 |
| Manifest migration and old-version upgrades | PB-0113 |
| Engine installation/version selection and locking | PB-0301 through PB-0309 |
| Blender inspection, inference, normalization, and export | PB-0405 through PB-0418 |
| Preview presentation, cameras, lighting, and gallery choices | PB-0906 through PB-0909 |
| Product wizard, material/rig/item editors, target selection, and file persistence UI | PB-1304 through PB-1309 |
| Marketplace listing, submission, and Fab-specific packaging rules | E09 through E12 tasks |

No `object`, dictionary, extension-data bag, or untyped optional section represents these deferred
concerns. The manifest includes only approved PB-0103 through PB-0107 typed intent.

## Five-Case Fixture Matrix

| Fixture | Case | Required section | Expected |
|---|---|---|---|
| `valid/static.json` | Static | None | Valid exact golden round trip |
| `valid/rigged.json` | Rigged | Rig | Valid exact golden round trip |
| `valid/rigged-animated.json` | Rigged animated | Rig and animation | Valid exact golden round trip |
| `valid/item-set.json` | Item set | Item set and assembly rules | Valid exact golden round trip |
| `valid/item-collection.json` | Item collection | Item collection | Valid exact golden round trip |
| `invalid/unknown-property.json` | Static | Unknown nested property | Schema rejection |
| `invalid/null-required.json` | Static | Null required value | Schema rejection |
| `invalid/wrong-type.json` | Static | Wrong target type | Schema rejection |
| `invalid/duplicate-source.json` | Static | Duplicate logical source | Domain rejection and blocking finding |
| `invalid/case-contradiction.json` | Static | Forbidden rig | Schema rejection |

Tests additionally mutate otherwise valid fixtures to cover unknown top-level and nested
properties, unknown schema versions, duplicate JSON properties, oversized and over-deep inputs,
malformed JSON, non-object roots, unresolved texture source references, nulls, wrong types, and
case contradictions.

## Dependency Decision

JsonSchema.Net 9.3.0 is the current approved stable version and supports .NET 10 and JSON Schema
Draft 2020-12. It is distributed under the MIT licence, operates offline, and does not require a
paid tool or hosted service. The central version, direct project reference, transitive graph, and
content hashes are pinned by `Directory.Packages.props` and tracked lock files. Licence and
dependency links are recorded in `docs/THIRD_PARTY_NOTICES.md`.

## Requirements-to-Tests Traceability

| PB-0110 requirement | Automated evidence |
|---|---|
| Valid schema identity and dialect | `EmbeddedSchemaUsesApprovedIdentityAndDialect` |
| All five cases deserialize and validate | `ValidFixturesRoundTripToExactDeterministicGoldenJson` |
| Exact deterministic serialization | Same golden test serializes twice and compares exact fixture bytes |
| Unknown properties, nulls, wrong types, versions, contradictions | `SchemaRejectsStrictContractViolations` and invalid-fixture theory |
| Duplicate JSON properties | `DuplicatePropertiesAreRejectedBeforeSchemaEvaluation` |
| Duplicate identities and unresolved references | `SemanticViolationsReturnDomainFindings` and Domain manifest tests |
| Bounded hostile input | `NullManifestAndOversizedInputAreRejected`, malformed/non-object tests, and depth mutation |
| Canonical product versions | `ProductVersionTests` valid/invalid theory |
| Offline/no-service runtime | Embedded schema and in-process JsonSchema.Net evaluation; no network API exists |

## Current Validation

| Validation | Result |
|---|---|
| Schema definition and five exact golden manifests | Pass; Draft 2020-12 identity/dialect and all five exact deterministic round trips validated |
| Focused Domain suite | Pass; 789 tests |
| Focused schema/Contracts suite | Pass; 185 tests |
| All four Release test projects | Pass; Domain 789, Contracts 185, Application 1, Infrastructure 1; 976 total |
| New Domain production coverage | Pass; all six generated class entries report 100% line and 100% branch coverage |
| New Contracts production coverage | Pass; all six generated class entries report 100% line and 100% branch coverage |
| Locked restore | Pass with repository-local .NET SDK 10.0.302 and tracked format-version-2 locks |
| Debug and Release builds | Pass; 0 warnings and 0 errors in each configuration |
| Solution architecture validator | Pass; 15 projects, 7 checks |
| Quality validator | Pass in Windows PowerShell 5.1 and repository-local PowerShell 7.6.4; 11 checks in each |
| ADR validator | Pass; 8 checks |
| Repository baseline with `RequireTrackedFiles` | Pass within full core CI; 29 checks |
| Dependency vulnerability audit | Pass; no vulnerable direct or transitive package reported for any of 15 projects |
| Formatting and diff checks | Pass; info-level `dotnet format --verify-no-changes` and `git diff --check` |
| Secret, personal-path, generated/binary, and prohibited-file scans | Pass; no match or prohibited changed path |
| Full core CI | Pass; all nine stages completed in 2 minutes 11.638 seconds with 976/976 tests |

## Remaining Gates

- User-controlled staging, task commit, branch push, merge, and `main` push.
- Successful required `main` CI.
- Explicit user completion confirmation after required `main` CI.
