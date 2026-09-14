namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Existing contained database paths shared by SQLite repositories; rejects reparse points.</summary>
internal static class SqliteRepositoryPath
{
    public static bool TryValidate(
        string? projectRoot,
        string? databasePath,
        out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(databasePath))
        {
            return false;
        }

        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
            string candidate = Path.GetFullPath(databasePath);
            if (!Path.IsPathFullyQualified(projectRoot) || !Path.IsPathFullyQualified(databasePath)
                || !Directory.Exists(root) || !File.Exists(candidate)
                || !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string current = root;
            foreach (string segment in Path.GetRelativePath(root, candidate).Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                current = Path.Combine(current, segment);
            }

            if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            normalizedPath = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

}
