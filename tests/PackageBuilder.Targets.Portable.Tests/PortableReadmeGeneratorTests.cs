using System.Globalization;
using System.Text;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableReadmeGeneratorTests
{
    [Fact]
    public async Task RendersDeterministicTypedStaticDocumentation()
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.Static, withTexture: true);
        PortableTextureCopyReceipt receipt = await PortableReadmeTestValues.TextureReceiptAsync(
            manifest.Materials[0].Definition.TextureAssignments[0]);
        PortableReadmeRequest request = PortableReadmeTestValues.Request(
            manifest: manifest,
            receipts: [receipt],
            glb: PortableGlbVariant.Standard);

        PortableReadmeResult<PortableReadmeDocument> result = PortableReadmeGenerator.Generate(request);

        Assert.True(result.IsValid);
        Assert.Equal(PortableReadmeError.None, result.Error);
        string text = Assert.IsType<PortableReadmeDocument>(result.Value).Text;
        Assert.Contains("# Silverwing Talonbow\n", text, StringComparison.Ordinal);
        Assert.Contains("Product case: Static model\n", text, StringComparison.Ordinal);
        Assert.Contains("- FBX: SilverwingTalonbow.fbx\n", text, StringComparison.Ordinal);
        Assert.Contains("- GLB: Silverwing_Talonbow.glb\n", text, StringComparison.Ordinal);
        Assert.Contains("T_SilverwingTalonbow_Albedo.png [Albedo, png, 2048x2048, sRGB]", text, StringComparison.Ordinal);
        Assert.Contains("- Width: 1.25 m\n- Height: 2.5 m\n- Depth: 0.75 m\n", text, StringComparison.Ordinal);
        Assert.Contains("- BowMaterial: opaque; textures: Albedo\n", text, StringComparison.Ordinal);
        Assert.Contains("## Rig\n- None\n", text, StringComparison.Ordinal);
        Assert.Contains("## Animations\n- None\n", text, StringComparison.Ordinal);
        Assert.Contains("AI-assisted: Concept generation and texture drafting were AI-assisted.", text, StringComparison.Ordinal);
        Assert.Contains("Email: support@example.com", text, StringComparison.Ordinal);
        Assert.EndsWith("Copyright © 2024-2026 Aviv Perets\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.Equal(Encoding.UTF8.GetBytes(text), result.Value!.Utf8Bytes);
    }

    [Theory]
    [InlineData("static", "Static model")]
    [InlineData("rigged", "Rigged model")]
    [InlineData("rigged-animated", "Rigged animated model")]
    [InlineData("item-set", "Item set")]
    [InlineData("item-collection", "Item collection")]
    public void RendersEveryProductCase(string identifier, string expected)
    {
        ProductCase productCase = ProductCase.TryParse(identifier).Value!;
        PortableReadmeDocument document = Assert.IsType<PortableReadmeDocument>(
            PortableReadmeGenerator.Generate(PortableReadmeTestValues.Request(productCase)).Value);

        Assert.Contains($"Product case: {expected}\n", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersRigAndAnimationMetadata()
    {
        PortableReadmeDocument document = PortableReadmeGenerator.Generate(
            PortableReadmeTestValues.Request(ProductCase.RiggedAnimated, glb: PortableGlbVariant.Rigged)).Value!;

        Assert.Contains("- GLB: Silverwing_Talonbow_rigged.glb", document.Text, StringComparison.Ordinal);
        Assert.Contains("- Type: generic", document.Text, StringComparison.Ordinal);
        Assert.Contains("- Root bone: Root", document.Text, StringComparison.Ordinal);
        Assert.Contains("- Bone count: 2", document.Text, StringComparison.Ordinal);
        Assert.Contains("BowShot: frames 1-31; 30 FPS; 1 s; loop: once; root motion: none", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersUntexturedMaterialsAndUncategorizedItemsExplicitly()
    {
        string materialText = PortableReadmeGenerator.Generate(
            PortableReadmeTestValues.Request(
                manifest: PortableReadmeTestValues.Manifest(
                    ProductCase.Static,
                    withUntexturedMaterial: true))).Value!.Text;
        string itemText = PortableReadmeGenerator.Generate(
            PortableReadmeTestValues.Request(
                manifest: PortableReadmeTestValues.Manifest(
                    ProductCase.ItemSet,
                    uncategorizedItem: true))).Value!.Text;

        Assert.Contains("BowMaterial: opaque; textures: none", materialText, StringComparison.Ordinal);
        Assert.Contains("Helm: uncategorized", itemText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("item-set", "Group type: set", "Helm: equipment")]
    [InlineData("item-collection", "Group type: collection", "Sword: equipment")]
    public void RendersItemInventories(string identifier, string group, string item)
    {
        ProductCase productCase = ProductCase.TryParse(identifier).Value!;
        string text = PortableReadmeGenerator.Generate(PortableReadmeTestValues.Request(productCase)).Value!.Text;

        Assert.Contains(group, text, StringComparison.Ordinal);
        Assert.Contains("Item count: 1", text, StringComparison.Ordinal);
        Assert.Contains(item, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("undeclared", null, "Undeclared")]
    [InlineData("no-ai-assistance", null, "No AI assistance declared")]
    [InlineData("ai-assisted", null, "AI-assisted")]
    public void RendersEveryAiDisclosureState(string identifier, string? detail, string expected)
    {
        AiDisclosureState state = AiDisclosureState.TryParse(identifier).Value!;
        PublisherProfile publisher = PortableReadmeTestValues.Publisher(state, detail);
        string text = PortableReadmeGenerator.Generate(
            PortableReadmeTestValues.Request(publisher: publisher)).Value!.Text;

        Assert.Contains($"## AI disclosure\n- {expected}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingIsCultureIndependentAndDoesNotInventGlb()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            string text = PortableReadmeGenerator.Generate(PortableReadmeTestValues.Request()).Value!.Text;

            Assert.Contains("Width: 1.25 m", text, StringComparison.Ordinal);
            Assert.DoesNotContain("- GLB:", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(0, PortableReadmeError.NullManifest)]
    [InlineData(1, PortableReadmeError.NullPublisherProfile)]
    [InlineData(2, PortableReadmeError.NullNamingProfile)]
    [InlineData(3, PortableReadmeError.NullDimensions)]
    [InlineData(4, PortableReadmeError.NullTextureReceipts)]
    [InlineData(5, PortableReadmeError.NullUsageInstructions)]
    public void RejectsNullRequiredInputs(int index, PortableReadmeError expected)
    {
        object?[] values =
        [
            PortableReadmeTestValues.Manifest(ProductCase.Static),
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            Array.Empty<PortableTextureCopyReceipt?>(),
            new string?[] { "Use safely." },
        ];
        values[index] = null;

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            (PackageBuilder.Domain.Manifests.ProductManifest?)values[0],
            (PublisherProfile?)values[1],
            (PortableNamingProfile?)values[2],
            (PortableModelDimensions?)values[3],
            null,
            (IEnumerable<PortableTextureCopyReceipt?>?)values[4],
            (IEnumerable<string?>?)values[5]);

        AssertFailure(result, expected);
    }

    [Theory]
    [InlineData("null-receipt", PortableReadmeError.NullTextureReceipt)]
    [InlineData("null-usage", PortableReadmeError.NullUsageInstruction)]
    [InlineData("empty-usage", PortableReadmeError.EmptyUsageInstructions)]
    [InlineData("many-usage", PortableReadmeError.TooManyUsageInstructions)]
    [InlineData("invalid-usage", PortableReadmeError.InvalidUsageInstruction)]
    public void RejectsInvalidCollections(string scenario, PortableReadmeError expected)
    {
        IEnumerable<PortableTextureCopyReceipt?> receipts = scenario == "null-receipt" ? [null] : [];
        IEnumerable<string?> usage = scenario switch
        {
            "null-usage" => [null],
            "empty-usage" => [],
            "many-usage" => Enumerable.Repeat<string?>("Use safely.", 33),
            "invalid-usage" => [" has edge whitespace"],
            _ => ["Use safely."],
        };

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            PortableReadmeTestValues.Manifest(ProductCase.Static),
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            receipts,
            usage);

        AssertFailure(result, expected);
    }

    [Fact]
    public void RejectsInvalidDimensionsAndNullGeneratorInput()
    {
        Assert.False(PortableModelDimensions.Create(0, 1, 1).IsValid);
        Assert.False(PortableModelDimensions.Create(1, double.NaN, 1).IsValid);
        Assert.False(PortableModelDimensions.Create(1, 1, double.PositiveInfinity).IsValid);
        AssertFailure(PortableReadmeGenerator.Generate(null), PortableReadmeError.NullManifest);
    }

    [Theory]
    [InlineData("target", PortableReadmeError.ManifestDoesNotTargetPortable)]
    [InlineData("naming", PortableReadmeError.ManifestNamingMismatch)]
    [InlineData("publisher", PortableReadmeError.PublisherProfileMismatch)]
    public void RejectsCrossContractIdentityMismatches(string scenario, PortableReadmeError expected)
    {
        ProductManifest manifest = PortableReadmeTestValues.Manifest(ProductCase.Static);
        if (scenario == "target")
        {
            manifest = PortableReadmeTestValues.Recreate(
                manifest,
                targets: [PackageBuilder.Domain.Targets.BuildTarget.Unity]);
        }

        PortableNamingProfile naming = scenario == "naming"
            ? PortableTestValues.Naming("OtherAsset", "Other_Folder")
            : PortableTestValues.Naming();
        PublisherProfile publisher = scenario == "publisher"
            ? Assert.IsType<PublisherProfile>(
                PublisherProfile.Create(
                    PackageBuilder.Domain.Naming.PublisherRoot.Create("OtherPublisher").Value,
                    PublisherDisplayName.Create("Other Publisher").Value,
                    SupportContact.CreateEmail("support@example.com").Value,
                    CopyrightNotice.Create(
                        CopyrightHolder.Create("Other Publisher").Value,
                        CopyrightYearPolicy.Create(CopyrightYearPolicyKind.SingleYear, 2026).Value).Value,
                    AiDisclosure.Create(AiDisclosureState.Undeclared).Value).Value)
            : PortableReadmeTestValues.Publisher();

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            manifest,
            publisher,
            naming,
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            [],
            ["Use safely."]);

        AssertFailure(result, expected);
    }

    [Theory]
    [InlineData("missing", PortableReadmeError.MissingTextureReceipt)]
    [InlineData("duplicate", PortableReadmeError.DuplicateTextureReceipt)]
    [InlineData("unexpected", PortableReadmeError.UnexpectedTextureReceipt)]
    public async Task RejectsInvalidTextureReceiptCoverage(string scenario, PortableReadmeError expected)
    {
        ProductManifest textured = PortableReadmeTestValues.Manifest(ProductCase.Static, withTexture: true);
        PortableTextureCopyReceipt receipt = await PortableReadmeTestValues.TextureReceiptAsync(
            textured.Materials[0].Definition.TextureAssignments[0]);
        ProductManifest manifest = scenario == "unexpected"
            ? PortableReadmeTestValues.Manifest(ProductCase.Static)
            : textured;
        IEnumerable<PortableTextureCopyReceipt?> receipts = scenario switch
        {
            "missing" => [],
            "duplicate" => [receipt, receipt],
            _ => [receipt],
        };

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            manifest,
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            receipts,
            ["Use safely."]);

        AssertFailure(result, expected);
    }

    [Fact]
    public async Task RendersNormalConventionAndAlternatePublisherFields()
    {
        TextureAssignment normal = PortableTestValues.Texture(TextureRole.Normal);
        ProductManifest staticManifest = PortableReadmeTestValues.Manifest(ProductCase.Static);
        ProductManifest manifest = PortableReadmeTestValues.Recreate(
            staticManifest,
            materials: [PortableReadmeTestValues.Material(normal)],
            sourceAssets: [.. staticManifest.SourceAssets, normal.SourceAsset]);
        PortableTextureCopyReceipt receipt = await PortableReadmeTestValues.TextureReceiptAsync(normal);

        string text = PortableReadmeGenerator.Generate(
            PortableReadmeTestValues.Request(
                manifest: manifest,
                publisher: PortableReadmeTestValues.PublisherWithUrlAndSingleYear(),
                receipts: [receipt])).Value!.Text;

        Assert.Contains("Normal, png, 2048x2048, Linear, open-gl", text, StringComparison.Ordinal);
        Assert.Contains("URL: https://example.com/support", text, StringComparison.Ordinal);
        Assert.EndsWith("Copyright © 2026 Aviv Perets\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidatesAndRendersEveryPortableTextureRoleInCanonicalOrder()
    {
        TextureRole[] roles =
        [
            TextureRole.Roughness,
            TextureRole.Normal,
            TextureRole.Albedo,
            TextureRole.Metallic,
            TextureRole.Emission,
            TextureRole.AmbientOcclusion,
        ];
        TextureAssignment[] assignments =
        [
            .. roles.Select(role => PortableTestValues.Texture(
                role,
                $"textures/{role.CanonicalIdentifier}.png")),
        ];
        ProductManifest baseline = PortableReadmeTestValues.Manifest(ProductCase.Static);
        ProductManifest manifest = PortableReadmeTestValues.Recreate(
            baseline,
            materials:
            [
                .. assignments.Select((assignment, index) =>
                    PortableReadmeTestValues.Material(assignment, $"Material{index}")),
            ],
            sourceAssets: [.. baseline.SourceAssets, .. assignments.Select(value => value.SourceAsset)]);
        var receipts = new List<PortableTextureCopyReceipt>();
        foreach (TextureAssignment assignment in assignments)
        {
            receipts.Add(await PortableReadmeTestValues.TextureReceiptAsync(assignment));
        }

        PortableReadmeDocument document = Assert.IsType<PortableReadmeDocument>(
            PortableReadmeGenerator.Generate(
                PortableReadmeTestValues.Request(manifest: manifest, receipts: receipts)).Value);

        string[] renderedRoles = [.. document.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(static line => line.StartsWith("- Texture:", StringComparison.Ordinal))];
        Assert.Equal(6, renderedRoles.Length);
        Assert.Contains("Albedo", renderedRoles[0], StringComparison.Ordinal);
        Assert.Contains("Roughness", renderedRoles[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllowsOneSharedCanonicalTextureAcrossMaterials()
    {
        TextureAssignment assignment = PortableTestValues.Texture();
        ProductManifest baseline = PortableReadmeTestValues.Manifest(ProductCase.Static);
        ProductManifest manifest = PortableReadmeTestValues.Recreate(
            baseline,
            materials:
            [
                PortableReadmeTestValues.Material(assignment, "MaterialA"),
                PortableReadmeTestValues.Material(assignment, "MaterialB"),
            ],
            sourceAssets: [.. baseline.SourceAssets, assignment.SourceAsset]);
        PortableTextureCopyReceipt receipt = await PortableReadmeTestValues.TextureReceiptAsync(assignment);

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            manifest,
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            [receipt],
            ["Use safely."]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("trailing ")]
    [InlineData("contains\u0001control")]
    public void RejectsEveryInvalidUsageInstructionShape(string instruction)
    {
        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            PortableReadmeTestValues.Manifest(ProductCase.Static),
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            [],
            [instruction]);

        AssertFailure(result, PortableReadmeError.InvalidUsageInstruction);
    }

    [Fact]
    public void RejectsOverlongUsageInstruction()
    {
        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            PortableReadmeTestValues.Manifest(ProductCase.Static),
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            [],
            [new string('a', 513)]);

        AssertFailure(result, PortableReadmeError.InvalidUsageInstruction);
    }

    [Fact]
    public void RejectsDistinctSourcesThatWouldCollideOnOneCanonicalRole()
    {
        ProductManifest baseline = PortableReadmeTestValues.Manifest(ProductCase.Static, withTexture: true);
        TextureAssignment otherTexture = PortableTestValues.Texture(
            logicalReference: "textures/other.png");
        ProductManifest colliding = PortableReadmeTestValues.Recreate(
            baseline,
            materials:
            [
                baseline.Materials[0],
                PortableReadmeTestValues.Material(otherTexture, "OtherMaterial"),
            ],
            sourceAssets: [.. baseline.SourceAssets, otherTexture.SourceAsset]);

        PortableReadmeResult<PortableReadmeRequest> result = PortableReadmeRequest.Create(
            colliding,
            PortableReadmeTestValues.Publisher(),
            PortableTestValues.Naming(),
            PortableModelDimensions.Create(1, 1, 1).Value,
            null,
            [],
            ["Use safely."]);

        AssertFailure(result, PortableReadmeError.TextureRoleCollision);
    }

    private static void AssertFailure<T>(PortableReadmeResult<T> result, PortableReadmeError expected)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
    }
}
