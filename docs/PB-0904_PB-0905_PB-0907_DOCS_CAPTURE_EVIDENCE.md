# PB-0904 / PB-0905 / PB-0907 — Case documentation and Unity still capture

## Scope and lifecycle

The user approved all three tasks on `codex/PB-0904-PB-0905-PB-0907-docs-capture`.
Fresh-main checks (fetch, main checkout, fast-forward pull, clean equality) preceded branch
creation at `a7b242bce35285f65d4f83dfffdc6879b1b93b9d`. PR #93/main CI succeeded and the user
confirmed its merge, so PB-0901–PB-0903 were rolled to DONE exactly once in this branch.
These three new tasks remain IN PROGRESS until publication and completion gates pass.
Counts: 264 total, 127 DONE, 3 IN PROGRESS, 0 BLOCKED, 134 BACKLOG, 137 remaining (48.1%).

## Acceptance and tests

| Task | Implemented behavior | Evidence |
| --- | --- | --- |
| PB-0904 | Static, rigged, animated, item-set and collection sections; rig identity/reference pose, target-specific animation usage, assembly membership/slots/declared compatibility; no unrelated headings | `MeasuredReadmeTests`: all five cases, absent sections, culture, assembly declarations and no invented rig support |
| PB-0905 | Ordered animation and item tables; inspected frame range/FPS/duration/loop/root motion; delivered-file identity, metre dimensions and triangle/material/texture counts | Explicit inspected values differ from planned manifest frames; single negative source frame has zero duration; exact coverage, duplicates, foreign/missing files, invalid geometry, shared-resource totals and hostile table data |
| PB-0907 | Hero perspective and front/back/left/right orthographic captures at 1920×1080; final materials, studio background/light, no overlays; hash receipts; state/resource/output cleanup | Real GPU integration `artifacts/u/83092df2`; all five PNGs decoded and measured, repeated hashes match, green final material and no magenta helper pixels; visual contact-sheet review; collision/traversal/null/missing-material and allocated-resource failure checks |

## API and reuse

`SharedReadmeGenerator.Generate(manifest, publisher, build, itemMeasurements,
animationMeasurements, cancellationToken)` accepts per-item `ReadmeItemMeasurements` and inspected
`AnimationDefinition` values. Adapters must supply measurements of delivered output, not proposed
manifest frames. Each clip name/rig and item ID/file must match the declared delivery. Tables use
declaration order and invariant numbers; shared resources are not summed into inaccurate totals.
Omitting required measured rows fails with a stable `DOC_*` error. Existing overloads remain;
the legacy portable request-based generator remains compatible for its existing callers.

The shared Scriban renderer, primitive-only projection, Unicode validation, escaping, strict
UTF-8/no-BOM/LF, cancellation and publisher sections are reused. Domain retains clip duration,
rig/slot/identity validation. No new package dependency or template execution surface was added.
Animation/item inputs are capped at 1024 rows and the template loop budget accommodates tables.

`UnityStillImageCapture.TryCapture(previewController, outputName, out receipts, out code)` runs
on the Editor main thread outside render callbacks in an isolated, settled overview project.
It captures the currently visible product and pose, creates a new
`PackageBuilderCaptures/capture-<outputName>` directory, and returns role, relative file, dimensions
and SHA-256 per image. Output names are bounded lowercase alphanumerics/hyphens; pre-existing
output and linked ancestors are rejected. No product transform or final material is replaced.

It reuses `PackageBuilderPreviewController.TryGetProductBounds`, the existing overview scene,
background assets and lights, plus the pinned URP
[single-camera render request API](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/User-Render-Requests.html).
Runtime preview assets remain independent of the Editor package. A temporary camera avoids
altering the interactive camera. Hidden helpers/canvases, controls and background transforms are
restored in `finally`; textures/camera are destroyed and partial owned output is removed.

Intentional platform duplication: Unity cannot load the net10 Domain assembly, so its float
FOV/padding/view roles have a Domain conformance check in `Test-UnityWorkerPackage.ps1`.
Camera-space AABB projection is an Editor implementation for fixed still views (including
orthographic fitting); the runtime controller retains interactive orbit framing. Future capture
adapters must preserve the Domain contract. No additional shared abstraction is justified yet.

## Validation and limits

- Real Unity 6000.3.10f1 / pinned URP 17.3.0 integration passed. The FBX fixture contains simple
  authored test geometry; the contact sheet is a technical rendering check, not marketing artwork.
- Repeated hashes establish repeatability on this fixed engine/GPU/scene. They do not establish
  bit-identical rendering across GPUs, engine versions, time-dependent shaders or changing poses.
- PB-0908 owns the general framing/exposure/empty-image finding engine; PB-0909 owns optimization
  and marketplace limits. This scope does not add those later gates or a new end-user UI.
- Locked restore and Release build passed with zero warnings/errors. All 2,487 tests passed
  (zero failures/skips), including ten new case/table checks; the test runner verified no source
  modifications. Evidence: `artifacts/test-results/PB-0904/summary.json` and per-project TRX files.
- Full solution format verification (info severity), Ruff lint and Ruff formatting passed.
  The worker package's ten checks, including Domain presentation conformance, passed.
- Repository baseline: all 35 checks passed after updating both exact Unity file inventories.
  Evidence: `artifacts/PB-0904-baseline-final.log`. This includes lifecycle/status accounting,
  template/worker policies, cleanup regressions, public-content scans and Git integrity.
- Automatic cleanup removed 1,606,341,896 bytes from the Unity run, including its clone and all
  full-resolution images. Only compact evidence remains; no generated package was retained.

## Reproduction and manual publication

```powershell
Set-Location C:\Dev\PackageBuilder
. .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet restore PackageBuilder.sln --locked-mode
dotnet build PackageBuilder.sln --configuration Release --no-restore
dotnet format PackageBuilder.sln --exclude third_party --no-restore --verify-no-changes --severity info
& .\scripts\Test-RepositoryBaseline.ps1
& .\scripts\Test-BaselineUnitTests.ps1 -Configuration Release -NoRestore -NoBuild -ResultSetName PB-0904 -VerifyNoSourceChanges
& .\scripts\Invoke-UnityStillImageCaptureIntegration.ps1
```

At the initial handoff this scope was uncommitted. On 2026-09-11 the user explicitly authorized
commit, push, merge and synchronization of main. Publication uses the validated scope below;
its resulting commit, merge and main CI evidence will be recorded at the next task rollover.
Commit message:
`feat: add case documentation tables and Unity still capture`.

After review, the user can publish the combined scope:

```powershell
git diff --check
git status --short
git add AGENTS.md docs scripts skills/package-builder-engineering src/PackageBuilder.Application/Documentation src/PackageBuilder.Targets.Portable/PortableReadmeGenerator.cs tests/PackageBuilder.Targets.Portable.Tests/MeasuredReadmeTests.cs engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor
git diff --cached --stat
git diff --cached
git commit -m "feat: add case documentation tables and Unity still capture"
git push -u origin codex/PB-0904-PB-0905-PB-0907-docs-capture
```

Inspect the staged diff and include only this scope. Merge/main CI/user confirmation remain
required; record DONE and one Completion Log row per task at the next branch rollover.

## Reviewable file inventory

- `AGENTS.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/Package_Builder_Plan.md`
- `docs/PB-0901_PB-0903_DOCUMENTATION_EVIDENCE.md`
- `docs/PB-0904_PB-0905_PB-0907_DOCS_CAPTURE_EVIDENCE.md`
- `docs/QUALITY_AND_RELEASE_GATES.md`
- `docs/README.md`
- `docs/TECH_STACK_AND_ARCHITECTURE.md`
- `docs/TEST_ARTIFACT_CLEANUP.md`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/PackageBuilder.UnityWorker.Editor.asmdef`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityStillImageCapture.cs`
- `engine-templates/unity/6000.3/Packages/com.packagebuilder.worker/Editor/UnityStillImageCaptureIntegration.cs`
- `scripts/Invoke-UnityStillImageCaptureIntegration.ps1`
- `scripts/TaskBranchPolicy.Common.ps1`
- `scripts/Test-RepositoryBaseline.ps1`
- `scripts/Test-UnityWorkerPackage.ps1`
- `scripts/Test-UnityProjectTemplate.ps1`
- `skills/package-builder-engineering/SKILL.md`
- `src/PackageBuilder.Application/Documentation/DocumentationTemplateEngine.cs`
- `src/PackageBuilder.Application/Documentation/ReadmeBuildData.cs`
- `src/PackageBuilder.Application/Documentation/ReadmeCaseSections.cs`
- `src/PackageBuilder.Application/Documentation/ReadmeTables.cs`
- `src/PackageBuilder.Application/Documentation/SharedReadmeGenerator.cs`
- `src/PackageBuilder.Application/Documentation/Templates/readme.sbn`
- `src/PackageBuilder.Targets.Portable/PortableReadmeGenerator.cs`
- `tests/PackageBuilder.Targets.Portable.Tests/MeasuredReadmeTests.cs`
