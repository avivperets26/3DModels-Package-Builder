using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents an extensible renderer- and marketplace-independent item category identifier.
/// </summary>
public sealed class ItemCategory : IEquatable<ItemCategory>
{
    private ItemCategory(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    public string CanonicalIdentifier { get; }

    /// <summary>
    /// Creates a lowercase ASCII word identifier separated by single hyphens. No category
    /// registry is hard-coded, so later domains may introduce new semantic categories.
    /// </summary>
    public static ItemValidationResult<ItemCategory> Create(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        return error switch
        {
            CanonicalIdentifierParseError.None =>
                ItemValidationResult<ItemCategory>.Success(new ItemCategory(identifier!)),
            CanonicalIdentifierParseError.Null =>
                ItemValidationResult<ItemCategory>.Failure(
                    ItemValidationError.NullCategoryIdentifier),
            CanonicalIdentifierParseError.Empty =>
                ItemValidationResult<ItemCategory>.Failure(
                    ItemValidationError.EmptyCategoryIdentifier),
            CanonicalIdentifierParseError.WhitespaceOnly =>
                ItemValidationResult<ItemCategory>.Failure(
                    ItemValidationError.WhitespaceOnlyCategoryIdentifier),
            _ => ItemValidationResult<ItemCategory>.Failure(
                ItemValidationError.MalformedCategoryIdentifier),
        };
    }

    public bool Equals(ItemCategory? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ItemCategory other && Equals(other);

    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    public override string ToString() => CanonicalIdentifier;
}
