# PB-0406 Texture Extraction and Role Inspection Evidence

## Status

- Task: PB-0406 — Implement texture extraction and role inspection
- Canonical branch: `feat/PB-0406-texture-inspection`
- Publication branch: `feat/PB-0406-texture-inspection`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

PB-0406 shares one explicitly approved publication cycle with PB-0407 and PB-0408. The exception
is recorded in the backlog, is limited to those three tasks, and creates no precedent.

## Implemented Boundary

`package_builder_blender.texture_inspection.inspect_textures(...)` copies Blender image and
material-node facts into immutable deterministic reports without saving, packing, unpacking,
extracting, rewriting, or relinking source data.

The report includes image dimensions, file format, exact colour-space name, conservative sRGB or
linear classification, packed byte count, packed/external/generated source kind, safe source
filename, material connection count, material and node identity, reachable shader destinations,
and a probable role with its evidence basis.

Probable roles are limited to albedo, normal, metallic, roughness, emission, ambient occlusion,
opacity, and height. A unique shader destination wins over a name hint. Conflicting hints report
`ambiguous`; missing evidence reports `unknown`. The inspector never silently converts either
state into a canonical role.

Only active node-based materials are inspected. Traversal is bounded to 512 links per image node,
and reports retain filenames rather than physical source paths.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_TEXTURE_INPUT_INVALID` | Image or material collections cannot be enumerated safely. |
| `BLENDER_TEXTURE_DATA_INVALID` | Image, packed-file, node, link, colour-space, or identity data is incomplete or inconsistent. |

Expected failures are PB-0109-compatible blocking findings from `blender-texture-inspector` and
never contain Blender exception text, source contents, or physical paths.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Packed and external images reported | packed/external inventory and packed-byte tests |
| Size and format reported | deterministic image metadata assertions |
| Colour space reported | exact name and conservative classification assertions |
| Material connection reported | direct and transitive shader-graph tests |
| Probable roles reported safely | connection, name-hint, ambiguous, and unknown behavior tests |
| Sources are not modified | API boundary exposes only reads; tests use immutable result assertions and no write methods |
| Invalid data fails safely | duplicate, dimensions, packed-size, missing-image, and unreadable-input tests |

## Validation

- Focused PB-0406 tests: 16 passed, 0 failed.
- Combined PB-0406/PB-0407/PB-0408 tests: 38 passed, 0 failed.
- All Blender worker tests: 88 passed, 0 failed.
- New production modules: 100% executable-line coverage under Python `trace`; no branch-coverage
  percentage is claimed.
- Debug solution build: 16 projects, 0 warnings, 0 errors.
- Release solution build: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 2 minutes 51.175 seconds.
- Repository-local Ruff 0.15.22 lint and formatting passed across all Blender Python files.
- NuGet vulnerability audit reported no known vulnerable direct or transitive package for any of
  the 16 projects.
- Locked restore, .NET formatting, PowerShell parsing, documentation links, lifecycle/task graph,
  secret/prohibited-content checks, and `git diff --check` passed.

## Evidence Limits

- “Texture extraction” in PB-0406 means extraction of texture metadata and packed-image facts for
  planning; physical file extraction remains a later normalization/output responsibility.
- No approved contained Blender executable is present. Strict plain-Python doubles cover the
  direct-data boundary, not real engine integration.
- Image colour management can contain custom names. Unknown names remain `other` rather than being
  guessed.
- This task changes no WPF view or rendered output, so manual visual testing is not applicable.

## Remaining Gates

User-controlled commit/push/merge, successful required `main` CI, explicit user confirmation, and
PB-0409 rollover remain. PB-0406 stays `[ ]` / 🟡 **PROCESS** and absent from the Completion Log.
