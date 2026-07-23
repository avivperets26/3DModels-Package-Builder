[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')
$script:PassCount = 0
$script:FailureCount = 0
$script:CheckoutVersion = '7.0.1'
$script:CheckoutSha = '3d3c42e5aac5ba805825da76410c181273ba90b1'
$script:SetupDotnetVersion = '6.0.0'
$script:SetupDotnetSha = 'a98b56852c35b8e3190ac28c8c2271da59106c68'

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

function Assert-ContainsAll {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string[]]$Patterns,
        [Parameter(Mandatory)][string]$Context
    )

    $missing = @($Patterns | Where-Object { $Text -notmatch $_ })
    if ($missing.Count -gt 0) {
        throw "$Context is missing required patterns: $($missing -join ', ')"
    }
}

function Get-JobBlock {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory)][string]$JobName
    )

    $startMatches = @(
        for ($index = 0; $index -lt $Lines.Count; $index++) {
            if ($Lines[$index] -match "^  $([regex]::Escape($JobName)):\s*$") {
                $index
            }
        }
    )
    if ($startMatches.Count -ne 1) {
        throw "Workflow job '$JobName' must exist exactly once; found $($startMatches.Count)."
    }

    $start = $startMatches[0]
    $end = $Lines.Count - 1
    for ($index = $start + 1; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -match '^  [A-Za-z0-9_-]+:\s*$') {
            $end = $index - 1
            break
        }
    }

    return ($Lines[$start..$end] -join [Environment]::NewLine)
}

function ConvertTo-WorkflowLines {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Text
    )

    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    return @(
        $normalized.Split(
            [char[]]@([char]10),
            [System.StringSplitOptions]::None
        )
    )
}

function ConvertTo-SanitizedPermissionEntry {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Entry
    )

    $sanitized = $Entry.
        Replace("`r", '<CR>').
        Replace("`n", '<LF>').
        Replace("`t", '<TAB>')
    $sanitized = [regex]::Replace(
        $sanitized,
        '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]',
        '<CONTROL>'
    )
    if ($sanitized.Length -gt 160) {
        $sanitized = $sanitized.Substring(0, 160) + '<TRUNCATED>'
    }

    return "[$sanitized]"
}

function Get-WorkflowPermissionEntries {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory)][int]$DeclarationIndex,
        [Parameter(Mandatory)][AllowEmptyString()][string]$DeclarationIndent
    )

    $entries = @(
        for ($index = $DeclarationIndex + 1; $index -lt $Lines.Count; $index++) {
            $line = $Lines[$index]
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            $keyMatch = [regex]::Match(
                $line,
                '^(?<indent>[ \t]*)[A-Za-z0-9_.-]+:'
            )
            if ($keyMatch.Success -and
                $keyMatch.Groups['indent'].Value.Length -le $DeclarationIndent.Length) {
                break
            }

            $line
        }
    )

    return $entries
}

function Assert-WorkflowPermissionPolicy {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines,
        [Parameter(Mandatory)][string]$Context
    )

    $declarations = @(
        for ($index = 0; $index -lt $Lines.Count; $index++) {
            $match = [regex]::Match(
                $Lines[$index],
                '^(?<indent>[ \t]*)permissions:[ \t]*$'
            )
            if ($match.Success) {
                [pscustomobject]@{
                    Index = $index
                    Indent = $match.Groups['indent'].Value
                }
            }
        }
    )

    $permissionEntries = @(
        foreach ($declaration in $declarations) {
            Get-WorkflowPermissionEntries `
                -Lines $Lines `
                -DeclarationIndex $declaration.Index `
                -DeclarationIndent $declaration.Indent
        }
    )
    $sanitizedEntries = if ($permissionEntries.Count -eq 0) {
        '<none>'
    }
    else {
        @(
            $permissionEntries |
                ForEach-Object { ConvertTo-SanitizedPermissionEntry -Entry $_ }
        ) -join ', '
    }
    $diagnostic = (
        "Permission blocks found: $($declarations.Count). " +
        "Permission entries found: $($permissionEntries.Count). " +
        "Sanitized permission entries: $sanitizedEntries."
    )

    if ($declarations.Count -ne 1) {
        throw "$Context must define exactly one permissions declaration anywhere in the workflow. $diagnostic"
    }
    if ($declarations[0].Indent.Length -ne 0) {
        throw "$Context permissions declaration must be top-level with no indentation. $diagnostic"
    }
    if ($permissionEntries.Count -ne 1) {
        throw "$Context permissions block must contain exactly one nonblank entry. $diagnostic"
    }
    if ($permissionEntries[0] -cne '  contents: read') {
        throw "$Context permission entry must be exactly '  contents: read'. $diagnostic"
    }

    return [pscustomobject]@{
        BlockCount = $declarations.Count
        EntryCount = $permissionEntries.Count
        Entries = $permissionEntries
    }
}

if (-not (Test-Path -LiteralPath $script:RepositoryRoot -PathType Container)) {
    throw "Repository root does not exist: $script:RepositoryRoot"
}

$gitRootOutput = @(& git -C $script:RepositoryRoot rev-parse --show-toplevel 2>&1)
if ($LASTEXITCODE -ne 0 -or $gitRootOutput.Count -ne 1) {
    throw "Unable to resolve the Git top level for '$script:RepositoryRoot'."
}
$gitRoot = [System.IO.Path]::GetFullPath([string]$gitRootOutput[0]).TrimEnd([char[]]'\/')
if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($gitRoot, $script:RepositoryRoot)) {
    throw "RepositoryRoot must be the Git top level. Git reports: $gitRoot"
}

$workflowPath = Join-Path $script:RepositoryRoot '.github\workflows\repository-baseline.yml'
$coreScriptPath = Join-Path $script:RepositoryRoot 'scripts\Invoke-CoreCi.ps1'
$testScriptPath = Join-Path $script:RepositoryRoot 'scripts\Test-BaselineUnitTests.ps1'
$requiredRelativePaths = @(
    '.github/workflows/repository-baseline.yml',
    'scripts/Invoke-CoreCi.ps1',
    'scripts/Test-BaselineUnitTests.ps1',
    'scripts/Test-CoreCiConfiguration.ps1',
    'scripts/Test-RepositoryBaseline.ps1',
    'scripts/Install-Ruff.ps1'
)

Invoke-Check 'Core-CI configuration files exist in the reviewable Git set' {
    $missingFiles = @($requiredRelativePaths | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $script:RepositoryRoot $_) -PathType Leaf)
    })
    if ($missingFiles.Count -gt 0) {
        throw "Missing files: $($missingFiles -join ', ')"
    }

    $reviewablePaths = @(
        & git -C $script:RepositoryRoot ls-files
        & git -C $script:RepositoryRoot ls-files --others --exclude-standard
    ) | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to enumerate the reviewable Git file set.'
    }

    $notReviewable = @($requiredRelativePaths | Where-Object { $_ -notin $reviewablePaths })
    if ($notReviewable.Count -gt 0) {
        throw "Core-CI files are ignored or outside the reviewable Git set: $($notReviewable -join ', ')"
    }
}

$workflow = Get-Content -Raw -LiteralPath $workflowPath -Encoding UTF8
$workflowLines = @(Get-Content -LiteralPath $workflowPath -Encoding UTF8)
$coreScript = Get-Content -Raw -LiteralPath $coreScriptPath -Encoding UTF8
$testScript = Get-Content -Raw -LiteralPath $testScriptPath -Encoding UTF8

Invoke-Check 'Workflow triggers and repository permissions are exact and read-only' {
    $triggerPattern = (
        '(?ms)^on:\r?\n' +
        '\s{2}push:\r?\n' +
        '\s{4}branches:\s*\[\s*main\s*\]\r?\n' +
        '\s{2}pull_request:\r?\n' +
        '\s{4}branches:\s*\[\s*main\s*\]'
    )
    if ($workflow -notmatch $triggerPattern) {
        throw 'Workflow must run on pushes to main and pull requests targeting main.'
    }

    $null = Assert-WorkflowPermissionPolicy `
        -Lines $workflowLines `
        -Context 'Workflow'

    $jobsStart = [array]::IndexOf($workflowLines, 'jobs:')
    if ($jobsStart -lt 0) {
        throw 'Workflow jobs block is missing.'
    }
    $jobDefinitions = @(
        $workflowLines[($jobsStart + 1)..($workflowLines.Count - 1)] |
            Where-Object { $_ -match '^  (?<name>[A-Za-z0-9_-]+):\s*$' } |
            ForEach-Object { [regex]::Match($_, '^  (?<name>[A-Za-z0-9_-]+):\s*$').Groups['name'].Value }
    )
    $expectedJobs = @('core-ci', 'validate-repository-baseline')
    if (@(Compare-Object ($expectedJobs | Sort-Object) ($jobDefinitions | Sort-Object)).Count -gt 0) {
        throw "Workflow must contain exactly the baseline and core jobs; found $($jobDefinitions -join ', ')."
    }

    $runnerValues = @(
        [regex]::Matches($workflow, '(?m)^\s*runs-on:\s*(?<value>\S+)\s*$') |
            ForEach-Object { $_.Groups['value'].Value }
    )
    if ($runnerValues.Count -ne 2 -or @($runnerValues | Where-Object { $_ -cne 'windows-latest' }).Count -gt 0) {
        throw 'Every workflow job must use the free windows-latest runner.'
    }
}

Invoke-Check 'Synthetic workflow permission cases are deterministic and fail closed' {
    $syntheticCases = @(
        [pscustomobject]@{
            Name = 'valid LF input'
            Text = "name: Test`n`npermissions:`n  contents: read`n`njobs:`n  test:`n"
            ShouldPass = $true
            BlockCount = 1
            EntryCount = 1
            EntryFragments = @()
        },
        [pscustomobject]@{
            Name = 'valid CRLF input'
            Text = "name: Test`r`n`r`npermissions:`r`n  contents: read`r`n`r`njobs:`r`n  test:`r`n"
            ShouldPass = $true
            BlockCount = 1
            EntryCount = 1
            EntryFragments = @()
        },
        [pscustomobject]@{
            Name = 'missing permissions'
            Text = "name: Test`n`njobs:`n  test:`n"
            ShouldPass = $false
            BlockCount = 0
            EntryCount = 0
            EntryFragments = @('<none>')
        },
        [pscustomobject]@{
            Name = 'duplicate permissions'
            Text = (
                "name: Test`npermissions:`n  contents: read`n" +
                "permissions:`n  contents: read`njobs:`n  test:`n"
            )
            ShouldPass = $false
            BlockCount = 2
            EntryCount = 2
            EntryFragments = @('[  contents: read]')
        },
        [pscustomobject]@{
            Name = 'job-level permissions'
            Text = (
                "name: Test`njobs:`n  test:`n    permissions:`n" +
                "      contents: read`n    steps:`n"
            )
            ShouldPass = $false
            BlockCount = 1
            EntryCount = 1
            EntryFragments = @('[      contents: read]')
        },
        [pscustomobject]@{
            Name = 'extra read permission'
            Text = (
                "name: Test`npermissions:`n  contents: read`n" +
                "  actions: read`njobs:`n  test:`n"
            )
            ShouldPass = $false
            BlockCount = 1
            EntryCount = 2
            EntryFragments = @('[  contents: read]', '[  actions: read]')
        },
        [pscustomobject]@{
            Name = 'write permission'
            Text = "name: Test`npermissions:`n  contents: write`njobs:`n  test:`n"
            ShouldPass = $false
            BlockCount = 1
            EntryCount = 1
            EntryFragments = @('[  contents: write]')
        },
        [pscustomobject]@{
            Name = 'invalid permission indentation'
            Text = "name: Test`npermissions:`n contents: read`njobs:`n  test:`n"
            ShouldPass = $false
            BlockCount = 1
            EntryCount = 1
            EntryFragments = @('[ contents: read]')
        }
    )

    $passedCases = 0
    foreach ($case in $syntheticCases) {
        $failureMessage = $null
        try {
            $lines = ConvertTo-WorkflowLines -Text $case.Text
            $null = Assert-WorkflowPermissionPolicy `
                -Lines $lines `
                -Context "Synthetic case '$($case.Name)'"
        }
        catch {
            $failureMessage = $_.Exception.Message
        }

        if ($case.ShouldPass) {
            if ($null -ne $failureMessage) {
                throw "Synthetic case '$($case.Name)' should pass: $failureMessage"
            }
        }
        else {
            if ($null -eq $failureMessage) {
                throw "Synthetic case '$($case.Name)' unexpectedly passed."
            }
            $expectedDiagnostics = @(
                "Permission blocks found: $($case.BlockCount).",
                "Permission entries found: $($case.EntryCount).",
                'Sanitized permission entries:'
            ) + $case.EntryFragments
            $missingDiagnostics = @(
                $expectedDiagnostics |
                    Where-Object {
                        $failureMessage.IndexOf(
                            $_,
                            [System.StringComparison]::Ordinal
                        ) -lt 0
                    }
            )
            if ($missingDiagnostics.Count -gt 0) {
                throw (
                    "Synthetic case '$($case.Name)' lacked diagnostics: " +
                    "$($missingDiagnostics -join ', '). Actual: $failureMessage"
                )
            }
        }

        $passedCases++
        Write-Host "       [PASS] Synthetic permission case: $($case.Name)"
    }

    if ($passedCases -ne $syntheticCases.Count) {
        throw "Expected $($syntheticCases.Count) synthetic permission cases; passed $passedCases."
    }
    Write-Host "       Synthetic permission cases passed: $passedCases/$($syntheticCases.Count)."
}

$baselineJob = Get-JobBlock -Lines $workflowLines -JobName 'validate-repository-baseline'
$coreJob = Get-JobBlock -Lines $workflowLines -JobName 'core-ci'

Invoke-Check 'PB-0002 repository-baseline job remains preserved and fail-closed' {
    Assert-ContainsAll -Text $baselineJob -Context 'Repository-baseline job' -Patterns @(
        '(?m)^\s{4}name:\s*Validate repository baseline\s*$',
        '(?m)^\s{4}runs-on:\s*windows-latest\s*$',
        '(?m)^\s{4}timeout-minutes:\s*10\s*$',
        "actions/checkout@$([regex]::Escape($script:CheckoutSha))",
        '(?m)^\s*fetch-depth:\s*0\s*$',
        '(?m)^\s*persist-credentials:\s*false\s*$',
        'scripts\\Test-RepositoryBaseline\.ps1',
        'GITHUB_WORKSPACE',
        'RequireTrackedFiles'
    )
    if ($baselineJob -match '(?m)^\s*needs:\s*' -or
        $baselineJob -match 'actions/setup-dotnet@' -or
        $baselineJob -match 'Invoke-CoreCi') {
        throw 'The bootstrap baseline job must remain independent and dependency-free.'
    }
}

Invoke-Check 'Core job depends on baseline and uses the approved Windows setup' {
    Assert-ContainsAll -Text $coreJob -Context 'Core-CI job' -Patterns @(
        '(?m)^\s{4}name:\s*Validate core application\s*$',
        '(?m)^\s{4}needs:\s*validate-repository-baseline\s*$',
        '(?m)^\s{4}runs-on:\s*windows-latest\s*$',
        '(?m)^\s{4}timeout-minutes:\s*30\s*$',
        "actions/checkout@$([regex]::Escape($script:CheckoutSha))",
        "actions/setup-dotnet@$([regex]::Escape($script:SetupDotnetSha))",
        '(?m)^\s*dotnet-version:\s*''10\.0\.302''\s*$',
        '(?m)^\s*fetch-depth:\s*0\s*$',
        '(?m)^\s*persist-credentials:\s*false\s*$',
        'scripts\\Invoke-CoreCi\.ps1',
        'GITHUB_WORKSPACE',
        'GitHubActions'
    )
}

Invoke-Check 'Every action is an exact reviewed immutable release commit' {
    $uses = @([regex]::Matches($workflow, '(?m)^\s*uses:\s*(?<value>\S+)\s*$'))
    if ($uses.Count -ne 3) {
        throw "Expected exactly three action references; found $($uses.Count)."
    }
    foreach ($use in $uses) {
        if ($use.Groups['value'].Value -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}$') {
            throw "Action is not pinned to an immutable commit SHA: $($use.Groups['value'].Value)"
        }
    }

    $checkoutUses = @($uses | Where-Object {
        $_.Groups['value'].Value -ceq "actions/checkout@$($script:CheckoutSha)"
    })
    $setupUses = @($uses | Where-Object {
        $_.Groups['value'].Value -ceq "actions/setup-dotnet@$($script:SetupDotnetSha)"
    })
    if ($checkoutUses.Count -ne 2 -or $setupUses.Count -ne 1) {
        throw 'Workflow action references do not match the reviewed checkout/setup-dotnet pins.'
    }
    if ($workflow -notmatch "actions/checkout v$([regex]::Escape($script:CheckoutVersion))" -or
        $workflow -notmatch "actions/setup-dotnet v$([regex]::Escape($script:SetupDotnetVersion))") {
        throw 'Workflow comments must identify the reviewed stable action releases.'
    }

    $fetchDepthCount = @([regex]::Matches($workflow, '(?m)^\s*fetch-depth:\s*0\s*$')).Count
    $persistCount = @([regex]::Matches($workflow, '(?m)^\s*persist-credentials:\s*false\s*$')).Count
    if ($fetchDepthCount -ne 2 -or $persistCount -ne 2) {
        throw 'Both checkout steps must fetch full history and disable persisted credentials.'
    }
}

Invoke-Check 'Local core entry script contains every required fail-closed stage in order' {
    $orderedStageNames = @(
        'Repository baseline validation',
        '.NET SDK 10.0.302 verification',
        'Locked solution restore',
        'Release solution build',
        '.NET formatting verification',
        'Ruff 0.15.22 installation and verification',
        'Ruff lint verification',
        'Ruff formatting verification',
        'Baseline test projects'
    )
    $previousIndex = -1
    foreach ($stageName in $orderedStageNames) {
        $currentIndex = $coreScript.IndexOf(
            "-Name '$stageName'",
            [System.StringComparison]::Ordinal
        )
        if ($currentIndex -lt 0) {
            throw "Core entry script is missing stage '$stageName'."
        }
        if ($currentIndex -le $previousIndex) {
            throw "Core entry script stage '$stageName' is out of order."
        }
        $previousIndex = $currentIndex
    }

    Assert-ContainsAll -Text $coreScript -Context 'scripts/Invoke-CoreCi.ps1' -Patterns @(
        'Test-RepositoryBaseline\.ps1',
        'RequireTrackedFiles',
        '''restore''',
        '''--locked-mode''',
        '''build''',
        '''Release''',
        '''--no-restore''',
        '''format''',
        '''--verify-no-changes''',
        'Install-Ruff\.ps1',
        '''check''',
        '''--no-fix''',
        '''--check''',
        'Test-BaselineUnitTests\.ps1',
        'NoRestore\s*=\s*\$true',
        'NoBuild\s*=\s*\$true',
        'ResultSetName\s*=\s*''PB-0009''',
        'VerifyNoSourceChanges\s*=\s*\$true'
    )
    if (@([regex]::Matches($coreScript, '''restore''')).Count -ne 1 -or
        @([regex]::Matches($coreScript, '''--locked-mode''')).Count -ne 1) {
        throw 'Core entry script must contain exactly one locked restore command.'
    }
}

Invoke-Check 'SDK selection, containment, telemetry opt-out, and reusable test safeguards are present' {
    Assert-ContainsAll -Text $coreScript -Context 'scripts/Invoke-CoreCi.ps1' -Patterns @(
        '\[switch\]\$GitHubActions',
        'GITHUB_ACTIONS',
        'GITHUB_WORKSPACE',
        'tools\\dotnet\\\$dotnetVersion\\dotnet\.exe',
        'runtime-data\\cli-home',
        'runtime-data\\nuget-packages',
        'runtime-data\\nuget-http-cache',
        'runtime-data\\nuget-scratch',
        'runtime-data\\nuget-plugins-cache',
        'runtime-data\\ruff-cache',
        'artifacts\\validation\\PB-0009\\temp',
        'logs\\validation\\PB-0009',
        'DOTNET_CLI_TELEMETRY_OPTOUT\s*=\s*''1''',
        'TESTINGPLATFORM_TELEMETRY_OPTOUT\s*=\s*''1'''
    )
    if ($coreScript -match '(?i)C:\\Dev\\PackageBuilder') {
        throw 'Core entry script must resolve the repository dynamically.'
    }

    Assert-ContainsAll -Text $testScript -Context 'scripts/Test-BaselineUnitTests.ps1' -Patterns @(
        '\[ValidateSet\(''Debug'',\s*''Release''\)\]',
        '\[switch\]\$NoRestore',
        '\[switch\]\$NoBuild',
        '\[string\]\$ResultSetName',
        '\[string\]\$DotnetExecutable',
        '\[switch\]\$GitHubActions',
        'Passed\s+-lt\s+4',
        '''--no-build'''
    )
}

Invoke-Check 'Workflow and core entry contain no prohibited CI behavior' {
    $workflowForbidden = [ordered]@{
        'failure suppression' = '(?i)continue-on-error|\|\|\s*true'
        'artifact transfer' = '(?i)upload-artifact|download-artifact'
        'secrets' = '(?i)\$\{\{\s*secrets\.|secrets\.'
        'write permission' = '(?im)^\s*(contents|actions|checks|deployments|id-token|packages|pull-requests|security-events|statuses):\s*write\s*$'
        'paid or self-hosted runner' = '(?i)self-hosted|runs-on:\s*(?:ubuntu|macos|windows-\d|[A-Za-z0-9_.-]+\s*,)'
        'engines' = '(?i)\bblender\b|\bunity\b|\bunreal\b'
        'publishing or deployment' = '(?i)dotnet\s+publish|nuget\s+push|gh\s+release|deployment|marketplace'
        'cache action' = '(?i)actions/cache@'
        'services' = '(?im)^\s*services:\s*$'
        'telemetry configuration' = '(?i)telemetry'
    }
    foreach ($entry in $workflowForbidden.GetEnumerator()) {
        if ($workflow -match $entry.Value) {
            throw "Workflow contains prohibited $($entry.Key) behavior."
        }
    }

    $coreForbidden = [ordered]@{
        'failure suppression' = '(?i)continue-on-error|\|\|\s*true'
        'engines' = '(?i)\bblender\b|\bunity\b|\bunreal\b'
        'publishing or deployment' = '(?i)dotnet\s+publish|nuget\s+push|gh\s+release|deployment|marketplace'
        'secret access' = '(?i)\$\{\{\s*secrets\.|secrets\.'
        'telemetry enablement' = '(?i)(DOTNET_CLI_TELEMETRY_OPTOUT|TESTINGPLATFORM_TELEMETRY_OPTOUT)\s*=\s*[''"]0[''"]'
    }
    foreach ($entry in $coreForbidden.GetEnumerator()) {
        if ($coreScript -match $entry.Value) {
            throw "Core entry script contains prohibited $($entry.Key) behavior."
        }
    }
}

if ($script:FailureCount -gt 0) {
    throw "Core-CI configuration validation failed: $($script:PassCount) passed, $($script:FailureCount) failed."
}

Write-Host (
    "Core-CI configuration validation passed: $($script:PassCount) checks, 0 failures; " +
    "checkout v$($script:CheckoutVersion), setup-dotnet v$($script:SetupDotnetVersion)."
) -ForegroundColor Green
