# PB-0418 Blender Failure and Regression Fixtures Evidence

## Status

- Canonical branch: `test/PB-0418-blender-regression-fixtures`
- Publication branch: `feat/PB-0416-normalized-glb-export`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-06

The shared publication branch is the exact user-approved PB-0416/PB-0417/PB-0418 exception. The
fixture portfolio, stable finding boundary, focused tests, and lifecycle remain independently
reviewable and the exception creates no precedent.

## Implemented Boundary

`package_builder_blender.regression_validation.validate_regression_observation(...)` accepts only
bounded inspection facts and emits deterministic blocking findings without exception or parser
detail leakage. The tracked `tests/fixtures/blender/regression/fixture-cases.json` portfolio defines
seven versioned cases: corrupt FBX, corrupt GLB, missing images, multiple rigs, no UVs, unsupported
data, and invalid animation. Minimal corrupt FBX/GLB payload descriptors are retained beside the
manifest and materialized with engine extensions only beneath ignored project artifacts during
tests, preserving the repository prohibition on tracked model binaries.

Stable finding order is corrupt source, missing texture reference, multiple unsupported rigs,
required UV absence, unsupported data, and invalid animation. Malformed observations fail closed as
`BLENDER_REGRESSION_INPUT_INVALID`; a successfully parsed mesh without a known regression returns a
small safe report. Existing FBX/GLB adapters are exercised with parser exceptions against both
corrupt retained files to prove cleanup, stable error codes, and diagnostic sanitization.

## Acceptance and Automated Evidence

| Required case | Stable finding |
|---|---|
| Corrupt FBX/GLB | `BLENDER_SOURCE_CORRUPT`; adapter boundaries return format-specific import failure |
| Missing images | `BLENDER_TEXTURE_REFERENCE_MISSING` |
| Multiple rigs | `BLENDER_MULTIPLE_RIGS_UNSUPPORTED` |
| No UVs on intended textured mesh | `BLENDER_UV_REQUIRED` |
| Unsupported data | `BLENDER_DATA_UNSUPPORTED` |
| Invalid animation | `BLENDER_ANIMATION_INVALID` |

Focused PB-0418 tests: 5 passed, 0 failed. All seven manifest cases, deterministic multi-failure
order, malformed facts, healthy facts, and corrupt import boundaries are covered. The complete
Blender suite reports 156 passed.

## Contained Blender 5.0.0 Evidence

`scripts/Test-BlenderPb0416ToPb0418.ps1` materializes the two inert tracked payloads with `.fbx` and
`.glb` extensions only inside ignored artifacts, then invokes Blender's real importers. Blender
rejected both corrupt sources without terminating the process. Actual Blender data blocks supply
the missing-image, two-armature, no-UV, Volume, and empty-Action facts. The production validator
emitted the exact seven catalogued finding-code sets: 7/7 passed with no worker crash.

The passing report and sanitized Blender log are retained beneath
`artifacts/PB-0416-PB-0418-real-blender/20260806-131612-bc6a7d1255d4426fb495d1d4acf6aefe`.

## Combined Local Validation

- PB-0416/PB-0417/PB-0418 focused tests: 19 passed, 0 failed.
- Built-in focused executable-line trace: `regression_validation` 100%; Python branch coverage is
  not currently measured or claimed.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 3 minutes 29.519 seconds after real-engine integration.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors; baseline .NET tests: 2,067 passed.
- Vulnerability audit: no known vulnerable direct or transitive NuGet packages.
- Ruff 0.15.22 lint/format, .NET format, locked restore, task graph, links, repository security
  checks, and `git diff --check` passed.

## Evidence Boundary

The tracked corrupt payloads and strict parser doubles retain stable worker-boundary failure
coverage. The contained Blender run now proves real importer rejection and derives the remaining
bounded facts from actual Blender data blocks. PB-0418 remains PROCESS only because publication
gates are outstanding; visual review is not an acceptance mechanism for corrupt-source findings.

## Remaining Gates

- User-controlled commit, push, merge, required `main` CI, and explicit completion confirmation.
- Next-task rollover synchronization after all completion gates pass.
