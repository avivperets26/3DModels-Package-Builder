[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$script:PassCount = 0
$script:FailureCount = 0
$script:ExpectedPackages = [ordered]@{
    'coverlet.collector' = '10.0.1'
    'Microsoft.NET.Test.Sdk' = '18.8.1'
    'xunit.v3.mtp-off' = '3.2.2'
    'xunit.runner.visualstudio' = '3.1.5'
}
$script:ExpectedProductionPackages = [ordered]@{
    'JsonSchema.Net' = '9.3.0'
}
$script:ProjectSpecifications = @(
    [pscustomobject]@{
        Name = 'PackageBuilder.Domain.Tests'
        Path = 'tests/PackageBuilder.Domain.Tests/PackageBuilder.Domain.Tests.csproj'
        ProductionProject = 'src/PackageBuilder.Domain/PackageBuilder.Domain.csproj'
        ProductionAssembly = 'PackageBuilder.Domain'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Application.Tests'
        Path = 'tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj'
        ProductionProject = 'src/PackageBuilder.Application/PackageBuilder.Application.csproj'
        ProductionAssembly = 'PackageBuilder.Application'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Infrastructure.Tests'
        Path = 'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj'
        ProductionProject = 'src/PackageBuilder.Infrastructure/PackageBuilder.Infrastructure.csproj'
        ProductionAssembly = 'PackageBuilder.Infrastructure'
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Contract.Tests'
        Path = 'tests/PackageBuilder.Contract.Tests/PackageBuilder.Contract.Tests.csproj'
        ProductionProject = 'src/PackageBuilder.Contracts/PackageBuilder.Contracts.csproj'
        ProductionAssembly = 'PackageBuilder.Contracts'
    }
)

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $script:RepositoryRoot) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    if (-not (Test-ContainedPath $resolved)) {
        throw "Path resolves outside the repository root: $resolved"
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

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory)][xml]$Project,
        [Parameter(Mandatory)][string]$Name
    )

    $nodes = @($Project.SelectNodes("/Project/PropertyGroup/$Name"))
    if ($nodes.Count -ne 1) {
        throw "Project property $Name must be defined exactly once; found $($nodes.Count)."
    }

    return $nodes[0].InnerText.Trim()
}

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

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

$gitRootOutput = @(& git -C $script:RepositoryRoot rev-parse --show-toplevel 2>&1)
if ($LASTEXITCODE -ne 0 -or $gitRootOutput.Count -ne 1) {
    throw "Unable to resolve the Git top level for '$script:RepositoryRoot'."
}
$gitRoot = [System.IO.Path]::GetFullPath([string]$gitRootOutput[0]).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $script:RepositoryRoot)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

Invoke-Check 'Exactly the four approved test projects exist' {
    $expected = @($script:ProjectSpecifications | ForEach-Object { $_.Path } | Sort-Object)
    $actual = @(
        Get-ChildItem -LiteralPath (Join-Path $script:RepositoryRoot 'tests') -Recurse -File -Filter '*.csproj' |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object { Get-RepositoryRelativePath $_.FullName }
    ) | Sort-Object

    $differences = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
    if ($differences.Count -gt 0) {
        $details = @($differences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        throw "Test-project inventory differs: $($details -join '; ')"
    }
}

Invoke-Check 'Central test package versions remain exactly pinned' {
    $packages = Get-XmlDocument 'Directory.Packages.props'
    $actualVersions = @($packages.SelectNodes('/Project/ItemGroup/PackageVersion'))
    $expectedCentralCount =
        $script:ExpectedPackages.Count + $script:ExpectedProductionPackages.Count
    if ($actualVersions.Count -ne $expectedCentralCount) {
        throw "Expected exactly $expectedCentralCount approved central packages; found $($actualVersions.Count)."
    }

    $seen = @{}
    foreach ($package in $actualVersions) {
        $name = [string]$package.Include
        $version = [string]$package.Version
        $isTestPackage = $script:ExpectedPackages.Contains($name)
        $isProductionPackage = $script:ExpectedProductionPackages.Contains($name)
        if (-not $isTestPackage -and -not $isProductionPackage) {
            throw "Unexpected central package version: $name"
        }
        if ($seen.ContainsKey($name)) {
            throw "Duplicate central package version: $name"
        }
        $expectedVersion = if ($isTestPackage) {
            $script:ExpectedPackages[$name]
        }
        else {
            $script:ExpectedProductionPackages[$name]
        }
        if ($version -cne $expectedVersion) {
            throw "$name must remain at $expectedVersion; found $version."
        }
        $seen[$name] = $true
    }
}

Invoke-Check 'Each test project uses xUnit v3 and references only its production assembly' {
    foreach ($specification in $script:ProjectSpecifications) {
        $project = Get-XmlDocument $specification.Path
        if ((Get-ProjectProperty -Project $project -Name 'TargetFramework') -cne 'net10.0' -or
            (Get-ProjectProperty -Project $project -Name 'OutputType') -cne 'Exe' -or
            (Get-ProjectProperty -Project $project -Name 'IsTestProject') -cne 'true' -or
            (Get-ProjectProperty -Project $project -Name 'IsPackable') -cne 'false') {
            throw "$($specification.Name) does not preserve the approved executable xUnit v3 test-project properties."
        }

        $packageReferences = @($project.SelectNodes('/Project/ItemGroup/PackageReference'))
        $packageNames = @($packageReferences | ForEach-Object { [string]$_.Include } | Sort-Object)
        $packageDifferences = @(
            Compare-Object `
                -ReferenceObject @($script:ExpectedPackages.Keys | Sort-Object) `
                -DifferenceObject $packageNames
        )
        if ($packageDifferences.Count -gt 0) {
            throw "$($specification.Name) must reference exactly the four approved test packages."
        }
        foreach ($packageReference in $packageReferences) {
            if ($packageReference.HasAttribute('Version') -or
                $packageReference.HasAttribute('VersionOverride') -or
                @($packageReference.SelectNodes('./Version|./VersionOverride')).Count -gt 0) {
                throw "$($specification.Name) contains an inline package version."
            }
        }

        foreach ($restrictedPackage in @('coverlet.collector', 'xunit.runner.visualstudio')) {
            $reference = @($packageReferences | Where-Object { [string]$_.Include -ceq $restrictedPackage })
            if ($reference.Count -ne 1 -or
                [string]$reference[0].PrivateAssets -cne 'all' -or
                [string]$reference[0].IncludeAssets -cne 'runtime; build; native; contentfiles; analyzers; buildtransitive') {
                throw "$($specification.Name) has invalid asset restrictions for $restrictedPackage."
            }
        }

        $xunitUsings = @($project.SelectNodes('/Project/ItemGroup/Using') | Where-Object {
            [string]$_.Include -ceq 'Xunit'
        })
        if ($xunitUsings.Count -ne 1) {
            throw "$($specification.Name) must import Xunit exactly once."
        }

        $projectReferences = @($project.SelectNodes('/Project/ItemGroup/ProjectReference'))
        if ($projectReferences.Count -ne 1) {
            throw "$($specification.Name) must have exactly one direct ProjectReference."
        }
        $projectDirectory = Split-Path (Join-Path $script:RepositoryRoot $specification.Path) -Parent
        $resolvedReference = [System.IO.Path]::GetFullPath(
            (Join-Path $projectDirectory ([string]$projectReferences[0].Include))
        )
        if (-not (Test-ContainedPath $resolvedReference) -or
            -not (Test-Path -LiteralPath $resolvedReference -PathType Leaf)) {
            throw "$($specification.Name) has a missing or escaping production ProjectReference."
        }
        $actualReference = Get-RepositoryRelativePath $resolvedReference
        if ($actualReference -cne $specification.ProductionProject) {
            throw "$($specification.Name) must reference $($specification.ProductionProject); found $actualReference."
        }
    }
}

Invoke-Check 'Each project contains a discoverable categorized assembly smoke test' {
    foreach ($specification in $script:ProjectSpecifications) {
        $projectDirectory = Split-Path (Join-Path $script:RepositoryRoot $specification.Path) -Parent
        $sources = @(
            Get-ChildItem -LiteralPath $projectDirectory -Recurse -File -Filter '*.cs' |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        )
        $matchingSources = @(
            foreach ($source in $sources) {
                $text = Get-Content -Raw -LiteralPath $source.FullName -Encoding UTF8
                $hasFact = $text -match '(?m)^\s*\[\s*Fact(?:Attribute)?(?:\(\s*\))?\s*\]\s*$'
                $hasSmokeTrait = $text -match (
                    '(?m)^\s*\[\s*Trait(?:Attribute)?\s*\(\s*"Category"\s*,\s*"Smoke"\s*\)\s*\]\s*$'
                )
                $hasDiscoverableMethod = $text -match (
                    '(?m)^\s*public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+[A-Za-z_][A-Za-z0-9_]*\s*\(\s*\)'
                )
                $assemblyPattern = 'AssemblyName\s*\(\s*"' +
                    [regex]::Escape($specification.ProductionAssembly) + '"\s*\)'
                if ($hasFact -and $hasSmokeTrait -and $hasDiscoverableMethod -and $text -match $assemblyPattern) {
                    $source
                }
            }
        )
        if ($matchingSources.Count -lt 1) {
            throw (
                "$($specification.Name) has no discoverable [Fact] with Category=Smoke " +
                "that loads $($specification.ProductionAssembly)."
            )
        }
    }
}

if ($script:FailureCount -gt 0) {
    throw "Test-project configuration validation failed: $($script:PassCount) passed, $($script:FailureCount) failed."
}

Write-Host (
    "Test-project configuration validation passed: $($script:ProjectSpecifications.Count) projects, " +
    "$($script:ExpectedPackages.Count) pinned packages, $($script:PassCount) checks, 0 failures."
) -ForegroundColor Green
