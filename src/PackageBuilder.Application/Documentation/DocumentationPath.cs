namespace PackageBuilder.Application.Documentation;

/// <summary>Conservative relative document/configuration names; filesystem containment is checked separately.</summary>
internal static class DocumentationPath
{
    internal static bool IsValid(string? value) => DocumentationText.IsValid(value, 512) &&
        !value!.Contains('\\') && !value.Contains(':') && value.Split('/').All(ValidSegment);

    private static bool ValidSegment(string segment)
    {
        if (segment.Length == 0 || segment is "." or ".." || segment != segment.Trim() || segment.EndsWith('.') ||
            segment.Any(character => "<>\"|?*".Contains(character, StringComparison.Ordinal)))
        {
            return false;
        }

        string stem = segment.Split('.')[0].TrimEnd().ToUpperInvariant();
        return stem is not "CON" and not "PRN" and not "AUX" and not "NUL" and not "CONIN$" and not "CONOUT$" &&
            !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                "123456789¹²³".Contains(stem[3], StringComparison.Ordinal));
    }
}
