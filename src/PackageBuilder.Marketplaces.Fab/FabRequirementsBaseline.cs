using System.Text;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Reviewed, offline seed; loading it never silently approves it as the user's current profile.</summary>
public static class FabRequirementsBaseline
{
    private static readonly Lazy<FabRequirementsProfile> _profile = new(() =>
    {
        using Stream stream = typeof(FabRequirementsBaseline).Assembly.GetManifestResourceStream(
            "PackageBuilder.Fab.Requirements.Baseline.json")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return FabRequirementsProfileJson.Load(reader.ReadToEnd()).Value
            ?? throw new InvalidOperationException("The embedded Fab requirements profile is invalid.");
    });

    public static FabRequirementsProfile Profile => _profile.Value;
}
