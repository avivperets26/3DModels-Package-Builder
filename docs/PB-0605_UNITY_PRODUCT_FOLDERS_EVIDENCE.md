# PB-0605 Unity Product Folder Generator Evidence

## Lifecycle

- Task: PB-0605 — Implement Unity product folder generator.
- Canonical and publication branch: `feat/PB-0605-unity-folder-generator`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.
- Publication topology: combined with PB-0606 under the explicit user-approved exception recorded
  in `docs/IMPLEMENTATION_BACKLOG.md`.

PB-0605 remains open until the user-controlled commit, push, merge, successful required `main` CI,
explicit completion confirmation, and next-task rollover are complete.

## Implemented Boundary

`UnityProductFolderGenerator` runs inside the dependency-free Editor-only Unity worker package. It
reads the already schema-validated manifest identity, independently enforces the PB-0101 publisher
and product-folder grammars at the Unity boundary, and produces one deterministic asset-root plan:

```text
Assets/<PublisherRoot>/<ProductFolder>/
```

Every supported product case receives:

- `Source`
- `Meshes`
- `Materials`
- `Textures`
- `Prefabs`
- `Documentation`
- `Scenes`
- `Scripts`

Only `rigged-animated` adds `Animations` and `Controllers`. Static, rigged-without-animation, item
set, and item collection products do not receive empty animation/controller folders. The generator
uses `AssetDatabase.CreateFolder`, so Unity owns each `.meta` file. An existing product root blocks
instead of merging with stale content, and a failed creation attempt removes every folder it
created. `_Template` is never part of the output plan.

## Validation

- Dependency-free static policy validator: 10 passed, 0 failed across PB-0605/PB-0606.
- Real Unity Editor: `6000.3.10f1` in a fresh isolated template clone.
- Product cases exercised: 5/5.
- Folder layouts created and read back through `AssetDatabase`: 5/5.
- Expected base-folder count: 8 for static, rigged, item set, and item collection.
- Expected animated-folder count: 10 for rigged-animated.
- Existing-product collision: rejected with `UNITY_PRODUCT_FOLDER_COLLISION`.
- Unsafe publisher identity: rejected before folder creation.
- `_Template` output: zero.
- Retained generated evidence: ignored beneath the legacy-safe short root `artifacts/u/<id>`.
- Tracked template opened or mutated by Unity: no.
- Final repository baseline: 32 passed, 0 failed.
- Final Core CI: all nine stages passed; 2,282 tests passed and Release build produced zero
  warnings or errors.

### Retained-clone reopen correction

The first retained clone used a 110-character project root. During manual reopening, Unity's
Mono.Cecil assembly validator reported a `DirectoryNotFoundException` for an existing
`System.Runtime.CompilerServices.Unsafe.dll` at a 272-character full path. Windows long paths were
enabled and a direct .NET file open passed, proving that the package was present and that the fault
was Unity's legacy path handling rather than incomplete package extraction.

Generated project roots now use `artifacts/u/<eight-hex-id>/p`. A reviewed worst-case path from the
pinned Unity package graph must remain at or below 248 characters before Unity starts. Automation
also launches a second clean batch-mode Unity process against the populated clone and fails on
path, assembly, or compilation diagnostics. This directly covers the manual-reopen failure mode.

### URP interactive-open correction

The corrected short clone then exposed a second manual-only defect: Unity displayed its modal URP
material-upgrade prompt. The clone contained zero `.mat` files beneath `Assets`; its 220 material
files belonged to Unity package caches. Pinned URP 17.3.0 declares ten material upgraders, while the
minimal template incorrectly recorded `m_LastMaterialVersion: 9`. The template now records 10.
Real integration validation reads the installed pinned package's upgrader declaration and requires
the cloned project marker to match it, preventing this dialog from being hidden by fast batch exits.

## Manual Visual Check

The integration script prints the exact short retained project path. That clone can be opened in
Unity and its `Assets/PBFolderTests` structure inspected in the Project window. This is a
folder/import-setting checkpoint,
not a rendered-product checkpoint; PB-0614 owns the first composed visual product scene.

Manual Unity review on 2026-08-07 passed. The corrected retained clone opened without a Console
error, package-path exception, or URP material-upgrade prompt. Screenshots confirmed all five
product-case roots, the exact eight base folders, the two animated-only folders, and no `_Template`
output.

## Remaining Gates

- User-controlled commit and branch push.
- Merge into and push of `main`.
- Successful required `main` CI.
- Explicit completion confirmation and next-task rollover synchronization.
