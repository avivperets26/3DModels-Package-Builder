[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptPath = [string]$MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'RepositoryRoot was not supplied and the executing script path is unavailable.'
    }

    $RepositoryRoot = Join-Path (Split-Path $scriptPath -Parent) '..'
}

$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$templateRoot = Join-Path $repositoryRootPath 'engine-templates\unity\6000.3'
if ([string]::IsNullOrWhiteSpace($UnityExecutable)) {
    $UnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
}
$unityPath = [IO.Path]::GetFullPath($UnityExecutable)
if (-not (Test-Path -LiteralPath $unityPath -PathType Leaf)) {
    throw "Approved Unity executable is unavailable: $unityPath"
}

& (Join-Path $repositoryRootPath 'scripts\Test-UnityProductPolicies.ps1') `
    -RepositoryRoot $repositoryRootPath

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runRoot = Join-Path $repositoryRootPath "artifacts\u\$runId"
$cloneRoot = Join-Path $runRoot 'p'
$logPath = Join-Path $runRoot 'unity-product-tests.log'
$reopenLogPath = Join-Path $runRoot 'unity-product-reopen.log'
$reopenRetryLogPath = Join-Path $runRoot 'unity-product-reopen-retry.log'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'

# Unity 6000.3 still reaches package assemblies through Mono APIs that can fail at legacy Windows
# path lengths even when the operating-system long-path policy is enabled. Keep the generated
# project short enough for the longest reviewed path in the pinned package graph.
$maximumLegacyCompatiblePathLength = 248
$representativePackagePath = Join-Path $cloneRoot `
    'Library\PackageCache\com.unity.collections@000000000000\Unity.Collections.LowLevel.ILSupport\source~\Unity.Collections.LowLevel.ILSupport.CodeGen\Unity.Collections.LowLevel.ILSupport.CodeGen.asmdef.meta'
if ($representativePackagePath.Length -gt $maximumLegacyCompatiblePathLength) {
    throw "The Unity integration clone is too deep for legacy package assembly APIs: $($representativePackagePath.Length) characters."
}

New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $temporaryRoot, $upmCacheRoot, $upmConfigRoot -Force |
    Out-Null
foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cloneRoot -Recurse
}

# Use the repository-authored normalized FBX rather than a mock ModelImporter input. Its camera,
# light, two meshes, and embedded materials make the PB-0609 through PB-0611 checks observable.
$modelTestRoot = Join-Path $cloneRoot 'Assets\PBModelTests'
foreach ($folderName in @('Source', 'Meshes', 'Materials', 'Prefabs')) {
    New-Item -ItemType Directory -Path (Join-Path $modelTestRoot $folderName) -Force | Out-Null
}
$staticFbxFixture = Join-Path $repositoryRootPath `
    'tests\fixtures\portable\static-vertical-slice\source\StoneArch.fbx'
if (-not (Test-Path -LiteralPath $staticFbxFixture -PathType Leaf)) {
    throw "Static Unity FBX fixture is missing: $staticFbxFixture"
}
Copy-Item -LiteralPath $staticFbxFixture -Destination (Join-Path $modelTestRoot 'Source\StoneArch.fbx')

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
    PACKAGEBUILDER_RETAIN_UNITY_TEST_ASSETS = '1'
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityProductEditorIntegrationTests.Run',
        '-logFile', $logPath
    )
    $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow

    $log = if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
    }
    else {
        throw 'Unity product integration log is missing.'
    }

    if ($process.ExitCode -ne 0 -or -not $log.Contains('PACKAGEBUILDER_UNITY_PRODUCT_TESTS_PASS')) {
        $tail = (@(Get-Content -LiteralPath $logPath -Tail 120) -join [Environment]::NewLine)
        throw "Unity product integration failed with exit code $($process.ExitCode).`n$tail"
    }
    if ($log -match '(?m)(error CS\d+|PACKAGEBUILDER_UNITY_PRODUCT_TESTS_FAIL)') {
        throw 'Unity product integration log contains a compilation or test failure.'
    }

    # Match the project marker to the actual upgrader inventory in the pinned package. A stale
    # marker opens a modal URP material-upgrade dialog during the first interactive Editor frame,
    # even when the project contains no customer material assets.
    $urpProjectSettingsPath = Join-Path $cloneRoot 'ProjectSettings\URPProjectSettings.asset'
    $materialPostprocessorPaths = @(Get-ChildItem -LiteralPath (Join-Path $cloneRoot `
                'Library\PackageCache') -Recurse -File -Filter 'MaterialPostprocessor.cs' |
        Where-Object {
            $_.FullName -match '[\\/]com\.unity\.render-pipelines\.universal@[^\\/]+[\\/]Editor[\\/]AssetPostProcessors[\\/]MaterialPostprocessor\.cs$'
        })
    if ($materialPostprocessorPaths.Count -ne 1) {
        throw "Expected one pinned URP MaterialPostprocessor.cs; found $($materialPostprocessorPaths.Count)."
    }
    $materialPostprocessorSource = Get-Content -LiteralPath $materialPostprocessorPaths[0].FullName `
        -Raw -Encoding UTF8
    $upgraderDeclaration = [regex]::Match($materialPostprocessorSource,
        'k_Upgraders\s*=\s*\{(?<upgraders>[^}]*)\}')
    if (-not $upgraderDeclaration.Success) {
        throw 'Pinned URP material upgrader inventory could not be read.'
    }
    $upgraderCount = @([regex]::Matches($upgraderDeclaration.Groups['upgraders'].Value,
            '\bUpgradeV\d+\b')).Count
    $urpProjectSettings = Get-Content -LiteralPath $urpProjectSettingsPath -Raw -Encoding UTF8
    $materialVersionMatch = [regex]::Match($urpProjectSettings,
        '(?m)^  m_LastMaterialVersion: (?<version>\d+)$')
    if (-not $materialVersionMatch.Success -or
        [int]$materialVersionMatch.Groups['version'].Value -ne $upgraderCount) {
        throw "URP material version marker does not match the pinned package upgrader count ($upgraderCount)."
    }

    # Reopen the populated project to catch package-cache and assembly-validation defects that are
    # invisible during the first import. This is the automated equivalent of the manual GUI reopen.
    $reopenArguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $cloneRoot,
        '-logFile', $reopenLogPath
    )
    $reopenProcess = Start-Process -FilePath $unityPath -ArgumentList $reopenArguments `
        -Wait -PassThru -NoNewWindow
    if ($reopenProcess.ExitCode -ne 0) {
        $firstReopenLog = if (Test-Path -LiteralPath $reopenLogPath) {
            Get-Content -LiteralPath $reopenLogPath -Raw -Encoding UTF8
        }
        else {
            ''
        }
        $knownNativeStartupRace = $firstReopenLog.Contains(
            "Assertion failed on expression: 'CurrentThread::IsMainThread()'") -and
            $firstReopenLog -notmatch '(?m)(DirectoryNotFoundException|Could not find a part of the path|error CS\d+)'
        if ($knownNativeStartupRace) {
            # Unity can tear down its licensing/windowing threads after the process exits. Retain
            # the failed native log, allow that teardown to settle, and require one fresh process
            # to prove the populated project itself reopens cleanly.
            Start-Sleep -Seconds 2
            $reopenLogPath = $reopenRetryLogPath
            $reopenArguments = @(
                '-batchmode',
                '-nographics',
                '-quit',
                '-projectPath', $cloneRoot,
                '-logFile', $reopenLogPath
            )
            $reopenProcess = Start-Process -FilePath $unityPath -ArgumentList $reopenArguments `
                -Wait -PassThru -NoNewWindow
        }
    }
}
finally {
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

$reopenLog = if (Test-Path -LiteralPath $reopenLogPath) {
    Get-Content -LiteralPath $reopenLogPath -Raw -Encoding UTF8
}
else {
    throw 'Unity product reopen log is missing.'
}
if ($reopenProcess.ExitCode -ne 0) {
    $tail = (@(Get-Content -LiteralPath $reopenLogPath -Tail 120) -join [Environment]::NewLine)
    throw "Unity product reopen failed with exit code $($reopenProcess.ExitCode).`n$tail"
}
if ($reopenLog -match '(?m)(DirectoryNotFoundException|Could not find a part of the path|error CS\d+)') {
    throw 'Unity product reopen log contains a path, assembly, or compilation failure.'
}

Write-Host 'Unity product folder Editor tests: passed'
Write-Host 'Unity TextureImporter Editor tests: passed'
Write-Host 'Unity metallic-smoothness exact pixel tests: passed'
Write-Host 'Unity URP/Lit material compiler tests: passed'
Write-Host 'Unity static ModelImporter Editor tests: passed'
Write-Host 'Unity standalone mesh extraction Editor tests: passed'
Write-Host 'Unity static prefab generation Editor tests: passed'
Write-Host 'Unity URP material upgrader marker validation: passed'
Write-Host 'Unity populated-project reopen validation: passed'
Write-Host 'Generated folder, texture, material, model, mesh, and prefab assets retained for manual Unity inspection.'
Write-Host "Manual Unity project: $cloneRoot"
Write-Host "Retained integration evidence: $runRoot"
