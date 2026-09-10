[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'))

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'UnityTestArtifacts.Common.ps1')
$repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$run = Join-Path $repository ('artifacts\u\' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$passed = 0

# Synthetic payloads exercise deletion without launching engines or generating large packages.
function Assert-TestArtifactCondition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-CleanupRejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert-TestArtifactCondition $rejected 'Unsafe cleanup was not rejected.'
}

try {
    New-Item -ItemType Directory -Path (Join-Path $run 'p/Library'),
        (Join-Path $run 'equipment-import'), (Join-Path $run 'prepared'), (Join-Path $run 'keep') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $run 'p/Library/cache.bin') -Value 'disposable'
    Set-Content -LiteralPath (Join-Path $run 'items.unitypackage') -Value 'disposable'
    Set-Content -LiteralPath (Join-Path $run 'equipment-import/reimport-result.json') -Value '{"passed":true}'
    Set-Content -LiteralPath (Join-Path $run 'prepared/silverwing-prepare-report.json') -Value '{"passed":true}'
    Set-Content -LiteralPath (Join-Path $run 'integration-result.json') -Value '{"schemaVersion":1,"preparationReport":"historical/prepared/silverwing-prepare-report.json"}'
    Set-Content -LiteralPath (Join-Path $run 'validation.log') -Value 'evidence'
    Set-Content -LiteralPath (Join-Path $run 'keep/user.txt') -Value 'unrelated'

    foreach ($invalid in @($repository, (Join-Path $repository 'tests/fixtures'),
            (Join-Path $repository 'artifacts/u'), (Join-Path $run '../unowned'),
            (Join-Path $repository 'artifacts/u-other/12345678'))) {
        Assert-CleanupRejected { Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $invalid }
    }
    $passed++

    Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run -KeepArtifacts
    Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run 'items.unitypackage')) 'Retention lost its package.'
    $passed++

    # A junction inside a disposable project must protect the entire deletion set and its target.
    $junction = Join-Path $run 'p/escape'
    New-Item -ItemType Junction -Path $junction -Target (Join-Path $run 'keep') | Out-Null
    try {
        Assert-CleanupRejected { Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run }
        Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run 'items.unitypackage')) 'Cleanup deleted before checking all paths.'
        Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run 'keep/user.txt')) 'Junction target was modified.'
    }
    finally { Remove-Item -LiteralPath $junction -Force }
    $passed++

    # Model the test-launched Hub lock found during the historical run cleanup.
    $script:taskProcessFilter = ''
    function Get-CimInstance([string]$Filter) {
        $script:taskProcessFilter = $Filter
        return [pscustomobject]@{ ProcessId = 65535; CommandLine = "Unity Hub.exe -projectPath $run" }
    }
    try {
        Assert-CleanupRejected { Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run }
        Assert-TestArtifactCondition ($script:taskProcessFilter.Contains('Unity Hub.exe')) 'Process guard omitted Unity Hub.'
        Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run 'items.unitypackage')) 'Active engine cleanup deleted its package.'
    }
    finally { Remove-Item Function:/Get-CimInstance }
    $passed++

    $caught = ''
    try {
        try { throw 'simulated engine failure' }
        finally { Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run }
    }
    catch { $caught = $_.Exception.Message }
    Assert-TestArtifactCondition ($caught -eq 'simulated engine failure') 'Cleanup hid the original engine error.'
    foreach ($payload in @('p', 'items.unitypackage', 'equipment-import', 'prepared')) {
        Assert-TestArtifactCondition (-not (Test-Path -LiteralPath (Join-Path $run $payload))) "Payload remains: $payload"
    }
    $passed++

    foreach ($evidence in @('validation.log', 'equipment-portable-reimport.json', 'silverwing-prepare-report.json', 'keep/user.txt')) {
        Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run $evidence)) "Lost evidence or unrelated data: $evidence"
    }
    $pointer = Get-Content -LiteralPath (Join-Path $run 'integration-result.json') -Raw | ConvertFrom-Json
    Assert-TestArtifactCondition (-not $pointer.artifactsRetained) 'Pointer incorrectly promises retained artifacts.'
    Assert-TestArtifactCondition (Test-Path -LiteralPath $pointer.preparationReport) 'Historical preparation report pointer was not updated.'
    $receiptPath = Join-Path $run 'cleanup-result.json'
    $receipt = Get-Content -LiteralPath $receiptPath -Raw
    Assert-TestArtifactCondition (($receipt | ConvertFrom-Json).removedBytes -gt 0) 'Cleanup did not account for removed bytes.'
    $passed++

    Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run
    Assert-TestArtifactCondition ((Get-Content -LiteralPath $receiptPath -Raw) -eq $receipt) 'Repeated cleanup replaced the original receipt.'
    $passed++

    # Inspect real entrypoint finally blocks so early failures cannot bypass the shared cleanup.
    foreach ($name in @('Product', 'MultiClip', 'TopologyMatrix', 'Silverwing')) {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot "Invoke-Unity${name}Integration.ps1"), [ref]$tokens, [ref]$errors)
        Assert-TestArtifactCondition ($errors.Count -eq 0) "Harness parse failure: $name"
        $guards = @($ast.FindAll({ param($node)
                    $node -is [Management.Automation.Language.TryStatementAst] -and
                    $null -ne $node.Finally -and $node.Finally.Extent.Text.Contains('Remove-UnityTestArtifacts')
                }, $true))
        Assert-TestArtifactCondition ($guards.Count -eq 1) "Harness has no single cleanup boundary: $name"
        Assert-TestArtifactCondition ($guards[0].Body.Extent.Text.Contains('New-Item -ItemType Directory')) "Run creation escapes cleanup: $name"
        $cleanupText = $guards[0].Finally.Extent.Text
        $cleanupAction = [scriptblock]::Create($cleanupText.Substring(1, $cleanupText.Length - 2))
        $repositoryRootPath = $repository
        $runRoot = $run
        $KeepArtifacts = $true
        $integrationSucceeded = $true
        New-Item -ItemType Directory -Path (Join-Path $run 'p') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $run 'p/partial.zip') -Value 'temporary'
        & $cleanupAction
        Assert-TestArtifactCondition (Test-Path -LiteralPath (Join-Path $run 'p/partial.zip')) "Successful explicit retention failed: $name"
        $integrationSucceeded = $false
        & $cleanupAction
        Assert-TestArtifactCondition (-not (Test-Path -LiteralPath (Join-Path $run 'p'))) "Failed run retained artifacts: $name"
    }
    $passed++
    Write-Host "Unity test artifact cleanup: $passed checks passed."
}
finally {
    Remove-UnityTestArtifacts -RepositoryRoot $repository -RunRoot $run
    # Only this test's tiny, explicitly created evidence directory is removed after verification.
    $expectedParent = Join-Path $repository 'artifacts/u'
    $resolvedRun = (Resolve-Path -LiteralPath $run).ProviderPath
    if ((Split-Path $resolvedRun -Parent) -ne $expectedParent) { throw 'Synthetic cleanup root escaped.' }
    Remove-Item -LiteralPath $resolvedRun -Recurse -Force
}
