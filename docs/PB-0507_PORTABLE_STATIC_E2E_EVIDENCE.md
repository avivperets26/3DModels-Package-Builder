# PB-0507 Portable Static Vertical-Slice Evidence

## Status

- Branch: `test/PB-0507-portable-static-e2e`
- Lifecycle: 🟡 **PROCESS**
- Date: 2026-08-06

PB-0507 is implemented, locally runnable, and merged. It remains open until the required workflow
for the exact `main` merge succeeds and the user explicitly confirms completion.

## Publication and External-CI Checkpoint

- Task commit: `03d18ae0a646b133ee55cbca808ed09c0fd8103f`.
- Pull request: [#66](https://github.com/avivperets26/3DModels-Package-Builder/pull/66).
- Merge commit: `c1a560c620ca8ad23b10becbd8f6c7860155107d` on 2026-08-06.
- GitHub created no check or workflow run for that merge while its official status service reported
  a major Actions outage.
- No CI exception is approved. PB-0507 remains `[ ]` / 🟡 **PROCESS** and is absent from the
  Completion Log.
- Independent PB-0601 work may proceed because it does not depend on PB-0507.

## Delivered Vertical Slice

The success path composes the existing production contracts and services rather than duplicating
their behavior:

1. Deserialize the tracked static product manifest and publisher profile.
2. Generate the deterministic PB-0308 build lock.
3. Inspect the manifest's static/portable invariants.
4. Stage and validate the self-authored FBX in the PB-0205 artifact store.
5. Generate the PB-0504 README and PB-0502 flat portable layout.
6. Create the PB-0505 deterministic FBX ZIP.
7. Run the PB-0506 fail-closed portable validator.
8. Write a deterministic compact JSON validation report and correlated PB-0212 JSON Lines log.
9. Persist every PB-0213 job transition in SQLite.
10. Atomically promote only the validated ZIP through PB-0206 into `artifacts/Builds`.

The final promoted release is `Stone_Arch/Stone_Arch_FBX.zip`. It contains exactly:

```text
Stone_Arch_fbx/README_StoneArch.txt
Stone_Arch_fbx/StoneArch.fbx
```

The failure path supplies blocking clean-reimport evidence. It writes a failed JSON report with
`PORTABLE_REIMPORT_FAILED`, completes the persisted job as failed, does not call the promoter, and
does not create a release directory.

## Fixture Provenance and Reimport Boundary

`tests/fixtures/portable/static-vertical-slice` is a repository-authored CC0-1.0 fixture. The
contained Blender 5.0.0 generator creates a single cube mesh, exports FBX 7400, and reimports it in
Blender before recording the exact file SHA-256 and one-mesh/no-rig/no-action evidence. No Meshy,
customer, marketplace, or third-party model is committed.

The fixed FBX identity is:

```text
9d3cc82c22918c15fa20c674b26df60d5b86a808bc7b1e9a1962eb33916fd9a9
```

PB-0507 consumes the tracked clean-reimport evidence and verifies it still matches the exact FBX.
PB-1601 through PB-1609 retain ownership of the broad golden-fixture and supported-version matrix.

## Validation Report Boundary

`PortableStaticValidationReportJson` emits schema version 1 with stable property order, sorted
PB-0109 findings, the manifest identity/case/target, exact PB-0308 build lock, exact and logical
archive identities, and a project-relative logical log reference. It accepts only a static manifest
that selects the portable target and a build lock for the same job. Physical paths are never
serialized.

This is a deliberately narrow PB-0507 target receipt. PB-0910 remains the owner of the complete
cross-target validation-report schema and renderer/marketplace result model.

## Automated Acceptance Evidence

| Acceptance condition | Evidence |
|---|---|
| One static source builds from a manifest | The success E2E loads the tracked manifest and real FBX fixture |
| Release is atomically promoted | The PB-0206 receipt and final `artifacts/Builds` file are asserted |
| Logs are retained | One parseable JSON object is asserted for every executed orchestration stage |
| JSON validation report is retained | The report is parsed and its status, identity, findings, and archive hash are asserted |
| Invalid output cannot promote | Blocking reimport evidence yields a failed job/report and zero promoter calls |
| Package is manually inspectable | The project-contained manual script retains, extracts, and reports all evidence paths |

## Manual and Visual Checkpoint

From PowerShell:

```powershell
& .\scripts\Invoke-PB0507PortableStaticManualTest.ps1 -OpenFolder
```

From Git Bash:

```bash
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass \
  -File scripts/Invoke-PB0507PortableStaticManualTest.ps1 \
  -OpenFolder
```

The command runs the exact success E2E, retains its project-contained workspace, extracts the
promoted ZIP, and prints the release, JSON report, and JSONL log paths. Open the extracted README
and import `StoneArch.fbx` into Blender to see the cube. This is the first physical portable-package
visual checkpoint; it is not yet the WPF-driven arbitrary-model workflow.

## Local Validation

- Manual retained-output command: passed; promoted ZIP, extracted package, report, and log exist.
- Focused report tests: 12 passed, 0 failed, 0 skipped.
- Success/failure vertical-slice tests: 2 passed, 0 failed, 0 skipped.
- Complete portable-target suite: 201 passed, 0 failed, 0 skipped.
- Solution architecture: 17 projects, 7 checks passed.
- Complete Core CI: all nine stages passed in 4 minutes 26.6 seconds.
- Complete solution tests: 2,268 passed, 0 failed, 0 skipped across six test projects.
- Repository baseline: 29 passed, 0 failed, including exact fixture identity/licence validation.
- Debug and Release builds: 17 projects, zero warnings and zero errors.
- Locked restore, .NET/Ruff formatting, PowerShell parsing, task graph, Markdown links,
  secret/prohibited-content checks, history integrity, and `git diff --check`: passed.
- NuGet restore reported no vulnerability warning for direct or transitive dependencies.
- Coverlet executed all 12 focused report tests but emitted an empty Cobertura document under this
  local toolchain. No percentage is claimed; the anomaly is retained under ignored
  `artifacts/PB-0507/coverage` and does not weaken any release threshold.

## Remaining Gates

- Successful required CI for exact `main` merge `c1a560c620ca8ad23b10becbd8f6c7860155107d`.
- Explicit user completion confirmation and next-task rollover synchronization.
