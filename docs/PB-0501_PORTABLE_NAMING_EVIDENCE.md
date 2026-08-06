# PB-0501 Portable Naming Evidence

## Status

- Canonical and publication branch: `feat/PB-0501-portable-naming`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

PB-0501 shares one explicitly user-approved publication cycle with PB-0502. Each task retains its
own acceptance boundary, production API, focused evidence, canonical branch, lifecycle, and
eventual Completion Log row. This exact exception creates no precedent.

## Implemented Boundary

`PortableNamingProfile` consumes the already validated PB-0101 `InternalAssetId` and
`ProductFolderName`. It never guesses identity from source filenames and performs no filesystem
access. The immutable profile composes:

- `<FolderName>_fbx` and `<AssetId>.fbx`;
- `<FolderName>_FBX.zip`;
- `<FolderName>.glb` for a standard/static GLB and `<FolderName>_rigged.glb` for a rigged or
  animated GLB;
- `README_<AssetId>.txt` inside the flat FBX folder and `README_<FolderName>.txt` at product level;
- `T_<AssetId>_Albedo|Normal|Metallic|Roughness|Emission|AO<extension>`;
- `<FolderName>_Cover|Front|Back|Left|Right<extension>`.

`PortableFileExtension` requires an explicit leading dot plus 1–10 lowercase ASCII letters or
digits. This preserves byte-format intent for PB-0503 without silently normalizing an extension.
The six portable separate-map roles are deliberately narrower than the complete Domain role set;
Opacity and Height remain unsupported until a later task explicitly defines their portable-output
policy. Generated examples and collision checks use ordinal-ignore-case filesystem semantics.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| FBX, standard/rigged GLB, and both README names match examples | exact-name unit assertions |
| Albedo, Normal, Metallic, Roughness, Emission, and AO match examples | six role-token theory cases |
| Media names and extensions match the product layout | five-view exact-name assertions |
| Collision rules are deterministic and portable | case-insensitive uniqueness and composer duplicate tests |
| Missing/unsupported qualifiers fail without throwing | null, malformed extension, unsupported role, and qualifier tests |

## Evidence Boundary

This task produces names only. PB-0502 assigns those names to a logical layout, PB-0503 owns
texture bytes and format validation, PB-0504 owns README contents, PB-0505 owns ZIP creation, and
PB-0506 owns final portable validation. There is no UI or rendered output, so manual visual testing
is not applicable.

## Local Validation

- Focused portable-target tests: 47 passed, 0 failed, 0 skipped.
- New `PackageBuilder.Targets.Portable` production code: 100% line and branch coverage after
  repository formatting.
- Complete solution tests: 2,114 passed, 0 failed, 0 skipped across six test projects.
- Debug and Release solution builds: 17 projects, 0 warnings, 0 errors.
- Repository baseline: 29 passed, 0 failed.
- Full Core CI: all nine stages passed in 4 minutes 39 seconds.
- Locked restore, .NET formatting, Ruff lint/formatting, task lifecycle, dependency graph, Markdown
  links, secret/prohibited-content checks, and `git diff --check`: passed.

## Publication Evidence

- Shared task commit: `b5f0f6aa527096e6337bb9efa856219231f49071`.
- Pull request: [#63](https://github.com/avivperets26/3DModels-Package-Builder/pull/63).
- Merge commit: `d9a7ab1c460f74fd11d7804e124ff788d84d7314`.
- Required successful `main` workflow:
  [31109207525](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31109207525).
- The user explicitly confirmed the merge and green required CI on 2026-08-06. No CI or quality
  exception was used; the shared publication exception creates no precedent.
