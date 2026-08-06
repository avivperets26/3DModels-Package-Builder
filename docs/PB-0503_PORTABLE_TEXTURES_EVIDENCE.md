# PB-0503 Portable Texture Copy and Conversion Evidence

## Status

- Canonical and publication branch: `feat/PB-0503-portable-textures`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-06

PB-0503 shares the explicitly user-approved publication cycle with PB-0504. Each task retains an
independent acceptance boundary, canonical branch, production API, evidence, and lifecycle. The
exception creates no precedent.

## Implemented Boundary

`PortableTextureProcessor.CopyAsync(...)` accepts only validated PB-0205 artifact records for the
portable target with role `normalized-texture`. It supports the six PB-0501 canonical separate-map
roles and validates PNG or JPEG bytes before producing a canonical filename/reference receipt.

The processor verifies source length and SHA-256 identity before inspection, streams through a
bounded pooled buffer, hashes the bytes again while copying, restores source position, removes
partial destination bytes on expected failures, supports cancellation, and returns stable
non-throwing errors. It never modifies the source.

PNG validation checks signature, IHDR shape, positive bounded dimensions, pixel-count limits, and
IEND. JPEG validation checks SOI/EOI, bounded marker segments, supported SOF markers, and positive
bounded dimensions. `.jpeg` may normalize to the canonical `.jpg` name because the byte format is
unchanged. PNG/JPEG cross-format requests block with `ConversionRequiresReencoding`; renaming a
suffix is never treated as conversion, and lossy re-encoding is not silently performed.

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Canonical separate textures are named and referenced correctly | exact six-role filename and flat-FBX relative-reference assertions |
| Bytes are copied without source mutation | exact destination-byte, source-position, length, and SHA-256 assertions |
| Formats are validated | valid and malformed PNG/JPEG structural and dimension cases |
| Unsafe or ambiguous conversion does not occur | unsupported-format and cross-format structured-failure tests |
| Integrity and failure behavior fail closed | changing source, short read, hash/length mismatch, cancellation, I/O, and cleanup-failure tests |

## Evidence Boundary

This task materializes one texture into a caller-owned stream. PB-0502 owns the folder plan,
PB-0504 owns README text, PB-0505 owns ZIP creation, and PB-0507 will orchestrate physical release
output. No image pixels are transformed and no WPF surface is added.

## Local Validation

- Focused portable-target suite: 162 passed, 0 failed, 0 skipped.
- All new PB-0503/PB-0504 production classes: 100% line and branch coverage in the Microsoft
  Cobertura report beneath ignored `artifacts/PB-0503-PB-0504/coverage-ms-final`.
- PNG/JPEG policy, all six roles, malformed structures, stream faults, cancellation, source changes,
  and deterministic identity/reference behavior are covered.
- Full Core CI: all nine stages passed in 4 minutes 21 seconds.
- Complete solution tests: 2,229 passed, 0 failed, 0 skipped across six test projects.
- Release build: 17 projects, 0 warnings, 0 errors.
- Repository baseline: 29 passed, 0 failed.
- Locked restore, info-level .NET formatting, Ruff lint/formatting, PowerShell parsing, task graph,
  Markdown links, secret/prohibited-content checks, and `git diff --check`: passed.

## Publication Evidence

- Combined task commit: `4b482e97cb4fadf112ebb95ad972831b5f6141cd`.
- PR: [#64](https://github.com/avivperets26/3DModels-Package-Builder/pull/64).
- Merge commit: `33d077a5eb63a9398d720fb60b08bfd8871f7bc5`.
- Required [main workflow run 31116763102](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31116763102): succeeded.
- Optional PR run `31116752821` failed while GitHub could not download the pinned action; the same
  commit merged and the required `main` workflow passed. Pull-request CI is optional, so no CI or
  quality exception was used.
- User confirmation and completion date: 2026-08-06.
