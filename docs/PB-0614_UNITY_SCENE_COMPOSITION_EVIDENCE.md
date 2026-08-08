# PB-0614 Unity Scene Composition Evidence

## Lifecycle

- Task: PB-0614 — Instantiate product into Unity overview scene.
- Canonical branch: `feat/PB-0614-unity-scene-composition`.
- Publication branch: `feat/PB-0612-unity-overview-scene` under the approved combined cycle.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-07.

## Implemented Composition

`UnityOverviewSceneComposer` accepts the clean PB-0612 template, one exact
`Prefabs/P_<AssetId>.prefab`, the product-local controller script, and the exact
`Scenes/S_<AssetId>_Overview.unity` destination.

The composer rejects unsafe or noncanonical references, missing folders/assets, occupied template
targets, and output collisions. It instantiates the requested prefab through `PrefabUtility`, places
it as the only direct child of `PreviewTarget`, resets the prefab-instance transform, auto-frames the
camera, saves a copy beneath the product root, reopens it, and verifies:

- Exactly one product exists beneath `PreviewTarget`.
- Product identity and prefab-source reference match the request exactly.
- Product root transform remains reset.
- Controller target, camera, and product-local script references are complete.
- No missing MonoBehaviour script exists.

## Real Unity Result

- Composed scene: `Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity`.
- Product: exactly one `P_StoneArch` prefab instance beneath `PreviewTarget`.
- Controller source: `Assets/PBModelTests/Scripts/PackageBuilderPreviewController.cs`.
- Real Play mode cycle entered and exited successfully with no package-caused error.
- Populated project clean reopen: passed.
- User manual inspection on 2026-08-08 confirmed exactly one `P_StoneArch` beneath
  `PreviewTarget`, complete controller references, correct rendered output, and no red Play mode
  Console errors.
- Full Core CI: all 9 stages passed; 18 projects built with 0 warnings and 0 errors.
- Complete automated test suite: 2,282 passed, 0 failed, 0 skipped.
- Repository baseline: 32/32 passed with all lifecycle and Completion Log checks consistent.
- Formatting, repository safety, and `git diff --check`: passed.
- Retained manual project: `artifacts/u/1c6667a0/p`.

## Publication Evidence

- Task commit: `dceb8838117c760a276dc2c1acbf0c4171af25b0`.
- Pull request: [#72](https://github.com/avivperets26/3DModels-Package-Builder/pull/72).
- Merge commit: `04de23b5204a64ad57426273991f82ce2649db40`.
- Required [main workflow run 31254437622](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31254437622): successful.
- User confirmation: 2026-08-08, including the intended single product and no red Play mode errors.
- Exception used: branch topology only; no CI, quality, or completion gate was waived.
