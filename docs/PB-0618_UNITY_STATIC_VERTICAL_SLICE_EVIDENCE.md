# PB-0618 Unity Static Vertical Slice Evidence

## Lifecycle

- Task: PB-0618 — Complete Unity static vertical slice.
- Canonical branch: `test/PB-0618-unity-static-e2e`.
- Publication branch: `test/PB-0617-unity-clean-reimport` under the approved combined cycle.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-08.

## Implemented Composition

`Invoke-UnityStaticVerticalSlice.ps1` composes rather than duplicates the existing PB-0507 portable
slice and PB-0617 Unity integration. The same retained `StoneArch.fbx` fixture produces:

- A validated portable FBX ZIP.
- An exact product-only Unity package.
- A product overview scene.
- Portable and clean-Unity-reimport JSON reports.
- Portable JSON Lines and Unity integration logs.
- One aggregate JSON validation report with source and target artifact SHA-256 identities.

Every target must pass before the script assembles staging. The completed directory is promoted by
one same-volume directory move into `artifacts/PB-0618/releases`; no partially assembled release is
exposed at the destination.

## Validation Checkpoint

- Dependency-free static composition policy: passed as part of 26/26 Unity policy checks.
- Script parsing and repository-contained output policy: passed.
- Full retained portable-plus-Unity execution: passed.
- Retained promoted release:
  `artifacts/PB-0618/releases/run-20260808-143128-93cc3bc1/StoneArch`.
- The retained portable test passed 1/1 and produced `Portable/Stone_Arch_FBX.zip` with SHA-256
  `ed1fe3a636ff7303d15a5a6c1942a75790fe3c7ddd45f1c0f1d218f4738eddf0`.
- The Unity integration produced `Unity/StoneArch.unitypackage` with SHA-256
  `8b1ae591e9359320e5eabbb54a0732137dd85e5a8248d0d08b75a6709d24ec87` and retained the overview
  scene, clean-reimport report, portable report, and both target logs.
- The aggregate schema-version-1 report has status `passed`, zero findings, and records the shared
  source SHA-256 `9d3cc82c22918c15fa20c674b26df60d5b86a808bc7b1e9a1962eb33916fd9a9`.
- The release contains exactly eight intended files. The `latest.txt` pointer resolves to the
  promoted release, and no staging directory remains after the same-volume atomic move.
- Repository baseline: 32/32 passed. Full nine-stage Core CI passed with all 2,282 tests passing.

## Publication Completion

- Final task commit: `6d82a7d55be66f8db0d63af36070b88ebe616a3b`.
- Published on the PB-0617 branch and merged through
  [PR #74](https://github.com/avivperets26/3DModels-Package-Builder/pull/74) as
  `73261cfdb578de968d8f72aa553742deab75eff8`.
- Required exact-merge [main workflow run 31308970351](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31308970351)
  succeeded.
- The user explicitly confirmed completion on 2026-08-09. No CI or quality exception was used.
