namespace PackageBuilder.Domain.Identifiers;

internal static class CanonicalIdentifierParser
{
    public static CanonicalIdentifierParseError Validate(string? identifier)
    {
        if (identifier is null)
        {
            return CanonicalIdentifierParseError.Null;
        }

        if (identifier.Length == 0)
        {
            return CanonicalIdentifierParseError.Empty;
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return CanonicalIdentifierParseError.WhitespaceOnly;
        }

        bool expectsLetter = true;
        foreach (char character in identifier)
        {
            if (character is >= 'a' and <= 'z')
            {
                expectsLetter = false;
                continue;
            }

            if (character == '-' && !expectsLetter)
            {
                expectsLetter = true;
                continue;
            }

            return CanonicalIdentifierParseError.Malformed;
        }

        return expectsLetter
            ? CanonicalIdentifierParseError.Malformed
            : CanonicalIdentifierParseError.None;
    }

    public static int GetStableOrdinalHashCode(string value)
    {
        // A local FNV-1a hash is stable across cultures, processes, and supported runtimes.
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return (int)hash;
        }
    }
}
