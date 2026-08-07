# PB-0603 Unity Batch Protocol Evidence

## Lifecycle

- Task: PB-0603 — Implement Unity batch entrypoint and progress protocol.
- Canonical branch: `feat/PB-0603-unity-entrypoint`.
- Publication branch: `feat/PB-0602-unity-worker-package` under the approved combined
  PB-0602/PB-0603/PB-0604 cycle.
- Status: `[ ]` / 🟡 **PROCESS**.
- Started: 2026-08-06.

PB-0603 remains open until the combined publication, required `main` CI, explicit completion
confirmation, and next-task rollover are complete.

## Protocol Boundary

`PackageBuilder.UnityWorker.Editor.UnityBatchEntrypoint.Run` accepts one
`-packageBuilderRequest` file. The version-1 request parser is bounded to 65,536 characters,
rejects missing, duplicate, unknown, nested, malformed, and unsafe logical-reference input, and
validates the requested Unity version and target before writing project content.

The worker:

1. Reads and validates the request without modifying the source template.
2. Emits deterministic JSON Lines progress and metric records to the Unity log/standard stream.
3. Checks the existing project-contained cancellation-file protocol.
4. Resolves all input, output, and result references beneath the isolated project clone.
5. Writes the probe asset and final result atomically, imports the asset synchronously, calls
   `AssetDatabase.SaveAssets`, and exits explicitly.

Stable process exit codes are:

| Code | Meaning |
|---:|---|
| 0 | Success |
| 2 | Invocation failure |
| 3 | Invalid request |
| 4 | Unsupported operation |
| 5 | Execution failure |
| 6 | Result-write failure |
| 7 | Cancelled |

## Real Unity Batch Evidence

- Unity Editor: official signed `6000.3.10f1`, revision `e35f0c77bd8e`.
- Operation: `probe-unity-worker`.
- Success process exit code: 0.
- Unsupported-operation process exit code: 4 with structured
  `UNITY_OPERATION_UNSUPPORTED` failure.
- Pre-existing cancellation-signal process exit code: 7 with acknowledged cancellation result.
- JSON Lines progress/metric records: 5 across the three launches.
- Saved asset: `Assets/PackageBuilderWorkerOutput/worker-probe.txt`.
- Atomic result: `PackageBuilder/worker-result.json` with protocol version 1, success status,
  exact worker/engine identity, and one artifact receipt.
- Editor assembly compilation: passed with no C# compilation error in the retained Unity log.
- Repository baseline: 31 passed, 0 failed.
- Full Core CI: all nine stages passed in 5 minutes 4.1 seconds.
- Release solution build: 18 projects, 0 warnings, 0 errors.
- Complete test suite: 2,282 passed, 0 failed, 0 skipped.

The reusable integration command is `scripts/Invoke-UnityWorkerIntegration.ps1`. It verifies the
Unity executable version, company, and Authenticode signer before launching and contains Unity
temporary/package-manager state beneath `runtime-data/unity/6000.3.10f1`.

## Remaining Gates

- User-controlled commit and combined branch push.
- Merge into and push of `main`.
- Successful required `main` CI.
- Explicit completion confirmation and next-task rollover synchronization.
