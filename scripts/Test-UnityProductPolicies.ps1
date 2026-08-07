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

$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$editorRoot = Join-Path $repositoryRootPath `
    'engine-templates\unity\6000.3\Packages\com.packagebuilder.worker\Editor'
$folderSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityProductFolderGenerator.cs') `
    -Raw -Encoding UTF8
$textureSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityTextureImporterPolicy.cs') `
    -Raw -Encoding UTF8
$packingSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityMetallicSmoothnessPacker.cs') `
    -Raw -Encoding UTF8
$materialSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityUrpLitMaterialCompiler.cs') `
    -Raw -Encoding UTF8
$testSource = Get-Content -LiteralPath (Join-Path $editorRoot 'UnityProductEditorIntegrationTests.cs') `
    -Raw -Encoding UTF8
$integrationSource = Get-Content -LiteralPath (Join-Path $repositoryRootPath `
        'scripts\Invoke-UnityProductIntegration.ps1') -Raw -Encoding UTF8
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

Invoke-Check 'Unity product folder policy covers every approved product case' {
    foreach ($value in @('static', 'rigged', 'rigged-animated', 'item-set', 'item-collection')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing product case: $value"
        }
    }
}

Invoke-Check 'Unity base product folder inventory is exact and template-free' {
    foreach ($value in @('Source', 'Meshes', 'Materials', 'Textures', 'Prefabs',
            'Documentation', 'Scenes', 'Scripts')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing base folder: $value"
        }
    }
    if ($folderSource.Contains('"_Template"')) {
        throw 'The production folder plan must not contain _Template.'
    }
}

Invoke-Check 'Animation folders are gated to the animated product case' {
    foreach ($value in @('Animations', 'Controllers', 'rigged-animated')) {
        if (-not $folderSource.Contains('"' + $value + '"')) {
            throw "Missing animated folder policy token: $value"
        }
    }
}

Invoke-Check 'Folder creation uses AssetDatabase and rejects existing product roots' {
    foreach ($value in @('AssetDatabase.CreateFolder', 'AssetDatabase.IsValidFolder',
            'UNITY_PRODUCT_FOLDER_COLLISION', 'AssetDatabase.DeleteAsset', 'AssetDatabase.SaveAssets')) {
        if (-not $folderSource.Contains($value)) {
            throw "Missing safe folder behavior: $value"
        }
    }
}

Invoke-Check 'Unity texture roles have explicit colour-space and type policy' {
    foreach ($value in @('albedo', 'emission', 'normal', 'metallic', 'roughness',
            'ambient-occlusion', 'opacity', 'height', 'TextureImporterType.NormalMap',
            'sRGBTexture')) {
        if (-not $textureSource.Contains($value)) {
            throw "Missing texture policy token: $value"
        }
    }
}

Invoke-Check 'Unity texture alpha handling is explicit and reapplied synchronously' {
    foreach ($value in @('TextureImporterAlphaSource.FromInput', 'TextureImporterAlphaSource.None',
            'alphaIsTransparency', 'SaveAndReimport')) {
        if (-not $textureSource.Contains($value)) {
            throw "Missing alpha/import behavior: $value"
        }
    }
}

Invoke-Check 'Unity metallic-smoothness packing is exact, dimension-safe, and source-restoring' {
    foreach ($value in @('UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH',
            'metallicPixels[index].r', 'byte.MaxValue - roughnessPixels[index].r',
            'TextureImporterCompression.Uncompressed', 'finally', 'Restore(metallicImporter)',
            'Restore(roughnessImporter)', 'TextureImporterAlphaSource.FromInput')) {
        if (-not $packingSource.Contains($value)) {
            throw "Missing metallic-smoothness packing behavior: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit compiler maps canonical textures, factors, surfaces, and culling' {
    foreach ($value in @('Universal Render Pipeline/Lit', '_BaseMap', '_BumpMap',
            '_MetallicGlossMap', '_EmissionMap', '_OcclusionMap', '_AlphaClip', '_Cutoff',
            '_Cull', '_Surface', '_Smoothness', '1f - request.RoughnessFactor')) {
        if (-not $materialSource.Contains($value)) {
            throw "Missing URP/Lit material mapping: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit emission intent is explicit before keyword canonicalization' {
    foreach ($value in @('MaterialGlobalIlluminationFlags.BakedEmissive',
            'MaterialGlobalIlluminationFlags.EmissiveIsBlack', 'maxColorComponent')) {
        if (-not $materialSource.Contains($value)) {
            throw "Missing emission/GI behavior: $value"
        }
    }
}

Invoke-Check 'Unity URP/Lit compiler delegates keyword and render-state canonicalization to URP' {
    foreach ($value in @('BaseShaderGUI.SetMaterialKeywords(candidate, LitGUI.SetMaterialKeywords)',
            'BaseShaderGUI.SetupMaterialBlendMode(candidate)', 'SynchronizeUrpState',
            'Unity.RenderPipelines.Universal.Editor')) {
        $found = $materialSource.Contains($value) -or
            (Get-Content -LiteralPath (Join-Path $editorRoot `
                    'PackageBuilder.UnityWorker.Editor.asmdef') -Raw -Encoding UTF8).Contains($value)
        if (-not $found) {
            throw "Missing URP canonicalization behavior: $value"
        }
    }
}

Invoke-Check 'Editor integration tests cover all cases, roles, collisions, and unsafe inputs' {
    foreach ($value in @('PACKAGEBUILDER_UNITY_PRODUCT_TESTS_PASS',
            'UNITY_PRODUCT_FOLDER_COLLISION', 'orm', '../outside.png',
            'TextureImporterType.NormalMap', 'AssetDatabase.IsValidFolder',
            'Texture2D(4, 4', 'new Color[16]', 'TestMetallicSmoothnessPacking',
            'UNITY_METALLIC_SMOOTHNESS_DIMENSION_MISMATCH', 'Metallic red channel is incorrect',
            'Smoothness alpha must equal one minus roughness', 'TestUrpLitMaterialCompilation',
            'VerifyUrpStateIsStable', '_METALLICSPECGLOSSMAP', '_SURFACE_TYPE_TRANSPARENT')) {
        if (-not $testSource.Contains($value)) {
            throw "Missing Editor integration assertion: $value"
        }
    }
}

Invoke-Check 'Unity integration uses a legacy-safe short clone and validates a clean reopen' {
    foreach ($value in @('artifacts\u', "Substring(0, 8)",
            '$maximumLegacyCompatiblePathLength = 248', 'unity-product-reopen.log',
            'unity-product-reopen-retry.log', 'CurrentThread::IsMainThread()',
            '$knownNativeStartupRace', "'-quit'", 'DirectoryNotFoundException',
            'Unity populated-project reopen validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing Unity reopen/path safeguard: $value"
        }
    }
}

Invoke-Check 'Unity integration rejects a stale URP material upgrader marker' {
    foreach ($value in @('URPProjectSettings.asset', 'MaterialPostprocessor.cs',
            'm_LastMaterialVersion', 'upgraderCount',
            'Unity URP material upgrader marker validation')) {
        if (-not $integrationSource.Contains($value)) {
            throw "Missing URP material upgrader safeguard: $value"
        }
    }
}

Invoke-Check 'Unity product policy sources are deterministic public-safe text' {
    $files = @(
        (Join-Path $editorRoot 'UnityProductFolderGenerator.cs'),
        (Join-Path $editorRoot 'UnityTextureImporterPolicy.cs'),
        (Join-Path $editorRoot 'UnityMetallicSmoothnessPacker.cs'),
        (Join-Path $editorRoot 'UnityUrpLitMaterialCompiler.cs'),
        (Join-Path $editorRoot 'UnityProductEditorIntegrationTests.cs')
    )
    $utf8 = New-Object Text.UTF8Encoding($false, $true)
    foreach ($file in $files) {
        $bytes = [IO.File]::ReadAllBytes($file)
        $text = $utf8.GetString($bytes)
        if ($text.Contains("`r") -or $text -match '(?i)\b[A-Z]:[\\/]Users[\\/]') {
            throw "Unity policy source is not public-safe LF text: $file"
        }
    }
}

Write-Host ''
Write-Host "Unity product policy validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Unity product policy validation failed.'
}
