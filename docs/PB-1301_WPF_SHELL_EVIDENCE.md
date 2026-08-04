# PB-1301 WPF Shell Evidence

**Task:** PB-1301 — Create WPF shell, dependency injection, and navigation  
**Branch:** `feat/PB-1301-wpf-shell`  
**Lifecycle:** 🟡 **PROCESS**  
**Evidence date:** 2026-08-04

## Scope and rollover

PB-0213 is `[x]` / 🟢 **DONE**, absent from Active Work, and recorded exactly once in the Completion
Log. Final task commit `9b377e4ce7ad9fc750b2b3ff8a6115a5fc5f3fe2` merged through
[pull request #42](https://github.com/avivperets26/3DModels-Package-Builder/pull/42) as
`206d999661a96ebf71ccf3e1dcf87342114ff06a`. Required
[main workflow run 30910471888](https://github.com/avivperets26/3DModels-Package-Builder/actions/runs/30910471888)
completed successfully for that merge. The user explicitly confirmed publication and green required
`main` CI on 2026-08-04. No exception was used.

PB-1301 remains `[ ]` / 🟡 **PROCESS** until its user-controlled commit, merge, successful required
`main` CI, explicit completion confirmation, and next-task rollover are complete.

## Implemented boundary

- `ShellHostFactory` and `AddPackageBuilderShell` compose the local desktop lifetime and shell
  services with `Microsoft.Extensions.Hosting` dependency injection.
- `ShellNavigationService` owns deterministic, case-sensitive route navigation and publishes the
  current module without containing application or build policy.
- `MainWindowViewModel` exposes immutable shell identity, five modules, current selection, and
  navigation state through CommunityToolkit.Mvvm.
- Overview, Products, Build queue, Validation, and Settings provide distinct, honest preview states.
  Work owned by PB-1302 through PB-1315 is explicitly labelled unavailable rather than simulated.
- `App` catches an unexpected host/composition failure and displays a sanitized
  `APP_STARTUP_FAILED` dialog. `StartupFailureViewModel` never retains or exposes the original
  exception details.
- The shell uses accessible names, visible focus, standard list selection, keyboard navigation, and
  Alt+O/P/B/V/S access keys. PB-1316 owns the complete accessibility and failure-state audit.
- No import, texture display, 3D rendering, package generation, engine execution, publishing,
  telemetry, or network behavior is added.

## Dependency and project review

| Item | Result |
|---|---|
| CommunityToolkit.Mvvm | 8.4.2, centrally pinned, MIT, direct WPF dependency |
| Microsoft.Extensions.Hosting | 10.0.10, centrally pinned, MIT, direct WPF dependency |
| WPF tests | Dedicated `net10.0-windows` xUnit v3 project referencing only the WPF production project |
| Solution inventory | 16 exact projects; five approved test projects |
| Locked restore | All projects retain deterministic format-version-2 lock files |

The dependencies require no paid IDE, hosted service, cloud account, telemetry, or runtime network
access. Their notices are recorded in `docs/THIRD_PARTY_NOTICES.md`.

## Automated evidence

| Check | Result |
|---|---|
| Focused WPF tests | 15 passed, 0 failed, 0 skipped |
| Navigation and view models | Catalog, ordinal routes, notifications, null selection, disposal, and sanitized failure state covered |
| Composition | Service graph resolves; host builds, starts, and stops without an external service |
| Production assembly smoke test | `PackageBuilder.App.Wpf` loads with the expected identity |
| Solution architecture | 16 projects; 7 checks passed |
| Test-project policy | Five projects; 4 checks passed |
| Central build configuration | 16 projects and 9 centrally pinned packages; 8 checks passed |
| Release WPF build | Passed with 0 warnings and 0 errors |
| Focused production coverage | All 11 testable presentation/composition classes: 100% line and branch; complete WPF assembly: 86.14% line and 87.5% branch including native window lifecycle |
| Debug solution build | 16 projects; 0 warnings and 0 errors |
| Release solution build | 16 projects; 0 warnings and 0 errors |
| Full Release tests | 1,729 passed, 0 failed, 0 skipped across five projects |
| Repository baseline | 29 checks passed, 0 failed |
| Full Core CI | All nine stages passed in 3m 21.654s |
| Dependency vulnerability audit | No vulnerable direct or transitive package reported for any of 16 projects |
| Formatting and diff policy | .NET info-level formatting, Ruff lint/format, and `git diff --check` passed |

Ignored Cobertura evidence is retained beneath `artifacts/PB-1301/coverage`. Testable presentation,
navigation, and composition classes are covered independently of the Windows window surface. XAML
compilation and direct visual inspection cover the actual native window; no unsupported claim of
fully automated native UI coverage is made.

## Manual visual evidence

The Release application launched successfully and remained alive with a native main window titled
`Package Builder` at 1280 × 800. Direct inspection confirmed readable light content, dark primary
navigation, visible selection/focus treatment, local-only status, explicit preview language, and no
clipped content at the tested size. Windows UI Automation selected Products and confirmed that the
`PRODUCT WORKSPACE` content appeared. Captures are retained under ignored
`artifacts/PB-1301/wpf-shell-window.png` and `wpf-shell-products.png`.

Run the same visual checkpoint from the repository root:

```powershell
& .\scripts\Enter-PackageBuilderEnvironment.ps1
dotnet run --project .\src\PackageBuilder.App.Wpf\PackageBuilder.App.Wpf.csproj
```

Use Tab, Shift+Tab, arrow keys, or Alt+O/P/B/V/S to navigate. This shell is the first visual product
checkpoint, but it intentionally does not yet accept a model or render a 3D preview.

## Remaining gates

All local implementation and validation gates are complete. PB-1301 still requires user-controlled
staging and commit, task-branch publication, merge into `main`, successful required `main` CI,
explicit user confirmation, and rollover synchronization at the start of PB-1302.
