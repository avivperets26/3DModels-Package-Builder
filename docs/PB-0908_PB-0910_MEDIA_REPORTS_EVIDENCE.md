# PB-0908 / PB-0909 / PB-0910 — Media validation and JSON reports

## Scope and lifecycle

The user approved these three tasks on one branch: `codex/PB-0908-PB-0910-media-reports`.
Fetch, main checkout, fast-forward pull and clean equality with origin/main preceded branch creation
at `3d7b564614474283548b49a50d5961f09e6e4efb`. PB-0904/PB-0905/PB-0907 were rolled to DONE once,
with task/merge SHA, successful main CI and user confirmation in the Completion Log.

All three tasks are DONE. Task commit `53ecb98befcef5ca0799036ecc4682eba5896724` was pushed and
merged into main as `0c950f0667b43c7a8f2d09423b3bc0351264a650`.
[Main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34589603966)
passed both jobs. The user accepted completion and requested PB-0911/PB-0912. Completion was
recorded exactly once at that branch start on 2026-09-11; the publication receipt is retained at
`artifacts/PB-0908-publication.json`. Publication-pending handoff notes below are historical.

## Acceptance and traceability

| Task / requirement | Implementation | Automated evidence |
| --- | --- | --- |
| PB-0908 — clipped, empty, tiny, exposure, materials, helpers, margins | Domain `PreviewImageValidator`; stable PB-0109 blocking findings and measured coverage/exposure/margins | `PreviewImageValidatorTests`: all eight defects, pixel-edge and projected/depth clipping, transparent empty image, valid product on black, missing/mismatched evidence, invalid tolerances, bounds and cancellation |
| PB-0908 — real renderer evidence | Unity final-material coverage pass, projected bounds, SHA-256 pairs; `PreviewCaptureDecoder` verifies bytes before decode | GPU capture harness: five views, repeat image/mask hashes, nonempty mask excluding studio, positive bounds, no helper/material defects, scene restoration and cleanup; WPF receipt hash/coverage bridge test |
| PB-0909 — 1920x1080 JPEG/PNG and limits | Bounded WIC codec; shared optimizer with Fab policy; atomic empty result on blockers | Real PNG/JPEG round trips, truncated/unsupported/oversized input, exact byte and gallery boundary tests, duplicate/empty inputs and cancellation |
| PB-0909 — visual quality | Re-decode every eligible candidate; normalized whole-image and worst 32x32 tile RGB RMS, exact alpha, delivered-pixel validation | Textured JPEG fixture, local-damage rejection, transparency-preserving PNG, deterministic encoding and independently checked SHA-256 |
| PB-0910 — stable report | `BuildValidationReportJson`, schema v1, existing build-lock/finding contracts | Round trip, redaction, Unicode, culture/order independence, job states, hashes, unknown/nested/duplicate/version fields, invalid references/resources, NaN, collection/input bounds, noncanonical and overflowing integers |
| PB-0908–PB-0910 — connected flow | `Invoke-UnityStillImageCaptureIntegration.ps1 -ValidateMediaPipeline` runs the same WPF test pipeline against five real GPU captures before cleanup | Capture receipts → verified decode/coverage → image checks → optimized gallery → hashes/metrics → strict report round trip; retains compact report/TRX/contact sheet only |
| SEC / cleanup / PERF-007 | Bounded in-memory processing; no source paths in artifact IDs; shared diagnostic redaction; finally cleanup; explicit resource units and unknowns | Hostile input tests, existing redaction regression suite, repository security/cleanup checks, real cleanup receipts; broad performance budgeting remains PB-1808 |

## API, policy and limits

`PreviewCaptureDecoder` accepts encoded final/coverage bytes, their typed SHA-256 receipts, bounds,
depth/material/helper inspection facts and cancellation. Caller/worker owns provenance: both files
and facts must describe the same final pose/camera. Coverage uses alpha >=16 and visible image alpha
>=16. Projected bounds are conservative AABB projections; touching a raster edge also flags clipping.
The Unity worker already refuses invalid materials before producing a capture. Other renderer
adapters may provide positive defect counts to the shared validator. Coverage images are diagnostic,
never gallery deliverables. No renderer-specific pixel policy is copied into Unity.

`PreviewMediaOptimizer` takes at most 32 owned captures, a `PreviewImagePolicy`, and a versioned
`PreviewMediaPolicy`. PNG is lossless; opaque images also try JPEG qualities 95/90/85/80/75. The
smallest qualifying result wins. The default quality budget is .02 global and .06 worst-tile RMS
on 0–1 RGB values. Alpha must match exactly; transparency is never silently flattened. Dimensions
remain 1920x1080. Output metrics describe the decoded delivery pixels. The optimizer writes no files.

Fab defaults conservatively interpret MB as decimal bytes and use exclusive limits of 3,000,000
per image and 25,000,000 per gallery. These are configurable versioned values, checked against
[Fab's official requirements](https://dev.epicgames.com/documentation/fab/asset-file-format-and-structure-requirements-in-fab)
on 2026-09-11. The current Windows-only codec uses existing WPF/WIC APIs documented by
[Microsoft](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-encode-and-decode-a-jpeg-image).
It replaces the planned but unimplemented SkiaSharp choice without adding a package/native dependency.
Encoded input is bounded at 32 MB; dimensions are checked before pixel allocation. PNG animation,
incomplete chunks/trailing content and missing JPEG end markers are rejected. OS codec internals
are outside the portable policy boundary; byte reproducibility is tested on the current Windows
runtime, not claimed across OS/codec versions.

Report schema ID: `https://schemas.packagebuilder.dev/validation-report/v1`. Load the companion
build-lock/v1 schema when using an external validator; the application registers it entirely offline.
Semantic validation additionally requires unique IDs/metric keys/stages, existing finding/metric
artifact references, matching job/version IDs and valid tool versions. IDs/source components are
1–128 ASCII letters/digits/dot/underscore/hyphen, starting alphanumeric; role/metric/stage names
use lower-kebab tokens. This deliberately excludes local filesystem paths from references.
Reports cap artifacts/findings at 1024, metrics at 4096, stages at 64 and JSON at 1,048,576 characters.
Finding text is bounded at 8192 characters and passes shared credential/user-profile redaction.
Final status derives from blockers/job failure, cancellation, completion, or unfinished state.
Resource counters may be null when not instrumented; this is not a claim that their usage was zero.
Time is measured input, never generated inside serialization. Arrays are sorted, numbers are
culture-independent, and output is compact UTF-8 without BOM/newline. HTML rendering stays PB-0911.

## Reuse and duplication audit

Searched existing preview/presentation rules, texture inspection, finding serialization, build locks,
JSON safeguards/schema registry and diagnostic redaction before adding code. Reused Domain artifact/job
IDs and findings, Contracts hashes/versions/duplicate-property safeguards, Application codec boundaries,
Unity's existing bounds/material/capture cleanup and the permanent test-artifact cleanup helper.
Portable texture inspection is header-only and cannot replace a full pixel codec; it remains unchanged.
Redaction moved to Contracts with an Infrastructure facade, preserving previous logging behavior.
Report finding schema fields intentionally mirror PB-0109 for external JSON validation; semantic
validation delegates to the existing finding reader. PB-0910 owns keeping this schema mirror compatible.
No cross-engine pixel checks or marketplace limits are duplicated in the Unity worker.

## Validation record

Validated on 2026-09-11:

- Locked restore succeeded with the approved .NET 10.0.302 SDK; no dependency/lock-file changes.
- Release solution build: zero warnings/errors (`artifacts/PB-0908-final-build.log`).
- Full seven-project suite: 2,530 discovered/passed, zero failures/skips; source snapshot remained
  unchanged during the run (`artifacts/test-results/PB-0908/summary.json` and per-project TRX).
- Scope tests: 14 image-policy, 19 report-contract and 10 WPF/media tests. Final formatting found
  two private test fields missing underscore prefixes; those were renamed, the solution rebuilt,
  and both affected test classes rerun successfully (24 tests). No production behavior changed.
- Full .NET formatting verification at severity info passed; Ruff lint passed and all 51 Python
  files were already formatted. `git diff --check` passed.
- Repository baseline: 35 checks passed, zero failed, including branch exception isolation,
  lifecycle accounting, architecture, public-content controls, Unity policy and cleanup regressions.
- First GPU run `artifacts/u/a5e9fed2` passed and removed its 1,607,850,000-byte disposable project.
- Connected GPU run `artifacts/u/23fd84f7` passed the five-view capture and WPF gallery/report pipeline.
  Delivery totaled 212,017 bytes (40,784–47,654 per image). Global RMS was .00329–.00494 rounded
  outward, worst-tile RMS at most .02681, and all images retained 1920x1080 dimensions. The strict
  report round trip passed with no findings. This is measured fixture evidence, not a universal
  performance guarantee. Its 1,607,060,407-byte clone and all full-size images were removed in finally.
- Both cleaned project paths were verified absent. Compact contact sheet, capture receipt, report,
  TRX/log and cleanup receipts remain. The contact sheet was visually inspected; source fixtures
  and intended releases remain intact. No generated package archives were retained.

The remaining work is review and an authorized commit/push/merge, followed by successful main CI
and user confirmation. Record each DONE transition at the next task branch rollover.

## Changed files and handoff

- Domain: `Media/PreviewRaster.cs`, `PreviewImageEvidence.cs`, `PreviewImageValidator.cs`, `PreviewMediaPolicy.cs`.
- Application: `Media/PreviewMediaContracts.cs`, `PreviewMediaOptimizer.cs`, `PreviewCaptureDecoder.cs`.
- Windows/Fab adapters: `App.Wpf/Media/WindowsPreviewImageCodec.cs`, `Marketplaces.Fab/FabPreviewMediaPolicy.cs`.
- Contracts: report models/JSON, shared diagnostic redactor, embedded schema registration; `schemas/validation-report.schema.json`.
- Infrastructure: existing log redactor delegates to the shared implementation.
- Tests: Domain image checks, Contract report checks, WPF codec/optimizer/report checks.
- Unity: still capture and integration; PowerShell capture harness optionally runs the connected media pipeline.
- Governance: AGENTS, engineering skill, branch policy/baseline regression; backlog and prior-scope rollover evidence.
- Documentation: plan, architecture, quality traceability, index, cleanup audit and this evidence.

Suggested commit: `feat: validate preview media and generate versioned JSON reports`.
After reviewing the diff, the user may run:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
git add AGENTS.md docs scripts skills/package-builder-engineering src tests schemas engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor
git diff --cached --stat
git diff --cached
git commit -m "feat: validate preview media and generate versioned JSON reports"
git push -u origin codex/PB-0908-PB-0910-media-reports
```

Publication/main CI/user confirmation remain required; task statuses stay IN PROGRESS until the
next branch rollover. No desktop visual interaction was changed. Compact render evidence can be
inspected without retaining disposable packages or Unity projects.
