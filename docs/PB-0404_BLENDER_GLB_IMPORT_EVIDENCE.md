# PB-0404 Blender GLB Import Adapter Evidence

## Status

- Task: PB-0404 — Implement GLB/glTF import adapter
- Branch: `feat/PB-0404-blender-glb-import`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-05

PB-0404 is implemented, locally validated, published, and synchronized as complete during the
approved PB-0405 rollover.

## Final Publication Evidence

- Final task commit: `851812e2a80c2a17197767f000800a87430e975b`.
- Branch: `feat/PB-0404-blender-glb-import`.
- Pull request: [#57](https://github.com/avivperets26/3DModels-Package-Builder/pull/57).
- Merge commit: `e9231a22125108c5b8670a36ef7bb6d42a67b9a4`.
- Required successful `main` CI:
  [workflow run 31040818232](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31040818232).
- Explicit user completion confirmation: 2026-08-05.
- Exception used: none.

## Implemented Boundary

- `import_glb` accepts one canonical, non-link, non-empty `.glb` regular file beneath the canonical
  job input root. Separate `.gltf` files are rejected because their external buffer/image dependency
  graph requires a distinct contained-reference preflight that this task does not own.
- PB-0402 resets the scene before import and after a failed or rejected partial import.
- The adapter calls Blender 5.0's standard `bpy.ops.import_scene.gltf` operator with every relevant
  choice explicit rather than depending on UI state or operator defaults.
- Imported images are packed; vertices and material slots are not merged; source normals, the
  round-trip bone heuristic, guessed original bind pose, and standard physical lighting are used.
- Bone-shape helpers, imported selection, scene collection wrapping, scene extras/custom
  properties, and WebP preference are disabled. Unused materials/images are retained for later
  source inspection.
- Successful reports contain the safe source filename, exact settings, and counts for created
  objects, meshes, materials, images, packed images, armatures, skinned meshes, and animations.
- A post-reset identity baseline excludes retained linked data that existed before import.
- Expected failures map to stable sanitized findings without raw exception text or physical paths.

The operator surface was verified against Blender's official 5.0 release branch of the
[glTF Blender I/O source](https://github.com/KhronosGroup/glTF-Blender-IO/blob/blender-v5.0-release/addons/io_scene_gltf2/__init__.py)
and the [Blender 5.0 glTF manual](https://docs.blender.org/manual/en/5.0/addons/import_export/scene_gltf2.html).
The manual documents import support for materials, images/textures, skins, and animations; Package
Builder does not claim support beyond what the retained evidence demonstrates.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_GLB_SOURCE_INVALID` | Source/root containment, type, link, extension, existence, or size failed. |
| `BLENDER_GLB_IMPORT_SETTINGS_INVALID` | One or more explicit operator settings are invalid. |
| `BLENDER_SCENE_RESET_FAILED` | A clean pre-import scene or identity baseline could not be established. |
| `BLENDER_GLB_IMPORT_FAILED` | Blender raised while importing. |
| `BLENDER_GLB_IMPORT_REJECTED` | Blender did not return exactly `FINISHED`. |
| `BLENDER_GLB_IMPORT_EMPTY` | Blender completed without creating a scene object. |
| `BLENDER_GLB_IMPORT_RESULT_INVALID` | Imported Blender resources could not be read safely. |
| `BLENDER_GLB_IMPORT_CLEANUP_FAILED` | Partial imported state could not be removed. |

Every finding is a blocking error from `blender-glb-importer` with sanitized actionable guidance.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| GLB materials import | `test_material_image_skin_and_animation_fixture_records_exact_settings` |
| Embedded images import and remain packed | the same fixture test covers regular and tiled packed images |
| Skin imports | modifier-based fixture plus `test_parented_mesh_is_reported_as_skinned` |
| Animations import | the combined fixture records three created actions |
| Retained linked resources are excluded | `test_preexisting_linked_data_is_not_counted_in_import_report` |
| Unsafe GLB and separate glTF sources fail before side effects | invalid-source and link tests |
| Invalid settings fail before side effects | `test_invalid_settings_fail_before_side_effects` |
| Blender failures are sanitized and partial state is removed | exception, rejection, cleanup, and unreadable-result tests |
| Empty imports and reset failures block release | empty-import and scene-reset tests |

## Current Validation

- Focused PB-0404 plus prior Blender worker tests: 40 passed.
- Focused `glb_import.py` trace coverage: 257/257 executable lines, 100% line execution.
- Debug solution build: 16 projects, 0 warnings, 0 errors.
- Release solution build: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 2 minutes 58.137 seconds.
- Repository-local Ruff lint and formatting: passed.
- Locked restore, .NET formatting, PowerShell parsing, task/dependency/lifecycle checks,
  documentation links, secret/prohibited-content checks, and `git diff --check`: passed.

## Evidence Limits

- No contained Blender executable is present, so real `bpy` execution and binary GLB parsing have
  not occurred and are not claimed.
- The focused tests use strict deterministic importer/data doubles. They verify the exact operator
  boundary, containment, material/image/skin/animation accounting, cleanup, and stable findings,
  but do not replace the later contained-engine fixture suite.
- Python branch coverage is not currently measured, so no branch-coverage claim is made.
- Separate `.gltf` source graphs are not accepted by this single-file GLB boundary. Supporting them
  safely requires bounded JSON parsing and complete canonical dependency-reference validation.

## Completion

No PB-0404 acceptance or publication gate remains. PB-0404 is `[x]` / 🟢 **DONE**, absent from
Active Work, and recorded exactly once in the Completion Log. The disclosed real-Blender and
Python branch-coverage evidence limits remain; completion does not convert them into unsupported
claims.
