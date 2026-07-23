[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptPathProperty = $MyInvocation.MyCommand.PSObject.Properties['Path']
    $invokedScriptPath = if ($null -eq $scriptPathProperty) {
        $null
    }
    else {
        [string]$scriptPathProperty.Value
    }
    if ([string]::IsNullOrWhiteSpace($invokedScriptPath)) {
        throw 'RepositoryRoot was not supplied and the executing script path is unavailable. Invoke this file with powershell.exe -File or pass -RepositoryRoot explicitly.'
    }

    try {
        $resolvedScriptPath = [System.IO.Path]::GetFullPath($invokedScriptPath)
    }
    catch {
        throw "RepositoryRoot was not supplied and the executing script path could not be resolved: $($_.Exception.Message)"
    }

    if (-not (Test-Path -LiteralPath $resolvedScriptPath -PathType Leaf)) {
        throw "RepositoryRoot was not supplied and the executing script path is unavailable: $resolvedScriptPath"
    }

    $scriptDirectory = [System.IO.Path]::GetDirectoryName($resolvedScriptPath)
    if ([string]::IsNullOrWhiteSpace($scriptDirectory)) {
        throw "RepositoryRoot was not supplied and the executing script directory could not be resolved from: $resolvedScriptPath"
    }

    $RepositoryRoot = Join-Path $scriptDirectory '..'
}

$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$script:GitIgnorePath = Join-Path $script:RepositoryRoot '.gitignore'

function Invoke-Git {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [switch]$AllowCheckIgnoreMiss
    )

    $output = @(& git -C $script:RepositoryRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $allowedExitCodes = if ($AllowCheckIgnoreMiss) { @(0, 1) } else { @(0) }
    if ($exitCode -notin $allowedExitCodes) {
        throw "git $($Arguments -join ' ') failed with exit code ${exitCode}:`n$($output -join [Environment]::NewLine)"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = @($output | ForEach-Object { [string]$_ })
    }
}

function Assert-RepositoryRelativePath {
    param([Parameter(Mandatory)][string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        throw 'A policy test path is empty.'
    }
    if ($RelativePath.Contains('\')) {
        throw "Policy test path '$RelativePath' must use repository-relative forward slashes."
    }
    if ([System.IO.Path]::IsPathRooted($RelativePath) -or
        $RelativePath -match '^[A-Za-z]:[/\\]' -or
        $RelativePath -match '^(//|\\\\)') {
        throw "Policy test path '$RelativePath' is absolute."
    }

    $segments = @($RelativePath.Split('/'))
    if ($segments -contains '..') {
        throw "Policy test path '$RelativePath' contains parent-directory traversal."
    }
    if ($segments -contains '.') {
        throw "Policy test path '$RelativePath' contains a redundant current-directory segment."
    }

    $platformRelativePath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $script:RepositoryRoot $platformRelativePath))
    $rootPrefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Policy test path '$RelativePath' resolves outside the repository."
    }
}

function Get-IgnoreMatch {
    param([Parameter(Mandatory)][string]$RelativePath)

    Assert-RepositoryRelativePath $RelativePath
    $result = Invoke-Git @('check-ignore', '-v', '--no-index', '--', $RelativePath) -AllowCheckIgnoreMiss
    $matches = @()

    foreach ($line in $result.Output) {
        $match = [regex]::Match($line, '^(?<source>.*):(?<line>\d+):(?<pattern>[^\t]*)\t(?<path>.*)$')
        if (-not $match.Success) {
            throw "Could not parse git check-ignore output for '$RelativePath': $line"
        }

        $matches += [pscustomobject]@{
            Source = $match.Groups['source'].Value
            Line = [int]$match.Groups['line'].Value
            Pattern = $match.Groups['pattern'].Value
            Path = $match.Groups['path'].Value
        }
    }

    $matchingRule = if ($matches.Count -gt 0) { $matches[-1] } else { $null }
    $isIgnored = $null -ne $matchingRule -and -not $matchingRule.Pattern.StartsWith('!')

    return [pscustomobject]@{
        IsIgnored = $isIgnored
        Match = $matchingRule
        ExitCode = $result.ExitCode
    }
}

function Format-IgnoreMatch {
    param($Match)

    if ($null -eq $Match) {
        return '<no matching rule>'
    }

    return "'$($Match.Pattern)' at $($Match.Source):$($Match.Line)"
}

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

$gitRootResult = Invoke-Git @('rev-parse', '--show-toplevel')
$gitRoot = [System.IO.Path]::GetFullPath($gitRootResult.Output[0]).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $script:RepositoryRoot)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}
if (-not (Test-Path -LiteralPath $script:GitIgnorePath -PathType Leaf)) {
    throw "Missing repository ignore policy: $script:GitIgnorePath"
}

$ignoreLines = @(Get-Content -LiteralPath $script:GitIgnorePath -Encoding UTF8)
$patterns = @(
    $ignoreLines |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith('#') }
)

$policyFailures = [System.Collections.Generic.List[string]]::new()
$requiredRootRules = @('/tools/', '/downloads/', '/logs/', '/runtime-data/', '/artifacts/')
foreach ($requiredRule in $requiredRootRules) {
    if ($requiredRule -notin $patterns) {
        $policyFailures.Add("Required repository-root rule is missing: '$requiredRule'.")
    }
}

$duplicatePatterns = @($patterns | Group-Object | Where-Object Count -gt 1)
foreach ($duplicate in $duplicatePatterns) {
    $policyFailures.Add("Duplicate ignore rule '$($duplicate.Name)' appears $($duplicate.Count) times.")
}

for ($index = 0; $index -lt $ignoreLines.Count; $index++) {
    $pattern = $ignoreLines[$index]
    if ([string]::IsNullOrWhiteSpace($pattern) -or $pattern.StartsWith('#')) {
        continue
    }

    $rule = if ($pattern.StartsWith('!')) { $pattern.Substring(1) } else { $pattern }
    if ($rule -match '^[A-Za-z]:[/\\]' -or $rule -match '^(//|\\\\)') {
        $policyFailures.Add("Rule at .gitignore:$($index + 1) is an absolute filesystem path: '$pattern'.")
    }
    if ($rule.Contains('\')) {
        $policyFailures.Add("Rule at .gitignore:$($index + 1) uses a backslash instead of a repository-relative slash: '$pattern'.")
    }
    if (@($rule.TrimStart('/').Split('/')) -contains '..') {
        $policyFailures.Add("Rule at .gitignore:$($index + 1) contains parent-directory traversal: '$pattern'.")
    }
    if ($rule -in @('*', '**', '/*', '/**', '**/*')) {
        $policyFailures.Add("Rule at .gitignore:$($index + 1) is unnecessarily broad: '$pattern'.")
    }

    $normalizedRule = $rule.TrimEnd('/')
    if ($normalizedRule -match '^(?:/|\*\*/)?\.vscode(?:/\*\*)?$') {
        $policyFailures.Add("Rule at .gitignore:$($index + 1) ignores the complete .vscode directory: '$pattern'.")
    }
}

$policyCases = @(
    # Required repository-local roots.
    [pscustomobject]@{ Category = 'Repository root'; Path = 'tools/dotnet/10.0.302/dotnet.exe'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Repository root'; Path = 'downloads/dotnet/10.0.302/sdk.zip'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Repository root'; Path = 'logs/setup/PB-0004/policy.log'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Repository root'; Path = 'runtime-data/jobs/synthetic/request.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Repository root'; Path = 'artifacts/Builds/Example/report.json'; Ignored = $true; Rule = $null },

    # .NET output and local caches.
    [pscustomobject]@{ Category = '.NET'; Path = 'src/PackageBuilder.Domain/bin/Debug/net10.0/PackageBuilder.Domain.dll'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = '.NET'; Path = 'src/PackageBuilder.Domain/obj/project.assets.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = '.NET'; Path = 'tests/PackageBuilder.Domain.Tests/TestResults/results.trx'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = '.NET'; Path = 'benchmarks/BenchmarkDotNet.Artifacts/results/report.html'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = '.NET'; Path = 'build/PackageBuilder.binlog'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = '.NET'; Path = 'tests/coverage.cobertura.xml'; Ignored = $true; Rule = $null },

    # IDE per-user and machine-local state.
    [pscustomobject]@{ Category = 'Visual Studio'; Path = '.vs/PackageBuilder/v17/.suo'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Visual Studio'; Path = 'src/PackageBuilder.App.Wpf/PackageBuilder.App.Wpf.csproj.user'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Visual Studio'; Path = 'PackageBuilder.sln.DotSettings.user'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'JetBrains'; Path = '.idea/workspace.xml'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'JetBrains'; Path = '.idea/httpRequests/http-client.cookies'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'JetBrains'; Path = '_ReSharper.Caches/cache.dat'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code local'; Path = '.vscode/settings.local.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code local'; Path = '.vscode/.history/settings_1.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code local'; Path = '.vscode/ipch/cache.ipch'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code local'; Path = '.vscode/compileCommands_Default.json'; Ignored = $true; Rule = $null },

    # PowerShell and Python caches.
    [pscustomobject]@{ Category = 'PowerShell'; Path = 'scripts/PesterResults.xml'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'PowerShell'; Path = 'runtime/ModuleAnalysisCache'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Python'; Path = 'workers/blender/__pycache__/entrypoint.cpython-314.pyc'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Python'; Path = 'tests/.pytest_cache/v/cache/nodeids'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Python'; Path = 'workers/blender/.ruff_cache/content'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Python'; Path = 'workers/blender/.venv/Scripts/python.exe'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Python'; Path = 'workers/blender/package_builder.egg-info/PKG-INFO'; Ignored = $true; Rule = $null },

    # Blender generated state.
    [pscustomobject]@{ Category = 'Blender'; Path = 'tests/fixtures/static/model.blend1'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Blender'; Path = 'tests/fixtures/static/scene_autosave.blend'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Blender'; Path = 'tests/fixtures/static/blendcache_scene/cache_0001.bphys'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Blender'; Path = 'workers/blender/blender.crash.txt'; Ignored = $true; Rule = $null },

    # Unity generated state.
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Library/ArtifactDB'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Temp/UnityLockfile'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Obj/Debug/generated.cs'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Logs/Editor.log'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/UserSettings/EditorUserSettings.asset'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Build/Windows/Example.exe'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Builds/Windows/Example.exe'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/.utmp/partial-file'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/.gradle/caches/cache.bin'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/MemoryCaptures/capture.snap'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Recordings/capture.mp4'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Crashes/crash.dmp'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Example.csproj'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unity'; Path = 'engine-templates/unity/Example/Example.sln'; Ignored = $true; Rule = $null },

    # Unreal generated state.
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/Binaries/Win64/ExampleEditor.dll'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/DerivedDataCache/Compressed.ddp'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/Intermediate/Build/Win64/action.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/Saved/Logs/Example.log'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/.vs/Example/v17/.suo'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal'; Path = 'engine-templates/unreal/Example/Example.sln'; Ignored = $true; Rule = $null },

    # Operating-system and temporary state.
    [pscustomobject]@{ Category = 'Operating system'; Path = 'docs/.DS_Store'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Operating system'; Path = 'tests/Thumbs.db'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Temporary'; Path = 'docs/notes.tmp'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Temporary'; Path = 'src/PackageBuilder.Domain/model.cs.swp'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Temporary'; Path = 'docs/~$draft.docx'; Ignored = $true; Rule = $null },

    # Credentials, private keys, signing material, and expected negations.
    [pscustomobject]@{ Category = 'Secrets'; Path = '.env'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Secrets'; Path = 'config/.env.local'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Secrets'; Path = 'config/.env.production.local'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Secrets'; Path = 'config/appsettings.local.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Secrets'; Path = 'config/credentials.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Secrets'; Path = 'config/client_secret_123.json'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Private key'; Path = 'config/signing-private.pem'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Private key'; Path = 'config/deployment.key'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Signing'; Path = 'config/release.pfx'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Signing'; Path = 'config/release.p12'; Ignored = $true; Rule = $null },
    [pscustomobject]@{ Category = 'Safe example'; Path = '.env.example'; Ignored = $false; Rule = '!.env.example' },
    [pscustomobject]@{ Category = 'Safe example'; Path = 'config/.env.template'; Ignored = $false; Rule = '!.env.template' },
    [pscustomobject]@{ Category = 'Safe example'; Path = 'config/client_secret.example.json'; Ignored = $false; Rule = '!**/client_secret.example.json' },

    # Shared editor configuration and legitimate source, fixture, engine, package, and documentation paths.
    [pscustomobject]@{ Category = 'VS Code shared'; Path = '.vscode/settings.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code shared'; Path = '.vscode/tasks.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code shared'; Path = '.vscode/launch.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'VS Code shared'; Path = '.vscode/extensions.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'JetBrains shared'; Path = '.idea/codeStyles/Project.xml'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = '3D source'; Path = 'tests/fixtures/static/model.fbx'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = '3D source'; Path = 'tests/fixtures/static/model.glb'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = '3D source'; Path = 'tests/fixtures/static/model.gltf'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = '3D source'; Path = 'tests/fixtures/static/model.obj'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Blender source'; Path = 'tests/fixtures/static/model.blend'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Texture source'; Path = 'tests/fixtures/static/albedo.png'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Texture source'; Path = 'tests/fixtures/static/preview.jpg'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Texture source'; Path = 'tests/fixtures/static/preview.jpeg'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Texture source'; Path = 'tests/fixtures/static/normal.tga'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Texture source'; Path = 'tests/fixtures/static/environment.exr'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/Assets/Models/model.fbx'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/Assets/Scenes/Overview.unity'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/Assets/Prefabs/Product.prefab'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/Assets/Materials/Product.mat'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/Packages/manifest.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unity source'; Path = 'engine-templates/unity/Example/ProjectSettings/ProjectVersion.txt'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Package input'; Path = 'tests/fixtures/packages/example.unitypackage'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal source'; Path = 'engine-templates/unreal/Example/Config/DefaultEngine.ini'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal source'; Path = 'engine-templates/unreal/Example/Content/Meshes/SM_Product.uasset'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal source'; Path = 'engine-templates/unreal/Example/Content/Maps/LV_Overview.umap'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal source'; Path = 'engine-templates/unreal/Example/Source/Example/Example.cpp'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Unreal source'; Path = 'engine-templates/unreal/Example/Example.uproject'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'C# source'; Path = 'src/PackageBuilder.Domain/ProductManifest.cs'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'C# project'; Path = 'src/PackageBuilder.Domain/PackageBuilder.Domain.csproj'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'C# solution'; Path = 'PackageBuilder.sln'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'PowerShell source'; Path = 'scripts/Build-Package.ps1'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Python source'; Path = 'workers/blender/entrypoint.py'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'JSON source'; Path = 'schemas/product-manifest.schema.json'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'YAML source'; Path = '.github/workflows/build.yml'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'YAML source'; Path = 'config/defaults.yaml'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Markdown source'; Path = 'docs/README.md'; Ignored = $false; Rule = $null },
    [pscustomobject]@{ Category = 'Documentation'; Path = 'docs/operator-guide.rst'; Ignored = $false; Rule = $null }
)

foreach ($case in $policyCases) {
    try {
        $result = Get-IgnoreMatch $case.Path
        $expectedState = if ($case.Ignored) { 'ignored' } else { 'trackable' }
        $actualState = if ($result.IsIgnored) { 'ignored' } else { 'trackable' }
        $matchDescription = Format-IgnoreMatch $result.Match

        if ($result.IsIgnored -ne $case.Ignored) {
            $policyFailures.Add(
                "[$($case.Category)] '$($case.Path)' expected $expectedState but was $actualState; matching rule: $matchDescription."
            )
            continue
        }

        if ($null -ne $case.Rule -and
            ($null -eq $result.Match -or $result.Match.Pattern -cne $case.Rule)) {
            $policyFailures.Add(
                "[$($case.Category)] '$($case.Path)' expected matching rule '$($case.Rule)' but found $matchDescription."
            )
        }
    }
    catch {
        $policyFailures.Add("[$($case.Category)] '$($case.Path)' could not be validated: $($_.Exception.Message)")
    }
}

$trackedResult = Invoke-Git @('ls-files')
$trackedPaths = @($trackedResult.Output | ForEach-Object { $_.Replace('\', '/') })
$trackedConflicts = @()
foreach ($trackedPath in $trackedPaths) {
    try {
        $result = Get-IgnoreMatch $trackedPath
        if ($result.IsIgnored) {
            $trackedConflicts += "'$trackedPath' matches $(Format-IgnoreMatch $result.Match)"
        }
    }
    catch {
        $policyFailures.Add("[Tracked file] '$trackedPath' could not be validated: $($_.Exception.Message)")
    }
}
if ($trackedConflicts.Count -gt 0) {
    $policyFailures.Add("Tracked files unexpectedly match the ignore policy: $($trackedConflicts -join '; ').")
}

if ($policyFailures.Count -gt 0) {
    foreach ($failure in $policyFailures) {
        Write-Host "[FAIL] $failure" -ForegroundColor Red
    }
    throw "Git ignore policy validation failed with $($policyFailures.Count) finding(s)."
}

$ignoredCount = @($policyCases | Where-Object Ignored).Count
$trackableCount = $policyCases.Count - $ignoredCount
Write-Host (
    "Git ignore policy validation: $($policyCases.Count) synthetic paths passed " +
    "($ignoredCount ignored, $trackableCount trackable); $($trackedPaths.Count) tracked paths checked; no conflicts."
) -ForegroundColor Green
