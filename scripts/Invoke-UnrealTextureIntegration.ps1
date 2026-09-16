#requires -Version 7.2
<#
.SYNOPSIS
Validates leased cloning, product folders and all texture policies using the signed candidate.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 600)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
$python = Join-Path $repo 'tools/blender/5.0.0/5.0/python/bin/python.exe'
& $python -B (Join-Path $PSScriptRoot 'unreal_import_integration.py') `
    --editor $candidate.Executable --timeout $TimeoutSeconds
if ($LASTEXITCODE -ne 0) { throw 'Unreal texture integration failed; inspect artifacts/PB-1104.' }
