<#
.SYNOPSIS
Exercises source-integrity and binary-exclusion failures in a disposable repository-local fixture.
.DESCRIPTION
Copies only validation inputs beneath artifacts. Mutations never touch production inputs;
each case requires its expected diagnostic rather than accepting any failure as success.
#>
[CmdletBinding()]
param([string]$RepositoryRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path (Split-Path $MyInvocation.MyCommand.Path -Parent) '..'
}
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$fixture = Join-Path $root ('artifacts/PB-0015/guards-' + [Guid]::NewGuid().ToString('N'))
$validator = Join-Path $root 'scripts/Test-JsonSchemaDistribution.ps1'
$paths = @('Directory.Packages.props', 'third_party/json-everything/source-manifest.json')
foreach ($directory in @('src', 'tests', 'third_party')) {
    $paths += @(Get-ChildItem -LiteralPath (Join-Path $root $directory) -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            ($_.Name -eq 'packages.lock.json' -or $_.Extension -eq '.csproj' -or
                $_.FullName -match '[\\/]third_party[\\/]json-everything[\\/]upstream[\\/]')
        } | ForEach-Object { $_.FullName.Substring($root.TrimEnd('\', '/').Length + 1) })
}
foreach ($path in $paths) {
    $destination = Join-Path $fixture $path
    [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
    [IO.File]::Copy((Join-Path $root $path), $destination)
}
& $validator -RepositoryRoot $fixture

function Assert-RejectedMutation {
    param([string]$RelativePath, [scriptblock]$Mutate, [string]$Diagnostic)
    $path = Join-Path $fixture $RelativePath
    $original = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
    try {
        & $Mutate $path
        $message = $null
        try { & $validator -RepositoryRoot $fixture } catch { $message = $_.Exception.Message }
        if ($null -eq $message -or -not $message.Contains($Diagnostic)) {
            throw "Expected '$Diagnostic'; received '$message'."
        }
        Write-Host "[PASS] Rejects $Diagnostic"
    }
    finally {
        if ($null -eq $original) { Remove-Item -LiteralPath $path -Force }
        else { [IO.File]::WriteAllBytes($path, $original) }
    }
}

Assert-RejectedMutation 'third_party/json-everything/upstream/LICENSE' {
    param($path)
    [IO.File]::AppendAllText($path, 'tampered')
} 'JSON source hash mismatch'

Assert-RejectedMutation 'third_party/json-everything/upstream/unreviewed.cs' {
    param($path)
    [IO.File]::WriteAllText($path, '// Unreviewed source')
} 'Unexpected JSON source file'

Assert-RejectedMutation 'third_party/json-everything/source-manifest.json' {
    param($path)
    $text = [IO.File]::ReadAllText($path).Replace('"LICENSE":', '"../LICENSE":')
    [IO.File]::WriteAllText($path, $text)
} 'escaping path'

Assert-RejectedMutation 'src/PackageBuilder.Contracts/packages.lock.json' {
    param($path)
    $lock = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $lock.dependencies.'net10.0' | Add-Member -Force -NotePropertyName 'JsonSchema.Net' -NotePropertyValue @{ type = 'Transitive'; resolved = '9.4.0' }
    [IO.File]::WriteAllText($path, ($lock | ConvertTo-Json -Depth 30))
} 'Publisher JSON binary is forbidden'

Assert-RejectedMutation 'src/PackageBuilder.Contracts/PackageBuilder.Contracts.csproj' {
    param($path)
    $text = [IO.File]::ReadAllText($path).Replace('</Project>', '<ItemGroup><PackageReference Include="JsonPointer.Net" /></ItemGroup></Project>')
    [IO.File]::WriteAllText($path, $text)
} 'Publisher JSON PackageReference is forbidden'

Assert-RejectedMutation 'Directory.Packages.props' {
    param($path)
    $text = [IO.File]::ReadAllText($path).Replace('</Project>', '<ItemGroup><PackageVersion Include="Json.More.Net" Version="3.0.1" /></ItemGroup></Project>')
    [IO.File]::WriteAllText($path, $text)
} 'Publisher JSON central package is forbidden'

& $validator -RepositoryRoot $fixture
Write-Host 'JSON distribution guard regression tests: 6 rejected mutations, clean fixture restored.' -ForegroundColor Green
