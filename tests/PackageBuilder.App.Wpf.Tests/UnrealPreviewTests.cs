using System.IO;
using System.Text.Json;
using PackageBuilder.Contracts.Preview;
using PackageBuilder.Domain.Preview;
using PackageBuilder.Targets.Unreal;

namespace PackageBuilder.App.Wpf.Tests;

/// <summary>Checks explicit interactive intent and reuse of the canonical wire contract.</summary>
[Trait("Task", "PB-1116")]
public sealed class UnrealPreviewTests
{
    [Fact]
    public void InteractivePlanPreservesCanonicalContractExactly()
    {
        string plan = UnrealPreviewPlan.Create("Product", PreviewExperienceDefaults.Contract);
        Assert.Equal(plan, UnrealPreviewPlan.Create("Product", PreviewExperienceDefaults.Contract));
        using var document = JsonDocument.Parse(plan);
        Assert.Equal("Product", document.RootElement.GetProperty("projectName").GetString());
        Assert.Equal("unreal-interactive-preview-v1", document.RootElement.GetProperty("profile").GetString());
        string contract = document.RootElement.GetProperty("experience").GetRawText();
        Assert.Equal(PreviewExperienceJson.Serialize(PreviewExperienceDefaults.Contract).Json, contract);
        Assert.NotNull(PreviewExperienceJson.Deserialize(contract).Json);
        Assert.Equal(contract, File.ReadAllText(Path.Combine(UnrealSurfaceTests.FindRepository(),
            "tests", "fixtures", "preview", "preview-experience-v1.json")).Trim());
    }

    [Theory]
    [InlineData("../Other")]
    [InlineData("C:/Other")]
    [InlineData("Bad Name")]
    public void InteractivePlanRejectsUnsafeProjectIdentity(string name) =>
        Assert.Throws<ArgumentException>(() => UnrealPreviewPlan.Create(name, PreviewExperienceDefaults.Contract));
}
