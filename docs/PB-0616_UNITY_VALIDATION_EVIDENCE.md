# PB-0616 Unity Logs, References, and Console Validation Evidence

## Lifecycle

- Task: PB-0616 — Implement Unity logs, reference, and console validator.
- Canonical branch: `feat/PB-0616-unity-validation`.
- Publication branch: `feat/PB-0615-unitypackage-export` under the approved combined cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-08.

## Implemented Blocking Validation

`UnityPackageValidator` returns an immutable, deterministic report and never repairs content. It
compares the actual product inventory with PB-0615's exact plan, checks canonical folder placement,
requires `.meta` files and unique GUIDs, resolves serialized GUID references, audits Unity
dependencies, and opens product prefabs and scenes to inspect their loaded object graphs.

Release is blocked for:

- Missing MonoBehaviour scripts, renderer materials, or material textures.
- Broken serialized GUID references or duplicate GUIDs.
- Case-insensitive duplicate paths, unexpected/missing export entries, or files in incorrect
  product folders.
- Any `Assets` dependency outside the product root.
- Compilation failure.
- Package-caused Warning, Error, Exception, or Assert console entries.

Findings use stable `UNITY_VALIDATION_*` codes, deterministic ordinal ordering, a logical asset
reference, and a blocking flag. No absolute local path or untrusted console text is exposed in a
finding.

## Real Negative Fixtures

Unity 6000.3.10f1 proved the validator blocks each representative failure without crashing or
mutating the source product:

- A saved prefab whose renderer material was removed.
- A material whose texture GUID was replaced by a nonexistent GUID.
- A scene whose controller-script GUID was replaced, producing both broken-GUID and missing-script
  findings.
- A duplicate planned asset path.
- A product file outside its required canonical folder.
- An explicit compilation-failure signal.
- Package-caused Warning and Error log entries.

All temporary invalid assets were removed in `finally`, followed by synchronous `AssetDatabase`
refresh. The final clean product validated, exported, entered/exited Play mode, and reopened without
a package-caused warning or error.

## Validation Results

- Unity product policy validator: 23/23 passed.
- Unity worker package validator: 9/9 passed.
- Real Unity logs/reference/GUID/path integration: passed.
- Real Unity exact archive validation: passed.
- Real Unity Play mode smoke test: passed.
- Populated-project clean reopen: passed.
- Repository baseline: 32/32 passed.
- Full Core CI: all 9 stages passed in 8 minutes 47.2 seconds.
- Release build: 18 projects, 0 warnings, 0 errors.
- Complete automated .NET suite: 2,282 passed, 0 failed, 0 skipped.
- Locked restore, .NET formatting, Ruff lint/formatting, security, history, and diff checks: passed.
- Corrected retained ignored evidence after the manual framing regression cycle:
  `artifacts/u/a7c04c07`.

## Remaining Gates

The combined PB-0615/PB-0616 change still requires user-controlled commit/push/merge, successful
required `main` CI, explicit completion confirmation, and next-task rollover.
