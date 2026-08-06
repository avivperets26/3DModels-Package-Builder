# Package Builder Blender Worker

This directory contains the protocol shell loaded by the selected contained Blender executable.
PB-0401 implements request validation, JSON Lines progress, atomic result output, and stable exit
codes. PB-0402 adds direct-data scene reset and temporary data-block ownership utilities. PB-0403
and PB-0404 add bounded FBX and single-file GLB import adapters. PB-0405 adds deterministic
geometry and transform inspection. PB-0406 through PB-0408 add read-only texture/material,
armature/weight, and Action/animation inspection. PB-0409 through PB-0416 add inference,
normalization, cleanup, and FBX/GLB export boundaries; PB-0417/PB-0418 add clean-reimport
comparison and stable regression findings. Full job-operation orchestration and contained Blender
execution remain later integration work.

## Context-safe scene utilities

`package_builder_blender.scene_utils.reset_scene(bpy.data)` removes all scene objects with
`BlendData.batch_remove` and recursively purges local orphaned data. It deliberately receives only
`bpy.data`: selection, active object, editor area, and current interaction mode cannot influence the
result. Linked orphaned data is retained.

`TemporaryDataBlocks(bpy.data)` owns operation-scoped data-blocks. It removes unique registered
blocks as one reverse-registration-order batch on explicit `close()` or context-manager exit,
including when processing raises. Cleanup failures remain retryable and are never reported as
success. Callers must not retain or access Blender references after these utilities remove them.

## Import adapters

`package_builder_blender.fbx_import.import_fbx(...)` accepts one canonical contained `.fbx` and
records explicit axis/scale options plus object, mesh, and armature counts.

`package_builder_blender.glb_import.import_glb(...)` accepts one canonical contained `.glb`, packs
images, preserves material/skin/animation data, disables UI and untrusted-extra behavior, and
reports only resources created by the import. Separate `.gltf` files are rejected because their
external resource graph requires a future contained-reference preflight.

Both boundaries reset direct Blender data before import, clean partial state after expected
failure, and return stable sanitized findings. They have deterministic plain-Python tests, but real
binary parsing is not claimed until an approved contained Blender executable runs the fixture suite.

## Geometry inspection

`package_builder_blender.geometry_inspection.inspect_geometry(...)` reads imported Blender objects
without selection or editor context. It reports immutable object transforms, mesh topology,
world-space bounds/dimensions, UV layers, corner normals, calculated tangents, material slots,
aggregate counts, and required 16/32-bit indices. Calculated tangents are freed after inspection.
Malformed, duplicate, missing, inconsistent, or non-finite data produces a stable sanitized
blocking finding rather than a raw Blender exception.

## Texture, rig, and animation inspection

`package_builder_blender.texture_inspection.inspect_textures(...)` reports immutable image
dimensions, formats, colour spaces, packed/external source facts, safe filenames, active material
connections, and conservative probable roles. It does not save, pack, unpack, extract, relink, or
otherwise modify an image.

`package_builder_blender.rig_inspection.inspect_rigs(...)` reports immutable armature hierarchy,
roots, bone rest data, deform flags, skinned meshes, vertex groups, missing and unmatched groups,
unweighted vertices, influence totals, and mesh parent-inverse context. It reads raw imported data
without posing, evaluating, or repairing a rig.

`package_builder_blender.animation_inspection.inspect_animations(...)` supports Blender 5 layered
Actions and legacy F-curves. It reports clip ranges, FPS, slot/layer/strip channel provenance,
keyframes/samples, motion, transform motion, and conservative loop-likelihood evidence without
evaluating playback or modifying animation data.

All three inspectors return stable sanitized blocking findings for expected invalid Blender data.
Their deterministic plain-Python tests do not claim actual `bpy` or contained-engine execution.

## Normalization, Export, and Reimport Validation

`case_inference`, `naming_normalization`, `transform_normalization`, `export_sets`,
`material_normalization`, and `rig_animation_normalization` implement manifest-owned normalization
without silently guessing product grouping or ambiguous texture roles. `fbx_export` and
`glb_export` validate exact static, rigged, or animated inventories, use selection-safe reviewed
Blender 5 operator policies, and remove incomplete output. GLB export additionally requires every
declared image to be inspected and connected and verifies the glTF 2.0 binary container header.

`clean_reimport` requires one independently empty-process observation per contained FBX/GLB and
compares exact object/mesh/material/skeleton/animation counts, bounds, and representative evaluated
deformation samples. `regression_validation` maps the versioned corrupt, missing-image,
multiple-rig, no-UV, unsupported-data, and invalid-animation portfolio to stable sanitized findings.
Strict doubles prove deterministic success, failure, and tolerance boundaries. The opt-in
contained-engine harness additionally exercises
static/rigged/animated GLB export, three distinct empty-process reimports, and all seven regression
cases against the approved Blender 5.0.0 runtime.

## Invocation

The .NET safe-process boundary supplies absolute contained paths through separate arguments:

```text
blender.exe --background --factory-startup --python workers/blender/entrypoint.py -- --request <absolute-request-file>
```

The request file's parent is the job workspace. Protocol logical references resolve beneath that
workspace and traversal, absolute references, backslashes, linked escapes, duplicate JSON
properties, unknown properties, oversized input, malformed UTF-8, and unsupported protocol
versions fail closed. The PB-0401 probe operation is `probe-blender-worker`; asset-processing
operations are deliberately unsupported until their owning tasks implement them.

## Exit Codes

| Code | Meaning | Result behavior |
|---|---|---|
| `0` | Probe succeeded | Atomic protocol-v1 success result written. |
| `2` | Invocation arguments invalid | No trustworthy request was available; no result is written. |
| `3` | Request invalid or unsafe | No trustworthy result destination was available; no result is written. |
| `4` | Operation unsupported | Atomic protocol-v1 failure result and blocking finding written. |
| `5` | Runtime Blender version mismatch or execution failure | Atomic protocol-v1 failure result written when possible. |
| `6` | Result could not be written safely | No successful result may be inferred. |

Standard output is reserved for compact protocol JSON Lines. Stable sanitized diagnostic codes use
standard error. No request content, absolute path, stack trace, or environment value is emitted.

## Local Tests

The shell is standard-library-only so its protocol boundary can be tested without installing or
launching Blender:

```powershell
python -m unittest discover -s tests/blender -p "test_*.py" -v
```

The shared .NET contract suite validates the same request, progress, and result golden files. Real
Blender scene behavior for PB-0416 through PB-0418 can be rerun without using the caller's current
directory:

```powershell
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File scripts\Test-BlenderPb0416ToPb0418.ps1 `
  -RepositoryRoot C:\Dev\PackageBuilder `
  -BlenderExecutable C:\Dev\PackageBuilder\tools\blender\5.0.0\blender.exe
```

The script retains `.blend`, `.glb`, observations, reports, and logs beneath ignored `artifacts` for
machine verification and supplementary manual visual inspection.

## Licensing Boundary

Blender is external software distributed under the GNU GPL. Package Builder does not redistribute
Blender in Git, accept its licence on the user's behalf, or determine eligibility. The current
ignored portable copy was downloaded only after explicit user authorization and remains beneath the
project root. The existing PB-0302 locator verifies the selected contained installation. See the official
[Blender licence page](https://www.blender.org/about/license/).
