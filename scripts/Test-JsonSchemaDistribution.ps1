<#
.SYNOPSIS
Verifies the pinned MIT source inventory and rejects publisher binaries in restore inputs.
.DESCRIPTION
Runs offline. Hashes are calculated after CRLF-to-LF normalization, the only permitted
import transformation. Unknown files, changed sources, links and escaping paths fail closed.
RepositoryRoot supports isolated negative fixtures as well as the real checkout.
#>
[CmdletBinding()]
param([string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path (Split-Path $MyInvocation.MyCommand.Path -Parent) '..'
}
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$vendor = Join-Path $root 'third_party/json-everything'
$upstream = [IO.Path]::GetFullPath((Join-Path $vendor 'upstream'))
$manifest = Get-Content -LiteralPath (Join-Path $vendor 'source-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.repository -cne 'https://github.com/json-everything/json-everything' -or
    $manifest.commit -cne '399f198431f65cf6896fe6038f833ef6d0b27a39' -or
    $manifest.archiveSha256 -cne '4fe5fcc230fe5fa81bff6b63379990f44b0ac94904d33048878a4e4c6e48593e' -or
    $manifest.normalization -cne 'CRLF to LF only') {
    throw 'JSON source provenance differs from the reviewed PB-0015 pin.'
}
$entries = @($manifest.files.PSObject.Properties)
if ($entries.Count -ne 181) { throw 'JSON source inventory must contain exactly 181 files.' }
$prefix = $upstream + [IO.Path]::DirectorySeparatorChar
$expected = @{}
$sha = [Security.Cryptography.SHA256]::Create()
try {
    foreach ($entry in $entries) {
        if ($entry.Name -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($entry.Name)) {
            throw 'JSON source manifest contains an escaping path.'
        }
        $path = [IO.Path]::GetFullPath((Join-Path $upstream $entry.Name))
        if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'JSON source path escapes the vendor directory.'
        }
        # Inspect ancestors before reading to avoid following a link outside the checkout.
        $item = Get-Item -LiteralPath $path -Force
        $ancestor = $item
        while ($null -ne $ancestor -and $ancestor.FullName.Length -ge $root.Length) {
            if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "JSON source path contains a reparse point: $($entry.Name)"
            }
            $ancestor = if ($ancestor -is [IO.DirectoryInfo]) { $ancestor.Parent } else { $ancestor.Directory }
        }
        if ($item.PSIsContainer -or $item.Length -gt 1MB) { throw "Invalid JSON source file: $($entry.Name)" }
        $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path)).Replace("`r`n", "`n")
        $actual = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-', '').ToLowerInvariant()
        if ($actual -cne $entry.Value) { throw "JSON source hash mismatch: $($entry.Name)" }
        $expected[$path] = $true
    }
}
finally { $sha.Dispose() }
foreach ($item in Get-ChildItem -LiteralPath $upstream -Recurse -Force) {
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked JSON source content is prohibited.' }
    if (-not $item.PSIsContainer -and -not $expected.ContainsKey($item.FullName)) {
        throw "Unexpected JSON source file: $($item.Name)"
    }
}

$publisherIds = @('JsonSchema.Net', 'JsonPointer.Net', 'Json.More.Net')
$lockCount = 0
foreach ($directory in @('src', 'tests', 'third_party')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $directory) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
        if ($file.Name -eq 'packages.lock.json') {
            $lockCount++
            $lock = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($framework in $lock.dependencies.PSObject.Properties) {
                foreach ($dependency in $framework.Value.PSObject.Properties) {
                    if ($publisherIds -contains $dependency.Name -and $dependency.Value.type -ne 'Project') {
                        throw "Publisher JSON binary is forbidden: $($file.FullName): $($dependency.Name)"
                    }
                }
            }
        }
        elseif ($file.Extension -eq '.csproj') {
            $project = [xml](Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8)
            foreach ($reference in $project.SelectNodes('//PackageReference')) {
                if ($publisherIds -contains $reference.GetAttribute('Include') -or
                    $publisherIds -contains $reference.GetAttribute('Update')) {
                    throw "Publisher JSON PackageReference is forbidden: $($file.FullName)"
                }
            }
        }
    }
}
if ($lockCount -ne 21) { throw "Expected 21 source/application/test lock files; found $lockCount." }
$central = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw -Encoding UTF8)
foreach ($package in $central.SelectNodes('//PackageVersion')) {
    if ($publisherIds -contains $package.GetAttribute('Include')) { throw 'Publisher JSON central package is forbidden.' }
}
Write-Host 'JSON Schema distribution: 181 pinned MIT files verified; 21 lock graphs exclude publisher binaries.' -ForegroundColor Green
