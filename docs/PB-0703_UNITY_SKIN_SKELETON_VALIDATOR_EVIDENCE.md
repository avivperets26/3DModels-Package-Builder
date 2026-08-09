# PB-0703 Unity Skin and Skeleton Validator Evidence

## Lifecycle

- Task: PB-0703 — Implement Unity skin and skeleton validator.
- Canonical and publication branch: `feat/PB-0703-unity-skin-validator`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-09.
- Publication is combined with PB-0704 and PB-0705 under the explicitly approved exception in the
  implementation backlog. The exception creates no precedent.

## Implemented Boundary

`UnitySkinSkeletonValidator` inspects an imported or instantiated hierarchy without mutating it.
The report records the SkinnedMeshRenderer count, unique-bone count, highest observed influence
count, unweighted-vertex count, and stable findings ordered by code and object path.

Validation fails closed for:

- no skinned renderer or a renderer without a mesh;
- a missing root bone or root/bones outside the product hierarchy;
- missing or duplicate bone references;
- bind-pose and renderer-bone count disagreement;
- malformed per-vertex weight data or an invalid bone index;
- more influences than the approved limit; and
- vertices with no positive total weight.

The implementation reads Unity's modern `GetBonesPerVertex` and `GetAllBoneWeights` data rather
than assuming legacy four-weight arrays. It is topology-neutral and works for creatures, props,
vehicles, weapons, wings, tails, quadrupeds, and humanoid-shaped skeletons alike.

## Real-Engine Validation

The shared contained-Blender fixture now produces a real two-bone deform armature, one skinned
mesh, complete vertex groups, bind data, and an optional action. Before validation, the Unity test
reapplies the PB-0701 Generic policy with hierarchy optimization disabled so the exact bones remain
available for inspection.

The retained successful integration is beneath `artifacts/u/7650ee0`:

- manual project: `artifacts/u/7650ee0/p`;
- clean-reimport project: `artifacts/u/7650ee0/r`;
- inspected rig: `Assets/PBRigPolicyTests/Source/RiggedProp.fbx`.

The real rig passed renderer, root, bone, bind-pose, maximum-influence, and unweighted-vertex
validation. Focused negative fixtures produced stable findings for missing renderers, missing mesh,
missing/out-of-hierarchy roots and bones, bind-pose mismatches, influence-limit violations, invalid
indices, and unweighted vertices.

## Validation Checkpoint

- Unity product-policy validator: 31/31 passed.
- Real contained-Blender FBX generation: passed.
- Real Unity Edit-mode integration: passed.
- Populated-project reopen, Play mode, exact package validation, and clean package reimport: passed.
- `git diff --check`: passed before documentation synchronization.
- No production dependency was added and no source asset was modified by validation.

## Remaining Gates

PB-0703 remains PROCESS until the combined change is committed, pushed, merged into and pushed on
`main`, required `main` CI succeeds, the user explicitly confirms completion, and the next-task
rollover records it exactly once in the Completion Log.
