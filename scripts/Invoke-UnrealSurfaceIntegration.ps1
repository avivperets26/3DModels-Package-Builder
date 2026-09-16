#requires -Version 7.2
<#
.SYNOPSIS
Validates host ORM pixels and native Unreal material/static-mesh save and reopen.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 600)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
& (Join-Path $repo 'tools/blender/5.0.0/5.0/python/bin/python.exe') -B (Join-Path $PSScriptRoot 'unreal_surface_integration.py') --editor $candidate.Executable --timeout $TimeoutSeconds
if ($LASTEXITCODE -ne 0) { throw 'Unreal surface integration failed; inspect artifacts/PB-1107.' }
