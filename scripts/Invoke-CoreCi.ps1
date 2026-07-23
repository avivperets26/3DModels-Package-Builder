[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$GitHubActions
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$solutionPath = Join-Path $repositoryRootPath 'PackageBuilder.sln'
$globalJsonPath = Join-Path $repositoryRootPath 'global.json'
$ruffConfigurationPath = Join-Path $repositoryRootPath 'ruff.toml'
$repositoryValidatorPath = Join-Path $repositoryRootPath 'scripts\Test-RepositoryBaseline.ps1'
$ruffInstallerPath = Join-Path $repositoryRootPath 'scripts\Install-Ruff.ps1'
$testValidatorPath = Join-Path $repositoryRootPath 'scripts\Test-BaselineUnitTests.ps1'
$logDirectory = Join-Path $repositoryRootPath 'logs\validation\PB-0009'
$logPath = Join-Path $logDirectory 'core-ci.log'
$temporaryDirectory = Join-Path $repositoryRootPath 'artifacts\validation\PB-0009\temp'
$script:LogLines = New-Object 'System.Collections.Generic.List[string]'
$script:StageResults = New-Object 'System.Collections.Generic.List[object]'
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
    'TESTINGPLATFORM_TELEMETRY_OPTOUT',
    'RUFF_CACHE_DIR'
)

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $repositoryRootPath + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $repositoryRootPath) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Write-CoreLog {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Message)

    $script:LogLines.Add($Message)
    Write-Host $Message
}

function Invoke-CoreStage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-CoreLog ''
    Write-CoreLog "[STAGE] $Name"
    try {
        & $Action
        $stopwatch.Stop()
        $script:StageResults.Add([pscustomobject]@{
            Name = $Name
            Status = 'PASS'
            Duration = $stopwatch.Elapsed
        })
        Write-CoreLog "[PASS] $Name ($($stopwatch.Elapsed.ToString('c')))"
    }
    catch {
        $stopwatch.Stop()
        $script:StageResults.Add([pscustomobject]@{
            Name = $Name
            Status = 'FAIL'
            Duration = $stopwatch.Elapsed
        })
        Write-CoreLog "[FAIL] $Name ($($stopwatch.Elapsed.ToString('c'))): $($_.Exception.Message)"
        throw
    }
}

function Invoke-NativeTool {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    Write-CoreLog "[RUN] $Name"
    Write-CoreLog "      $Executable $($Arguments -join ' ')"
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
        Write-CoreLog "      $([string]$line)"
    }
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode."
    }

    return $output
}

function Set-CoreEnvironment {
    param([Parameter(Mandatory)][string]$DotnetRoot)

    foreach ($name in $script:EnvironmentNames) {
        $script:OriginalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    $script:EnvironmentConfigured = $true

    $containedValues = [ordered]@{
        DOTNET_CLI_HOME = Join-Path $repositoryRootPath 'runtime-data\cli-home'
        NUGET_PACKAGES = Join-Path $repositoryRootPath 'runtime-data\nuget-packages'
        NUGET_HTTP_CACHE_PATH = Join-Path $repositoryRootPath 'runtime-data\nuget-http-cache'
        NUGET_SCRATCH = Join-Path $repositoryRootPath 'runtime-data\nuget-scratch'
        NUGET_PLUGINS_CACHE_PATH = Join-Path $repositoryRootPath 'runtime-data\nuget-plugins-cache'
        TEMP = $temporaryDirectory
        TMP = $temporaryDirectory
        RUFF_CACHE_DIR = Join-Path $repositoryRootPath 'runtime-data\ruff-cache'
    }
    foreach ($entry in $containedValues.GetEnumerator()) {
        if (-not (Test-ContainedPath $entry.Value)) {
            throw "Environment path escapes the repository root: $($entry.Key)=$($entry.Value)"
        }
        if (-not (Test-Path -LiteralPath $entry.Value -PathType Container)) {
            New-Item -ItemType Directory -Path $entry.Value -Force | Out-Null
        }
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

    # DOTNET_ROOT may be runner-managed only in the explicit verified GitHub mode.
    if (-not $GitHubActions -and -not (Test-ContainedPath $DotnetRoot)) {
        throw "Local DOTNET_ROOT must remain beneath the repository: $DotnetRoot"
    }
    [Environment]::SetEnvironmentVariable('DOTNET_ROOT', $DotnetRoot, 'Process')

    $fixedValues = [ordered]@{
        DOTNET_MULTILEVEL_LOOKUP = '0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
    }
    foreach ($entry in $fixedValues.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }

}

function Restore-CoreEnvironment {
    if (-not $script:EnvironmentConfigured) {
        return
    }

    foreach ($name in $script:EnvironmentNames) {
        [Environment]::SetEnvironmentVariable($name, $script:OriginalEnvironment[$name], 'Process')
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

foreach ($requiredFile in @(
    $solutionPath,
    $globalJsonPath,
    $ruffConfigurationPath,
    $repositoryValidatorPath,
    $ruffInstallerPath,
    $testValidatorPath
)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required core-CI input is missing: $requiredFile"
    }
}

$globalJson = Get-Content -Raw -LiteralPath $globalJsonPath -Encoding UTF8 | ConvertFrom-Json
$dotnetVersion = [string]$globalJson.sdk.version
if ($dotnetVersion -cne '10.0.302') {
    throw "Core CI requires .NET SDK 10.0.302; global.json contains '$dotnetVersion'."
}

if ($GitHubActions) {
    if ($env:GITHUB_ACTIONS -cne 'true' -or [string]::IsNullOrWhiteSpace($env:GITHUB_WORKSPACE)) {
        throw 'The -GitHubActions mode requires GITHUB_ACTIONS=true and GITHUB_WORKSPACE.'
    }
    $githubWorkspace = [System.IO.Path]::GetFullPath($env:GITHUB_WORKSPACE).TrimEnd([char[]]'\/')
    if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($githubWorkspace, $repositoryRootPath)) {
        throw "RepositoryRoot must equal GITHUB_WORKSPACE in GitHub Actions. Workspace: $githubWorkspace"
    }

    # setup-dotnet manages this external runner path; exact version verification remains mandatory below.
    $dotnetCommand = @(Get-Command 'dotnet.exe' -CommandType Application -ErrorAction Stop)
    if ($dotnetCommand.Count -lt 1) {
        throw 'The setup-dotnet managed dotnet.exe is unavailable on PATH.'
    }
    $dotnetExecutable = [System.IO.Path]::GetFullPath($dotnetCommand[0].Source)
}
else {
    if ($env:GITHUB_ACTIONS -ceq 'true') {
        throw 'GitHub Actions must invoke this script with the explicit -GitHubActions switch.'
    }
    $dotnetExecutable = Join-Path $repositoryRootPath "tools\dotnet\$dotnetVersion\dotnet.exe"
    if (-not (Test-ContainedPath $dotnetExecutable)) {
        throw "Local dotnet path escapes the repository: $dotnetExecutable"
    }
}

if (-not (Test-Path -LiteralPath $dotnetExecutable -PathType Leaf)) {
    throw "Selected .NET executable is missing: $dotnetExecutable"
}
$dotnetRoot = Split-Path $dotnetExecutable -Parent

foreach ($directory in @($logDirectory, $temporaryDirectory)) {
    if (-not (Test-ContainedPath $directory)) {
        throw "Core-CI directory escapes the repository root: $directory"
    }
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}

$completed = $false
$pipelineStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    Set-CoreEnvironment -DotnetRoot $dotnetRoot
    Push-Location $repositoryRootPath
    try {
        Invoke-CoreStage -Name 'Repository baseline validation' -Action {
            & $repositoryValidatorPath -RepositoryRoot $repositoryRootPath -RequireTrackedFiles
        }

        Invoke-CoreStage -Name '.NET SDK 10.0.302 verification' -Action {
            $versionOutput = @(
                Invoke-NativeTool `
                    -Name '.NET SDK version query' `
                    -Executable $dotnetExecutable `
                    -Arguments @('--version')
            )
            if (($versionOutput -join ' ').Trim() -cne $dotnetVersion) {
                throw "Selected SDK must be exactly $dotnetVersion; found '$($versionOutput -join ' ')'."
            }
        }

        Invoke-CoreStage -Name 'Locked solution restore' -Action {
            Invoke-NativeTool `
                -Name 'Locked solution restore' `
                -Executable $dotnetExecutable `
                -Arguments @(
                    'restore',
                    $solutionPath,
                    '--locked-mode',
                    '--verbosity',
                    'minimal'
                ) | Out-Null
        }

        Invoke-CoreStage -Name 'Release solution build' -Action {
            Invoke-NativeTool `
                -Name 'Release solution build' `
                -Executable $dotnetExecutable `
                -Arguments @(
                    'build',
                    $solutionPath,
                    '--configuration',
                    'Release',
                    '--no-restore',
                    '--nologo',
                    '--verbosity',
                    'minimal'
                ) | Out-Null
        }

        Invoke-CoreStage -Name '.NET formatting verification' -Action {
            Invoke-NativeTool `
                -Name '.NET formatting verification' `
                -Executable $dotnetExecutable `
                -Arguments @(
                    'format',
                    $solutionPath,
                    '--no-restore',
                    '--verify-no-changes',
                    '--severity',
                    'info',
                    '--verbosity',
                    'minimal'
                ) | Out-Null
        }

        Invoke-CoreStage -Name 'Ruff 0.15.22 installation and verification' -Action {
            & $ruffInstallerPath -RepositoryRoot $repositoryRootPath
            $ruffExecutable = Join-Path $repositoryRootPath 'tools\ruff\0.15.22\ruff.exe'
            if (-not (Test-ContainedPath $ruffExecutable) -or
                -not (Test-Path -LiteralPath $ruffExecutable -PathType Leaf)) {
                throw "Verified Ruff executable is missing: $ruffExecutable"
            }
            $versionOutput = @(
                Invoke-NativeTool `
                    -Name 'Ruff version query' `
                    -Executable $ruffExecutable `
                    -Arguments @('--version')
            )
            if (($versionOutput -join ' ').Trim() -cne 'ruff 0.15.22') {
                throw "Ruff must be exactly 0.15.22; found '$($versionOutput -join ' ')'."
            }
        }

        $ruffExecutable = Join-Path $repositoryRootPath 'tools\ruff\0.15.22\ruff.exe'
        Invoke-CoreStage -Name 'Ruff lint verification' -Action {
            Invoke-NativeTool `
                -Name 'Ruff lint verification' `
                -Executable $ruffExecutable `
                -Arguments @(
                    'check',
                    '--config',
                    $ruffConfigurationPath,
                    '--no-fix',
                    '.'
                ) | Out-Null
        }

        Invoke-CoreStage -Name 'Ruff formatting verification' -Action {
            Invoke-NativeTool `
                -Name 'Ruff formatting verification' `
                -Executable $ruffExecutable `
                -Arguments @(
                    'format',
                    '--config',
                    $ruffConfigurationPath,
                    '--check',
                    '.'
                ) | Out-Null
        }

        Invoke-CoreStage -Name 'Baseline test projects' -Action {
            $testArguments = @{
                RepositoryRoot = $repositoryRootPath
                Configuration = 'Release'
                NoRestore = $true
                NoBuild = $true
                ResultSetName = 'PB-0009'
                VerifyNoSourceChanges = $true
            }
            if ($GitHubActions) {
                $testArguments.GitHubActions = $true
                $testArguments.DotnetExecutable = $dotnetExecutable
            }
            & $testValidatorPath @testArguments
        }

        $completed = $true
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-CoreLog ''
    Write-CoreLog "[FAIL] Core CI stopped: $($_.Exception.Message)"
    throw
}
finally {
    $pipelineStopwatch.Stop()
    Write-CoreLog ''
    Write-CoreLog 'Core CI summary'
    foreach ($stage in $script:StageResults) {
        Write-CoreLog (
            "  [$($stage.Status)] $($stage.Name) ($($stage.Duration.ToString('c')))"
        )
    }
    $pipelineStatus = if ($completed) { 'PASS' } else { 'FAIL' }
    Write-CoreLog "  [$pipelineStatus] Total ($($pipelineStopwatch.Elapsed.ToString('c')))"

    Restore-CoreEnvironment
    [System.IO.File]::WriteAllLines(
        $logPath,
        $script:LogLines,
        (New-Object System.Text.UTF8Encoding($false))
    )
}
