using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Application.Documentation;

/// <summary>Case-specific factual prose. Compatibility is limited to declared rig/set metadata.</summary>
internal static class ReadmeCaseSections
{
    internal static IEnumerable<(string Heading, string[] Lines)> Create(ProductManifest manifest, BuildTarget target)
    {
        if (manifest.ProductCase.Equals(ProductCase.Static))
        {
            yield return ("Static model", ["No rig or animation is included."]);
        }

        if (manifest.Rig is not null)
        {
            yield return ("Rig", [$"Type: {manifest.Rig.RigType.CanonicalIdentifier}",
                $"Root bone: {manifest.Rig.Skeleton.Root.Identity}",
                $"Bone count: {manifest.Rig.Skeleton.Bones.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"Reference pose: supplied for all {manifest.Rig.ReferencePose.Bones.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} bones.",
                "Compatibility: use the supplied skeleton; compatibility with other skeletons is not implied."]);
            if (manifest.Animations.Count == 0)
            {
                yield return ("Animation availability", ["No animation clips are included."]);
            }
            else
            {
                string usage = target.Equals(BuildTarget.Unity)
                    ? "Use the supplied clip names in an Animator Controller configured for the supplied rig."
                    : target.Equals(BuildTarget.Unreal)
                    ? "Use the supplied clip names with the imported skeleton in an Animation Blueprint."
                    : "Import the supplied rig and select the named animation takes in your destination application.";
                yield return ("Animation usage", [usage, "Use the measured loop and root-motion settings in the animation table."]);
            }
        }

        if (manifest.ItemSet is not null)
        {
            yield return ("Item set", [$"Items: {manifest.ItemSet.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                "Use the item inventory for member names and attachment slots."]);
            AssembledSetRules? assembly = manifest.ItemSet.AssembledSetRules;
            yield return ("Assembly and compatibility", assembly is null
                ? ["No assembled-set compatibility rules are declared; use members individually."]
                : new[] { $"Assembled members: {assembly.Members.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    $"Unique attachment slots required: {assembly.RequireUniqueAttachmentSlots}" }
                    .Concat(assembly.Members.Select(member => $"Member: {member.ItemId.Value}; slot: {member.AttachmentSlot?.CanonicalIdentifier ?? "None"}"))
                    .Concat(assembly.CompatibilityMetadata.Select(entry => $"{entry.Key.Value}: {entry.Value}")).ToArray());
        }

        if (manifest.ItemCollection is not null)
        {
            yield return ("Item collection", [$"Items: {manifest.ItemCollection.Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                "Members are independent products; use the declaration-ordered inventory to locate each delivered model."]);
        }
    }
}
