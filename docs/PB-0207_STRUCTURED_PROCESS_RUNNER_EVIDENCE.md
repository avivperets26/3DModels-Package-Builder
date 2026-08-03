# PB-0207 Structured External Process Runner Evidence

**Task:** PB-0207 — Implement structured external process runner  
**Branch:** `feat/PB-0207-process-runner`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-08-03

## Scope and rollover

PB-0206 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the
Completion Log. Final task commit `b4aec5ebb1edcbcc9b29b43ffc3c9f175d69eed9` merged through
[pull request #35](https://github.com/avivperets26/3DModels-Package-Builder/pull/35) as
`570182210df18c7af2f2cac1a3ffdc09279aa46a`.
[PR workflow run 30840565528](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30840565528)
and required
[main workflow run 30840576320](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30840576320)
completed successfully. The user explicitly confirmed the push, merge, green required `main` CI,
and completion on 2026-08-03. No exception was used.

PB-0207 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log until its one
user-controlled publication, required `main` CI, explicit confirmation, and next-task rollover.

## Implemented boundary

- `IExternalProcessRunner` accepts an immutable request with the build-job identity, project root,
  executable, working/temp/cache/log directories, literal arguments, explicit environment, and a
  bounded per-stream capture limit.
- `ProcessStartInfo.UseShellExecute` is false and arguments are added only through `ArgumentList`.
  No shell command string, quoting reconstruction, `cmd.exe`, or PowerShell invocation is used.
- The child environment starts empty. Only reviewed Windows bootstrap variables are copied, caller
  entries are validated and added literally, and profile/temp/cache/log variables are forced to the
  request's contained roots. Caller attempts to replace runner-owned variables are rejected.
- Executable, working, temporary, cache, and log paths must be existing canonical absolute strict
  descendants of the project root. UNC, relative, aliased, missing, outside, root-equal, and
  reparse-point paths fail before launch through sanitized structured failures.
- The executable is opened with write/replacement sharing denied while its size and SHA-256 are
  streamed and launch begins. The receipt records a safe project-relative path, byte count,
  lowercase SHA-256, optional file/product version, exit code, and separate stdout/stderr captures.
- Both streams are drained concurrently. Captures retain at most the explicit limit and report
  truncation without allowing a full child pipe to deadlock the process.
- A nonzero exit code is observable process output, not a runner failure. Expected validation,
  launch, access, I/O, and metadata failures return safe codes/locations/diagnostics without
  echoing rejected paths, environment values, or arguments.

PB-0207 intentionally adds no cancellation, timeout, process-tree termination, or cleanup policy;
PB-0208 owns that lifecycle. PB-0209 owns JSON Lines framing and recovery. PB-0212 owns structured
redacted logging, PB-0213 orchestration/retry, PB-1503 the later cross-cutting hostile-argument
suite, and PB-1811 the final external-process security hardening gate.

## Requirements-to-tests matrix

| Requirement | Focused evidence |
|---|---|
| Literal shell-free argument transport | `RunsContainedExecutableWithLiteralArgumentsExplicitEnvironmentAndSeparateStreams`, including spaces, quotes, Unicode, metacharacters, and an empty argument |
| Separate stdout/stderr and exact exit code | `RunsContainedExecutableWithLiteralArgumentsExplicitEnvironmentAndSeparateStreams` |
| Explicit isolated environment and working directory | `RunsContainedExecutableWithLiteralArgumentsExplicitEnvironmentAndSeparateStreams`, `RejectsInvalidEnvironment` |
| Bounded capture that still drains streams | `BoundedCaptureDrainsBothStreamsAndReportsTruncation`, `RejectsInvalidCaptureLimits` |
| Executable identity metadata | `RunsContainedExecutableWithLiteralArgumentsExplicitEnvironmentAndSeparateStreams`, `ContractConstructorsRejectNullRequiredValues` |
| Project-root containment for every launch path | `EveryLaunchPathMustBeContained`, `RejectsMissingAndOutsidePaths`, `RejectsInvalidNonCanonicalAndMissingDirectoryPaths` |
| Reparse-boundary rejection | `RejectsDirectoryAndExecutableReparseBoundaries`, `RejectsReparsePointProjectRoot` |
| Sanitized structured failure | `InvalidExecutableReturnsSanitizedStructuredFailure`, `ExpectedProcessFailureClassifierHasAnExplicitBoundary` |
| Immutable strict contracts | `RequestSnapshotsArgumentsAndEnvironment`, `ContractConstructorsRejectNullRequiredValues`, `ResultFactoriesAndRunnerNullGuardAreStrict` |

The integration probe is compiled at test time from the tracked
`tests/fixtures/processes/ProcessProbe.cs` source. Its executable, build state, working/temp/cache/log
directories, and coverage remain ignored beneath `artifacts/PB-0207`; no probe binary or generated
project is tracked.

## Local validation

| Validation | Result |
|---|---|
| Focused PB-0207 tests | Pass; 34 passed, 0 failed, 0 skipped |
| Critical production coverage | Pass; all 14 new production and compiler-generated components report 100% line and branch coverage in the Microsoft Cobertura report beneath `artifacts/PB-0207/coverage-final-3` |
| Debug solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Release solution build | Pass; 15 projects, 0 warnings, 0 errors |
| Complete core tests | Pass; 1,546 passed, 0 failed, 0 skipped: Domain 789, Application 53, Infrastructure 320, Contracts 384 |
| Repository baseline | Pass; 29 checks, 0 failures |
| Full local Core CI | Pass; all 9 stages completed in 3m 8.547s on the exact final worktree |
| Formatting and repository safety | Pass; .NET info-level and Ruff formatting, `git diff --check`, links, lifecycle, task graph, secrets, personal paths, prohibited/generated content, and history integrity |

No dependency, engine, network, paid service, telemetry, or approved quality-threshold change is
included.

## Manual and visual testing

PB-0207 has no WPF screen, renderer, model import, or package preview, so there is no end-user
visual test yet. The focused suite does perform real shell-free child-process execution and
verifies its exact argument, environment, directory, stream, exit-code, containment, and metadata
behavior. The first supported visual workflow remains the later WPF vertical slice.

## Remaining gates

Final exact-worktree validation, user-controlled commit and branch push, merge into and push of
`main`, successful required `main` CI, explicit completion confirmation, and PB-0208 rollover
remain.
