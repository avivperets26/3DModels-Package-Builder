# PB-0611 Unity Prefab Generation Evidence

## Lifecycle

- Task: PB-0611 — Implement Unity prefab generator.
- Canonical branch: `feat/PB-0611-unity-prefabs`.
- Publication branch: `feat/PB-0609-unity-static-importer` under the explicit user-approved
  PB-0609/PB-0610/PB-0611 combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.

## Implemented Policy

`UnityPrefabGenerator` accepts one imported static FBX, the complete PB-0610 source-to-standalone
mesh binding set, an explicit non-empty compiled-material set, and the exact
`Prefabs/P_<AssetId>.prefab` output.

The generated asset has:

- One `P_<AssetId>` product root with reset local position, rotation, and scale, matching Unity's
  required main-asset/filename identity.
- One reset direct `P_Model` child containing the imported hierarchy.
- Every MeshFilter and SkinnedMeshRenderer rebound to an exact standalone mesh from the supplied
  binding plan.
- Every renderer material present in the explicit compiled-material plan.
- No missing MonoBehaviour scripts.

The generator rejects incomplete plans and output collisions, saves through `PrefabUtility`,
reloads the asset, and re-verifies hierarchy, transforms, exact mesh/material references, and
missing-script state. A failed save or verification removes the partial prefab.

## Corrective Validation Cycle

The first real run reached prefab persistence and returned the original aggregate verification
diagnostic. The verification boundary was then made stricter and more actionable: hierarchy,
material, mesh, and missing-script failures now have separate stable diagnostics, mesh bindings
are validated before generation, and saved meshes must belong to the exact supplied binding set.
No acceptance condition was weakened.

The precise diagnostic then proved that Unity renames a prefab's main GameObject to the prefab
filename when it is saved. The policy now establishes the canonical `P_<AssetId>` root before
persistence and verifies the same identity after clean import. This aligns the hierarchy with the
required `P_<AssetId>.prefab` filename while retaining the reset product root and direct reset
`P_Model` child.

## Real Unity Result

- Static prefab Editor checks: passed.
- Generated asset: `Assets/PBModelTests/Prefabs/P_StoneArch.prefab`.
- Root: reset `P_StoneArch`; direct child: reset `P_Model`.
- Both rendered fixture objects retain the compiled material and standalone shared mesh.
- Output-collision and saved-reference checks: passed.
- Populated-project clean reopen: passed.
- Retained evidence: `artifacts/u/59282008`.
- Repository baseline: 32/32 passed across 764 tracked paths.
- Final Core CI: all nine stages and 2,282/2,282 tests passed.

## Manual Visual Check

After final integration passes, open the retained clone and inspect
`Assets/PBModelTests/Prefabs/P_StoneArch.prefab`. The Inspector/Hierarchy must show the `P_StoneArch`
root with one direct `P_Model` child, both transforms reset, two rendered fixture objects using the
compiled `M_StoneArch_URP` material, and shared `MS_StoneArch` mesh data. Scene composition and Play
mode remain owned by PB-0612 through PB-0614.

## Remaining Gates

- User-controlled publication, successful required `main` CI, explicit confirmation, and
  successor-task rollover.
