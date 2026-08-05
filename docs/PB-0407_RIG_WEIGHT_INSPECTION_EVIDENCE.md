# PB-0407 Armature, Skin, and Weight Inspection Evidence

## Status

- Task: PB-0407 — Implement armature, skin, and weight inspection
- Canonical branch: `feat/PB-0407-rig-inspection`
- Publication branch: `feat/PB-0406-texture-inspection`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

PB-0407 shares the explicitly approved PB-0406/PB-0407/PB-0408 publication cycle. Its canonical
identity, acceptance, tests, and evidence remain separate.

## Implemented Boundary

`package_builder_blender.rig_inspection.inspect_rigs(...)` reads armature objects, rest-pose bones,
Armature modifiers, vertex groups, and raw vertex memberships without posing, evaluating, or
changing Blender data.

The immutable report includes armature identity, all bones and parents, root bones, deform flags,
local head/tail coordinates, local rest matrices, skinned mesh identity, referenced armature,
vertex count, vertex-group names, missing deform groups, unmatched groups, unweighted vertex
indices, maximum positive deform influences, and the mesh object's parent-inverse matrix. The
field is deliberately named `parent_inverse_matrix`; it is not mislabeled as a universal inverse
bind matrix. Bone rest matrices provide the reported bind/rest context.

Skeletons may have multiple roots and are reported exactly. Cycles, orphan parents, duplicate
identities, multiple Armature modifiers, missing armatures, empty skinned meshes, malformed group
indices, invalid weights, or non-finite matrix/vector values fail closed.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_RIG_INPUT_INVALID` | Scene objects cannot be enumerated safely. |
| `BLENDER_RIG_DATA_INVALID` | Armature, hierarchy, skin, group, weight, or matrix data is incomplete or inconsistent. |

Expected failures are sanitized PB-0109-compatible blocking findings from
`blender-rig-inspector`.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Skeletons, hierarchy, roots, and bones reported | complete skeleton and multiple-root tests |
| Deform flags reported | deform-bone totals and per-bone assertions |
| Skinned meshes and bind/rest data reported | Armature-modifier, bone-rest, and parent-inverse assertions |
| Missing groups reported | missing deform and unmatched group assertions |
| Unweighted vertices reported | zero-positive-deform-influence tests |
| Malformed data fails safely | hierarchy, modifier, group, weight, empty-mesh, matrix, identity, and unreadable tests |

## Validation

- Focused PB-0407 tests: 12 passed, 0 failed.
- Combined PB-0406/PB-0407/PB-0408 tests: 38 passed, 0 failed.
- All Blender worker tests: 88 passed, 0 failed.
- New production modules: 100% executable-line coverage under Python `trace`; no branch-coverage
  percentage is claimed.
- Debug and Release solution builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29 passed; full Core CI passed all nine stages.
- Ruff lint/formatting, locked restore, .NET formatting, PowerShell parsing, documentation links,
  lifecycle/task graph, vulnerability, secret/prohibited-content, and diff checks passed.

## Evidence Limits

- Raw imported mesh data is reported; evaluated modifiers, pose evaluation, retargeting, weight
  repair, and export validation belong to later tasks.
- Multiple roots are factual metadata, not automatically classified as an error.
- No approved contained Blender executable is present; real Blender integration is not claimed.
- This task changes no WPF view or rendered output, so manual visual testing is not applicable.

## Remaining Gates

User-controlled commit/push/merge, successful required `main` CI, explicit user confirmation, and
PB-0409 rollover remain. PB-0407 stays `[ ]` / 🟡 **PROCESS** and absent from the Completion Log.
