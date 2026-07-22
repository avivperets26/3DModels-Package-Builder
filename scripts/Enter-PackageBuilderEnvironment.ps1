[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$requiredRoot = 'C:\Dev\PackageBuilder'

if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($projectRoot, $requiredRoot)) {
    throw "Package Builder must be developed from $requiredRoot. Resolved root: $projectRoot"
}

$dotnetRoot = Join-Path $projectRoot 'tools\dotnet\10.0.302'
$dotnetExecutable = Join-Path $dotnetRoot 'dotnet.exe'

if (-not (Test-Path -LiteralPath $dotnetExecutable -PathType Leaf)) {
    throw "The repository-local .NET SDK is missing: $dotnetExecutable"
}

$runtimeDirectories = @(
    (Join-Path $projectRoot 'runtime-data\cli-home'),
    (Join-Path $projectRoot 'runtime-data\nuget-packages'),
    (Join-Path $projectRoot 'runtime-data\nuget-http-cache'),
    (Join-Path $projectRoot 'runtime-data\temp')
)

foreach ($directory in $runtimeDirectories) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }
}

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = $runtimeDirectories[0]
$env:NUGET_PACKAGES = $runtimeDirectories[1]
$env:NUGET_HTTP_CACHE_PATH = $runtimeDirectories[2]
$env:TEMP = $runtimeDirectories[3]
$env:TMP = $runtimeDirectories[3]
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$pathEntries = $env:PATH -split ';'
if ($pathEntries -notcontains $dotnetRoot) {
    $env:PATH = "$dotnetRoot;$env:PATH"
}

Write-Host "Package Builder environment ready at $projectRoot"
Write-Host "dotnet: $dotnetExecutable"
