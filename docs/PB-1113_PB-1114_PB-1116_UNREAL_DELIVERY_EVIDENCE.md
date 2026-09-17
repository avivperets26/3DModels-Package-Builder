# PB-1113 / PB-1114 / PB-1116 — Unreal delivery and preview scope

Branch: `feat/PB-1113-PB-1114-PB-1116-unreal-package-preview`.
Fetched/pulled main base: `4389ff27dca342578745a339e8ff415697958d39`.
The user approved these three tasks together on 2026-09-16 and requested finishing PB-1116
on 2026-09-17. The user subsequently authorized commit, push and merge on 2026-09-17.
This document records local acceptance before publication; exact Git/main-CI evidence belongs
in the publication handoff and the next branch's completion rollover.
PB-1110–PB-1112 roll to DONE once here using task `a7aa8c4`, merge `4389ff2`,
[successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/35131715740)
and the user's acceptance and selection of this successor scope.

## Current status

All three tasks are implemented and locally validated. PB-1116 native Blueprint/UMG controls
passed live PIE interaction and helper-free extracted-project play, including the complete
interactive delivery flow and deliberate rejection of a missing preview asset.
All three remain unchecked / IN PROGRESS until the existing publication, main CI,
user-confirmation and next-branch rollover gates. There is no remaining toolchain blocker.

## Implementation and reuse

`UnrealPreviewPlan.Create` serializes the existing `PreviewExperienceContract` through
`PreviewExperienceJson`. Optional `unreal-preview-plan.json` makes interactive intent explicit;
absence retains static overview behavior. The native boundary checks versions, identity,
supported bindings/focus order, bounded values, recovery controls and agreement with the
existing overview background/lights before authoring assets. Unsupported policy is rejected.
Item/animation controls belong to later E12 work; this implements the static-product subset.

The generator adds only `WBP_Preview`, `BP_Preview` and `BP_PreviewGameMode` beneath the
product's `Preview` folder and one controller actor to the existing overview. Native Blueprint
math translates shared yaw wrapping, pitch/zoom limits, input increments and reset values.
It moves the camera and key light, never the product. The overlay provides labelled buttons,
light sliders, visible keyboard focus, accessible names/help, hide and a separate restore control.
Empty panel space cannot initiate a camera drag. Fixed UI scaling keeps small viewports readable.
Explicit key/fill forward-shading priorities prevent the on-screen competing-light warning;
warnings are not hidden with a console flag.

An editor-only helper uses public Kismet/UMG APIs for widget-variable registration, delegates,
native function nodes and CreateWidget authoring. Typed native call nodes avoid a candidate
operator-promotion crash. Test-only input dispatch and viewport capture stay in that helper.
Its successful build/binary hashes are checked before copying it into an owned generation clone.
No helper calls/classes, plugin, C++ source or binary ships in the ZIP. Editable graph metadata
may reference Epic's built-in BlueprintGraph/UMGEditor; runtime uses built-in Engine/UMG.

`prepare-unreal-delivery` reuses native overview validation and sanitizes reimport filenames
through `AssetImportData.scripted_add_filename`, never byte-patching assets. It saves,
revalidates and returns a hash-bound inventory. `UnrealProjectArchive` reuses the deterministic,
streamed `VerifiedReleaseArchiveWriter`; extraction reuses `SafeZipArchiveService`.
Only the product descriptor, reviewed config, assets/map and UTF-8 README enter the ZIP.
Caches, build files, Plugins, unrelated products and absolute source paths are excluded.
The descriptor disables engine plugins by default and declares no plugins/modules.

The harness reuses signed candidate discovery, existing Blender/static fixtures, the host plan
exporter, `UnrealProjectClone`, process leases and guarded finally cleanup. It hides the original
project, reopens the extraction, executes its saved preview without the authoring helper and
verifies all delivered hashes afterward. Vendor Python/editor scripting plugins are enabled
only as external verification instrumentation; they are not shipped.

Reuse audit: shared contracts/serialization/state vectors, overview framing/materials, native
validation, archive/extraction and process/cleanup utilities are reused. Blueprint bytecode
cannot call managed Domain state types, so the Unreal adapter owns an intentional native math
translation verified against the same Domain vectors. No parallel business-rule model,
WPF preview policy or archive implementation was introduced.

## Acceptance mapping

| Requirement | Check |
| --- | --- |
| PB-1113 / SEC-002 / REL-008 clean inventory | Reject generated/foreign/traversal/alternate-stream paths; exact native asset closure |
| PB-1113 / PERF-003 deterministic ZIP | Shared streamed writer; reversed order yields identical bytes; mutation clears partial ZIP |
| PB-1113 no machine paths/editor dependencies | Native sanitization, UTF-8/UTF-16 source-root scan, closed descriptor/config |
| PB-1114 safe extraction/reopen | Every SHA-256 checked; fresh Unreal processes with original project unavailable |
| PB-1114 negative gates | Missing map/preview, restored source path and extra descriptor fields block validation |
| PB-1114 preservation | Source and delivered hashes unchanged after reopening/PIE |
| PB-1116 shared behavior | Six camera/three lighting vectors in native graphs and PIE; non-finite guards |
| PB-1116 controls | Drag/wheel, arrows/zoom, R/L reset, Tab/Shift+Tab, focus isolation, Enter/Space, sliders/buttons |
| PB-1116 capture/accessibility | Actual visible/hidden captures; accessible names/help, visible focus, persistent restore control |
| PB-1116 runtime isolation | Product unchanged; no default pawn; helper absent in extracted PIE |
| PB-1116 diagnostics | Independent stock-engine baseline; other warnings/errors fail; explicit directional-light priority |
| INSTALL-009 cleanup | Owned ZIPs, source/extracted projects and fault copies removed in finally on success/failure |

## Native evidence

Final interactive delivery: `artifacts/PB-1113/a31b537364244998a80da0a94ad4fb58/receipt.json`.
It records 15 delivered files, 13 accepted native operations, `passed`, `realEngineRun`,
`sourceUnchanged`, `freshProjectHashesUnchanged` and `cleanupSucceeded` all true.
`preview-play.json` records 59 checks; `fresh-preview-play.json` records eight checks with the
authoring helper absent. Both processes exit 0. Native commandlet operations have no
warnings/errors. The baseline and each PIE log contain only the separately classified engine
notice described below. Final visible/hidden screenshots were inspected: readable labels,
visible focus, correct panel recovery, no on-screen lighting warning, and unchanged product.
The editor viewport captured at 704×467; these are interaction screenshots, not PB-1111's
1920×1080 deliverable media. No claim of interactive multi-resolution acceptance is made.

The previous full interactive run `57359789ded34bb384635874b1cb9a1b` also passed, with 12
operations before the missing-preview negative case was added. Both owned ZIPs/projects were
removed. Four late crash XMLs from earlier failed graph probes remain after cleanup was denied;
see the cleanup audit. They are diagnostics, not test packages or project assets.

Regression: Release build succeeded with zero warnings/errors. All 54 Unreal Python tests and
Ruff lint/format checks passed. The seven-project .NET run covered 2,815 tests: 2,814 initially
passed and one existing process-timeout test missed its 250 ms startup deadline while Unreal,
the full suite and formatting ran concurrently. After the heavy run finished, the entire
Infrastructure project passed 653/653 unchanged, including that test. Its separate TRX is
`artifacts/test-results/PB-1116/PB1116-infrastructure-rerun.trx`; original TRXs are retained.
Three C# formatting findings (import order and two `var` declarations) were corrected and the
solution rebuilt. Final non-mutating .NET/Ruff formatting verification passed, as did all
36 repository baseline checks and all 11 quality/release checks. Backlog verification reports
265 total, 157 DONE, three IN PROGRESS, zero BLOCKED, 105 BACKLOG (108 remaining, 59.2%).
The changed-file credential-pattern scan found no credential matches. `git diff --check`
passed. No Git publication or main CI is claimed by these local results.

Earlier accepted static run: `artifacts/PB-1113/a7473877a0c144b9a7d0ab32141ebf0a/receipt.json`
(12 files, nine operations, source/extraction unchanged, cleanup true). This historical receipt
does not claim interactive acceptance. Its generated ZIP/project paths no longer exist.

Native helper: `artifacts/PB-1116/57e71bcbd2654ed5bd9733ebda98f2a6/receipt.json` records build
and smoke exit 0, source/binary identities and cleanup. Native graph conformance:
`artifacts/PB-1116/40230c3689af4fb8b2da7714b3d5bcb7/receipt.json` passed six camera/three
lighting vectors and non-finite guards without native warnings/errors. Compact evidence stays;
generated projects are removed.

Unreal 5.8.2 emits one `r.MotionVectorSimulation` render-thread safety warning in rendered PIE.
A separate world with only Epic's stock cube reproduces it before product generation. The
harness records it separately only if the identical message occurs exactly once in both baseline
and product logs. Missing baseline, duplicates, other warnings or errors fail. Commandlet
validation still requires zero warnings/errors. PIE uses English culture to avoid unrelated
candidate startup localization smoke failures. This is development compatibility evidence,
not global engine-version approval.

## Toolchain and historical recovery

The user authorized VS Code plus C++ Build Tools and explicitly authorized retrying a cancelled
SDK elevation prompt on 2026-09-17. Installed: Build Tools 17.14.37710.0, MSVC 14.44.35229.0
(family 14.44.35207), Windows SDK 10.0.22621.0 and .NET Framework SDK 4.8. Signed installer
receipt: `downloads/visualstudio/2022/installer-receipt.json`. The external engine exception
remains limited to the verified `C:\Program Files\Epic Games\UE_5.8` installation.

Missing-SDK receipt `7452a933884740e18959707c3645bb0e` and initial helper successes
`c272c84a3f2c44cf8f89d1b0cc2c4c48` / `63a69a3c49cf4d9ca8c6198485613ada` remain historical
under `artifacts/PB-1116`. Their owned projects were cleaned. SDK cancellation is resolved.
Previously denied scratch cleanup remains in [the cleanup audit](TEST_ARTIFACT_CLEANUP.md)
and was not retried or bypassed. Reusable tools/DDC are deliberately retained.

## Changed files and repeat validation

Host plan/archive: `src/PackageBuilder.Targets.Unreal/UnrealPreviewPlan.cs` and
`UnrealProjectArchive.cs`. Native implementation: `workers/unreal/package_builder_unreal/preview*.py`,
`overview.py`, `project_validation.py`, `delivery.py`, worker dispatch and the editor helper.
Harnesses: `scripts/unreal_preview*`, `unreal_delivery*` and their PowerShell entries. Tests:
Unreal host/Python tests, Domain state vectors and canonical JSON fixture. Documentation:
backlog, plan, architecture, gates, cleanup audit, Unreal README and this evidence. Existing
combined-scope AGENTS/skill/branch-policy changes remain included.

```powershell
. .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet build PackageBuilder.sln -c Release --no-restore
dotnet test PackageBuilder.sln -c Release --no-build --no-restore
& .\scripts\Invoke-UnrealPreviewEditorBuild.ps1
& .\scripts\Invoke-UnrealDeliveryIntegration.ps1 -Preview
& .\tools\blender\5.0.0\5.0\python\bin\python.exe -B -m unittest discover -s tests/unreal -q
& .\scripts\Test-Formatting.ps1
& .\scripts\Update-BacklogStatus.ps1
& .\scripts\Test-RepositoryBaseline.ps1
```

Suggested commit: `feat: add clean Unreal delivery and interactive preview`.
On publication approval, from the documented branch/root:

```powershell
git diff --check
git add AGENTS.md docs scripts skills/package-builder-engineering/SKILL.md src/PackageBuilder.Targets.Unreal tests workers/unreal
git commit -m "feat: add clean Unreal delivery and interactive preview"
git push -u origin feat/PB-1113-PB-1114-PB-1116-unreal-package-preview
```

Merge only after reviewing the validated change, then verify main CI and follow the documented
user-confirmation/next-branch rollover. No completion-log row is added for these tasks yet.
