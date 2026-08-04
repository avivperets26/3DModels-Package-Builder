# PB-0301 Tool Version and Installation Model Evidence

**Task:** PB-0301 — Implement tool-installation and semantic engine-version models
**Branch:** `feat/PB-0301-tool-version-models`
**Lifecycle:** 🟡 **PROCESS**
**Evidence date:** 2026-08-04

## Scope

PB-0301 adds immutable renderer-independent tool metadata under
`PackageBuilder.Domain.Tools`. It represents .NET, Blender, Unity, and Unreal; canonical vendor
versions; stable and preview channels; executable paths; contained or external installation state;
and whether an installation is selected.

The Domain performs no filesystem access, process execution, registry lookup, networking,
installation, licence acceptance, persistence, catalog refresh, candidate promotion, or UI work.
PB-0302 through PB-0304 own discovery and executable verification. PB-0305 and PB-0306 own release
catalogs and Latest Approved Stable selection.

## Public Model

| Type | Purpose |
|---|---|
| `ToolKind` | Exactly .NET, Blender, Unity, and Unreal. |
| `ToolReleaseChannel` | Stable or Preview, derived from the canonical version. |
| `ToolVersion` | Immutable tool-specific canonical value with deterministic semantic comparison. |
| `ToolInstallationContainment` | Records whether the executable is contained or external. |
| `ToolInstallation` | Immutable matching tool/version/path/root/selection metadata. |
| `ToolModelValidationResult<T>` | Structured non-throwing expected validation result. |

Production functions include XML documentation explaining their purpose and boundary.

## Version Rules

- .NET uses three numeric components and optional lowercase `preview` or `rc` identifiers followed
  only by canonical numeric identifiers.
- Blender uses three numeric components and optional lowercase `alpha`, `beta`, or `rc` identifiers
  followed only by canonical numeric identifiers.
- Unreal uses three numeric components; preview versions require lowercase `preview` or `rc` plus
  at least one canonical numeric identifier.
- Unity uses `<major>.<minor>.<patch><a|b|f|p><revision>`. Alpha and beta are Preview; final and
  patch releases are Stable.
- Leading zeroes, build metadata, unknown labels, missing required numeric components, overflow,
  surrounding whitespace, and noncanonical casing are rejected.
- Comparison is numeric and vendor-aware, so `10` sorts after `9`; stable sorts after prerelease;
  Unity stages sort `a`, `b`, `f`, then `p`. Comparison and hashing are culture-independent.

## Containment and Selection Rules

- Executable and tools-root values must be canonical fully qualified Windows paths.
- The approved root must end in `tools`; a trailing directory separator is normalized away.
- Relative, forward-slash, traversal, noncanonical, whitespace-padded, and invalid-character paths
  are rejected without filesystem access.
- A strict descendant of the approved root is Contained. Every other canonical path is External.
- External installations are retained as informational detections but cannot be selected.
- Selection and deselection create immutable copies. Tool and version must match.

## Tests and Coverage

The focused xUnit v3 suite covers all four vendor grammars and channels, hostile and boundary
inputs, numeric/prerelease/Unity ordering, every comparison operator including null behavior,
Turkish-culture independence, stable equality/hashing, containment boundaries, invalid Windows
paths, selection rejection, immutable copies, and tool/version mismatch.

Focused coverage is collected beneath ignored `artifacts/PB-0301`. All executable production
classes introduced by PB-0301 must report 100% line and branch coverage before publication.

## Validation Results

| Validation | Result |
|---|---|
| Focused PB-0301 tests | Pass; 57 passed, 0 failed, 0 skipped. |
| Focused PB-0301 production coverage | Pass; all executable production classes report 100% line and branch coverage. |
| Complete Domain suite | Pass; 846 passed, 0 failed, 0 skipped. |
| Complete five-project test portfolio | Pass; 1,786 passed, 0 failed, 0 skipped. |
| Debug and Release solution builds | Pass; 16 projects, 0 warnings, 0 errors in each configuration. |
| Repository baseline | Pass; 29 checks passed, 0 failed. |
| Full Core CI | Pass; all 9 fail-closed stages passed in 3 minutes 35 seconds. |
| Formatting, diff, security, and prohibited-content scans | Pass. |

## Manual Visual Test

Not applicable. PB-0301 is a pure Domain model and does not change the WPF shell or render assets.
PB-1301 remains the current runnable visual checkpoint.

## Remaining Gates

PB-0301 remains `[ ]` / 🟡 **PROCESS** and absent from the Completion Log. All local implementation
and validation gates are complete. User-controlled commit and branch push, merge into and push of
`main`, successful required `main` CI, explicit user completion confirmation, and next-task
rollover remain. No exception is used.
