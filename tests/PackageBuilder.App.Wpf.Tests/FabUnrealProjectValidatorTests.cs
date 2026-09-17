using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

[Trait("Task", "PB-1006")]
[Trait("Task", "PB-1115")]
public sealed class FabUnrealProjectValidatorTests
{
    internal static FabUnrealProjectInspection Project(FabValidationContext context, byte[] bytes)
    {
        string[] paths = ["Product.uproject", "Config/DefaultEngine.ini", "Content/Product/Maps/L_Overview.umap",
            "Content/Product/Documentation/README.md", "Content/Product/Meshes/SM_Product.uasset"];
        ArtifactContentIdentity content = Identity(bytes);
        return new(Id("unreal"), content, "Product_Unreal.zip", "Product", "5.8.0", [.. paths],
            [.. paths.Select(path => new FabUnrealFile(path, Identity(Encoding.UTF8.GetBytes(path)), true, false))],
            Evidence(context, Id("unreal"), content, "unreal-project"), Evidence(context, Id("unreal"), content, "unreal-clean-reopen"),
            Evidence(context, Id("unreal"), content, "unreal-logs"));
    }

    [Fact]
    public void ExactNativeObservationPassesAndPinsProfile()
    {
        FabArtifactValidation result = FabUnrealProjectValidator.Validate(Profile(), Context("unreal"), Project(Context("unreal"), "zip"u8.ToArray()), Token);
        Assert.True(result.Passed);
        Assert.Equal(Profile().Sha256, result.ProfileSha256);
    }

    [Theory]
    [InlineData("Saved/log.txt")]
    [InlineData("Intermediate/test.uasset")]
    [InlineData("DerivedDataCache/test.uasset")]
    [InlineData("Binaries/module.dll")]
    [InlineData("Plugins/Other/Other.uplugin")]
    [InlineData("Other.uproject")]
    [InlineData("Content/Other/Meshes/SM_Other.uasset")]
    [InlineData("Content/Product/Meshes/wrong.uasset")]
    [InlineData("Content/Product/Textures/not-a-texture.uasset")]
    [InlineData("Content/Product/Textures/../bad.uasset")]
    [InlineData("C:/source.uasset")]
    public void UnplannedOrForbiddenPathsBlockEvenWhenDeclaredInInventory(string path)
    {
        FabUnrealProjectInspection project = Project(Context("unreal"), "zip"u8.ToArray());
        project = project with { Files = [.. project.Files, new(path, Identity("bad"u8), true, false)], ExpectedFiles = [.. project.ExpectedFiles, path] };
        Has(FabUnrealProjectValidator.Validate(Profile(), Context("unreal"), project, Token), "FAB_UNREAL_LAYOUT_INVALID");
    }

    [Theory]
    [InlineData(0, "FAB_UNREAL_PROJECT_MISSING")]
    [InlineData(1, "FAB_UNREAL_PROJECT_MISSING")]
    [InlineData(2, "FAB_UNREAL_OVERVIEW_MISSING")]
    [InlineData(3, "FAB_UNREAL_DOCS_MISSING")]
    public void MissingRequiredComponentsBlock(int index, string code)
    {
        FabUnrealProjectInspection project = Project(Context("unreal"), "zip"u8.ToArray());
        Has(FabUnrealProjectValidator.Validate(Profile(), Context("unreal"), project with { Files = project.Files.RemoveAt(index) }, Token), code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UnusedAssetsAndRedirectorsBlock(bool used, bool redirector)
    {
        FabUnrealProjectInspection project = Project(Context("unreal"), "zip"u8.ToArray());
        Has(FabUnrealProjectValidator.Validate(Profile(), Context("unreal"), project with
        { Files = project.Files.SetItem(4, project.Files[4] with { IsUsed = used, IsRedirector = redirector }) }, Token), "FAB_UNREAL_UNUSED_OR_REDIRECTOR");
    }

    [Fact]
    public void StaleMissingAndFailedNativeGatesBlock()
    {
        FabValidationContext context = Context("unreal");
        FabUnrealProjectInspection project = Project(context, "zip"u8.ToArray());
        foreach (FabUnrealProjectInspection changed in new[] {
            project with { ProjectValidation = project.ProjectValidation with { Completed = false } },
            project with { CleanReopen = project.CleanReopen with { Content = Identity("changed"u8) } },
            project with { LogValidation = project.LogValidation with { ProductKey = "Other" } },
            project with { CleanReopen = null! } })
        { Has(FabUnrealProjectValidator.Validate(Profile(), context, changed, Token), "FAB_EVIDENCE_INVALID"); }
        Has(FabUnrealProjectValidator.Validate(Profile(), context, project with { LogValidation = project.LogValidation with { Findings = Failure() } }, Token), "UNITY_TEST_ERROR");
        Has(FabUnrealProjectValidator.Validate(Profile(), context, null, Token), "FAB_UNREAL_EVIDENCE_INVALID");
    }

    [Fact]
    public void LimitUsesUncompressedProjectSizeAndUnknownRulesBlock()
    {
        FabUnrealProjectInspection project = Project(Context("unreal"), "z"u8.ToArray());
        Has(FabUnrealProjectValidator.Validate(Profile(root => Limit(root, "unreal-project-bytes", 20)), Context("unreal"), project, Token), "FAB_LIMIT_EXCEEDED");
        Has(FabUnrealProjectValidator.Validate(Profile(root => root["rules"]!.AsArray().Single(rule => rule!["id"]!.GetValue<string>() == "unreal-layout")!["status"] = "unresolved"), Context("unreal"), project, Token), "FAB_RULE_REVIEW_REQUIRED");
        _ = Assert.Throws<OperationCanceledException>(() => FabUnrealProjectValidator.Validate(Profile(), Context("unreal"), project, new(true)));
    }

    [Fact]
    public async Task ComposerIncludesOnlyInspectedUnrealAndPinnedEngineMetadata()
    {
        using var fixture = new FabReleaseFixtures(FabRequirementsBaseline.UnrealStaticReleaseProfile);
        FabValidationContext context = fixture.Request.Context with { Listing = fixture.Request.Context.Listing with { Formats = ["fbx", "unity", "unreal"] } };
        byte[] bytes = "synthetic trusted archive observation"u8.ToArray();
        FabReleaseRequest request = fixture.Request with
        {
            Context = context,
            Unreal = Project(context, bytes),
            Sources = [.. fixture.Request.Sources, FabReleaseFixtures.Source(context, "unreal", "unreal", "Product_Unreal.zip", bytes)]
        };
        using var output = new MemoryStream();
        FabComposedRelease result = await fixture.Composer().ComposeAsync(request, output, Token);
        Assert.True(result.IsSuccess, string.Join(",", result.Validation.Findings.Select(f => f.Code.Value)));
        using var zip = new ZipArchive(output, ZipArchiveMode.Read, true);
        Assert.NotNull(zip.GetEntry("Product/1.0.0/Unreal/Product_Unreal.zip"));
        using var listing = JsonDocument.Parse(zip.GetEntry("Product/1.0.0/listing.json")!.Open());
        Assert.Contains(listing.RootElement.GetProperty("engineVersions").EnumerateArray(), e => e.GetProperty("engine").GetString() == "unreal" && e.GetProperty("version").GetString() == "5.8.0");
        foreach (FabReleaseRequest changed in new[] {
            request with { Unreal = null },
            request with { Unreal = request.Unreal! with { EngineVersion = "5.8.2" } },
            request with { Sources = fixture.Request.Sources },
            request with { Unreal = request.Unreal! with { Content = Identity("stale"u8) } },
            request with { Context = fixture.Request.Context } })
        {
            using var rejected = new MemoryStream();
            Assert.False((await fixture.Composer().ComposeAsync(changed, rejected, Token)).IsSuccess);
            Assert.Equal(0, rejected.Length);
        }
    }
}
