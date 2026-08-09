# PB-0705 Unity Animation Import and Clip Evidence

## Lifecycle

- Task: PB-0705 — Implement animation import and clip extraction.
- Canonical branch: `feat/PB-0705-unity-animation-clips`.
- Publication branch: `feat/PB-0703-unity-skin-validator` under the approved combined exception.
- Status: `[x]` / 🟢 **DONE**.
- Started: 2026-08-09.

## Implemented Clip Contract

`UnityAnimationClipImporter` accepts a safe source FBX, an existing product Animations folder, and
one or more manifest clip plans. Each plan owns an exact source take, clip identifier, inclusive
source frame range, and finite positive sample rate. Plans are sorted ordinally and produce
collision-safe `A_<AssetId>_<ClipId>.anim` assets.

The importer rejects unsafe paths, missing or duplicate source takes, malformed or duplicate clip
identities, out-of-range frames, non-positive or non-finite rates, rate mismatches, and occupied
outputs. It verifies the imported clip name, sample rate, calculated duration, and persisted asset
before returning success.

PB-0701 intentionally disables animation import during rig configuration. Source-take discovery
therefore performs a narrow temporary animation-enabled reimport only when take metadata is absent,
copies the reviewed take data, and restores the complete prior animation settings in `finally`.
Failed extraction similarly restores importer state and deletes all partial clip assets.

## Real-Engine Validation

The contained Blender generator creates an optional `Bend` action on the same two-bone skinned
fixture at frames 1, 11, and 21 with 30 FPS. The retained Unity project under
`artifacts/u/7650ee0/p` contains:

- source: `Assets/PBAnimationTests/Source/AnimatedProp.fbx`;
- extracted clip: `Assets/PBAnimationTests/Animations/A_AnimatedProp_Bend.anim`.

Unity rediscovered the source take after the rig policy had disabled animation, imported the exact
manifest range and sample rate, extracted the stable clip asset, and rejected a repeated request
that would collide with the existing output.

## Validation Checkpoint

- Source-action discovery and exact manifest-range extraction: passed.
- Exact 30 FPS rate and duration verification: passed.
- Deterministic `A_` naming and occupied-output rejection: passed.
- Temporary discovery-state restoration and failed-transaction cleanup: passed.
- Unity product-policy validator: 31/31 passed.
- Full real Unity product integration: passed in 309.3 seconds.
- Populated reopen, Play mode, package validation, and clean reimport: passed.
- `git diff --check`: passed before documentation synchronization.

## Publication Evidence

- Task commit: `03ec3ee6e28a78975c86e8352b26ee02a91c2a8d`.
- Pull request: [#76](https://github.com/avivperets26/3DModels-Package-Builder/pull/76).
- Merge commit: `3c58790b738d0b3998356bfa9a95742e1d72f195`.
- Required `main` CI: explicitly confirmed successful by the user on 2026-08-09.
- Exception: the approved PB-0703/PB-0704/PB-0705 combined-publication exception; no CI or quality
  exception was used, and the publication exception creates no precedent.

PB-0705 was removed from Active Work and recorded exactly once in the Completion Log during the
PB-0706 rollover.
