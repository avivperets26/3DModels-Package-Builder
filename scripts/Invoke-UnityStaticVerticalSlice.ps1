[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$UnityExecutable,
    [switch]$OpenFolder
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
$runId = "run-{0}-{1}" -f (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss'), `
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
$taskRoot = Join-Path $repositoryRootPath 'artifacts\PB-0618'
$runRoot = Join-Path $taskRoot $runId
$stagingRoot = Join-Path $runRoot 'staging\StoneArch'
$releaseRoot = Join-Path $taskRoot "releases\$runId\StoneArch"
$unityPointerPath = Join-Path $runRoot 'unity-integration-result.json'
$portablePointerPath = Join-Path $repositoryRootPath 'artifacts\PB-0507\manual\latest.txt'

. (Join-Path $PSScriptRoot 'UnityTestArtifacts.Common.ps1')
try {
    # PB-0618 deliberately composes the already-validated PB-0507 and PB-0617 boundaries. It does
    # not duplicate their target logic, which keeps the vertical slice representative of production.
    & (Join-Path $repositoryRootPath 'scripts\Invoke-PB0507PortableStaticManualTest.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'The portable static slice failed before Unity composition.'
    }

    & (Join-Path $repositoryRootPath 'scripts\Invoke-UnityProductIntegration.ps1') `
        -RepositoryRoot $repositoryRootPath `
        -UnityExecutable $UnityExecutable `
        -ResultPointerPath $unityPointerPath -KeepArtifacts
    if ($LASTEXITCODE -ne 0) {
        throw 'The Unity static slice failed before release composition.'
    }

    function Read-KeyValueFile([string]$Path) {
        $values = @{}
        foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
            $separator = $line.IndexOf('=')
            if ($separator -gt 0) {
                $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
            }
        }
        return $values
    }

    if (-not (Test-Path -LiteralPath $portablePointerPath -PathType Leaf)) {
        throw 'PB-0507 did not retain its manual output pointer.'
    }
    $portable = Read-KeyValueFile $portablePointerPath
    $unity = Get-Content -LiteralPath $unityPointerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($required in @('release', 'report', 'log')) {
        if (-not $portable.ContainsKey($required) -or
            -not (Test-Path -LiteralPath $portable[$required] -PathType Leaf)) {
            throw "PB-0507 retained evidence is missing '$required'."
        }
    }
    foreach ($required in @('package', 'scene', 'integrationLog', 'cleanReimportResult')) {
        if ([string]::IsNullOrWhiteSpace([string]$unity.$required) -or
            -not (Test-Path -LiteralPath $unity.$required -PathType Leaf)) {
            throw "Unity retained evidence is missing '$required'."
        }
    }

    $sourceFixture = Join-Path $repositoryRootPath `
        'tests\fixtures\portable\static-vertical-slice\source\StoneArch.fbx'
    $sourceHash = (Get-FileHash -LiteralPath $sourceFixture -Algorithm SHA256).Hash.ToLowerInvariant()
    $portableArchiveHash = (Get-FileHash -LiteralPath $portable.release -Algorithm SHA256).Hash.ToLowerInvariant()
    $unityPackageHash = (Get-FileHash -LiteralPath $unity.package -Algorithm SHA256).Hash.ToLowerInvariant()
    $cleanResult = Get-Content -LiteralPath $unity.cleanReimportResult -Raw -Encoding UTF8 |
        ConvertFrom-Json
    if (-not $cleanResult.passed) {
        throw 'The clean Unity reimport result blocks PB-0618 promotion.'
    }

    # All target evidence is assembled beneath staging. A single same-volume directory rename makes
    # the customer-visible release appear atomically only after every validation result has passed.
    New-Item -ItemType Directory -Path `
        (Join-Path $stagingRoot 'Portable'), `
        (Join-Path $stagingRoot 'Unity'), `
        (Join-Path $stagingRoot 'Reports'), `
        (Join-Path $stagingRoot 'Logs') -Force | Out-Null
    Copy-Item -LiteralPath $portable.release -Destination `
        (Join-Path $stagingRoot 'Portable\Stone_Arch_FBX.zip')
    Copy-Item -LiteralPath $unity.package -Destination `
        (Join-Path $stagingRoot 'Unity\StoneArch.unitypackage')
    Copy-Item -LiteralPath $unity.scene -Destination `
        (Join-Path $stagingRoot 'Unity\S_StoneArch_Overview.unity')
    Copy-Item -LiteralPath $portable.report -Destination `
        (Join-Path $stagingRoot 'Reports\portable-validation-report.json')
    Copy-Item -LiteralPath $unity.cleanReimportResult -Destination `
        (Join-Path $stagingRoot 'Reports\unity-clean-reimport-result.json')
    Copy-Item -LiteralPath $portable.log -Destination `
        (Join-Path $stagingRoot 'Logs\portable-job.log')
    Copy-Item -LiteralPath $unity.integrationLog -Destination `
        (Join-Path $stagingRoot 'Logs\unity-integration.log')

    $validationReport = [ordered]@{
        schemaVersion = 1
        status = 'passed'
        productId = 'StoneArch'
        productCase = 'static'
        sourceSha256 = $sourceHash
        source = [ordered]@{
            logicalReference = 'tests/fixtures/portable/static-vertical-slice/source/StoneArch.fbx'
            sha256 = $sourceHash
        }
        portable = [ordered]@{
            artifact = 'Portable/Stone_Arch_FBX.zip'
            sha256 = $portableArchiveHash
            report = 'Reports/portable-validation-report.json'
        }
        unity = [ordered]@{
            artifact = 'Unity/StoneArch.unitypackage'
            sha256 = $unityPackageHash
            overviewScene = 'Unity/S_StoneArch_Overview.unity'
            cleanReimportReport = 'Reports/unity-clean-reimport-result.json'
        }
        findings = @()
    }
    $validationReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath `
        (Join-Path $stagingRoot 'validation-report.json') -Encoding UTF8

    if (Test-Path -LiteralPath $releaseRoot) {
        throw "The PB-0618 release destination already exists: $releaseRoot"
    }
    $releaseParent = Split-Path $releaseRoot -Parent
    New-Item -ItemType Directory -Path $releaseParent -Force | Out-Null
    [IO.Directory]::Move($stagingRoot, $releaseRoot)
    if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot 'validation-report.json') -PathType Leaf) -or
        (Test-Path -LiteralPath $stagingRoot)) {
        throw 'PB-0618 atomic release promotion did not complete cleanly.'
    }

    $latestPath = Join-Path $taskRoot 'latest.txt'
    @(
        "release=$releaseRoot",
        "report=$(Join-Path $releaseRoot 'validation-report.json')",
        "portable=$(Join-Path $releaseRoot 'Portable\Stone_Arch_FBX.zip')",
        "unity=$(Join-Path $releaseRoot 'Unity\StoneArch.unitypackage')",
        'unityArtifactsRetained=false',
        "unityCleanupReport=$(Join-Path $unity.runRoot 'cleanup-result.json')",
        "unityProject=$($unity.project)",
        "cleanUnityProject=$($unity.cleanProject)"
    ) | Set-Content -LiteralPath $latestPath -Encoding UTF8

    Write-Host ''
    Write-Host 'PB-0618 Unity static vertical slice passed and was atomically promoted.' `
        -ForegroundColor Green
    Write-Host "Promoted release: $releaseRoot"
    Write-Host "Validation report: $(Join-Path $releaseRoot 'validation-report.json')"
    Write-Host 'Temporary Unity projects are cleaned after release composition.'

    if ($OpenFolder) {
        Invoke-Item -LiteralPath $releaseRoot
    }
}
finally {
    # The child must stay available until composition consumes it. Failed child runs clean themselves.
    if (Test-Path -LiteralPath $unityPointerPath -PathType Leaf) {
        $childEvidence = Get-Content -LiteralPath $unityPointerPath -Raw | ConvertFrom-Json
        Remove-UnityTestArtifacts -RepositoryRoot $repositoryRootPath -RunRoot $childEvidence.runRoot
        $childEvidence.artifactsRetained = $false
        $childEvidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $unityPointerPath -Encoding UTF8
    }
}
