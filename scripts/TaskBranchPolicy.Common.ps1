# Canonical PB branch identity policy shared by repository and quality validators.
function Test-PackageBuilderTaskBranch {
    [OutputType([bool])]
    param([string]$TaskId, [string]$Branch)

    if ($TaskId -in @('PB-1110', 'PB-1111', 'PB-1112') -and
        $Branch -eq 'feat/PB-1110-PB-1112-unreal-overview-validation') { return $true }

    if ($TaskId -in @('PB-1107', 'PB-1108', 'PB-1109') -and
        $Branch -eq 'feat/PB-1107-PB-1109-unreal-materials-meshes') { return $true }

    if ($TaskId -in @('PB-1104', 'PB-1105', 'PB-1106') -and
        $Branch -eq 'codex/PB-1104-PB-1106-unreal-import') { return $true }

    if ($TaskId -in @('PB-1101', 'PB-1102', 'PB-1103') -and
        $Branch -eq 'codex/PB-1101-PB-1103-unreal-foundation') { return $true }

    if ($TaskId -in @('PB-1008', 'PB-1009', 'PB-1010') -and
        $Branch -eq 'codex/PB-1008-PB-1010-fab-release') { return $true }

    if ($TaskId -in @('PB-1004', 'PB-1005', 'PB-1007') -and
        $Branch -eq 'codex/PB-1004-PB-1005-PB-1007-fab-validators') { return $true }

    if ($TaskId -eq 'PB-0016' -and $Branch -eq 'codex/PB-0016-dependency-refresh') { return $true }

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
    if ($TaskId -in @('PB-0911', 'PB-0912') -and
        $Branch -eq 'codex/PB-0911-PB-0912-reports-support') { return $true }
    if ($TaskId -in @('PB-1001', 'PB-1002', 'PB-1003') -and
        $Branch -eq 'codex/PB-1001-PB-1003-fab-foundation') { return $true }
    $match = [regex]::Match($Branch,
        '^(chore|docs|feat|fix|test|security|release)/(?<id>PB-\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*$')
    return $match.Success -and $match.Groups['id'].Value -eq $TaskId
}
