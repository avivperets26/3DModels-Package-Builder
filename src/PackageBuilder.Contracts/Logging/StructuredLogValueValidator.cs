using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Logging;

internal static class StructuredLogValueValidator
{
    public static bool IsToken(string? value, int maximumLength) =>
        value is not null &&
        value.Length is > 0 &&
        value.Length <= maximumLength &&
        IsAsciiLetterOrDigit(value[0]) &&
        value.All(character =>
            IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    public static StructuredLogResult<ReadOnlyCollection<StructuredLogProperty>> SnapshotProperties(
        IEnumerable<StructuredLogProperty?>? properties)
    {
        if (properties is null)
        {
            return StructuredLogResult.Success(
                new ReadOnlyCollection<StructuredLogProperty>([]));
        }

        var snapshot = new List<StructuredLogProperty>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (StructuredLogProperty? property in properties)
        {
            if (property is null ||
                !IsToken(property.Name, 128) ||
                property.Value is { Length: > 8_192 } ||
                property.Value?.Any(char.IsControl) == true)
            {
                return StructuredLogResult.Failure<ReadOnlyCollection<StructuredLogProperty>>(
                    "LOG_PROPERTY_INVALID",
                    "A log property is null, malformed, too long, or contains control characters.");
            }

            if (!names.Add(property.Name))
            {
                return StructuredLogResult.Failure<ReadOnlyCollection<StructuredLogProperty>>(
                    "LOG_PROPERTY_DUPLICATE",
                    "Log property names must be unique using ordinal comparison.");
            }

            snapshot.Add(property);
            if (snapshot.Count > 64)
            {
                return StructuredLogResult.Failure<ReadOnlyCollection<StructuredLogProperty>>(
                    "LOG_PROPERTY_LIMIT_EXCEEDED",
                    "A log event cannot contain more than 64 properties.");
            }
        }

        snapshot.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        return StructuredLogResult.Success(
            new ReadOnlyCollection<StructuredLogProperty>(snapshot));
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
