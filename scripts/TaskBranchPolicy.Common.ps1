# Canonical PB branch identity policy shared by repository and quality validators.
function Test-PackageBuilderTaskBranch {
    [OutputType([bool])]
    param([string]$TaskId, [string]$Branch)

    # The user approved only these three task identities on this exact combined branch.
    if ($TaskId -in @('PB-0805', 'PB-0806', 'PB-0807') -and
        $Branch -eq 'feat/PB-0805-PB-0807-multi-item-flow') { return $true }

    if ($TaskId -in @('PB-0808', 'PB-0809', 'PB-0810') -and
        $Branch -eq 'feat/PB-0808-PB-0810-selector-portable-e2e') { return $true }
    $match = [regex]::Match($Branch,
        '^(chore|docs|feat|fix|test|security|release)/(?<id>PB-\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*$')
    return $match.Success -and $match.Groups['id'].Value -eq $TaskId
}
