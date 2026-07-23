[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $invokedScriptPath = [string]$MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($invokedScriptPath)) {
        throw 'RepositoryRoot was not supplied and the executing script path is unavailable.'
    }

    $RepositoryRoot = Join-Path (Split-Path $invokedScriptPath -Parent) '..'
}

$script:FailureCount = 0
$script:PassCount = 0
$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')

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

    if ([System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $script:RepositoryRoot)) {
        return '.'
    }

    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return $resolved.Substring($prefix.Length).Replace('\', '/')
}

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory)][xml]$ProjectXml,
        [Parameter(Mandatory)][string]$Name
    )

    foreach ($property in @($ProjectXml.SelectNodes("/Project/PropertyGroup/$Name"))) {
        if (-not [string]::IsNullOrWhiteSpace($property.InnerText)) {
            return $property.InnerText.Trim()
        }
    }

    return $null
}

function Get-ProjectReferences {
    param([Parameter(Mandatory)][xml]$ProjectXml)

    return @($ProjectXml.SelectNodes('/Project/ItemGroup/ProjectReference'))
}

function Get-PackageReferenceNames {
    param([Parameter(Mandatory)][xml]$ProjectXml)

    $names = @()
    foreach ($packageReference in @($ProjectXml.SelectNodes('/Project/ItemGroup/PackageReference'))) {
        if (-not [string]::IsNullOrWhiteSpace([string]$packageReference.Include)) {
            $names += [string]$packageReference.Include
        }
    }

    return $names
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

$projectSpecifications = @(
    [pscustomobject]@{
        Name = 'PackageBuilder.Domain'
        Path = 'src/PackageBuilder.Domain/PackageBuilder.Domain.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @()
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Contracts'
        Path = 'src/PackageBuilder.Contracts/PackageBuilder.Contracts.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Domain')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Application'
        Path = 'src/PackageBuilder.Application/PackageBuilder.Application.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts', 'PackageBuilder.Domain')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Infrastructure'
        Path = 'src/PackageBuilder.Infrastructure/PackageBuilder.Infrastructure.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Tools.Blender'
        Path = 'src/PackageBuilder.Tools.Blender/PackageBuilder.Tools.Blender.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Targets.Portable'
        Path = 'src/PackageBuilder.Targets.Portable/PackageBuilder.Targets.Portable.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Targets.Unity'
        Path = 'src/PackageBuilder.Targets.Unity/PackageBuilder.Targets.Unity.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Targets.Unreal'
        Path = 'src/PackageBuilder.Targets.Unreal/PackageBuilder.Targets.Unreal.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Marketplaces.Fab'
        Path = 'src/PackageBuilder.Marketplaces.Fab/PackageBuilder.Marketplaces.Fab.csproj'
        Framework = 'net10.0'
        Kind = 'ClassLibrary'
        References = @('PackageBuilder.Contracts')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.App.Wpf'
        Path = 'src/PackageBuilder.App.Wpf/PackageBuilder.App.Wpf.csproj'
        Framework = 'net10.0-windows'
        Kind = 'Wpf'
        References = @(
            'PackageBuilder.Application',
            'PackageBuilder.Infrastructure',
            'PackageBuilder.Marketplaces.Fab',
            'PackageBuilder.Targets.Portable',
            'PackageBuilder.Targets.Unity',
            'PackageBuilder.Targets.Unreal',
            'PackageBuilder.Tools.Blender'
        )
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Cli'
        Path = 'src/PackageBuilder.Cli/PackageBuilder.Cli.csproj'
        Framework = 'net10.0'
        Kind = 'Console'
        References = @(
            'PackageBuilder.Application',
            'PackageBuilder.Infrastructure',
            'PackageBuilder.Marketplaces.Fab',
            'PackageBuilder.Targets.Portable',
            'PackageBuilder.Targets.Unity',
            'PackageBuilder.Targets.Unreal',
            'PackageBuilder.Tools.Blender'
        )
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Domain.Tests'
        Path = 'tests/PackageBuilder.Domain.Tests/PackageBuilder.Domain.Tests.csproj'
        Framework = 'net10.0'
        Kind = 'Test'
        References = @('PackageBuilder.Domain')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Application.Tests'
        Path = 'tests/PackageBuilder.Application.Tests/PackageBuilder.Application.Tests.csproj'
        Framework = 'net10.0'
        Kind = 'Test'
        References = @('PackageBuilder.Application')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Infrastructure.Tests'
        Path = 'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj'
        Framework = 'net10.0'
        Kind = 'Test'
        References = @('PackageBuilder.Infrastructure')
    },
    [pscustomobject]@{
        Name = 'PackageBuilder.Contract.Tests'
        Path = 'tests/PackageBuilder.Contract.Tests/PackageBuilder.Contract.Tests.csproj'
        Framework = 'net10.0'
        Kind = 'Test'
        References = @('PackageBuilder.Contracts')
    }
)

$specificationByName = @{}
$specificationByPath = @{}
foreach ($specification in $projectSpecifications) {
    $specificationByName[$specification.Name] = $specification
    $specificationByPath[$specification.Path.ToLowerInvariant()] = $specification
}

$solutionPath = Join-Path $script:RepositoryRoot 'PackageBuilder.sln'
$script:InventoryReady = $true

Invoke-Check 'Required solution and project inventory is exact' {
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        $script:InventoryReady = $false
        throw 'Missing PackageBuilder.sln at the repository root.'
    }

    $expectedPaths = @($projectSpecifications | ForEach-Object { $_.Path } | Sort-Object)
    $actualPaths = @(
        foreach ($directory in @('src', 'tests')) {
            $fullDirectory = Join-Path $script:RepositoryRoot $directory
            if (Test-Path -LiteralPath $fullDirectory -PathType Container) {
                Get-ChildItem -LiteralPath $fullDirectory -Recurse -File -Filter '*.csproj' |
                    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
                    ForEach-Object { Get-RepositoryRelativePath $_.FullName }
            }
        }
    ) | Sort-Object

    $differences = @(Compare-Object -ReferenceObject $expectedPaths -DifferenceObject $actualPaths)
    if ($differences.Count -gt 0) {
        $script:InventoryReady = $false
        $description = @($differences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" })
        throw "Project inventory differs from the required 15 projects: $($description -join '; ')"
    }
}

if (-not $script:InventoryReady) {
    throw 'Architecture validation stopped because the required inventory is unavailable.'
}

$solutionProjects = @()
$solutionText = Get-Content -Raw -LiteralPath $solutionPath -Encoding UTF8
foreach ($match in [regex]::Matches(
    $solutionText,
    '(?m)^Project\("[^"]+"\)\s*=\s*"(?<name>[^"]+)",\s*"(?<path>[^"]+\.csproj)",'
)) {
    $solutionProjects += [pscustomobject]@{
        Name = $match.Groups['name'].Value
        Path = $match.Groups['path'].Value.Replace('\', '/')
    }
}

Invoke-Check 'Every required project is included in the solution exactly once' {
    if ($solutionProjects.Count -ne $projectSpecifications.Count) {
        throw "Expected 15 project entries in PackageBuilder.sln; found $($solutionProjects.Count)."
    }

    foreach ($specification in $projectSpecifications) {
        $matches = @($solutionProjects | Where-Object {
            [System.StringComparer]::OrdinalIgnoreCase.Equals($_.Path, $specification.Path)
        })
        if ($matches.Count -ne 1) {
            throw "$($specification.Path) has $($matches.Count) solution entries; expected exactly one."
        }
        if ($matches[0].Name -cne $specification.Name) {
            throw "$($specification.Path) uses solution name '$($matches[0].Name)'; expected '$($specification.Name)'."
        }
    }

    $unexpected = @($solutionProjects | Where-Object {
        -not $specificationByPath.ContainsKey($_.Path.ToLowerInvariant())
    })
    if ($unexpected.Count -gt 0) {
        throw "Unexpected solution projects: $(($unexpected.Path | Sort-Object) -join ', ')"
    }
}

$projectXmlByName = @{}
foreach ($specification in $projectSpecifications) {
    $fullPath = Join-Path $script:RepositoryRoot $specification.Path
    $projectXmlByName[$specification.Name] = [xml](
        Get-Content -Raw -LiteralPath $fullPath -Encoding UTF8
    )
}

Invoke-Check 'Target frameworks, project types, assembly names, and root namespaces are correct' {
    foreach ($specification in $projectSpecifications) {
        $projectXml = $projectXmlByName[$specification.Name]
        $targetFramework = Get-ProjectProperty $projectXml 'TargetFramework'
        $targetFrameworks = Get-ProjectProperty $projectXml 'TargetFrameworks'
        $outputType = Get-ProjectProperty $projectXml 'OutputType'
        $useWpf = Get-ProjectProperty $projectXml 'UseWPF'
        $isTestProject = Get-ProjectProperty $projectXml 'IsTestProject'
        $assemblyName = Get-ProjectProperty $projectXml 'AssemblyName'
        $rootNamespace = Get-ProjectProperty $projectXml 'RootNamespace'

        if ($targetFramework -cne $specification.Framework -or $null -ne $targetFrameworks) {
            throw "$($specification.Name) must target only $($specification.Framework)."
        }

        $effectiveAssemblyName = if ($null -eq $assemblyName) { $specification.Name } else { $assemblyName }
        $effectiveRootNamespace = if ($null -eq $rootNamespace) { $specification.Name } else { $rootNamespace }
        if ($effectiveAssemblyName -cne $specification.Name) {
            throw "$($specification.Name) has inconsistent assembly name '$effectiveAssemblyName'."
        }
        if ($effectiveRootNamespace -cne $specification.Name) {
            throw "$($specification.Name) has inconsistent root namespace '$effectiveRootNamespace'."
        }

        switch ($specification.Kind) {
            'Wpf' {
                if ($outputType -cne 'WinExe' -or $useWpf -cne 'true' -or $isTestProject -eq 'true') {
                    throw "$($specification.Name) must be a WPF WinExe with UseWPF=true."
                }
            }
            'Console' {
                if ($outputType -cne 'Exe' -or $useWpf -eq 'true' -or $isTestProject -eq 'true') {
                    throw "$($specification.Name) must be a console Exe."
                }
            }
            'Test' {
                if (($null -ne $outputType -and $outputType -cne 'Library') -or
                    $useWpf -eq 'true' -or $isTestProject -cne 'true') {
                    throw "$($specification.Name) must be a non-WPF test library with IsTestProject=true."
                }

                $packageNames = @(Get-PackageReferenceNames $projectXml)
                foreach ($requiredPackage in @('Microsoft.NET.Test.Sdk', 'xunit', 'xunit.runner.visualstudio')) {
                    if ($packageNames -notcontains $requiredPackage) {
                        throw "$($specification.Name) is missing required test package $requiredPackage."
                    }
                }
            }
            'ClassLibrary' {
                if (($null -ne $outputType -and $outputType -cne 'Library') -or
                    $useWpf -eq 'true' -or $isTestProject -eq 'true') {
                    throw "$($specification.Name) must be a non-WPF class library."
                }
            }
            default {
                throw "Unknown project kind '$($specification.Kind)'."
            }
        }
    }
}

$actualReferenceGraph = @{}
$adapterNames = @(
    'PackageBuilder.Tools.Blender',
    'PackageBuilder.Targets.Portable',
    'PackageBuilder.Targets.Unity',
    'PackageBuilder.Targets.Unreal',
    'PackageBuilder.Marketplaces.Fab'
)

Invoke-Check 'Project references are present, contained, known, and inward-only' {
    foreach ($specification in $projectSpecifications) {
        $projectXml = $projectXmlByName[$specification.Name]
        $projectDirectory = Split-Path (Join-Path $script:RepositoryRoot $specification.Path) -Parent
        $actualNames = @()

        foreach ($projectReference in @(Get-ProjectReferences $projectXml)) {
            $include = [string]$projectReference.Include
            if ([string]::IsNullOrWhiteSpace($include)) {
                throw "$($specification.Name) contains an empty ProjectReference."
            }

            $resolvedReference = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $include))
            if (-not (Test-ContainedPath $resolvedReference)) {
                throw "$($specification.Name) references a project outside the repository: $include"
            }
            if (-not (Test-Path -LiteralPath $resolvedReference -PathType Leaf)) {
                throw "$($specification.Name) references a missing project: $include"
            }

            $relativeReference = Get-RepositoryRelativePath $resolvedReference
            if (-not $specificationByPath.ContainsKey($relativeReference.ToLowerInvariant())) {
                throw "$($specification.Name) references a project outside the approved inventory: $relativeReference"
            }

            $targetName = $specificationByPath[$relativeReference.ToLowerInvariant()].Name
            if ($actualNames -contains $targetName) {
                throw "$($specification.Name) references $targetName more than once."
            }
            $actualNames += $targetName
        }

        $actualReferenceGraph[$specification.Name] = @($actualNames)
        $missing = @($specification.References | Where-Object { $actualNames -notcontains $_ })
        $unexpected = @($actualNames | Where-Object { $specification.References -notcontains $_ })
        if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
            throw (
                "$($specification.Name) has an invalid direct-reference set. " +
                "Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
            )
        }
    }
}

Invoke-Check 'Adapters do not reference other adapters' {
    foreach ($adapterName in $adapterNames) {
        foreach ($referenceName in @($actualReferenceGraph[$adapterName])) {
            if ($adapterNames -contains $referenceName) {
                throw "$adapterName references adapter $referenceName."
            }
        }
    }
}

Invoke-Check 'Project-reference graph is acyclic' {
    $remainingDependencies = @{}
    $dependents = @{}
    foreach ($specification in $projectSpecifications) {
        $remainingDependencies[$specification.Name] = @($actualReferenceGraph[$specification.Name]).Count
        $dependents[$specification.Name] = @()
    }
    foreach ($specification in $projectSpecifications) {
        foreach ($dependency in @($actualReferenceGraph[$specification.Name])) {
            $dependents[$dependency] = @($dependents[$dependency]) + $specification.Name
        }
    }

    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    foreach ($specification in $projectSpecifications) {
        if ($remainingDependencies[$specification.Name] -eq 0) {
            $queue.Enqueue($specification.Name)
        }
    }

    $visited = 0
    while ($queue.Count -gt 0) {
        $name = $queue.Dequeue()
        $visited++
        foreach ($dependent in @($dependents[$name])) {
            $remainingDependencies[$dependent]--
            if ($remainingDependencies[$dependent] -eq 0) {
                $queue.Enqueue($dependent)
            }
        }
    }

    if ($visited -ne $projectSpecifications.Count) {
        $cycleMembers = @(
            $remainingDependencies.Keys |
                Where-Object { $remainingDependencies[$_] -gt 0 } |
                Sort-Object
        )
        throw "Project-reference cycle detected among: $($cycleMembers -join ', ')"
    }
}

Invoke-Check 'Generated placeholder classes and tests are absent' {
    $placeholderFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $script:RepositoryRoot 'src') -Recurse -File |
            Where-Object { $_.Name -eq 'Class1.cs' }
        Get-ChildItem -LiteralPath (Join-Path $script:RepositoryRoot 'tests') -Recurse -File |
            Where-Object { $_.Name -match '^UnitTest\d*\.cs$' }
    )
    if ($placeholderFiles.Count -gt 0) {
        $relativePaths = @($placeholderFiles | ForEach-Object {
            Get-RepositoryRelativePath $_.FullName
        })
        throw "Generated placeholder files remain: $($relativePaths -join ', ')"
    }
}

if ($script:FailureCount -gt 0) {
    throw "Solution architecture validation failed: $($script:PassCount) passed, $($script:FailureCount) failed."
}

Write-Host (
    "Solution architecture validation passed: $($projectSpecifications.Count) projects, " +
    "$($script:PassCount) checks, 0 failures."
) -ForegroundColor Green
