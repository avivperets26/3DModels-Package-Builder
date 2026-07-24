# PB-0103 Source Assets and Textures Evidence

**Task:** PB-0103 — Implement source-asset and texture-assignment models
**Branch:** `feat/PB-0103-source-assets-textures`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-24

## Scope

PB-0103 adds immutable source metadata, the complete approved canonical source texture-role inventory, renderer-independent colour spaces and normal conventions, and validated image-to-role assignments. The implementation remains in `PackageBuilder.Domain`, preserves PB-0101 and PB-0102 APIs, and adds no filesystem, archive, image-decoding, engine, adapter, marketplace, WPF, persistence, serialization, or networking behavior.

This task also performs the approved PB-0102 rollover. PB-0102 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion Log. PB-0103 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and is absent from the Completion Log.

## PB-0102 Final Publication Evidence

- Final task commit: `16d89bddcac6d26680a20bd7a30956fde1d09dd2`.
- Pull request: [#16](https://github.com/avivperets26/3DModels-Package-Builder/pull/16).
- Successful PR workflow: [run 30092231887](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30092231887).
- Merge commit: `0ac28fbc61b7f5287c4161b1329b50df19dd7e22`.
- Successful required main workflow: [run 30092238172](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30092238172).
- The user explicitly confirmed completion on 2026-07-24.
- No CI, completion, or quality exception was used.

The permanent PB-0012/PB-0013 validators retain only their fixed foundation assertions. Generic lifecycle validation in `Test-RepositoryBaseline.ps1` owns the moving active task.

## Public API

| API | Purpose |
|---|---|
| `SourceAssetKind.Fbx`, `.Glb`, `.Archive`, `.Image` | Closed source-kind identities with stable canonical identifiers. |
| `SourceAssetKind.All` / `.TryParse(string?)` | Stable read-only registry and PB-0102-compatible expected-failure parsing. |
| `SourceAsset.Create(SourceAssetKind?, string?, string?)` | Creates immutable source metadata without inspecting a physical file. |
| `SourceAssetValidationResult` | Returns a source asset or a precise task-local expected-input error. |
| `TextureRole` | Closed inventory of the eight approved canonical source roles. |
| `ColourSpace.Srgb`, `.Linear` | Renderer-independent colour-space identities. |
| `NormalConvention.Auto`, `.OpenGl`, `.DirectX` | Renderer-independent normal orientation. |
| `TextureAssignment.Create(...)` | Associates one Image source with a compatible role, colour space, and applicable normal convention. |
| `TextureAssignmentValidationResult` | Returns an immutable assignment or a precise task-local expected-input error. |

All constructors that could retain invalid state are private. Canonical identities are immutable singletons; registries are read-only and stably ordered. Expected user-input failures do not require exceptions.

## Source-Asset Contract

| Kind | Canonical identifier | Explicit extension rule |
|---|---|---|
| FBX | `fbx` | Final logical-reference segment and supplied original filename must end in `.fbx`. |
| GLB | `glb` | Final logical-reference segment and supplied original filename must end in `.glb`. |
| Archive | `archive` | Final logical-reference segment and supplied original filename must end in `.zip`. |
| Image | `image` | No extension allow-list is imposed by PB-0103. |

Extension checks use ordinal case-insensitive comparison because the file formats are explicit, while the accepted reference is preserved exactly. Image validation deliberately does not invent a broad format list before evidence approves one.

Logical source references:

- Are relative domain references, not concrete filesystem paths.
- Use `/` as the only canonical separator.
- Preserve accepted Unicode, spaces inside segments, casing, and punctuation exactly.
- Compare ordinally and case-sensitively; `Textures/A.png` and `textures/A.png` are distinct domain references.
- Reject null, empty, whitespace-only, rooted, drive-relative, URI-like/colon, backslash, empty-segment, `.`/`..`, segment-edge whitespace, and control-character forms.
- Are never trimmed, Unicode-normalized, case-folded, separator-replaced, traversal-resolved, or silently repaired.

An optional original filename must be exactly one valid logical-reference segment and must obey the same explicit kind/extension rule. PB-0103 does not add `SourceAssetSet`; duplicate-set policy and physical Windows collision checks are therefore not represented prematurely.

## Texture Roles and Colour Spaces

| Role | Canonical identifier | Required colour space | Normal-map data |
|---|---|---|---|
| Albedo | `albedo` | sRGB | No |
| Normal | `normal` | Linear | Yes |
| Metallic | `metallic` | Linear | No |
| Roughness | `roughness` | Linear | No |
| Emission | `emission` | sRGB | No |
| Ambient Occlusion | `ambient-occlusion` | Linear | No |
| Opacity | `opacity` | Linear | No |
| Height | `height` | Linear | No |

`TextureRole.Albedo.DisplayName` is exactly `Albedo`. `Albeado` is rejected, and Base Color or Diffuse remain future classifier inputs rather than parallel canonical roles. Unity metallic-smoothness and Unreal ORM are target outputs owned by later adapters and are not roles.

## Normal-Convention Rules

Normal conventions have canonical identifiers `auto`, `open-gl`, and `direct-x`, with display names Auto, OpenGL, and DirectX.

- A Normal assignment must declare exactly one supported convention.
- Auto records intentional ambiguity for later inspection or user review; it does not claim that orientation was detected.
- A non-Normal assignment must have no normal convention.
- Missing or contradictory convention data returns a validation failure and is never dropped or corrected silently.

## Texture-Assignment Validation

Validation is deterministic and ordered:

1. A source asset is required.
2. The source kind must be Image.
3. A known closed canonical role is required.
4. A known colour space is required.
5. The colour space must match the role table.
6. Normal requires a convention; every other role forbids one.

Unknown or ambiguous classifier input cannot create an assignment because callers can supply only a canonical `TextureRole`. An Image may remain unassigned outside this model until review.

Equality for source assets and assignments is type-safe, ordinal, culture-invariant, and includes every retained field. Stable hash implementations avoid current-culture and per-process randomized string hashing.

## Deferred Work

- PB-0201/PB-0202 and related infrastructure own root containment, canonical physical paths, reparse/symlink checks, file existence, hashing, archive limits, archive inspection, and extraction.
- PB-0104 owns materials and shader-independent material properties.
- PB-0406 owns image inspection and texture classification heuristics.
- PB-0413 and target tasks own source normalization, copying, conversion, and engine-specific import behavior.
- PB-0607 and PB-1107 own Unity metallic-smoothness and Unreal ORM packing.
- PB-0109 owns global validation findings and stable finding codes.
- PB-0110 owns manifest schemas, JSON serialization, and converters.
- Image decoding, marketplace rules, target import settings, and source mutation are not implemented.

## Test Inventory

The xUnit v3 PB-0103 public-behavior suite covers:

- All four source kinds, canonical identifiers, parsing, equality, culture invariance, stable order, and immutable registries.
- Valid single- and multi-segment references, Unicode, internal spaces, case preservation, and optional original filenames.
- Null, empty, whitespace, rooted, UNC-like, drive-rooted, drive-relative, URI-like/colon, backslash, empty-segment, traversal, segment-whitespace, control-character, malformed, and extension-mismatch inputs.
- Explicit FBX, GLB, and ZIP extension rules plus the absence of an invented Image extension allow-list.
- All eight roles, exact canonical Albedo behavior, rejection of `Albeado`, classifier aliases, and target packing roles.
- Both colour spaces and all three normal conventions, their identifiers, parsing, equality, culture invariance, stable order, and immutable registries.
- Every valid and invalid role/colour-space pair.
- All valid Normal/convention pairs, missing convention, and convention rejection on non-Normal roles.
- Null dependencies, non-Image sources, deterministic equality/hashing, immutability, and engine/adapter dependency boundaries.
- Existing PB-0101 and PB-0102 compatibility through the complete Domain and core suites.

## Coverage Evidence

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` with `ExcludeAssembliesWithoutSources=None` for deterministic PathMap assemblies. The final-source Cobertura report remains beneath ignored `artifacts/PB-0103/coverage-final-source`.

| Production file | Line coverage | Branch coverage |
|---|---:|---:|
| `SourceAsset.cs` | 100% | 100% |
| `SourceAssetKind.cs` | 100% | 100% |
| `SourceAssetValidationResult.cs` | 100% | 100% |
| `SourceReferenceValidator.cs` | 100% | 100% |
| `ColourSpace.cs` | 100% | 100% |
| `NormalConvention.cs` | 100% | 100% |
| `TextureAssignment.cs` | 100% | 100% |
| `TextureAssignmentValidationResult.cs` | 100% | 100% |
| `TextureRole.cs` | 100% | 100% |

## Architecture Evidence

- `PackageBuilder.Domain.csproj` retains zero `ProjectReference` and `PackageReference` items.
- PB-0103 production code uses only base-class-library and Domain namespaces.
- No Blender, WPF, Unity, Unreal, target-adapter, marketplace, persistence, networking, concrete filesystem, archive, or image library is referenced.
- No PB-0104 material, PB-0109 global finding, PB-0110 JSON, classifier, packing, or engine-import implementation is introduced.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0103 tests | Pass; 99 passed, 0 failed, 0 skipped. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 260 passed, 0 failed, 0 skipped, preserving the previous 161 tests. |
| Per-file coverage | Pass; all nine new production files report 100% line and 100% branch coverage. |
| `Test-SolutionArchitecture.ps1` | Pass; 7 checks passed, 0 failed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| `Test-ArchitectureDecisionRecords.ps1` | Pass; 8 checks passed, 0 failed. |
| `Test-RepositoryBaseline.ps1 -RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| `Invoke-CoreCi.ps1` | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| All four core test projects | Pass; 263 discovered, 263 passed, 0 failed, 0 skipped. |
| .NET/Ruff formatting and lint | Pass; `dotnet format --verify-no-changes --severity info`, Ruff lint, and Ruff format verification. |
| `git diff --check` | Pass directly and through repository baseline/core CI. |

## Remaining Gates

PB-0103 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and remains absent from the Completion Log. Local implementation and the requested validation matrix pass. User-controlled staging, task commit, task-branch push, merge into and push of `main`, successful required `main` CI, explicit completion confirmation, and next-task rollover synchronization remain.
