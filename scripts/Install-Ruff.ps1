[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$ruffVersion = '0.15.22'
$artifactName = 'ruff-x86_64-pc-windows-msvc.zip'
$expectedSha256 = '6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d'
$releaseBaseUrl = "https://releases.astral.sh/github/ruff/releases/download/$ruffVersion"
$archiveUrl = "$releaseBaseUrl/$artifactName"
$checksumUrl = "$archiveUrl.sha256"

$repositoryRootPath = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$downloadDirectory = Join-Path $repositoryRootPath "downloads\ruff\$ruffVersion"
$archivePath = Join-Path $downloadDirectory $artifactName
$checksumPath = "$archivePath.sha256"
$toolDirectory = Join-Path $repositoryRootPath "tools\ruff\$ruffVersion"
$ruffExecutable = Join-Path $toolDirectory 'ruff.exe'
$temporaryDirectory = Join-Path $repositoryRootPath "artifacts\setup\PB-0007\ruff-$ruffVersion"
$logDirectory = Join-Path $repositoryRootPath 'logs\setup\PB-0007'
$logPath = Join-Path $logDirectory 'ruff-install.log'
$script:LogLines = New-Object 'System.Collections.Generic.List[string]'

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $repositoryRootPath + [System.IO.Path]::DirectorySeparatorChar
    return $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-ManagedDirectory {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-ContainedPath $Path)) {
        throw "Managed path escapes the repository root: $Path"
    }
    if ([System.StringComparer]::OrdinalIgnoreCase.Equals(
            [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/'),
            $repositoryRootPath
        )) {
        throw 'A managed directory must not be the repository root.'
    }
}

function Write-SetupLog {
    param([Parameter(Mandatory)][string]$Message)

    $line = "[$([DateTime]::UtcNow.ToString('o'))] $Message"
    $script:LogLines.Add($line)
    Write-Host $Message
}

function Get-GitTopLevel {
    $output = @(& git -C $repositoryRootPath rev-parse --show-toplevel 2>&1)
    if ($LASTEXITCODE -ne 0 -or $output.Count -ne 1) {
        throw "Unable to resolve the Git top level for '$repositoryRootPath'."
    }
    return [System.IO.Path]::GetFullPath([string]$output[0]).TrimEnd([char[]]'\/')
}

if (-not (Test-Path -LiteralPath $repositoryRootPath -PathType Container)) {
    throw "Repository root does not exist: $repositoryRootPath"
}

$gitRoot = Get-GitTopLevel
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $repositoryRootPath)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

foreach ($managedDirectory in @($downloadDirectory, $toolDirectory, $temporaryDirectory, $logDirectory)) {
    Assert-ManagedDirectory $managedDirectory
}

foreach ($requiredFile in @('PackageBuilder.sln', 'ruff.toml')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRootPath $requiredFile) -PathType Leaf)) {
        throw "Repository marker is missing: $requiredFile"
    }
}

foreach ($directory in @($downloadDirectory, $logDirectory)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
}

try {
    Write-SetupLog "Installing repository-local Ruff $ruffVersion."
    Write-SetupLog "Official archive: $archiveUrl"
    Write-SetupLog "Official checksum: $checksumUrl"

    if ($Force -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        Write-SetupLog "Downloading $artifactName to $archivePath"
        Invoke-WebRequest -UseBasicParsing -Uri $archiveUrl -OutFile $archivePath
    }
    else {
        Write-SetupLog "Using existing archive at $archivePath"
    }

    if ($Force -or -not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) {
        Write-SetupLog "Downloading the official SHA-256 file to $checksumPath"
        Invoke-WebRequest -UseBasicParsing -Uri $checksumUrl -OutFile $checksumPath
    }
    else {
        Write-SetupLog "Using existing checksum file at $checksumPath"
    }

    $checksumText = (Get-Content -Raw -LiteralPath $checksumPath).Trim()
    $checksumMatch = [regex]::Match(
        $checksumText,
        "^(?<hash>[0-9a-fA-F]{64})\s+\*?$([regex]::Escape($artifactName))$"
    )
    if (-not $checksumMatch.Success) {
        throw "The official checksum file has an unexpected format: $checksumText"
    }

    $publishedSha256 = $checksumMatch.Groups['hash'].Value.ToLowerInvariant()
    if ($publishedSha256 -ne $expectedSha256) {
        throw "Published SHA-256 '$publishedSha256' does not match the tracked pin '$expectedSha256'."
    }

    $actualSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "Downloaded Ruff archive failed SHA-256 verification. Expected $expectedSha256; found $actualSha256."
    }
    Write-SetupLog "SHA-256 verified: $actualSha256"

    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryDirectory -Force

    $extractedExecutables = @(
        Get-ChildItem -LiteralPath $temporaryDirectory -Filter 'ruff.exe' -File -Recurse
    )
    if ($extractedExecutables.Count -ne 1) {
        throw "Expected exactly one ruff.exe in the verified archive; found $($extractedExecutables.Count)."
    }

    if (Test-Path -LiteralPath $toolDirectory) {
        Remove-Item -LiteralPath $toolDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
    Copy-Item -LiteralPath $extractedExecutables[0].FullName -Destination $ruffExecutable

    $versionOutput = @(& $ruffExecutable --version 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($versionOutput -join ' ').Trim() -ne "ruff $ruffVersion") {
        throw "Installed Ruff version check failed. Output: $($versionOutput -join [Environment]::NewLine)"
    }

    Write-SetupLog "Installed executable: $ruffExecutable"
    Write-SetupLog "Version verified: $($versionOutput -join ' ')"
    Write-SetupLog 'Ruff installation completed successfully.'
}
catch {
    Write-SetupLog "ERROR: $($_.Exception.Message)"
    throw
}
finally {
    if (-not (Test-Path -LiteralPath $logDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }
    [System.IO.File]::WriteAllLines(
        $logPath,
        $script:LogLines,
        (New-Object System.Text.UTF8Encoding($false))
    )
}
