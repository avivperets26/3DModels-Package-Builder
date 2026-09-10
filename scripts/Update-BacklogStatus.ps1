# Rebuild or verify totals from canonical task headers; never infer completion from history.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = '',
    [switch]$Write
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}
$path = Join-Path $RepositoryRoot 'docs/IMPLEMENTATION_BACKLOG.md'
$text = [IO.File]::ReadAllText($path)
$counts = @{ BACKLOG = 0; 'IN PROGRESS' = 0; BLOCKED = 0; DONE = 0 }
$icons = @{
    BACKLOG = [char]::ConvertFromUtf32(0x26AA)
    'IN PROGRESS' = [char]::ConvertFromUtf32(0x1F7E1)
    BLOCKED = [char]::ConvertFromUtf32(0x1F534)
    DONE = [char]::ConvertFromUtf32(0x1F7E2)
}
$ids = @{}
$headers = [regex]::Matches($text, '(?m)^- \[(?<checked>[ x])\] \*\*(?<id>PB-\d{4})\b[^\r\n]*')
if ($headers.Count -eq 0) { throw 'No canonical PB task definitions found.' }
foreach ($header in $headers) {
    $id = $header.Groups['id'].Value
    if ($ids.ContainsKey($id)) { throw "Duplicate task definition: $id" }
    $ids[$id] = $true
    $markers = [regex]::Matches($header.Value, '\*\*(BACKLOG|IN PROGRESS|BLOCKED|DONE)\*\*')
    if ($markers.Count -ne 1) { throw "$id must have exactly one canonical status marker." }
    $state = $markers[0].Groups[1].Value
    if (-not $header.Value.Contains($icons[$state] + ' **' + $state + '**')) {
        throw "$id has an incorrect status icon."
    }
    if (($header.Groups['checked'].Value -eq 'x') -ne ($state -eq 'DONE')) {
        throw "$id checkbox and status disagree."
    }
    $counts[$state]++
}
$total = $headers.Count
$done = $counts['DONE']
$percent = (100.0 * $done / $total).ToString('F1', [Globalization.CultureInfo]::InvariantCulture)
$newline = "`n"
if ($text.Contains("`r`n")) { $newline = "`r`n" }
$rows = @(
    '<!-- status-summary:start -->'
    '| Measure | Tasks |'
    '| --- | ---: |'
    "| Total | $total |"
    "| $($icons['BACKLOG']) Backlog | $($counts['BACKLOG']) |"
    "| $($icons['IN PROGRESS']) In progress | $($counts['IN PROGRESS']) |"
    "| $($icons['BLOCKED']) Blocked | $($counts['BLOCKED']) |"
    "| $($icons['DONE']) Done | $done |"
    "| Remaining (all not done) | $($total - $done) |"
    "| Overall completion | $done / $total ($percent%) |"
    '<!-- status-summary:end -->'
)
$pattern = '(?s)<!-- status-summary:start -->.*?<!-- status-summary:end -->'
if ([regex]::Matches($text, '<!-- status-summary:start -->').Count -ne 1 -or
    [regex]::Matches($text, '<!-- status-summary:end -->').Count -ne 1 -or
    [regex]::Matches($text, $pattern).Count -ne 1) {
    throw 'Expected exactly one ordered status-summary marker pair.'
}
$expected = $rows -join $newline
$actual = [regex]::Match($text, $pattern).Value
if ($actual -cne $expected) {
    if (-not $Write) { throw 'Backlog totals are stale. Run scripts/Update-BacklogStatus.ps1 -Write.' }
    $updated = [regex]::Replace($text, $pattern, $expected)
    [IO.File]::WriteAllText($path, $updated, (New-Object System.Text.UTF8Encoding($false)))
}
Write-Host "Backlog verified: $total total; $done done; $($counts['IN PROGRESS']) in progress; $($counts['BLOCKED']) blocked; $($counts['BACKLOG']) backlog; $percent% complete."
