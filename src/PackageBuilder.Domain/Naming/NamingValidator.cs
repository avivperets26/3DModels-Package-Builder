using System.Text.RegularExpressions;

namespace PackageBuilder.Domain.Naming;

internal static partial class NamingValidator
{
    private static readonly HashSet<string> _windowsReservedNames = new(
        new[]
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9",
        },
        StringComparer.OrdinalIgnoreCase);

    public static NamingValidationError ValidateDisplayName(string? value) =>
        ValidateCommon(value, rejectTrailingDotOrSpace: false);

    public static NamingValidationError ValidateInternalAssetId(string? value)
    {
        NamingValidationError commonError = ValidateCommon(value, rejectTrailingDotOrSpace: false);
        if (commonError != NamingValidationError.None)
        {
            return commonError;
        }

        if (!IsAsciiLetter(value![0]))
        {
            return NamingValidationError.InvalidCharacter;
        }

        for (int index = 1; index < value.Length; index++)
        {
            if (!IsAsciiLetterOrDigit(value[index]))
            {
                return NamingValidationError.InvalidCharacter;
            }
        }

        return NamingValidationError.None;
    }

    public static NamingValidationError ValidateProductFolderName(string? value)
    {
        NamingValidationError fileSystemError = ValidateFileSystemSegment(value);
        if (fileSystemError != NamingValidationError.None)
        {
            return fileSystemError;
        }

        if (!IsAsciiLetterOrDigit(value![0]))
        {
            return NamingValidationError.InvalidCharacter;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!IsAsciiLetterOrDigit(character) && character != '_' && character != '-')
            {
                return NamingValidationError.InvalidCharacter;
            }
        }

        return NamingValidationError.None;
    }

    public static NamingValidationError ValidatePublisherRoot(string? value)
    {
        NamingValidationError fileSystemError = ValidateFileSystemSegment(value);
        if (fileSystemError != NamingValidationError.None)
        {
            return fileSystemError;
        }

        if (!IsAsciiLetter(value![0]))
        {
            return NamingValidationError.InvalidCharacter;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (!IsAsciiLetterOrDigit(character) && character != '_')
            {
                return NamingValidationError.InvalidCharacter;
            }
        }

        return NamingValidationError.None;
    }

    public static int GetStableOrdinalHashCode(string value)
    {
        // A local FNV-1a hash avoids culture and per-process string-hash randomization.
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

    private static NamingValidationError ValidateFileSystemSegment(string? value)
    {
        NamingValidationError commonError = ValidateCommon(value, rejectTrailingDotOrSpace: true);
        return commonError != NamingValidationError.None
            ? commonError
            : _windowsReservedNames.Contains(value!)
            ? NamingValidationError.ReservedFileSystemName
            : NamingValidationError.None;
    }

    private static NamingValidationError ValidateCommon(
        string? value,
        bool rejectTrailingDotOrSpace)
    {
        if (value is null)
        {
            return NamingValidationError.Null;
        }

        if (value.Length == 0)
        {
            return NamingValidationError.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return NamingValidationError.WhitespaceOnly;
        }

        if (rejectTrailingDotOrSpace && (value[^1] == '.' || value[^1] == ' '))
        {
            return NamingValidationError.TrailingDotOrSpace;
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return NamingValidationError.LeadingOrTrailingWhitespace;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return NamingValidationError.ControlCharacter;
            }
        }

        return IsWindowsRooted(value)
            ? NamingValidationError.RootedPath
            : TraversalSegmentRegex().IsMatch(value)
                ? NamingValidationError.Traversal
                : value.IndexOfAny(['/', '\\']) >= 0
                    ? NamingValidationError.DirectorySeparator
                    : NamingValidationError.None;
    }

    private static bool IsWindowsRooted(string value) =>
        // System.IO path rules vary by host; these checks preserve Windows behavior everywhere.
        value[0] is '/' or '\\' || value.Length >= 2 && IsAsciiLetter(value[0]) && value[1] == ':';

    private static bool IsAsciiLetterOrDigit(char value) =>
        IsAsciiLetter(value) || value is >= '0' and <= '9';

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    [GeneratedRegex(@"(?:^|[\\/])\.\.?(?:[\\/]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex TraversalSegmentRegex();
}
