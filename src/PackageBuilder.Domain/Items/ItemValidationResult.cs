namespace PackageBuilder.Domain.Items;

/// <summary>Identifies why PB-0106 item metadata or a group definition was rejected.</summary>
public enum ItemValidationError
{
    None = 0,
    NullCategoryIdentifier,
    EmptyCategoryIdentifier,
    WhitespaceOnlyCategoryIdentifier,
    MalformedCategoryIdentifier,
    NullSlotIdentifier,
    EmptySlotIdentifier,
    WhitespaceOnlySlotIdentifier,
    MalformedSlotIdentifier,
    NullSharedAssetId,
    NullSharedAssetSource,
    NullItemId,
    NullCategories,
    NullCategory,
    DuplicateCategory,
    NullSharedAssetReferences,
    NullSharedAssetReference,
    DuplicateSharedAssetReference,
    NullRelationshipEndpoint,
    SelfRelationship,
    NullCompatibilityKey,
    NullCompatibilityValue,
    EmptyCompatibilityValue,
    WhitespaceOnlyCompatibilityValue,
    CompatibilityValueEdgeWhitespace,
    CompatibilityValueContainsControlCharacter,
    NullAssembledMemberItemId,
    NullAssembledMembers,
    NullAssembledMember,
    DuplicateAssembledMember,
    NullCompatibilityMetadata,
    NullCompatibilityEntry,
    DuplicateCompatibilityKey,
    NullItems,
    NullItem,
    DuplicateItemId,
    NullRelationships,
    NullRelationship,
    UnknownRelationshipEndpoint,
    DuplicateRelationship,
    NullSharedAssets,
    NullSharedAsset,
    DuplicateSharedAssetId,
    UnknownSharedAssetReference,
    UnreferencedSharedAsset,
    AssembledSetRulesNotAllowed,
    UnknownAssembledMember,
    MissingAssembledMember,
    ContradictoryAssembledMemberSlot,
    ConflictingAttachmentSlot,
}

/// <summary>Represents a task-local expected validation outcome for PB-0106 values.</summary>
public sealed class ItemValidationResult<T>
    where T : class
{
    private ItemValidationResult(bool isValid, T? value, ItemValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public ItemValidationError Error { get; }

    internal static ItemValidationResult<T> Success(T value) =>
        new(true, value, ItemValidationError.None);

    internal static ItemValidationResult<T> Failure(ItemValidationError error) =>
        new(false, null, error);
}
