# Shared explicit candidate preflight; never changes global engine approval.
function Assert-UnrealContainedPath([string]$Path, [string]$Parent) {
    # Reject junctions before traversal, including ancestors of not-yet-created output paths.
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside its owned root.' }
    for ($probe = $full; $probe; $probe = [IO.Path]::GetDirectoryName($probe)) {
        if ((Test-Path -LiteralPath $probe) -and
            ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Linked paths are not permitted.'
        }
    }
}


function Get-UnrealCandidate {
    param([string]$repo)
$profile = Get-Content -LiteralPath (Join-Path $repo 'profiles/engines/unreal-5.8.2-candidate.json') -Raw | ConvertFrom-Json
$engine = $profile.userApprovedExternalInstallationRoot
$executable = Join-Path $engine 'Engine/Binaries/Win64/UnrealEditor-Cmd.exe'

# Exact user-approved external installation; do not grant execution to arbitrary discovery results.
if ($engine -cne 'C:\Program Files\Epic Games\UE_5.8') { throw 'Unreviewed external engine root.' }
Assert-UnrealContainedPath $executable $engine
Assert-UnrealContainedPath (Join-Path $engine 'Engine/Build/Build.version') $engine
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Unreal $($profile.version) is not installed. Install it through Epic into $engine, then rerun."
}
$signature = Get-AuthenticodeSignature -LiteralPath $executable
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Epic Games') {
    throw 'The candidate editor does not have a valid Epic Games signature.'
}
# Invoke the existing production locator, rather than copying its Build.version/layout rules.
$previousEngine = $env:PB_UNREAL_ENGINE_ROOT
try {
    $env:PB_UNREAL_ENGINE_ROOT = $engine
    & dotnet test (Join-Path $repo 'tests/PackageBuilder.Infrastructure.Tests/PackageBuilder.Infrastructure.Tests.csproj') `
        -c Release --no-restore --filter 'FullyQualifiedName~FoundationCandidateUsesVerifiedContainedDiscovery' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Production installation discovery failed.' }
} finally { $env:PB_UNREAL_ENGINE_ROOT = $previousEngine }

    return @{ Profile = $profile; Executable = $executable; Signature = $signature; Engine = $engine }
}
