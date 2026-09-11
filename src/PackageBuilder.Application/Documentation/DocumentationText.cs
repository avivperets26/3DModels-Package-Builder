using System.Text;

namespace PackageBuilder.Application.Documentation;

/// <summary>Validates bounded Unicode prose and quotes Markdown/HTML metacharacters as literal data.</summary>
public static class DocumentationText
{
    private static readonly UTF8Encoding _utf8 = new(false, true);

    public static bool IsValid(string? value, int maximumLength = 2048)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength ||
            value.Any(char.IsControl) || value != value.Trim())
        {
            return false;
        }

        try
        { _ = _utf8.GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    /// <summary>Escaped output is not a template, link, HTML fragment or Markdown instruction.</summary>
    public static string Escape(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            _ = "&<>\\`*_{}[]()#+-.!|~".Contains(character, StringComparison.Ordinal)
                ? result.Append("&#").Append(((int)character).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';')
                : result.Append(character);
        }

        return result.ToString();
    }
}
