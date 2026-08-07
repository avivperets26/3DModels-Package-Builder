# PB-0606 Unity TextureImporter Policy Evidence

## Lifecycle

- Task: PB-0606 — Implement Unity TextureImporter policies.
- Canonical branch: `feat/PB-0606-unity-texture-importers`.
- Publication branch: `feat/PB-0605-unity-folder-generator` under the explicit user-approved
  PB-0605/PB-0606 combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.

PB-0606 remains open until the combined change is committed, pushed, merged, passes required
`main` CI, receives explicit user confirmation, and rolls over on the next task branch.

## Implemented Policy

`UnityTextureImporterPolicy` accepts only canonical separate texture roles and applies the policy
through a real `UnityEditor.TextureImporter` followed by `SaveAndReimport`.

| Canonical role | sRGB | Importer type | Alpha source | Alpha as transparency |
|---|---:|---|---|---:|
| Albedo | Yes | Default | From input | Yes |
| Emission | Yes | Default | None | No |
| Normal | No | Normal map | None | No |
| Metallic | No | Default | None | No |
| Roughness | No | Default | None | No |
| Ambient Occlusion | No | Default | None | No |
| Opacity | No | Default | From input | No |
| Height | No | Default | None | No |

The policy rejects unsafe/non-Assets references, missing/non-texture importers, unknown roles, and
ambiguous packed roles such as ORM. PB-0607 owns the later explicit metallic-smoothness packing
step; PB-0606 never silently interprets a packed source as a canonical separate map.

## Real Editor Validation

- Dependency-free static policy validator: 10 passed, 0 failed across PB-0605/PB-0606.
- Real Unity Editor: `6000.3.10f1` in a fresh isolated template clone.
- Generated real PNG inputs: 8.
- Generated dimensions: 4x4, the smallest block-compression-safe manual fixture size.
- Canonical roles applied and read back from `TextureImporter`: 8/8.
- sRGB role assertions: passed for Albedo and Emission.
- Linear data-map assertions: passed for Normal, Metallic, Roughness, Ambient Occlusion, Opacity,
  and Height.
- Normal-map type assertion: passed.
- Explicit alpha-source/transparency assertions: passed for every role.
- Ambiguous `orm` role and outside-project reference: rejected.
- Retained generated evidence: ignored beneath the legacy-safe short root `artifacts/u/<id>`.
- Populated-project reopen: validated in a second clean Unity process to catch package-cache and
  assembly-validation failures before manual inspection.
- URP project material marker: verified against the ten upgraders in pinned URP 17.3.0; the
  retained test clone contains zero project `.mat` files and must not show the older-material prompt.
- Final repository baseline: 32 passed, 0 failed.
- Final Core CI: all nine stages passed; 2,282 tests passed and Release build produced zero
  warnings or errors.

The first real run exposed an incorrect linear Emission policy before publication. Emission was
corrected to sRGB with alpha disabled, and the complete eight-role Editor suite then passed. This
corrective loop is retained as evidence that the test observes Unity's actual imported settings.

Manual review on 2026-08-07 confirmed the expected folder inventory and importer values with no
Console error. It also exposed a non-blocking Unity Inspector compression warning on the original
2x2 synthetic PNGs. The fixtures were increased to 4x4 so the manual acceptance project is clean
without weakening any colour-space, type, or alpha assertion.

The warning-free follow-up review passed. The Inspector showed 4x4 block-compressed Albedo without
the former yellow compression notice. Supplied screenshots confirmed Albedo and Emission as sRGB,
Normal as a normal map, Metallic/Roughness/Ambient Occlusion/Height as linear data, and Opacity as
linear with input alpha retained and transparency disabled.

## Manual Visual Check

The integration script prints the exact short retained project path. The imported test textures
beneath `Assets/PBTextureTests` can be selected in that clone to
inspect `sRGB (Color Texture)`, Texture Type, Alpha Source, and Alpha Is Transparency. Material appearance is not yet a
valid visual acceptance check because PB-0607/PB-0608 own channel packing and URP/Lit material
compilation.

## Remaining Gates

- User-controlled commit and combined branch push.
- Merge into and push of `main`.
- Successful required `main` CI.
- Explicit completion confirmation and next-task rollover synchronization.
