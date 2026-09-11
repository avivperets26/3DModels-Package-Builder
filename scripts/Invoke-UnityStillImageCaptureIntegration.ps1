[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
. (Join-Path $PSScriptRoot 'UnityTestArtifacts.Common.ps1')
$run = Join-Path $repository ('artifacts/u/' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$project = Join-Path $run 'p'
$process = $null
$oldEnvironment = @{}
try {
    New-Item -ItemType Directory -Path $project -Force | Out-Null
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        Copy-Item -LiteralPath (Join-Path $repository "engine-templates/unity/6000.3/$folder") -Destination $project -Recurse
    }
    $fixtures = Join-Path $project 'Assets/CaptureTests'
    New-Item -ItemType Directory -Path $fixtures -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repository 'tests/fixtures/portable/static-vertical-slice/source/StoneArch.fbx') -Destination $fixtures
    $cache = Join-Path $repository 'runtime-data/unity/6000.3.10f1'
    $environment = @{ TEMP = "$cache/temp"; TMP = "$cache/temp"; UPM_CACHE_PATH = "$cache/upm-cache";
        UPM_NPM_CACHE_PATH = "$cache/upm-cache"; UPM_CONFIG_PATH = "$cache/upm-config" }
    foreach ($key in $environment.Keys) {
        New-Item -ItemType Directory -Path $environment[$key] -Force | Out-Null
        $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $environment[$key], 'Process')
    }
    $arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod',
        'PackageBuilder.UnityWorker.Editor.UnityStillImageCaptureIntegration.Run', '-logFile', ('"' + "$run/capture.log" + '"'))
    Write-Host "Unity capture integration: $run"
    $process = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe' `
        -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(900000)) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
        throw 'Unity capture integration timed out after 15 minutes.'
    }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "$run/capture-receipts.json")) {
        throw "Unity capture failed; inspect $run/capture.log"
    }
    Write-Host "PASS: five views and repeat/negative checks; evidence $run"
}
finally {
    foreach ($key in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $oldEnvironment[$key], 'Process') }
    if ($null -ne $process -and $process.HasExited) {
        Stop-CompletedUnityTestHub -RepositoryRoot $repository -ProjectPath $project -CompletedEditorProcessId $process.Id
    }
    Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run
}
