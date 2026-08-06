# PB-0416 Normalized GLB Export Evidence

## Status

- Canonical and publication branch: `feat/PB-0416-normalized-glb-export`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

PB-0416 shares one explicitly user-approved publication cycle with PB-0417 and PB-0418. Each task
retains its own acceptance boundary, module, tests, evidence, lifecycle, canonical branch, and
eventual Completion Log row. This exact exception creates no precedent.

## Implemented Boundary

`package_builder_blender.glb_export.export_normalized_glb(...)` accepts a complete normalized plan,
PB-0406 texture inspection report, scene objects, images, Actions, and the PB-0412 selection guard.
Before Blender runs it requires a new canonical direct-child `.glb` path, exact unique selected
mesh/armature inventory, exact attached materials, exact inspected and connected images, exact
Actions, and a product-case-consistent rig/animation shape.

The Blender 5 policy exports glTF 2.0 Binary with embedded images, selected meshes/armatures,
materials, UVs, normals, tangents, skins, morphs, deform bones, and sampled Actions when animated.
It excludes cameras, lights, extras, unused images/textures, NLA strips, leaf bones, Draco,
gltfpack, WebP variants, external collections, settings persistence, and overwrite. PB-0414 owns
destructive baking, so export-side animation baking/optimization is disabled. Successful output
must have a structurally consistent GLB header and JSON chunk; rejection or exceptions remove the
exact partial file and restore selection/visibility state.

Blender 5.0.0's background glTF operator emits a callback traceback when
`export_action_filter=False` is assigned explicitly because that callback dereferences a UI-only
Scene collection. The operator's reviewed official default is already false, so the production
policy deliberately omits that one optional argument. A focused assertion prevents accidental
reintroduction; Action filtering remains disabled without changing export behavior.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Static GLB with intended texture | static plan, connected-image, operator-policy, and GLB-header assertions |
| Rigged GLB with skin | exact mesh/armature inventory and skin/deform-bone option assertions |
| Animated GLB with intended Action | non-empty exact Action inventory and animation-policy assertions |
| Exact selected content and state restoration | in-export selection plus post-success restoration assertions |
| Fail closed and avoid overwrite/partial output | unsafe/existing path, cancelled, corrupt, and cleanup assertions |

## Contained Blender 5.0.0 Evidence

The user explicitly authorized the official portable Blender runtime on 2026-08-06. The ignored
local runtime is `tools/blender/5.0.0/blender.exe`; downloads and mutable state remain beneath
`downloads/blender` and `runtime-data/blender/5.0.0`.

- Official archive:
  `https://download.blender.org/release/Blender5.0/blender-5.0.0-windows-x64.zip`
- Official checksum list:
  `https://download.blender.org/release/Blender5.0/blender-5.0.0.sha256`
- Verified SHA-256:
  `14D491B6E491C35950B89BAC8CAA4AB7115596CC056775F427806D3EA2EAC698`
- Authenticode: valid; signer `Blender Foundation`; certificate thumbprint
  `3000ED18BD640AB50063D2AE9B1C59518EC18985`.
- Runtime probe: Blender `5.0.0`, release build hash `a37564c4df7a`, Windows x64.
- Archive safety: 6,831 entries, 965,509,154 expanded bytes, one expected archive root, and zero
  absolute, drive-qualified, or traversal entries.

`scripts/Test-BlenderPb0416ToPb0418.ps1` generated three retained synthetic `.blend` scenes and
exported their GLBs through the production function. The GLB JSON chunks were checked directly:
each contains the exact selected object nodes, one exact material, one exact image embedded through
a buffer view with no external URI, and one texture; rigged products contain one skin and only the
animated product contains the exact Action.

Real PB-0416 result: 3/3 static, rigged, and animated GLB exports passed. The latest retained run is
`artifacts/PB-0416-PB-0418-real-blender/20260806-131612-bc6a7d1255d4426fb495d1d4acf6aefe`.

Focused PB-0416 tests: 7 passed, 0 failed. The complete Blender suite reports 156 passed.

## Combined Local Validation

- PB-0416/PB-0417/PB-0418 focused tests: 19 passed, 0 failed.
- Built-in focused executable-line trace: all three new production modules 100%; Python branch
  coverage is not currently measured or claimed.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 3 minutes 29.519 seconds after real-engine integration.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors; baseline .NET tests: 2,067 passed.
- Vulnerability audit: no known vulnerable direct or transitive NuGet packages.
- Ruff 0.15.22 lint/format, .NET format, locked restore, task graph, links, repository security
  checks, and `git diff --check` passed.

## Evidence Boundary

The reviewed options follow the official
[Blender 5 glTF export API](https://docs.blender.org/api/5.0/bpy.ops.export_scene.html#bpy.ops.export_scene.gltf)
and [glTF 2.0 export manual](https://docs.blender.org/manual/en/5.0/addons/import_export/scene_gltf2.html).
Strict exporter doubles retain deterministic failure coverage, while the contained Blender run now
proves real geometry, embedded image, material, skin, Action, and evaluated-motion round trips.
Visual review is available from the retained `.blend` and `.glb` files but remains supplementary to
the automated evidence.

## Publication Evidence

- Task commit: `e43ded6d7a36764df42cca89f0380a4d40aeb251`.
- Pull request: [#62](https://github.com/avivperets26/3DModels-Package-Builder/pull/62).
- Merge commit: `45a6af25813c2494771ca6237f2be7d1eb83695d`.
- Required successful [main workflow run 31104970924](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31104970924).
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-06. No CI or quality exception was used.
