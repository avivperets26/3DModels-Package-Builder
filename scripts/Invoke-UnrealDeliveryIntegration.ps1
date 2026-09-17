#requires -Version 7.2
<#
.SYNOPSIS
Creates a clean Unreal ZIP, extracts it safely, validates fresh native reopening and removes test outputs.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 900, [switch]$Preview, [switch]$Fab)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
[string[]]$previewArguments = if ($Preview) { @('--preview') } else { @() }
if ($Fab) { $previewArguments += '--fab' }
& (Join-Path $repo 'tools/blender/5.0.0/5.0/python/bin/python.exe') -B (Join-Path $PSScriptRoot 'unreal_delivery_integration.py') --editor $candidate.Executable --timeout $TimeoutSeconds @previewArguments
if ($LASTEXITCODE -ne 0) {
    $taskEvidence = if ($Fab) { 'PB-1115' } else { 'PB-1113' }
    throw "Unreal delivery integration failed; inspect artifacts/$taskEvidence."
}
