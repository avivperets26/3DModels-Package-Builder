[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$RequireTrackedFiles
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:FailureCount = 0
$script:PassCount = 0
$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git -C $script:RepositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }

    return $output
}

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $script:RepositoryRoot) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
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

function Get-BacklogTasks {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines)

    $definitions = @()
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        $match = [regex]::Match($Lines[$index], '^\s*- \[(?<check>[ x])\] \*\*(?<id>PB-\d{4})\b')
        if ($match.Success) {
            $definitions += [pscustomobject]@{
                Id = $match.Groups['id'].Value
                Checked = $match.Groups['check'].Value -eq 'x'
                Index = $index
                Header = $Lines[$index]
            }
        }
    }

    for ($taskIndex = 0; $taskIndex -lt $definitions.Count; $taskIndex++) {
        $task = $definitions[$taskIndex]
        $end = if ($taskIndex + 1 -lt $definitions.Count) { $definitions[$taskIndex + 1].Index - 1 } else { $Lines.Count - 1 }
        $block = @($Lines[$task.Index..$end])
        $branchMatches = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Branch:\s+`(?<branch>[^`]+)`\s*$')
            if ($match.Success) { $match.Groups['branch'].Value }
        })
        $dependencyMatches = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Depends on:\s+(?<dependencies>.+?)\s*$')
            if ($match.Success) { $match.Groups['dependencies'].Value }
        })

        Add-Member -InputObject $task -NotePropertyName Branch -NotePropertyValue $branchMatches
        Add-Member -InputObject $task -NotePropertyName DependencyText -NotePropertyValue $dependencyMatches
    }

    return $definitions
}

function Test-Markdown {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $script:RepositoryRoot $RelativePath
    $lines = @(Get-Content -LiteralPath $fullPath -Encoding UTF8)
    if ($lines.Count -eq 0) {
        throw "$RelativePath is empty."
    }

    $openFence = $null
    $previousHeading = 0
    $sawHeading = $false
    $tableColumns = $null

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line.Contains([string][char]0xFFFD)) {
            throw "$RelativePath contains a Unicode replacement character at line $($index + 1)."
        }

        $fence = [regex]::Match($line, '^\s*(?<value>`{3,}|~{3,})')
        if ($fence.Success) {
            $character = $fence.Groups['value'].Value.Substring(0, 1)
            if ($null -eq $openFence) {
                $openFence = $character
            }
            elseif ($openFence -eq $character) {
                $openFence = $null
            }
            continue
        }

        if ($null -ne $openFence) {
            continue
        }

        $heading = [regex]::Match($line, '^(?<marks>#{1,6})\s+\S')
        if ($heading.Success) {
            $level = $heading.Groups['marks'].Value.Length
            if (-not $sawHeading -and $level -ne 1) {
                throw "$RelativePath must begin its heading hierarchy at level 1."
            }
            if ($sawHeading -and $level -gt ($previousHeading + 1)) {
                throw "$RelativePath skips from heading level $previousHeading to $level at line $($index + 1)."
            }
            $sawHeading = $true
            $previousHeading = $level
        }

        if ($line -match '^\s*\|.*\|\s*$') {
            $currentColumns = ([regex]::Matches($line, '(?<!\\)\|')).Count - 1
            if ($null -ne $tableColumns -and $currentColumns -ne $tableColumns) {
                throw "$RelativePath has an inconsistent Markdown table at line $($index + 1)."
            }
            $tableColumns = $currentColumns
        }
        else {
            $tableColumns = $null
        }

        foreach ($link in [regex]::Matches($line, '!?' + '\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)')) {
            $target = $link.Groups['target'].Value.Trim([char[]]'<>')
            if ($target.StartsWith('#') -or $target.StartsWith('//') -or $target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
                continue
            }

            $pathPart = ($target -split '#', 2)[0]
            $pathPart = ($pathPart -split '\?', 2)[0]
            if ([string]::IsNullOrWhiteSpace($pathPart)) {
                continue
            }

            $decodedPath = [System.Uri]::UnescapeDataString($pathPart).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            if ([System.IO.Path]::IsPathRooted($decodedPath)) {
                throw "$RelativePath contains a rooted local link '$target' at line $($index + 1)."
            }

            $targetPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $fullPath -Parent) $decodedPath))
            if (-not (Test-ContainedPath $targetPath)) {
                throw "$RelativePath contains a link outside the repository: '$target'."
            }
            if (-not (Test-Path -LiteralPath $targetPath)) {
                throw "$RelativePath contains a missing local link '$target' at line $($index + 1)."
            }
        }
    }

    if (-not $sawHeading) {
        throw "$RelativePath contains no Markdown heading."
    }
    if ($null -ne $openFence) {
        throw "$RelativePath contains an unclosed fenced code block."
    }
}

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

$gitRoot = @(Invoke-Git @('rev-parse', '--show-toplevel'))[0]
$gitRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $script:RepositoryRoot)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

if ($env:GITHUB_ACTIONS -eq 'true') {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_WORKSPACE)) {
        throw 'GITHUB_WORKSPACE is required in GitHub Actions.'
    }
    $workspace = [System.IO.Path]::GetFullPath($env:GITHUB_WORKSPACE).TrimEnd([char[]]'\/')
    if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($workspace, $script:RepositoryRoot)) {
        throw "RepositoryRoot must resolve from GITHUB_WORKSPACE in GitHub Actions. Workspace: $workspace"
    }
}

$trackedPaths = @(Invoke-Git @('ls-files') | ForEach-Object { $_.Replace('\', '/') })
$candidatePaths = @(
    $trackedPaths
    Invoke-Git @('ls-files', '--others', '--exclude-standard') | ForEach-Object { $_.Replace('\', '/') }
) | Sort-Object -Unique
$trackedLookup = @{}
foreach ($path in $trackedPaths) { $trackedLookup[$path.ToLowerInvariant()] = $true }
$candidateLookup = @{}
foreach ($path in $candidatePaths) { $candidateLookup[$path.ToLowerInvariant()] = $true }

$backlogPath = Join-Path $script:RepositoryRoot 'docs\IMPLEMENTATION_BACKLOG.md'
$backlogLines = @(Get-Content -LiteralPath $backlogPath -Encoding UTF8)
$tasks = @(Get-BacklogTasks $backlogLines)

Invoke-Check 'Required repository files exist in the reviewable Git set' {
    $requiredFiles = @(
        '.gitignore',
        'AGENTS.md',
        'global.json',
        'docs/Package_Builder_Plan.md',
        'docs/TECH_STACK_AND_ARCHITECTURE.md',
        'docs/IMPLEMENTATION_BACKLOG.md',
        'docs/QUALITY_AND_RELEASE_GATES.md',
        'docs/PB-0001_ENVIRONMENT_BASELINE.md',
        'docs/PB-0002_REPOSITORY_BASELINE.md',
        'scripts/Enter-PackageBuilderEnvironment.ps1',
        'scripts/Test-RepositoryBaseline.ps1',
        '.github/workflows/repository-baseline.yml'
    )

    $missing = @($requiredFiles | Where-Object { -not $candidateLookup.ContainsKey($_.ToLowerInvariant()) })
    if ($missing.Count -gt 0) {
        throw "Missing required files: $($missing -join ', ')"
    }

    $mustAlreadyBeTracked = $requiredFiles | Where-Object {
        $_ -notin @('scripts/Test-RepositoryBaseline.ps1', '.github/workflows/repository-baseline.yml')
    }
    $notTracked = @($mustAlreadyBeTracked | Where-Object { -not $trackedLookup.ContainsKey($_.ToLowerInvariant()) })
    if ($RequireTrackedFiles) {
        $notTracked = @($requiredFiles | Where-Object { -not $trackedLookup.ContainsKey($_.ToLowerInvariant()) })
    }
    if ($notTracked.Count -gt 0) {
        throw "Required files are not tracked: $($notTracked -join ', ')"
    }
}

Invoke-Check 'global.json contains the approved .NET SDK pin' {
    $globalJson = Get-Content -Raw -LiteralPath (Join-Path $script:RepositoryRoot 'global.json') -Encoding UTF8 | ConvertFrom-Json
    if ($globalJson.sdk.version -ne '10.0.302') { throw "Expected SDK 10.0.302; found '$($globalJson.sdk.version)'." }
    if ($globalJson.sdk.rollForward -ne 'disable') { throw "Expected rollForward 'disable'." }
    if ($globalJson.sdk.allowPrerelease -ne $false) { throw 'Expected allowPrerelease false.' }
}

Invoke-Check 'PowerShell scripts parse successfully' {
    $scripts = @($candidatePaths | Where-Object { $_ -match '(?i)\.ps1$' })
    if ($scripts.Count -eq 0) { throw 'No PowerShell scripts were found.' }
    $parseFailures = @()
    foreach ($relativePath in $scripts) {
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $script:RepositoryRoot $relativePath),
            [ref]$tokens,
            [ref]$errors
        ) | Out-Null
        foreach ($error in $errors) {
            $parseFailures += "${relativePath}:$($error.Extent.StartLineNumber): $($error.Message)"
        }
    }
    if ($parseFailures.Count -gt 0) { throw ($parseFailures -join '; ') }
}

Invoke-Check 'Repository ignore policy passes synthetic and tracked-file validation' {
    $validatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-GitIgnorePolicy.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-GitIgnorePolicy.ps1.'
    }

    & $validatorPath -RepositoryRoot $script:RepositoryRoot
}

Invoke-Check 'Git ignore validator supports standalone Windows PowerShell invocation' {
    $validatorPath = [System.IO.Path]::GetFullPath(
        (Join-Path $script:RepositoryRoot 'scripts\Test-GitIgnorePolicy.ps1')
    )
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-GitIgnorePolicy.ps1.'
    }

    $windowsPowerShellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShellPath -PathType Leaf)) {
        throw "Windows PowerShell executable is unavailable: $windowsPowerShellPath"
    }

    $validatorOutput = @(
        & $windowsPowerShellPath `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $validatorPath 2>&1
    )
    $validatorExitCode = $LASTEXITCODE
    if ($validatorExitCode -ne 0) {
        $capturedOutput = $validatorOutput -join [Environment]::NewLine
        throw "Standalone ignore-policy validator failed with exit code ${validatorExitCode}. Captured output:`n$capturedOutput"
    }
}

Invoke-Check 'Central build configuration passes dependency-free validation' {
    $validatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-CentralBuildConfiguration.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-CentralBuildConfiguration.ps1.'
    }

    & $validatorPath -RepositoryRoot $script:RepositoryRoot
}

Invoke-Check 'Central build validator supports standalone Windows PowerShell invocation' {
    $validatorPath = [System.IO.Path]::GetFullPath(
        (Join-Path $script:RepositoryRoot 'scripts\Test-CentralBuildConfiguration.ps1')
    )
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-CentralBuildConfiguration.ps1.'
    }

    $windowsPowerShellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShellPath -PathType Leaf)) {
        throw "Windows PowerShell executable is unavailable: $windowsPowerShellPath"
    }

    $validatorOutput = @(
        & $windowsPowerShellPath `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $validatorPath `
            -RepositoryRoot $script:RepositoryRoot 2>&1
    )
    $validatorExitCode = $LASTEXITCODE
    if ($validatorExitCode -ne 0) {
        $capturedOutput = $validatorOutput -join [Environment]::NewLine
        throw "Standalone central-build validator failed with exit code ${validatorExitCode}. Captured output:`n$capturedOutput"
    }
}

Invoke-Check 'Test-project configuration passes dependency-free validation' {
    $validatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-TestProjects.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-TestProjects.ps1.'
    }

    & $validatorPath -RepositoryRoot $script:RepositoryRoot
}

Invoke-Check 'Test-project validator supports standalone Windows PowerShell invocation' {
    $validatorPath = [System.IO.Path]::GetFullPath(
        (Join-Path $script:RepositoryRoot 'scripts\Test-TestProjects.ps1')
    )
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-TestProjects.ps1.'
    }

    $windowsPowerShellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShellPath -PathType Leaf)) {
        throw "Windows PowerShell executable is unavailable: $windowsPowerShellPath"
    }

    $validatorOutput = @(
        & $windowsPowerShellPath `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $validatorPath `
            -RepositoryRoot $script:RepositoryRoot 2>&1
    )
    $validatorExitCode = $LASTEXITCODE
    if ($validatorExitCode -ne 0) {
        $capturedOutput = $validatorOutput -join [Environment]::NewLine
        throw "Standalone test-project validator failed with exit code ${validatorExitCode}. Captured output:`n$capturedOutput"
    }
}

Invoke-Check 'Formatting configuration passes dependency-free validation' {
    $validatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-FormattingConfiguration.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-FormattingConfiguration.ps1.'
    }

    & $validatorPath -RepositoryRoot $script:RepositoryRoot
}

Invoke-Check 'Formatting configuration validator supports standalone Windows PowerShell invocation' {
    $validatorPath = [System.IO.Path]::GetFullPath(
        (Join-Path $script:RepositoryRoot 'scripts\Test-FormattingConfiguration.ps1')
    )
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-FormattingConfiguration.ps1.'
    }

    $windowsPowerShellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShellPath -PathType Leaf)) {
        throw "Windows PowerShell executable is unavailable: $windowsPowerShellPath"
    }

    $validatorOutput = @(
        & $windowsPowerShellPath `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $validatorPath `
            -RepositoryRoot $script:RepositoryRoot 2>&1
    )
    $validatorExitCode = $LASTEXITCODE
    if ($validatorExitCode -ne 0) {
        $capturedOutput = $validatorOutput -join [Environment]::NewLine
        throw "Standalone formatting-configuration validator failed with exit code ${validatorExitCode}. Captured output:`n$capturedOutput"
    }
}

Invoke-Check 'Markdown structure and local links are valid' {
    $markdownFiles = @($candidatePaths | Where-Object { $_ -match '(?i)\.md$' })
    foreach ($relativePath in $markdownFiles) { Test-Markdown $relativePath }
}

Invoke-Check 'PB task definition IDs are unique' {
    $duplicates = @($tasks | Group-Object Id | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($duplicates.Count -gt 0) { throw "Duplicate task IDs: $($duplicates -join ', ')" }
    if ($tasks.Count -eq 0) { throw 'No PB task definitions were found.' }
}

Invoke-Check 'PB dependencies are valid and acyclic' {
    $taskLookup = @{}
    foreach ($task in $tasks) { $taskLookup[$task.Id] = $task }
    $dependencies = @{}

    foreach ($task in $tasks) {
        if ($task.DependencyText.Count -ne 1) {
            throw "$($task.Id) must have exactly one Depends on line."
        }

        $dependencySet = @{}
        foreach ($match in [regex]::Matches($task.DependencyText[0], 'PB-\d{4}')) {
            $dependencySet[$match.Value] = $true
        }
        foreach ($range in [regex]::Matches($task.DependencyText[0], 'PB-(?<start>\d{4})\s+through\s+PB-(?<end>\d{4})')) {
            $start = [int]$range.Groups['start'].Value
            $end = [int]$range.Groups['end'].Value
            foreach ($candidate in $tasks) {
                $number = [int]$candidate.Id.Substring(3)
                if ($number -ge $start -and $number -le $end) { $dependencySet[$candidate.Id] = $true }
            }
        }

        foreach ($dependency in $dependencySet.Keys) {
            if (-not $taskLookup.ContainsKey($dependency)) { throw "$($task.Id) depends on unknown task $dependency." }
            if ($dependency -eq $task.Id) { throw "$($task.Id) depends on itself." }
        }
        $dependencies[$task.Id] = @($dependencySet.Keys)
    }

    $remaining = @{}
    $dependents = @{}
    foreach ($task in $tasks) {
        $remaining[$task.Id] = $dependencies[$task.Id].Count
        $dependents[$task.Id] = @()
    }
    foreach ($task in $tasks) {
        foreach ($dependency in $dependencies[$task.Id]) {
            $dependents[$dependency] = @($dependents[$dependency]) + $task.Id
        }
    }

    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    foreach ($task in $tasks) { if ($remaining[$task.Id] -eq 0) { $queue.Enqueue($task.Id) } }
    $visited = 0
    while ($queue.Count -gt 0) {
        $id = $queue.Dequeue()
        $visited++
        foreach ($dependent in $dependents[$id]) {
            $remaining[$dependent]--
            if ($remaining[$dependent] -eq 0) { $queue.Enqueue($dependent) }
        }
    }
    if ($visited -ne $tasks.Count) {
        $cycleMembers = @($remaining.Keys | Where-Object { $remaining[$_] -gt 0 } | Sort-Object)
        throw "Dependency cycle detected among: $($cycleMembers -join ', ')"
    }
}

Invoke-Check 'Task branch names and lifecycle markers are valid' {
    $allowedBranch = '^(chore|docs|feat|fix|test|security|release)/(?<id>PB-\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*$'
    $doneEmoji = [char]::ConvertFromUtf32(0x1F7E2)
    $processEmoji = [char]::ConvertFromUtf32(0x1F7E1)
    $blockedEmoji = [char]::ConvertFromUtf32(0x1F534)
    $doneMarker = $doneEmoji + ' **DONE**'
    $processMarker = $processEmoji + ' **PROCESS**'
    $blockedMarker = $blockedEmoji + ' **BLOCKED**'
    $lifecycleStates = @{}

    foreach ($task in $tasks) {
        if ($task.Branch.Count -ne 1) { throw "$($task.Id) must have exactly one Branch line." }
        $branchMatch = [regex]::Match($task.Branch[0], $allowedBranch)
        if (-not $branchMatch.Success -or $branchMatch.Groups['id'].Value -ne $task.Id) {
            throw "$($task.Id) has invalid branch '$($task.Branch[0])'."
        }

        $state = $null
        if ($task.Header.Contains($doneMarker)) { $state = 'DONE' }
        if ($task.Header.Contains($processMarker)) { if ($null -ne $state) { throw "$($task.Id) has multiple lifecycle markers." }; $state = 'PROCESS' }
        if ($task.Header.Contains($blockedMarker)) { if ($null -ne $state) { throw "$($task.Id) has multiple lifecycle markers." }; $state = 'BLOCKED' }
        $hasLifecycleToken = $task.Header.Contains($doneEmoji) -or $task.Header.Contains($processEmoji) -or
            $task.Header.Contains($blockedEmoji) -or $task.Header -match '\*\*(DONE|PROCESS|BLOCKED)\*\*'
        if ($hasLifecycleToken -and $null -eq $state) { throw "$($task.Id) has an invalid lifecycle marker." }
        if ($task.Checked -and $state -ne 'DONE') { throw "$($task.Id) is checked but is not DONE." }
        if (-not $task.Checked -and $state -eq 'DONE') { throw "$($task.Id) is DONE but is not checked." }
        $lifecycleStates[$task.Id] = $state
    }

    $activeStart = [array]::IndexOf($backlogLines, '## 3. Active Work')
    $activeEnd = [array]::IndexOf($backlogLines, '## 4. Completion Log')
    if ($activeStart -lt 0 -or $activeEnd -le $activeStart) { throw 'Active Work table boundaries were not found.' }
    $activeIds = @()
    foreach ($line in $backlogLines[($activeStart + 1)..($activeEnd - 1)]) {
        $row = [regex]::Match($line, '^\|\s*(?<id>PB-\d{4})\s*\|\s*(?<status>.*?)\s*\|\s*`(?<branch>[^`]+)`')
        if (-not $row.Success) { continue }
        $id = $row.Groups['id'].Value
        $activeIds += $id
        $task = @($tasks | Where-Object Id -eq $id)
        if ($task.Count -ne 1) { throw "Active Work contains unknown task $id." }
        $statusMatch = [regex]::Match($row.Groups['status'].Value, '\*\*(?<state>PROCESS|BLOCKED)\*\*')
        if (-not $statusMatch.Success) { throw "Active Work task $id has an invalid status." }
        if ($null -ne $lifecycleStates[$id] -and $lifecycleStates[$id] -ne $statusMatch.Groups['state'].Value) {
            throw "Active Work status for $id does not match its task marker."
        }
        if ($row.Groups['branch'].Value -ne $task[0].Branch[0]) { throw "Active Work branch for $id does not match its task definition." }
    }
    $expectedActive = @($tasks | Where-Object { $lifecycleStates[$_.Id] -in @('PROCESS', 'BLOCKED') } | ForEach-Object Id)
    $missingActive = @($expectedActive | Where-Object { $_ -notin $activeIds })
    if ($missingActive.Count -gt 0) {
        throw "Tasks with active lifecycle markers are missing from Active Work: $($missingActive -join ', ')."
    }
}

Invoke-Check 'Completion Log exactly matches completed tasks' {
    $completionStart = [array]::IndexOf($backlogLines, '## 4. Completion Log')
    $completionEnd = [array]::IndexOf($backlogLines, '## 5. Milestones')
    if ($completionStart -lt 0 -or $completionEnd -le $completionStart) { throw 'Completion Log boundaries were not found.' }
    $loggedRows = @($backlogLines[($completionStart + 1)..($completionEnd - 1)] | ForEach-Object {
        $row = [regex]::Match($_, '^\|\s*(?<id>PB-\d{4})\s*\|\s*`(?<branch>[^`]+)`')
        if ($row.Success) { [pscustomobject]@{ Id = $row.Groups['id'].Value; Branch = $row.Groups['branch'].Value } }
    })
    $logged = @($loggedRows | ForEach-Object Id)
    $completed = @($tasks | Where-Object Checked | ForEach-Object Id)
    $duplicates = @($logged | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    $missing = @($completed | Where-Object { $_ -notin $logged })
    $unexpected = @($logged | Where-Object { $_ -notin $completed })
    if ($duplicates.Count -gt 0 -or $missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "Completion Log mismatch. Duplicate: $($duplicates -join ', '); missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }
    foreach ($loggedRow in $loggedRows) {
        $task = @($tasks | Where-Object Id -eq $loggedRow.Id)[0]
        if ($loggedRow.Branch -ne $task.Branch[0]) { throw "Completion Log branch for $($loggedRow.Id) does not match its task definition." }
    }
}

Invoke-Check 'Tracked and candidate files contain no prohibited content' {
    $runtimePath = '^(tools|downloads|logs|runtime-data|artifacts)(/|$)'
    $generatedPath = '(^|/)(bin|obj|\.vs|Library|Temp|UserSettings|Intermediate|Saved|DerivedDataCache|__pycache__)(/|$)'
    $prohibitedExtension = '(?i)\.(exe|dll|pdb|msi|msix|appx|zip|7z|rar|nupkg|vsix|fbx|glb|gltf|blend|unitypackage|uasset|umap|pak|pfx|p12|key|dmp)$'
    $badPaths = @($candidatePaths | Where-Object { $_ -match $runtimePath -or $_ -match $generatedPath -or $_ -match $prohibitedExtension })
    if ($badPaths.Count -gt 0) { throw "Prohibited repository paths: $($badPaths -join ', ')" }

    foreach ($entry in Invoke-Git @('ls-files', '--stage')) {
        $match = [regex]::Match($entry, '^(?<mode>\d{6})\s+[0-9a-f]+\s+\d+\t(?<path>.+)$')
        if (-not $match.Success -or $match.Groups['mode'].Value -notin @('100644', '100755')) {
            throw "Tracked special file mode is prohibited: $entry"
        }
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $secretPatterns = @(
        '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
        '\bgh[pousr]_[A-Za-z0-9]{36,}\b',
        '\bAKIA[0-9A-Z]{16}\b',
        '\bxox[baprs]-[A-Za-z0-9-]{10,}\b',
        '\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}\b',
        '\bAIza[0-9A-Za-z_-]{35}\b',
        '\bsk_live_[0-9A-Za-z]{16,}\b',
        '(?i)AccountKey\s*=\s*[A-Za-z0-9+/=]{20,}',
        '(?i)\bhttps?://[^/\s:@]+:[^@\s/]+@',
        '(?i)\b(password|passwd|api[_-]?key|client[_-]?secret)\s*[:=]\s*["''][^"'']{8,}["'']'
    )
    $personalPathPatterns = @(
        '(?i)\b[A-Z]:[\\/]Users[\\/](?!Public(?:[\\/]|$)|Default(?: User)?(?:[\\/]|$))[^\\/\s]+',
        '(?i)(?<![A-Za-z0-9_])/(?:home|Users)/[A-Za-z0-9._-]+'
    )

    foreach ($relativePath in $candidatePaths) {
        $fullPath = Join-Path $script:RepositoryRoot $relativePath
        if (-not (Test-ContainedPath $fullPath)) { throw "Repository path escapes the root: $relativePath" }
        $item = Get-Item -LiteralPath $fullPath
        if ($item.Length -gt 1MB) { throw "Repository file exceeds the 1 MiB baseline limit: $relativePath" }
        $bytes = [System.IO.File]::ReadAllBytes($fullPath)
        if ($bytes -contains 0) { throw "Binary content is prohibited: $relativePath" }
        try { $text = $strictUtf8.GetString($bytes) } catch { throw "File is not valid UTF-8 text: $relativePath" }
        foreach ($pattern in $secretPatterns) {
            if ($text -match $pattern) { throw "Potential secret detected in $relativePath." }
        }
        foreach ($pattern in $personalPathPatterns) {
            if ($text -match $pattern) { throw "Personal filesystem path detected in $relativePath." }
        }
    }
}

Invoke-Check 'Core CI configuration preserves and extends the repository baseline' {
    $validatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-CoreCiConfiguration.ps1'
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-CoreCiConfiguration.ps1.'
    }

    & $validatorPath -RepositoryRoot $script:RepositoryRoot
}

Invoke-Check 'Core CI configuration validator supports standalone Windows PowerShell invocation' {
    $validatorPath = [System.IO.Path]::GetFullPath(
        (Join-Path $script:RepositoryRoot 'scripts\Test-CoreCiConfiguration.ps1')
    )
    if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
        throw 'Missing scripts/Test-CoreCiConfiguration.ps1.'
    }

    $windowsPowerShellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShellPath -PathType Leaf)) {
        throw "Windows PowerShell executable is unavailable: $windowsPowerShellPath"
    }

    $validatorOutput = @(
        & $windowsPowerShellPath `
            -NoProfile `
            -NonInteractive `
            -ExecutionPolicy Bypass `
            -File $validatorPath `
            -RepositoryRoot $script:RepositoryRoot 2>&1
    )
    $validatorExitCode = $LASTEXITCODE
    if ($validatorExitCode -ne 0) {
        $capturedOutput = $validatorOutput -join [Environment]::NewLine
        throw "Standalone core-CI validator failed with exit code ${validatorExitCode}. Captured output:`n$capturedOutput"
    }
}

Invoke-Check 'git diff --check passes for working tree and index' {
    Invoke-Git @('diff', '--check') | Out-Null
    Invoke-Git @('diff', '--cached', '--check') | Out-Null
}

Invoke-Check 'Reachable Git history passes strict integrity checks' {
    Invoke-Git @('fsck', '--full', '--strict', '--no-dangling') | Out-Null
    $objects = @(Invoke-Git @('rev-list', '--objects', '--all', '--missing=print'))
    if ($objects.Count -eq 0) { throw 'Reachable history contains no objects.' }
    $missing = @($objects | Where-Object { $_ -match '^\?' })
    if ($missing.Count -gt 0) { throw "Reachable history has missing objects: $($missing -join ', ')" }
}

Write-Host ''
Write-Host "Repository baseline validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Repository baseline validation failed.'
}
