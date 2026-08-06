# PB-0502 Portable Folder Composer Evidence

## Status

- Canonical branch: `feat/PB-0502-portable-folder-composer`
- Publication branch: `feat/PB-0501-portable-naming`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-06

The shared branch is the exact user-approved PB-0501/PB-0502 publication exception. Task identity,
tests, acceptance evidence, and lifecycle remain independent and the exception creates no
precedent.

## Implemented Boundary

`PortableCompositionArtifact` qualifies one PB-0205 `ArtifactStoreRecord` for exactly one portable
layout purpose. The record must be in `validated` state, target `portable`, use the exact expected
role, and carry only the purpose-specific texture, GLB, media, or extension qualifiers. Media is
currently the approved JPEG layout; canonical texture output extensions remain explicit.

`PortableFolderComposer.Compose(...)` is side-effect free. It requires exactly one normalized FBX,
portable FBX archive, flat README, and product README. A normalized GLB is optional and unique.
Textures and media are optional and unique by canonical role/view. Artifact IDs are unique under
case-insensitive portable-filesystem rules, and generated entries are sorted ordinally. The result
contains two immutable plans:

- `<FolderName>_fbx/` with a flat FBX, separate canonical textures, and flat README;
- `<FolderName>/` with the FBX ZIP, optional GLB, product README, and `Media/` images.

The composer includes only explicitly supplied qualified records. It does not enumerate a source
folder and therefore cannot accidentally include backups, caches, helpers, or unrelated files. It
does not open, copy, rename, or modify source bytes.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Exact flat FBX folder | complete 8-entry example assertion and minimal-layout assertion |
| Exact top-level product/media layout | complete 8-entry example assertion and optional-GLB assertion |
| No duplicates | artifact-ID, case-only ID, singleton-purpose, texture-role, and media-view tests |
| No unrelated files | exact target/lifecycle/role qualification and exact output-count assertions |
| Deterministic immutable output | shuffled-input equality and read-only collection tests |

## Evidence Boundary

This is a logical plan, not a byte-copy implementation. PB-0503 supplies the validated texture
artifacts, PB-0504 generates README artifacts, PB-0505 builds the deterministic archive, and the
later orchestration slice executes the plan. There is no WPF surface or physical folder output to
review visually in PB-0502.

## Local Validation

- Focused portable-target tests: 47 passed, 0 failed, 0 skipped, including complete/minimal
  layouts, shuffled-input determinism, duplicate rejection, lifecycle/role qualification, and
  immutable result checks.
- New `PackageBuilder.Targets.Portable` production code: 100% line and branch coverage after
  repository formatting.
- Complete solution tests: 2,114 passed, 0 failed, 0 skipped across six test projects.
- Debug and Release solution builds: 17 projects, 0 warnings, 0 errors.
- Repository baseline: 29 passed, 0 failed.
- Full Core CI: all nine stages passed in 4 minutes 39 seconds.
- Locked restore, .NET formatting, Ruff lint/formatting, task lifecycle, dependency graph, Markdown
  links, secret/prohibited-content checks, and `git diff --check`: passed.

## Remaining Gates

- User-controlled commit, push, merge into and push of `main`.
- Successful required `main` CI and explicit user confirmation.
- Next-task rollover synchronization after all completion gates pass.
