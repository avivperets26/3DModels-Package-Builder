[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$BlenderExecutable
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ProjectRoot {
    # Derive the repository from this tracked script without depending on the caller's working directory.
    if (-not [string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        return (Resolve-Path -LiteralPath $RepositoryRoot).Path
    }

    $scriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrWhiteSpace($scriptPath)) {
        throw 'The repository root is required when the executing script path is unavailable.'
    }

    return (Resolve-Path -LiteralPath (Join-Path (Split-Path -Parent $scriptPath) '..')).Path
}

function Invoke-ContainedBlender {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string[]]$ScriptArguments,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    # Capture every native invocation so a failed engine process remains diagnosable offline.
    $arguments = @('--background', '--factory-startup', '--python', $ScriptPath, '--') + $ScriptArguments
    $priorErrorPreference = $ErrorActionPreference
    try {
        # Windows PowerShell surfaces native stderr as error records; Blender warnings are not failures.
        $ErrorActionPreference = 'Continue'
        $output = @(& $script:ResolvedBlender @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $priorErrorPreference
    }
    $output | Set-Content -LiteralPath $LogPath -Encoding UTF8
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0) {
        throw "Contained Blender failed with exit code $exitCode. See $LogPath"
    }
}

$resolvedRoot = Resolve-ProjectRoot
$toolsRoot = (Resolve-Path -LiteralPath (Join-Path $resolvedRoot 'tools')).Path
if ([string]::IsNullOrWhiteSpace($BlenderExecutable)) {
    $BlenderExecutable = Join-Path $toolsRoot 'blender\5.0.0\blender.exe'
}
$script:ResolvedBlender = (Resolve-Path -LiteralPath $BlenderExecutable).Path
if (-not $script:ResolvedBlender.StartsWith($toolsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Blender must be contained beneath the repository tools root.'
}

$signature = Get-AuthenticodeSignature -LiteralPath $script:ResolvedBlender
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike 'CN=Blender Foundation,*') {
    throw 'The contained Blender executable does not have the approved valid Blender Foundation signature.'
}

$versionOutput = @(& $script:ResolvedBlender --background --factory-startup --version 2>&1)
if ($LASTEXITCODE -ne 0 -or $versionOutput[0] -notmatch '^Blender 5\.0\.0\b') {
    throw 'The contained executable is not the approved Blender 5.0.0 runtime.'
}

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([Guid]::NewGuid().ToString('N'))
$artifactBase = Join-Path $resolvedRoot 'artifacts\PB-0416-PB-0418-real-blender'
$runRoot = Join-Path $artifactBase $runId
$exports = Join-Path $runRoot 'exports'
$observations = Join-Path $runRoot 'observations'
$regressions = Join-Path $runRoot 'regressions'
$logs = Join-Path $runRoot 'logs'
$runtime = Join-Path $resolvedRoot 'runtime-data\blender\5.0.0'
$temporary = Join-Path $runtime 'temp'
$config = Join-Path $runtime 'config'
$scripts = Join-Path $runtime 'scripts'
$dataFiles = Join-Path $runtime 'datafiles'
New-Item -ItemType Directory -Path $runRoot, $observations, $regressions, $logs -Force | Out-Null
New-Item -ItemType Directory -Path $temporary, $config, $scripts, $dataFiles -Force | Out-Null

# Force all Blender-owned mutable state into ignored project-contained roots.
$env:TEMP = $temporary
$env:TMP = $temporary
$env:BLENDER_USER_CONFIG = $config
$env:BLENDER_USER_SCRIPTS = $scripts
$env:BLENDER_USER_DATAFILES = $dataFiles

$engineScripts = Join-Path $resolvedRoot 'tests\blender\engine'
$expectations = Join-Path $exports 'expectations.json'
Invoke-ContainedBlender `
    -ScriptPath (Join-Path $engineScripts 'pb0416_generate_glbs.py') `
    -ScriptArguments @($resolvedRoot, $exports, $expectations) `
    -LogPath (Join-Path $logs 'PB-0416-generate.log')

$expectationDocument = Get-Content -LiteralPath $expectations -Raw | ConvertFrom-Json
foreach ($artifact in $expectationDocument.artifacts) {
    $source = Join-Path $exports $artifact.sourceFilename
    $observation = Join-Path $observations ($artifact.sourceFilename + '.json')
    Invoke-ContainedBlender `
        -ScriptPath (Join-Path $engineScripts 'pb0417_observe_reimport.py') `
        -ScriptArguments @($source, $expectations, $observation) `
        -LogPath (Join-Path $logs ('PB-0417-' + $artifact.sourceFilename + '.log'))
}

$reimportReport = Join-Path $runRoot 'PB-0417-clean-reimport-report.json'
Invoke-ContainedBlender `
    -ScriptPath (Join-Path $engineScripts 'pb0417_validate_reimports.py') `
    -ScriptArguments @($resolvedRoot, $exports, $observations, $reimportReport) `
    -LogPath (Join-Path $logs 'PB-0417-validate.log')

$fixtureRoot = Join-Path $resolvedRoot 'tests\fixtures\blender\regression'
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'corrupt-fbx.payload') -Destination (Join-Path $regressions 'corrupt-fbx.fbx')
Copy-Item -LiteralPath (Join-Path $fixtureRoot 'corrupt-glb.payload') -Destination (Join-Path $regressions 'corrupt-glb.glb')
$regressionReport = Join-Path $runRoot 'PB-0418-regression-report.json'
Invoke-ContainedBlender `
    -ScriptPath (Join-Path $engineScripts 'pb0418_run_regressions.py') `
    -ScriptArguments @($resolvedRoot, (Join-Path $fixtureRoot 'fixture-cases.json'), $regressions, $regressionReport) `
    -LogPath (Join-Path $logs 'PB-0418-regressions.log')

$reimportResult = Get-Content -LiteralPath $reimportReport -Raw | ConvertFrom-Json
$regressionResult = Get-Content -LiteralPath $regressionReport -Raw | ConvertFrom-Json
if (-not $reimportResult.succeeded -or -not $regressionResult.succeeded) {
    throw 'A retained PB-0417 or PB-0418 report did not pass.'
}

Write-Host "PB-0416 real GLB exports: 3/3 passed"
Write-Host "PB-0417 separate clean reimports: $($reimportResult.artifacts.Count)/3 passed"
Write-Host "PB-0418 real Blender regression fixtures: $($regressionResult.fixtures.Count)/7 passed"
Write-Host "Retained visual and machine-readable evidence: $runRoot"
