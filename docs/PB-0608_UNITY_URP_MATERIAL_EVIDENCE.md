# PB-0608 Unity URP/Lit Material Compiler Evidence

## Lifecycle

- Task: PB-0608 — Implement Unity URP/Lit material compiler.
- Canonical branch: `feat/PB-0608-unity-urp-material`.
- Publication branch: `feat/PB-0607-unity-metallic-smoothness` under the explicit user-approved
  PB-0607/PB-0608 combined cycle.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-07.

PB-0608 was published with PB-0607 under the approved combined exception, passed required `main`
CI, and was explicitly confirmed complete. Its final lifecycle synchronization occurs on the
PB-0609/PB-0610/PB-0611 successor branch.

## Implemented Compiler

`UnityUrpLitMaterialCompiler` creates new `Universal Render Pipeline/Lit` material assets from
validated factors, surface intent, culling intent, and resolved Unity texture references.

| Canonical intent | URP/Lit state |
|---|---|
| Base | `_BaseMap`, `_BaseColor`, opacity in color alpha |
| Normal | `_BumpMap`, `_BumpScale`, Normal-map importer required |
| Metallic/Smoothness | `_MetallicGlossMap`, metallic workflow, metallic factor, `1 - roughness` smoothness factor, metallic-alpha channel |
| Emission | `_EmissionMap`, `_EmissionColor`, explicit baked-emissive or black GI flags |
| Ambient Occlusion | `_OcclusionMap`, `_OcclusionStrength` |
| Double sided | `_Cull = 0`; otherwise back-face culling `_Cull = 2` |

Opaque, Cutout, and Transparent are the only accepted surface modes. Opaque requires full opacity;
Cutout requires a unit-range cutoff; non-Cutout modes reject a cutoff. Output collisions, unsafe
references, missing folders, invalid numeric values, missing textures, and a non-Normal imported
normal map fail closed with stable diagnostics.

The compiler invokes the pinned URP Editor assembly's public
`BaseShaderGUI.SetMaterialKeywords(..., LitGUI.SetMaterialKeywords)` and
`BaseShaderGUI.SetupMaterialBlendMode(...)` functions before persistence. These are the same
canonicalizers used by the URP/Lit Inspector and synchronize keywords, blend factors, depth write,
render type, render queue, double-sided GI, and emission flags. A second canonicalization pass must
produce an identical state signature; that idempotence is the automated no-Inspector-Fix contract.

## Surface Validation

| Surface | Required queue/tag/keyword behavior | Result |
|---|---|---|
| Opaque | Geometry, Opaque, depth write, no alpha test | Passed |
| Cutout | AlphaTest, TransparentCutout, `_ALPHATEST_ON`, cutoff, double-sided case | Passed |
| Transparent | Transparent, Transparent tag, `_SURFACE_TYPE_TRANSPARENT`, no depth write | Passed |

Every material also passed Base, Normal, MetallicSmoothness, Emission, AO, factor, culling, and
keyword assertions. Required keywords `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_EMISSION`, and
`_OCCLUSIONMAP` were present.

## Corrective Validation Cycles

1. The first real run stopped at test compilation because the test source omitted the
   `UnityEngine.Rendering` namespace for `RenderQueue`; production compilation was unaffected.
2. The second run passed all packing checks and exposed missing `_EMISSION`. URP requires explicit
   global-illumination intent, so production now sets `BakedEmissive` or `EmissiveIsBlack` before
   canonicalization.
3. The third run passed all Editor assertions. Its immediate clean-reopen process hit a Unity
   native licensing/windowing teardown race before loading the project. Reopening the identical
   retained project in a fresh process exited 0. The harness now retains that native log, waits two
   seconds, and allows exactly one retry only for the reviewed main-thread assertion signature;
   path, assembly, or compilation failures are never retried.
4. The final fresh run passed the complete Editor suite and clean second-process reopen.

## Validation

- Dependency-free Unity product validator: 14 passed, 0 failed.
- Worker package validator: 9 passed, 0 failed.
- Unity project template validator: 8 passed, 0 failed.
- Real generated materials: Opaque, Cutout, Transparent — 3/3.
- Common map/factor/keyword assertions: passed for 3/3.
- Surface, culling, queue, tag, blend/depth, and cutoff assertions: passed for 3/3.
- URP canonicalization idempotence: passed for 3/3.
- Existing output collision: rejected.
- Populated-project clean reopen: passed.
- Retained manual project: `artifacts/u/4ee25942/p`.
- Repository baseline: 32 passed, 0 failed.
- Full Core CI: all 9 stages passed.
- Release solution build: 18 projects, 0 warnings, 0 errors.
- Complete .NET test suite: 2,282 passed, 0 failed, 0 skipped.
- .NET and Ruff formatting verification: passed.

## Manual Visual Check

Open the retained project in Unity and select the three assets in `Assets/PBMaterialTests`. Each
must show the URP/Lit Inspector with the expected maps and surface options and without an Inspector
Fix prompt. This is the first material-Inspector checkpoint; a composed lit product scene remains
owned by PB-0614.

The user completed this checkpoint on 2026-08-07 with no Console errors or Inspector Fix prompt.
Screenshot evidence confirmed the Opaque material uses front-face rendering without alpha clipping,
the Cutout material remains Opaque with alpha clipping enabled at `0.42` and both-face rendering, and
the Transparent material uses Transparent/Alpha with front-face rendering. All three displayed the
expected Base, MetallicSmoothness, Normal, Occlusion, and Emission inputs in the URP/Lit Inspector.

## Publication Evidence

- Final task commit: `dda9130cc40b20b09f355c9ffe29db4e9e9df682`.
- Pull request: [#70](https://github.com/avivperets26/3DModels-Package-Builder/pull/70).
- Merge commit: `ea3ceedf108d9f2e867a4b00c1641a5925735cf8`.
- Required exact-merge `main` CI:
  [run 31196967286](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31196967286),
  successful.
- User confirmation and completion date: 2026-08-07.
- Exception boundary: only the previously approved combined branch topology was used; no test,
  engine, quality, CI, security, documentation, or completion gate was waived.
