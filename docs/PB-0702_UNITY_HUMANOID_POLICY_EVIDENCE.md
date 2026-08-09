# PB-0702 Unity Humanoid Validation Policy Evidence

## Lifecycle

- Task: PB-0702 — Implement optional Humanoid validation policy.
- Canonical branch: `feat/PB-0702-unity-humanoid-policy`.
- Publication branch: `feat/PB-0701-unity-generic-rig` under the approved combined exception.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-09.

## Manifest-Only Humanoid Boundary

Humanoid import is never inferred. `UnityRigModelImporterPolicy` accepts it only when the request
contains all of the following:

- explicit Humanoid mode;
- explicit manifest Humanoid opt-in;
- at least one mapping whose Unity human-bone name is known to `HumanTrait`;
- unique human and source bone identities;
- source bone names that resolve exactly once in the imported hierarchy; and
- a safe model, root, and exposed-transform plan.

This is an optional human-retargeting adapter only. Dragons, dogs and other quadrupeds, winged
creatures, bows, vehicles, and non-humanoid bipeds with tails remain Generic in Unity and do not
need, receive, or synthesize a Humanoid mapping.

The policy builds a `HumanDescription`, imports as Human, then loads the single generated Avatar
from the FBX sub-assets. Success requires both `Avatar.isValid` and `Avatar.isHuman`; importer state
alone is insufficient.

## Failure and Fallback Semantics

An invalid Humanoid Avatar restores the prior importer state and returns
`UNITY_HUMANOID_AVATAR_INVALID`. It does not silently downgrade. Generic fallback occurs only when
the same request carries separate `ApproveGenericFallback` approval. The fallback then runs the
complete PB-0701 Generic transaction and reports both requested and applied modes plus
`UsedApprovedFallback=true`.

The real two-bone fixture intentionally cannot form a valid Humanoid Avatar. Editor integration
proves three distinct outcomes:

1. Humanoid without manifest opt-in is rejected before mutation.
2. Manifest-selected Humanoid with an incomplete mapping fails and restores the source state.
3. The same invalid Humanoid request becomes Generic only after explicit fallback approval.

## Compatibility Correction

The first real Unity 6 compilation attempt exposed an unsupported assumption that generated Avatars
were readable from `ModelImporter.avatar`. Unity 6 exposes them as imported FBX sub-assets. The
implementation was corrected to use `AssetDatabase.LoadAllAssetsAtPath`, require exactly one
Avatar, and verify its validity and human classification. Production behavior was not weakened.

## Validation Checkpoint

- Manifest opt-in, mapping, duplicate, source-resolution, and Generic/Humanoid option-separation
  policy assertions: passed.
- Real Unity invalid-Humanoid rollback and no-fallback behavior: passed.
- Real Unity explicitly approved Generic fallback and exact final importer verification: passed.
- Latest retained successful real-engine run: `artifacts/u/dee856f4`.
- Dependency-free Unity worker, template, and product-policy validators: 9/9, 8/8, and 28/28
  passed.
- Repository baseline: 32/32 passed.
- Full nine-stage Core CI: passed in 4m 26.8s; Release built all 18 projects with zero warnings and
  zero errors, formatting passed, and all 2,282 tests passed with none failed or skipped.
- Manual visual testing is not an acceptance requirement because this task validates importer and
  Avatar policy. Humanoid animation/retargeting visualization belongs to later rigged and animated
  vertical slices.
- Reuse audit: Humanoid validation extends the same importer transaction and structured-result
  boundary as Generic import; fallback calls the canonical Generic policy instead of duplicating it.

## Publication and Completion

- Task commit: `6c3a7945558317a69ee77ba38a0cc959e26a4a04`.
- Pull request: [#75](https://github.com/avivperets26/3DModels-Package-Builder/pull/75).
- Merge commit: `dc239ec565925898e5206336ea51eeff27692e92`.
- Required exact-merge [main workflow run 31312634024](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31312634024): succeeded.
- User confirmation and completion date: 2026-08-09.
- Exception used: only the approved PB-0701/PB-0702 combined publication topology; no quality,
  dependency, validation, engine, CI, or security gate was waived.
