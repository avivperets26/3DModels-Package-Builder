# PB-0409 Automatic Product-Case Inference Evidence

## Status

- Canonical branch: `feat/PB-0409-case-inference`
- Publication branch: `feat/PB-0409-case-inference`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-05

PB-0409 shares one explicitly approved publication cycle with PB-0410 and PB-0411. Its API,
acceptance mapping, tests, evidence, canonical branch identity, and eventual Completion Log row
remain independent.

## Implemented Boundary

`package_builder_blender.case_inference.infer_product_case(...)` consumes completed geometry, rig,
and animation inspection reports. It infers `static` only when meshes exist without a complete
skeleton/skin pair or motion; `rigged` when both skeleton and skin exist without motion; and
`rigged-animated` only when a complete rig and an inspected moving clip exist.

Actions without actual motion do not make a product animated. Motion without a complete rig,
partial rig/skin state, empty geometry, malformed counts, unknown cases, and contradictory
single-product manifest declarations fail closed. Item-set and item-collection are never guessed
from mesh or file counts; either result requires the matching explicit manifest case.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_CASE_INPUT_INVALID` | Inspection facts or manifest case are malformed. |
| `BLENDER_CASE_RIG_INCOMPLETE` | Only one of skeleton or skinned-mesh binding exists. |
| `BLENDER_CASE_ANIMATION_WITHOUT_RIG` | Motion exists without a complete rig. |
| `BLENDER_CASE_MANIFEST_CONFLICT` | A declared single-product case contradicts inspected facts. |

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Static inference | mesh-only fact test |
| Rigged inference | complete skeleton/skin test |
| Animated inference | motion versus still-Action test |
| Set/collection ambiguity requires manifest | multi-mesh no-guess and explicit set/collection tests |
| Contradictions fail closed | partial-rig, motion-without-rig, manifest-conflict, and malformed tests |

## Validation

- Focused PB-0409 tests: 8 passed, 0 failed.
- Focused executable-line trace: 99%; Python branch coverage is not currently measured or claimed.
- Combined focused tests: 29 passed; all Blender worker tests: 117 passed.
- Debug and Release builds: 16 projects, 0 warnings, 0 errors.
- Baseline .NET tests: 2,067 passed, 0 failed, 0 skipped.
- Repository baseline: 29/29; final Core CI: all nine stages passed in 2 minutes 35.460 seconds.
- Ruff lint/formatting, locked restore, .NET formatting, PowerShell parsing, documentation/task graph,
  secret/prohibited-content, vulnerability, and `git diff --check` checks passed.

## Evidence Limits and Remaining Gates

The inference boundary is renderer-independent plain Python. No approved contained Blender runtime
is present, so real Blender integration is not claimed. No UI or rendered output changed; manual
visual testing is not applicable. Commit, branch push, merge into and push of `main`, required
`main` CI, explicit user confirmation, and next-task rollover remain.
