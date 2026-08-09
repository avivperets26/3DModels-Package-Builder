# PB-0701 Unity Generic Rig Importer Evidence

## Lifecycle

- Task: PB-0701 — Implement Unity Generic rig importer policy.
- Canonical and publication branch: `feat/PB-0701-unity-generic-rig`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-09.
- Publication is combined with PB-0702 under the explicitly approved exception recorded in the
  implementation backlog. The exception creates no precedent.

## Implemented Policy

`UnityRigModelImporterPolicy` accepts one manifest-owned request and applies a deterministic
Generic rig transaction to a product FBX beneath its `Source` folder. The policy:

- selects `Generic` explicitly rather than inferring Humanoid from skeleton presence;
- creates the Avatar from the imported model and clears any source Avatar;
- requires an existing, exact root-node path;
- applies preserve-hierarchy and optimize-game-object choices explicitly;
- validates every exposed transform against Unity's imported transform paths;
- sorts exposed paths ordinally before persistence;
- disables animation import because clip ownership remains with PB-0703 through PB-0706;
- saves, reimports, reloads, and verifies every reviewed setting; and
- restores the importer snapshot when a mutation fails.

Unsafe asset references, missing importers, nonexistent roots, duplicate or unknown exposed paths,
and Generic requests carrying Humanoid-only options fail closed with stable diagnostics.

Generic is intentionally a topology-neutral contract, not a synonym for a human-shaped skeleton.
The production policy does not inspect limb count or require human bone names, so articulated props
and vehicles, quadrupeds, winged creatures, and non-humanoid bipeds with tails follow this same
import boundary. PB-0714 owns the redistribution-safe topology fixture matrix and deformation/reimport
proof; PB-1214 applies the equivalent matrix to Unreal skeletal import.

## Real Rig Fixture and Editor Validation

The project-contained Blender generator
`tests/blender/engine/pb0701_generate_rig_fbx.py` creates a minimal real FBX with:

- one two-bone armature (`Root` and `Tip`);
- both bones marked for deformation;
- one skinned mesh with four fully weighted vertices;
- stable object, mesh, rig, bone, and vertex-group names; and
- no animation, leaf bones, camera, light, or unrelated helper.

The Unity integration harness generates this fixture with the contained Blender installation before
opening the isolated Unity clone. Unity applies the Generic request, rereads the importer, and
requires a valid non-Humanoid Avatar together with the exact root, hierarchy, optimization, exposed
transform, and animation settings.

The latest retained successful run is beneath `artifacts/u/dee856f4`. Its manual project is
`artifacts/u/dee856f4/p`, and the inspected FBX is
`Assets/PBRigPolicyTests/Source/RiggedProp.fbx`.

## Validation Checkpoint

- Real Blender FBX generation: passed; the retained fixture is nonempty and imports as an armature
  and skinned mesh.
- Real Unity Generic importer transaction and exact reread verification: passed.
- Real Unity product integration, exact package verification, clean package reimport, Play mode,
  and populated-project reopen: passed.
- Clean-reimport structured result: passed with one renderer, one material, five textures, and zero
  findings.
- Dependency-free Unity worker, template, and product-policy validators: passed before the final
  repository-wide validation cycle (9/9, 8/8, and 28/28 respectively).
- Repository baseline: 32/32 passed.
- Full nine-stage Core CI: passed in 4m 26.8s; Release built all 18 projects with zero warnings and
  zero errors, formatting passed, and all 2,282 tests passed with none failed or skipped.
- Manual visual review is optional for this importer-policy task. The retained rig FBX can be
  selected in Unity's Project window and its Rig tab inspected; animated deformation becomes a
  visual acceptance gate in PB-0711/PB-0712.
- User manual checkpoint on 2026-08-09: `Assets/PBRigPolicyTests/Source/RiggedProp.fbx` opened without
  Console errors and visibly showed Generic, Create From This Model, Standard four-bone weights,
  Strip Bones, and Optimize Game Objects as expected.
- Reuse audit: PB-0701 owns the single Unity rig-import transaction used by Generic requests and the
  explicitly approved PB-0702 fallback. No product-topology-specific importer copy was introduced.

## Remaining Gates

PB-0701 remains PROCESS until the combined task commit is published, merged into `main`, required
`main` CI succeeds, the user explicitly confirms completion, and the next task synchronizes the
Completion Log.
