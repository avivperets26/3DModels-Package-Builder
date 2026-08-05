# PB-0405 Geometry and Transform Inspection Evidence

## Status

- Task: PB-0405 — Implement geometry and transform inspection
- Branch: `feat/PB-0405-geometry-inspection`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

PB-0405 is implemented and under local validation. It remains active until the user-controlled
commit, push, merge, required `main` CI, explicit confirmation, and next-task rollover gates pass.

## Implemented Boundary

`package_builder_blender.geometry_inspection.inspect_geometry(...)` receives imported Blender
objects and reads direct data only. It does not depend on selection, active object, editor area,
interaction mode, or filesystem paths.

The immutable result reports:

- every object's ordinal identity, Blender type, and decomposed world translation, quaternion,
  and scale;
- every mesh object's vertex, polygon, and loop-triangle counts;
- all eight transformed object bounding-box corners, world minimum/maximum, and dimensions;
- UV-layer names, face-corner value counts, and active-render status;
- validated face-corner normal and loop-tangent counts;
- material-slot names in Blender order;
- `uint16` or `uint32` index requirements; and
- deterministic scene totals and aggregate world bounds.

The inspector calls Blender's mesh-owned loop-triangle and tangent calculation APIs explicitly.
Calculated tangents are released immediately after their finite values and bitangent signs are
validated. A mesh without UV layers reports zero tangents without manufacturing data.

## Safety and Determinism

- Inputs are copied into immutable plain-Python reports rather than retaining Blender references.
- Objects and mesh reports are sorted by ordinal, case-sensitive object name.
- Duplicate/control-character identities, malformed topology, missing meshes, unavailable bounds,
  invalid material or UV metadata, and non-finite transforms, bounds, normals, tangents, or signs
  fail closed.
- Expected failures return one stable PB-0109-compatible blocking finding. Raw Blender exception
  text, physical paths, and source content are never included.
- Index format is based on both the vertex-buffer size and maximum referenced vertex index, so
  meshes exceeding the unsigned 16-bit range require `uint32`.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_GEOMETRY_INPUT_INVALID` | Scene objects cannot be enumerated safely or the scene is empty. |
| `BLENDER_GEOMETRY_DATA_INVALID` | Blender geometry, transform, shading, or identity data is incomplete, inconsistent, or non-finite. |
| `BLENDER_GEOMETRY_MESH_MISSING` | The imported scene has objects but no usable mesh object. |

Every finding is emitted by `blender-geometry-inspector` as a blocking error with sanitized
corrective guidance.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Objects and transforms reported | complete-facts and deterministic multi-object tests |
| Meshes, vertices, polygons, and triangles reported | complete-facts and aggregate tests |
| Dimensions and world bounds reported | transformed and aggregate bounds assertions |
| UVs, normals, tangents, and material slots reported | complete shading-facts test |
| Index requirement reported | 16-bit complete fixture and 65,537-vertex 32-bit fixture |
| UV-less meshes remain inspectable | zero-tangent test |
| Invalid and unreadable Blender data fails safely | input, topology, identity, finite-value, tangent, and cleanup failure tests |

## Validation

- Focused PB-0405 tests: 10 passed, 0 failed.
- All Blender worker tests: 50 passed, 0 failed.
- Focused `geometry_inspection.py` trace measurement: 97% executable-line coverage. No branch
  coverage percentage is claimed.
- Debug solution build: 16 projects, 0 warnings, 0 errors.
- Release solution build: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 3 minutes 23.647 seconds.
- Repository-local Ruff 0.15.22 lint and formatting: passed across all 14 Python files.
- NuGet vulnerability audit: no known vulnerable direct or transitive package reported for any of
  the 16 projects.
- Locked restore, .NET formatting, PowerShell parsing, task/dependency/lifecycle checks,
  documentation links, secret/prohibited-content checks, and `git diff --check`: passed.

## Evidence Limits

- No approved contained Blender executable is currently present. The focused suite uses strict
  deterministic data doubles, so actual `bpy` execution, evaluated modifier geometry, and engine
  version behavior are not claimed.
- The inspector reports the imported object data supplied to it; applying modifiers or evaluating
  a dependency graph is outside PB-0405.
- Python branch coverage is not currently measured, so no branch-coverage claim is made.
- Material texture connectivity, rigs/weights, animations, normalization, export, and clean
  reimport remain owned by PB-0406 through PB-0417.
- This task has no WPF or rendered visual change, so manual visual testing is not applicable yet.

## Remaining Gates

Final local validation, user-controlled staging/commit/push/merge, successful required `main` CI,
explicit user completion confirmation, and PB-0406 rollover remain. PB-0405 stays `[ ]` /
🟡 **PROCESS** and absent from the Completion Log. No exception is used.
