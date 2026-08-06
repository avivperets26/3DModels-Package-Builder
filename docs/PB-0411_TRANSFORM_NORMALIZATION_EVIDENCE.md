# PB-0411 Unit, Axis, Pivot, and Transform Normalization Evidence

## Status

- Canonical branch: `feat/PB-0411-transform-normalization`
- Publication branch: `feat/PB-0409-case-inference`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

The shared publication branch is the exact user-approved PB-0409/PB-0410/PB-0411 exception and
creates no precedent.

## Implemented Boundary

`package_builder_blender.transform_normalization.normalize_transforms(...)` validates explicit
source and target forward/up axes, positive finite unit scales, target display-unit system, and
one pivot policy: keep, world-bounds center, or centered base. It computes an orthonormal axis
conversion, applies the unit ratio and pivot translation to every object world matrix, updates
scene unit settings, and reports deterministic object/mesh counts and world bounds before and
after normalization.

The operation snapshots all object matrices and unit settings. It fingerprints bone hierarchy and
rest matrices, raw vertex memberships/weights, and legacy or Blender 5 layered Action F-curve
points before and after the conversion. Any deformation-data change or invalid/malformed Blender
data fails the transaction and restores matrices and unit settings; incomplete rollback requires
workspace discard. Mesh data is not baked here because PB-0414 owns modifier and transform baking.

## Stable Finding

| Code | Meaning |
|---|---|
| `BLENDER_TRANSFORM_NORMALIZATION_FAILED` | Policy/data are invalid, application failed, or deformation changed. |

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Configured units are applied | unit-ratio, dimensions, and scene-unit assertions |
| Configured axes are applied | nonidentity orthogonal axis-conversion test |
| Configured pivot is applied | keep, bounds-center, and bounds-base tests |
| Deformation does not change | raw weight, bone rest, Action curve, forced-mutation, and rollback tests |
| Before/after metrics reported | count, bounds, and dimension assertions |

## Validation

- Focused PB-0411 tests: 13 passed, 0 failed.
- Focused executable-line trace: 98%; Python branch coverage is not currently measured or claimed.
- Combined focused tests: 29 passed; all Blender worker tests: 117 passed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; final Core CI: all nine stages passed in 2 minutes 35.460 seconds.
- Ruff lint/formatting, locked restore, .NET formatting, PowerShell parsing, documentation/task graph,
  secret/prohibited-content, vulnerability, and `git diff --check` checks passed.

## Evidence Limits and Remaining Gates

Blender's current API documents `UnitSettings.scale_length`, the six signed axis tokens accepted by
`axis_conversion`, and unique ID data-block naming/collision behavior. Plain-Python doubles cover
the transformation boundary, but no approved contained Blender runtime is present, so real engine
integration is not claimed. No UI or rendered output changed; manual visual testing is not
applicable. User-controlled Git publication, required `main` CI, explicit confirmation, and
next-task rollover remain.
