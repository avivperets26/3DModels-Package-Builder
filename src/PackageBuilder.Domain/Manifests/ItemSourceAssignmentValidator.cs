using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Manifests;

/// <summary>Validates explicit ownership without filesystem access, heuristic grouping or partial acceptance.</summary>
internal static class ItemSourceAssignmentValidator
{
    /// <summary>Checks exact references and complete ownership against an already validated manifest inventory.</summary>
    internal static IReadOnlyList<ValidationFinding> Validate(
        IReadOnlyList<ItemSourceAssignment?> assignments,
        IReadOnlyList<ItemDefinition>? items,
        IReadOnlyList<SourceAsset> sources)
    {
        if (items is null)
        {
            return [ProductManifest.Finding("MANIFEST_ITEM_SOURCE_CASE_INVALID", "Item source assignments require an item set or collection.")];
        }

        var findings = new List<ValidationFinding>();
        var itemIds = items.Select(item => item.Id.Value).ToHashSet(StringComparer.Ordinal);
        var sourcesByReference = sources.ToDictionary(source => source.LogicalReference, StringComparer.Ordinal);
        var pairs = new HashSet<(string Item, string Source)>();
        var modelOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var mappedItems = new HashSet<string>(StringComparer.Ordinal);
        foreach (ItemSourceAssignment? assignment in assignments)
        {
            if (assignment?.ItemId is null || assignment.SourceReference is null)
            {
                findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_INVALID", "An item source assignment is incomplete."));
                continue;
            }

            string id = assignment.ItemId.Value;
            if (!itemIds.Contains(id) || !sourcesByReference.TryGetValue(assignment.SourceReference, out SourceAsset? source))
            {
                findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_UNKNOWN", "An assignment must reference an exact existing item ID and source reference."));
                continue;
            }

            if (!pairs.Add((id, source.LogicalReference)))
            {
                findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_DUPLICATE", "An item/source pair is declared more than once."));
            }

            if (source.Kind.Equals(SourceAssetKind.Image))
            {
                // Reviewed images may be shared. Content/semantic deduplication belongs to PB-0802.
                continue;
            }

            _ = mappedItems.Add(id);
            if (!modelOwners.TryAdd(source.LogicalReference, id) && modelOwners[source.LogicalReference] != id)
            {
                findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED", $"Model source '{source.LogicalReference}' has multiple item owners. Review and split file-level inputs before mapping."));
            }
        }

        foreach (ItemDefinition item in items.Where(item => !mappedItems.Contains(item.Id.Value)))
        {
            findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED", $"Item '{item.Id.Value}' needs an explicitly reviewed model source."));
        }

        foreach (SourceAsset source in sources.Where(source => !source.Kind.Equals(SourceAssetKind.Image) && !modelOwners.ContainsKey(source.LogicalReference)))
        {
            findings.Add(ProductManifest.Finding("MANIFEST_ITEM_SOURCE_REVIEW_REQUIRED", $"Model source '{source.LogicalReference}' needs an explicitly reviewed item owner."));
        }

        return findings.AsReadOnly();
    }
}
