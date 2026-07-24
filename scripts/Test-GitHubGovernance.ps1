[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:FailureCount = 0
$script:PassCount = 0
$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$script:ApprovedRepositoryUrl = 'https://github.com/avivperets26/3DModels-Package-Builder'
$script:ApprovedOwner = '@avivperets26'

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

function Assert-Matches {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Description
    )

    $options = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    if (-not [regex]::IsMatch($Text, $Pattern, $options)) {
        throw "Missing or invalid governance content: $Description."
    }
}

function Assert-EquivalentSets {
    param(
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string[]]$Actual,
        [Parameter(Mandatory)][string]$Description
    )

    $expectedValues = @($Expected | Sort-Object -Unique)
    $actualValues = @($Actual | Sort-Object -Unique)
    $missing = @($expectedValues | Where-Object { $_ -notin $actualValues })
    $unexpected = @($actualValues | Where-Object { $_ -notin $expectedValues })
    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "$Description mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }
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

function ConvertFrom-SimpleYamlScalar {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Value)

    $result = $Value.Trim()
    if ($result.Length -ge 2) {
        $first = $result.Substring(0, 1)
        $last = $result.Substring($result.Length - 1, 1)
        if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
            return $result.Substring(1, $result.Length - 2)
        }
    }
    return $result
}

function Get-IssueTemplateFrontMatter {
    param([Parameter(Mandatory)][string]$RelativePath)

    $text = Read-RepositoryText $RelativePath
    $lines = @($text -split '\r?\n')
    if ($lines.Count -lt 3 -or $lines[0] -cne '---') {
        throw "$RelativePath must begin with YAML front matter."
    }

    $closingIndex = -1
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -ceq '---') {
            $closingIndex = $index
            break
        }
    }
    if ($closingIndex -lt 2) {
        throw "$RelativePath has missing or empty YAML front matter."
    }

    $values = @{}
    for ($index = 1; $index -lt $closingIndex; $index++) {
        $match = [regex]::Match(
            $lines[$index],
            '^(?<key>[A-Za-z][A-Za-z0-9_-]*):\s*(?<value>.*)\s*$'
        )
        if (-not $match.Success) {
            throw "$RelativePath has unsupported front matter at line $($index + 1)."
        }
        $key = $match.Groups['key'].Value
        if ($values.ContainsKey($key)) {
            throw "$RelativePath repeats front-matter key '$key'."
        }
        $values[$key] = ConvertFrom-SimpleYamlScalar $match.Groups['value'].Value
    }

    return [pscustomobject]@{
        Values = $values
        Body = @($lines[($closingIndex + 1)..($lines.Count - 1)]) -join "`n"
    }
}

function Get-HeadingNames {
    param([Parameter(Mandatory)][string]$Text)

    return @($Text -split '\r?\n' | ForEach-Object {
        $match = [regex]::Match($_, '^#{1,6}\s+(?<heading>.+?)\s*$')
        if ($match.Success) {
            $match.Groups['heading'].Value
        }
    })
}

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

$gitRoot = @(Invoke-Git @('rev-parse', '--show-toplevel'))[0]
$gitRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $script:RepositoryRoot)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

$trackedPaths = @(Invoke-Git @('ls-files') | ForEach-Object { $_.Replace('\', '/') })
$candidatePaths = @(
    $trackedPaths
    Invoke-Git @('ls-files', '--others', '--exclude-standard') |
        ForEach-Object { $_.Replace('\', '/') }
) | Sort-Object -Unique

$requiredPaths = @(
    '.github/pull_request_template.md',
    '.github/ISSUE_TEMPLATE/bug_report.md',
    '.github/ISSUE_TEMPLATE/feature_request.md',
    '.github/ISSUE_TEMPLATE/config.yml',
    '.github/CODEOWNERS',
    '.github/dependabot.yml',
    'SECURITY.md',
    'scripts/Test-GitHubGovernance.ps1',
    'docs/PB-0011_GITHUB_GOVERNANCE_EVIDENCE.md'
)

Invoke-Check 'Required governance files use exact supported locations' {
    foreach ($relativePath in $requiredPaths) {
        if ($candidatePaths -cnotcontains $relativePath) {
            throw "$relativePath is missing from the reviewable Git set or uses incorrect casing."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot $relativePath) -PathType Leaf)) {
            throw "$relativePath does not exist."
        }
    }

    $allowedIssueTemplatePaths = @(
        '.github/ISSUE_TEMPLATE/bug_report.md',
        '.github/ISSUE_TEMPLATE/feature_request.md',
        '.github/ISSUE_TEMPLATE/config.yml'
    )
    $unexpectedIssueTemplates = @(
        $candidatePaths |
            Where-Object { $_ -match '^(?i)\.github/ISSUE_TEMPLATE/' } |
            Where-Object { $_ -cnotin $allowedIssueTemplatePaths }
    )
    if ($unexpectedIssueTemplates.Count -gt 0) {
        throw "Unexpected issue template or preview Issue Form: $($unexpectedIssueTemplates -join ', ')"
    }

    $competingLocations = @(
        $candidatePaths | Where-Object {
            ($_ -match '^(?i)(?:docs/)?pull_request_template\.(?:md|txt)$') -or
            ($_ -match '^(?i)\.github/PULL_REQUEST_TEMPLATE/') -or
            ($_ -match '^(?i)(?:CODEOWNERS|docs/CODEOWNERS)$') -or
            ($_ -match '^(?i)(?:\.github|docs)/SECURITY\.md$')
        }
    )
    if ($competingLocations.Count -gt 0) {
        throw "Competing governance location detected: $($competingLocations -join ', ')"
    }
}

Invoke-Check 'Stable Markdown issue templates and chooser configuration are valid' {
    $expectedKeys = @('name', 'about', 'title', 'labels', 'assignees')
    $templates = @(
        [pscustomobject]@{
            Path = '.github/ISSUE_TEMPLATE/bug_report.md'
            Title = '[Bug] '
            Headings = @(
                'Summary',
                'Reproduction Steps',
                'Expected Behavior',
                'Actual Behavior',
                'Environment',
                'Validation and Redacted Diagnostics',
                'Additional Context'
            )
        },
        [pscustomobject]@{
            Path = '.github/ISSUE_TEMPLATE/feature_request.md'
            Title = '[Feature] '
            Headings = @(
                'Problem or Opportunity',
                'Proposed Outcome',
                'Scope',
                'Requirements and Validation',
                'UX, Security, Performance, and Licensing',
                'Alternatives Considered',
                'Additional Context'
            )
        }
    )

    foreach ($template in $templates) {
        $frontMatter = Get-IssueTemplateFrontMatter $template.Path
        Assert-EquivalentSets $expectedKeys @($frontMatter.Values.Keys) "$($template.Path) front-matter keys"
        if ($frontMatter.Values['name'].Length -le 3) {
            throw "$($template.Path) front-matter name must be longer than three characters."
        }
        if ([string]::IsNullOrWhiteSpace($frontMatter.Values['about'])) {
            throw "$($template.Path) front-matter about value is required."
        }
        if ($frontMatter.Values['title'] -cne $template.Title) {
            throw "$($template.Path) has an unexpected title prefix."
        }
        $headings = @(Get-HeadingNames $frontMatter.Body)
        $missingHeadings = @($template.Headings | Where-Object { $_ -notin $headings })
        if ($missingHeadings.Count -gt 0) {
            throw "$($template.Path) is missing headings: $($missingHeadings -join ', ')"
        }
        Assert-Matches $frontMatter.Body 'public repository' "$($template.Path) public-repository warning"
        Assert-Matches $frontMatter.Body 'Do not include vulnerability details, credentials, private keys, private assets, unredacted logs, or personal data' "$($template.Path) sensitive-content warning"
        Assert-Matches $frontMatter.Body ([regex]::Escape($script:ApprovedRepositoryUrl + '/security/policy')) "$($template.Path) security-policy link"
    }

    $config = Read-RepositoryText '.github/ISSUE_TEMPLATE/config.yml'
    if ($config -match "`t") {
        throw 'Issue-template chooser configuration must not contain tabs.'
    }
    Assert-Matches $config '^blank_issues_enabled:\s*false\s*$' 'blank issues disabled'
    Assert-Matches $config '^contact_links:\s*$' 'contact_links collection'
    Assert-Matches $config '^\s{2}- name:\s*Security policy\s*$' 'security policy contact name'
    Assert-Matches $config ('^\s{4}url:\s*' + [regex]::Escape($script:ApprovedRepositoryUrl + '/security/policy') + '\s*$') 'security policy contact URL'
    Assert-Matches $config '^\s{4}about:.*never post vulnerability details or sensitive data publicly\.\s*$' 'security contact safety warning'
    $configLines = @($config -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($configLines.Count -ne 5) {
        throw "Issue-template chooser must contain exactly five non-empty canonical lines; found $($configLines.Count)."
    }
}

Invoke-Check 'Pull-request template covers review and manual publication policy' {
    $pullRequestTemplate = Read-RepositoryText '.github/pull_request_template.md'
    $requiredHeadings = @(
        'Pull Request Review',
        'PB Task or Dependency-Update Identity',
        'Summary and Scope',
        'Requirements and Tests Mapping',
        'Tests and Validation',
        'Documentation Impact',
        'Quality and Public-Repository Checks',
        'Publication Control'
    )
    $headings = @(Get-HeadingNames $pullRequestTemplate)
    $missingHeadings = @($requiredHeadings | Where-Object { $_ -notin $headings })
    if ($missingHeadings.Count -gt 0) {
        throw "Pull-request template is missing headings: $($missingHeadings -join ', ')"
    }

    foreach ($requirement in ([ordered]@{
            'PB or dependency identity' = 'PB task.*Dependabot dependency-update proposal'
            'requirements and tests mapping' = 'Requirement or acceptance criterion.*Test or validation evidence'
            'UX and accessibility' = 'UX and accessibility impact'
            'performance' = 'Performance impact'
            'security' = 'Security and threat-model impact'
            'containment' = 'beneath `C:\\Dev\\PackageBuilder`'
            'licensing' = 'Dependency and licence impact'
            'public repository' = 'No credentials, private keys, personal data, private assets, unredacted logs'
            'optional PRs' = 'Pull requests are optional'
            'no automatic merge or publication' = 'does not enable or authorize automatic merge, publication'
            'manual control' = 'Merge, push, release, and repository-setting changes remain explicit user-controlled actions'
        }).GetEnumerator()) {
        Assert-Matches $pullRequestTemplate $requirement.Value $requirement.Key
    }
}

Invoke-Check 'CODEOWNERS syntax, default owner, and governance ownership are valid' {
    $codeOwners = Read-RepositoryText '.github/CODEOWNERS'
    $entries = @()
    foreach ($line in @($codeOwners -split '\r?\n')) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }
        if ($trimmed.StartsWith('!') -or $trimmed.Contains('[') -or $trimmed.Contains(']') -or
            $trimmed.StartsWith('\#')) {
            throw "Unsupported CODEOWNERS pattern syntax: $trimmed"
        }
        $parts = @([regex]::Split($trimmed, '\s+') | Where-Object { $_ -ne '' })
        if ($parts.Count -lt 2) {
            throw "CODEOWNERS entry has no owner: $trimmed"
        }
        $owners = @($parts[1..($parts.Count - 1)])
        foreach ($owner in $owners) {
            if ($owner -notmatch '^@[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$') {
                throw "Invalid CODEOWNERS owner '$owner'."
            }
        }
        $entries += [pscustomobject]@{
            Pattern = $parts[0]
            Owners = $owners
        }
    }

    $duplicates = @($entries | Group-Object Pattern | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($duplicates.Count -gt 0) {
        throw "Duplicate CODEOWNERS patterns: $($duplicates -join ', ')"
    }
    $defaultEntry = @($entries | Where-Object Pattern -ceq '*')
    $governanceEntry = @($entries | Where-Object Pattern -ceq '/.github/')
    if ($defaultEntry.Count -ne 1 -or $defaultEntry[0].Owners -cnotcontains $script:ApprovedOwner) {
        throw "CODEOWNERS must define '$script:ApprovedOwner' as the default owner."
    }
    if ($governanceEntry.Count -ne 1 -or $governanceEntry[0].Owners -cnotcontains $script:ApprovedOwner) {
        throw "CODEOWNERS must explicitly assign /.github/ to '$script:ApprovedOwner'."
    }
    $defaultIndex = [array]::IndexOf(@($entries | ForEach-Object Pattern), '*')
    $governanceIndex = [array]::IndexOf(@($entries | ForEach-Object Pattern), '/.github/')
    if ($governanceIndex -le $defaultIndex) {
        throw 'The explicit /.github/ CODEOWNERS rule must follow the default rule.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot '.github') -PathType Container)) {
        throw 'The /.github/ CODEOWNERS path does not exist.'
    }
}

Invoke-Check 'Dependabot v2 monitors bounded weekly NuGet and GitHub Actions updates' {
    $dependabot = Read-RepositoryText '.github/dependabot.yml'
    if ($dependabot -match "`t") {
        throw 'Dependabot configuration must not contain tabs.'
    }
    Assert-Matches $dependabot '^version:\s*2\s*$' 'Dependabot version 2'
    Assert-Matches $dependabot '^updates:\s*$' 'Dependabot updates collection'

    $lines = @($dependabot -split '\r?\n')
    $allowedLinePattern = '^(?:' +
        'version:\s*2|' +
        'updates:|' +
        '\s{2}- package-ecosystem:\s*["'']?[a-z0-9-]+["'']?|' +
        '\s{4}(?:directory|target-branch):\s*"[^"]+"|' +
        '\s{4}schedule:|' +
        '\s{4}open-pull-requests-limit:\s*\d+|' +
        '\s{6}(?:interval|day|time|timezone):\s*"[^"]+"' +
        ')\s*$'
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
            continue
        }
        if ($line -notmatch $allowedLinePattern) {
            throw "Dependabot configuration contains an unsupported key or structure: $line"
        }
    }

    $starts = @()
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $match = [regex]::Match(
            $lines[$index],
            '^\s{2}- package-ecosystem:\s*["'']?(?<ecosystem>[a-z0-9-]+)["'']?\s*$'
        )
        if ($match.Success) {
            $starts += [pscustomobject]@{
                Index = $index
                Ecosystem = $match.Groups['ecosystem'].Value
            }
        }
    }
    if ($starts.Count -ne 2) {
        throw "Dependabot must contain exactly two ecosystem entries; found $($starts.Count)."
    }
    Assert-EquivalentSets @('nuget', 'github-actions') @($starts | ForEach-Object Ecosystem) 'Dependabot ecosystems'

    for ($entryIndex = 0; $entryIndex -lt $starts.Count; $entryIndex++) {
        $start = $starts[$entryIndex].Index
        $end = if ($entryIndex + 1 -lt $starts.Count) {
            $starts[$entryIndex + 1].Index - 1
        }
        else {
            $lines.Count - 1
        }
        $block = @($lines[$start..$end]) -join "`n"
        Assert-Matches $block '^\s{4}directory:\s*"/"\s*$' "$($starts[$entryIndex].Ecosystem) root directory"
        Assert-Matches $block '^\s{4}schedule:\s*$' "$($starts[$entryIndex].Ecosystem) schedule"
        Assert-Matches $block '^\s{6}interval:\s*"weekly"\s*$' "$($starts[$entryIndex].Ecosystem) weekly interval"
        Assert-Matches $block '^\s{4}target-branch:\s*"main"\s*$' "$($starts[$entryIndex].Ecosystem) main target"
        $limitMatch = [regex]::Match(
            $block,
            '(?m)^\s{4}open-pull-requests-limit:\s*(?<limit>\d+)\s*$'
        )
        if (-not $limitMatch.Success) {
            throw "$($starts[$entryIndex].Ecosystem) is missing open-pull-requests-limit."
        }
        $limit = [int]$limitMatch.Groups['limit'].Value
        if ($limit -lt 1 -or $limit -gt 5) {
            throw "$($starts[$entryIndex].Ecosystem) open-pull-requests-limit must be between 1 and 5."
        }
    }

    $prohibitedDependabot = '(?im)^\s*(?:registries|insecure-external-code-execution):|(?:\$\{\{)|\b(?:password|token|secret|username|automerge|auto-merge|merge-method)\b'
    if ($dependabot -match $prohibitedDependabot) {
        throw 'Dependabot configuration contains a registry, credential, automerge, or unsupported unsafe option.'
    }
}

Invoke-Check 'Secret-scanning exclusions and competing dependency bots are absent' {
    $secretScanningPaths = @(
        '.github/secret_scanning.yml',
        '.github/secret_scanning.yaml'
    )
    foreach ($relativePath in $secretScanningPaths) {
        if (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot $relativePath)) {
            throw "Secret-scanning exclusion file must be absent: $relativePath"
        }
    }
    $reviewableExclusions = @(
        $candidatePaths | Where-Object { $_ -match '(?i)(^|/)secret_scanning\.ya?ml$' }
    )
    if ($reviewableExclusions.Count -gt 0) {
        throw "Secret-scanning exclusion file is reviewable: $($reviewableExclusions -join ', ')"
    }

    $renovatePaths = @(
        $candidatePaths | Where-Object {
            $_ -match '(?i)(^|/)(?:renovate\.json5?|\.renovaterc(?:\.json5?)?|renovate-config\.(?:js|cjs|mjs))$'
        }
    )
    if ($renovatePaths.Count -gt 0) {
        throw "Renovate configuration is prohibited while Dependabot is selected: $($renovatePaths -join ', ')"
    }
}

Invoke-Check 'Security policy states the current safe-reporting limitation' {
    $security = Read-RepositoryText 'SECURITY.md'
    $requiredHeadings = @(
        'Security Policy',
        'Supported Versions',
        'Reporting a Vulnerability',
        'Current Reporting Limitation',
        'Scope of This Policy'
    )
    $headings = @(Get-HeadingNames $security)
    $missingHeadings = @($requiredHeadings | Where-Object { $_ -notin $headings })
    if ($missingHeadings.Count -gt 0) {
        throw "SECURITY.md is missing headings: $($missingHeadings -join ', ')"
    }
    Assert-Matches $security 'Do not post vulnerability details, credentials, private keys, sensitive assets, unredacted logs, personal data' 'public vulnerability-disclosure prohibition'
    Assert-Matches $security 'No private vulnerability-reporting channel or private contact address is documented or verified' 'unverified private reporting limitation'
    Assert-Matches $security 'PB-0011 does not change GitHub repository settings' 'no settings change'
    Assert-Matches $security 'minimal public issue asking the maintainer to establish a private contact method.*include no vulnerability details or sensitive data' 'safe contact-establishment fallback'
    Assert-Matches $security 'full triage, response-target, emergency-patch, and disclosure procedure remains assigned to the later' 'later full procedure'
    if ($security -match '(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b') {
        throw 'SECURITY.md must not invent or publish an email reporting address.'
    }
    if ($security -match '(?i)private vulnerability reporting (?:is|has been) enabled') {
        throw 'SECURITY.md makes an unverified private-vulnerability-reporting claim.'
    }
}

Invoke-Check 'Governance content has no credential, private-path, or unsupported capability example' {
    $contentPaths = @(
        '.github/pull_request_template.md',
        '.github/ISSUE_TEMPLATE/bug_report.md',
        '.github/ISSUE_TEMPLATE/feature_request.md',
        '.github/ISSUE_TEMPLATE/config.yml',
        '.github/CODEOWNERS',
        '.github/dependabot.yml',
        'SECURITY.md'
    )
    $secretPatterns = @(
        '-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----',
        '\bgh[pousr]_[A-Za-z0-9]{36,}\b',
        '\bAKIA[0-9A-Z]{16}\b',
        '\bxox[baprs]-[A-Za-z0-9-]{10,}\b',
        '\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}\b',
        '\bAIza[0-9A-Za-z_-]{35}\b',
        '\bsk_live_[0-9A-Za-z]{16,}\b',
        '(?i)\b(password|passwd|api[_-]?key|client[_-]?secret)\s*[:=]\s*["''][^"'']{8,}["'']'
    )
    $personalPathPatterns = @(
        '(?i)\b[A-Z]:[\\/]Users[\\/](?!Public(?:[\\/]|$)|Default(?: User)?(?:[\\/]|$))[^\\/\s]+',
        '(?i)(?<![A-Za-z0-9_])/(?:home|Users)/[A-Za-z0-9._-]+'
    )

    foreach ($relativePath in $contentPaths) {
        $text = Read-RepositoryText $relativePath
        foreach ($pattern in $secretPatterns) {
            if ($text -match $pattern) {
                throw "$relativePath contains a credential-shaped example."
            }
        }
        foreach ($pattern in $personalPathPatterns) {
            if ($text -match $pattern) {
                throw "$relativePath contains a personal filesystem path."
            }
        }
        if ($text -match '(?i)pull requests?\s+(?:are|is)\s+(?:required|mandatory)' -or
            $text -match '(?i)must\s+(?:open|use)\s+(?:a\s+)?pull request') {
            throw "$relativePath incorrectly makes pull requests mandatory."
        }
        if ($text -match '(?i)(?:automatic|auto)[ -]?(?:merge|publication)\s+(?:is|has been)\s+enabled') {
            throw "$relativePath claims unsupported automatic merge or publication."
        }
    }
}

Invoke-Check 'PB-0011 evidence and lifecycle state remain active and consistent' {
    $evidence = Read-RepositoryText 'docs/PB-0011_GITHUB_GOVERNANCE_EVIDENCE.md'
    Assert-Matches $evidence '\*\*Official documentation review date:\*\*\s*2026-07-24' 'official documentation review date'
    Assert-Matches $evidence 'public repositories.*secret scanning runs automatically for free' 'public secret-scanning behavior'
    Assert-Matches $evidence 'secret_scanning\.yml.*only.*exclude' 'secret_scanning.yml exclusion-only behavior'
    Assert-Matches $evidence 'PB-1611.*pinned local and CI.*dependency.*licence.*vulnerability.*secret' 'PB-1611 boundary'
    Assert-Matches $evidence 'No GitHub repository setting was changed' 'external settings boundary'

    $backlog = Read-RepositoryText 'docs/IMPLEMENTATION_BACKLOG.md'
    Assert-Matches $backlog '- \[ \] \*\*PB-0011\b.*' 'PB-0011 unchecked task'
    $processMarker = [char]::ConvertFromUtf32(0x1F7E1) + ' **PROCESS**'
    Assert-Matches $backlog (
        '- \[ \] \*\*PB-0011\b[^\r\n]*' + [regex]::Escape($processMarker)
    ) 'PB-0011 PROCESS lifecycle'
    $activeRows = @([regex]::Matches($backlog, '(?m)^\|\s*PB-0011\s*\|'))
    if ($activeRows.Count -ne 1) {
        throw "PB-0011 must appear exactly once in Active Work; found $($activeRows.Count)."
    }

    $completionStart = $backlog.IndexOf('## 4. Completion Log', [System.StringComparison]::Ordinal)
    $completionEnd = $backlog.IndexOf('## 5. Milestones', [System.StringComparison]::Ordinal)
    if ($completionStart -lt 0 -or $completionEnd -le $completionStart) {
        throw 'Completion Log boundaries were not found.'
    }
    $completionLog = $backlog.Substring($completionStart, $completionEnd - $completionStart)
    if ($completionLog -match '(?m)^\|\s*PB-0011\s*\|') {
        throw 'PB-0011 must not be in the Completion Log on its task branch.'
    }
}

Write-Host ''
Write-Host "GitHub governance validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'GitHub governance validation failed.'
}
