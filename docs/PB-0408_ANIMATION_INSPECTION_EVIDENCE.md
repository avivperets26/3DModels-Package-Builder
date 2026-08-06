# PB-0408 Action and Animation Inspection Evidence

## Status

- Task: PB-0408 — Implement action and animation inspection
- Canonical branch: `feat/PB-0408-animation-inspection`
- Publication branch: `feat/PB-0406-texture-inspection`
- Lifecycle: 🟢 **DONE**
- Date: 2026-08-05

PB-0408 shares the explicitly approved PB-0406/PB-0407/PB-0408 publication cycle. Its canonical
identity, acceptance, tests, and evidence remain separate.

## Implemented Boundary

`package_builder_blender.animation_inspection.inspect_animations(...)` reports Blender 5 layered
Actions as well as legacy Action F-curves without evaluating, playing, sampling, or changing
animation data.

Each Action becomes one source clip report with frame range, scene FPS adjusted by `fps_base`,
duration, channels, keyframe/sample counts, motion presence, transform-motion presence, and
conservative loop-likelihood metadata. Layered channels retain slot identifier, layer name, and
strip index, so equal data paths in distinct Blender 5 layers are not collapsed.

Motion requires differing finite values or a supported procedural generator/noise modifier with
points. A CYCLES modifier is loop evidence but does not manufacture motion for a static channel.
Loop likelihood is `likely`, `unlikely`, or `unknown`, with explicit reasons such as a name hint or
CYCLES modifier; it is metadata for review rather than an authoritative loop setting.

## Stable Findings

| Code | Meaning |
|---|---|
| `BLENDER_ANIMATION_INPUT_INVALID` | Action data cannot be enumerated safely. |
| `BLENDER_ANIMATION_DATA_INVALID` | FPS, Action, layer, strip, slot, F-curve, range, point, modifier, or identity data is inconsistent. |

Expected failures are sanitized PB-0109-compatible blocking findings from
`blender-animation-inspector`.

## Acceptance Mapping

| Acceptance condition | Automated evidence |
|---|---|
| Actions and clips reported | legacy, layered, empty-inventory, and empty-Action tests |
| Frame ranges and FPS reported | keyframe-derived range, Action fallback, and `fps_base` tests |
| Channels reported | legacy channel and Blender 5 slot/layer/strip tests |
| Motion presence reported | moving, sampled, static, and transform-channel tests |
| Likely loop behavior reported | name-hint, CYCLES, unlikely, and unknown tests |
| Invalid animation data fails safely | duplicate, invalid range/FPS, non-finite, and unreadable tests |

## Validation

- Focused PB-0408 tests: 10 passed, 0 failed.
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

- One Blender Action is treated as one source clip. NLA composition and evaluated scene motion are
  outside this task.
- Loop likelihood is deliberately conservative metadata and never changes `use_cyclic`, modifiers,
  extrapolation, keyframes, or clip settings.
- No approved contained Blender executable is present; real Blender 5 execution is not claimed.
- This task changes no WPF view or rendered output, so manual visual testing is not applicable.

## Publication Evidence

Task commit `28bfe9393b4a8c0d6a12775046390435ca785faa` was published on the approved shared PB-0406 branch
and merged through pull request #59 as `67668d24c3a5ea418affc01bde7267580ac9fb22`. Required `main`
workflow run 31047283980 succeeded, and the user explicitly confirmed completion on 2026-08-05.
No CI or quality exception was used. PB-0408 is `[x]` / 🟢 **DONE** and appears exactly once in the
Completion Log under its canonical task identity.
