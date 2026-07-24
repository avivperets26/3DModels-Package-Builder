# PB-0105 Rig and Animation Domain Evidence

**Task:** PB-0105 — Implement rig and animation definitions
**Branch:** `feat/PB-0105-rig-animation-domain`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-07-24

## Scope

PB-0105 adds immutable renderer-independent rigging and animation intent in `PackageBuilder.Domain.Rigging` and `PackageBuilder.Domain.Animations`. It adds no FBX/GLB parsing, skin weights, animation curves, baking, compression, retargeting, root-motion extraction, rendering, visual preview, engine-specific assets, axes, units, naming prefixes, humanoid bone maps, filesystem, persistence, networking, marketplace, Blender, Unity, Unreal, or WPF behavior.

This branch also performs the approved PB-0104 rollover. PB-0104 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion Log with the supplied publication evidence. PB-0105 remains `[ ]` / 🟡 **PROCESS**, appears in Active Work, and is absent from the Completion Log.

## PB-0104 Final Publication Evidence

- Final task commit: `5d3f52c107f3de7fa5bac80d85559c80aeaad6b4`.
- Pull request: [#18](https://github.com/avivperets26/3DModels-Package-Builder/pull/18).
- Successful PR workflow: [run 30097711367](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30097711367).
- Merge commit: `1983201ea7a810aac4ca74db0351d73c5554a929`.
- Successful required main workflow: [run 30097716685](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30097716685).
- The user explicitly confirmed completion on 2026-07-24.
- No CI, completion, or quality exception was used.

## Public API

| API | Purpose |
|---|---|
| `RigType.Generic`, `.Humanoid` | Closed approved rig identities; Humanoid is never inferred from skeleton presence. |
| `BoneDefinition.Create(...)` | Validates an exact ordinal bone identity and optional parent reference. |
| `SkeletonDefinition.Create(...)` | Validates and snapshots one rooted acyclic hierarchy. |
| `RigTransform.Create(...)` | Validates finite translation/scale and canonicalizes a finite non-zero quaternion. |
| `BonePose.Create(...)` | Associates one validated bone identity with one immutable local transform. |
| `PoseDefinition.Create(...)` | Validates a complete deterministic reference/rest pose for a skeleton. |
| `RigDefinition.Create(...)` | Combines an explicit rig type, skeleton, and matching reference pose. |
| `LoopBehavior.Once`, `.Loop` | Closed playback-loop metadata. |
| `RootMotionStatus.None`, `.RootBone` | Closed root-motion metadata. |
| `AnimationDefinition.Create(...)` | Validates immutable clip name, range, FPS, loop, root motion, and rig metadata. |

Every expected invalid-input API returns a task-local structured validation result. Constructors capable of retaining invalid state are private. PB-0105 does not pre-empt PB-0109's global validation-finding or stable error-code model.

## Skeleton and Identity Invariants

- Bone identities preserve accepted Unicode, casing, and interior spaces exactly.
- Identity comparison, parent resolution, duplicate detection, equality, and hashing use deterministic ordinal case-sensitive semantics.
- Null, empty, whitespace-only, edge-whitespace, and control-character identities are rejected rather than trimmed or normalized.
- A skeleton contains exactly one root whose parent is null.
- Null bones, duplicates, self-parenting, missing/orphaned parents, missing roots, multiple roots, and direct or indirect cycles are rejected.
- Retained order is root-first depth-first; sibling order is ordinal. Input enumeration order does not affect the logical value.
- No arbitrary bone-count limit is imposed.

## Transform and Pose Invariants

- Translation and scale accept every finite signed `double`, including negative and zero values. No engine axis, unit, handedness, or positive-scale rule is imposed.
- Rotation requires four finite components and a non-zero quaternion.
- Quaternion normalization scales by the largest absolute component before measuring length, avoiding overflow or underflow for finite boundary values.
- Stored rotations are unit quaternions. Quaternion sign is canonicalized by positive W, then the first non-zero X/Y/Z component, so `q` and `-q` represent one deterministic value.
- Every numeric component rejects `NaN`, positive infinity, and negative infinity; zero-length rotation is rejected.
- A `PoseDefinition` requires exactly one known bone reference for every skeleton bone. Null, unknown, duplicate, and missing entries are rejected.
- Pose order follows skeleton order regardless of input order, and retained collections are immutable snapshots.

## Animation Invariants

- Clip names preserve accepted Unicode and casing exactly and reject null, empty, whitespace-only, edge-whitespace, and control-character inputs.
- Start and end frames are inclusive signed `long` source frames. Negative frames and the complete `long` boundary range are valid; reversed ranges are rejected.
- Inclusive frame count is `EndFrame - StartFrame + 1` and is represented as `decimal`, avoiding an arbitrary clip-length cap.
- Duration is `(EndFrame - StartFrame) / FPS`, because duration measures intervals between inclusive samples. A one-frame clip has zero duration.
- FPS must be finite and strictly positive. Metadata whose computed `double` duration is not finite is rejected.
- `Once` and `Loop` are explicit. Root motion is either `None` with no bone identity or `RootBone` with an exact ordinal reference to the validated skeleton root.
- Root motion is metadata only; PB-0105 performs no motion extraction or engine configuration.

## Determinism and Equality

All PB-0105 types are immutable value-style types. Stable FNV-based hashes use ordinal UTF-16 identity data and numeric bit representations, normalize signed zero consistently with equality, and do not depend on current culture, UI culture, process-randomized string hashing, input hierarchy order, or input pose order.

## Test Inventory

The xUnit v3 PB-0105 suite covers:

- Every approved rig, loop, and root-motion identity, canonical parser, stable order, immutable registry, ordinal casing, equality, and culture independence.
- Valid hierarchies plus null input, no root, multiple roots, missing parents, self-parenting, ordinal duplicates, and direct/indirect cycles.
- Finite signed translation/scale boundaries; robust quaternion normalization, equivalent sign, zero length, and NaN/both infinities in all ten transform components.
- Complete poses, canonical order, immutable snapshots, and null, unknown, duplicate, or missing bone entries.
- Rig/reference-pose consistency and explicit Generic/Humanoid selection.
- Clip name, inclusive range, negative frames, complete numeric boundaries, reversed ranges, duration, zero/negative/NaN/infinite FPS, loops, and consistent root-motion metadata.
- Immutability, equality, deterministic ordering, signed-zero hashing, exact ordinal casing, culture independence, and Domain dependency isolation.

## Coverage Evidence

Coverage uses the centrally pinned `coverlet.collector` `10.0.1` with `ExcludeAssembliesWithoutSources=None`. The focused final-source report remains beneath ignored `artifacts/PB-0105`.

All 18 new production files report 100% line and 100% branch coverage in the final focused PB-0105 run.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0105 tests | Pass; 110 passed, 0 failed, 0 skipped. |
| Per-file coverage | Pass; all 18 new production files report 100% line and 100% branch coverage. |
| Complete `PackageBuilder.Domain.Tests` | Pass; 468 passed, 0 failed, 0 skipped, preserving the previous 358 tests. |
| All four core test projects | Pass; 471 discovered, 471 passed, 0 failed, 0 skipped. |
| Solution architecture validator | Pass; 7 checks passed across the exact 15-project inventory. |
| Quality validator in PowerShell 7 and Windows PowerShell 5.1 | Pass in both shells; 11 checks passed, 0 failed per run. |
| ADR validator | Pass; 8 checks passed, 0 failed. |
| Repository baseline with `RequireTrackedFiles` | Pass; 29 checks passed, 0 failed. |
| Full core CI | Pass; all 9 fail-closed stages passed. |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors. |
| Formatting and lint | Pass; `.NET` formatting plus Ruff lint/format checks. |
| Diff and public-repository scans | Pass; `git diff --check` plus candidate secret, personal-path, generated-content, prohibited-file, and trailing-whitespace scans. |

## Remaining Gates

PB-0105 remains `[ ]` / 🟡 **PROCESS**, stays in Active Work, and remains absent from the Completion Log. Local implementation and the requested validation matrix pass. User-controlled staging, task commit, task-branch push, merge into and push of `main`, successful required `main` CI, explicit completion confirmation, and next-task rollover synchronization remain.
