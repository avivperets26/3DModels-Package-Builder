using System.Text.Json;
using PackageBuilder.Contracts.Preview;
using PackageBuilder.Domain.Preview;

namespace PackageBuilder.Targets.Unreal;

/// <summary>Records explicit interactive intent and the canonical contract for the Unreal adapter.</summary>
public static class UnrealPreviewPlan
{
    /// <summary>Uses shared serialization and validation instead of reimplementing preview policy.</summary>
    public static string Create(string projectName, PreviewExperienceContract experience)
    {
        UnrealImportPlan.ValidateName(projectName);
        PreviewExperienceJsonResult serialized = PreviewExperienceJson.Serialize(experience);
        if (serialized.Json is null)
        { throw new ArgumentException("A valid preview contract is required.", nameof(experience)); }
        using var document = JsonDocument.Parse(serialized.Json);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            profile = "unreal-interactive-preview-v1",
            projectName,
            experience = document.RootElement
        });
    }
}
