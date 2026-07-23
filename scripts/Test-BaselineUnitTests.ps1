[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$VerifyNoSourceChanges
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$solutionPath = Join-Path $repositoryRootPath 'PackageBuilder.sln'
$globalJsonPath = Join-Path $repositoryRootPath 'global.json'
$structuralValidatorPath = Join-Path $repositoryRootPath 'scripts\Test-TestProjects.ps1'
$logDirectory = Join-Path $repositoryRootPath 'logs\validation\PB-0008'
$logPath = Join-Path $logDirectory 'baseline-unit-tests.log'
$resultsRoot = Join-Path $repositoryRootPath 'artifacts\test-results\PB-0008'
$summaryPath = Join-Path $resultsRoot 'summary.json'
$temporaryDirectory = Join-Path $repositoryRootPath 'artifacts\validation\PB-0008\temp'
$script:LogLines = New-Object 'System.Collections.Generic.List[string]'
$script:OriginalEnvironment = @{}
$script:EnvironmentConfigured = $false
$script:EnvironmentNames = @(
    'DOTNET_ROOT',
    'DOTNET_CLI_HOME',
    'NUGET_PACKAGES',
    'NUGET_HTTP_CACHE_PATH',
    'NUGET_SCRATCH',
    'NUGET_PLUGINS_CACHE_PATH',
    'TEMP',
    'TMP',
    'DOTNET_MULTILEVEL_LOOKUP',
    'DOTNET_SKIP_FIRST_TIME_EXPERIENCE',
    'DOTNET_NOLOGO',
    'DOTNET_CLI_TELEMETRY_OPTOUT',
    'TESTINGPLATFORM_TELEMETRY_OPTOUT'
)
$script:ProjectSpecifications = @(
    [pscustomobject]@{
        Name = 'PackageBuilder.Domain.Tests'
        Path = 'tests/PackageBuilder.Domain.Tests/PackageBuilder.Domain.Tests.csproj'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Application.Tests'
        Path = 'tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Infrastructure.Tests'
        Path = 'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Contract.Tests'
        Path = 'tests/PackageBuilder.Contract.Tests/PackageBuilder.Contract.Tests.csproj'
    }
)

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $repositoryRootPath + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $repositoryRootPath) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Write-ValidationLog {
    param([Parameter(Mandatory)][string]$Message)

    $script:LogLines.Add($Message)
    Write-Host $Message
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowNonZeroExit
    )

    Write-ValidationLog "[RUN] $Name"
    Write-ValidationLog "      $Executable $($Arguments -join ' ')"
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 surfaces native stderr as ErrorRecord objects.
        $ErrorActionPreference = 'Continue'
        $output = @(& $Executable @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    foreach ($line in $output) {
        Write-ValidationLog "      $([string]$line)"
    }

    if ($exitCode -ne 0 -and -not $AllowNonZeroExit) {
        throw "$Name failed with exit code $exitCode."
    }
    if ($exitCode -eq 0) {
        Write-ValidationLog "[PASS] $Name"
    }
    else {
        Write-ValidationLog "[FAIL] $Name exited with code $exitCode"
    }

    return $exitCode
}

function Get-SourceCandidateHashes {
    $paths = @(
        & git -C $repositoryRootPath ls-files
        & git -C $repositoryRootPath ls-files --others --exclude-standard
    ) | Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate repository source candidates.'
    }

    $hashes = @{}
    foreach ($relativePath in $paths) {
        $fullPath = Join-Path $repositoryRootPath $relativePath
        if (-not (Test-ContainedPath $fullPath) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Source candidate is invalid: $relativePath"
        }
        $hashes[$relativePath.Replace('\', '/')] = (Get-FileHash -Algorithm SHA256 -LiteralPath $fullPath).Hash
    }

    return $hashes
}

function Assert-HashesUnchanged {
    param(
        [Parameter(Mandatory)][hashtable]$Before,
        [Parameter(Mandatory)][hashtable]$After
    )

    $allPaths = @($Before.Keys + $After.Keys | Sort-Object -Unique)
    $changed = @(
        $allPaths | Where-Object {
            -not $Before.ContainsKey($_) -or
            -not $After.ContainsKey($_) -or
            $Before[$_] -ne $After[$_]
        }
    )
    if ($changed.Count -gt 0) {
        throw "Verification mode modified source candidates: $($changed -join ', ')"
    }
}

function Set-ContainedEnvironment {
    param([Parameter(Mandatory)][string]$DotnetRoot)

    foreach ($name in $script:EnvironmentNames) {
        $script:OriginalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    $values = [ordered]@{
        DOTNET_ROOT = $DotnetRoot
        DOTNET_CLI_HOME = Join-Path $repositoryRootPath 'runtime-data\cli-home'
        NUGET_PACKAGES = Join-Path $repositoryRootPath 'runtime-data\nuget-packages'
        NUGET_HTTP_CACHE_PATH = Join-Path $repositoryRootPath 'runtime-data\nuget-http-cache'
        NUGET_SCRATCH = Join-Path $repositoryRootPath 'runtime-data\nuget-scratch'
        NUGET_PLUGINS_CACHE_PATH = Join-Path $repositoryRootPath 'runtime-data\nuget-plugins-cache'
        TEMP = $temporaryDirectory
        TMP = $temporaryDirectory
        DOTNET_MULTILEVEL_LOOKUP = '0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    }

    foreach ($entry in $values.GetEnumerator()) {
        $isPathValue = $entry.Key -notmatch (
            '^(DOTNET_MULTILEVEL_LOOKUP|DOTNET_SKIP_FIRST_TIME_EXPERIENCE|DOTNET_NOLOGO|' +
            'DOTNET_CLI_TELEMETRY_OPTOUT|TESTINGPLATFORM_TELEMETRY_OPTOUT)$'
        )
        if ($isPathValue) {
            if (-not (Test-ContainedPath $entry.Value)) {
                throw "Environment path escapes the repository root: $($entry.Key)=$($entry.Value)"
            }
            if (-not (Test-Path -LiteralPath $entry.Value -PathType Container)) {
                New-Item -ItemType Directory -Path $entry.Value -Force | Out-Null
            }
        }
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    $script:EnvironmentConfigured = $true
}

function Restore-Environment {
    if (-not $script:EnvironmentConfigured) {
        return
    }

    foreach ($name in $script:EnvironmentNames) {
        [Environment]::SetEnvironmentVariable($name, $script:OriginalEnvironment[$name], 'Process')
    }
}

function Get-TestCounts {
    param([Parameter(Mandatory)][string]$TrxPath)

    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) {
        throw "Test result file was not created: $TrxPath"
    }
    if (-not (Test-ContainedPath $TrxPath)) {
        throw "Test result file escapes the repository root: $TrxPath"
    }

    $trx = [xml](Get-Content -Raw -LiteralPath $TrxPath -Encoding UTF8)
    $counters = $trx.SelectSingleNode(
        "/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']"
    )
    if ($null -eq $counters) {
        throw "TRX result contains no counters: $TrxPath"
    }

    return [pscustomobject]@{
        Discovered = [int]$counters.GetAttribute('total')
        Passed = [int]$counters.GetAttribute('passed')
        Failed = [int]$counters.GetAttribute('failed')
        Skipped = [int]$counters.GetAttribute('notExecuted')
    }
}

if (-not (Test-Path -LiteralPath $repositoryRootPath -PathType Container)) {
    throw "Repository root does not exist: $repositoryRootPath"
}

$gitRootOutput = @(& git -C $repositoryRootPath rev-parse --show-toplevel 2>&1)
if ($LASTEXITCODE -ne 0 -or $gitRootOutput.Count -ne 1) {
    throw "Unable to resolve the Git top level for '$repositoryRootPath'."
}
$gitRoot = [System.IO.Path]::GetFullPath([string]$gitRootOutput[0]).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $repositoryRootPath)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

foreach ($requiredFile in @($solutionPath, $globalJsonPath, $structuralValidatorPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required test input is missing: $requiredFile"
    }
}

$globalJson = Get-Content -Raw -LiteralPath $globalJsonPath -Encoding UTF8 | ConvertFrom-Json
$dotnetVersion = [string]$globalJson.sdk.version
if ($dotnetVersion -cne '10.0.302') {
    throw "Expected repository-local .NET SDK 10.0.302; found '$dotnetVersion'."
}
$dotnetRoot = Join-Path $repositoryRootPath "tools\dotnet\$dotnetVersion"
$dotnetExecutable = Join-Path $dotnetRoot 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable -PathType Leaf)) {
    throw "Repository-local .NET SDK is missing: $dotnetExecutable"
}

foreach ($directory in @($logDirectory, $resultsRoot, $temporaryDirectory)) {
    if (-not (Test-ContainedPath $directory)) {
        throw "Validation directory escapes the repository root: $directory"
    }
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}

$beforeHashes = if ($VerifyNoSourceChanges) { Get-SourceCandidateHashes } else { $null }
$completed = $false

try {
    Set-ContainedEnvironment -DotnetRoot $dotnetRoot
    Push-Location $repositoryRootPath
    try {
        & $structuralValidatorPath -RepositoryRoot $repositoryRootPath
        Write-ValidationLog '[PASS] Test-project structural validation'

        $dotnetVersionOutput = @(& $dotnetExecutable --version 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($dotnetVersionOutput -join ' ').Trim() -cne $dotnetVersion) {
            throw "Repository-local .NET SDK version check failed: $($dotnetVersionOutput -join ' ')"
        }
        Write-ValidationLog "[PASS] .NET SDK version $dotnetVersion"

        Invoke-Tool `
            -Name 'Locked solution restore' `
            -Executable $dotnetExecutable `
            -Arguments @('restore', $solutionPath, '--locked-mode', '--verbosity', 'minimal') | Out-Null

        $projectResults = @()
        $validationFailures = New-Object 'System.Collections.Generic.List[string]'
        foreach ($specification in $script:ProjectSpecifications) {
            $projectPath = Join-Path $repositoryRootPath $specification.Path
            $projectResultDirectory = Join-Path $resultsRoot $specification.Name
            if (-not (Test-ContainedPath $projectResultDirectory)) {
                throw "Test result directory escapes the repository root: $projectResultDirectory"
            }
            if (-not (Test-Path -LiteralPath $projectResultDirectory -PathType Container)) {
                New-Item -ItemType Directory -Path $projectResultDirectory -Force | Out-Null
            }

            $trxFileName = "$($specification.Name).trx"
            $trxPath = Join-Path $projectResultDirectory $trxFileName
            if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
                # Remove only the exact prior generated result so stale counts cannot satisfy validation.
                if (-not (Test-ContainedPath $trxPath)) {
                    throw "Prior test result escapes the repository root: $trxPath"
                }
                Remove-Item -LiteralPath $trxPath -Force
            }

            $testExitCode = Invoke-Tool `
                -Name "$($specification.Name) tests" `
                -Executable $dotnetExecutable `
                -Arguments @(
                    'test',
                    $projectPath,
                    '--configuration',
                    'Debug',
                    '--no-restore',
                    '--logger',
                    "trx;LogFileName=$trxFileName",
                    '--results-directory',
                    $projectResultDirectory,
                    '--verbosity',
                    'minimal'
                ) `
                -AllowNonZeroExit

            try {
                $counts = Get-TestCounts -TrxPath $trxPath
            }
            catch {
                $validationFailures.Add("$($specification.Name): $($_.Exception.Message)")
                $counts = [pscustomobject]@{
                    Discovered = 0
                    Passed = 0
                    Failed = 0
                    Skipped = 0
                }
            }

            $otherOutcomes = $counts.Discovered - $counts.Passed - $counts.Failed - $counts.Skipped
            Write-ValidationLog (
                "[RESULT] $($specification.Name): discovered=$($counts.Discovered), " +
                "passed=$($counts.Passed), failed=$($counts.Failed), skipped=$($counts.Skipped)"
            )
            if ($testExitCode -ne 0) {
                $validationFailures.Add("$($specification.Name) exited with code $testExitCode.")
            }
            if ($counts.Discovered -lt 1) {
                $validationFailures.Add("$($specification.Name) discovered zero tests.")
            }
            if ($counts.Failed -gt 0) {
                $validationFailures.Add("$($specification.Name) reported $($counts.Failed) failed test(s).")
            }
            if ($counts.Skipped -gt 0) {
                $validationFailures.Add("$($specification.Name) reported $($counts.Skipped) unexpectedly skipped test(s).")
            }
            if ($otherOutcomes -ne 0) {
                $validationFailures.Add("$($specification.Name) reported $otherOutcomes unclassified test outcome(s).")
            }

            $projectResults += [pscustomobject]@{
                Project = $specification.Name
                Discovered = $counts.Discovered
                Passed = $counts.Passed
                Failed = $counts.Failed
                Skipped = $counts.Skipped
            }
        }

        $totals = [pscustomobject]@{
            Discovered = [int](($projectResults | Measure-Object -Property Discovered -Sum).Sum)
            Passed = [int](($projectResults | Measure-Object -Property Passed -Sum).Sum)
            Failed = [int](($projectResults | Measure-Object -Property Failed -Sum).Sum)
            Skipped = [int](($projectResults | Measure-Object -Property Skipped -Sum).Sum)
        }
        Write-ValidationLog (
            "[TOTAL] discovered=$($totals.Discovered), passed=$($totals.Passed), " +
            "failed=$($totals.Failed), skipped=$($totals.Skipped)"
        )

        $summary = [ordered]@{
            task = 'PB-0008'
            sdkVersion = $dotnetVersion
            configuration = 'Debug'
            projects = @($projectResults | ForEach-Object {
                [ordered]@{
                    project = $_.Project
                    discovered = $_.Discovered
                    passed = $_.Passed
                    failed = $_.Failed
                    skipped = $_.Skipped
                }
            })
            total = [ordered]@{
                discovered = $totals.Discovered
                passed = $totals.Passed
                failed = $totals.Failed
                skipped = $totals.Skipped
            }
        }
        $summaryJson = $summary | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText(
            $summaryPath,
            $summaryJson + [Environment]::NewLine,
            (New-Object System.Text.UTF8Encoding($false))
        )
        Write-ValidationLog "[PASS] Deterministic logical summary written to $summaryPath"

        if ($VerifyNoSourceChanges) {
            Assert-HashesUnchanged -Before $beforeHashes -After (Get-SourceCandidateHashes)
            Write-ValidationLog '[PASS] Verification mode did not modify source candidates.'
        }

        if ($validationFailures.Count -gt 0) {
            throw "Baseline unit-test validation failed: $($validationFailures -join ' ')"
        }

        $completed = $true
        Write-ValidationLog '[PASS] Baseline unit-test validation succeeded.'
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-ValidationLog "[FAIL] $($_.Exception.Message)"
    throw
}
finally {
    Restore-Environment
    [System.IO.File]::WriteAllLines(
        $logPath,
        $script:LogLines,
        (New-Object System.Text.UTF8Encoding($false))
    )
    if (-not $completed) {
        Write-Host "Baseline unit-test validation failed. See $logPath" -ForegroundColor Red
    }
}
