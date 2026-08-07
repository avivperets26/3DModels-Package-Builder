# PB-0609 Unity Static ModelImporter Evidence

## Lifecycle

- Task: PB-0609 — Implement static `ModelImporter` policy.
- Canonical and publication branch: `feat/PB-0609-unity-static-importer`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.
- Publication topology: combined with PB-0610 and PB-0611 under the explicit user-approved
  exception in `docs/IMPLEMENTATION_BACKLOG.md`.

PB-0609 remains open until the user-controlled commit, push, merge, successful required `main` CI,
explicit completion confirmation, and next-task rollover are complete.

## Implemented Policy

`UnityStaticModelImporterPolicy` accepts only a safe product `Source` FBX, positive finite global
scale, explicit preserve-hierarchy intent, and a complete material-remap plan. It applies and then
reads back all of the following:

- `animationType = None` and animation import disabled.
- Camera, light, visibility, and blend-shape import disabled.
- Imported normals and calculated Mikk tangents.
- Explicit global scale and preserve-hierarchy choice.
- Material-description import with embedded-location suppression and local name search.
- A complete ordinal source-material map to existing compiled assets beneath `Materials`.

Missing or duplicate material identities, incomplete plans, invalid paths, missing importers or
materials, and post-import mismatches fail with stable diagnostics. Mutation captures the complete
reviewed importer/remap state and attempts restoration if application or verification fails.

## Validation Boundary

The retained real Unity fixture is the repository-authored `StoneArch.fbx`. Its source material
identities are deliberately enumerated in reverse order before application and reapplied in a
different order; the saved remap signature must remain identical. An incomplete plan is rejected
before mutation. The imported hierarchy must contain meshes but no Camera, Light, Animator, or
legacy Animation component, and every renderer must resolve to the compiled URP material.

The first real run exposed an incorrect test assumption about imported mesh count, not an importer
defect: Unity deduplicated identical FBX geometry into a shared mesh. The corrected assertion uses
the actual unique referenced-mesh set and thereby preserves PB-0610's no-duplicate requirement.

A later idempotency run exposed a real validation defect: after an external remap Unity renderers
report the target material name, while the `ModelImporter` remap table correctly retains the FBX
source identity. Completeness now uses imported material subassets and importer source-remap keys,
with renderer names only as a pre-remap fallback. The plan must exactly equal that authoritative
source set. The final real integration applied the plan twice in different orders and passed.

## Real Unity Result

- Unity product policy validator: 17/17 passed.
- Static `ModelImporter` Editor checks: passed against `StoneArch.fbx`.
- Deterministic material reapplication: passed.
- Populated-project clean reopen: passed.
- Retained evidence: `artifacts/u/59282008`.
- Retained manual project: `artifacts/u/59282008/p`.
- Repository baseline: 32/32 passed across 764 tracked paths.
- Final Core CI: all nine stages passed; 18-project Release build produced zero warnings/errors;
  2,282/2,282 tests passed.

An earlier Core CI attempt observed one transient failure in the unrelated PB-0206 artifact
promotion collision-bound test while its 10,000 files were created alongside the complete suite.
The unchanged test passed in isolation, the complete Infrastructure suite passed 647/647, and the
final authoritative Core CI passed 2,282/2,282. No unrelated implementation or test was changed.

## Remaining Gates

- User-controlled commit/push/merge, successful required `main` CI, explicit confirmation, and
  successor-task rollover.
