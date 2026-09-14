using System.Text;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Reviewed, offline seed; loading it never silently approves it as the user's current profile.</summary>
public static class FabRequirementsBaseline
{
    private static readonly Lazy<FabRequirementsProfile> _profile = new(() => Load("Baseline"));
    private static readonly Lazy<FabRequirementsProfile> _artifactValidationProfile = new(() => Load("ArtifactValidation"));

    private static FabRequirementsProfile Load(string resource)
    {
        using Stream stream = typeof(FabRequirementsBaseline).Assembly.GetManifestResourceStream(
            $"PackageBuilder.Fab.Requirements.{resource}.json")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return FabRequirementsProfileJson.Load(reader.ReadToEnd()).Value
            ?? throw new InvalidOperationException("The embedded Fab requirements profile is invalid.");
    }

    public static FabRequirementsProfile Profile => _profile.Value;

    /// <summary>New review candidate for the artifact validators; does not replace an approved cached
    /// revision or the historical baseline. Import/test/approve it explicitly through the existing updater.</summary>
    public static FabRequirementsProfile ArtifactValidationProfile => _artifactValidationProfile.Value;
}
