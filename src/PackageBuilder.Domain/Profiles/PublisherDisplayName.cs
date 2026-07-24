namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents a validated human-readable publisher display name.</summary>
public sealed class PublisherDisplayName : IEquatable<PublisherDisplayName>
{
    /// <summary>Gets the maximum supported UTF-16 length.</summary>
    public const int MaximumLength = 256;

    private PublisherDisplayName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProfileValidationResult<PublisherDisplayName> Create(string? value)
    {
        ProfileValidationError error = ProfileTextValidator.Validate(
            value,
            MaximumLength,
            ProfileValidationError.NullPublisherDisplayName,
            ProfileValidationError.EmptyPublisherDisplayName,
            ProfileValidationError.WhitespaceOnlyPublisherDisplayName,
            ProfileValidationError.PublisherDisplayNameEdgeWhitespace,
            ProfileValidationError.PublisherDisplayNameContainsControlCharacter,
            ProfileValidationError.PublisherDisplayNameTooLong);
        return error == ProfileValidationError.None
            ? ProfileValidationResult<PublisherDisplayName>.Success(
                new PublisherDisplayName(value!))
            : ProfileValidationResult<PublisherDisplayName>.Failure(error);
    }

    public bool Equals(PublisherDisplayName? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PublisherDisplayName other && Equals(other);

    public override int GetHashCode() => StableProfileHash.Create().Add(Value).ToHashCode();

    public override string ToString() => Value;
}
