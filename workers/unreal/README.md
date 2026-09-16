# Unreal foundation worker

PB-1101–PB-1103 provide a versioned UE 5.8 template and protocol shell. Real 5.8.2
foundation save/reopen and rejected-request checks passed on 2026-09-16. Full product compatibility
and global ApprovedLatest selection are separate gates.

The user installed and explicitly approved `C:\Program Files\Epic Games\UE_5.8`.
The harness requires that exact root, valid Epic Games executable signature and matching 5.8.2
metadata/runtime version. AGENTS records this external-engine exception; all project outputs stay
in the repository. Production discovery keeps other external detections informational.
The user handles Epic sign-in, licence acceptance and eligibility. Unreal is an externally licensed
prerequisite; see [Epic's licensing terms](https://www.unrealengine.com/license). No paid IDE is
required for this content-only Python plugin. No engine is installed or terms accepted automatically.

From repository-local PowerShell 7, run:

```powershell
. ./scripts/Enter-PackageBuilderEnvironment.ps1
./scripts/Invoke-UnrealFoundationIntegration.ps1
```

The harness reuses the production Unreal installation locator and shared bounded version parser,
requires the exact candidate
version, copies only the six reviewed template files into a unique disposable project, then
starts three separate `UnrealEditor-Cmd.exe` processes. The first saves one probe material; the
second loads it and checks its bytes; the third rejects an unsupported request without changing it. Per-process timeout defaults to ten minutes. Timeout
stops the owned process tree. This is a development smoke harness, not the production cloning
and exclusive-execution adapter planned in PB-1104.

Requests use the existing worker v1 contract. `probe-unreal-worker` and `verify-unreal-worker`
are the only operations; requests cannot select arbitrary Python code. The plugin bootstrap
is passed explicitly with `-run=pythonscript -script=...`. There is no automatic startup script.
Python is an editor automation feature, not customer runtime code; Epic currently labels it
Experimental in its [Python documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/scripting-the-unreal-editor-using-python).

The bootstrap expects trusted harness variables `PACKAGEBUILDER_WORKERS_ROOT` and
`PACKAGEBUILDER_UNREAL_REQUEST`. Both the `workers/shared` and `workers/unreal` directories
must be available. The shared module owns bounded request parsing, logical path containment,
link/junction rejection, JSON Lines encoding and atomic result writes. Native Unreal output
is separate from the dedicated, flushed `output/worker-events.jsonl` protocol stream.
Worker return codes are 0 success, 3 invalid request, 4 unsupported operation, 5 engine failure,
6 result-write failure. The bootstrap maps nonzero codes to a commandlet failure; Unreal's
process exit code need not equal the internal protocol code. Results never promote output.

The tracked template has only the project descriptor, two configs, a `Content/Pack` placeholder,
plugin descriptor and bootstrap. No caches or `.uasset` files belong in it. Dynamic product
names, imports, overview maps and customer-package stripping remain later E11 tasks. The
worker plugin and probe must never be shipped in a customer release.

Default cleanup removes the entire owned test clone after success or failure. Compact results,
progress and native logs remain in `artifacts/PB-1103/<run-id>`; the receipt distinguishes
real execution, smoke success and cleanup success. The reusable local filesystem DDC stays under `runtime-data/unreal`;
it is deliberately reused. If cleanup fails, the harness fails and records the remaining run;
inspect it before retrying bounded cleanup. Native logs are local diagnostics, not safe-to-publish
support bundles. Final successful logs are warning/error-free and confirm the repository cache path. The harness
uses `-ddc=(Local)` and disables Zen auto-launch. The first diagnostic run created Zen helper
copies in the user profile; automatic approval review rejected their cleanup. See the
[cleanup audit](../../docs/TEST_ARTIFACT_CLEANUP.md) for this retained exception.

A passing smoke is only candidate evidence. Full approved-version selection requires the
existing compatibility suite and explicit approval; this harness does not alter the approval database.
