# PB-0104 Material Domain Evidence

**Task:** PB-0104 — Implement renderer-independent material definitions
**Branch:** `feat/PB-0104-material-domain`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-07-24

## Scope

PB-0104 adds immutable renderer-independent material intent in `PackageBuilder.Domain.Materials`. It reuses PB-0103 `TextureAssignment`, `TextureRole`, and `SourceAsset` APIs and preserves their eight canonical roles. No Unity, Unreal, Blender, WPF, filesystem, persistence, networking, marketplace, rendering, image conversion, channel packing, or specular-workflow behavior is added.

This branch also performed the approved PB-0103 rollover. PB-0103 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion Log with its supplied publication evidence. PB-0104 passed its publication gates and is synchronized as `[x]` / 🟢 **DONE** during the PB-0105 rollover.

## PB-0104 Final Publication Evidence

- Final task commit: `5d3f52c107f3de7fa5bac80d85559c80aeaad6b4`.
- Pull request: [#18](https://github.com/avivperets26/3DModels-Package-Builder/pull/18).
- Successful PR workflow: [run 30097711367](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30097711367).
- Merge commit: `1983201ea7a810aac4ca74db0351d73c5554a929`.
- Successful required main workflow: [run 30097716685](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30097716685).
- The user explicitly confirmed completion on 2026-07-24.
- No CI, completion, or quality exception was used.

## PB-0103 Final Publication Evidence

- Final task commit: `3e21b2aa118f43dc024a377eb855e08af4838c4b`.
- Pull request: [#17](https://github.com/avivperets26/3DModels-Package-Builder/pull/17).
- Successful PR workflow: [run 30095076362](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30095076362).
- Merge commit: `b562be3a69c97d1b8eb7924c48ea47b1b4727eb2`.
- Successful required main workflow: [run 30095081353](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30095081353).
- The user explicitly confirmed completion on 2026-07-24.
- No CI, completion, or quality exception was used.

## Public API

| API | Purpose |
|---|---|
| `SurfaceMode.Opaque`, `.Cutout`, `.Transparent` | Closed renderer-independent surface modes. |
| `SurfaceMode.All` / `.TryParse(string?)` | Stable read-only registry and exact ordinal expected-failure parsing. |
| `EmissionProperties.Create(...)` | Validates immutable linear RGB emission and intensity. |
| `EmissionPropertiesValidationResult` | Returns task-local structured emission validation. |
| `UvTransform.Create(...)` | Validates immutable renderer-independent UV scale and offset. |
| `UvTransformValidationResult` | Returns task-local structured UV validation. |
| `MaterialDefinition.Create(...)` | Validates and snapshots complete renderer-independent material intent. |
| `MaterialDefinitionValidationResult` | Returns a material or a precise task-local expected-input error. |

All constructors that could retain invalid state are private. PB-0104 does not pre-empt PB-0109's global validation-finding or stable error-code model.

## Numeric Contract

| Property | Valid values |
|---|---|
| Metallic factor | Finite inclusive interval `[0, 1]` |
| Roughness factor | Finite inclusive interval `[0, 1]` |
| Normal scale | Finite values greater than or equal to `0`; no arbitrary maximum |
| Emission RGB | Finite values greater than or equal to `0`; HDR values above `1` remain valid |
| Emission intensity | Finite values greater than or equal to `0`; no arbitrary maximum |
| Ambient-occlusion strength | Finite inclusive interval `[0, 1]` |
| Height/displacement scale | Any finite signed value |
| Opacity | Finite inclusive interval `[0, 1]` |
| Alpha cutoff | Cutout-only finite inclusive interval `[0, 1]` |
| UV scale/offset | Any finite signed values, including zero and negative scale |

Every applicable field rejects `NaN`, positive infinity, and negative infinity. These ranges encode factor semantics or the smallest renderer-independent constraint; no undocumented texture-size, displacement, UV, or HDR limit is introduced.

## Surface Invariants

- Opaque requires opacity `1` and forbids alpha cutoff.
- Cutout accepts unit-interval opacity and requires a unit-interval alpha cutoff.
- Transparent accepts unit-interval opacity and forbids alpha cutoff.
- Validation order is deterministic and returns one structured task-local error for expected invalid input.

## Texture Assignment Invariants

- Every PB-0103 canonical role is accepted: Albedo, Normal, Metallic, Roughness, Emission, Ambient Occlusion, Opacity, and Height.
- The input collection and retained output are immutable snapshots.
- Retained assignments use the stable `TextureRole.All` order regardless of input order.
- More than one assignment for the same canonical role is rejected even when source images differ.
- Null collections and null elements are rejected.

## Determinism and Equality

`SurfaceMode`, `EmissionProperties`, `UvTransform`, and `MaterialDefinition` are immutable value-style types. Equality includes every retained field and uses the ordinal PB-0103 source and role identities. Stable FNV-based hashing uses numeric bit representations, normalizes signed zero consistently with equality, and does not depend on current culture, UI culture, process-randomized string hashing, or input assignment order.

## Deferred Work

- PB-0109 owns global validation findings and stable finding codes.
- PB-0110 owns manifest serialization, schemas, and converters.
- PB-0406/PB-0413 own source classification and material/image normalization.
- PB-0606 through PB-0608 own Unity import settings, packing, shaders, assets, and render queues.
- PB-1106 through PB-1108 own Unreal import settings, ORM packing, materials, and instances.
- Image decoding, conversion, rendering, visual previews, filesystem access, and engine integration are not implemented.
- A UV-set index, base-colour factor, or specular workflow requires a later explicit approved requirement; PB-0104 does not invent their semantics.

## Test Inventory

The xUnit v3 PB-0104 public-behavior suite covers:

- All three surface modes, identifiers, display names, exact parsing, casing, equality, culture independence, stable order, and immutable registry behavior.
- Emission boundaries, HDR values, every negative/non-finite rejection, immutability, equality, signed-zero hashing, and culture independence.
- UV finite positive, zero, negative, and extreme values; every NaN/infinity position; immutability, equality, signed-zero hashing, and culture independence.
- Every material scalar boundary and invalid out-of-range, NaN, positive-infinity, and negative-infinity case.
- Every valid surface mode and every valid or invalid opacity/alpha-cutoff relationship.
- Every canonical texture role, duplicate rejection, null input, snapshot immutability, canonical ordering, equality, casing, and deterministic culture-independent hashing.
- Domain dependency isolation and preservation of all existing Domain behavior.

## Coverage Evidence

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` with `ExcludeAssembliesWithoutSources=None`. Generated reports remain beneath ignored `artifacts/PB-0104`.

| Production file | Line coverage | Branch coverage |
|---|---:|---:|
| `EmissionProperties.cs` | 100% | 100% |
| `EmissionPropertiesValidationResult.cs` | 100% | 100% |
| `MaterialDefinition.cs` | 100% | 100% |
| `MaterialDefinitionValidationResult.cs` | 100% | 100% |
| `StableMaterialHash.cs` | 100% | 100% |
| `SurfaceMode.cs` | 100% | 100% |
| `UvTransform.cs` | 100% | 100% |
| `UvTransformValidationResult.cs` | 100% | 100% |

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0104 tests | Pass; 98 passed, 0 failed, 0 skipped. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 358 passed, 0 failed, 0 skipped, preserving the previous 260 tests. |
| Per-file coverage | Pass; all eight new production files report 100% line and 100% branch coverage. |
| `Test-SolutionArchitecture.ps1` | Pass; 7 checks passed, 0 failed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| `Test-ArchitectureDecisionRecords.ps1` | Pass; 8 checks passed, 0 failed. |
| `Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| `Invoke-CoreCi.ps1` | Pass after correcting three `IDE0046` formatting diagnostics; all 9 fail-closed stages passed in the final run. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| All four core test projects | Pass; 361 discovered, 361 passed, 0 failed, 0 skipped. |
| .NET/Ruff formatting and lint | Pass; `dotnet format --verify-no-changes --severity info`, Ruff lint, and Ruff format verification. |
| `git diff --check` | Pass directly and through repository baseline/core CI. |

## Completion

PB-0104 passed its local, Git, GitHub, required `main` CI, and explicit user-confirmation gates without exception. The PB-0105 rollover marks it `[x]` / 🟢 **DONE**, removes it from Active Work, and records it exactly once in the Completion Log.
