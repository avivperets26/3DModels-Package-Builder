# Unreal foundation worker

PB-1110–PB-1112 add `create-unreal-overview`, `validate-unreal-overview` and
`render-unreal-previews` for one static product mesh. The companion overview plan comes from
the shared presentation specification. Capture receipts include final image and product-only
coverage hashes; the host must run shared media validation and inspect complete native logs
before release. Project-only redirector fixup runs under the existing clone lease.
See [overview evidence](../../docs/PB-1110_PB-1112_UNREAL_OVERVIEW_EVIDENCE.md).

PB-1107–PB-1109 add `import-unreal-surfaces` and `verify-unreal-surfaces`, consuming
`unreal-surface-plan.json` alongside the existing texture plan after textures are imported.
Masters, instances and static meshes are built only from explicit validated settings; source
FBX hashes and every target are checked before mutation. Use
`scripts/Invoke-UnrealSurfaceIntegration.ps1` for native save/reopen and rendered acceptance.
See [limits and evidence](../../docs/PB-1107_PB-1109_UNREAL_SURFACE_EVIDENCE.md).

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
and exclusive-execution adapter implemented separately in PB-1104.

Requests use the existing worker v1 contract. `probe-unreal-worker` and `verify-unreal-worker`
remain foundation operations; requests cannot select arbitrary Python code. The plugin bootstrap
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
names and texture imports now use the adapter described below; overview maps and customer-package stripping remain later E11 tasks. The
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

## Isolated jobs and texture imports (PB-1104–PB-1106)

`UnrealImportPlan.Create` in `PackageBuilder.Targets.Unreal` accepts existing validated Domain
identities, product cases and texture assignments, plus snapshot hashes/lengths. Its
`unreal-content-v1` profile uses one `/Game/<ProjectName>` Pack root with Meshes, Materials,
Textures, Maps and Documentation. Rigged cases add Skeletons, animated cases add Animations,
and sets/collections add Blueprints. Names are bounded ASCII identifiers; collisions are rejected
case-insensitively rather than silently renamed. Publisher branding remains configuration-owned.

The host serializes `ToJson()` into the input snapshot as `unreal-import-plan.json`. Texture
source references are relative to that input snapshot. `UnrealProjectClone` copies only reviewed
template sources, renames the project/Pack placeholder and holds an OS lease across every engine
process and cleanup. A second process cannot lease the same job; a second execution on the same
lease is rejected. The adapter must be used by every caller that launches an Editor against a
job; it is not an operating-system sandbox for manually launched external applications.
The caller supplies the preflight-verified executable and trusted argument array; input manifests
cannot choose executables or scripts. Timeout stops the owned process before disposal.

The worker supports `import-unreal-textures` and `verify-unreal-textures`. It validates the entire
bounded plan and input hashes before importing, never overwrites an existing asset, and applies
explicit settings through Unreal's TextureFactory and AssetImportTask. Albedo/emission use sRGB;
data and normal textures use linear space. Normal compression, data masks and colour compression
are explicit. Albedo/opacity preserve alpha; other roles discard it. OpenGL normal maps flip green
for Unreal; DirectX does not. Auto orientation must be resolved before planning. Compression is
applied before sRGB because Unreal can reset colour space during a compression change.

The separate verify operation loads saved Texture2D assets without repairing them, checks every
policy property and returns their exact hashes. The live harness verifies all nine policy cases,
unchanged input bytes, fresh-process reopen, duplicate-import rejection and cleanup:

```powershell
./scripts/Invoke-UnrealTextureIntegration.ps1
```

Evidence stays in `artifacts/PB-1104/<run-id>`; disposable projects and images are removed from
`artifacts/ue/<run-id>` in finally. Source fixtures and shared DDC remain. No UI, materials,
meshes, ORM packing, product rendering or final package export was added by that historical scope.
See [acceptance evidence](../../docs/PB-1104_PB-1106_UNREAL_IMPORT_EVIDENCE.md).

## Interactive preview and clean delivery (PB-1113 / PB-1114 / PB-1116)

Supply `unreal-preview-plan.json` from `UnrealPreviewPlan.Create` alongside the existing import,
surface and overview plans to request an interactive static-product overview. Install the
hash-verified editor helper into the disposable generation clone using
`scripts/unreal_preview_helper.py`; `Invoke-UnrealPreviewEditorBuild.ps1` builds it with the
documented VS Code-compatible tools. `create-unreal-overview` then adds the three product-local
Blueprint/UMG assets. Missing helper or unsupported contract values fail before generation.
Without that plan, the overview remains static.

Press Play in the delivered overview. Drag the studio to orbit and use the wheel to zoom.
Click the studio for keyboard arrows, Page Up/Page Down or +/- zoom. R resets the camera;
L resets lighting. Tab/Shift+Tab focuses controls and Enter/Space activates them. Focus Light
Direction for arrow-key adjustment, or drag the yaw/pitch sliders. H hides/restores the panel;
Show Controls remains available when hidden. The model's original transform is unchanged.

`prepare-unreal-delivery` validates and sanitizes metadata, returning a hash-bound clean inventory
for `UnrealProjectArchive`. `verify-unreal-delivery` independently verifies the extraction.
Neither worker nor authoring helper is delivered; the ZIP has no plugin/module dependency.
Run `scripts/Invoke-UnrealDeliveryIntegration.ps1 -Preview` for controls, captures, fresh
helper-free PIE, negative gates, preservation and automatic test-package cleanup.
See [delivery evidence](../../docs/PB-1113_PB-1114_PB-1116_UNREAL_DELIVERY_EVIDENCE.md).
