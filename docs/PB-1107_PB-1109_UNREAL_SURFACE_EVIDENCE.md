# PB-1107–PB-1109 — Unreal ORM, materials and static meshes

Date: 2026-09-16. Branch: `feat/PB-1107-PB-1109-unreal-materials-meshes`.
Base: freshly fetched/pulled clean main `e1616b807c7e40e0a537fc1abdfe3b8b7eceadba`.
The pending conventional-prefix edit was backed up, removed from main after backup verification,
and restored on this branch. After local validation, the user explicitly authorized commit,
push and merge on 2026-09-16; staging the reviewed scope is included in that publication.
PB-1104–PB-1106 roll to DONE once, backed by task `8a7647f`, merge `e1616b8`, successful
[main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/35103041229)
and user acceptance and selection of these successor tasks.

## Behavior and boundaries

- ORM takes each separate linear map's red channel: AO → R, roughness → G, metallic → B;
  missing AO and output alpha are 255. Matching dimensions are required; no resampling,
  gamma conversion or source mutation. The existing bounded WIC codec handles PNG/JPEG input
  and lossless PNG output: at most 8,388,608 pixels and 32,000,000 encoded input bytes.
- `UnrealMaterialEntry` projects canonical Domain intent into a companion `unreal-surfaces-v1`
  plan. Native `M_` masters and `MI_` instances support base colour, normal strength, ORM and AO
  strength, emission colour/intensity, UV transforms, opaque/masked/translucent modes, opacity
  and two-sided rendering. Displacement is explicitly unsupported and rejected before mutation.
- Static normalized FBX produces one `SM_`, converts source axes/units once, preserves imported
  normals/tangents and source LOD0, and replaces every material slot with its planned instance.
  Named source slots must match exactly before assignment; Nanite is disabled and LOD group is None.
  Expected centimetre dimensions detect scale/axis errors. Collision is `none` (NoCollision),
  `box` or `complex-as-simple` (BlockAll). NoCollision also uses empty simple geometry for
  complex queries, so consuming components cannot inadvertently enable triangle collision. Complex-as-simple is for static, not simulated bodies.
- Whole-plan/source/destination checks precede writes. Texture settings are independently
  verified, overwrite is rejected and exact asset hashes are returned. Fresh-process verification
  never repairs settings; failure invalidates the clone and never promotes output. Limits are
  128 materials, 128 meshes, 64 slots per mesh and 256 MiB per FBX, with bounded finite parameters.
- Test rendering under `scripts/` is an acceptance fixture, not PB-1110/PB-1111's product preview.
  Final Unreal releases, rigged/animated imports and global engine approval remain later work.

## Acceptance mapping

| Criterion / requirement | Automated evidence |
| --- | --- |
| PB-1107 exact channels, optional AO, alpha, no gamma | `UnrealSurfaceTests.EveryOrmPixelSurvivesPngRoundtripWithoutGammaOrChannelChanges`; native masks import |
| PB-1107 mismatched/malformed/oversized data | `OrmRejectsMismatchedDimensionsAndMalformedRaster`; live source-hash preservation |
| PB-1108 canonical intent / ENG-003 | `CanonicalMaterialsPreserveModesAndReferences`, `SurfacePlanRejectsBadBindingsAndCollisionPolicies` |
| PB-1108 graph edges, persistence, instances | `EnginePolicyTests.test_failed_graph_edges_and_missing_nodes_fail_closed`; native `verify_material` after restart |
| PB-1108 rendered appearance | `unreal_surface_render_probe.py` lit captures and measured surface/emission/roughness/two-sided comparisons |
| PB-1109 geometry, normals, LOD, collisions | `test_import_policy_preserves_normals_and_never_auto_generates_collision`; native `verify_mesh`, three collision cases, asymmetric 100×200×300 cm fixture |
| PB-1109 material bindings | Native slot-count rejection and fresh-process verification of each instance reference |
| SEC-002 / REL-008 closed/hostile inputs | `SurfaceBoundaryTests`; existing texture/protocol containment regressions |
| REL-002 save/reopen, no overwrite, immutable source | `Invoke-UnrealSurfaceIntegration.ps1`, hashes before/after reopen and rejected duplicate import |
| Cleanup / INSTALL-009 | Shared `UnrealProjectClone`, guarded finally cleanup, per-run receipt |

## Reuse audit

Reused Domain material, emission, UV, surface-mode, texture and identity types, and the bounded
immutable `PreviewRaster`. Its historical name does not change its reusable RGBA ownership
semantics. Extended WIC decoding/encoding while retaining preview-only resolution checks.
Reused texture-plan validation and persisted-policy checks, bounded JSON, containment, protocol
events/results, project leases, signature/discovery preflight and cleanup. The fixture calls
PB-0415's normalized FBX exporter. Texture and surface harnesses share command/environment setup
in `unreal_candidate_process.py`.

Unity's metallic/smoothness packer and static importer were inspected. Their packing layout and
native APIs differ; Unreal contains only its target-specific adaptation. Cross-language input
checks are intentional trust-boundary validation; live plans are produced by .NET. Future E11
callers should reuse these adapters.

In installed 5.8.2 source, the material-instance scalar setter returns false even after success;
the adapter reads back the value instead. Commandlets do not instantiate StaticMeshEditorSubsystem;
the stateless mesh operations use its class default object when no subsystem instance exists.
API sources: Epic's [MaterialEditingLibrary](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/MaterialEditingLibrary)
[collision complexity](https://dev.epicgames.com/documentation/unreal-engine/simple-versus-complex-collision-in-unreal-engine)
and [SceneCaptureComponent2D](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/SceneCaptureComponent2D).
Actual engine runs remain the compatibility evidence.

## Validation state

All three tickets are implemented and locally validated. Publication, main CI and user
confirmation remain; their own branch correctly retains IN PROGRESS and unchecked boxes.

- Release build: zero warnings/errors. Full .NET suite: **2,796 passed, zero failed/skipped**;
  `artifacts/test-results/PB-1107/summary.json`. Final focused Unreal tests: **23 passed**.
- Unreal Python: **38 passed**; Blender regression: **156 passed**. Repository baseline:
  **36 passed**; quality/release gates: **11 passed**. Repository-owned .NET formatting,
  Ruff checks and formatting (71 Python files) pass.
- Signed Unreal **5.8.2** live receipt:
  `artifacts/PB-1107/5ca759be4d814c389a0f194e44bb2522/receipt.json`.
  Texture import, surface import, independent-process reopen and rejected duplicate import
  passed for **14 assets**. Sources and saved assets retained their exact hashes; positive
  import/reopen logs contained no warnings/errors. Geometry measured 100 × 200 × 300 cm,
  with named material slots, LOD/normal settings and all three collision policies verified.
- GPU captures passed and the opaque/cutout/transparent images were visually inspected.
  `render/render-receipt.json` beneath that run records mean absolute RGB differences:
  opaque/cutout **1.812**, opaque/transparent **1.985**, emission **6.712**,
  roughness **0.225**, metallic **4.761**, normal strength **0.426**, two-sided **14.478**
  (each must exceed 0.05). Opaque foreground covered 9,423 pixels; inside-facing geometry
  covered 65,536 pixels with two-sided rendering and zero after culling was enabled.
  Fixed exposure and waiting for async asset compilation make captures meaningful; the
  earlier black-image attempt correctly failed. Render-only changes were never saved.
- The existing live texture harness passed after sharing process setup:
  `artifacts/PB-1104/7e911c3a2480467995b15b12f71a371a/receipt.json`.
- All six surface test projects, including failed diagnostic attempts, and the texture
  regression project were removed by guarded finally cleanup. Compact logs, receipts and
  captures remain. The cleanup audit is `artifacts/PB-1107/cleanup-verified.json`.
  Automatic approval review blocked removal of 24 empty .NET scratch directories (zero
  files); see [cleanup exception](TEST_ARTIFACT_CLEANUP.md). No retry was attempted.
  Historical exceptions are unchanged.

Changed files are grouped around the C# ORM/material/surface-plan adapters and WIC codec,
the Unreal material/mesh worker modules, unit and native integration/render fixtures, shared
candidate process setup, and branch-policy checks. AGENTS, the engineering skill, backlog,
plan, architecture, quality gates, cleanup audit and worker README document the same scope.
No implementation blockers or unresolved scope decisions remain. The candidate engine is
validated for this scope; global engine approval and final Unreal release assembly are separate.

## Manual publication after validation

Suggested commit: `feat: add Unreal ORM materials and static mesh policies`.
Review and stage only this scope before publication:

```powershell
git diff --check
git status --short
git add <reviewed-files>
git commit -m "feat: add Unreal ORM materials and static mesh policies"
git push -u origin feat/PB-1107-PB-1109-unreal-materials-meshes
```

Tasks remain `[ ]` / IN PROGRESS through publication. After merge, successful main CI and user
confirmation, the next branch records each completion once through the rollover rule.
