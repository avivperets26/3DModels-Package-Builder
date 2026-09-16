#requires -Version 7.2
<#
.SYNOPSIS
Validates native overview generation, rendering, media quality, redirector repair and project gates.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 900)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
& (Join-Path $repo 'tools/blender/5.0.0/5.0/python/bin/python.exe') -B (Join-Path $PSScriptRoot 'unreal_overview_integration.py') --editor $candidate.Executable --timeout $TimeoutSeconds
if ($LASTEXITCODE -ne 0) { throw 'Unreal overview integration failed; inspect artifacts/PB-1110.' }
