using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Products;

/// <summary>
/// Identifies one supported product case without imposing engine or item-manifest settings.
/// </summary>
public sealed class ProductCase : IEquatable<ProductCase>
{
    private ProductCase(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    /// <summary>Gets the case for a model without a rig and without animation.</summary>
    public static ProductCase Static { get; } = new("static");

    /// <summary>Gets the case for a model with a rig but without animation.</summary>
    public static ProductCase Rigged { get; } = new("rigged");

    /// <summary>Gets the case for a model with a rig and animation.</summary>
    public static ProductCase RiggedAnimated { get; } = new("rigged-animated");

    /// <summary>
    /// Gets the case for related items intended to form a coordinated or assembled set.
    /// Item rig and animation details remain part of later item manifests.
    /// </summary>
    public static ProductCase ItemSet { get; } = new("item-set");

    /// <summary>
    /// Gets the case for multiple independently usable items grouped as a collection.
    /// Item rig and animation details remain part of later item manifests.
    /// </summary>
    public static ProductCase ItemCollection { get; } = new("item-collection");

    private static readonly ReadOnlyCollection<ProductCase> _all = Array.AsReadOnly<ProductCase>(
        [
            Static,
            Rigged,
            RiggedAnimated,
            ItemSet,
            ItemCollection,
        ]);

    /// <summary>Gets every supported case in stable canonical order.</summary>
    public static IReadOnlyList<ProductCase> All => _all;

    /// <summary>Gets the lowercase, hyphen-separated canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Parses an exact ordinal canonical identifier without throwing for expected input.</summary>
    /// <param name="identifier">The canonical identifier to parse.</param>
    /// <returns>The matching singleton or an explicit expected-input error.</returns>
    public static CanonicalIdentifierParseResult<ProductCase> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<ProductCase>.Failure(error);
        }

        foreach (ProductCase productCase in All)
        {
            if (string.Equals(
                identifier,
                productCase.CanonicalIdentifier,
                StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<ProductCase>.Success(productCase);
            }
        }

        return CanonicalIdentifierParseResult<ProductCase>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(ProductCase? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ProductCase other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
