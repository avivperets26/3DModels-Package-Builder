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

$runtimeDirectories = [ordered]@{
    CliHome = Join-Path $projectRoot 'runtime-data\cli-home'
    NuGetPackages = Join-Path $projectRoot 'runtime-data\nuget-packages'
    NuGetHttpCache = Join-Path $projectRoot 'runtime-data\nuget-http-cache'
    NuGetScratch = Join-Path $projectRoot 'runtime-data\nuget-scratch'
    NuGetPluginsCache = Join-Path $projectRoot 'runtime-data\nuget-plugins-cache'
    Temp = Join-Path $projectRoot 'runtime-data\temp'
}

foreach ($directory in $runtimeDirectories.Values) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }
}

$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = $runtimeDirectories.CliHome
$env:NUGET_PACKAGES = $runtimeDirectories.NuGetPackages
$env:NUGET_HTTP_CACHE_PATH = $runtimeDirectories.NuGetHttpCache
$env:NUGET_SCRATCH = $runtimeDirectories.NuGetScratch
$env:NUGET_PLUGINS_CACHE_PATH = $runtimeDirectories.NuGetPluginsCache
$env:TEMP = $runtimeDirectories.Temp
$env:TMP = $runtimeDirectories.Temp
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'

$pathEntries = $env:PATH -split ';'
if ($pathEntries -notcontains $dotnetRoot) {
    $env:PATH = "$dotnetRoot;$env:PATH"
}

Write-Host "Package Builder environment ready at $projectRoot"
Write-Host "dotnet: $dotnetExecutable"
