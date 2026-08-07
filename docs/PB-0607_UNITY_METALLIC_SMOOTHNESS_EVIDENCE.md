# PB-0607 Unity Metallic-Smoothness Packing Evidence

## Lifecycle

- Task: PB-0607 — Implement Unity metallic-smoothness texture packing.
- Canonical and publication branch: `feat/PB-0607-unity-metallic-smoothness`.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-07.
- Publication topology: combined with PB-0608 under the explicit user-approved exception recorded
  in `docs/IMPLEMENTATION_BACKLOG.md`.

PB-0607 remains open until the user-controlled commit, push, merge, successful required `main` CI,
explicit completion confirmation, and next-task rollover are complete.

## Implemented Policy

`UnityMetallicSmoothnessPacker` accepts separate canonical Metallic and Roughness texture assets
and creates one PNG for URP/Lit:

| Output channel | Value |
|---|---|
| Red | Metallic red |
| Green | Deterministic zero |
| Blue | Deterministic zero |
| Alpha | `255 - Roughness red` |

Both source textures must be safe `Assets/` references, imported as linear Default textures, and
have identical width and height. Source/output reference collisions, existing output assets,
missing output folders, missing/non-texture inputs, incorrect source policy, and dimension mismatch
fail with stable diagnostic codes.

The packer temporarily requests readable, uncompressed Unity imports to avoid sampling lossy
platform previews. It captures the original readability, compression, and crunch settings and
restores both source importers in `finally`, including failure paths. The generated texture imports
as linear data, retains source alpha, disables alpha-as-transparency, and is not left CPU-readable.

## Real Unity Validation

- Unity Editor: official `6000.3.10f1` with pinned URP `17.3.0`.
- Exact generated input dimensions: 4x4.
- Exact packed pixels checked: 16/16.
- Metallic red preservation: 16/16.
- Inverted roughness alpha: 16/16.
- Deterministic zero green/blue: 32/32 channel assertions.
- Source importer restoration: passed for both inputs.
- Output linear/alpha/non-readable policy: passed.
- Existing-output collision: rejected.
- 4x4 versus 8x4 dimension mismatch: rejected with
  `UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH`.
- Fresh isolated-clone integration and second-process reopen: passed.
- Retained manual project: ignored under `artifacts/u/4ee25942/p`.
- Repository baseline: 32 passed, 0 failed.
- Full Core CI: all 9 stages passed.
- Release solution build: 18 projects, 0 warnings, 0 errors.
- Complete .NET test suite: 2,282 passed, 0 failed, 0 skipped.
- .NET and Ruff formatting verification: passed.

The first real execution after implementation reached and passed all packing checks before a
separate PB-0608 emission-keyword assertion failed. The final fresh execution passed the complete
combined Editor suite and clean reopen.

## Manual Visual Check

Open `artifacts/u/4ee25942/p` with Unity `6000.3.10f1`. Select
`Assets/PBTextureTests/metallic-smoothness.png`; it must be a linear Default texture with input alpha
retained and no alpha transparency. The Project view also contains the separate source maps used by
the exact pixel test. Visual colour alone is not proof of channel values; the automated PNG-byte
assertions remain authoritative.

The user completed this checkpoint on 2026-08-07. Unity opened with no Console errors, the generated
4x4 texture displayed as RGBA UNorm, sRGB was disabled, input alpha was retained, alpha transparency
was disabled, and no compression warning was present. The screenshot evidence agrees with the
automated exact-channel assertions.

## Remaining Gates

- User-controlled commit and combined branch push.
- Merge into and push of `main`.
- Successful required `main` CI.
- Explicit completion confirmation and next-task rollover synchronization.
