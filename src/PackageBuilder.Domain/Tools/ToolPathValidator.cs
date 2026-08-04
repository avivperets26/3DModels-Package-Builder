namespace PackageBuilder.Domain.Tools;

/// <summary>Validates canonical Windows paths and strict project-tool containment without filesystem I/O.</summary>
internal static class ToolPathValidator
{
    public static ToolModelValidationError Validate(
        string? executablePath,
        string? approvedToolsRoot,
        out string normalizedExecutable,
        out string normalizedToolsRoot,
        out ToolInstallationContainment containment)
    {
        normalizedExecutable = string.Empty;
        normalizedToolsRoot = string.Empty;
        containment = ToolInstallationContainment.External;

        ToolModelValidationError rootError = NormalizePath(approvedToolsRoot, allowTrailingSeparator: true, out normalizedToolsRoot);
        if (rootError != ToolModelValidationError.None ||
            !string.Equals(Path.GetFileName(normalizedToolsRoot), "tools", StringComparison.OrdinalIgnoreCase))
        {
            return ToolModelValidationError.InvalidToolsRoot;
        }

        ToolModelValidationError executableError = NormalizePath(
            executablePath,
            allowTrailingSeparator: false,
            out normalizedExecutable);
        if (executableError != ToolModelValidationError.None)
        {
            return executableError;
        }

        string containmentPrefix = normalizedToolsRoot + Path.DirectorySeparatorChar;
        containment = normalizedExecutable.StartsWith(containmentPrefix, StringComparison.OrdinalIgnoreCase)
            ? ToolInstallationContainment.Contained
            : ToolInstallationContainment.External;
        return ToolModelValidationError.None;
    }

    private static ToolModelValidationError NormalizePath(
        string? value,
        bool allowTrailingSeparator,
        out string normalized)
    {
        normalized = string.Empty;
        if (value is null)
        {
            return ToolModelValidationError.Null;
        }

        if (value.Length == 0)
        {
            return ToolModelValidationError.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return ToolModelValidationError.WhitespaceOnly;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return ToolModelValidationError.LeadingOrTrailingWhitespace;
        }

        if (value.Contains('/') || !Path.IsPathFullyQualified(value))
        {
            return ToolModelValidationError.PathNotRooted;
        }

        try
        {
            string fullPath = Path.GetFullPath(value);
            string canonical = allowTrailingSeparator ? Path.TrimEndingDirectorySeparator(fullPath) : fullPath;
            string inputCanonical = allowTrailingSeparator ? Path.TrimEndingDirectorySeparator(value) : value;
            if (!string.Equals(canonical, inputCanonical, StringComparison.OrdinalIgnoreCase) ||
                (!allowTrailingSeparator && Path.EndsInDirectorySeparator(value)))
            {
                return ToolModelValidationError.PathNotCanonical;
            }

            normalized = canonical;
            return ToolModelValidationError.None;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ToolModelValidationError.PathNotCanonical;
        }
    }
}
