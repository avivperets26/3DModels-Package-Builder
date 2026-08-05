# Package Builder Blender Worker

This directory contains the protocol shell loaded by the selected contained Blender executable.
PB-0401 implements request validation, JSON Lines progress, atomic result output, and stable exit
codes. PB-0402 adds direct-data scene reset and temporary data-block ownership utilities. PB-0403
and PB-0404 add bounded FBX and single-file GLB import adapters. PB-0405 adds deterministic
geometry and transform inspection. Texture, rig, animation, normalization, export, and reimport
remain assigned to later tasks.

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
Blender scene behavior and contained engine integration remain later tasks.

## Licensing Boundary

Blender is external software distributed under the GNU GPL. Package Builder does not redistribute
Blender here, install it, accept its licence, or determine eligibility. The user selects a
contained installation that the existing PB-0302 locator verifies. See the official
[Blender licence page](https://www.blender.org/about/license/).
