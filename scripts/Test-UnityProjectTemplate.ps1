[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptPath = [string]$MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'RepositoryRoot was not supplied and the executing script path is unavailable.'
    }

    $RepositoryRoot = Join-Path (Split-Path $scriptPath -Parent) '..'
}

$script:RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$script:TemplateRoot = Join-Path $script:RepositoryRoot 'engine-templates\unity\6000.3'
$script:PassCount = 0
$script:FailureCount = 0

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

function Get-RelativeTemplatePath {
    param([Parameter(Mandatory)][string]$Path)

    $prefix = $script:TemplateRoot.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Template path escapes the approved root: $resolved"
    }

    return $resolved.Substring($prefix.Length).Replace('\', '/')
}

$expectedAssetFiles = @(
    'Assets/PackageBuilder.meta',
    'Assets/PackageBuilder/Preview.meta',
    'Assets/PackageBuilder/Preview/PackageBuilder.Preview.asmdef',
    'Assets/PackageBuilder/Preview/PackageBuilder.Preview.asmdef.meta',
    'Assets/PackageBuilder/Preview/PackageBuilderPreviewController.cs',
    'Assets/PackageBuilder/Preview/PackageBuilderPreviewController.cs.meta',
    'Assets/Settings.meta',
    'Assets/Settings/DefaultVolumeProfile.asset',
    'Assets/Settings/DefaultVolumeProfile.asset.meta',
    'Assets/Settings/Mobile_Renderer.asset',
    'Assets/Settings/Mobile_Renderer.asset.meta',
    'Assets/Settings/Mobile_RPAsset.asset',
    'Assets/Settings/Mobile_RPAsset.asset.meta',
    'Assets/Settings/PC_Renderer.asset',
    'Assets/Settings/PC_Renderer.asset.meta',
    'Assets/Settings/PC_RPAsset.asset',
    'Assets/Settings/PC_RPAsset.asset.meta',
    'Assets/Settings/UniversalRenderPipelineGlobalSettings.asset',
    'Assets/Settings/UniversalRenderPipelineGlobalSettings.asset.meta'
)
$expectedProjectSettings = @(
    'AudioManager.asset',
    'ClusterInputManager.asset',
    'DynamicsManager.asset',
    'EditorBuildSettings.asset',
    'EditorSettings.asset',
    'GraphicsSettings.asset',
    'InputManager.asset',
    'MemorySettings.asset',
    'MultiplayerManager.asset',
    'NavMeshAreas.asset',
    'PackageManagerSettings.asset',
    'Physics2DSettings.asset',
    'PresetManager.asset',
    'ProjectSettings.asset',
    'ProjectVersion.txt',
    'QualitySettings.asset',
    'ShaderGraphSettings.asset',
    'TagManager.asset',
    'TimeManager.asset',
    'UnityConnectSettings.asset',
    'URPProjectSettings.asset',
    'VersionControlSettings.asset',
    'VFXManager.asset',
    'XRSettings.asset'
) | ForEach-Object { "ProjectSettings/$_" }
$expectedWorkerPackageFiles = @(
    'Packages/com.packagebuilder.worker/package.json',
    'Packages/com.packagebuilder.worker/Editor/PackageBuilder.UnityWorker.Editor.asmdef',
    'Packages/com.packagebuilder.worker/Editor/UnityBatchEntrypoint.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityWorkerExitCode.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityWorkerFileSystem.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityWorkerJson.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityWorkerRequest.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityProductFolderGenerator.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityTextureImporterPolicy.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityMetallicSmoothnessPacker.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityUrpLitMaterialCompiler.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityStaticModelImporterPolicy.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityMeshAssetExtractor.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityPrefabGenerator.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityProductEditorIntegrationTests.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityOverviewScenePipeline.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityOverviewPlayModeSmokeTest.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityPackageExporter.cs',
    'Packages/com.packagebuilder.worker/Editor/UnityPackageValidator.cs'
)
$expectedFiles = @(
    $expectedAssetFiles +
    'Packages/manifest.json' +
    $expectedWorkerPackageFiles +
    $expectedProjectSettings
) | Sort-Object

Invoke-Check 'Template has exactly the approved Unity project roots' {
    if (-not (Test-Path -LiteralPath $script:TemplateRoot -PathType Container)) {
        throw "Unity template root does not exist: $script:TemplateRoot"
    }

    $actualRoots = @(Get-ChildItem -LiteralPath $script:TemplateRoot -Directory | ForEach-Object Name | Sort-Object)
    $expectedRoots = @('Assets', 'Packages', 'ProjectSettings')
    if (($actualRoots -join '|') -cne ($expectedRoots -join '|')) {
        throw "Expected only Assets, Packages, and ProjectSettings; found: $($actualRoots -join ', ')."
    }
}

Invoke-Check 'Template file inventory is minimal and deterministic' {
    $actualFiles = @(Get-ChildItem -LiteralPath $script:TemplateRoot -Recurse -File |
        ForEach-Object { Get-RelativeTemplatePath $_.FullName } |
        Sort-Object)
    $missing = @($expectedFiles | Where-Object { $_ -notin $actualFiles })
    $unexpected = @($actualFiles | Where-Object { $_ -notin $expectedFiles })
    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "Inventory mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }
}

Invoke-Check 'Unity Editor and URP versions are pinned to the approved pair' {
    $versionText = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'ProjectSettings\ProjectVersion.txt') -Raw -Encoding UTF8
    $expectedVersion = "m_EditorVersion: 6000.3.10f1`nm_EditorVersionWithRevision: 6000.3.10f1 (e35f0c77bd8e)`n"
    if ($versionText.Replace("`r`n", "`n") -cne $expectedVersion) {
        throw 'ProjectVersion.txt does not contain the exact approved Unity editor identity.'
    }

    $manifestPath = Join-Path $script:TemplateRoot 'Packages\manifest.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $dependencies = @($manifest.dependencies.PSObject.Properties)
    if ($dependencies.Count -ne 1 -or
        $dependencies[0].Name -cne 'com.unity.render-pipelines.universal' -or
        [string]$dependencies[0].Value -cne '17.3.0') {
        throw 'Packages/manifest.json must pin only URP 17.3.0 as a direct dependency.'
    }

    $urpProjectSettings = Get-Content -LiteralPath (Join-Path $script:TemplateRoot `
            'ProjectSettings\URPProjectSettings.asset') -Raw -Encoding UTF8
    $materialVersionMatches = @([regex]::Matches($urpProjectSettings,
            '(?m)^  m_LastMaterialVersion: 10$'))
    if ($materialVersionMatches.Count -ne 1) {
        throw 'URPProjectSettings.asset must record material upgrader version 10 for pinned URP 17.3.0.'
    }
}

Invoke-Check 'URP settings preserve complete local asset references' {
    $graphics = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'ProjectSettings\GraphicsSettings.asset') -Raw -Encoding UTF8
    $quality = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'ProjectSettings\QualitySettings.asset') -Raw -Encoding UTF8
    $mobile = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'Assets\Settings\Mobile_RPAsset.asset') -Raw -Encoding UTF8
    $pc = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'Assets\Settings\PC_RPAsset.asset') -Raw -Encoding UTF8
    $global = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'Assets\Settings\UniversalRenderPipelineGlobalSettings.asset') -Raw -Encoding UTF8
    $requiredPairs = @(
        @($graphics, 'guid: 4b83569d67af61e458304325a23e5dfd'),
        @($graphics, 'guid: 18dc0cd2c080841dea60987a38ce93fa'),
        @($quality, 'guid: 5e6cbd92db86f4b18aec3ed561671858'),
        @($quality, 'guid: 4b83569d67af61e458304325a23e5dfd'),
        @($mobile, 'guid: 65bc7dbf4170f435aa868c779acfb082'),
        @($pc, 'guid: f288ae1f4751b564a96ac7587541f7a2'),
        @($mobile, 'guid: 10fc4df2da32a41aaa32d77bc913491c'),
        @($pc, 'guid: 10fc4df2da32a41aaa32d77bc913491c'),
        @($global, 'guid: ab09877e2e707104187f6f83e2f62510')
    )
    foreach ($pair in $requiredPairs) {
        if (-not $pair[0].Contains($pair[1])) {
            throw "Required URP reference is absent: $($pair[1])"
        }
    }
}

Invoke-Check 'URP asset metadata matches every referenced project GUID' {
    $expectedGuids = @{
        'Assets\Settings\DefaultVolumeProfile.asset.meta' = 'ab09877e2e707104187f6f83e2f62510'
        'Assets\Settings\Mobile_Renderer.asset.meta' = '65bc7dbf4170f435aa868c779acfb082'
        'Assets\Settings\Mobile_RPAsset.asset.meta' = '5e6cbd92db86f4b18aec3ed561671858'
        'Assets\Settings\PC_Renderer.asset.meta' = 'f288ae1f4751b564a96ac7587541f7a2'
        'Assets\Settings\PC_RPAsset.asset.meta' = '4b83569d67af61e458304325a23e5dfd'
        'Assets\Settings\UniversalRenderPipelineGlobalSettings.asset.meta' = '18dc0cd2c080841dea60987a38ce93fa'
    }
    foreach ($entry in $expectedGuids.GetEnumerator()) {
        $metadata = Get-Content -LiteralPath (Join-Path $script:TemplateRoot $entry.Key) -Raw -Encoding UTF8
        if ($metadata -notmatch "(?m)^guid: $([regex]::Escape($entry.Value))$") {
            throw "Unity GUID mismatch in $($entry.Key)."
        }
    }
}

Invoke-Check 'Template contains no sample product, scene, media, or stale reference' {
    $buildSettings = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'ProjectSettings\EditorBuildSettings.asset') -Raw -Encoding UTF8
    $playerSettings = Get-Content -LiteralPath (Join-Path $script:TemplateRoot 'ProjectSettings\ProjectSettings.asset') -Raw -Encoding UTF8
    if ($buildSettings -notmatch '(?m)^  m_Scenes: \[\]$' -or $buildSettings -notmatch '(?m)^  m_configObjects: \{\}$') {
        throw 'EditorBuildSettings must contain no prior scene or input-action reference.'
    }
    if ($playerSettings -match 'SampleScene|com\.unity\.template|Unity Technologies|AvivPeretsFBX') {
        throw 'Player settings retain prior template or publisher identity.'
    }
    $forbiddenExtensions = '(?i)\.(dll|exe|unity|prefab|fbx|glb|gltf|blend|png|jpg|jpeg|tga|psd)$'
    $forbiddenFiles = @(Get-ChildItem -LiteralPath (Join-Path $script:TemplateRoot 'Assets') -Recurse -File |
        Where-Object { $_.Name -match $forbiddenExtensions })
    if ($forbiddenFiles.Count -gt 0) {
        throw "Template contains product, executable, scene, or media files: $($forbiddenFiles.Name -join ', ')."
    }

    $runtimeScripts = @(Get-ChildItem -LiteralPath (Join-Path $script:TemplateRoot 'Assets') `
        -Recurse -File -Filter '*.cs' | ForEach-Object { Get-RelativeTemplatePath $_.FullName })
    if ($runtimeScripts.Count -ne 1 -or
        $runtimeScripts[0] -cne 'Assets/PackageBuilder/Preview/PackageBuilderPreviewController.cs') {
        throw 'Only the generic product-local preview controller source is permitted in template Assets.'
    }
}

Invoke-Check 'Template contains no Unity caches or generated output' {
    $prohibitedDirectories = @('Library', 'Temp', 'Logs', 'UserSettings', 'obj', 'Build', 'Builds', '.gradle', '.utmp')
    $matches = @(Get-ChildItem -LiteralPath $script:TemplateRoot -Recurse -Directory |
        Where-Object { $_.Name -in $prohibitedDirectories })
    if ($matches.Count -gt 0) {
        throw "Generated Unity directories are prohibited: $($matches.FullName -join ', ')."
    }
}

Invoke-Check 'Template text is portable, public-safe UTF-8 with LF endings' {
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    $prohibitedPatterns = @(
        '(?i)\b[A-Z]:[\\/]Users[\\/]',
        '(?i)(?<![A-Za-z0-9_])/(?:home|Users)/[A-Za-z0-9._-]+',
        '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
        '(?i)\b(password|passwd|api[_-]?key|client[_-]?secret)\s*[:=]\s*["''][^"'']{8,}["'']'
    )
    foreach ($file in Get-ChildItem -LiteralPath $script:TemplateRoot -Recurse -File) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        if ($bytes -contains 0) { throw "Binary content is not allowed: $($file.FullName)" }
        try { $text = $strictUtf8.GetString($bytes) } catch { throw "File is not valid UTF-8: $($file.FullName)" }
        if ($text.Contains("`r")) { throw "File is not LF-normalized: $($file.FullName)" }
        foreach ($pattern in $prohibitedPatterns) {
            if ($text -match $pattern) { throw "Public-safety pattern detected in $($file.FullName)." }
        }
    }
}

Write-Host ''
Write-Host "Unity project template validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Unity project template validation failed.'
}
