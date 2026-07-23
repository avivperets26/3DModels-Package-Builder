[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:FailureCount = 0
$script:PassCount = 0
$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')

$script:ExpectedProjects = @(
    'src/PackageBuilder.App.Wpf/PackageBuilder.App.Wpf.csproj',
    'src/PackageBuilder.Application/PackageBuilder.Application.csproj',
    'src/PackageBuilder.Cli/PackageBuilder.Cli.csproj',
    'src/PackageBuilder.Contracts/PackageBuilder.Contracts.csproj',
    'src/PackageBuilder.Domain/PackageBuilder.Domain.csproj',
    'src/PackageBuilder.Infrastructure/PackageBuilder.Infrastructure.csproj',
    'src/PackageBuilder.Marketplaces.Fab/PackageBuilder.Marketplaces.Fab.csproj',
    'src/PackageBuilder.Targets.Portable/PackageBuilder.Targets.Portable.csproj',
    'src/PackageBuilder.Targets.Unity/PackageBuilder.Targets.Unity.csproj',
    'src/PackageBuilder.Targets.Unreal/PackageBuilder.Targets.Unreal.csproj',
    'src/PackageBuilder.Tools.Blender/PackageBuilder.Tools.Blender.csproj',
    'tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj',
    'tests/PackageBuilder.Contract.Tests/PackageBuilder.Contract.Tests.csproj',
    'tests/PackageBuilder.Domain.Tests/PackageBuilder.Domain.Tests.csproj',
    'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj'
)

$script:ExpectedPackageVersions = @{
    'coverlet.collector' = '10.0.1'
    'Microsoft.NET.Test.Sdk' = '18.8.1'
    'xunit.v3.mtp-off' = '3.2.2'
    'xunit.runner.visualstudio' = '3.1.5'
}

$script:ForbiddenLegacyXunitPackages = @(
    'xunit',
    'xunit.abstractions',
    'xunit.assert',
    'xunit.core',
    'xunit.extensibility.core',
    'xunit.extensibility.execution'
)

$script:ExpectedTestProjects = @(
    'tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj',
    'tests/PackageBuilder.Contract.Tests/PackageBuilder.Contract.Tests.csproj',
    'tests/PackageBuilder.Domain.Tests/PackageBuilder.Domain.Tests.csproj',
    'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj'
)

function Invoke-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Check
    )

    try {
        & $Check
        $script:PassCount++
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    catch {
        $script:FailureCount++
        Write-Host "[FAIL] $Name" -ForegroundColor Red
        Write-Host "       $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $script:RepositoryRoot) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-ContainedPath $resolved)) {
        throw "Path escapes the repository root: $resolved"
    }

    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return $resolved.Substring($prefix.Length).Replace('\', '/')
}

function Get-XmlDocument {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $script:RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required XML file is missing: $RelativePath"
    }

    try {
        return [xml](Get-Content -Raw -LiteralPath $fullPath -Encoding UTF8)
    }
    catch {
        throw "${RelativePath} is not valid XML: $($_.Exception.Message)"
    }
}

function Get-SinglePropertyNode {
    param(
        [Parameter(Mandatory)][xml]$Xml,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Source
    )

    $nodes = @($Xml.SelectNodes("/Project/PropertyGroup/$Name"))
    if ($nodes.Count -ne 1) {
        throw "$Source must define $Name exactly once; found $($nodes.Count)."
    }

    return $nodes[0]
}

function Assert-PropertyValue {
    param(
        [Parameter(Mandatory)][xml]$Xml,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Expected,
        [Parameter(Mandatory)][string]$Source
    )

    $node = Get-SinglePropertyNode -Xml $Xml -Name $Name -Source $Source
    if ($node.InnerText.Trim() -cne $Expected) {
        throw "$Source property $Name must be '$Expected'; found '$($node.InnerText.Trim())'."
    }
}

function Test-StableExactVersion {
    param([Parameter(Mandatory)][string]$Version)

    return $Version -match '^\d+\.\d+\.\d+(?:\.\d+)?$'
}

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

Invoke-Check 'Required central configuration and exact 15-project inventory exist' {
    $requiredFiles = @(
        'global.json',
        'Directory.Build.props',
        'Directory.Packages.props',
        'NuGet.config',
        'scripts/Enter-PackageBuilderEnvironment.ps1'
    )
    $missing = @($requiredFiles | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot $_) -PathType Leaf)
    })
    if ($missing.Count -gt 0) {
        throw "Missing required central files: $($missing -join ', ')"
    }

    $actualProjects = @(
        foreach ($directory in @('src', 'tests')) {
            Get-ChildItem -LiteralPath (Join-Path $script:RepositoryRoot $directory) -Recurse -File -Filter '*.csproj' |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
                ForEach-Object { Get-RepositoryRelativePath $_.FullName }
        }
    ) | Sort-Object

    $differences = @(Compare-Object -ReferenceObject ($script:ExpectedProjects | Sort-Object) -DifferenceObject $actualProjects)
    if ($differences.Count -gt 0) {
        $details = @($differences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        throw "Project inventory differs from the approved 15 projects: $($details -join '; ')"
    }
}

Invoke-Check 'global.json enforces the exact approved SDK policy' {
    $globalPath = Join-Path $script:RepositoryRoot 'global.json'
    $globalJson = Get-Content -Raw -LiteralPath $globalPath -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $globalJson.sdk) { throw 'global.json has no sdk object.' }
    if ($globalJson.sdk.version -cne '10.0.302') {
        throw "Expected SDK 10.0.302; found '$($globalJson.sdk.version)'."
    }
    if ($globalJson.sdk.rollForward -cne 'disable') {
        throw "Expected rollForward 'disable'; found '$($globalJson.sdk.rollForward)'."
    }
    if ($globalJson.sdk.allowPrerelease -ne $false) {
        throw 'Expected allowPrerelease false.'
    }

    $sdkProperties = @($globalJson.sdk.PSObject.Properties.Name | Sort-Object)
    $expectedProperties = @('allowPrerelease', 'rollForward', 'version') | Sort-Object
    if (@(Compare-Object $expectedProperties $sdkProperties).Count -gt 0) {
        throw "global.json sdk must contain only: $($expectedProperties -join ', ')."
    }

    $rootProperties = @($globalJson.PSObject.Properties.Name)
    if ($rootProperties.Count -ne 1 -or $rootProperties[0] -cne 'sdk') {
        throw 'global.json must contain only the sdk object so dotnet test remains on the VSTest runner.'
    }
}

Invoke-Check 'Directory.Build.props enforces shared compiler, analyzer, and reproducibility policy' {
    $props = Get-XmlDocument 'Directory.Build.props'
    $requiredValues = [ordered]@{
        Nullable = 'enable'
        ImplicitUsings = 'enable'
        Deterministic = 'true'
        DeterministicSourcePaths = 'true'
        DebugType = 'portable'
        PathMap = '$(MSBuildProjectDirectory)=/_/$(MSBuildProjectName)'
        EnableNETAnalyzers = 'true'
        RunAnalyzersDuringBuild = 'true'
        AnalysisLevel = 'latest-recommended'
        EnforceCodeStyleInBuild = 'true'
        TreatWarningsAsErrors = 'true'
        CodeAnalysisTreatWarningsAsErrors = 'true'
    }
    foreach ($entry in $requiredValues.GetEnumerator()) {
        Assert-PropertyValue -Xml $props -Name $entry.Key -Expected $entry.Value -Source 'Directory.Build.props'
    }

    $ciNode = Get-SinglePropertyNode -Xml $props -Name 'ContinuousIntegrationBuild' -Source 'Directory.Build.props'
    if ($ciNode.InnerText.Trim() -cne 'true') {
        throw 'ContinuousIntegrationBuild must evaluate to true when its condition matches.'
    }
    $condition = [string]$ciNode.Condition
    foreach ($signal in @('$(CI)', '$(GITHUB_ACTIONS)', '$(TF_BUILD)')) {
        if (-not $condition.Contains($signal)) {
            throw "ContinuousIntegrationBuild condition is missing $signal."
        }
    }
    if ($condition -notmatch "'true'") {
        throw 'ContinuousIntegrationBuild condition must require an active CI signal.'
    }
}

Invoke-Check 'Directory.Packages.props enforces central management and approved stable versions' {
    $packages = Get-XmlDocument 'Directory.Packages.props'
    $requiredValues = [ordered]@{
        ManagePackageVersionsCentrally = 'true'
        CentralPackageVersionOverrideEnabled = 'false'
        CentralPackageTransitivePinningEnabled = 'false'
        RestorePackagesWithLockFile = 'true'
    }
    foreach ($entry in $requiredValues.GetEnumerator()) {
        Assert-PropertyValue -Xml $packages -Name $entry.Key -Expected $entry.Value -Source 'Directory.Packages.props'
    }

    $packageVersions = @($packages.SelectNodes('/Project/ItemGroup/PackageVersion'))
    if ($packageVersions.Count -ne $script:ExpectedPackageVersions.Count) {
        throw "Expected exactly $($script:ExpectedPackageVersions.Count) central package versions; found $($packageVersions.Count)."
    }

    $seen = @{}
    foreach ($packageVersion in $packageVersions) {
        $name = [string]$packageVersion.Include
        $version = [string]$packageVersion.Version
        if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($version)) {
            throw 'Every PackageVersion requires non-empty Include and Version attributes.'
        }
        if ($seen.ContainsKey($name)) { throw "Duplicate central package version: $name" }
        $seen[$name] = $true
        if (-not $script:ExpectedPackageVersions.ContainsKey($name)) {
            throw "Unapproved central package: $name"
        }
        if ($version -cne $script:ExpectedPackageVersions[$name]) {
            throw "$name must use $($script:ExpectedPackageVersions[$name]); found $version."
        }
        if (-not (Test-StableExactVersion $version)) {
            throw "$name uses a floating, wildcard, range, or prerelease version: $version"
        }
        if ($packageVersion.HasAttribute('Update') -or $packageVersion.HasAttribute('VersionOverride')) {
            throw "$name must use an Include plus one central Version only."
        }
    }
}

Invoke-Check 'All projects inherit central properties and package versions without escapes' {
    foreach ($relativeProject in $script:ExpectedProjects) {
        $project = Get-XmlDocument $relativeProject
        foreach ($forbiddenProperty in @(
            'Nullable',
            'ImplicitUsings',
            'ManagePackageVersionsCentrally',
            'CentralPackageVersionOverrideEnabled',
            'RestorePackagesWithLockFile'
        )) {
            if (@($project.SelectNodes("/Project/PropertyGroup/$forbiddenProperty")).Count -gt 0) {
                throw "$relativeProject duplicates centrally inherited property $forbiddenProperty."
            }
        }

        $isTestProject = $script:ExpectedTestProjects -contains $relativeProject
        $packageReferences = @($project.SelectNodes('/Project/ItemGroup/PackageReference'))
        if (-not $isTestProject -and $packageReferences.Count -gt 0) {
            throw "$relativeProject contains unapproved production PackageReference items."
        }

        $seenPackages = @{}
        foreach ($packageReference in $packageReferences) {
            $name = [string]$packageReference.Include
            if ([string]::IsNullOrWhiteSpace($name)) {
                throw "$relativeProject contains a PackageReference without Include."
            }
            if (-not $script:ExpectedPackageVersions.ContainsKey($name)) {
                throw "$relativeProject references unapproved package $name."
            }
            if ($seenPackages.ContainsKey($name)) {
                throw "$relativeProject references $name more than once."
            }
            $seenPackages[$name] = $true
            if ($packageReference.HasAttribute('Version') -or $packageReference.HasAttribute('VersionOverride') -or
                @($packageReference.SelectNodes('./Version|./VersionOverride')).Count -gt 0) {
                throw "$relativeProject must not define an inline version for $name."
            }
        }
        if ($isTestProject) {
            Assert-PropertyValue -Xml $project -Name 'OutputType' -Expected 'Exe' -Source $relativeProject
            Assert-PropertyValue -Xml $project -Name 'IsTestProject' -Expected 'true' -Source $relativeProject
            Assert-PropertyValue -Xml $project -Name 'IsPackable' -Expected 'false' -Source $relativeProject

            foreach ($mtpProperty in @('UseMicrosoftTestingPlatformRunner', 'TestingPlatformDotnetTestSupport')) {
                if (@($project.SelectNodes("/Project/PropertyGroup/$mtpProperty")).Count -gt 0) {
                    throw "$relativeProject must not define $mtpProperty; VSTest is the approved dotnet test runner."
                }
            }

            $missingPackages = @($script:ExpectedPackageVersions.Keys | Where-Object {
                -not $seenPackages.ContainsKey($_)
            })
            if ($missingPackages.Count -gt 0 -or $seenPackages.Count -ne $script:ExpectedPackageVersions.Count) {
                throw "$relativeProject must reference exactly the four approved test packages. Missing: $($missingPackages -join ', ')"
            }

            foreach ($restrictedPackage in @('coverlet.collector', 'xunit.runner.visualstudio')) {
                $packageReference = @($packageReferences | Where-Object {
                    [string]$_.Include -ceq $restrictedPackage
                })
                if ($packageReference.Count -ne 1 -or
                    [string]$packageReference[0].PrivateAssets -cne 'all' -or
                    [string]$packageReference[0].IncludeAssets -cne 'runtime; build; native; contentfiles; analyzers; buildtransitive') {
                    throw "$relativeProject must preserve the approved PrivateAssets and IncludeAssets restrictions for $restrictedPackage."
                }
            }
        }

        $projectDirectory = Split-Path (Join-Path $script:RepositoryRoot $relativeProject) -Parent
        foreach ($projectReference in @($project.SelectNodes('/Project/ItemGroup/ProjectReference'))) {
            $include = [string]$projectReference.Include
            if ([string]::IsNullOrWhiteSpace($include) -or [System.IO.Path]::IsPathRooted($include)) {
                throw "$relativeProject has an empty or rooted ProjectReference: '$include'."
            }
            $resolvedReference = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $include))
            if (-not (Test-ContainedPath $resolvedReference) -or
                -not (Test-Path -LiteralPath $resolvedReference -PathType Leaf)) {
                throw "$relativeProject has a missing or escaping ProjectReference: $include"
            }
        }

        foreach ($import in @($project.SelectNodes('/Project/Import'))) {
            $importPath = [string]$import.Project
            if ([System.IO.Path]::IsPathRooted($importPath) -or $importPath -match '(^|[\\/])\.\.([\\/]|$)') {
                throw "$relativeProject has an escaping explicit Import: $importPath"
            }
        }
    }
}

Invoke-Check 'NuGet source and package location are explicit, portable, and contained' {
    $nuget = Get-XmlDocument 'NuGet.config'
    if (@($nuget.SelectNodes('/configuration/packageSources/clear')).Count -ne 1) {
        throw 'NuGet.config must clear inherited package sources.'
    }
    $sources = @($nuget.SelectNodes('/configuration/packageSources/add'))
    if ($sources.Count -ne 1) {
        throw "NuGet.config must define exactly one package source; found $($sources.Count)."
    }
    if ([string]$sources[0].key -cne 'nuget.org' -or
        [string]$sources[0].value -cne 'https://api.nuget.org/v3/index.json' -or
        [string]$sources[0].protocolVersion -cne '3') {
        throw 'The only approved source is nuget.org at https://api.nuget.org/v3/index.json using protocol 3.'
    }
    if (@($nuget.SelectNodes('/configuration/packageSourceCredentials')).Count -gt 0) {
        throw 'NuGet.config must not contain package source credentials.'
    }
    if (@($nuget.SelectNodes('/configuration/disabledPackageSources/clear')).Count -ne 1) {
        throw 'NuGet.config must clear inherited disabled-source state.'
    }

    $configEntries = @($nuget.SelectNodes('/configuration/config/add'))
    if ($configEntries.Count -ne 1 -or [string]$configEntries[0].key -cne 'globalPackagesFolder') {
        throw 'NuGet.config must define only the globalPackagesFolder config entry.'
    }
    $packagePath = [string]$configEntries[0].value
    if ($packagePath -cne 'runtime-data/nuget-packages' -or
        [System.IO.Path]::IsPathRooted($packagePath) -or
        $packagePath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "globalPackagesFolder must be the portable contained path runtime-data/nuget-packages; found '$packagePath'."
    }
    $resolvedPackagePath = [System.IO.Path]::GetFullPath((Join-Path $script:RepositoryRoot $packagePath))
    if (-not (Test-ContainedPath $resolvedPackagePath)) {
        throw "globalPackagesFolder escapes the repository: $resolvedPackagePath"
    }

    $additionalConfigs = @(
        foreach ($directory in @('src', 'tests', 'scripts', 'docs', '.github')) {
            $fullDirectory = Join-Path $script:RepositoryRoot $directory
            if (Test-Path -LiteralPath $fullDirectory -PathType Container) {
                Get-ChildItem -LiteralPath $fullDirectory -Recurse -File -Filter 'NuGet.config'
            }
        }
    )
    if ($additionalConfigs.Count -gt 0) {
        throw "Nested NuGet.config files are not approved: $(($additionalConfigs.FullName | ForEach-Object { Get-RepositoryRelativePath $_ }) -join ', ')"
    }
}

Invoke-Check 'Environment entry contains every NuGet and CLI cache beneath the repository' {
    $environmentPath = Join-Path $script:RepositoryRoot 'scripts\Enter-PackageBuilderEnvironment.ps1'
    $environmentText = Get-Content -Raw -LiteralPath $environmentPath -Encoding UTF8
    $directoryMatches = @([regex]::Matches(
        $environmentText,
        "(?m)^\s*(?<name>CliHome|NuGetPackages|NuGetHttpCache|NuGetScratch|NuGetPluginsCache|Temp)\s*=\s*Join-Path\s+\`$projectRoot\s+'(?<path>[^']+)'\s*$"
    ))
    $expectedDirectories = @{
        CliHome = 'runtime-data\cli-home'
        NuGetPackages = 'runtime-data\nuget-packages'
        NuGetHttpCache = 'runtime-data\nuget-http-cache'
        NuGetScratch = 'runtime-data\nuget-scratch'
        NuGetPluginsCache = 'runtime-data\nuget-plugins-cache'
        Temp = 'runtime-data\temp'
    }
    if ($directoryMatches.Count -ne $expectedDirectories.Count) {
        throw "Environment script must define exactly $($expectedDirectories.Count) contained runtime directories; found $($directoryMatches.Count)."
    }
    foreach ($match in $directoryMatches) {
        $name = $match.Groups['name'].Value
        $path = $match.Groups['path'].Value
        if (-not $expectedDirectories.ContainsKey($name) -or $path -cne $expectedDirectories[$name]) {
            throw "Unexpected runtime directory mapping: $name = $path"
        }
        if (-not (Test-ContainedPath ([System.IO.Path]::GetFullPath((Join-Path $script:RepositoryRoot $path))))) {
            throw "Runtime directory escapes the repository: $path"
        }
    }

    $expectedAssignments = @{
        DOTNET_CLI_HOME = 'CliHome'
        NUGET_PACKAGES = 'NuGetPackages'
        NUGET_HTTP_CACHE_PATH = 'NuGetHttpCache'
        NUGET_SCRATCH = 'NuGetScratch'
        NUGET_PLUGINS_CACHE_PATH = 'NuGetPluginsCache'
        TEMP = 'Temp'
        TMP = 'Temp'
    }
    foreach ($entry in $expectedAssignments.GetEnumerator()) {
        $pattern = "(?m)^\s*\`$env:$([regex]::Escape($entry.Key))\s*=\s*\`$runtimeDirectories\.$([regex]::Escape($entry.Value))\s*$"
        if ($environmentText -notmatch $pattern) {
            throw "Environment script must map $($entry.Key) to $($entry.Value)."
        }
    }

    foreach ($optOutVariable in @('DOTNET_CLI_TELEMETRY_OPTOUT', 'TESTINGPLATFORM_TELEMETRY_OPTOUT')) {
        $pattern = "(?m)^\s*\`$env:$([regex]::Escape($optOutVariable))\s*=\s*'1'\s*$"
        if ($environmentText -notmatch $pattern) {
            throw "Environment script must set $optOutVariable to 1."
        }
    }
}

Invoke-Check 'Every project has a consistent deterministic NuGet lock file without legacy xUnit v2 dependencies' {
    foreach ($relativeProject in $script:ExpectedProjects) {
        $projectDirectory = Split-Path (Join-Path $script:RepositoryRoot $relativeProject) -Parent
        $lockPath = Join-Path $projectDirectory 'packages.lock.json'
        if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
            throw "$relativeProject is missing packages.lock.json. Run dotnet restore without --locked-mode to generate it."
        }

        $lockText = Get-Content -Raw -LiteralPath $lockPath -Encoding UTF8
        if ($lockText -match '(?i)\b[A-Z]:[\\/]' -or $lockText -match '(?i)(?<![A-Za-z0-9_])/(?:home|Users)/') {
            throw "$(Get-RepositoryRelativePath $lockPath) contains an absolute workstation path."
        }
        $lock = $lockText | ConvertFrom-Json
        if ($lock.version -ne 2) {
            throw "$(Get-RepositoryRelativePath $lockPath) must use lock-file format version 2."
        }

        $frameworks = @($lock.dependencies.PSObject.Properties)
        if ($frameworks.Count -ne 1) {
            throw "$(Get-RepositoryRelativePath $lockPath) must contain exactly one target framework; found $($frameworks.Count)."
        }

        $isTestProject = $script:ExpectedTestProjects -contains $relativeProject
        $directPackages = @{}
        foreach ($dependency in @($frameworks[0].Value.PSObject.Properties)) {
            if ($script:ForbiddenLegacyXunitPackages -contains $dependency.Name) {
                throw "$(Get-RepositoryRelativePath $lockPath) contains forbidden legacy xUnit v2 package $($dependency.Name)."
            }

            $entry = $dependency.Value
            if ([string]$entry.type -eq 'Project') { continue }
            $resolved = [string]$entry.resolved
            if (-not (Test-StableExactVersion $resolved)) {
                throw "$(Get-RepositoryRelativePath $lockPath) contains an unstable resolved version for $($dependency.Name): $resolved"
            }
            if ([string]$entry.type -eq 'Direct') {
                if (-not $isTestProject) {
                    throw "$(Get-RepositoryRelativePath $lockPath) contains unexpected direct package $($dependency.Name)."
                }
                if (-not $script:ExpectedPackageVersions.ContainsKey($dependency.Name)) {
                    throw "$(Get-RepositoryRelativePath $lockPath) contains unapproved direct package $($dependency.Name)."
                }
                $expectedVersion = $script:ExpectedPackageVersions[$dependency.Name]
                if ($resolved -cne $expectedVersion -or [string]$entry.requested -cne "[$expectedVersion, )") {
                    throw "$(Get-RepositoryRelativePath $lockPath) does not lock $($dependency.Name) to approved version $expectedVersion."
                }
                $directPackages[$dependency.Name] = $true
            }
        }

        if ($isTestProject) {
            $missingDirect = @($script:ExpectedPackageVersions.Keys | Where-Object {
                -not $directPackages.ContainsKey($_)
            })
            if ($missingDirect.Count -gt 0 -or $directPackages.Count -ne $script:ExpectedPackageVersions.Count) {
                throw "$(Get-RepositoryRelativePath $lockPath) is missing direct packages: $($missingDirect -join ', ')"
            }
        }
    }

    $allLocks = @(
        foreach ($directory in @('src', 'tests')) {
            Get-ChildItem -LiteralPath (Join-Path $script:RepositoryRoot $directory) -Recurse -File -Filter 'packages.lock.json' |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        }
    )
    if ($allLocks.Count -ne $script:ExpectedProjects.Count) {
        throw "Expected exactly $($script:ExpectedProjects.Count) project lock files; found $($allLocks.Count)."
    }
}

if ($script:FailureCount -gt 0) {
    throw "Central build configuration validation failed: $($script:PassCount) passed, $($script:FailureCount) failed."
}

Write-Host (
    "Central build configuration validation passed: $($script:ExpectedProjects.Count) projects, " +
    "$($script:ExpectedPackageVersions.Count) central packages, $($script:PassCount) checks, 0 failures."
) -ForegroundColor Green
