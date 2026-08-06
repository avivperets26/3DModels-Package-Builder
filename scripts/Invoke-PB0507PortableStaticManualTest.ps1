[CmdletBinding()]
param(
    [switch]$OpenFolder
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptPath = [string]$MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptPath)) {
    throw 'The executing script path is unavailable.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $scriptPath -Parent) '..'))
$environmentScript = Join-Path $repositoryRoot 'scripts\Enter-PackageBuilderEnvironment.ps1'
$testProject = Join-Path $repositoryRoot 'tests\PackageBuilder.Targets.Portable.Tests\PackageBuilder.Targets.Portable.Tests.csproj'
$latestPointer = Join-Path $repositoryRoot 'artifacts\PB-0507\manual\latest.txt'

# The retained-output flag is scoped to this process and restored for repeatable interactive use.
$previousRetain = [Environment]::GetEnvironmentVariable('PACKAGEBUILDER_PB0507_RETAIN_OUTPUT', 'Process')
try {
    . $environmentScript
    [Environment]::SetEnvironmentVariable('PACKAGEBUILDER_PB0507_RETAIN_OUTPUT', '1', 'Process')

    & dotnet restore $testProject --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'PB-0507 manual-test restore failed.'
    }

    & dotnet test $testProject `
        --no-restore `
        --configuration Release `
        --filter 'FullyQualifiedName~CompleteStaticManifestBuildsValidatedAtomicallyPromotedRelease'
    if ($LASTEXITCODE -ne 0) {
        throw 'PB-0507 manual test failed. Review the test output above.'
    }
}
finally {
    [Environment]::SetEnvironmentVariable(
        'PACKAGEBUILDER_PB0507_RETAIN_OUTPUT',
        $previousRetain,
        'Process')
}

if (-not (Test-Path -LiteralPath $latestPointer -PathType Leaf)) {
    throw 'The PB-0507 test passed but did not write its retained-output pointer.'
}

$values = @{}
foreach ($line in Get-Content -LiteralPath $latestPointer -Encoding UTF8) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) {
        $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
    }
}

foreach ($required in @('workspace', 'release', 'report', 'log')) {
    if (-not $values.ContainsKey($required) -or
        -not (Test-Path -LiteralPath $values[$required])) {
        throw "The retained PB-0507 output is missing '$required'."
    }
}

$inspectionRoot = Join-Path $values.workspace 'inspection'
if (-not (Test-Path -LiteralPath $inspectionRoot)) {
    Expand-Archive -LiteralPath $values.release -DestinationPath $inspectionRoot
}

Write-Host ''
Write-Host 'PB-0507 portable static manual test passed.' -ForegroundColor Green
Write-Host "Workspace: $($values.workspace)"
Write-Host "Promoted release: $($values.release)"
Write-Host "Extracted package: $inspectionRoot"
Write-Host "JSON validation report: $($values.report)"
Write-Host "JSON Lines job log: $($values.log)"
Write-Host ''
Write-Host 'Inspect the extracted README and import StoneArch.fbx into Blender to view the cube.'

if ($OpenFolder) {
    Invoke-Item -LiteralPath $values.workspace
}
