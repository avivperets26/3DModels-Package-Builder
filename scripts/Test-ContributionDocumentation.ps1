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

function Assert-Matches {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Pattern,
        [Parameter(Mandatory)][string]$Description
    )

    if (-not [regex]::IsMatch(
            $Text,
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
            [System.Text.RegularExpressions.RegexOptions]::Multiline
        )) {
        throw "Missing required documentation statement: $Description."
    }
}

function Get-HeadingNames {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines)

    return @($Lines | ForEach-Object {
        $match = [regex]::Match($_, '^#{1,6}\s+(?<name>.+?)\s*$')
        if ($match.Success) {
            $match.Groups['name'].Value
        }
    })
}

function Get-HeadingSection {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory)][string]$Heading
    )

    $start = -1
    $level = 0
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        $match = [regex]::Match($Lines[$index], '^(?<marks>#{1,6})\s+(?<name>.+?)\s*$')
        if ($match.Success -and $match.Groups['name'].Value -ceq $Heading) {
            $start = $index
            $level = $match.Groups['marks'].Value.Length
            break
        }
    }

    if ($start -lt 0) {
        throw "Heading '$Heading' was not found."
    }

    $end = $Lines.Count
    for ($index = $start + 1; $index -lt $Lines.Count; $index++) {
        $match = [regex]::Match($Lines[$index], '^(?<marks>#{1,6})\s+\S')
        if ($match.Success -and $match.Groups['marks'].Value.Length -le $level) {
            $end = $index
            break
        }
    }

    if ($end -le ($start + 1)) {
        return @()
    }

    return @($Lines[($start + 1)..($end - 1)])
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

function Test-LocalMarkdownLinks {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines
    )

    $fullPath = Join-Path $script:RepositoryRoot $RelativePath
    foreach ($line in $Lines) {
        foreach ($link in [regex]::Matches(
                $line,
                '!?' + '\[[^\]]*\]\((?<target><[^>]+>|[^)\s]+)(?:\s+["''][^"'']*["''])?\)'
            )) {
            $target = $link.Groups['target'].Value.Trim([char[]]'<>')
            if ($target.StartsWith('#') -or $target.StartsWith('//') -or
                $target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
                continue
            }

            $pathPart = ($target -split '#', 2)[0]
            $pathPart = ($pathPart -split '\?', 2)[0]
            if ([string]::IsNullOrWhiteSpace($pathPart)) {
                continue
            }

            $decodedPath = [System.Uri]::UnescapeDataString($pathPart).Replace(
                '/',
                [System.IO.Path]::DirectorySeparatorChar
            )
            if ([System.IO.Path]::IsPathRooted($decodedPath)) {
                throw "$RelativePath contains a rooted local Markdown link: $target"
            }

            $targetPath = [System.IO.Path]::GetFullPath(
                (Join-Path (Split-Path $fullPath -Parent) $decodedPath)
            )
            $rootPrefix = $script:RepositoryRoot + [System.IO.Path]::DirectorySeparatorChar
            if (-not $targetPath.StartsWith(
                    $rootPrefix,
                    [System.StringComparison]::OrdinalIgnoreCase
                ) -and -not [System.StringComparer]::OrdinalIgnoreCase.Equals(
                    $targetPath,
                    $script:RepositoryRoot
                )) {
                throw "$RelativePath contains a Markdown link outside the repository: $target"
            }
            if (-not (Test-Path -LiteralPath $targetPath)) {
                throw "$RelativePath contains a missing local Markdown link: $target"
            }
        }
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

$trackedPaths = @(Invoke-Git @('ls-files') | ForEach-Object { $_.Replace('\', '/') })
$candidatePaths = @(
    $trackedPaths
    Invoke-Git @('ls-files', '--others', '--exclude-standard') |
        ForEach-Object { $_.Replace('\', '/') }
) | Sort-Object -Unique
$candidateLookup = @{}
foreach ($path in $candidatePaths) {
    $candidateLookup[$path.ToLowerInvariant()] = $true
}

$readmePath = Join-Path $script:RepositoryRoot 'README.md'
$contributingPath = Join-Path $script:RepositoryRoot 'CONTRIBUTING.md'
$agentsPath = Join-Path $script:RepositoryRoot 'AGENTS.md'
$backlogPath = Join-Path $script:RepositoryRoot 'docs\IMPLEMENTATION_BACKLOG.md'

Invoke-Check 'README and CONTRIBUTING exist in the reviewable Git set' {
    foreach ($relativePath in @('README.md', 'CONTRIBUTING.md')) {
        if (-not $candidateLookup.ContainsKey($relativePath.ToLowerInvariant())) {
            throw "$relativePath is neither tracked nor an unignored reviewable file."
        }
        if (-not (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot $relativePath) -PathType Leaf)) {
            throw "$relativePath does not exist."
        }
    }
}

$readmeLines = @(Get-Content -LiteralPath $readmePath -Encoding UTF8)
$contributingLines = @(Get-Content -LiteralPath $contributingPath -Encoding UTF8)
$agentsLines = @(Get-Content -LiteralPath $agentsPath -Encoding UTF8)
$backlogLines = @(Get-Content -LiteralPath $backlogPath -Encoding UTF8)
$readme = $readmeLines -join "`n"
$contributing = $contributingLines -join "`n"
$agents = $agentsLines -join "`n"
$backlog = $backlogLines -join "`n"

Invoke-Check 'Required README and CONTRIBUTING sections are present' {
    $readmeHeadings = @(Get-HeadingNames $readmeLines)
    $contributingHeadings = @(Get-HeadingNames $contributingLines)
    $requiredReadmeHeadings = @(
        'Package Builder',
        'Development Status',
        'Planned Package Targets',
        'Planned Asset Cases',
        'No-Cost Prerequisites',
        'Repository-Local .NET 10 SDK',
        'Visual Studio Code Workflow',
        'Local Commands',
        'Repository Structure',
        'Workspace Containment',
        'Version and Identity Boundaries',
        'Project Documents'
    )
    $requiredContributingHeadings = @(
        'Contributing to Package Builder',
        'Before You Begin',
        'PB Tasks and Branch Workflow',
        'Allowed Branch Types',
        'Lifecycle Markers',
        'Permanent One-Merge Rollover',
        'Pull Requests and Direct Merges',
        'GitHub Governance',
        'Manual Git Ownership',
        'Documentation Synchronization',
        'Commit Messages',
        'Version and Dependency Policy',
        'Visual Studio Code Workflow',
        'Free Tooling and Single-Root Containment',
        'Public Repository Safeguards',
        'Complete Local Validation'
    )

    $missingReadme = @($requiredReadmeHeadings | Where-Object { $_ -notin $readmeHeadings })
    $missingContributing = @(
        $requiredContributingHeadings | Where-Object { $_ -notin $contributingHeadings }
    )
    if ($missingReadme.Count -gt 0 -or $missingContributing.Count -gt 0) {
        throw "Missing headings. README: $($missingReadme -join ', '); CONTRIBUTING: $($missingContributing -join ', ')."
    }
}

Invoke-Check 'README describes only the approved purpose, planned scope, and current status' {
    Assert-Matches $readme 'local-first Windows desktop application' 'local-first Windows purpose'
    Assert-Matches $readme 'repository-foundation stage' 'current foundation-stage status'
    Assert-Matches $readme 'does not yet import models, build packages, provide the desktop workflow, or produce marketplace-ready releases' 'unfinished functionality disclaimer'
    foreach ($target in @('Portable FBX and GLB', 'Unity packages', 'Unreal Engine project archives', 'Marketplace packaging')) {
        Assert-Matches $readme ([regex]::Escape($target)) "planned target '$target'"
    }
    foreach ($assetCase in @(
            'Static model without a rig or animation',
            'Rigged model without animation',
            'Rigged model with one or more animations',
            'Related item set',
            'Collection of independent items'
        )) {
        Assert-Matches $readme ([regex]::Escape($assetCase)) "planned asset case '$assetCase'"
    }
    if ($readme -match '(?im)^\s*Package Builder (?:currently )?(?:imports|builds|generates|exports)\b' -or
        $readme -match '(?im)^\s*Package Builder is (?:complete|production[- ]ready)\b') {
        throw 'README makes an unsupported current product-capability claim.'
    }
}

Invoke-Check 'Contribution policy statements cover the approved workflow and safeguards' {
    $requirements = [ordered]@{
        'read AGENTS first' = 'Read \[AGENTS\.md\]\(AGENTS\.md\) completely before'
        'one task per branch' = 'one implementation PB task per branch'
        'branch format' = '<type>/PB-####-short-description'
        'documentation synchronization' = 'Documentation is part of implementation'
        'clearly scoped commit' = 'clearly scoped commit'
        'version pinning' = 'Pin every approved SDK, tool, engine, action, and dependency version'
        'Visual Studio Code workflow' = 'Visual Studio Code with a PowerShell terminal is the supported development baseline'
        'free workflow' = 'free local or self-hosted workflow'
        'single-root containment' = 'must remain beneath `C:\\Dev\\PackageBuilder`'
        'public repository' = 'approved GitHub repository is public'
        'secrets prohibited' = 'Credentials, tokens, private keys'
        'personal data prohibited' = 'Personal data or personal filesystem paths'
        'downloaded tools prohibited' = 'Downloaded SDKs, tools, installers, or engine installations'
        'caches and logs prohibited' = 'Caches, logs, temporary files'
        'generated engine state prohibited' = 'Unity or Unreal generated engine state'
        'private assets prohibited' = 'customer assets, or other private assets'
        'unlicensed third-party content prohibited' = 'Third-party files without a licence that permits public redistribution'
    }
    foreach ($requirement in $requirements.GetEnumerator()) {
        Assert-Matches $contributing $requirement.Value $requirement.Key
    }
}

Invoke-Check 'Branch types and lifecycle markers agree with AGENTS and the backlog' {
    $approvedTypes = @('chore', 'docs', 'feat', 'fix', 'test', 'security', 'release')
    $backlogAllowedStart = [array]::IndexOf($backlogLines, 'Allowed types:')
    $backlogAllowedEnd = [array]::IndexOf($backlogLines, 'Example:')
    if ($backlogAllowedStart -lt 0 -or $backlogAllowedEnd -le $backlogAllowedStart) {
        throw 'Backlog allowed-branch-type boundaries were not found.'
    }
    $backlogTypeLines = @(
        $backlogLines[($backlogAllowedStart + 1)..($backlogAllowedEnd - 1)]
    )
    $contributingTypeLines = @(
        Get-HeadingSection -Lines $contributingLines -Heading 'Allowed Branch Types'
    )
    $branchTypePattern = '^\s*-\s+`(?<type>[a-z]+)`\s+' +
        [regex]::Escape([string][char]0x2014) + '\s+'
    $backlogTypes = @($backlogTypeLines | ForEach-Object {
        $match = [regex]::Match($_, $branchTypePattern)
        if ($match.Success) {
            $match.Groups['type'].Value
        }
    })
    $contributingTypes = @($contributingTypeLines | ForEach-Object {
        $match = [regex]::Match($_, $branchTypePattern)
        if ($match.Success) {
            $match.Groups['type'].Value
        }
    })
    if ($backlogTypes.Count -eq 0 -or $contributingTypes.Count -eq 0) {
        throw "Branch-type extraction returned no entries. Backlog lines/types: $($backlogTypeLines.Count)/$($backlogTypes.Count); CONTRIBUTING lines/types: $($contributingTypeLines.Count)/$($contributingTypes.Count)."
    }
    Assert-EquivalentSets $approvedTypes $backlogTypes 'Backlog branch types'
    Assert-EquivalentSets $approvedTypes $contributingTypes 'CONTRIBUTING branch types'

    $doneMarker = [char]::ConvertFromUtf32(0x1F7E2) + ' **DONE**'
    $processMarker = [char]::ConvertFromUtf32(0x1F7E1) + ' **PROCESS**'
    $blockedMarker = [char]::ConvertFromUtf32(0x1F534) + ' **BLOCKED**'
    foreach ($marker in @($doneMarker, $processMarker, $blockedMarker)) {
        foreach ($document in @(
                [pscustomobject]@{ Name = 'AGENTS.md'; Text = $agents },
                [pscustomobject]@{ Name = 'backlog'; Text = $backlog },
                [pscustomobject]@{ Name = 'CONTRIBUTING.md'; Text = $contributing }
            )) {
            if (-not $document.Text.Contains($marker)) {
                throw "$($document.Name) is missing lifecycle marker $marker."
            }
        }
    }

    Assert-Matches $contributing 'DONE.*acceptance criterion.*required automated test.*Git and GitHub gate.*main.*CI gate.*explicit user confirmation' 'DONE lifecycle meaning'
    Assert-Matches $contributing 'PROCESS.*work is active.*task stays `\[ \]`' 'PROCESS lifecycle meaning'
    Assert-Matches $contributing 'BLOCKED.*specific unresolved dependency, decision, permission, external state, or repeated failure.*keep the task `\[ \]`' 'BLOCKED lifecycle meaning'
}

Invoke-Check 'Optional PRs, direct merges, and the one-merge rollover are represented correctly' {
    foreach ($document in @(
            [pscustomobject]@{ Name = 'AGENTS.md'; Text = $agents },
            [pscustomobject]@{ Name = 'backlog'; Text = $backlog },
            [pscustomobject]@{ Name = 'CONTRIBUTING.md'; Text = $contributing }
        )) {
        Assert-Matches $document.Text 'Pull requests are optional' "$($document.Name) optional-PR policy"
        Assert-Matches $document.Text 'direct merge.*local validation' "$($document.Name) direct-merge policy"
        Assert-Matches $document.Text 'successful.*main.*CI' "$($document.Name) required main-CI policy"
        Assert-Matches $document.Text 'explicit user confirmation' "$($document.Name) explicit-confirmation policy"
    }

    $orderedPublicationSteps = @(
        'Complete local validation.',
        'Commit the task branch.',
        'Push the task branch.',
        'Merge it into `main`.',
        'Push `main`.',
        'Wait for successful required `main` CI.',
        'Receive explicit user confirmation'
    )
    $lastIndex = -1
    foreach ($step in $orderedPublicationSteps) {
        $stepIndex = $contributing.IndexOf($step, [System.StringComparison]::OrdinalIgnoreCase)
        if ($stepIndex -le $lastIndex) {
            throw "CONTRIBUTING has a missing or out-of-order publication step: $step"
        }
        $lastIndex = $stepIndex
    }

    Assert-Matches $contributing 'beginning of the next task branch' 'next-branch rollover timing'
    Assert-Matches $contributing 'Add exactly one Completion Log row' 'single Completion Log row'
    Assert-Matches $contributing 'Do not create a completion-only branch, commit, pull request, or merge' 'completion-only cycle prohibition'
    Assert-Matches $contributing 'user exclusively controls staging, commits, pushes, merges, pull requests, tags, releases, and GitHub settings' 'manual Git ownership'
}

Invoke-Check 'Task, dependency, product release, and marketplace-profile versions are distinct' {
    $combinedDocumentation = $readme + "`n" + $contributing
    Assert-Matches $combinedDocumentation 'PB-####.*(?:task ID|backlog task ID).*not.*product version' 'PB task IDs are not product versions'
    Assert-Matches $combinedDocumentation 'Pinned.*tool.*dependency versions.*(?:not|never).*Package Builder release versions' 'pinned versions are not product releases'
    Assert-Matches $combinedDocumentation 'Package Builder product releases.*(?:separately|separate).*marketplace-requirements profiles' 'product releases are separate from marketplace profiles'
    Assert-Matches $combinedDocumentation 'No final.*release-versioning scheme.*approved yet' 'unapproved release-versioning decision'
}

Invoke-Check 'Documented commands are exact and reference real repository files' {
    $requiredCommands = @(
        'Set-Location C:\Dev\PackageBuilder',
        '. .\scripts\Enter-PackageBuilderEnvironment.ps1',
        '& .\scripts\Install-Ruff.ps1',
        '& .\scripts\Test-ContributionDocumentation.ps1',
        '& .\scripts\Test-GitHubGovernance.ps1',
        '& .\scripts\Test-RepositoryBaseline.ps1 -RequireTrackedFiles',
        'dotnet restore .\PackageBuilder.sln --locked-mode',
        'dotnet build .\PackageBuilder.sln --configuration Release --no-restore',
        'dotnet format .\PackageBuilder.sln --no-restore --verify-no-changes --severity info --verbosity minimal',
        '& .\scripts\Test-Formatting.ps1',
        '& .\scripts\Test-BaselineUnitTests.ps1 -VerifyNoSourceChanges',
        '& .\scripts\Invoke-CoreCi.ps1'
    )
    foreach ($command in $requiredCommands) {
        foreach ($document in @(
                [pscustomobject]@{ Name = 'README.md'; Text = $readme },
                [pscustomobject]@{ Name = 'CONTRIBUTING.md'; Text = $contributing }
            )) {
            if (-not $document.Text.Contains($command)) {
                throw "$($document.Name) is missing required command: $command"
            }
        }
    }

    $combinedDocumentation = $readme + "`n" + $contributing
    $references = @(
        [regex]::Matches(
            $combinedDocumentation,
            '(?i)(?:\.\\)?scripts\\[A-Za-z0-9._-]+\.ps1|PackageBuilder\.sln|global\.json|Directory\.Build\.props|Directory\.Packages\.props|ruff\.toml'
        ) | ForEach-Object { $_.Value }
    ) | Sort-Object -Unique
    foreach ($reference in $references) {
        $relativePath = $reference
        if ($relativePath.StartsWith('.\')) {
            $relativePath = $relativePath.Substring(2)
        }
        $fullPath = Join-Path $script:RepositoryRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Documented command/file reference does not exist: $reference"
        }
    }
}

Invoke-Check 'Local Markdown links resolve inside the repository' {
    Test-LocalMarkdownLinks 'README.md' $readmeLines
    Test-LocalMarkdownLinks 'CONTRIBUTING.md' $contributingLines
}

Invoke-Check 'No paid tool is mandatory and no unfinished capability is presented as available' {
    foreach ($document in @(
            [pscustomobject]@{ Name = 'README.md'; Lines = $readmeLines; Text = $readme },
            [pscustomobject]@{ Name = 'CONTRIBUTING.md'; Lines = $contributingLines; Text = $contributing }
        )) {
        Assert-Matches $document.Text 'No paid.*(?:mandatory|prerequisite)|paid.*(?:optional|cannot become prerequisites)' "$($document.Name) no-paid-tool policy"
        foreach ($line in $document.Lines) {
            if ($line -match '(?i)\bpaid\b' -and
                $line -match '(?i)\b(?:must|required|requires?|mandatory|prerequisite)\b' -and
                $line -notmatch '(?i)\b(?:no|not|never|without|optional|nonessential|cannot)\b') {
                throw "$($document.Name) describes a paid tool as mandatory: $line"
            }
        }
    }
    Assert-Matches $readme 'planned scope, not currently available product functionality' 'planned-versus-current product disclaimer'
}

Invoke-Check 'Contribution documents contain no secret, personal-path, or prohibited-content example' {
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
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

    foreach ($relativePath in @('README.md', 'CONTRIBUTING.md')) {
        $fullPath = Join-Path $script:RepositoryRoot $relativePath
        $bytes = [System.IO.File]::ReadAllBytes($fullPath)
        if ($bytes -contains 0) {
            throw "$relativePath contains binary data."
        }
        try {
            $text = $strictUtf8.GetString($bytes)
        }
        catch {
            throw "$relativePath is not valid UTF-8."
        }
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
        if ($text -match '(?i)\]\([^)]*\.(exe|dll|pdb|msi|msix|appx|zip|7z|rar|nupkg|vsix|fbx|glb|blend|unitypackage|uasset|umap|pak|pfx|p12|key|dmp)(?:[#?][^)]*)?\)') {
            throw "$relativePath links to prohibited tracked content."
        }
    }
}

Write-Host ''
Write-Host "Contribution documentation validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Contribution documentation validation failed.'
}
