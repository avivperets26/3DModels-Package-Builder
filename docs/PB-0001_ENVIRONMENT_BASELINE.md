# PB-0001 Environment Baseline

**Recorded:** 2026-07-22
**Project root:** `C:\Dev\PackageBuilder`
**Task status:** 🟡 **PROCESS** — local installation and revalidation pass; PB-0001 remains open until the remaining user-controlled completion gates are satisfied.

## Containment Policy

Every Package Builder project file, download, tool, log, runtime-data file, cache, and generated artifact must resolve beneath `C:\Dev\PackageBuilder`. A sibling data root is prohibited. The repository-local generated directories are excluded from Git by `.gitignore`.

The .NET CLI must be entered through:

```powershell
. .\scripts\Enter-PackageBuilderEnvironment.ps1
```

This selects the repository-local SDK and redirects the .NET CLI home, NuGet packages, NuGet HTTP cache, and temporary files beneath `runtime-data`.

## Approved .NET Baseline

Microsoft's current .NET 10 release metadata reports .NET 10 as an active LTS channel and SDK `10.0.302` as the latest SDK. The release date is 2026-07-14 and the included runtime is `10.0.10`.

| Item | Value |
|---|---|
| SDK | `10.0.302` |
| Runtime | `10.0.10` |
| Architecture | Windows x64 |
| Install root | `tools\dotnet\10.0.302` |
| Download root | `downloads\dotnet\10.0.302` |
| Verification logs | `logs\setup\PB-0001` |
| Official release metadata | `downloads\dotnet\10.0.302\dotnet-10.0-releases.json` |

Official source: <https://dotnet.microsoft.com/en-us/download/dotnet/10.0>

## Download Integrity

Both staged downloads match the SHA-512 values in Microsoft's official .NET 10 release metadata.

| File | Bytes | SHA-512 |
|---|---:|---|
| `dotnet-sdk-10.0.302-win-x64.exe` | 213,804,008 | `FFA847D86755033A4E2C8DD19AB3B0D9C8AE129E1E59CEF460F792CCED6319C69F730B96E05C5BB88BA906094F332BF5232D4C417605789F03A310DD8F3D22C2` |
| `dotnet-sdk-10.0.302-win-x64.zip` | 297,545,270 | `7D170ED75FA9AF34C00646621D92011DBD71943952E2787CD15DF9BE78E6452B55DADEF34D7EFF77B802E6AF4959E071A55855AC649AFEAC70901C3A2A258716` |

The Windows installer has a valid Microsoft `.NET` Authenticode signature. The extracted `dotnet.exe` also has a valid Microsoft `.NET` signature.

The verified ZIP contains 5,611 files and 798,277,746 uncompressed bytes. Every installed file matches its ZIP entry by path, length, and SHA-256. The transfer into the project root matched all 5,619 staged files and 1,309,628,020 bytes by SHA-256 before the old transfer folder was removed.

## Required Command Results

After entering the environment:

```text
> dotnet --version
10.0.302

> dotnet --list-sdks
10.0.302 [C:\Dev\PackageBuilder\tools\dotnet\10.0.302\sdk]
```

`dotnet --info` reports:

- SDK `10.0.302` at `C:\Dev\PackageBuilder\tools\dotnet\10.0.302\sdk\10.0.302`.
- Host and Windows desktop runtime `10.0.10`.
- RID `win-x64`.
- `DOTNET_ROOT` and `DOTNET_CLI_HOME` beneath the project root.
- No installed optional workloads.

Full outputs are retained in `logs\setup\PB-0001`.

A repository-local CLI smoke test created, restored, and built a `net10.0-windows` WPF application in Release configuration with zero warnings and zero errors. Its generated project and build output remain beneath `artifacts\verification\PB-0001`, and its logs remain beneath `logs\setup\PB-0001`.

## Free and Editor-Independent Development

The baseline uses the no-cost .NET SDK, PowerShell, Git, and Visual Studio Code. WPF projects are created, restored, built, tested, and run with `dotnet` commands and repository scripts. Paid Visual Studio is optional and is not a development prerequisite. `dotnet new list wpf` confirms that the SDK provides the WPF application and library templates required for CLI/VS Code development, and the successful smoke build proves the CLI path works on this machine.

No mandatory Package Builder component may require a paid software edition, paid subscription, or paid hosted service. Optional remote hosting and CI integrations must have a no-cost path and cannot block local development or operation.

## Revalidation Evidence — 2026-07-22

PB-0001 was revalidated on branch `chore/PB-0001-dotnet-10-sdk` at commit `fc34bffff838cac41198940ed54b91b25c33f838` after entering the repository environment with `scripts\Enter-PackageBuilderEnvironment.ps1`.

| Validation | Result | Evidence |
|---|---|---|
| `dotnet --version` | Pass — exactly `10.0.302` | `logs\setup\PB-0001\revalidation-dotnet-version.log` |
| `dotnet --list-sdks` | Pass — the only SDK is `10.0.302 [C:\Dev\PackageBuilder\tools\dotnet\10.0.302\sdk]` | `logs\setup\PB-0001\revalidation-dotnet-list-sdks.log` |
| `dotnet --info` and runtimes | Pass — SDK `10.0.302`, RID `win-x64`, and .NET/Windows Desktop runtimes `10.0.10` resolve beneath the repository-local SDK root | `logs\setup\PB-0001\revalidation-dotnet-info.log`; `revalidation-dotnet-list-runtimes.log` |
| `global.json` | Pass — version `10.0.302`, `rollForward` is `disable`, and `allowPrerelease` is `false` | `global.json` and `revalidation-command-hash-signature-build-summary.json` |
| Official installer SHA-512 | Pass — `FFA847D86755033A4E2C8DD19AB3B0D9C8AE129E1E59CEF460F792CCED6319C69F730B96E05C5BB88BA906094F332BF5232D4C417605789F03A310DD8F3D22C2` | `revalidation-download-sha512.log` |
| Official ZIP SHA-512 | Pass — `7D170ED75FA9AF34C00646621D92011DBD71943952E2787CD15DF9BE78E6452B55DADEF34D7EFF77B802E6AF4959E071A55855AC649AFEAC70901C3A2A258716` | `revalidation-download-sha512.log` |
| Authenticode | Pass — installer and installed `dotnet.exe` both report `Valid`, signed by `.NET, Microsoft Corporation` | `revalidation-authenticode.log` |
| Installed SDK versus approved ZIP | Pass — 5,611 ZIP files, 5,611 installed files, and 798,277,746 uncompressed bytes; zero missing, extra, length-mismatched, path-invalid, or SHA-256-mismatched files | `revalidation-installed-sdk-integrity.log`; `revalidation-installed-sdk-integrity.json` |
| Containment | Pass — `DOTNET_ROOT`, `DOTNET_CLI_HOME`, NuGet package/cache paths, `TEMP`, `TMP`, resolved `dotnet`, logs, downloads, tools, artifacts, and runtime data are beneath `C:\Dev\PackageBuilder`; multilevel lookup is disabled | `revalidation-containment.log` |
| WPF CLI templates | Pass — WPF application and class-library templates are available | `revalidation-dotnet-wpf-templates.log` |
| Clean WPF CLI restore/build | Pass — a fresh `net10.0-windows` project beneath `artifacts\verification\PB-0001\revalidation-wpf-20260722T185712Z` restored and built in Release with warnings treated as errors; result was 0 warnings and 0 errors | `revalidation-wpf-new.log`; `revalidation-wpf-restore.log`; `revalidation-wpf-build.log` |
| Required logs | Pass — all required command, hash, signature, integrity, containment, template, restore, build, ignore, and repository-check evidence files exist beneath `logs\setup\PB-0001` | `revalidation-logs-and-git-ignore.json`; `revalidation-repository-checks.json` |
| Git exclusion | Pass — tools, downloads, logs, runtime data, and generated WPF artifacts are ignored; none are tracked | `revalidation-logs-and-git-ignore.json` |
| Old staging transfer | Pass — the previously verified external PB-0001 staging transfer remains absent; its personal absolute path is intentionally omitted from public documentation | `revalidation-logs-and-git-ignore.json` |
| Repository safety and documentation | Pass — no secret, absolute personal path, prohibited tracked file, Markdown, task-ID, dependency-reference, completion-state, or diff-integrity finding | `revalidation-repository-checks.json` |

The combined command, hash, signature, containment, template, and build summary is retained at `logs\setup\PB-0001\revalidation-command-hash-signature-build-summary.json`. All revalidation logs and generated build artifacts are beneath the project root and excluded from Git.

## One-Time GitHub CI Bootstrap Exception

- **Scope:** PB-0001 only, and only the missing GitHub CI-pass gate for this machine-local repository-contained SDK verification task.
- **Reason:** PB-0001 establishes the SDK required to build later CI infrastructure, while no GitHub workflow currently exists and hosted CI cannot reproduce the already verified machine-local installation layout.
- **Approval:** Explicitly approved by the user on 2026-07-22.
- **Unchanged requirements:** every local PB-0001 acceptance check, documentation update, user-controlled commit and push, integration into `main`, and explicit user completion confirmation remains required.
- **No precedent:** this exception does not weaken CI requirements for PB-0002 or any later task and cannot be reused without a new explicit user approval and documented scope.
