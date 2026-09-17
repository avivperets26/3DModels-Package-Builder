#requires -Version 7.2
<#
.SYNOPSIS
Builds and smoke-tests the editor-only UMG helper using the approved VS Code toolchain.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 900)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
$compiler = Join-Path $repo 'tools/msvc/2022/VC/Tools/MSVC/14.44.35207/bin/Hostx64/x64/cl.exe'
Assert-UnrealContainedPath $compiler $repo
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf) -or
    (Get-Item -LiteralPath $compiler).VersionInfo.ProductVersion -ne '14.44.35229.0') {
    throw 'Install the approved VS 2022 MSVC 14.44.35229 toolchain before building the helper.'
}
if (-not (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Microsoft SDKs\NETFXSDK\4.8')) {
    throw 'Unreal editor compilation requires the .NET Framework 4.8 SDK component.'
}
& (Join-Path $repo 'tools/blender/5.0.0/5.0/python/bin/python.exe') -B `
    (Join-Path $PSScriptRoot 'unreal_preview_editor_build.py') --editor $candidate.Executable --timeout $TimeoutSeconds
if ($LASTEXITCODE -ne 0) { throw 'Native editor helper verification failed; inspect artifacts/PB-1116.' }
