# PB-1110–PB-1112 — Unreal overview, capture and validation

Branch: `feat/PB-1110-PB-1112-unreal-overview-validation`.
Clean fetched/pulled main base: `8ae7175aba5eb9732728b690bcb6daf0ed0b0dc2`.
PB-1107–PB-1109 roll to DONE once using task `2b187bf`, merge `8ae7175`,
[successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/35113648602)
and the user's acceptance/selection of this successor scope. After local validation, the user
explicitly authorized commit, push and merge on 2026-09-16.

## Scope and reuse

This static-product milestone builds one native saved overview map with the product at its
unchanged transform, key/fill lights, studio floor/background, a fitted camera and optional label.
Requested hero and orthographic views use the shared presentation tokens and 1920×1080 output.
Detail poses, skeletal/set/collection behavior, the interactive preview shell, final ZIP and
global engine approval remain their separately tracked tasks.

Reuse: Domain presentation specification/defaults; texture and surface import/verification;
MaterialGraph; shared Python bounded JSON/path/protocol; exclusive UnrealProjectClone;
signed candidate invocation; normalized Blender FBX export; WIC image codec;
PreviewCaptureDecoder, PreviewImageValidator and PreviewMediaOptimizer. Native camera axes,
actor construction and package references remain Unreal-specific adapters. Python plan checks
are intentional validation at the untrusted engine boundary, not new presentation policy.

Still receipts bind image and product-only opacity coverage hashes to the same pose/camera.
Final colour uses native RGBA8 sRGB readback; product coverage uses an independent HDR opacity pass.
The adapter maps renderer-relative light intensity to 12 lux per unit at fixed manual exposure.
Raw capture success is not media/release approval: the host must run the shared media validator
and inspect native diagnostics. The project/capture log gate blocks every warning/error, including unknown attribution.
Repair separately records the exact native local-SCC deletion notice only when its path is
inside the owned content tree and a matching unreferenced-redirector deletion event exists.
Unknown repair diagnostics still block; an independent scan must prove all redirectors gone.

Redirectors are repaired using Epic's project-only `ResavePackages -fixupredirects` commandlet
inside the existing owned clone lease. Two bounded native passes resave references and then
refresh registry state/delete unreferenced redirectors; Python independently scans afterwards.
Unused assets are detected through map dependency reachability and exact registry/filesystem
inventories. They block validation and are not silently deleted from a user's project.
Source: [Epic redirector documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/asset-redirectors-in-unreal-engine).

## Acceptance mapping

| Requirement | Evidence |
| --- | --- |
| PB-1110 / ENG-003 shared intent and explicit static scope | `UnrealOverviewTests`, closed overview plan |
| PB-1110 map composition and no prior content | Native create/reopen, exact actor and package inventories |
| PB-1111 framing and source preservation | Analytic eight-corner framing tests, native saved-asset/source hashes |
| PB-1111 / PB-0906 final pixels and coverage | Five native views, hash-checked decoder and shared media validation |
| PB-1112 redirectors and unused assets | Real rename/fixup, injected unused material, dependency traversal |
| PB-1112 map and lighting | Separate-process reopen and deliberately invalid saved light |
| PB-1112 / REL-008 log gates | Warning/error fault vectors and positive native log inspection |
| SEC-002 bounded inputs | Hostile closed-plan tests, existing containment/source checks |
| INSTALL-009 cleanup | Finally disposal for each native job; read-only cleanup receipt |

## Validation state

Locally implemented and validated on Unreal 5.8.2. Successful signed-engine run:
`artifacts/PB-1110/4f7dd0ad96ba4071bf739855a9356288/receipt.json` (`passed`, `mediaPassed`
and `cleanupSucceeded` all true). Eleven native operation checks pass, including clean
fresh-process reopening, unused-content rejection, real redirector rejection/two-pass repair,
post-repair unused-content rejection, fixture cleanup, bad-light rejection and final restoration.
Nine saved native assets plus the template placeholder are unchanged by validation/rendering;
input hashes are preserved. Repair maintenance notices are recorded in `repair-diagnostics.json`;
all successful product/capture validation logs have no blocking diagnostics.

Hero, Front, Back, Left and Right each render at 1920×1080 from the saved material instance.
`previews/media-validation.json` contains no findings: coverage is 23.34% (hero) and 36%
(orthographic); maximum dark fraction is 37.05%, bright fraction is zero. Optimized gallery
size is 831,800 bytes (individual files 141,057–178,676 bytes). The five final-colour images
were visually inspected for complete framing, saved material appearance and absence of labels
or helpers. The shared media policy was not relaxed; linear-output and low-native-light
attempts correctly failed exposure checks before calibration.

Cleanup audit: `artifacts/PB-1110/cleanup-verified.json` confirms all 17 owned projects were
removed, including failed diagnostic attempts; `artifacts/ue` is empty. Only local diagnostic
logs, receipts and preview images are retained. The separate 48-empty-scratch-directory
removal rejection is recorded in `TEST_ARTIFACT_CLEANUP.md`; no generated packages remain.

Local regression: Release build has zero warnings/errors; all 2,799 .NET tests pass with
source-immutability verification. Unreal Python: 46 passed; Blender Python: 156 passed.
Repository baseline: 36 passed; quality/release gates: 11 passed. Ruff lint/format and
full .NET formatting checks pass. Logs: `logs/PB-1110-*`; .NET evidence:
`artifacts/test-results/PB-1110`.

## Publication handoff

Suggested commit: `feat: add Unreal overview previews and project validation`.
Publication and main CI remain required. Tasks stay unchecked / IN PROGRESS on this branch;
the next approved task branch records DONE once after successful publication and user acceptance.

## Changed components and manual publication commands

Implementation adds `UnrealOverviewPlan`, five native overview/capture/validation modules,
three worker operations, an owned integration runner and fault fixtures. Tests extend the existing
WPF native fixture helpers and add overview plan, camera, input-boundary and diagnostic vectors.
Plan, architecture, quality gates, worker usage, AGENTS/engineering skill, branch policy and
backlog are synchronized. Shared domain/media/import behavior is reused as documented above.

The user authorized publication after reviewing the local validation handoff. The commands below
document the task-branch publication flow; exact commit, merge and main-CI evidence is recorded
through the permanent next-branch rollover.

```powershell
Set-Location C:\Dev\PackageBuilder
git diff --check
git add AGENTS.md docs scripts skills/package-builder-engineering tests/PackageBuilder.App.Wpf.Tests tests/unreal src/PackageBuilder.Targets.Unreal/UnrealOverviewPlan.cs workers/unreal
git commit -m "feat: add Unreal overview previews and project validation"
git push -u origin feat/PB-1110-PB-1112-unreal-overview-validation
```

Merge/main push, main CI and user acceptance remain the publication gates before rollover.
## Completion rollover — 2026-09-16

Published task `a7aa8c45da4bd3c613e2e167578d1816b386553d` was merged as
`4389ff27dca342578745a339e8ff415697958d39`. Both required jobs passed in
[main CI 35131715740](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/35131715740).
The user accepted publication and selected PB-1113/PB-1114/PB-1116. Each previous task is
recorded DONE exactly once in that successor branch; prior local evidence remains historical.
