namespace PackageBuilder.Targets.Portable;

/// <summary>Represents one explicit lowercase portable output extension, including its leading dot.</summary>
public sealed class PortableFileExtension : IEquatable<PortableFileExtension>
{
    private PortableFileExtension(string value) => Value = value;

    public static PortableFileExtension Png { get; } = new(".png");

    public static PortableFileExtension Jpg { get; } = new(".jpg");

    public string Value { get; }

    /// <summary>Accepts a bounded lowercase ASCII extension without normalizing caller intent.</summary>
    public static PortableValidationResult<PortableFileExtension> Create(string? value)
    {
        if (value is null)
        {
            return PortableValidationResult<PortableFileExtension>.Failure(PortableValidationError.NullExtension);
        }

        if (value.Length is < 2 or > 11 || value[0] != '.')
        {
            return PortableValidationResult<PortableFileExtension>.Failure(PortableValidationError.InvalidExtension);
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
            {
                return PortableValidationResult<PortableFileExtension>.Failure(PortableValidationError.InvalidExtension);
            }
        }

        return PortableValidationResult<PortableFileExtension>.Success(new PortableFileExtension(value));
    }

    public bool Equals(PortableFileExtension? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PortableFileExtension other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
