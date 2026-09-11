using System.Text.RegularExpressions;

namespace PackageBuilder.Contracts.Logging;

/// <summary>Applies deterministic deny-list redaction before values reach persistent diagnostics.</summary>
public static partial class SensitiveDiagnosticValueRedactor
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
    private static readonly string[] _privateNames = ["path", "directory", "filename", "url", "uri", "email", "username"];

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

    /// <summary>Stricter sharing policy: removes complete paths/URLs, quoted credentials, private-key
    /// blocks and common bare access tokens. Existing local-log redaction behavior remains compatible.</summary>
    public static string RedactForExport(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        string normalized = NormalizeName(name);
        if (_privateNames.Any(normalized.Contains))
        { return RedactedValue; }
        // Local logs may already contain a shortened credential followed by its quoted tail.
        // Omit the entire field whenever it contains a credential assignment or authorization.
        string unquoted = value.Replace("\"", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);
        if (SensitiveAssignmentRegex().IsMatch(unquoted) || AuthorizationRegex().IsMatch(value) ||
            value.Contains("PRIVATE KEY", StringComparison.Ordinal) || SourceFileRegex().IsMatch(value))
        { return RedactedValue; }
        if (value.Contains("%USERPROFILE%", StringComparison.OrdinalIgnoreCase) || ExportPathRegex().IsMatch(value) || RelativePathRegex().IsMatch(value))
        { return "[REDACTED_PATH]"; }
        string safe = ExportUrlRegex().Replace(value, "[REDACTED_URL]");
        safe = ExportTokenRegex().Replace(safe, RedactedValue);
        safe = EmailRegex().Replace(safe, "[REDACTED_EMAIL]");
        return Redact(name, safe);
    }

    [GeneratedRegex("(?:[\"'](?:[A-Za-z]:[\\\\/]|\\\\\\\\|/)[^\"'\\r\\n]*[\"']|[A-Za-z]:[\\\\/][^\\s<>\"']*|\\\\\\\\[^\\s<>\"']+|(?:^|[\\s(=])/(?:[^\\s<>\"']+))", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExportPathRegex();

    [GeneratedRegex(@"(?:^|[\s""'(=])(?:\.{1,2}|~|[A-Za-z0-9_.-]+)[\\/][^\s<>""']+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex RelativePathRegex();

    [GeneratedRegex(@"\b(?:https?|file|ftp)://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExportUrlRegex();

    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|xox[baprs]-[A-Za-z0-9-]+|AKIA[A-Z0-9]{16}|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExportTokenRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b[^\s]+\.(?:fbx|obj|gltf|glb|blend|uasset|unitypackage|png|jpe?g|tga|exr|tiff?|psd)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SourceFileRegex();

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
