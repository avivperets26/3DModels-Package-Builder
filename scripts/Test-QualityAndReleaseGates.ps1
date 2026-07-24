[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..')
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

function Test-ContainedPath {
    param([Parameter(Mandatory)][string]$Path)

    $resolved = [System.IO.Path]::GetFullPath($Path).TrimEnd([char[]]'\/')
    $prefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
    return [System.StringComparer]::OrdinalIgnoreCase.Equals($resolved, $script:RepositoryRoot) -or
        $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Read-RepositoryText {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $script:RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Required file does not exist: $RelativePath"
    }

    $bytes = [System.IO.File]::ReadAllBytes($fullPath)
    if ($bytes -contains 0) {
        throw "$RelativePath contains binary data."
    }

    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    try {
        return $strictUtf8.GetString($bytes)
    }
    catch {
        throw "$RelativePath is not valid UTF-8 text."
    }
}

function Get-SectionBody {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Heading
    )

    $pattern = '(?ms)^## ' + [regex]::Escape($Heading) + '\s*\r?\n(?<body>.*?)(?=^## |\z)'
    $match = [regex]::Match($Text, $pattern)
    if (-not $match.Success) {
        throw "Missing section: $Heading"
    }

    return $match.Groups['body'].Value.Trim()
}

function Get-ExpectedRequirementIds {
    $groups = [ordered]@{
        UX = 9
        TEST = 13
        PERF = 8
        SEC = 13
        INSTALL = 10
        ENG = 7
        REL = 8
    }

    $ids = @()
    foreach ($group in $groups.GetEnumerator()) {
        for ($number = 1; $number -le $group.Value; $number++) {
            $ids += ('{0}-{1:D3}' -f $group.Key, $number)
        }
    }
    return $ids
}

function Expand-RequirementExpression {
    param([Parameter(Mandatory)][string]$Expression)

    # Normalize the Markdown en/em dash range separators without relying on the
    # host shell's script-file encoding behavior.
    $normalized = $Expression.Replace([string][char]0x2013, '-').Replace([string][char]0x2014, '-')
    $results = @()
    foreach ($match in [regex]::Matches(
        $normalized,
        '(?<start>[A-Z]+-(?<startNumber>\d{3}))(?:\s*-\s*(?<end>[A-Z]+-(?<endNumber>\d{3})))?'
    )) {
        $startId = $match.Groups['start'].Value
        $prefix = $startId.Split('-')[0]
        $startNumber = [int]$match.Groups['startNumber'].Value
        $endNumber = $startNumber
        if ($match.Groups['end'].Success) {
            $endId = $match.Groups['end'].Value
            if ($endId.Split('-')[0] -ne $prefix) {
                throw "Requirement range crosses groups: $($match.Value)"
            }
            $endNumber = [int]$match.Groups['endNumber'].Value
        }
        if ($endNumber -lt $startNumber) {
            throw "Requirement range is descending: $($match.Value)"
        }
        for ($number = $startNumber; $number -le $endNumber; $number++) {
            $results += ('{0}-{1:D3}' -f $prefix, $number)
        }
    }
    return $results
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
                Header = $Lines[$index]
                Index = $index
            }
        }
    }

    for ($taskIndex = 0; $taskIndex -lt $definitions.Count; $taskIndex++) {
        $task = $definitions[$taskIndex]
        $end = if ($taskIndex + 1 -lt $definitions.Count) {
            $definitions[$taskIndex + 1].Index - 1
        }
        else {
            $Lines.Count - 1
        }
        $block = @($Lines[$task.Index..$end])
        $branch = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Branch:\s+`(?<value>[^`]+)`\s*$')
            if ($match.Success) { $match.Groups['value'].Value }
        })
        $owner = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Owner:\s+(?<value>.+?)\s*$')
            if ($match.Success) { $match.Groups['value'].Value }
        })
        $dependencies = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Depends on:\s+(?<value>.+?)\s*$')
            if ($match.Success) { $match.Groups['value'].Value }
        })
        $doneWhen = @($block | ForEach-Object {
            $match = [regex]::Match($_, '^\s+- Done when:\s+(?<value>.+?)\s*$')
            if ($match.Success) { $match.Groups['value'].Value }
        })

        Add-Member -InputObject $task -NotePropertyName Branch -NotePropertyValue $branch
        Add-Member -InputObject $task -NotePropertyName Owner -NotePropertyValue $owner
        Add-Member -InputObject $task -NotePropertyName DependencyText -NotePropertyValue $dependencies
        Add-Member -InputObject $task -NotePropertyName DoneWhen -NotePropertyValue $doneWhen
    }

    return $definitions
}

function Assert-MarkdownAndLocalLinks {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $script:RepositoryRoot $RelativePath
    $lines = @(Get-Content -LiteralPath $fullPath -Encoding UTF8)
    if ($lines.Count -eq 0 -or $lines[0] -notmatch '^# \S') {
        throw "$RelativePath must be non-empty and begin with a level-one heading."
    }

    $openFence = $null
    foreach ($line in $lines) {
        if ($line.Contains([string][char]0xFFFD)) {
            throw "$RelativePath contains a Unicode replacement character."
        }
        $fence = [regex]::Match($line, '^\s*(?<value>`{3,}|~{3,})')
        if ($fence.Success) {
            $character = $fence.Groups['value'].Value.Substring(0, 1)
            if ($null -eq $openFence) { $openFence = $character }
            elseif ($openFence -eq $character) { $openFence = $null }
            continue
        }
        if ($null -ne $openFence) { continue }

        foreach ($link in [regex]::Matches(
            $line,
            '!?\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
        )) {
            $target = $link.Groups['target'].Value.Trim([char[]]'<>')
            if ($target.StartsWith('#') -or $target.StartsWith('//') -or
                $target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
                continue
            }
            $pathPart = ($target -split '#', 2)[0]
            $pathPart = ($pathPart -split '\?', 2)[0]
            if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }
            $decoded = [System.Uri]::UnescapeDataString($pathPart).Replace(
                '/',
                [System.IO.Path]::DirectorySeparatorChar
            )
            if ([System.IO.Path]::IsPathRooted($decoded)) {
                throw "$RelativePath contains rooted local link '$target'."
            }
            $targetPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $fullPath -Parent) $decoded))
            if (-not (Test-ContainedPath $targetPath)) {
                throw "$RelativePath contains link outside the repository: '$target'."
            }
            if (-not (Test-Path -LiteralPath $targetPath)) {
                throw "$RelativePath contains missing local link '$target'."
            }
        }
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

$paths = [ordered]@{
    Rules = 'AGENTS.md'
    Plan = 'docs/Package_Builder_Plan.md'
    Architecture = 'docs/TECH_STACK_AND_ARCHITECTURE.md'
    Backlog = 'docs/IMPLEMENTATION_BACKLOG.md'
    Quality = 'docs/QUALITY_AND_RELEASE_GATES.md'
    AdrTraceability = 'docs/adr/ADR-0009-requirements-traceability-and-release-evidence.md'
    AdrUx = 'docs/adr/ADR-0010-accessible-guided-dry-run-workflow.md'
    AdrSecurity = 'docs/adr/ADR-0011-threat-model-secrets-and-network-consent.md'
    AdrQuality = 'docs/adr/ADR-0012-quality-toolchain-and-thresholds.md'
    AdrInstallation = 'docs/adr/ADR-0013-installer-portable-and-lifecycle-safety.md'
    Pb0012Evidence = 'docs/PB-0012_INITIAL_ADRS_EVIDENCE.md'
    Pb0013Evidence = 'docs/PB-0013_QUALITY_RELEASE_GATES_EVIDENCE.md'
    DocsIndex = 'docs/README.md'
}
$texts = @{}
foreach ($entry in $paths.GetEnumerator()) {
    $texts[$entry.Key] = Read-RepositoryText $entry.Value
}
$backlogLines = @($texts['Backlog'] -split '\r?\n')
$tasks = @(Get-BacklogTasks $backlogLines)
$expectedIds = @(Get-ExpectedRequirementIds)

Invoke-Check 'Exact 68-requirement inventory and groups are stable' {
    $definitionLines = @($texts['Quality'] -split '\r?\n' | Where-Object {
        $_ -match '^- \*\*(UX|TEST|PERF|SEC|INSTALL|ENG|REL)-\d{3}\b'
    })
    $actualIds = @($definitionLines | ForEach-Object {
        [regex]::Match($_, '^- \*\*(?<id>[A-Z]+-\d{3})\b').Groups['id'].Value
    })
    if ($actualIds.Count -ne 68) {
        throw "Expected 68 normative definitions; found $($actualIds.Count)."
    }
    $duplicates = @($actualIds | Group-Object | Where-Object Count -ne 1 | ForEach-Object Name)
    if ($duplicates.Count -gt 0) {
        throw "Duplicate requirement definitions: $($duplicates -join ', ')."
    }
    if (($actualIds -join ',') -ne ($expectedIds -join ',')) {
        $missing = @($expectedIds | Where-Object { $_ -notin $actualIds })
        $unexpected = @($actualIds | Where-Object { $_ -notin $expectedIds })
        throw "Requirement inventory/order mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }
    if ($texts['Quality'] -notmatch 'exactly 68 requirements') {
        throw 'The normative document does not state the exact 68-requirement total.'
    }
    Write-Host '       Inventory: UX 9; TEST 13; PERF 8; SEC 13; INSTALL 10; ENG 7; REL 8; total 68.'
}

Invoke-Check 'Requirement-to-E18 ownership mapping is exact and complete' {
    $matrix = Get-SectionBody $texts['Quality'] '9. Requirements-to-Tests Traceability Matrix'
    $mappedOwners = @{}
    foreach ($line in ($matrix -split '\r?\n')) {
        $row = [regex]::Match(
            $line,
            '^\|\s*(?<expression>(?:UX|TEST|PERF|SEC|INSTALL|ENG|REL)-[^|]+?)\s*\|\s*(?<task>PB-\d{4})\s*\|'
        )
        if (-not $row.Success) { continue }
        foreach ($id in @(Expand-RequirementExpression $row.Groups['expression'].Value)) {
            if ($mappedOwners.ContainsKey($id)) {
                throw "$id is mapped more than once."
            }
            $mappedOwners[$id] = $row.Groups['task'].Value
        }
    }

    $expectedOwners = @{}
    foreach ($id in @(Expand-RequirementExpression 'UX-001-UX-003')) { $expectedOwners[$id] = 'PB-1802' }
    foreach ($id in @(Expand-RequirementExpression 'UX-004-UX-007')) { $expectedOwners[$id] = 'PB-1804' }
    foreach ($id in @(Expand-RequirementExpression 'UX-008-UX-009')) { $expectedOwners[$id] = 'PB-1803' }
    foreach ($id in @(Expand-RequirementExpression 'TEST-001-TEST-003')) { $expectedOwners[$id] = 'PB-1801' }
    foreach ($id in @(Expand-RequirementExpression 'TEST-004-TEST-006')) { $expectedOwners[$id] = 'PB-1805' }
    foreach ($id in @(Expand-RequirementExpression 'TEST-007-TEST-010')) { $expectedOwners[$id] = 'PB-1806' }
    $expectedOwners['TEST-011'] = 'PB-1807'
    foreach ($id in @(Expand-RequirementExpression 'TEST-012-TEST-013')) { $expectedOwners[$id] = 'PB-1805' }
    foreach ($id in @(Expand-RequirementExpression 'PERF-001-PERF-002, PERF-007-PERF-008')) { $expectedOwners[$id] = 'PB-1808' }
    foreach ($id in @(Expand-RequirementExpression 'PERF-003-PERF-006')) { $expectedOwners[$id] = 'PB-1809' }
    foreach ($id in @(Expand-RequirementExpression 'SEC-001-SEC-004')) { $expectedOwners[$id] = 'PB-1810' }
    foreach ($id in @(Expand-RequirementExpression 'SEC-005-SEC-008, SEC-012')) { $expectedOwners[$id] = 'PB-1811' }
    foreach ($id in @(Expand-RequirementExpression 'SEC-009-SEC-011, SEC-013')) { $expectedOwners[$id] = 'PB-1812' }
    foreach ($id in @(Expand-RequirementExpression 'INSTALL-001-INSTALL-010')) { $expectedOwners[$id] = 'PB-1813' }
    foreach ($id in @(Expand-RequirementExpression 'ENG-001-ENG-007')) { $expectedOwners[$id] = 'PB-1814' }
    foreach ($id in @(Expand-RequirementExpression 'REL-001-REL-008')) { $expectedOwners[$id] = 'PB-1815' }

    foreach ($id in $expectedIds) {
        if (-not $mappedOwners.ContainsKey($id)) { throw "$id has no E18 owner mapping." }
        if ($mappedOwners[$id] -ne $expectedOwners[$id]) {
            throw "$id maps to $($mappedOwners[$id]); expected $($expectedOwners[$id])."
        }
    }
    $unexpected = @($mappedOwners.Keys | Where-Object { $_ -notin $expectedIds })
    if ($unexpected.Count -gt 0) {
        throw "Mapping contains unexpected requirement IDs: $($unexpected -join ', ')."
    }
    Write-Host "       Ownership rows cover all $($mappedOwners.Count) normative requirements."
}

Invoke-Check 'Mapped E18 tasks have owners, valid dependencies, and measurable Done-when clauses' {
    $taskLookup = @{}
    foreach ($task in $tasks) {
        if ($taskLookup.ContainsKey($task.Id)) { throw "Duplicate backlog task $($task.Id)." }
        $taskLookup[$task.Id] = $task
    }
    $mappedTaskIds = @(
        'PB-1801', 'PB-1802', 'PB-1803', 'PB-1804', 'PB-1805',
        'PB-1806', 'PB-1807', 'PB-1808', 'PB-1809', 'PB-1810',
        'PB-1811', 'PB-1812', 'PB-1813', 'PB-1814', 'PB-1815'
    )
    foreach ($taskId in $mappedTaskIds) {
        if (-not $taskLookup.ContainsKey($taskId)) { throw "Missing mapped E18 task $taskId." }
        $task = $taskLookup[$taskId]
        if ($task.Owner.Count -ne 1 -or [string]::IsNullOrWhiteSpace($task.Owner[0])) {
            throw "$taskId must have exactly one non-empty Owner line."
        }
        if ($task.DependencyText.Count -ne 1 -or $task.DependencyText[0] -eq 'none') {
            throw "$taskId must have one non-empty dependency clause."
        }
        $dependencyIds = @([regex]::Matches($task.DependencyText[0], 'PB-\d{4}') | ForEach-Object Value)
        if ($dependencyIds.Count -eq 0) { throw "$taskId has no concrete dependency ID." }
        foreach ($dependencyId in $dependencyIds) {
            if (-not $taskLookup.ContainsKey($dependencyId)) {
                throw "$taskId depends on unknown task $dependencyId."
            }
            if ($dependencyId -eq $taskId) { throw "$taskId depends on itself." }
        }
        if ($task.DoneWhen.Count -ne 1 -or $task.DoneWhen[0].Length -lt 120) {
            throw "$taskId must have one detailed measurable Done-when clause."
        }
        if ($task.DoneWhen[0] -notmatch '(?i)\b(pass|fail|reject|record|generate|validate|prove|report|test|evidence)\w*\b') {
            throw "$taskId Done-when clause lacks a measurable validation or evidence outcome."
        }
        if ($task.DoneWhen[0] -match '(?i)\b(TODO|TBD|TBC|FIXME)\b|may\s+(?:initially|temporarily)\s+be\s+placeholder') {
            throw "$taskId contains unresolved placeholder language."
        }
    }
    Write-Host '       All 15 E18 owner tasks have concrete dependencies and measurable acceptance evidence.'
}

Invoke-Check 'Coverage, mutation, test-portfolio, and meaningful-test rules agree' {
    $thresholdDocuments = @('Plan', 'Architecture', 'Backlog', 'Quality', 'AdrQuality')
    foreach ($name in $thresholdDocuments) {
        $text = $texts[$name]
        if ($text -notmatch '(?is)90% line.{0,100}85% branch') {
            throw "$name does not preserve the 90% line / 85% branch threshold."
        }
        $criticalLines = @($text -split '\r?\n' | Where-Object {
            $_ -match '100% branch' -and $_ -match '(?i)security' -and $_ -match '(?i)path' -and
                $_ -match '(?i)naming' -and $_ -match '(?i)manifest' -and $_ -match '(?i)package-integrity'
        })
        if ($criticalLines.Count -eq 0) {
            throw "$name does not preserve the five critical areas at 100% branch coverage."
        }
        if ($text -notmatch '(?is)mutation.{0,500}(high-risk|high risk).{0,250}(approval|approved)') {
            throw "$name does not preserve mutation and high-risk-survivor approval rules."
        }
        foreach ($testType in @(
            'unit', 'contract', 'integration', 'end-to-end', 'UI',
            'regression', 'installer', 'upgrade', 'failure-recovery'
        )) {
            if ($text -notmatch ('(?i)\b' + [regex]::Escape($testType) + '\b')) {
                throw "$name does not mention required test type '$testType'."
            }
        }
        if ($text -notmatch '(?is)(percentage|percentages|coverage|mutation).{0,300}(never|not|cannot|supplement|rather than replace).{0,300}(proof|prove|replace|substitute|satisf|criterion|requirement|evidence)') {
            throw "$name does not state that metrics cannot replace meaningful requirement evidence."
        }
    }
}

Invoke-Check 'REL-001 through REL-008 definitions and fail-closed summary are identical' {
    $expectedReleaseDescriptions = [ordered]@{
        'REL-001' = 'A normative requirement or PB acceptance criterion has no mapped test, regardless of any supplementary manual or documentary verification.'
        'REL-002' = 'A required automated test, manual verification, clean reimport/reopen, or engine fixture validation fails or is missing.'
        'REL-003' = 'Line, branch, critical-code branch, or approved mutation thresholds are not met, or an exclusion lacks user approval.'
        'REL-004' = 'A critical or high vulnerability remains without a time-bounded, explicitly user-approved exception.'
        'REL-005' = 'An approved time, memory, disk, or regression budget is exceeded without an explicitly user-approved exception.'
        'REL-006' = 'An accessibility-critical or keyboard-only workflow fails.'
        'REL-007' = 'Required installation, repair, upgrade, downgrade-prevention, uninstall, privilege, or retained-data validation fails.'
        'REL-008' = 'A generated package fails content integrity, validation-report consistency, clean engine import/reopen, or unexpected-content scanning.'
    }
    $qualityReleaseLines = @($texts['Quality'] -split '\r?\n' | Where-Object {
        $_ -match '^- \*\*REL-\d{3}\b'
    })
    if ($qualityReleaseLines.Count -ne 8) {
        throw "Expected 8 release definitions; found $($qualityReleaseLines.Count)."
    }
    for ($index = 0; $index -lt $qualityReleaseLines.Count; $index++) {
        $match = [regex]::Match(
            $qualityReleaseLines[$index],
            '^- \*\*(?<id>REL-\d{3})\b[^:]*:\*\*\s+(?<description>.+)$'
        )
        if (-not $match.Success) {
            throw "Malformed release definition: $($qualityReleaseLines[$index])"
        }
        $expectedId = 'REL-{0:D3}' -f ($index + 1)
        if ($match.Groups['id'].Value -ne $expectedId -or
            $match.Groups['description'].Value -ne $expectedReleaseDescriptions[$expectedId]) {
            throw "$expectedId differs from the approved release-blocking rule."
        }
    }
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'AdrTraceability', 'AdrQuality')) {
        $alternateDefinitions = @($texts[$name] -split '\r?\n' | Where-Object {
            $_ -match '^- \*\*REL-\d{3}\b'
        })
        if ($alternateDefinitions.Count -gt 0) {
            throw "$name contains alternate REL definitions."
        }
    }
    $canonicalSummary = 'The canonical release blockers are REL-001 through REL-008 in `docs/QUALITY_AND_RELEASE_GATES.md`; missing, stale, unreadable, contradictory, or failing evidence blocks release.'
    foreach ($name in @('Plan', 'Architecture', 'Backlog', 'Quality', 'AdrQuality')) {
        $count = ([regex]::Matches($texts[$name], [regex]::Escape($canonicalSummary))).Count
        if ($count -ne 1) {
            throw "$name must contain the canonical release-blocker summary exactly once; found $count."
        }
    }
    Write-Host '       REL-001 through REL-008 are complete, unique, normative in one source, and fail closed.'
}

Invoke-Check 'Cross-document UX, security, installation, containment, free-tooling, and Git policies agree' {
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'Quality', 'AdrUx')) {
        foreach ($term in @('keyboard', 'high contrast', 'focus', 'dry-run', 'cancel', 'retry')) {
            if ($texts[$name] -notmatch ('(?i)' + [regex]::Escape($term))) {
                throw "$name is missing UX/accessibility term '$term'."
            }
        }
    }
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'Quality', 'AdrSecurity')) {
        foreach ($term in @('threat model', 'path traversal', 'secret', 'redact', 'consent')) {
            if ($texts[$name] -notmatch ('(?i)' + [regex]::Escape($term))) {
                throw "$name is missing security/privacy term '$term'."
            }
        }
    }
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'Quality', 'AdrInstallation')) {
        foreach ($term in @('Visual Studio Code', 'installer', 'diagnostic', 'uninstall')) {
            if ($texts[$name] -notmatch ('(?i)' + [regex]::Escape($term))) {
                throw "$name is missing installation/free-tooling term '$term'."
            }
        }
    }
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'Quality', 'AdrTraceability')) {
        if ($texts[$name] -notmatch '(?is)(user.control|explicit user control|user-controlled Git|Git and remote actions remain under explicit user control)') {
            throw "$name does not preserve manual Git ownership."
        }
    }
    foreach ($name in @('Rules', 'Plan', 'Architecture', 'Backlog', 'Quality', 'AdrQuality')) {
        if ($texts[$name] -notmatch '(?i)(free local|free tooling|no-cost|paid .* not|must not depend on paid)') {
            throw "$name does not preserve a free local or self-hosted workflow."
        }
    }
}

Invoke-Check 'Markdown and repository-local links are valid for PB-0013 sources' {
    foreach ($relativePath in @(
        $paths['Plan'],
        $paths['Architecture'],
        $paths['Backlog'],
        $paths['Quality'],
        $paths['AdrTraceability'],
        $paths['AdrUx'],
        $paths['AdrSecurity'],
        $paths['AdrQuality'],
        $paths['AdrInstallation'],
        $paths['Pb0012Evidence'],
        $paths['Pb0013Evidence'],
        $paths['DocsIndex']
    )) {
        Assert-MarkdownAndLocalLinks $relativePath
    }
}

Invoke-Check 'PB task IDs, dependencies, branches, lifecycle, Active Work, and Completion Log are consistent' {
    $taskLookup = @{}
    $allowedBranch = '^(chore|docs|feat|fix|test|security|release)/(?<id>PB-\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*$'
    foreach ($task in $tasks) {
        if ($taskLookup.ContainsKey($task.Id)) { throw "Duplicate task ID $($task.Id)." }
        $taskLookup[$task.Id] = $task
        if ($task.Branch.Count -ne 1) { throw "$($task.Id) must have exactly one Branch line." }
        $branchMatch = [regex]::Match($task.Branch[0], $allowedBranch)
        if (-not $branchMatch.Success -or $branchMatch.Groups['id'].Value -ne $task.Id) {
            throw "$($task.Id) has invalid branch '$($task.Branch[0])'."
        }
        if ($task.DependencyText.Count -ne 1) {
            throw "$($task.Id) must have exactly one Depends on line."
        }
        foreach ($dependency in @([regex]::Matches($task.DependencyText[0], 'PB-\d{4}') | ForEach-Object Value)) {
            if ($dependency -eq $task.Id) { throw "$($task.Id) depends on itself." }
        }
    }
    foreach ($task in $tasks) {
        foreach ($dependency in @([regex]::Matches($task.DependencyText[0], 'PB-\d{4}') | ForEach-Object Value)) {
            if (-not $taskLookup.ContainsKey($dependency)) {
                throw "$($task.Id) depends on unknown task $dependency."
            }
        }
    }

    $activeSection = Get-SectionBody $texts['Backlog'] '3. Active Work'
    $activeIds = @([regex]::Matches($activeSection, '(?m)^\|\s*(PB-\d{4})\s*\|') | ForEach-Object {
        $_.Groups[1].Value
    })
    $completionSection = Get-SectionBody $texts['Backlog'] '4. Completion Log'
    $loggedIds = @([regex]::Matches($completionSection, '(?m)^\|\s*(PB-\d{4})\s*\|') | ForEach-Object {
        $_.Groups[1].Value
    })
    $loggedDuplicates = @($loggedIds | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($loggedDuplicates.Count -gt 0) {
        throw "Completion Log duplicates: $($loggedDuplicates -join ', ')."
    }
    $completedIds = @($tasks | Where-Object Checked | ForEach-Object Id)
    $missingLogged = @($completedIds | Where-Object { $_ -notin $loggedIds })
    $unexpectedLogged = @($loggedIds | Where-Object { $_ -notin $completedIds })
    if ($missingLogged.Count -gt 0 -or $unexpectedLogged.Count -gt 0) {
        throw "Completion Log mismatch. Missing: $($missingLogged -join ', '); unexpected: $($unexpectedLogged -join ', ')."
    }
    $pb0012 = $taskLookup['PB-0012']
    $pb0013 = $taskLookup['PB-0013']
    if (-not $pb0012.Checked -or $pb0012.Header -notmatch '\*\*DONE\*\*') {
        throw 'PB-0012 must be checked and DONE after rollover.'
    }
    if ('PB-0012' -in $activeIds -or (@($loggedIds | Where-Object { $_ -eq 'PB-0012' }).Count -ne 1)) {
        throw 'PB-0012 must be absent from Active Work and logged exactly once.'
    }
    if ($pb0013.Checked -or $pb0013.Header -notmatch '\*\*PROCESS\*\*') {
        throw 'PB-0013 must remain unchecked and PROCESS on its own branch.'
    }
    if ('PB-0013' -notin $activeIds -or 'PB-0013' -in $loggedIds) {
        throw 'PB-0013 must remain in Active Work and absent from the Completion Log.'
    }
}

Invoke-Check 'PB-0012 rollover and historical PB-0013 evidence are preserved' {
    foreach ($commit in @(
        '335691dcceeaa645231539a2ec83a3dae9db2a3e',
        'f4b5a5d39b2de97e404f837150bbe0d869e3a366',
        'fc34bffff838cac41198940ed54b91b25c33f838',
        'a1032c48f2a8d0dc98d0c589f1a845605950952b',
        '13e5875b686c3219e3571d45ceaa93c463e881ff'
    )) {
        Invoke-Git @('cat-file', '-e', ($commit + '^{commit}')) | Out-Null
    }
    foreach ($relation in @(
        @('a1032c48f2a8d0dc98d0c589f1a845605950952b', '13e5875b686c3219e3571d45ceaa93c463e881ff'),
        @('13e5875b686c3219e3571d45ceaa93c463e881ff', 'f4b5a5d39b2de97e404f837150bbe0d869e3a366'),
        @('335691dcceeaa645231539a2ec83a3dae9db2a3e', 'f4b5a5d39b2de97e404f837150bbe0d869e3a366')
    )) {
        & git -C $script:RepositoryRoot merge-base --is-ancestor $relation[0] $relation[1]
        if ($LASTEXITCODE -ne 0) {
            throw "$($relation[0]) is not an ancestor of $($relation[1])."
        }
    }
    $combinedEvidence = $texts['Backlog'] + "`n" + $texts['Pb0012Evidence'] + "`n" + $texts['Pb0013Evidence']
    foreach ($token in @(
        '335691dcceeaa645231539a2ec83a3dae9db2a3e',
        '/pull/13',
        '/actions/runs/30083665801',
        'f4b5a5d39b2de97e404f837150bbe0d869e3a366',
        '/actions/runs/30083674462',
        'fc34bffff838cac41198940ed54b91b25c33f838',
        'a1032c48f2a8d0dc98d0c589f1a845605950952b',
        '/pull/1',
        '13e5875b686c3219e3571d45ceaa93c463e881ff',
        'No CI, completion, or quality exception was used'
    )) {
        if (-not $combinedEvidence.Contains($token)) {
            throw "Required rollover/history evidence is missing token '$token'."
        }
    }
    if ($combinedEvidence -notmatch '(?is)one-task-per-branch conflict.{0,1200}(preserved|without rewrite)') {
        throw 'Historical PB-0013 one-task-per-branch conflict is not preserved explicitly.'
    }
    if ($combinedEvidence -notmatch '(?is)correct documented.*branch.{0,500}fast-forwarded') {
        throw 'Current PB-0013 continuation branch interpretation is missing.'
    }
}

Invoke-Check 'PB-0013 changed-file history contains no application implementation' {
    $allowedPattern = '^(AGENTS\.md|docs/.+\.md|scripts/Test-ArchitectureDecisionRecords\.ps1|scripts/Test-QualityAndReleaseGates\.ps1|scripts/Test-RepositoryBaseline\.ps1)$'
    $commitLines = @(Invoke-Git @('log', '--all', '--format=%H%x09%s', '--grep=PB-0013'))
    $commitIds = @(
        'fc34bffff838cac41198940ed54b91b25c33f838',
        'a1032c48f2a8d0dc98d0c589f1a845605950952b'
    )
    foreach ($line in $commitLines) {
        $match = [regex]::Match($line, '^(?<commit>[0-9a-f]{40})\t')
        if ($match.Success -and $match.Groups['commit'].Value -notin $commitIds) {
            $commitIds += $match.Groups['commit'].Value
        }
    }
    foreach ($commit in $commitIds) {
        foreach ($path in @(Invoke-Git @('diff-tree', '--no-commit-id', '--name-only', '-r', $commit))) {
            $normalized = $path.Replace('\', '/')
            if ($normalized -notmatch $allowedPattern) {
                throw "PB-0013 commit $commit contains out-of-scope application path '$normalized'."
            }
        }
    }

    $currentBranch = @(Invoke-Git @('branch', '--show-current'))[0]
    if ($currentBranch -eq 'docs/PB-0013-quality-release-gates') {
        $candidatePaths = @(
            Invoke-Git @('diff', '--name-only', 'HEAD')
            Invoke-Git @('diff', '--cached', '--name-only')
            Invoke-Git @('ls-files', '--others', '--exclude-standard')
        ) | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique
        if (@(Invoke-Git @('show-ref', '--verify', '--quiet', 'refs/heads/main')).Count -ge 0) {
            $mergeBase = @(Invoke-Git @('merge-base', 'main', 'HEAD'))[0]
            $candidatePaths += @(Invoke-Git @('diff', '--name-only', ($mergeBase + '..HEAD'))) |
                ForEach-Object { $_.Replace('\', '/') }
            $candidatePaths = @($candidatePaths | Sort-Object -Unique)
        }
        foreach ($path in $candidatePaths) {
            if ($path -notmatch $allowedPattern) {
                throw "Current PB-0013 scope contains application or unrelated path '$path'."
            }
        }
    }
}

Invoke-Check 'No unresolved placeholders or unsupported quality claims remain' {
    foreach ($name in @(
        'Rules', 'Plan', 'Architecture', 'Backlog', 'Quality',
        'AdrTraceability', 'AdrUx', 'AdrSecurity', 'AdrQuality', 'AdrInstallation',
        'Pb0012Evidence', 'Pb0013Evidence'
    )) {
        foreach ($line in ($texts[$name] -split '\r?\n')) {
            if ($line -match '(?i)\b(TODO|TBD|TBC|FIXME)\b|lorem ipsum|replace me|may\s+(?:initially|temporarily)\s+be\s+placeholder') {
                throw "$name contains unresolved placeholder language: $line"
            }
            if ($line -match '(?i)(best practice|production[ -]ready|production readiness|\bis secure\b|\bis fast\b)') {
                if ($line -notmatch '(?i)(must not|never claim|unsupported|without corresponding|require[sd]? .*evidence|no review may|rejects|does not claim)') {
                    throw "$name contains an unsupported quality claim: $line"
                }
            }
        }
    }
}

Write-Host ''
Write-Host "Quality and release-gate validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Quality and release-gate validation failed.'
}
