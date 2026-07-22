# PB-0001 Environment Baseline

**Recorded:** 2026-07-22
**Project root:** `C:\Dev\PackageBuilder`
**Task status:** Local installation and verification complete; PB-0001 remains open until every backlog completion gate is satisfied.

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
