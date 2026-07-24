using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Items;

/// <summary>Represents one extensible assembled-set compatibility metadata entry.</summary>
public sealed class CompatibilityMetadataEntry : IEquatable<CompatibilityMetadataEntry>
{
    private CompatibilityMetadataEntry(InternalAssetId key, string value)
    {
        Key = key;
        Value = value;
    }

    public InternalAssetId Key { get; }

    public string Value { get; }

    public static ItemValidationResult<CompatibilityMetadataEntry> Create(
        InternalAssetId? key,
        string? value)
    {
        if (key is null)
        {
            return ItemValidationResult<CompatibilityMetadataEntry>.Failure(
                ItemValidationError.NullCompatibilityKey);
        }

        ItemValidationError error = ValidateValue(value);
        return error == ItemValidationError.None
            ? ItemValidationResult<CompatibilityMetadataEntry>.Success(
                new CompatibilityMetadataEntry(key, value!))
            : ItemValidationResult<CompatibilityMetadataEntry>.Failure(error);
    }

    public bool Equals(CompatibilityMetadataEntry? other) =>
        other is not null &&
        Key.Equals(other.Key) &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CompatibilityMetadataEntry other && Equals(other);

    public override int GetHashCode() =>
        StableItemHash.Create().Add(Key.Value).Add(Value).ToHashCode();

    private static ItemValidationError ValidateValue(string? value) =>
        value is null
            ? ItemValidationError.NullCompatibilityValue
            : value.Length == 0
            ? ItemValidationError.EmptyCompatibilityValue
            : string.IsNullOrWhiteSpace(value)
            ? ItemValidationError.WhitespaceOnlyCompatibilityValue
            : char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
            ? ItemValidationError.CompatibilityValueEdgeWhitespace
            : value.Any(char.IsControl)
            ? ItemValidationError.CompatibilityValueContainsControlCharacter
            : ItemValidationError.None;
}
