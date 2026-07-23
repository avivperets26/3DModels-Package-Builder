[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..'
}

$script:FailureCount = 0
$script:PassCount = 0
$script:RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([char[]]'\/')

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

$editorConfigPath = Join-Path $script:RepositoryRoot '.editorconfig'
$ruffConfigurationPath = Join-Path $script:RepositoryRoot 'ruff.toml'
$installerPath = Join-Path $script:RepositoryRoot 'scripts\Install-Ruff.ps1'
$formattingValidatorPath = Join-Path $script:RepositoryRoot 'scripts\Test-Formatting.ps1'
$evidencePath = Join-Path $script:RepositoryRoot 'docs\PB-0007_FORMATTING_EVIDENCE.md'
$requiredRelativePaths = @(
    '.editorconfig',
    'ruff.toml',
    'scripts/Install-Ruff.ps1',
    'scripts/Test-Formatting.ps1',
    'scripts/Test-FormattingConfiguration.ps1',
    'docs/PB-0007_FORMATTING_EVIDENCE.md'
)

Invoke-Check 'Required formatting baseline files exist in the reviewable Git set' {
    $missing = @(
        @(
            $editorConfigPath,
            $ruffConfigurationPath,
            $installerPath,
            $formattingValidatorPath,
            $evidencePath
        ) | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
    )
    if ($missing.Count -gt 0) {
        throw "Missing files: $($missing -join ', ')"
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
        throw "Formatting files are ignored or outside the reviewable Git set: $($notReviewable -join ', ')"
    }
}

Invoke-Check '.editorconfig defines the cross-language formatting policy' {
    $configuration = Get-Content -Raw -LiteralPath $editorConfigPath -Encoding UTF8
    Assert-ContainsAll -Text $configuration -Context '.editorconfig' -Patterns @(
        '(?m)^root\s*=\s*true\s*$',
        '(?m)^charset\s*=\s*utf-8\s*$',
        '(?m)^end_of_line\s*=\s*lf\s*$',
        '(?m)^insert_final_newline\s*=\s*true\s*$',
        '(?m)^trim_trailing_whitespace\s*=\s*true\s*$',
        '(?m)^\[\*\.cs\]\s*$',
        '(?m)^\[\*\.\{xml,xaml,xsd,xslt,props,targets,csproj\}\]\s*$',
        '(?m)^\[\*\.json\]\s*$',
        '(?m)^\[\*\.\{yml,yaml\}\]\s*$',
        '(?m)^\[\*\.md\]\s*$',
        '(?m)^\[\*\.\{ps1,psd1,psm1\}\]\s*$',
        '(?m)^\[\*\.\{py,pyi,pyw\}\]\s*$',
        'csharp_style_namespace_declarations\s*=\s*file_scoped:warning',
        'csharp_prefer_braces\s*=\s*true:warning',
        'dotnet_diagnostic\.IDE0055\.severity\s*=\s*warning',
        'dotnet_naming_rule\.interfaces_use_i_prefix',
        'dotnet_naming_rule\.private_fields_use_underscore',
        'packages\.lock\.json',
        'generated_code\s*=\s*true'
    )
}

Invoke-Check 'Ruff configuration is exact, stable, contained, and maintainable' {
    $configuration = Get-Content -Raw -LiteralPath $ruffConfigurationPath -Encoding UTF8
    Assert-ContainsAll -Text $configuration -Context 'ruff.toml' -Patterns @(
        '(?m)^required-version\s*=\s*"==0\.15\.22"\s*$',
        '(?m)^target-version\s*=\s*"py311"\s*$',
        '(?m)^preview\s*=\s*false\s*$',
        '(?m)^fix\s*=\s*false\s*$',
        '(?m)^unsafe-fixes\s*=\s*false\s*$',
        '(?m)^respect-gitignore\s*=\s*true\s*$',
        '(?m)^force-exclude\s*=\s*true\s*$',
        '(?m)^cache-dir\s*=\s*"runtime-data/ruff-cache"\s*$',
        '(?s)\[lint\].*?"I".*?"UP".*?"B".*?"S".*?"SIM".*?"RUF"',
        '(?s)exclude\s*=\s*\[.*?"artifacts".*?\]',
        '(?s)exclude\s*=\s*\[.*?"downloads".*?\]',
        '(?s)exclude\s*=\s*\[.*?"logs".*?\]',
        '(?s)exclude\s*=\s*\[.*?"runtime-data".*?\]',
        '(?s)exclude\s*=\s*\[.*?"tools".*?\]',
        '(?s)\[format\].*?line-ending\s*=\s*"lf"'
    )
    if ($configuration -match '(?s)select\s*=\s*\[[^\]]*"ALL"') {
        throw 'Ruff lint selection must not enable every rule blindly.'
    }
}

Invoke-Check 'Ruff installer pins official URLs and verifies SHA-256' {
    $installer = Get-Content -Raw -LiteralPath $installerPath -Encoding UTF8
    Assert-ContainsAll -Text $installer -Context 'scripts/Install-Ruff.ps1' -Patterns @(
        '\$ruffVersion\s*=\s*''0\.15\.22''',
        '\$expectedSha256\s*=\s*''6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d''',
        'https://releases\.astral\.sh/github/ruff/releases/download/',
        'Invoke-WebRequest',
        'Get-FileHash\s+-Algorithm\s+SHA256',
        'Expand-Archive',
        'downloads\\ruff',
        'tools\\ruff',
        'logs\\setup\\PB-0007',
        'artifacts\\setup\\PB-0007'
    )
    if ($installer -match '(?im)\bInvoke-Expression\b|\biex\b|Invoke-WebRequest[^\r\n]*\|') {
        throw 'Ruff installer must not pipe remote content to a shell or Invoke-Expression.'
    }
}

Invoke-Check 'Formatting validator is non-mutating by default and uses local tools' {
    $validator = Get-Content -Raw -LiteralPath $formattingValidatorPath -Encoding UTF8
    Assert-ContainsAll -Text $validator -Context 'scripts/Test-Formatting.ps1' -Patterns @(
        '\[string\]\$RepositoryRoot',
        'IsNullOrWhiteSpace\(\$RepositoryRoot\)',
        '\$RepositoryRoot\s*=\s*Join-Path\s+\$PSScriptRoot\s+''\.\.''',
        '\[switch\]\$Fix',
        'tools\\dotnet\\',
        'tools\\ruff\\',
        '''--no-restore''',
        '''--verify-no-changes''',
        '''--no-fix''',
        '''--check''',
        'if\s*\(\$Fix\)',
        'Get-FormatCandidateHashes',
        'Assert-HashesUnchanged',
        'logs\\validation\\PB-0007',
        'artifacts\\validation\\PB-0007'
    )
}

Invoke-Check 'Formatting evidence records the approved tool policy' {
    $evidence = Get-Content -Raw -LiteralPath $evidencePath -Encoding UTF8
    Assert-ContainsAll -Text $evidence -Context 'docs/PB-0007_FORMATTING_EVIDENCE.md' -Patterns @(
        'PB-0007',
        '0\.15\.22',
        '6e5419593984941405e9add902e89c6ea4af87d97919ac5ef82e1bc4e43bbd8d',
        '10\.0\.302',
        'dotnet format',
        'ruff check',
        'ruff format(?:\s+--config\s+\S+)?\s+--check',
        'scripts[\\/]Test-Formatting\.ps1',
        'scripts[\\/]Install-Ruff\.ps1'
    )
}

Write-Host ''
Write-Host "Formatting configuration validation: $script:PassCount passed, $script:FailureCount failed."
if ($script:FailureCount -gt 0) {
    throw 'Formatting configuration validation failed.'
}
