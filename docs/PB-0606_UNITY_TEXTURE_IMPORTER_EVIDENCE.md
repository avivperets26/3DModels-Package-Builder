# PB-0606 Unity TextureImporter Policy Evidence

## Lifecycle

- Task: PB-0606 — Implement Unity TextureImporter policies.
- Canonical branch: `feat/PB-0606-unity-texture-importers`.
- Publication branch: `feat/PB-0605-unity-folder-generator` under the explicit user-approved
  PB-0605/PB-0606 combined cycle.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-07.

PB-0606 completed through the combined PB-0605/PB-0606 publication and PB-0607/PB-0608 rollover.

## Publication Evidence

- Final combined task commit: `f6c12c553229b245256b519f568de7fa0772c0ad`.
- Integration: [PR #69](https://github.com/avivperets26/3DModels-Package-Builder/pull/69), merged
  as `e575365df6ee9b93648e65bea02394596ace52e6`.
- Required exact-merge `main` CI: [run 31180117662](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31180117662), successful.
- User confirmation: 2026-08-07.
- Exception: no CI or quality exception; the approved combined cycle affected branch topology only.

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

None for PB-0606.
