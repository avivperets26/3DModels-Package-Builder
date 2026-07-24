namespace PackageBuilder.Domain.Manifests;

/// <summary>A product release version using three non-negative decimal components.</summary>
public sealed class ProductVersion : IEquatable<ProductVersion>
{
    private ProductVersion(string value) => Value = value;

    public string Value { get; }

    public static ProductVersionResult Create(string? value)
    {
        if (value is null)
        {
            return ProductVersionResult.Failure(ProductVersionError.Null);
        }

        if (value.Length == 0)
        {
            return ProductVersionResult.Failure(ProductVersionError.Empty);
        }

        string[] components = value.Split('.');
        if (components.Length != 3)
        {
            return ProductVersionResult.Failure(ProductVersionError.Malformed);
        }

        foreach (string component in components)
        {
            if (component.Length == 0 ||
                component.Length > 1 && component[0] == '0' ||
                component.Any(character => character is < '0' or > '9'))
            {
                return ProductVersionResult.Failure(ProductVersionError.Malformed);
            }
        }

        return ProductVersionResult.Success(new ProductVersion(value));
    }

    public bool Equals(ProductVersion? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ProductVersion other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in Value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }

    public override string ToString() => Value;
}

public enum ProductVersionError
{
    None = 0,
    Null,
    Empty,
    Malformed,
}

public sealed class ProductVersionResult
{
    private ProductVersionResult(bool isValid, ProductVersion? value, ProductVersionError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public ProductVersion? Value { get; }

    public ProductVersionError Error { get; }

    internal static ProductVersionResult Success(ProductVersion value) =>
        new(true, value, ProductVersionError.None);

    internal static ProductVersionResult Failure(ProductVersionError error) =>
        new(false, null, error);
}
