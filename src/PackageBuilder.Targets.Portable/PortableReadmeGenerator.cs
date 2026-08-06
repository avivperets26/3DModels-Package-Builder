using System.Globalization;
using System.Text;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Targets.Portable;

/// <summary>Renders marketplace-neutral case-aware portable documentation from typed data only.</summary>
public static class PortableReadmeGenerator
{
    private static readonly Dictionary<string, string> _caseNames =
        new(StringComparer.Ordinal)
        {
            ["static"] = "Static model",
            ["rigged"] = "Rigged model",
            ["rigged-animated"] = "Rigged animated model",
            ["item-set"] = "Item set",
            ["item-collection"] = "Item collection",
        };

    public static PortableReadmeResult<PortableReadmeDocument> Generate(PortableReadmeRequest? request)
    {
        if (request is null)
        {
            return PortableReadmeResult<PortableReadmeDocument>.Failure(PortableReadmeError.NullManifest);
        }

        var text = new StringBuilder(2048);
        AppendIdentity(text, request);
        AppendFormats(text, request);
        AppendDimensions(text, request.Dimensions);
        AppendMaterials(text, request.Manifest);
        AppendRig(text, request.Manifest);
        AppendAnimations(text, request.Manifest);
        AppendItems(text, request.Manifest);
        AppendUsage(text, request.UsageInstructions);
        AppendPublisher(text, request.PublisherProfile);
        return PortableReadmeResult<PortableReadmeDocument>.Success(
            new PortableReadmeDocument(text.ToString()));
    }

    private static void AppendIdentity(StringBuilder text, PortableReadmeRequest request)
    {
        _ = text.Append("# ").Append(request.Manifest.DisplayName.Value).Append('\n')
            .Append('\n')
            .Append("Product ID: ").Append(request.Manifest.AssetId.Value).Append('\n')
            .Append("Product case: ").Append(CaseName(request.Manifest.ProductCase.CanonicalIdentifier)).Append('\n')
            .Append("Version: ").Append(request.Manifest.Version.Value).Append('\n')
            .Append("Publisher: ").Append(request.PublisherProfile.DisplayName.Value)
            .Append(" (").Append(request.PublisherProfile.Root.Value).Append(")\n");
    }

    private static void AppendFormats(StringBuilder text, PortableReadmeRequest request)
    {
        _ = text.Append("\n## Delivered files\n")
            .Append("- FBX: ").Append(request.NamingProfile.FbxFileName).Append('\n');
        if (request.GlbVariant is not null)
        {
            _ = text.Append("- GLB: ")
                .Append(request.NamingProfile.GetGlbFileName(request.GlbVariant).Value)
                .Append('\n');
        }

        _ = text.Append("- FBX archive: ").Append(request.NamingProfile.FbxArchiveFileName).Append('\n')
            .Append("- README: ").Append(request.NamingProfile.ProductReadmeFileName).Append('\n');
        foreach (PortableTextureCopyReceipt receipt in request.TextureReceipts)
        {
            _ = text.Append("- Texture: ").Append(receipt.FileName)
                .Append(" [").Append(receipt.Role.DisplayName)
                .Append(", ").Append(receipt.Format.Identifier)
                .Append(", ").Append(receipt.Width.ToString(CultureInfo.InvariantCulture))
                .Append('x').Append(receipt.Height.ToString(CultureInfo.InvariantCulture))
                .Append(", ").Append(receipt.ColourSpace.DisplayName);
            if (receipt.NormalConvention is not null)
            {
                _ = text.Append(", ").Append(receipt.NormalConvention.CanonicalIdentifier);
            }

            _ = text.Append("]\n");
        }
    }

    private static void AppendDimensions(StringBuilder text, PortableModelDimensions dimensions)
    {
        _ = text.Append("\n## Dimensions\n")
            .Append("- Width: ").Append(Measure(dimensions.Width)).Append(" m\n")
            .Append("- Height: ").Append(Measure(dimensions.Height)).Append(" m\n")
            .Append("- Depth: ").Append(Measure(dimensions.Depth)).Append(" m\n");
    }

    private static void AppendMaterials(StringBuilder text, ProductManifest manifest)
    {
        _ = text.Append("\n## Materials\n");
        if (manifest.Materials.Count == 0)
        {
            _ = text.Append("- None declared\n");
            return;
        }

        foreach (ManifestMaterial material in manifest.Materials)
        {
            string roles = material.Definition.TextureAssignments.Count == 0
                ? "none"
                : string.Join(", ", material.Definition.TextureAssignments.Select(static value => value.Role.DisplayName));
            _ = text.Append("- ").Append(material.Id.Value)
                .Append(": ").Append(material.Definition.SurfaceMode.CanonicalIdentifier)
                .Append("; textures: ").Append(roles).Append('\n');
        }
    }

    private static void AppendRig(StringBuilder text, ProductManifest manifest)
    {
        _ = text.Append("\n## Rig\n");
        if (manifest.Rig is null)
        {
            _ = text.Append("- None\n");
            return;
        }

        _ = text.Append("- Type: ").Append(manifest.Rig.RigType.CanonicalIdentifier).Append('\n')
            .Append("- Root bone: ").Append(manifest.Rig.Skeleton.Root.Identity).Append('\n')
            .Append("- Bone count: ").Append(manifest.Rig.Skeleton.Bones.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static void AppendAnimations(StringBuilder text, ProductManifest manifest)
    {
        _ = text.Append("\n## Animations\n");
        if (manifest.Animations.Count == 0)
        {
            _ = text.Append("- None\n");
            return;
        }

        foreach (AnimationDefinition animation in manifest.Animations)
        {
            _ = text.Append("- ").Append(animation.Name)
                .Append(": frames ").Append(animation.StartFrame.ToString(CultureInfo.InvariantCulture))
                .Append('-').Append(animation.EndFrame.ToString(CultureInfo.InvariantCulture))
                .Append("; ").Append(Measure(animation.FramesPerSecond)).Append(" FPS")
                .Append("; ").Append(Measure(animation.DurationSeconds)).Append(" s")
                .Append("; loop: ").Append(animation.LoopBehavior.CanonicalIdentifier)
                .Append("; root motion: ").Append(animation.RootMotionStatus.CanonicalIdentifier)
                .Append('\n');
        }
    }

    private static void AppendItems(StringBuilder text, ProductManifest manifest)
    {
        _ = text.Append("\n## Items\n");
        IReadOnlyList<ItemDefinition>? items = manifest.ItemSet?.Items ?? manifest.ItemCollection?.Items;
        if (items is null)
        {
            _ = text.Append("- Not applicable\n");
            return;
        }

        _ = text.Append("- Group type: ")
            .Append(manifest.ItemSet is null ? "collection" : "set")
            .Append('\n')
            .Append("- Item count: ").Append(items.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (ItemDefinition item in items)
        {
            string categories = item.Categories.Count == 0
                ? "uncategorized"
                : string.Join(", ", item.Categories.Select(static category => category.CanonicalIdentifier));
            _ = text.Append("- ").Append(item.Id.Value).Append(": ").Append(categories).Append('\n');
        }
    }

    private static void AppendUsage(StringBuilder text, IReadOnlyList<string> instructions)
    {
        _ = text.Append("\n## Usage\n");
        foreach (string instruction in instructions)
        {
            _ = text.Append("- ").Append(instruction).Append('\n');
        }
    }

    private static void AppendPublisher(StringBuilder text, PublisherProfile publisher)
    {
        _ = text.Append("\n## AI disclosure\n- ")
            .Append(AiText(publisher.AiDisclosure)).Append('\n')
            .Append("\n## Support\n- ")
            .Append(publisher.SupportContact.Kind == SupportContactKind.Email ? "Email: " : "URL: ")
            .Append(publisher.SupportContact.Value).Append('\n')
            .Append("\n## Copyright\n- Copyright © ")
            .Append(CopyrightYears(publisher.Copyright.YearPolicy))
            .Append(' ').Append(publisher.Copyright.Holder.Value).Append('\n');
    }

    private static string CaseName(string identifier) => _caseNames[identifier];

    private static string AiText(AiDisclosure disclosure)
    {
        string state = disclosure.State.Equals(AiDisclosureState.Undeclared)
            ? "Undeclared"
            : disclosure.State.Equals(AiDisclosureState.NoAiAssistance)
            ? "No AI assistance declared"
            : "AI-assisted";
        return disclosure.Text is null ? state : $"{state}: {disclosure.Text}";
    }

    private static string CopyrightYears(CopyrightYearPolicy policy) =>
        policy.StartYear.HasValue ? $"{policy.StartYear.Value}-{policy.Year}" : policy.Year.ToString(CultureInfo.InvariantCulture);

    private static string Measure(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
