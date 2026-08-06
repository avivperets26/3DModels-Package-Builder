# PB-0417 Blender Clean-Reimport Validation Evidence

## Status

- Canonical branch: `test/PB-0417-blender-reimport-validation`
- Publication branch: `feat/PB-0416-normalized-glb-export`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

The publication topology is the exact user-approved PB-0416/PB-0417/PB-0418 exception. Task
identity and evidence remain independent and the exception creates no precedent.

## Implemented Boundary

`package_builder_blender.clean_reimport.validate_clean_reimports(...)` validates only contained,
non-link, non-empty direct artifact references for FBX and GLB. Each expectation declares its
product case, exact object/mesh/material/skeleton/animation counts, finite ordered bounds, and, for
animated products, unique representative object/clip/frame/vertex deformation samples.

The injected process boundary must return one observation from a new empty Blender process per
artifact. Empty-scene proof, source identity, and unique non-empty process identity are mandatory.
Counts compare exactly; bounds and representative deformation compare against explicit finite
tolerances. Missing samples, changed identities, changed positions, mismatched counts/bounds,
non-empty or reused processes, unsafe paths, and runner exceptions become stable sanitized findings.

## Acceptance and Automated Evidence

| Requirement | Focused evidence |
|---|---|
| Fresh process for every artifact | unique process identity and empty-scene assertions for FBX and GLB |
| Compare complete inventories | exact five-count match and mismatch assertions |
| Compare bounds | exact, tolerated, and outside-tolerance assertions |
| Compare representative deformation | exact, tolerated, missing, and changed sample assertions |
| Fail safely | malformed expectation, unsafe path, reused process, and runner-exception assertions |

Focused PB-0417 tests: 7 passed, 0 failed. The complete Blender suite reports 156 passed.

## Contained Blender 5.0.0 Evidence

The opt-in engine harness started three separate Blender 5.0.0 processes from factory-empty state,
one for each PB-0416 static, rigged, and animated GLB. Every process has a distinct retained process
identity. Exact object, mesh, material, skeleton, and Action counts matched. Mesh bounds matched at
the reference frame within `1e-4`; the animated evaluated vertex matched its object/Action/frame/
vertex identity and position within `1e-4`.

An initial real run correctly failed because the observer measured animated bounds at frame 20
after taking the deformation sample. The observer was corrected to restore frame 1 in `finally`;
the production comparison and its tolerances were not weakened. The clean rerun passed 3/3.

Machine-readable observations and the passing comparison report are retained beneath
`artifacts/PB-0416-PB-0418-real-blender/20260806-131612-bc6a7d1255d4426fb495d1d4acf6aefe`.

## Combined Local Validation

- PB-0416/PB-0417/PB-0418 focused tests: 19 passed, 0 failed.
- Built-in focused executable-line trace: `clean_reimport` 100%; Python branch coverage is not
  currently measured or claimed.
- Repository baseline: 29/29 passed.
- Full Core CI: all nine stages passed in 3 minutes 29.519 seconds after real-engine integration.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors; baseline .NET tests: 2,067 passed.
- Vulnerability audit: no known vulnerable direct or transitive NuGet packages.
- Ruff 0.15.22 lint/format, .NET format, locked restore, task graph, links, repository security
  checks, and `git diff --check` passed.

## Evidence Boundary

Tests use a strict fresh-process runner boundary and prove that independent observations are
required and compared correctly. The contained run additionally proves real GLB reimports in
separate empty Blender processes. This task validated the GLB artifacts produced by PB-0416; PB-0415 retains its own FBX
export evidence and future matrix tasks own broader cross-version fixture execution.

## Publication Evidence

- Task commit: `e43ded6d7a36764df42cca89f0380a4d40aeb251`.
- Pull request: [#62](https://github.com/avivperets26/3DModels-Package-Builder/pull/62).
- Merge commit: `45a6af25813c2494771ca6237f2be7d1eb83695d`.
- Required successful [main workflow run 31104970924](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31104970924).
- The user explicitly confirmed the push, merge, green required `main` CI, and completion on
  2026-08-06. No CI or quality exception was used.
