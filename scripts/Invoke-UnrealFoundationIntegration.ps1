#requires -Version 7.2
<#
.SYNOPSIS
Verifies the candidate installation, then saves/reopens an asset in a disposable project.
.DESCRIPTION
Requires a user-installed Epic engine. Never installs, accepts licences or approves production
compatibility. Keeps compact receipts and native logs; removes its unique project on all exits.
#>
[CmdletBinding()]
param([ValidateRange(30, 3600)][int]$TimeoutSeconds = 600)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Enter-PackageBuilderEnvironment.ps1')
. (Join-Path $PSScriptRoot 'UnrealCandidate.Common.ps1')
$candidate = Get-UnrealCandidate $repo
$profile = $candidate.Profile
$executable = $candidate.Executable
$signature = $candidate.Signature
$engine = $candidate.Engine

$id = [Guid]::NewGuid().ToString('N')
$runs = Join-Path $repo 'artifacts/ue'
$run = Join-Path $runs $id
$evidence = Join-Path $repo "artifacts/PB-1103/$id"
$template = Join-Path $repo $profile.template
$success = $false
$cleaned = $false
$process = $null
$receipt = [ordered]@{ schemaVersion = 1; engineVersion = $profile.version; realEngineRun = $false;
    smokePassed = $false; approval = 'candidate-only'; cleanupSucceeded = $false; operations = @();
    executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant();
    signatureStatus = $signature.Status.ToString(); installationRoot = $engine }
Assert-UnrealContainedPath $run $runs
Assert-UnrealContainedPath $evidence $repo
Assert-UnrealContainedPath $template $repo
try {
    New-Item -ItemType Directory -Path $run, $evidence -Force | Out-Null
    # Copy only reviewed source files. Never carry cached or generated template state into a run.
    $files = @('PackageBuilder.uproject', 'Config/DefaultEngine.ini', 'Config/DefaultEditor.ini',
        'Content/Pack/.gitkeep', 'Plugins/PackageBuilderWorker/PackageBuilderWorker.uplugin',
        'Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py')
    foreach ($file in $files) {
        $source = Join-Path $template $file
        Assert-UnrealContainedPath $source $template
        $destination = Join-Path $run "project/$file"
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    New-Item -ItemType Directory -Path (Join-Path $run 'input'), (Join-Path $run 'temp') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $run 'input/product.json'), '{}')
    $assetHash = $null
    foreach ($operation in @('probe-unreal-worker', 'verify-unreal-worker', 'unsupported-unreal-operation')) {
        $expectedFailure = $operation -eq 'unsupported-unreal-operation'
        $request = [ordered]@{ protocolVersion = 1; jobId = "Job-$id"; operation = $operation;
            productManifestReference = 'input/product.json'; inputDirectoryReference = 'input';
            outputDirectoryReference = 'output'; resultFileReference = "output/$operation.json";
            engineVersion = $profile.version; target = 'unreal' }
        $requestPath = Join-Path $run 'request.json'
        [IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json))
        $start = [Diagnostics.ProcessStartInfo]::new($executable)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.WorkingDirectory = $run
        foreach ($argument in @((Join-Path $run 'project/PackageBuilder.uproject'), '-run=pythonscript',
            "-script=$(Join-Path $run 'project/Plugins/PackageBuilderWorker/Content/Python/pb_worker_entry.py')",
            '-unattended', '-nosplash', '-nosound', '-NullRHI', '-ddc=(Local)',
            '-ini:EditorSettings:[/Script/UnrealEd.AnalyticsPrivacySettings]:bSendUsageData=False',
            "-UserDir=$(Join-Path $run 'user')", "-abslog=$(Join-Path $evidence "$operation.log")")) {
            $start.ArgumentList.Add($argument)
        }
        $start.Environment['PACKAGEBUILDER_WORKERS_ROOT'] = Join-Path $repo 'workers'
        $start.Environment['PACKAGEBUILDER_UNREAL_REQUEST'] = $requestPath
        $start.Environment['PYTHONDONTWRITEBYTECODE'] = '1'
        $start.Environment['UE-LocalDataCachePath'] = Join-Path $repo 'runtime-data/unreal/5.8.2/ddc'
        $start.Environment['UE-SharedDataCachePath'] = 'None'
        $start.Environment['TEMP'] = Join-Path $run 'temp'
        $start.Environment['TMP'] = Join-Path $run 'temp'
        $process = [Diagnostics.Process]::Start($start)
        $receipt.realEngineRun = $true
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'Unreal smoke test timed out; its process tree was stopped.'
        }
        $exitCode = $process.ExitCode
        if (($exitCode -ne 0) -ne $expectedFailure) { throw "Unexpected Unreal exit during $operation; inspect the retained engine log." }
        $process.Dispose()
        $process = $null
        $resultPath = Join-Path $run $request.resultFileReference
        Assert-UnrealContainedPath $resultPath $run
        if ((Get-Item -LiteralPath $resultPath).Length -gt 1MB) { throw 'Worker result exceeds smoke-test bounds.' }
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($expectedFailure) {
            if ($result.status -cne 'failure' -or $result.jobId -cne $request.jobId -or
                @($result.artifacts).Count -ne 0 -or $result.outputsPromoted -ne $false -or
                @($result.findings).Count -ne 1 -or $result.findings[0].code -cne 'UNREAL_OPERATION_UNSUPPORTED' -or
                $result.findings[0].blocksRelease -ne $true) { throw 'Expected structured failure was not produced.' }
            $asset = Join-Path $run 'project/Content/Pack/PB_WorkerProbe.uasset'
            Assert-UnrealContainedPath $asset $run
            if ((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant() -cne $assetHash) {
                throw 'Rejected operation changed the saved asset.'
            }
            Copy-Item -LiteralPath $resultPath -Destination (Join-Path $evidence "$operation.json")
            $receipt.operations += $operation
            continue
        }
        $nativeLog = Join-Path $evidence "$operation.log"
        if (Select-String -LiteralPath $nativeLog -Pattern '\bWarning:|\bError:|Fatal error:' -Quiet) {
            throw 'Successful smoke operation emitted engine warnings/errors; review before accepting.'
        }
        if ($result.protocolVersion -ne 1 -or $result.jobId -cne $request.jobId -or
            $result.engineVersion -cne $profile.version -or $result.status -cne 'success' -or
            $result.outputsPromoted -ne $false -or @($result.artifacts).Count -ne 1 -or @($result.findings).Count -ne 0) {
            throw 'The engine did not produce the expected successful candidate smoke result.'
        }
        $asset = Join-Path $run 'project/Content/Pack/PB_WorkerProbe.uasset'
        Assert-UnrealContainedPath $asset $run
        $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($result.artifacts[0].sha256 -cne $hash -or
            $result.artifacts[0].byteCount -ne (Get-Item -LiteralPath $asset).Length -or
            ($assetHash -and $assetHash -cne $hash)) { throw 'Saved/reopened artifact evidence does not match.' }
        $assetHash = $hash
        $eventsPath = Join-Path $run 'output/worker-events.jsonl'
        Assert-UnrealContainedPath $eventsPath $run
        if ((Get-Item -LiteralPath $eventsPath).Length -gt 1MB) { throw 'Progress exceeds smoke-test bounds.' }
        $events = @(Get-Content -LiteralPath $eventsPath | ForEach-Object { $_ | ConvertFrom-Json })
        if ($events.Count -ne 2 -or $events[0].percent -ne 0 -or $events[1].percent -ne 100 -or
            @($events | Where-Object { $_.jobId -cne $request.jobId -or $_.eventKind -cne 'progress' }).Count) {
            throw 'Worker progress is incomplete or mismatched.'
        }
        Copy-Item -LiteralPath $resultPath -Destination (Join-Path $evidence "$operation.json")
        Copy-Item -LiteralPath $eventsPath -Destination (Join-Path $evidence "$operation.events.jsonl")
        $receipt.operations += $operation
    }
    $receipt.smokePassed = $true
    $success = $true
} finally {
    if ($process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    try {
        if (Test-Path -LiteralPath $run) {
            Assert-UnrealContainedPath $run $runs
            # Inspect every directory before descent; never follow a linked engine-created path.
            $pending = [Collections.Generic.Stack[string]]::new()
            $pending.Push($run)
            while ($pending.Count) {
                foreach ($entry in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
                    Assert-UnrealContainedPath $entry.FullName $run
                    if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
                }
            }
            Remove-Item -LiteralPath $run -Recurse -Force
        }
        $cleaned = -not (Test-Path -LiteralPath $run)
    } finally {
        $receipt.cleanupSucceeded = $cleaned
        if (Test-Path -LiteralPath $evidence) {
            [IO.File]::WriteAllText((Join-Path $evidence 'receipt.json'), ($receipt | ConvertTo-Json -Depth 8))
        }
    }
}
if (-not $success -or -not $cleaned) { throw 'Unreal foundation validation or cleanup failed.' }
Write-Host "Candidate smoke passed; production approval remains separate. Evidence: $evidence"
