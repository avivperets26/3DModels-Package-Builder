namespace PackageBuilder.Contracts.Tools;

/// <summary>Opens allowlisted official installation guidance after application-level consent.</summary>
public interface IEngineInstallationGuidanceLauncher
{
    Task<bool> OpenAsync(Uri officialGuidanceUri, CancellationToken cancellationToken = default);
}
