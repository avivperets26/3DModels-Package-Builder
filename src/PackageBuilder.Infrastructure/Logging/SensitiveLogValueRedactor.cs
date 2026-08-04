using System.Text.RegularExpressions;

namespace PackageBuilder.Infrastructure.Logging;

/// <summary>Applies deterministic deny-list redaction before values reach persistent logs.</summary>
internal static partial class SensitiveLogValueRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly string[] _sensitiveNames =
    [
        "accesskey",
        "apikey",
        "authorization",
        "cookie",
        "credential",
        "password",
        "privatekey",
        "secret",
        "setcookie",
        "token",
    ];

    public static string Redact(string name, string value)
    {
        string normalizedName = NormalizeName(name);
        if (_sensitiveNames.Any(normalizedName.Contains))
        {
            return RedactedValue;
        }

        string redacted = AuthorizationRegex().Replace(value, match => $"{match.Groups[1].Value} {RedactedValue}");
        redacted = SensitiveAssignmentRegex().Replace(
            redacted,
            match => $"{match.Groups[1].Value}{match.Groups[2].Value}{RedactedValue}");
        return UserProfileRegex().Replace(redacted, "%USERPROFILE%");
    }

    private static string NormalizeName(string value) =>
        new([.. value.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant)]);

    [GeneratedRegex(
        @"\b(Bearer|Basic)\s+[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex AuthorizationRegex();

    [GeneratedRegex(
        @"\b((?:access[_-]?key|api[_-]?key|authorization|cookie|credential|password|private[_-]?key|secret|token)\s*)([=:]\s*)[^\s,;&]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(
        @"\b[A-Za-z]:\\Users\\[^\\/:*?\""<>|\s]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex UserProfileRegex();
}
