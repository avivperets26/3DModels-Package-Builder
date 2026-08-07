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
$script:PackageRoot = Join-Path $script:RepositoryRoot 'engine-templates\unity\6000.3\Packages\com.packagebuilder.worker'
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

function Get-PackageRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $prefix = $script:PackageRoot.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Worker package path escapes its root: $resolved"
    }

    return $resolved.Substring($prefix.Length).Replace('\', '/')
}

$expectedFiles = @(
    'package.json',
    'Editor/PackageBuilder.UnityWorker.Editor.asmdef',
    'Editor/UnityBatchEntrypoint.cs',
    'Editor/UnityWorkerExitCode.cs',
    'Editor/UnityWorkerFileSystem.cs',
    'Editor/UnityWorkerJson.cs',
    'Editor/UnityWorkerRequest.cs',
    'Editor/UnityProductFolderGenerator.cs',
    'Editor/UnityTextureImporterPolicy.cs',
    'Editor/UnityMetallicSmoothnessPacker.cs',
    'Editor/UnityUrpLitMaterialCompiler.cs',
    'Editor/UnityProductEditorIntegrationTests.cs'
) | Sort-Object

Invoke-Check 'Embedded Unity worker package inventory is exact' {
    if (-not (Test-Path -LiteralPath $script:PackageRoot -PathType Container)) {
        throw "Worker package root is missing: $script:PackageRoot"
    }

    $actual = @(Get-ChildItem -LiteralPath $script:PackageRoot -Recurse -File |
        ForEach-Object { Get-PackageRelativePath $_.FullName } |
        Sort-Object)
    $differences = @(Compare-Object -ReferenceObject $expectedFiles -DifferenceObject $actual)
    if ($differences.Count -gt 0) {
        throw "Worker package inventory differs: $($differences.InputObject -join ', ')."
    }
}

Invoke-Check 'Package metadata pins the approved Editor-only worker identity' {
    $metadata = Get-Content -LiteralPath (Join-Path $script:PackageRoot 'package.json') -Raw -Encoding UTF8 |
        ConvertFrom-Json
    $dependencyCount = if ($metadata.PSObject.Properties.Name -contains 'dependencies') {
        @($metadata.dependencies.PSObject.Properties).Count
    }
    else {
        0
    }
    if ([string]$metadata.name -cne 'com.packagebuilder.worker' -or
        [string]$metadata.version -cne '1.0.0' -or
        [string]$metadata.unity -cne '6000.3' -or
        $dependencyCount -ne 0) {
        throw 'Worker package identity, version, Unity family, or dependency boundary is invalid.'
    }
}

Invoke-Check 'Assembly definition compiles only inside the Unity Editor with the pinned URP Editor API' {
    $assembly = Get-Content -LiteralPath (
        Join-Path $script:PackageRoot 'Editor\PackageBuilder.UnityWorker.Editor.asmdef'
    ) -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]$assembly.name -cne 'PackageBuilder.UnityWorker.Editor' -or
        @($assembly.includePlatforms).Count -ne 1 -or
        [string]$assembly.includePlatforms[0] -cne 'Editor' -or
        @($assembly.references).Count -ne 1 -or
        [string]$assembly.references[0] -cne 'Unity.RenderPipelines.Universal.Editor' -or
        @($assembly.precompiledReferences).Count -ne 0 -or
        [bool]$assembly.allowUnsafeCode -or
        [bool]$assembly.overrideReferences -or
        -not [bool]$assembly.autoReferenced) {
        throw 'Worker assembly must remain safe, Editor-only, and limited to the pinned URP Editor API.'
    }
}

$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $script:PackageRoot 'Editor') -File -Filter '*.cs')
$sourceText = ($sourceFiles | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
}) -join "`n"

Invoke-Check 'Batch entrypoint exposes the stable request and exit-code protocol' {
    foreach ($required in @(
        'public static class UnityBatchEntrypoint',
        'public static void Run()',
        '-packageBuilderRequest',
        'probe-unity-worker',
        'Success = 0',
        'InvocationFailure = 2',
        'InvalidRequest = 3',
        'UnsupportedOperation = 4',
        'ExecutionFailure = 5',
        'ResultWriteFailure = 6',
        'Cancelled = 7'
    )) {
        if (-not $sourceText.Contains($required)) {
            throw "Required Unity batch protocol token is missing: $required"
        }
    }
}

Invoke-Check 'Request parsing is bounded, strict, versioned, and path-safe' {
    foreach ($required in @(
        'MaximumRequestCharacters = 65_536',
        'UNITY_REQUEST_UNKNOWN_PROPERTY',
        'UNITY_REQUEST_MISSING_PROPERTY',
        'UNITY_REQUEST_SCHEMA_INVALID',
        'IsLogicalReference',
        'properties.Add(property)'
    )) {
        if (-not $sourceText.Contains($required)) {
            throw "Required request-validation behavior is missing: $required"
        }
    }
}

Invoke-Check 'Progress, metric, result, and cancellation records use protocol v1' {
    foreach ($required in @(
        '\"protocolVersion\":1',
        '\"eventKind\":\"progress\"',
        '\"eventKind\":\"metric\"',
        '\"status\":\"success\"',
        '\"status\":\"failure\"',
        '\"status\":\"cancelled\"',
        'PACKAGEBUILDER_CANCELLATION_FILE'
    )) {
        if (-not $sourceText.Contains($required)) {
            throw "Required worker protocol output is missing: $required"
        }
    }
}

Invoke-Check 'Result and probe writes are contained, atomic, and explicitly saved' {
    foreach ($required in @(
        'ResolveProjectReference',
        'WriteAllTextAtomically',
        'stream.Flush(true)',
        'File.Replace',
        'AssetDatabase.ImportAsset',
        'AssetDatabase.SaveAssets'
    )) {
        if (-not $sourceText.Contains($required)) {
            throw "Required safe write/save behavior is missing: $required"
        }
    }
}

Invoke-Check 'Worker package contains no runtime assembly or customer export content' {
    $runtimeDirectories = @(Get-ChildItem -LiteralPath $script:PackageRoot -Recurse -Directory |
        Where-Object { $_.Name -in @('Runtime', 'Plugins', 'Assets') })
    if ($runtimeDirectories.Count -gt 0) {
        throw "Runtime/customer content is prohibited: $($runtimeDirectories.FullName -join ', ')."
    }
    if ($sourceText -match '(?m)^\s*(using\s+System\.Net|DllImport|Process\.Start|HttpClient|WebRequest)') {
        throw 'Worker package introduces networking, native loading, or child-process execution.'
    }
}

Invoke-Check 'Worker sources are public-safe UTF-8 text with LF endings' {
    $strictUtf8 = New-Object Text.UTF8Encoding($false, $true)
    foreach ($file in Get-ChildItem -LiteralPath $script:PackageRoot -Recurse -File) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        if ($bytes -contains 0) { throw "Binary worker content is prohibited: $($file.FullName)" }
        try { $text = $strictUtf8.GetString($bytes) } catch { throw "Worker file is not UTF-8: $($file.FullName)" }
        if ($text.Contains("`r")) { throw "Worker file is not LF-normalized: $($file.FullName)" }
        if ($text -match '(?i)\b[A-Z]:[\\/]Users[\\/]|-----BEGIN .*PRIVATE KEY-----') {
            throw "Worker file contains public-unsafe content: $($file.FullName)"
        }
    }
}

Write-Host ''
Write-Host "Unity worker package validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Unity worker package validation failed.'
}
