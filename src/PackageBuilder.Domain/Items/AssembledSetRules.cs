using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents optional assembled-set membership, slot uniqueness, and compatibility metadata.
/// </summary>
public sealed class AssembledSetRules : IEquatable<AssembledSetRules>
{
    private AssembledSetRules(
        ReadOnlyCollection<AssembledSetMember> members,
        bool requireUniqueAttachmentSlots,
        ReadOnlyCollection<CompatibilityMetadataEntry> compatibilityMetadata)
    {
        Members = members;
        RequireUniqueAttachmentSlots = requireUniqueAttachmentSlots;
        CompatibilityMetadata = compatibilityMetadata;
    }

    /// <summary>Gets members in deterministic ordinal item-ID order.</summary>
    public IReadOnlyList<AssembledSetMember> Members { get; }

    public bool RequireUniqueAttachmentSlots { get; }

    /// <summary>Gets compatibility entries in deterministic ordinal key order.</summary>
    public IReadOnlyList<CompatibilityMetadataEntry> CompatibilityMetadata { get; }

    public static ItemValidationResult<AssembledSetRules> Create(
        IEnumerable<AssembledSetMember?>? members,
        bool requireUniqueAttachmentSlots,
        IEnumerable<CompatibilityMetadataEntry?>? compatibilityMetadata)
    {
        if (members is null)
        {
            return Failure(ItemValidationError.NullAssembledMembers);
        }

        var byItem = new Dictionary<string, AssembledSetMember>(StringComparer.Ordinal);
        foreach (AssembledSetMember? member in members)
        {
            if (member is null)
            {
                return Failure(ItemValidationError.NullAssembledMember);
            }

            if (!byItem.TryAdd(member.ItemId.Value, member))
            {
                return Failure(ItemValidationError.DuplicateAssembledMember);
            }
        }

        if (compatibilityMetadata is null)
        {
            return Failure(ItemValidationError.NullCompatibilityMetadata);
        }

        var byKey = new Dictionary<string, CompatibilityMetadataEntry>(StringComparer.Ordinal);
        foreach (CompatibilityMetadataEntry? entry in compatibilityMetadata)
        {
            if (entry is null)
            {
                return Failure(ItemValidationError.NullCompatibilityEntry);
            }

            if (!byKey.TryAdd(entry.Key.Value, entry))
            {
                return Failure(ItemValidationError.DuplicateCompatibilityKey);
            }
        }

        AssembledSetMember[] orderedMembers =
        [
            .. byItem.Values.OrderBy(member => member.ItemId.Value, StringComparer.Ordinal),
        ];
        CompatibilityMetadataEntry[] orderedMetadata =
        [
            .. byKey.Values.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal),
        ];
        return ItemValidationResult<AssembledSetRules>.Success(
            new AssembledSetRules(
                Array.AsReadOnly(orderedMembers),
                requireUniqueAttachmentSlots,
                Array.AsReadOnly(orderedMetadata)));
    }

    public bool Equals(AssembledSetRules? other) =>
        other is not null &&
        RequireUniqueAttachmentSlots == other.RequireUniqueAttachmentSlots &&
        Members.SequenceEqual(other.Members) &&
        CompatibilityMetadata.SequenceEqual(other.CompatibilityMetadata);

    public override bool Equals(object? obj) => obj is AssembledSetRules other && Equals(other);

    public override int GetHashCode()
    {
        StableItemHash hash = StableItemHash.Create().Add(RequireUniqueAttachmentSlots);
        foreach (AssembledSetMember member in Members)
        {
            hash = hash
                .Add(member.ItemId.Value)
                .Add(member.AttachmentSlot?.CanonicalIdentifier ?? string.Empty);
        }

        foreach (CompatibilityMetadataEntry entry in CompatibilityMetadata)
        {
            hash = hash.Add(entry.Key.Value).Add(entry.Value);
        }

        return hash.ToHashCode();
    }

    private static ItemValidationResult<AssembledSetRules> Failure(
        ItemValidationError error) =>
        ItemValidationResult<AssembledSetRules>.Failure(error);
}
