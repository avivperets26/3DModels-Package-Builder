# PB-0506 Portable Target Validator Evidence

## Status

- Canonical branch: `feat/PB-0506-portable-validator`
- Publication branch: `feat/PB-0505-deterministic-fbx-zip`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

The shared branch is the exact user-approved PB-0505/PB-0506 publication exception. Task identity,
acceptance evidence, canonical branch, and eventual Completion Log row remain independent.

## Implemented Boundary

`PortableTargetValidator.ValidateAsync(...)` performs a read-only fail-closed validation over the
typed naming/folder plan, actual ZIP bytes and PB-0505 receipt, PB-0503 texture receipts, PB-0504
README bytes, exported-asset reference evidence, and PB-0417 clean-reimport results.

The validator checks the exact archive SHA-256 and length before ZIP inspection, then verifies
manifest-derived receipt entries, archive order, canonical entry names, and fixed timestamps. It
requires the canonical FBX, validates an intended optional GLB, exact separate textures, README
identity, FBX texture references, self-contained GLB references, and exactly one successful
clean-reimport result per delivered model.

Every failure is returned as an immutable PB-0109 `ValidationFinding` with stable code, Error
severity, corrective action, source `portable-target-validator`, and `BlocksRelease=true`. A report
passes only when it contains no blocking finding. Expected invalid input, cancellation, corrupt
identity, and I/O failure never crash the validator or disclose physical paths.

## Stable Finding Codes

- `PORTABLE_NAMING_INVALID`
- `PORTABLE_ARCHIVE_INVALID`
- `PORTABLE_TEXTURES_INVALID`
- `PORTABLE_README_INVALID`
- `PORTABLE_REFERENCES_INVALID`
- `PORTABLE_REIMPORT_FAILED`

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Archive and FBX | exact archive identity/layout metadata plus canonical FBX presence |
| GLB | optional intended GLB must be unique, self-contained, and reimport successfully |
| Textures | canonical copied texture receipts exactly match the flat-folder manifest |
| README | generated UTF-8 bytes exactly match the planned artifact identity |
| Naming and references | typed roots/files and FBX/GLB external-reference policies are enforced |
| Reimport result | every delivered FBX/GLB requires exactly one passing PB-0417 result |
| Blocking pass/fail | every negative category yields a blocking structured finding; valid package has none |

## Local Validation

- Focused portable-target suite: 187 passed, 0 failed, 0 skipped.
- Valid package with and without optional GLB: pass with zero findings.
- Archive, naming, texture, README, reference, reimport, cancellation, and I/O negatives: blocking.
- All 18 instrumented PB-0505/PB-0506 production classes: 100% line and branch coverage in ignored
  `artifacts/PB-0505-PB-0506/coverage-final-2`.
- Full Core CI: all nine stages passed in 3 minutes 46.8 seconds.
- Complete solution tests: 2,254 passed, 0 failed, 0 skipped across six test projects.
- Debug and Release solution builds: 17 projects, zero warnings and zero errors.
- Repository baseline: 29 passed, 0 failed.
- Locked restore, .NET/Ruff formatting, PowerShell parsing, task graph, Markdown links,
  secret/prohibited-content checks, history integrity, and `git diff --check`: passed.
- NuGet audit: no vulnerable direct or transitive package reported across all 17 projects.

## Manual and Visual Boundary

PB-0506 has no WPF or renderer surface. Its output can be inspected programmatically as findings,
but the first user-runnable physical portable package and end-to-end validation report are owned by
PB-0507. Visual model/texture inspection remains the Blender/engine preview responsibility.

## Publication and Completion

- Combined task commit: `38d82cc00572f506c3dc2cf67f996d64e50e64dd`.
- Integration: [pull request #65](https://github.com/avivperets26/3DModels-Package-Builder/pull/65).
- `main` merge: `725c5d21fa5d28342d62946b4ac93184a33656f9`.
- Required [main workflow run 31123410839](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31123410839): successful for the exact merge.
- User confirmation and rollover date: 2026-08-06.
- Exception boundary: the approved combined publication branch affected topology only; no CI,
  quality, or completion exception was used, and no precedent was created.
