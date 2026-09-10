# PB-0714 Unity Generic Topology Matrix Evidence

## Lifecycle

- Task: PB-0714 - Validate Unity arbitrary Generic rig topology matrix.
- Branch: `test/PB-0714-unity-generic-topologies`.
- Status: `[x]` / 🟢 **DONE**; historical completion reconciled on 2026-09-10 at user request in PB-0802.
- Published task `07c0b57c18423403a54ff437b5874d4d0e5d2e31`, PR #86 merge `24f42d6d2cee7fb3e3e101bdbfa860a73b09934a`, and [successful exact-main CI](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31502886537) verified through GitHub. The evidence below records the original local validation, not a new run.
- Started: 2026-08-11.

## Reusable Procedural Matrix

`tests/blender/engine/pb0714_generate_topology_matrix.py` generates every fixture from repository
code with Blender 5.0.0. It writes five animated skinned FBXs, one intentionally invalid
multi-root FBX, and one versioned engine-neutral manifest. No downloaded, private, marketplace, or
third-party asset is used or tracked.

| Category | Asset ID | Declared root | Moving bone | Bones |
|---|---|---|---|---:|
| Articulated mechanical bow | `MechanicalBow` | `BowRoot` | `RightLimb` | 4 |
| Articulated mechanical vehicle | `MechanicalVehicle` | `VehicleRoot` | `FrontWheelRight` | 8 |
| Quadruped with tail | `QuadrupedTail` | `BodyRoot` | `TailTip` | 9 |
| Winged creature | `WingedCreature` | `CreatureRoot` | `WingRightTip` | 7 |
| Non-humanoid biped with tail | `BipedTail` | `PelvisRoot` | `TailTip` | 9 |

Every bone owns a weighted disconnected cube in one skinned renderer. This makes the declared bone
inventory, one-influence skin weights, hierarchy, and representative deformation observable without
assuming a human skeleton or a particular creature shape. Each valid source has one non-looping
`Deform` action spanning frames 1 through 21 at 30 FPS.

The manifest is intentionally engine-neutral so PB-1214 can consume the same generated fixture
definitions for Unreal rather than recreating topology expectations.

## Unity Validation

The Unity integration reuses the maintained Generic importer, skin/skeleton validator, clip
importer, Animator Controller generator, animated-prefab generator, motion validator, exact package
exporter, and clean-reimport helper. The shared motion expectation now names its moving bone; callers
that omit the name retain the existing `Tip` default.

For each valid case, validation requires:

- Generic import with no Humanoid avatar and hierarchy optimization disabled;
- the exact declared root, bone-parent hierarchy, renderer, and complete bone array;
- one valid skin, one influence per vertex, and zero unweighted vertices;
- one 0.667-second, 30 FPS, non-looping clip bound to the declared moving bone;
- one exact controller state and one animated prefab;
- sampled bone rotation, skinned-renderer deformation, and non-looping completion.

Negative validation requires the exact stable findings:

- `UNITY_TOPOLOGY_ROOT_COUNT_INVALID` for the two-root fixture;
- `UNITY_ANIMATION_BINDINGS_MISMATCH:InvalidBinding` for a missing binding target.

## Automated Evidence

Retained run `artifacts/u/08f090a4` passed Blender generation, Unity 6000.3.10f1 product generation,
exact package validation/export, and a second fresh-project package import. The clean structured
result reports:

| Acceptance evidence | Result |
|---|---:|
| Validation findings | 0 |
| Valid topology cases | 5 |
| Animation clips | 5 |
| Animator Controller states | 5 |
| Generic/non-Humanoid import verified | true |
| Declared roots and hierarchy verified | true |
| Skin weights verified | true |
| Representative animation motion verified | true |
| Stable negative findings verified | true |

Repository regression validation also passed:

- Unity template inventory: 8/8;
- Unity worker inventory: 9/9;
- Unity product policies: 41/41;
- repository baseline: 32/32;
- Ruff lint and formatting: 49 files;
- Markdown lint: 143 files, 0 errors;
- Blender/Python unit tests: 156/156;
- maintained sequential .NET test harness: 2,320/2,320.

Retained evidence:

- generated project: `artifacts/u/08f090a4/p`;
- clean reimport project: `artifacts/u/08f090a4/t`;
- exact package:
  `artifacts/u/08f090a4/p/PackageBuilderExports/GenericTopologyMatrix.unitypackage`;
- structured clean result: `artifacts/u/08f090a4/unity-topology-clean-result.json`;
- hardened-validator clean rerun:
  `artifacts/u/08f090a4/unity-topology-clean-result-final.json`;
- engine-neutral fixture manifest: `artifacts/u/08f090a4/g/topology-matrix.txt`.

## Manual Unity Evidence

On 2026-08-11, the user supplied Unity screenshots that visually confirm:

- all five generated prefabs and all five Animator Controllers are present;
- the displayed bow, vehicle, quadruped-with-tail, winged-creature, and biped-with-tail prefabs
  preserve their distinct declared roots and bone hierarchies;
- the prefabs use their matching Animator Controller and Generic avatar;
- inspected clips are 0.667 seconds at 30 FPS with `Loop Time` disabled;
- the Animation window exposes curves for the expected topology-specific bones; and
- the displayed controller graphs contain one orange default `Deform` state plus the
  `Replay_Deform` trigger path.

The separated cubes visible in these screenshots are intentional test geometry. Each cube is
independently weighted so bone movement and skin deformation remain easy to identify and measure.
The screenshots supplement, but do not replace, the automated clean-reimport assertions above.

## Reuse and Duplication Audit

- Reused all production Generic-rig, skin, animation, controller, prefab, package, and clean-import
  adapters; the topology matrix adds fixture composition and assertions, not parallel domain rules.
- Generalized the existing motion validator with a safe explicit clip scope and moving-bone name
  instead of adding topology-specific animation sampling.
- Reused one manifest and one Unity matrix validator before export and after clean reimport.
- Intentional duplication: the harness repeats the five stable category tokens from the manifest so
  it can fail closed if an engine adapter silently omits or renames a required matrix case. PB-1214
  owns later Unreal consumption of the same manifest.

## Work Remaining

PB-0714 remains PROCESS until the user commits and pushes the task branch, merges and pushes
`main`, required exact-merge `main` CI succeeds, and the user explicitly confirms completion.
