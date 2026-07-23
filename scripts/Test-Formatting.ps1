[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Fix
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
$logDirectory = Join-Path $repositoryRootPath 'logs\validation\PB-0007'
$logPath = Join-Path $logDirectory 'formatting-validation.log'
$validationDirectory = Join-Path $repositoryRootPath 'artifacts\validation\PB-0007'
$temporaryDirectory = Join-Path $validationDirectory 'temp'
$script:LogLines = New-Object 'System.Collections.Generic.List[string]'
$script:OriginalEnvironment = @{}
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

function Write-ValidationLog {
    param([Parameter(Mandatory)][string]$Message)

    $script:LogLines.Add($Message)
    Write-Host $Message
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string[]]$Arguments
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
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode."
    }
    Write-ValidationLog "[PASS] $Name"
}

function Get-FormatCandidateHashes {
    $paths = @(
        & git -C $repositoryRootPath ls-files
        & git -C $repositoryRootPath ls-files --others --exclude-standard
    ) | Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate repository formatting candidates.'
    }

    $hashes = @{}
    foreach ($relativePath in $paths) {
        $fullPath = Join-Path $repositoryRootPath $relativePath
        if (-not (Test-ContainedPath $fullPath) -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Formatting candidate is invalid: $relativePath"
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
        throw "Default validation modified formatting candidates: $($changed -join ', ')"
    }
}

function Set-ContainedEnvironment {
    param(
        [Parameter(Mandatory)][string]$DotnetRoot,
        [Parameter(Mandatory)][string]$RuffCache
    )

    foreach ($name in $script:EnvironmentNames) {
        $script:OriginalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    $values = @{
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
        RUFF_CACHE_DIR = $RuffCache
    }

    foreach ($entry in $values.GetEnumerator()) {
        if (-not (Test-ContainedPath $entry.Value) -and $entry.Key -notmatch '^(DOTNET_|TESTINGPLATFORM_)') {
            throw "Environment path escapes the repository root: $($entry.Key)=$($entry.Value)"
        }
        if ($entry.Key -notmatch '^(DOTNET_MULTILEVEL_LOOKUP|DOTNET_SKIP_FIRST_TIME_EXPERIENCE|DOTNET_NOLOGO|DOTNET_CLI_TELEMETRY_OPTOUT|TESTINGPLATFORM_TELEMETRY_OPTOUT)$') {
            if (-not (Test-Path -LiteralPath $entry.Value -PathType Container)) {
                New-Item -ItemType Directory -Path $entry.Value -Force | Out-Null
            }
        }
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}

function Restore-Environment {
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

foreach ($requiredFile in @($solutionPath, $globalJsonPath, $ruffConfigurationPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required formatting input is missing: $requiredFile"
    }
}

$globalJson = Get-Content -Raw -LiteralPath $globalJsonPath -Encoding UTF8 | ConvertFrom-Json
$dotnetVersion = [string]$globalJson.sdk.version
if ($dotnetVersion -ne '10.0.302') {
    throw "Expected repository-local .NET SDK 10.0.302; found '$dotnetVersion'."
}
$dotnetRoot = Join-Path $repositoryRootPath "tools\dotnet\$dotnetVersion"
$dotnetExecutable = Join-Path $dotnetRoot 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable -PathType Leaf)) {
    throw "Repository-local .NET SDK is missing: $dotnetExecutable"
}

$ruffConfiguration = Get-Content -Raw -LiteralPath $ruffConfigurationPath -Encoding UTF8
$ruffVersionMatch = [regex]::Match(
    $ruffConfiguration,
    '(?m)^\s*required-version\s*=\s*"==(?<version>\d+\.\d+\.\d+)"\s*$'
)
if (-not $ruffVersionMatch.Success) {
    throw 'ruff.toml must contain an exact required-version pin.'
}
$ruffVersion = $ruffVersionMatch.Groups['version'].Value
$ruffExecutable = Join-Path $repositoryRootPath "tools\ruff\$ruffVersion\ruff.exe"
if (-not (Test-Path -LiteralPath $ruffExecutable -PathType Leaf)) {
    throw "Repository-local Ruff is missing: $ruffExecutable. Run scripts\Install-Ruff.ps1."
}

foreach ($directory in @($logDirectory, $validationDirectory, $temporaryDirectory)) {
    if (-not (Test-ContainedPath $directory)) {
        throw "Validation directory escapes the repository root: $directory"
    }
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}

$beforeHashes = if ($Fix) { $null } else { Get-FormatCandidateHashes }
$completed = $false

try {
    Set-ContainedEnvironment -DotnetRoot $dotnetRoot -RuffCache (Join-Path $repositoryRootPath 'runtime-data\ruff-cache')
    Push-Location $repositoryRootPath
    try {
        $dotnetVersionOutput = @(& $dotnetExecutable --version 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($dotnetVersionOutput -join ' ').Trim() -ne $dotnetVersion) {
            throw "Repository-local .NET SDK version check failed: $($dotnetVersionOutput -join ' ')"
        }
        Write-ValidationLog "[PASS] .NET SDK version $dotnetVersion"

        $ruffVersionOutput = @(& $ruffExecutable --version 2>&1)
        if ($LASTEXITCODE -ne 0 -or ($ruffVersionOutput -join ' ').Trim() -ne "ruff $ruffVersion") {
            throw "Repository-local Ruff version check failed: $($ruffVersionOutput -join ' ')"
        }
        Write-ValidationLog "[PASS] Ruff version $ruffVersion"

        if ($Fix) {
            Write-ValidationLog '[INFO] Explicit fix mode selected; safe formatting changes may be written.'
            Invoke-Tool `
                -Name '.NET formatting fix' `
                -Executable $dotnetExecutable `
                -Arguments @(
                    'format',
                    $solutionPath,
                    '--no-restore',
                    '--severity',
                    'info',
                    '--verbosity',
                    'minimal'
                )
            Invoke-Tool `
                -Name 'Ruff safe lint fix' `
                -Executable $ruffExecutable `
                -Arguments @(
                    'check',
                    '--config',
                    $ruffConfigurationPath,
                    '--fix',
                    '.'
                )
            Invoke-Tool `
                -Name 'Ruff formatting fix' `
                -Executable $ruffExecutable `
                -Arguments @(
                    'format',
                    '--config',
                    $ruffConfigurationPath,
                    '.'
                )
        }
        else {
            Write-ValidationLog '[INFO] Verification mode selected; source files must remain unchanged.'
        }

        Invoke-Tool `
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
            )
        Invoke-Tool `
            -Name 'Ruff lint verification' `
            -Executable $ruffExecutable `
            -Arguments @(
                'check',
                '--config',
                $ruffConfigurationPath,
                '--no-fix',
                '.'
            )
        Invoke-Tool `
            -Name 'Ruff formatting verification' `
            -Executable $ruffExecutable `
            -Arguments @(
                'format',
                '--config',
                $ruffConfigurationPath,
                '--check',
                '.'
            )

        if (-not $Fix) {
            Assert-HashesUnchanged -Before $beforeHashes -After (Get-FormatCandidateHashes)
            Write-ValidationLog '[PASS] Verification mode did not modify source files.'
        }

        $completed = $true
        Write-ValidationLog '[PASS] Repository formatting validation succeeded.'
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
        Write-Host "Formatting validation failed. See $logPath" -ForegroundColor Red
    }
}
