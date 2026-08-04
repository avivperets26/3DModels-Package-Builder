using System.Globalization;
using System.Text.RegularExpressions;

namespace PackageBuilder.Domain.Tools;

/// <summary>Parses canonical vendor versions into numeric precedence components.</summary>
internal static partial class ToolVersionParser
{
    public static ToolModelValidationError Parse(
        ToolKind tool,
        string? value,
        out ToolVersionParts parts)
    {
        parts = default;
        ToolModelValidationError textError = ValidateText(value);
        return textError != ToolModelValidationError.None
            ? textError
            : !Enum.IsDefined(tool)
            ? ToolModelValidationError.UndefinedTool
            : tool == ToolKind.Unity
            ? ParseUnity(value!, out parts)
            : ParseSemantic(tool, value!, out parts);
    }

    private static ToolModelValidationError ParseSemantic(
        ToolKind tool,
        string value,
        out ToolVersionParts parts)
    {
        parts = default;
        Match match = SemanticVersionRegex().Match(value);
        if (!match.Success ||
            !TryParseNumber(match.Groups["major"].Value, out int major) ||
            !TryParseNumber(match.Groups["minor"].Value, out int minor) ||
            !TryParseNumber(match.Groups["patch"].Value, out int patch))
        {
            return ToolModelValidationError.InvalidVersion;
        }

        string prerelease = match.Groups["prerelease"].Value;
        string[] identifiers = prerelease.Length == 0 ? [] : prerelease.Split('.');
        if (!IdentifiersAreCanonical(identifiers) || !PrereleaseIsSupported(tool, identifiers))
        {
            return ToolModelValidationError.InvalidVersion;
        }

        parts = new ToolVersionParts(
            [major, minor, patch],
            identifiers,
            prerelease.Length == 0 ? ToolReleaseChannel.Stable : ToolReleaseChannel.Preview,
            UnityStage: -1,
            UnityRevision: -1);
        return ToolModelValidationError.None;
    }

    private static ToolModelValidationError ParseUnity(string value, out ToolVersionParts parts)
    {
        parts = default;
        Match match = UnityVersionRegex().Match(value);
        if (!match.Success ||
            !TryParseNumber(match.Groups["major"].Value, out int major) ||
            !TryParseNumber(match.Groups["minor"].Value, out int minor) ||
            !TryParseNumber(match.Groups["patch"].Value, out int patch) ||
            !TryParseNumber(match.Groups["revision"].Value, out int revision))
        {
            return ToolModelValidationError.InvalidVersion;
        }

        // The regular expression limits the stage to this ordered vendor alphabet.
        int stage = "abfp".IndexOf(match.Groups["stage"].Value[0]);

        parts = new ToolVersionParts(
            [major, minor, patch],
            [],
            stage >= 2 ? ToolReleaseChannel.Stable : ToolReleaseChannel.Preview,
            stage,
            revision);
        return ToolModelValidationError.None;
    }

    private static bool PrereleaseIsSupported(ToolKind tool, string[] identifiers)
    {
        if (identifiers.Length == 0)
        {
            return true;
        }

        string label = identifiers[0];
        if (tool == ToolKind.DotNet)
        {
            return label is "preview" or "rc" && AllFollowingAreNumeric(identifiers);
        }

        if (tool == ToolKind.Blender)
        {
            return label is "alpha" or "beta" or "rc" && AllFollowingAreNumeric(identifiers);
        }

        // Parse routes Unity through its vendor grammar and rejects undefined kinds first,
        // so Unreal is the only remaining semantic-version tool.
        return label is "preview" or "rc" && identifiers.Length >= 2 && AllFollowingAreNumeric(identifiers);
    }

    private static bool AllFollowingAreNumeric(string[] identifiers)
    {
        for (int index = 1; index < identifiers.Length; index++)
        {
            if (!int.TryParse(identifiers[index], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IdentifiersAreCanonical(IEnumerable<string> identifiers) =>
        identifiers.All(identifier =>
            !identifier.All(char.IsDigit) || identifier == "0" || identifier[0] != '0');

    private static ToolModelValidationError ValidateText(string? value) =>
        value is null
            ? ToolModelValidationError.Null
            : value.Length == 0
                ? ToolModelValidationError.Empty
                : string.IsNullOrWhiteSpace(value)
                    ? ToolModelValidationError.WhitespaceOnly
                    : !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                        ? ToolModelValidationError.LeadingOrTrailingWhitespace
                        : ToolModelValidationError.None;

    private static bool TryParseNumber(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);

    [GeneratedRegex(
        "^(?<major>0|[1-9][0-9]*)\\.(?<minor>0|[1-9][0-9]*)\\.(?<patch>0|[1-9][0-9]*)(?:-(?<prerelease>[0-9a-z-]+(?:\\.[0-9a-z-]+)*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex(
        "^(?<major>0|[1-9][0-9]*)\\.(?<minor>0|[1-9][0-9]*)\\.(?<patch>0|[1-9][0-9]*)(?<stage>[abfp])(?<revision>0|[1-9][0-9]*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex UnityVersionRegex();
}

/// <summary>Stores parsed precedence data without exposing parser details publicly.</summary>
internal readonly record struct ToolVersionParts(
    int[] Numbers,
    string[] PrereleaseIdentifiers,
    ToolReleaseChannel Channel,
    int UnityStage,
    int UnityRevision);
