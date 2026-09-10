# Removes only the known disposable outputs of an isolated artifacts/u/<run-id> integration.
# Logs, inventories, result pointers and copied diagnostic reports stay at the run root.
function Remove-UnityTestArtifacts {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$RunRoot,
        [switch]$KeepArtifacts
    )

    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
    $runs = Join-Path $repository 'artifacts\u'
    $run = [IO.Path]::GetFullPath($RunRoot).TrimEnd([char[]]'\/')
    if ((Split-Path $run -Parent) -ne $runs -or (Split-Path $run -Leaf) -cnotmatch '^[0-9a-f]{8}$') {
        throw "Refusing cleanup outside an isolated Unity test run: $run"
    }
    # Check every ancestor before resolving or enumerating: junctions must never redirect cleanup.
    $ancestor = $run
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing cleanup through a reparse point: $ancestor"
            }
        }
        $ancestor = Split-Path $ancestor -Parent
    }
    if (-not (Test-Path -LiteralPath $run -PathType Container)) { return }
    if ($KeepArtifacts) {
        Write-Host "Successful test artifacts retained by request: $run; remove after inspection."
        return
    }
    # A stopped harness can leave its Editor alive; never remove a project still in use.
    foreach ($process in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe' OR Name = 'blender.exe' OR Name = 'Unity Hub.exe'") {
        if ($process.CommandLine -and $process.CommandLine.IndexOf($run, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Close the test engine process $($process.ProcessId) before cleaning $run."
        }
    }

    $payloadNames = @('p', 'r', 'g', 'i', 'c', 'e', 'm', 't', 's', 'prepared',
        'unitypackage-extracted', 'silverwing-unitypackage-extracted', 'equipment',
        'equipment-import', 'equipment-import-contained', 'items.unitypackage')
    $targets = @()
    [long]$bytes = 0
    foreach ($name in $payloadNames) {
        $target = [IO.Path]::GetFullPath((Join-Path $run $name))
        if ((Split-Path $target -Parent) -ne $run) { throw "Cleanup target escaped its run: $target" }
        if (-not (Test-Path -LiteralPath $target)) { continue }
        # Enumerate without following links. Validate the whole deletion set before deleting anything.
        $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
        $pending.Push((Get-Item -LiteralPath $target -Force))
        while ($pending.Count -gt 0) {
            $item = $pending.Pop()
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing cleanup of a test tree containing a reparse point: $($item.FullName)"
            }
            if ($item.Attributes -band [IO.FileAttributes]::Directory) {
                foreach ($child in ([IO.DirectoryInfo]$item).EnumerateFileSystemInfos()) {
                    $pending.Push($child)
                }
            }
            else { $bytes += $item.Length }
        }
        $targets += $target
    }

    foreach ($name in @('cleanup-result.json', 'integration-result.json', 'equipment-portable-reimport.json',
            'equipment-contained-reimport.json', 'silverwing-prepare-report.json')) {
        $destination = Join-Path $run $name
        if ((Test-Path -LiteralPath $destination) -and
            ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to write cleanup evidence through a reparse point: $destination"
        }
    }
    foreach ($report in @(
            @('equipment-import/reimport-result.json', 'equipment-portable-reimport.json'),
            @('equipment-import-contained/reimport-result.json', 'equipment-contained-reimport.json'),
            @('prepared/silverwing-prepare-report.json', 'silverwing-prepare-report.json'))) {
        $source = Join-Path $run $report[0]
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $run $report[1]) -Force
        }
    }
    foreach ($target in $targets) {
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
        if (Test-Path -LiteralPath $target) { throw "Test artifact cleanup was incomplete: $target" }
    }
    $reportPath = Join-Path $run 'cleanup-result.json'
    # Preserve the previous receipt on repeated cleanup of an already cleaned run.
    if ($targets.Count -gt 0 -or -not (Test-Path -LiteralPath $reportPath)) {
        [ordered]@{
            schemaVersion = 1
            status = 'cleaned'
            runRoot = $run
            cleanedAtUtc = [DateTime]::UtcNow.ToString('o')
            removedPaths = $targets
            removedBytes = $bytes
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    }
    $pointer = Join-Path $run 'integration-result.json'
    if (Test-Path -LiteralPath $pointer -PathType Leaf) {
        $evidence = Get-Content -LiteralPath $pointer -Raw | ConvertFrom-Json
        $evidence | Add-Member -NotePropertyName artifactsRetained -NotePropertyValue $false -Force
        $evidence | Add-Member -NotePropertyName cleanupReport -NotePropertyValue $reportPath -Force
        if ($evidence.PSObject.Properties['equipmentPortableReimportResult']) {
            $evidence.equipmentPortableReimportResult = Join-Path $run 'equipment-portable-reimport.json'
        }
        if ($evidence.PSObject.Properties['preparationReport']) {
            $evidence.preparationReport = Join-Path $run 'silverwing-prepare-report.json'
        }
        $evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $pointer -Encoding UTF8
    }
    Write-Host "Test artifacts cleaned: $run ($bytes bytes removed); logs and reports preserved."
}
