# PB-0504 Portable README Generator Evidence

## Status

- Canonical branch: `feat/PB-0504-portable-readme`
- Publication branch: `feat/PB-0503-portable-textures`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-06

The shared branch is the exact user-approved PB-0503/PB-0504 publication exception. Task identity,
acceptance evidence, canonical branch, and eventual Completion Log row remain independent.

## Implemented Boundary

`PortableReadmeRequest.Create(...)` validates a PB-0110 typed manifest, PB-0107 publisher profile,
PB-0501 naming profile, positive measured dimensions, optional GLB variant, PB-0503 texture-copy
receipts, and bounded usage instructions. Cross-contract publisher/naming mismatches, duplicate or
missing texture receipts, unexpected assignments, and distinct sources colliding on one canonical
portable role fail explicitly.

`PortableReadmeGenerator.Generate(...)` emits deterministic LF text and UTF-8 bytes without a BOM.
It renders product identity and case, FBX/GLB/archive/README names, texture format/dimensions/colour
space/normal convention, model dimensions in metres, materials, rig type/root/bone count,
animation ranges/FPS/duration/loop/root motion, set or collection inventory, usage, AI disclosure,
support contact, and copyright from typed values. It uses invariant formatting and invents no GLB,
rig, animation, texture, item, or profile fact.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Case-specific contents | exact tests for static, rigged, animated, set, and collection cases |
| Formats and dimensions | FBX/optional GLB/archive/README, texture metadata, and invariant metre values |
| Materials, rig, and animation | textured/untextured materials plus exact rig and clip metadata assertions |
| Usage and item inventories | bounded validated usage plus categorized/uncategorized item tests |
| AI, support, and copyright | every AI state, email/HTTPS support, single-year/range policies |
| Deterministic safe output | LF/UTF-8 equality, culture switch, ordering, and invalid-input tests |

## Evidence Boundary

This task generates typed text in memory. PB-0505 places the flat README in the archive, PB-0507
will produce a physical release, and PB-0901 will own broader marketplace boilerplate rendering.
No UI or preview renderer changes in PB-0504.

## Local Validation

- Focused portable-target suite: 162 passed, 0 failed, 0 skipped.
- All new PB-0503/PB-0504 production classes: 100% line and branch coverage in the Microsoft
  Cobertura report beneath ignored `artifacts/PB-0503-PB-0504/coverage-ms-final`.
- Full Core CI: all nine stages passed in 4 minutes 21 seconds.
- Complete solution tests: 2,229 passed, 0 failed, 0 skipped across six test projects.
- Release build: 17 projects, 0 warnings, 0 errors.
- Repository baseline: 29 passed, 0 failed.
- Locked restore, info-level .NET formatting, Ruff lint/formatting, PowerShell parsing, task graph,
  Markdown links, secret/prohibited-content checks, and `git diff --check`: passed.

## Remaining Gates

- User-controlled commit, push, merge into and push of `main`.
- Successful required `main` CI and explicit user confirmation.
- Next-task rollover synchronization.
