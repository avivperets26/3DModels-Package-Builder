using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Items;

/// <summary>
/// Represents an extensible logical body or attachment slot without engine socket behavior.
/// </summary>
public sealed class AttachmentSlot : IEquatable<AttachmentSlot>
{
    private AttachmentSlot(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    public string CanonicalIdentifier { get; }

    public static ItemValidationResult<AttachmentSlot> Create(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        return error switch
        {
            CanonicalIdentifierParseError.None =>
                ItemValidationResult<AttachmentSlot>.Success(new AttachmentSlot(identifier!)),
            CanonicalIdentifierParseError.Null =>
                ItemValidationResult<AttachmentSlot>.Failure(
                    ItemValidationError.NullSlotIdentifier),
            CanonicalIdentifierParseError.Empty =>
                ItemValidationResult<AttachmentSlot>.Failure(
                    ItemValidationError.EmptySlotIdentifier),
            CanonicalIdentifierParseError.WhitespaceOnly =>
                ItemValidationResult<AttachmentSlot>.Failure(
                    ItemValidationError.WhitespaceOnlySlotIdentifier),
            _ => ItemValidationResult<AttachmentSlot>.Failure(
                ItemValidationError.MalformedSlotIdentifier),
        };
    }

    public bool Equals(AttachmentSlot? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AttachmentSlot other && Equals(other);

    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    public override string ToString() => CanonicalIdentifier;
}
