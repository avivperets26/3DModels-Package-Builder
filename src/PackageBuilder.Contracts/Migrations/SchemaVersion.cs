namespace PackageBuilder.Contracts.Migrations;

/// <summary>A positive, bounded schema-version identity.</summary>
public sealed class SchemaVersion : IComparable<SchemaVersion>, IEquatable<SchemaVersion>
{
    private SchemaVersion(int value) => Value = value;

    public int Value { get; }

    public static SchemaVersionResult Create(long value) =>
        value is < 1 or > int.MaxValue
            ? SchemaVersionResult.Failure()
            : SchemaVersionResult.Success(new SchemaVersion((int)value));

    public int CompareTo(SchemaVersion? other) =>
        other is null ? 1 : Value.CompareTo(other.Value);

    public bool Equals(SchemaVersion? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is SchemaVersion other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(SchemaVersion? left, SchemaVersion? right) =>
        Equals(left, right);

    public static bool operator !=(SchemaVersion? left, SchemaVersion? right) =>
        !Equals(left, right);

    public static bool operator <(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) < 0;

    public static bool operator <=(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) > 0;

    public static bool operator >=(SchemaVersion left, SchemaVersion right) =>
        left.CompareTo(right) >= 0;

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class SchemaVersionResult
{
    private SchemaVersionResult(bool isValid, SchemaVersion? value)
    {
        IsValid = isValid;
        Value = value;
    }

    public bool IsValid { get; }

    public SchemaVersion? Value { get; }

    internal static SchemaVersionResult Success(SchemaVersion value) => new(true, value);

    internal static SchemaVersionResult Failure() => new(false, null);
}
