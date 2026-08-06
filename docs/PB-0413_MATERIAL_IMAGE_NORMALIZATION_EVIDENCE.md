# PB-0413 Material and Image Normalization Evidence

## Status

- Canonical branch: `feat/PB-0413-material-normalization`
- Publication branch: `feat/PB-0412-scene-cleanup`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

The shared publication branch is the exact user-approved PB-0412/PB-0413/PB-0414/PB-0415
exception. It changes only publication topology and creates no precedent.

## Implemented Boundary

`package_builder_blender.material_normalization.normalize_material_images(...)` accepts the
PB-0406 inspection report and a complete manifest assignment for every image. Each image retains
one separate canonical role: Albedo, Normal, Metallic, Roughness, Emission, Ambient Occlusion,
Opacity, or Height. Combined ORM/metallic-roughness names, unsafe paths, missing images, duplicate
outputs, and noncanonical filenames fail before mutation.

An unambiguous inspected role must agree with the manifest by default. Unknown, ambiguous, or
contradictory evidence returns `BLENDER_TEXTURE_ROLE_AMBIGUOUS`; only an explicit reviewed manifest
override can continue. Successful normalization writes `//Textures/<canonical-name>` references,
uses sRGB for Albedo/Emission and Non-Color for data maps, verifies dimensions/format are unchanged,
and rolls back all references/color spaces if Blender rejects a value. It does not save, unpack,
pack, convert, combine, or alter pixels.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Normalize texture references | exact Blender-relative path assertions |
| Retain separate canonical maps | three-role independent output test and combined-map rejection |
| Apply role-appropriate color space | sRGB and Non-Color assertions |
| Block ambiguity rather than guess | unknown/ambiguous/contradictory tests |
| Permit only reviewed resolution | explicit manifest override test |
| Avoid partial mutation | forced setter rejection and complete rollback test |

Focused PB-0413 tests: 5 passed, 0 failed.

## Combined Validation and Limit

- New focused tests: 20 passed; all Blender worker tests: 137 passed.
- Built-in focused executable-line trace: all four new production modules 100%; Python branch
  coverage is not currently measured or claimed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; Core CI: all nine stages passed in 3 minutes 2.498 seconds.
- Ruff lint/format, .NET format, locked restore, task graph, local links, security scans,
  vulnerability audit, and `git diff --check` passed.

Tests use direct image doubles; real Blender image loading/saving is intentionally outside this
evidence and no visual result is claimed.

## Publication Evidence

- Shared task commit: `ba9e617b76b076e58aaa4e2279432b784ec373d7`.
- Pull request: [#61](https://github.com/avivperets26/3DModels-Package-Builder/pull/61).
- Merge commit: `e75f9d41f6091d47b915e1da3be3564f2895839c`.
- Required [main workflow run 31095384477](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31095384477) succeeded.
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-06. No CI or quality exception was used.
