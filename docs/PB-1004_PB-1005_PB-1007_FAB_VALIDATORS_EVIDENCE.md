# PB-1004 / PB-1005 / PB-1007 — Fab artifact validators

## Scope and lifecycle

All three tasks are DONE, rolled over at the start of PB-1008–PB-1010 after task commit
`2113870e7cb3a9af3d6225040caaeb99aadad1a2`, main merge `491efe6175d2e0858146ba9701bfea70e297ab60`,
[successful main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/34892739991)
and user confirmation. The implementation used the user-approved combined branch
`codex/PB-1004-PB-1005-PB-1007-fab-validators`, created from freshly fetched/pulled clean main
`7bbfc38bf4abddf56807881e953e7ce349de9155` on 2026-09-14. PB-0016 rolls to DONE once here,
using its task/main commits, successful main CI and the user's acceptance of the next scope.
The publication-pending handoff below is historical and is preserved for traceability.

## Acceptance and boundaries

| Task | Implementation | Acceptance checks |
| --- | --- | --- |
| PB-1004 | `FabPortableValidator.ValidateAsync` | Profile-selected primary formats, one per selected format, additional-file count/bytes, relevance against exact manifest file set, safe ZIP structure/names, before/after artifact hashes, upstream portable findings |
| PB-1005 | `FabUnityPackageValidator.Validate` | Exact product root and planned/imported inventory, package bytes, path length, duplicate payloads/paths, unused/tool/generated assets, nested content, dependency resolution/disclosure, overview scene, offline docs, bound package/clean-import findings |
| PB-1007 | `FabMediaGalleryValidator.Validate` | Final PNG/JPEG decode and extension, artifact hash, measured dimensions, strict per-image/gallery byte limits, designated thumbnail, per-item coverage, shared product-specific required views |

These APIs belong to the Fab adapter. No UI controls, upload, release composer or Unreal
validator is included. PB-1009 owns final composition. This is artifact-check evidence, not
certification that every current marketplace clause is implemented or that Fab accepted a listing.

## Profile and source review

The original `2026-09-12.1` profile and its bytes stay available. New explicit candidate
`FabRequirementsBaseline.ArtifactValidationProfile` loads `2026-09-14.1`, adding a measured
Unity path-length rule and explicit generated-delivery policies. It never changes a cached
approved profile or build.lock automatically: import, compare, test and approve through the
existing updater. Validator results retain the exact selected profile SHA-256.

Reviewed 2026-09-14 against [Fab file requirements](https://dev.epicgames.com/documentation/en-us/fab/asset-file-format-and-structure-requirements-in-fab),
[Unity submission guidelines](https://assetstore.unity.com/publishing/submission-guidelines) and
[Fab listing guidance](https://dev.epicgames.com/documentation/en-us/fab/publishing-assets-for-sale-or-free-download-in-fab).
The technical-page review date remains 2026-09-12. Its unresolved full-review clause and the
ambiguous published exchange-format size remain explicit release-blocking review findings.
Tests use labelled synthetic resolved profiles to exercise acceptance paths; those fixtures
are not official clarifications, approval receipts or a silently promoted production profile.

Generated naming uses a conservative safe ASCII subset and strict planned roots. Nested source
archives, special-folder exceptions, UPM packages and video/3D-only galleries are outside this
generated-output capability. The stricter local policies are labelled `package-builder-policy`.
The thumbnail points to a gallery image and is counted once. Shared view policy requires a hero,
animation pose for animated products, and the appropriate set/collection overview; multi-item
overview evidence must cover every manifest item. Image inspection accepts larger dimensions
within the existing 8,388,608-pixel / 32 MB decoder safety budget. Capture/optimizer Decode still
requires its original fixed 1920×1080 raster; this behavior is not broadened.

## Trust and reuse audit

`FabTargetEvidence` is an in-process trusted orchestration contract, not supplier-authored JSON.
The caller must run existing target validation and preserve its complete findings. Job, logical
artifact, product, content SHA-256/length and completed stage must match. A missing/default or
wrong-stage result blocks; a producer's success flag alone cannot replace the evidence record.
Unity's complete post-import AssetDatabase inventory, dependency observations and usage flags
come from the trusted worker/clean-import stage; this pure validator does not execute Editor APIs
or authenticate arbitrary external claims. The expected file set comes from the manifest/build
plan, independently of actual inspection. Callers must keep artifact-store inputs frozen during
validation and revalidate after mutation; findings do not authorize later bytes with another hash.

Portable inspection reuses `ISafeArchiveService` and `IArtifactHashService`, including containment,
reparse-point, archive-bomb, extension and collision protections. It never extracts the ZIP.
It requires the existing portable-target result and compares actual ZIP names with the plan.
Unity engine GUID/reference/material/log checks stay in the existing `UnityPackageValidator`
and clean-reimport implementation; Fab consumes their bound findings instead of copying them.
`FabValidation` centralizes rule bounds, missing-rule review and report-compatible findings.
`FabContentPath` uses the existing naming value validator. The existing presentation specification
exposes its required/allowed-view rules for reuse. `WindowsPreviewImageCodec` implements the new
neutral `IEncodedImageInspector` contract through the same bounded WIC decoder. No new library,
parallel pixel codec, engine-specific marketplace rule or new test project is introduced.

## Validation and cleanup

The complete Release suite passed **2,734/2,734 tests**, with zero failed or skipped tests,
including **62 new cases**. Tests cover real contained ZIPs and real WIC PNG/JPEG decoding,
hostile inputs, missing/stale/cross-job evidence, limits, dependency disclosure and view coverage.
Unity observations in these adapter tests are synthetic; no fresh Unity export/import execution
is claimed. Existing engine tests remain the authority for engine behavior. Compact logs/TRX stay
under ignored `artifacts/PB-1004`; the deterministic full-suite summary is
`artifacts/test-results/PB-1004/summary.json`. Source stability verification passed during that run.
Locked restore succeeded, the Release solution build completed with zero warnings/errors, and
the repository baseline passed **35/35 checks**, including its nine artifact-cleanup cases.
The CI-equivalent .NET formatting verification (excluding vendored `third_party` sources),
Ruff lint and Ruff formatting all passed; Ruff checked 51 formatted files. The final test-constant
naming correction was recompiled and its targeted path-limit test passed (1/1); no test behavior
changed after the complete suite. Git whitespace and recognizable secret-pattern checks passed.

ZIP test workspaces use constructor-failure cleanup and disposal beneath the test output PB-1004
root. Image tests use memory only. No large engine clones or real asset packages are generated.
The post-test receipt `artifacts/PB-1004/cleanup-verified.json` confirms zero remaining PB-1004
run directories and zero files. The baseline's `artifacts/u/638666ca` disposable outputs were
removed while compact evidence was preserved.
The prior 15 empty PB-1001 directories remain the recorded automatic-review exception; no blocked
removal is retried. See [cleanup audit](TEST_ARTIFACT_CLEANUP.md).

## Handoff

Changed files cover the three validators and shared Fab contracts, a neutral image inspection
contract and the reused WPF decoder, shared preview-view policy accessors, the new profile resource,
focused tests, branch rules/AGENTS/engineering skill and documentation/status evidence.
Suggested commit: `feat: validate Fab portable Unity and media deliveries`.

On 2026-09-14 the user explicitly authorized the commit, push and merge flow. The verified
Git credential identity and target owner are `avivperets26`; the repository is the approved
public `avivperets26/3DModels-Package-Builder`. Exact task/main commits and main CI results
are recorded after publication in ignored `artifacts/PB-1004-publication.json` and carried
into the next branch's completion rollover. No completion-only commit is made here.
The original handoff commands below are retained as a reference, not proof of publication:

```powershell
Set-Location C:\Dev\PackageBuilder
git status --short
git diff --check
# Stage only the reviewed files shown by git status.
git commit -m "feat: validate Fab portable Unity and media deliveries"
git push -u origin codex/PB-1004-PB-1005-PB-1007-fab-validators
```

Keep all three tasks unchecked until their publication, main CI, confirmation and rollover gates pass.
