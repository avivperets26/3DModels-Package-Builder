# PB-0610 Unity Safe Mesh Extraction Evidence

## Lifecycle

- Task: PB-0610 — Implement safe mesh extraction policy.
- Canonical branch: `feat/PB-0610-unity-mesh-assets`.
- Publication branch: `feat/PB-0609-unity-static-importer` under the explicit user-approved
  PB-0609/PB-0610/PB-0611 combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.

## Implemented Policy

`UnityMeshAssetExtractor` reads only meshes actually referenced by imported `MeshFilter` and
`SkinnedMeshRenderer` components. It requires each mesh to belong to the requested source FBX,
deduplicates shared references by Unity local file ID, and sorts by ordinal mesh name then local ID.

- One unique mesh becomes `MS_<AssetId>.asset`.
- Multiple unique meshes become `MS_<AssetId>_01.asset`, `_02.asset`, and so on.
- Existing or case-equivalent outputs block before creation.
- Every clone is verified for source inequality plus exact vertex and submesh counts.
- Partial output is deleted transactionally on every failure path.
- Generated `.asset` files are allowed only beneath `Meshes`; none are created beneath `Source`.

The returned immutable binding set maps each imported source mesh to exactly one standalone mesh so
later prefab generation can replace every reference without guessing by name.

## Real Fixture Behavior

The `StoneArch.fbx` fixture contains two rendered mesh objects with identical geometry. Unity
imports those renderers against one shared mesh identity. The extractor therefore creates exactly
one `MS_StoneArch.asset`, and the prefab reuses it for both renderers. This is the intended safe
behavior: two object references remain, while duplicate standalone mesh data is not generated.

The integration also retries extraction against the existing output and requires the stable
`UNITY_MESH_EXTRACTION_OUTPUT_COLLISION` failure without changing the successful asset.

## Real Unity Result

- Safe extraction Editor checks: passed against the imported `StoneArch.fbx` hierarchy.
- Unique referenced meshes: one shared Unity mesh for two rendered objects.
- Standalone outputs: exactly `Assets/PBModelTests/Meshes/MS_StoneArch.asset`.
- Source-folder `.asset` outputs: zero.
- Populated-project clean reopen: passed.
- Retained evidence: `artifacts/u/59282008`.
- Repository baseline: 32/32 passed across 764 tracked paths.
- Final Core CI: all nine stages and 2,282/2,282 tests passed.

## Remaining Gates

- User-controlled publication, successful required `main` CI, explicit confirmation, and
  successor-task rollover.
