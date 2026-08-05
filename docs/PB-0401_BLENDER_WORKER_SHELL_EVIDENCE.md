# PB-0401 Blender Worker Package and Entrypoint Evidence

**Task:** PB-0401 — Create Blender worker package and entrypoint
**Branch:** `feat/PB-0401-blender-worker-shell`
**Lifecycle:** 🟢 **DONE**
**Evidence date:** 2026-08-05

## Scope

PB-0401 creates the versioned `package_builder_blender` Python package and the bootstrap passed to
Blender's `--python` argument. The shell receives one PB-0112 protocol-v1 request file, emits
PB-0209-compatible compact JSON Lines progress, and atomically writes a PB-0112 protocol-v1 result.
The standard-library-only shell can be tested without installing or launching Blender, while the
same bootstrap detects `bpy` and records or verifies the hosted Blender version when Blender loads
it.

Only `probe-blender-worker` succeeds in this task. Scene reset, context utilities, FBX/GLB import,
inspection, normalization, export, and clean reimport remain PB-0402 through PB-0414. Other valid
operations return a blocking `BLENDER_OPERATION_UNSUPPORTED` finding and structured failure rather
than claiming unavailable processing.

## Protocol and Security Boundary

- Request files must be absolute regular non-link files, valid UTF-8 JSON, non-empty, and no larger
  than 1 MiB.
- Duplicate and unknown properties, excess JSON depth, unsupported versions, invalid identities,
  malformed values, and unsafe logical references fail closed.
- The request parent is the job workspace. Result references receive link-aware resolution and
  cannot escape that workspace through traversal, absolute paths, backslashes, or linked parents.
- Progress is flushed as exactly one compact JSON object per physical line.
- Result output uses a same-directory temporary file, flush, `fsync`, and atomic replacement;
  abandoned temporary output is cleaned after write failure.
- Standard error exposes stable codes only, not request content, absolute paths, environment
  values, exception text, or stack traces.
- Hosted Blender version mismatch and runtime initialization errors produce blocking sanitized
  findings and never claim promoted output.
- The worker performs no network communication, telemetry, download, installation, licence
  acceptance, embedded-script execution, or asset processing in PB-0401.

## Exit Codes

| Exit | Meaning | Durable result |
|---|---|---|
| `0` | Worker probe succeeded | Success result written atomically. |
| `2` | Invocation arguments invalid | No trustworthy request; no result. |
| `3` | Request invalid or unsafe | No trustworthy destination; no result. |
| `4` | Operation unsupported | Failure result and blocking finding written. |
| `5` | Runtime mismatch or execution initialization failure | Failure result written when possible. |
| `6` | Result write failed | No successful result may be inferred. |

## Acceptance Mapping

| Acceptance criterion | Automated evidence |
|---|---|
| Blender worker receives a request file | `test_probe_emits_shared_json_lines_and_atomically_writes_shared_result` and `BlenderProbeRequestGoldenRoundTripsThroughSharedProtocol`. |
| Progress uses shared JSON Lines | probe test plus `BlenderWorkerGoldensUseSharedProgressAndResultContracts`. |
| Result is written durably using the shared contract | probe atomic/temp-file assertions plus the shared result golden contract test. |
| Exit codes are documented and observable | unsupported, invalid-request, missing-separator, runtime-mismatch, runtime-exception, and result-write tests. |
| Unsafe and hostile requests fail before side effects | duplicate/unknown/traversal/oversized request loop and request-symlink test. |
| Errors are actionable but sanitized | unsupported/runtime findings and result-write/runtime-exception redaction assertions. |
| Exact Blender runtime is enforced when hosted | runtime mismatch and omitted-version recording tests using a deterministic `bpy` boundary double. |

## Current Validation

| Validation | Current result |
|---|---|
| Focused Python worker tests | Pass; 10 passed, 0 failed, 0 skipped. |
| Focused shared .NET worker-contract tests | Pass; 112 passed, 0 failed, 0 skipped. |
| Ruff lint and formatting | Pass for `workers/blender` and `tests/blender`. |
| Complete five-project .NET test portfolio | Pass; 2,067 passed, 0 failed, 0 skipped: Domain 857, Application 130, Infrastructure 647, Contracts 418, WPF 15. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline and full local Core CI | Pass; baseline 29/29 and all 9 Core CI stages passed in 2 minutes 56 seconds. |
| Formatting and prohibited-content checks | Pass; strict information-level .NET formatting, Ruff lint/formatting, `git diff --check`, and repository public-content checks succeeded. |
| Python line tracing | CPython standard-library trace executed all 10 tests and recorded 248/275 executable worker-package lines (90.18%). It does not measure branches or replace the future approved coverage tooling/threshold enforcement, so branch coverage remains an evidence gap. |
| Actual Blender executable integration | Not run: no contained verified Blender installation is present. PB-0401 tests the protocol shell in CPython with a deterministic `bpy` boundary double and does not claim engine integration. |

## Licensing

Blender is external GNU GPL software. This task neither redistributes nor installs Blender, accepts
its licence, or determines eligibility. The user selects a contained Blender installation verified
by PB-0302. The official terms are available on the
[Blender licence page](https://www.blender.org/about/license/).

## Final Publication Evidence

- Final task commit: `0251fb96f551f651a4c65c1220beeca3c727f061`.
- Pull request: [#54](https://github.com/avivperets26/3DModels-Package-Builder/pull/54).
- Merge commit on `main`: `b8568c5ab0c2252e01aeee13dfe6e9f678fc145e`.
- Required successful `main` CI: [workflow run 31024666006](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/31024666006), completed successfully for that exact merge commit.
- User confirmation: explicit push, merge, green required `main` CI, and completion confirmation on 2026-08-05.
- Exceptions: none. Python branch coverage and actual contained Blender integration remain disclosed evidence gaps; no unsupported claim is made.

PB-0401 is logically complete and its `[x]` / 🟢 **DONE** status, Active Work removal, and single
Completion Log row are synchronized at the beginning of PB-0402 under the permanent one-merge
rollover workflow.
