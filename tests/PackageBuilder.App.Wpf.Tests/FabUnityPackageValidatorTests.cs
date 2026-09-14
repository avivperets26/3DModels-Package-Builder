using System.Collections.Immutable;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

public sealed class FabUnityPackageValidatorTests
{
    [Fact]
    public void CompleteEngineEvidenceAndInventoryPassWithoutReimplementingEditorChecks()
    {
        FabArtifactValidation result = Validate(Package());
        Assert.True(result.Passed);
        Assert.Equal(Profile().Sha256, result.ProfileSha256);
    }

    [Theory]
    [InlineData("missing-import", "FAB_EVIDENCE_INVALID")]
    [InlineData("stale-import", "FAB_EVIDENCE_INVALID")]
    [InlineData("wrong-stage", "FAB_EVIDENCE_INVALID")]
    [InlineData("target-error", "UNITY_TEST_ERROR")]
    [InlineData("wrong-root", "FAB_UNITY_ROOT_INVALID")]
    [InlineData("missing-file", "FAB_UNITY_INVENTORY_INVALID")]
    [InlineData("duplicate-payload", "FAB_UNITY_DUPLICATE")]
    [InlineData("unused", "FAB_UNITY_REDUNDANT")]
    [InlineData("missing-dependency", "FAB_UNITY_DEPENDENCY_INVALID")]
    [InlineData("wrong-format", "FAB_UNITY_FORMAT_INVALID")]
    public void CorruptOrIncompleteEvidenceBlocks(string mutation, string code)
    {
        FabUnityPackageInspection package = Package();
        package = mutation switch
        {
            "missing-import" => package with { CleanImport = package.CleanImport with { Completed = false } },
            "stale-import" => package with { CleanImport = package.CleanImport with { Content = Identity("other package"u8) } },
            "wrong-stage" => package with { PackageValidation = package.PackageValidation with { Stage = "portable-target" } },
            "target-error" => package with { PackageValidation = package.PackageValidation with { Findings = Failure() } },
            "wrong-root" => package with { ProductRoot = "Assets/Other" },
            "missing-file" => package with { Assets = [.. package.Assets.Skip(1)] },
            "duplicate-payload" => package with { Assets = package.Assets.SetItem(1, package.Assets[1] with { Content = package.Assets[0].Content }) },
            "unused" => package with { Assets = package.Assets.SetItem(0, package.Assets[0] with { IsUsed = false }) },
            "missing-dependency" => package with { Assets = package.Assets.SetItem(0, package.Assets[0] with { Dependencies = ["Assets/Missing.mat"] }) },
            _ => package with { FileName = "Product.zip" },
        };
        Has(Validate(package), code);
    }

    [Theory]
    [InlineData("Assets/Product/../Outside.prefab", "FAB_UNITY_ROOT_INVALID")]
    [InlineData("Assets/Product/AssetStoreTools/Tool.cs", "FAB_UNITY_REDUNDANT")]
    [InlineData("Assets/Product/Source/Hidden.zip", "FAB_UNITY_NESTED_CONTENT")]
    [InlineData("Assets/Product/Movies/Help.mp4", "FAB_UNITY_NESTED_CONTENT")]
    [InlineData("Assets/Product/Source/CON.fbx", "FAB_UNITY_ROOT_INVALID")]
    public void UnsafeAndRedundantContentIsRejectedEvenWhenPlanned(string path, string code) =>
        Has(Validate(ReplaceFirstPath(Package(), path)), code);

    [Fact]
    public void UnityPathLimitIsStrictAndComesFromSelectedProfile()
    {
        FabUnityPackageInspection package = Package();
        const string Prefix = "Assets/Product/Models/";
        FabUnityPackageInspection valid = ReplaceFirstPath(package, Prefix + new string('A', 149 - Prefix.Length - 4) + ".fbx");
        Assert.True(Validate(valid).Passed);
        Has(Validate(ReplaceFirstPath(package, Prefix + new string('A', 150 - Prefix.Length - 4) + ".fbx")), "FAB_LIMIT_EXCEEDED");
        Has(FabUnityPackageValidator.Validate(Profile(root => Limit(root, "unity-path-length", 20)), Context(), package, Token), "FAB_LIMIT_EXCEEDED");
    }

    [Fact]
    public void ExternalDependenciesNeedExactRootsVersionsInstallAndDisclosure()
    {
        FabUnityPackageInspection package = Package();
        package = package with
        {
            Assets = package.Assets.SetItem(0, package.Assets[0] with { Dependencies = ["Packages/com.example.shader/Shader.shader"] }),
            ExternalDependencies = [new("Packages/com.example.shader", "1.2.0", true, true)],
        };
        Assert.True(Validate(package).Passed);
        Has(Validate(package with { ExternalDependencies = [package.ExternalDependencies[0] with { Disclosed = false }] }), "FAB_UNITY_DEPENDENCY_INVALID");
        Has(Validate(package with { ExternalDependencies = [package.ExternalDependencies[0] with { Installed = false }] }), "FAB_UNITY_DEPENDENCY_INVALID");
        Has(Validate(package with { ExternalDependencies = [package.ExternalDependencies[0] with { Root = "Packages/com.example" }] }), "FAB_UNITY_DEPENDENCY_INVALID");
    }

    [Theory]
    [InlineData(".unity", "FAB_UNITY_DEMO_MISSING")]
    [InlineData(".md", "FAB_UNITY_DOCS_MISSING")]
    public void RequiredSceneAndDocumentationAreChecked(string extension, string code)
    {
        FabUnityPackageInspection package = Package();
        ImmutableArray<FabUnityAsset> assets = [.. package.Assets.Where(asset => !asset.Path.EndsWith(extension, StringComparison.Ordinal))];
        Has(Validate(package with { Assets = assets, ExpectedAssets = [.. assets.Select(asset => asset.Path)] }), code);
    }

    [Fact]
    public void UnresolvedProfileRulesBlockEvenGoodUnityEvidence() =>
        Has(FabUnityPackageValidator.Validate(FabRequirementsBaseline.ArtifactValidationProfile, Context(), Package(), Token), "FAB_RULE_REVIEW_REQUIRED");

    [Fact]
    public void CancelledValidationThrowsInsteadOfReturningPass()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _ = Assert.ThrowsAny<OperationCanceledException>(() => FabUnityPackageValidator.Validate(Profile(), Context(), Package(), cancelled.Token));
    }

    private static FabArtifactValidation Validate(FabUnityPackageInspection package) =>
        FabUnityPackageValidator.Validate(Profile(), Context(), package, Token);

    private static FabUnityPackageInspection ReplaceFirstPath(FabUnityPackageInspection package, string path)
    {
        ImmutableArray<FabUnityAsset> assets = package.Assets.SetItem(0, package.Assets[0] with { Path = path });
        return package with { Assets = assets, ExpectedAssets = [.. assets.Select(asset => asset.Path)] };
    }

    private static FabUnityPackageInspection Package()
    {
        string[] names = ["Assets/Product/Models/Model.fbx", "Assets/Product/Scenes/Overview.unity", "Assets/Product/Documentation/README.md"];
        ImmutableArray<FabUnityAsset> assets = [.. names.Select(path => new FabUnityAsset(path, Identity(System.Text.Encoding.UTF8.GetBytes(path)), [], true))];
        return new(Id(), Identity("synthetic unity package"u8), "Product.unitypackage", "Assets/Product", [.. names], assets, [],
            Evidence(Context(), Id(), Identity("synthetic unity package"u8), "unity-package"),
            Evidence(Context(), Id(), Identity("synthetic unity package"u8), "unity-clean-import"));
    }
}
