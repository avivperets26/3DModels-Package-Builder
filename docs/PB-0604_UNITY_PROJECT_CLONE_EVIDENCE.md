# PB-0604 Unity Project Clone Evidence

## Lifecycle

- Task: PB-0604 — Implement Unity template cloning and exclusive job execution.
- Canonical branch: `feat/PB-0604-unity-job-clone`.
- Publication branch: `feat/PB-0602-unity-worker-package` under the approved combined
  PB-0602/PB-0603/PB-0604 cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-06.

PB-0604 remains open until the combined publication, required `main` CI, explicit completion
confirmation, and next-task rollover are complete.

## Clone and Lock Contract

`UnityProjectCloneService` accepts a validated build-job identity plus project, template, and job
directories. It requires the template and job roots to be strict, non-overlapping descendants of
the project root and rejects reparse points and any template root other than `Assets`, `Packages`,
and `ProjectSettings`.

Each job:

1. Acquires `.packagebuilder-unity-clone.lock` with `FileShare.None`.
2. Copies the template in ordinal deterministic order into a unique staging directory.
3. Flushes copied files before atomically moving staging to `unity-project`.
4. Holds the exclusive lock for the complete `UnityProjectCloneLease` lifetime.
5. Records completion as success, failure, or cancellation.
6. Applies one explicit policy: delete always, retain on failure/cancellation, or retain always.
7. Removes partial staging on cancellation or copy failure and returns a structured failure.

No process is started and no tracked template is changed by the clone service.

## Validation

- New Unity target test project: `PackageBuilder.Targets.Unity.Tests`.
- Focused Release tests: 14 passed, 0 failed, 0 skipped.
- Exact clone-content and exclusive-lock test: passed.
- Concurrent-writer rejection: passed.
- Seven success/failure/cancellation retention combinations: passed.
- Unfinished-lease failure retention: passed.
- Existing-clone preservation: passed.
- Pre-cancelled cleanup: passed with no clone, staging, or lock residue.
- Missing, outside-project, overlapping, malformed-template, invalid-policy, and missing-job
  rejection: passed.
- Solution architecture: 18 projects, 7 checks passed.
- Test-project configuration: seven approved test projects, 4 checks passed.
- Repository baseline: 31 passed, 0 failed.
- Full Core CI: all nine stages passed in 5 minutes 4.1 seconds.
- Release solution build: 18 projects, 0 warnings, 0 errors.
- Complete test suite: 2,282 passed, 0 failed, 0 skipped across seven test projects.
- Coverlet collector attempts in both Release and Debug passed all 14 tests but emitted empty
  Cobertura reports. This known collector/toolchain anomaly is disclosed rather than represented as
  coverage evidence; PB-1806 remains the owner of the enforced repository-wide coverage gate.

Test workspaces are unique, ignored, and contained beneath `artifacts/test-workspaces/PB-0604`.

## Remaining Gates

- User-controlled commit and combined branch push.
- Merge into and push of `main`.
- Successful required `main` CI.
- Explicit completion confirmation and next-task rollover synchronization.
