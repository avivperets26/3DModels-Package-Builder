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

function Get-MarkdownLinkTargets {
    param([Parameter(Mandatory)][string]$Text)

    # Inline links are the repository's supported documentation form; external schemes and
    # same-page anchors are filtered by the caller when filesystem resolution is required.
    $pattern = '!?\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
    return @([regex]::Matches($Text, $pattern) | ForEach-Object {
        $_.Groups['target'].Value.Trim([char[]]'<>')
    })
}

function Get-LocalLinkPath {
    param([Parameter(Mandatory)][string]$Target)

    if ($Target.StartsWith('#') -or $Target.StartsWith('//') -or
        $Target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
        return $null
    }

    $pathPart = ($Target -split '#', 2)[0]
    $pathPart = ($pathPart -split '\?', 2)[0]
    if ([string]::IsNullOrWhiteSpace($pathPart)) {
        return $null
    }

    return [System.Uri]::UnescapeDataString($pathPart).Replace('\', '/')
}

function Assert-Pattern {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Description
    )

    $options = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::Multiline -bor
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    if (-not [regex]::IsMatch($Text, $Pattern, $options)) {
        throw "Missing or inconsistent ADR policy: $Description."
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

# Include untracked, non-ignored files so the complete task can be validated before the user stages it.
$candidatePaths = @(
    Invoke-Git @('ls-files')
    Invoke-Git @('ls-files', '--others', '--exclude-standard')
) | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique

$expectedAdrs = @(
    [pscustomobject]@{ Number = '0001'; File = 'ADR-0001-dotnet-10-and-wpf.md'; Title = '.NET 10 LTS and WPF' },
    [pscustomobject]@{ Number = '0002'; File = 'ADR-0002-external-engine-workers.md'; Title = 'External Engine Workers' },
    [pscustomobject]@{ Number = '0003'; File = 'ADR-0003-json-file-worker-protocol.md'; Title = 'JSON File Worker Protocol' },
    [pscustomobject]@{ Number = '0004'; File = 'ADR-0004-immutable-staging-and-atomic-promotion.md'; Title = 'Immutable Staging and Atomic Promotion' },
    [pscustomobject]@{ Number = '0005'; File = 'ADR-0005-latest-approved-stable-engine-policy.md'; Title = 'Latest Approved Stable Engine Policy' },
    [pscustomobject]@{ Number = '0006'; File = 'ADR-0006-sqlite-build-history.md'; Title = 'SQLite Build History' },
    [pscustomobject]@{ Number = '0007'; File = 'ADR-0007-compiled-in-adapters-for-v1.md'; Title = 'Compiled-in Adapters for Version 1' },
    [pscustomobject]@{ Number = '0008'; File = 'ADR-0008-marketplace-requirements-profiles.md'; Title = 'Marketplace Requirements Profiles' },
    [pscustomobject]@{ Number = '0009'; File = 'ADR-0009-requirements-traceability-and-release-evidence.md'; Title = 'Requirements Traceability and Release Evidence' },
    [pscustomobject]@{ Number = '0010'; File = 'ADR-0010-accessible-guided-dry-run-workflow.md'; Title = 'Accessible Guided Dry-run Workflow' },
    [pscustomobject]@{ Number = '0011'; File = 'ADR-0011-threat-model-secrets-and-network-consent.md'; Title = 'Threat Model, Secrets, and Network Consent' },
    [pscustomobject]@{ Number = '0012'; File = 'ADR-0012-quality-toolchain-and-thresholds.md'; Title = 'Quality Toolchain and Thresholds' },
    [pscustomobject]@{ Number = '0013'; File = 'ADR-0013-installer-portable-and-lifecycle-safety.md'; Title = 'Installer, Portable Distribution, and Lifecycle Safety' }
)

$requiredSections = @(
    'Status',
    'Date',
    'Context',
    'Decision',
    'Alternatives Considered',
    'Consequences and Trade-offs',
    'Migration or Evolution Considerations',
    'Implementation Status and Follow-up Work',
    'Related Documentation'
)
$validStatuses = @('Proposed', 'Accepted', 'Deprecated', 'Superseded')
$adrRoot = Join-Path $script:RepositoryRoot 'docs\adr'
$implementationBoundary = 'Acceptance records the architecture direction; it does not indicate that implementation is complete.'

Invoke-Check 'ADR inventory is exact, unique, and sequential' {
    if (-not (Test-Path -LiteralPath $adrRoot -PathType Container)) {
        throw 'docs/adr does not exist.'
    }

    $actualFiles = @(Get-ChildItem -LiteralPath $adrRoot -Filter 'ADR-*.md' -File | ForEach-Object Name)
    $expectedFiles = @($expectedAdrs | ForEach-Object File)
    $missing = @($expectedFiles | Where-Object { $_ -cnotin $actualFiles })
    $unexpected = @($actualFiles | Where-Object { $_ -cnotin $expectedFiles })
    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0) {
        throw "ADR file inventory mismatch. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', ')."
    }

    $numberMatches = @($actualFiles | ForEach-Object {
        $match = [regex]::Match($_, '^ADR-(?<number>\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$')
        if (-not $match.Success) {
            throw "Invalid ADR filename: $_"
        }
        $match.Groups['number'].Value
    })
    $duplicates = @($numberMatches | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    $expectedNumbers = @($expectedAdrs | ForEach-Object Number)
    if ($duplicates.Count -gt 0 -or @(Compare-Object $expectedNumbers $numberMatches).Count -gt 0) {
        throw "ADR numbers are duplicate, unexpected, or non-sequential. Duplicate: $($duplicates -join ', ')."
    }

    foreach ($adr in $expectedAdrs) {
        $relativePath = 'docs/adr/' + $adr.File
        if ($candidatePaths -cnotcontains $relativePath) {
            throw "$relativePath is absent from the reviewable Git set or has incorrect casing."
        }
    }
}

Invoke-Check 'ADR titles, sections, statuses, and dates are valid' {
    foreach ($adr in $expectedAdrs) {
        $relativePath = 'docs/adr/' + $adr.File
        $text = Read-RepositoryText $relativePath
        $expectedTitle = '# ADR-' + $adr.Number + ': ' + $adr.Title
        $firstLine = @($text -split '\r?\n')[0]
        if ($firstLine -cne $expectedTitle) {
            throw "$relativePath must start with '$expectedTitle'."
        }

        $previousIndex = -1
        foreach ($section in $requiredSections) {
            $heading = '## ' + $section
            $matches = @([regex]::Matches($text, '(?m)^' + [regex]::Escape($heading) + '\s*$'))
            if ($matches.Count -ne 1) {
                throw "$relativePath must contain exactly one '$heading' heading."
            }
            if ($matches[0].Index -le $previousIndex) {
                throw "$relativePath has required sections out of order."
            }
            $previousIndex = $matches[0].Index
            if ([string]::IsNullOrWhiteSpace((Get-SectionBody $text $section))) {
                throw "$relativePath has an empty '$section' section."
            }
        }

        $status = Get-SectionBody $text 'Status'
        if ($status -cnotin $validStatuses) {
            throw "$relativePath has invalid status '$status'."
        }

        $dateText = Get-SectionBody $text 'Date'
        $parsedDate = [datetime]::MinValue
        $dateValid = [datetime]::TryParseExact(
            $dateText,
            'yyyy-MM-dd',
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$parsedDate
        )
        if (-not $dateValid) {
            throw "$relativePath has invalid ISO date '$dateText'."
        }
    }
}

Invoke-Check 'ADRs contain no placeholders and separate decisions from implementation' {
    foreach ($adr in $expectedAdrs) {
        $relativePath = 'docs/adr/' + $adr.File
        $text = Read-RepositoryText $relativePath
        if ($text -match '(?i)\b(?:TODO|TBD|FIXME|XXX|PLACEHOLDER)\b|lorem\s+ipsum|\{\{[^}]+\}\}') {
            throw "$relativePath contains placeholder content."
        }
        if (-not $text.Contains($implementationBoundary)) {
            throw "$relativePath does not contain the required implementation-completion distinction."
        }

        $relatedBody = Get-SectionBody $text 'Related Documentation'
        $localRepositoryLinks = @(Get-MarkdownLinkTargets $relatedBody | ForEach-Object {
            Get-LocalLinkPath $_
        } | Where-Object { $null -ne $_ })
        if ($localRepositoryLinks.Count -lt 2) {
            throw "$relativePath must link at least two relevant repository documents."
        }
    }
}

Invoke-Check 'All local Markdown links resolve inside the repository' {
    foreach ($relativePath in @($candidatePaths | Where-Object { $_ -match '(?i)\.md$' })) {
        $text = Read-RepositoryText $relativePath
        foreach ($target in Get-MarkdownLinkTargets $text) {
            $localPath = Get-LocalLinkPath $target
            if ($null -eq $localPath) {
                continue
            }
            if ([System.IO.Path]::IsPathRooted($localPath)) {
                throw "$relativePath contains rooted local link '$target'."
            }

            $sourceDirectory = Split-Path (Join-Path $script:RepositoryRoot $relativePath) -Parent
            $nativePath = $localPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $sourceDirectory $nativePath))
            if (-not (Test-ContainedPath $resolvedPath)) {
                throw "$relativePath contains a link outside the repository: '$target'."
            }
            if (-not (Test-Path -LiteralPath $resolvedPath)) {
                throw "$relativePath contains a missing local link '$target'."
            }
        }
    }
}

Invoke-Check 'Documentation and ADR indexes link the exact ADR inventory' {
    $indexCases = @(
        [pscustomobject]@{ Path = 'docs/README.md'; Prefix = 'adr/' },
        [pscustomobject]@{ Path = 'docs/adr/README.md'; Prefix = '' }
    )
    foreach ($indexCase in $indexCases) {
        $text = Read-RepositoryText $indexCase.Path
        $targets = @(Get-MarkdownLinkTargets $text | ForEach-Object {
            Get-LocalLinkPath $_
        } | Where-Object { $null -ne $_ -and $_ -match '(^|/)ADR-\d{4}-.*\.md$' })

        foreach ($adr in $expectedAdrs) {
            $expectedTarget = $indexCase.Prefix + $adr.File
            $count = @($targets | Where-Object { $_ -ceq $expectedTarget }).Count
            if ($count -ne 1) {
                throw "$($indexCase.Path) must link $expectedTarget exactly once; found $count."
            }
        }
        if ($targets.Count -ne $expectedAdrs.Count) {
            throw "$($indexCase.Path) contains an unexpected or duplicate ADR link."
        }
    }

    $rootReadme = Read-RepositoryText 'README.md'
    if (@(Get-MarkdownLinkTargets $rootReadme | Where-Object { $_ -ceq 'docs/README.md' }).Count -ne 1) {
        throw 'README.md must link docs/README.md exactly once.'
    }
}

Invoke-Check 'Architecture section 29 links the exact ADR inventory' {
    $architecture = Read-RepositoryText 'docs/TECH_STACK_AND_ARCHITECTURE.md'
    # Bound the inventory to section 29 so a coincidental ADR link elsewhere cannot satisfy it.
    $sectionMatch = [regex]::Match(
        $architecture,
        '(?ms)^## 29\. Architecture Decision Records\s*(?<body>.*?)(?=^## 30\.)'
    )
    if (-not $sectionMatch.Success) {
        throw 'Architecture section 29 boundaries were not found.'
    }

    $targets = @(Get-MarkdownLinkTargets $sectionMatch.Groups['body'].Value | ForEach-Object {
        Get-LocalLinkPath $_
    } | Where-Object { $null -ne $_ -and $_ -match '^adr/ADR-\d{4}-.*\.md$' })
    foreach ($adr in $expectedAdrs) {
        $expectedTarget = 'adr/' + $adr.File
        $count = @($targets | Where-Object { $_ -ceq $expectedTarget }).Count
        if ($count -ne 1) {
            throw "Architecture section 29 must link $expectedTarget exactly once; found $count."
        }
    }
    if ($targets.Count -ne $expectedAdrs.Count) {
        throw 'Architecture section 29 contains an unexpected or duplicate ADR link.'
    }
}

Invoke-Check 'ADRs preserve permanent architecture and repository policies' {
    # These focused assertions protect the approved cross-cutting boundaries without
    # attempting to replace the more detailed product, quality, and backlog validators.
    $policyRequirements = @(
        [pscustomobject]@{ File = 'ADR-0001-dotnet-10-and-wpf.md'; Pattern = '\.NET 10 LTS'; Description = '.NET 10 LTS selection' },
        [pscustomobject]@{ File = 'ADR-0001-dotnet-10-and-wpf.md'; Pattern = '\bWPF\b'; Description = 'WPF selection' },
        [pscustomobject]@{ File = 'ADR-0001-dotnet-10-and-wpf.md'; Pattern = 'Visual Studio Code.*Paid Visual Studio.*not required'; Description = 'free Visual Studio Code workflow' },
        [pscustomobject]@{ File = 'ADR-0002-external-engine-workers.md'; Pattern = 'external engine workers'; Description = 'external worker boundary' },
        [pscustomobject]@{ File = 'ADR-0002-external-engine-workers.md'; Pattern = 'licensing.*eligibility.*seat.*royalty'; Description = 'engine licensing disclosure' },
        [pscustomobject]@{ File = 'ADR-0002-external-engine-workers.md'; Pattern = 'must not automate editor mouse clicks'; Description = 'native API rather than editor-click automation' },
        [pscustomobject]@{ File = 'ADR-0004-immutable-staging-and-atomic-promotion.md'; Pattern = 'C:\\Dev\\PackageBuilder'; Description = 'single-root containment' },
        [pscustomobject]@{ File = 'ADR-0004-immutable-staging-and-atomic-promotion.md'; Pattern = 'immutable.*atomically'; Description = 'immutable staging and atomic promotion' },
        [pscustomobject]@{ File = 'ADR-0005-latest-approved-stable-engine-policy.md'; Pattern = 'Latest Approved Stable'; Description = 'engine version policy' },
        [pscustomobject]@{ File = 'ADR-0005-latest-approved-stable-engine-policy.md'; Pattern = 'Exclude.*preview'; Description = 'preview exclusion' },
        [pscustomobject]@{ File = 'ADR-0005-latest-approved-stable-engine-policy.md'; Pattern = 'Last Known Good'; Description = 'fallback policy' },
        [pscustomobject]@{ File = 'ADR-0007-compiled-in-adapters-for-v1.md'; Pattern = 'compile.*version 1 adapters'; Description = 'compiled-in adapter selection' },
        [pscustomobject]@{ File = 'ADR-0007-compiled-in-adapters-for-v1.md'; Pattern = 'Do not load arbitrary third-party DLL'; Description = 'deferred arbitrary plugin loading' },
        [pscustomobject]@{ File = 'ADR-0007-compiled-in-adapters-for-v1.md'; Pattern = 'Publisher names.*configuration'; Description = 'configurable publisher identity' },
        [pscustomobject]@{ File = 'ADR-0008-marketplace-requirements-profiles.md'; Pattern = 'independently versioned profiles'; Description = 'marketplace profile versioning' },
        [pscustomobject]@{ File = 'ADR-0008-marketplace-requirements-profiles.md'; Pattern = 'Fab is the first'; Description = 'Fab-first marketplace independence' },
        [pscustomobject]@{ File = 'ADR-0008-marketplace-requirements-profiles.md'; Pattern = 'manual action|manual publishing'; Description = 'manual marketplace publication' },
        [pscustomobject]@{ File = 'ADR-0009-requirements-traceability-and-release-evidence.md'; Pattern = 'Every normative requirement.*every PB acceptance criterion'; Description = 'criterion-level traceability' },
        [pscustomobject]@{ File = 'ADR-0009-requirements-traceability-and-release-evidence.md'; Pattern = 'fail-closed'; Description = 'fail-closed evidence' },
        [pscustomobject]@{ File = 'ADR-0009-requirements-traceability-and-release-evidence.md'; Pattern = 'cannot commit.*push.*merge.*publish'; Description = 'manual Git and publication control' },
        [pscustomobject]@{ File = 'ADR-0010-accessible-guided-dry-run-workflow.md'; Pattern = 'keyboard-only.*screen readers.*high contrast.*scalable text'; Description = 'accessibility requirements' },
        [pscustomobject]@{ File = 'ADR-0010-accessible-guided-dry-run-workflow.md'; Pattern = 'dry run.*without changing sources'; Description = 'side-effect-free dry run' },
        [pscustomobject]@{ File = 'ADR-0010-accessible-guided-dry-run-workflow.md'; Pattern = 'progress.*elapsed time.*cancellation'; Description = 'progress and cancellation' },
        [pscustomobject]@{ File = 'ADR-0011-threat-model-secrets-and-network-consent.md'; Pattern = 'approved repository is public'; Description = 'public-repository boundary' },
        [pscustomobject]@{ File = 'ADR-0011-threat-model-secrets-and-network-consent.md'; Pattern = 'Never place tokens, credentials, private keys'; Description = 'secret prohibition' },
        [pscustomobject]@{ File = 'ADR-0011-threat-model-secrets-and-network-consent.md'; Pattern = 'explicit user consent.*offline behavior'; Description = 'network consent and offline behavior' },
        [pscustomobject]@{ File = 'ADR-0012-quality-toolchain-and-thresholds.md'; Pattern = '90% line coverage.*85% branch coverage'; Description = 'overall coverage thresholds' },
        [pscustomobject]@{ File = 'ADR-0012-quality-toolchain-and-thresholds.md'; Pattern = '100% branch coverage.*security validation.*path handling.*naming.*manifest validation.*package-integrity'; Description = 'critical coverage threshold' },
        [pscustomobject]@{ File = 'ADR-0012-quality-toolchain-and-thresholds.md'; Pattern = 'free local or self-hosted'; Description = 'free quality toolchain' },
        [pscustomobject]@{ File = 'ADR-0012-quality-toolchain-and-thresholds.md'; Pattern = 'small, medium, and large fixtures'; Description = 'performance fixture classes' },
        [pscustomobject]@{ File = 'ADR-0013-installer-portable-and-lifecycle-safety.md'; Pattern = 'deferring installer and update technology selection to PB-1612'; Description = 'installer selection deferral' },
        [pscustomobject]@{ File = 'ADR-0013-installer-portable-and-lifecycle-safety.md'; Pattern = 'No installer or update technology is selected by this ADR'; Description = 'no premature installer choice' },
        [pscustomobject]@{ File = 'ADR-0013-installer-portable-and-lifecycle-safety.md'; Pattern = 'portable distribution.*technically practical'; Description = 'portable distribution requirement' },
        [pscustomobject]@{ File = 'ADR-0013-installer-portable-and-lifecycle-safety.md'; Pattern = 'Preserve user projects, source assets, generated packages, release artifacts'; Description = 'lifecycle data preservation' }
    )

    $allAdrText = ''
    foreach ($adr in $expectedAdrs) {
        $allAdrText += Read-RepositoryText ('docs/adr/' + $adr.File)
        $allAdrText += "`n"
    }
    if ($allAdrText -match '(?i)C:\\Dev\\PackageBuilderData') {
        throw 'An ADR references the prohibited sibling data root.'
    }

    foreach ($requirement in $policyRequirements) {
        $text = Read-RepositoryText ('docs/adr/' + $requirement.File)
        Assert-Pattern $text $requirement.Pattern $requirement.Description
    }
}

Invoke-Check 'PB-0011 rollover and PB-0012 lifecycle evidence are consistent' {
    $backlog = Read-RepositoryText 'docs/IMPLEMENTATION_BACKLOG.md'
    $doneMarker = [char]::ConvertFromUtf32(0x1F7E2) + ' **DONE**'
    $processMarker = [char]::ConvertFromUtf32(0x1F7E1) + ' **PROCESS**'
    Assert-Pattern $backlog (
        '- \[x\] \*\*PB-0011\b[^\r\n]*' + [regex]::Escape($doneMarker)
    ) 'PB-0011 completed task'
    Assert-Pattern $backlog (
        '- \[ \] \*\*PB-0012\b[^\r\n]*' + [regex]::Escape($processMarker)
    ) 'PB-0012 active task'

    $activeStart = $backlog.IndexOf('## 3. Active Work', [System.StringComparison]::Ordinal)
    $activeEnd = $backlog.IndexOf('## 4. Completion Log', [System.StringComparison]::Ordinal)
    $completionEnd = $backlog.IndexOf('## 5. Milestones', [System.StringComparison]::Ordinal)
    if ($activeStart -lt 0 -or $activeEnd -le $activeStart -or $completionEnd -le $activeEnd) {
        throw 'Backlog lifecycle boundaries were not found.'
    }
    $activeSection = $backlog.Substring($activeStart, $activeEnd - $activeStart)
    $completionSection = $backlog.Substring($activeEnd, $completionEnd - $activeEnd)
    if (@([regex]::Matches($activeSection, '(?m)^\|\s*PB-0011\s*\|')).Count -ne 0) {
        throw 'PB-0011 must be absent from Active Work.'
    }
    if (@([regex]::Matches($activeSection, '(?m)^\|\s*PB-0012\s*\|')).Count -ne 1) {
        throw 'PB-0012 must appear exactly once in Active Work.'
    }
    if (@([regex]::Matches($completionSection, '(?m)^\|\s*PB-0011\s*\|')).Count -ne 1) {
        throw 'PB-0011 must appear exactly once in the Completion Log.'
    }
    if (@([regex]::Matches($completionSection, '(?m)^\|\s*PB-0012\s*\|')).Count -ne 0) {
        throw 'PB-0012 must not appear in the Completion Log on its task branch.'
    }

    $evidence = Read-RepositoryText 'docs/PB-0012_INITIAL_ADRS_EVIDENCE.md'
    Assert-Pattern $evidence 'Acceptance records the architecture direction; it does not indicate that implementation is complete' 'PB-0012 implementation boundary'
    Assert-Pattern $evidence 'installer technology remains deferred to PB-1612' 'PB-1612 boundary'
    Assert-Pattern $evidence '02491ce01e32559c2b41ce886f5595c286677555.*pull request #12.*5b37b3c8081d246c03eabe8dc3099b1a99f31ca1.*30080298582.*30080304495' 'PB-0011 rollover evidence'
}

Write-Host ''
Write-Host "Architecture decision record validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Architecture decision record validation failed.'
}
