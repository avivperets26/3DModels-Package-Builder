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
$versionInfo = (Get-Item -LiteralPath $unityPath).VersionInfo
if ([string]$versionInfo.ProductVersion -cne '6000.3.10f1_e35f0c77bd8e' -or
    [string]$versionInfo.CompanyName -cne 'Unity Technologies') {
    throw 'Unity executable identity does not match the approved 6000.3.10f1 revision.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $unityPath
if ($signature.Status -ne 'Valid' -or
    $signature.SignerCertificate.Subject -notlike 'CN=Unity Technologies SF,*') {
    throw 'Unity executable does not have the approved valid Unity Technologies signature.'
}

& (Join-Path $repositoryRootPath 'scripts\Test-UnityProjectTemplate.ps1') `
    -RepositoryRoot $repositoryRootPath
& (Join-Path $repositoryRootPath 'scripts\Test-UnityWorkerPackage.ps1') `
    -RepositoryRoot $repositoryRootPath

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N'))
$runRoot = Join-Path $repositoryRootPath "artifacts\PB-0602-PB-0604\$runId"
$cloneRoot = Join-Path $runRoot 'unity-project'
$requestPath = Join-Path $runRoot 'worker-request.json'
$unityLogPath = Join-Path $runRoot 'unity-editor.log'
$unsupportedLogPath = Join-Path $runRoot 'unity-unsupported-operation.log'
$cancelledLogPath = Join-Path $runRoot 'unity-cancelled.log'
$resultPath = Join-Path $cloneRoot 'PackageBuilder\worker-result.json'
$unsupportedResultPath = Join-Path $cloneRoot 'PackageBuilder\unsupported-result.json'
$cancelledResultPath = Join-Path $cloneRoot 'PackageBuilder\cancelled-result.json'
$cancellationPath = Join-Path $runRoot 'cancellation.signal'
$cacheRoot = Join-Path $repositoryRootPath 'runtime-data\unity\6000.3.10f1'
$temporaryRoot = Join-Path $cacheRoot 'temp'
$upmCacheRoot = Join-Path $cacheRoot 'upm-cache'
$upmConfigRoot = Join-Path $cacheRoot 'upm-config'

New-Item -ItemType Directory -Path $runRoot, $cloneRoot, $temporaryRoot, $upmCacheRoot, $upmConfigRoot -Force |
    Out-Null

# The integration clone is ignored, disposable evidence. The tracked template is never opened by Unity.
foreach ($rootName in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $templateRoot $rootName) -Destination $cloneRoot -Recurse
}

$request = [ordered]@{
    protocolVersion = 1
    jobId = 'Job-Unity-Integration-01'
    operation = 'probe-unity-worker'
    productManifestReference = 'Packages/manifest.json'
    inputDirectoryReference = 'Assets'
    outputDirectoryReference = 'Assets/PackageBuilderWorkerOutput'
    resultFileReference = 'PackageBuilder/worker-result.json'
    engineVersion = '6000.3.10f1'
    target = 'unity'
}
$request | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath $requestPath -Encoding UTF8

function Invoke-UnityBatchProbe {
    param([Parameter(Mandatory)][string]$LogPath)

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $cloneRoot,
        '-executeMethod', 'PackageBuilder.UnityWorker.Editor.UnityBatchEntrypoint.Run',
        '-packageBuilderRequest', $requestPath,
        '-logFile', $LogPath
    )
    # Unity is a Windows GUI subsystem executable and can return control to a
    # PowerShell host before the Editor process exits. Start-Process -Wait is
    # therefore required to observe the actual batch-mode exit code.
    $process = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    return $process.ExitCode
}

$originalEnvironment = @{}
$environment = [ordered]@{
    TEMP = $temporaryRoot
    TMP = $temporaryRoot
    UPM_NPM_CACHE_PATH = $upmCacheRoot
    UPM_CACHE_PATH = $upmCacheRoot
    UPM_CONFIG_PATH = $upmConfigRoot
}
try {
    foreach ($entry in $environment.GetEnumerator()) {
        $originalEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $exitCode = Invoke-UnityBatchProbe -LogPath $unityLogPath

    $request.jobId = 'Job-Unity-Unsupported-01'
    $request.operation = 'unsupported-worker-operation'
    $request.resultFileReference = 'PackageBuilder/unsupported-result.json'
    $request | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath $requestPath -Encoding UTF8
    $unsupportedExitCode = Invoke-UnityBatchProbe -LogPath $unsupportedLogPath

    $originalEnvironment['PACKAGEBUILDER_CANCELLATION_FILE'] =
        [Environment]::GetEnvironmentVariable('PACKAGEBUILDER_CANCELLATION_FILE', 'Process')
    Set-Content -LiteralPath $cancellationPath -Value 'cancel' -Encoding Ascii
    [Environment]::SetEnvironmentVariable('PACKAGEBUILDER_CANCELLATION_FILE', $cancellationPath, 'Process')
    $request.jobId = 'Job-Unity-Cancelled-01'
    $request.operation = 'probe-unity-worker'
    $request.resultFileReference = 'PackageBuilder/cancelled-result.json'
    $request | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath $requestPath -Encoding UTF8
    $cancelledExitCode = Invoke-UnityBatchProbe -LogPath $cancelledLogPath
}
finally {
    foreach ($entry in $originalEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

if ($exitCode -ne 0) {
    $tail = if (Test-Path -LiteralPath $unityLogPath) {
        (Get-Content -LiteralPath $unityLogPath -Tail 80) -join [Environment]::NewLine
    }
    else {
        '<Unity log missing>'
    }
    throw "Unity worker integration exited with code $exitCode.`n$tail"
}
if ($unsupportedExitCode -ne 4) {
    throw "Unity unsupported-operation probe exited with $unsupportedExitCode instead of 4."
}
if ($cancelledExitCode -ne 7) {
    throw "Unity cancellation probe exited with $cancelledExitCode instead of 7."
}

$assemblyPath = Join-Path $cloneRoot 'Library\ScriptAssemblies\PackageBuilder.UnityWorker.Editor.dll'
$artifactPath = Join-Path $cloneRoot 'Assets\PackageBuilderWorkerOutput\worker-probe.txt'
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw 'Unity did not compile the Editor-only worker assembly.'
}
if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw 'Unity worker did not save the expected probe asset and result.'
}

$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$result.protocolVersion -ne 1 -or
    [string]$result.jobId -cne 'Job-Unity-Integration-01' -or
    [string]$result.status -cne 'success' -or
    [string]$result.workerVersion -cne '1.0.0' -or
    [string]$result.engineVersion -cne '6000.3.10f1' -or
    @($result.artifacts).Count -ne 1 -or
    [string]$result.artifacts[0].logicalReference -cne 'Assets/PackageBuilderWorkerOutput/worker-probe.txt') {
    throw 'Unity worker result does not match the protocol-v1 success contract.'
}
$unsupportedResult = Get-Content -LiteralPath $unsupportedResultPath -Raw -Encoding UTF8 |
    ConvertFrom-Json
if ([string]$unsupportedResult.status -cne 'failure' -or
    [string]$unsupportedResult.findings[0].code -cne 'UNITY_OPERATION_UNSUPPORTED') {
    throw 'Unity worker unsupported-operation result is not the expected structured failure.'
}
$cancelledResult = Get-Content -LiteralPath $cancelledResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([string]$cancelledResult.status -cne 'cancelled' -or
    [string]$cancelledResult.cancellation.outcome -cne 'acknowledged') {
    throw 'Unity worker cancellation result does not acknowledge cancellation.'
}

$combinedOutput = @(
    Get-Content -LiteralPath $unityLogPath
    Get-Content -LiteralPath $unsupportedLogPath
    Get-Content -LiteralPath $cancelledLogPath
)
$jsonLines = @($combinedOutput | Where-Object { $_ -match '^\{"protocolVersion":1,"eventKind":"(progress|metric)"' })
if ($jsonLines.Count -lt 4) {
    throw "Expected at least four worker JSON Lines records; found $($jsonLines.Count)."
}

$compileErrors = @($combinedOutput | Where-Object { $_ -match '(?i)(^|\s)(error CS\d+|compilation failed)' })
if ($compileErrors.Count -gt 0) {
    throw "Unity log contains compilation errors: $($compileErrors -join '; ')"
}

Write-Host 'Unity worker package compilation: passed'
Write-Host 'Unity protocol-v1 batch probe: passed'
Write-Host 'Stable unsupported-operation exit code 4: passed'
Write-Host 'Stable cancellation exit code 7: passed'
Write-Host "Worker JSON Lines records: $($jsonLines.Count)"
Write-Host "Retained integration evidence: $runRoot"
