# Canonical PB branch identity policy shared by repository and quality validators.
function Test-PackageBuilderTaskBranch {
    [OutputType([bool])]
    param([string]$TaskId, [string]$Branch)

    # The user approved only these three task identities on this exact combined branch.
    if ($TaskId -in @('PB-0805', 'PB-0806', 'PB-0807') -and
        $Branch -eq 'feat/PB-0805-PB-0807-multi-item-flow') { return $true }

    if ($TaskId -in @('PB-0808', 'PB-0809', 'PB-0810') -and
        $Branch -eq 'feat/PB-0808-PB-0810-selector-portable-e2e') { return $true }
    if ($TaskId -in @('PB-0901', 'PB-0902', 'PB-0903') -and
        $Branch -eq 'codex/PB-0901-PB-0903-documentation') { return $true }
    if ($TaskId -in @('PB-0904', 'PB-0905', 'PB-0907') -and
        $Branch -eq 'codex/PB-0904-PB-0905-PB-0907-docs-capture') { return $true }
    if ($TaskId -in @('PB-0908', 'PB-0909', 'PB-0910') -and
        $Branch -eq 'codex/PB-0908-PB-0910-media-reports') { return $true }
    $match = [regex]::Match($Branch,
        '^(chore|docs|feat|fix|test|security|release)/(?<id>PB-\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*$')
    return $match.Success -and $match.Groups['id'].Value -eq $TaskId
}
