# PB-0704 Unity Rigged-No-Animation Prefab Evidence

## Lifecycle

- Task: PB-0704 — Implement rigged-no-animation prefab flow.
- Canonical branch: `feat/PB-0704-unity-rigged-no-animation`.
- Publication branch: `feat/PB-0703-unity-skin-validator` under the approved combined exception.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-09.

## Case-2 Output Contract

`UnityRiggedPrefabGenerator` consumes one imported Generic rig and creates:

- `Prefabs/P_<AssetId>.prefab` with a reset `P_<AssetId>` root;
- exactly one reset `P_Model` child containing the imported skeleton and skinned mesh; and
- `Documentation/SKEL_<AssetId>.json` with schema version, asset identity, Generic rig type,
  renderer count, bone count, maximum influences, and `hasAnimationClips=false`.

The generator invokes the canonical PB-0703 validator before saving and again after reloading the
prefab. Missing scripts, invalid skin data, non-reset transforms, unexpected hierarchy, or failed
metadata import block the operation and remove partial outputs.

Case 2 deliberately creates no `Animations` or `Controllers` folder, no AnimationClip or
AnimatorController assets, and no empty Animation or controller-less Animator component. This
keeps rig-only packages truthful and prevents empty animation UI in customer projects.

## Real-Engine Validation

The retained successful integration under `artifacts/u/7650ee0/p` contains:

- `Assets/PBRigPolicyTests/Prefabs/P_RiggedProp.prefab`;
- `Assets/PBRigPolicyTests/Documentation/SKEL_RiggedProp.json`; and
- no case-2 Animations or Controllers output.

Unity reopened the generated project, loaded and revalidated the prefab, completed Play mode
without errors, exported the exact package, and cleanly reimported it into the isolated fresh
project under `artifacts/u/7650ee0/r`.

## Validation Checkpoint

- Positive real skinned-prefab and metadata flow: passed.
- Invalid-skin, occupied-output, unsafe-reference, and partial-output cleanup assertions: passed.
- Case-2 no-clip/no-controller/no-empty-component assertions: passed.
- Unity product-policy validator: 31/31 passed.
- Full real Unity product integration: passed in 309.3 seconds.
- `git diff --check`: passed before documentation synchronization.

## Remaining Gates

PB-0704 remains PROCESS until the combined change is committed, pushed, merged into and pushed on
`main`, required `main` CI succeeds, the user explicitly confirms completion, and rollover records
independent completion evidence.
