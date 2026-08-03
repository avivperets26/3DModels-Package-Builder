using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Archives;

/// <summary>Defines explicit fail-closed quotas and the extension allow-list for one archive operation.</summary>
public sealed class ArchiveSafetyPolicy
{
    private readonly HashSet<string> _allowedExtensions;

    private ArchiveSafetyPolicy(
        long maximumArchiveBytes,
        int maximumEntryCount,
        int maximumPathDepth,
        long maximumEntryUncompressedBytes,
        long maximumTotalUncompressedBytes,
        double maximumExpansionRatio,
        IEnumerable<string> allowedExtensions,
        bool allowExtensionlessFiles)
    {
        MaximumArchiveBytes = maximumArchiveBytes;
        MaximumEntryCount = maximumEntryCount;
        MaximumPathDepth = maximumPathDepth;
        MaximumEntryUncompressedBytes = maximumEntryUncompressedBytes;
        MaximumTotalUncompressedBytes = maximumTotalUncompressedBytes;
        MaximumExpansionRatio = maximumExpansionRatio;
        AllowExtensionlessFiles = allowExtensionlessFiles;

        string[] extensions = [.. allowedExtensions.Order(StringComparer.OrdinalIgnoreCase)];
        AllowedExtensions = new ReadOnlyCollection<string>(extensions);
        _allowedExtensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
    }

    public long MaximumArchiveBytes { get; }

    public int MaximumEntryCount { get; }

    public int MaximumPathDepth { get; }

    public long MaximumEntryUncompressedBytes { get; }

    public long MaximumTotalUncompressedBytes { get; }

    public double MaximumExpansionRatio { get; }

    public bool AllowExtensionlessFiles { get; }

    public IReadOnlyList<string> AllowedExtensions { get; }

    /// <summary>Creates a validated immutable policy; no numeric limit is silently selected.</summary>
    public static ArchiveOperationResult<ArchiveSafetyPolicy> Create(
        long maximumArchiveBytes,
        int maximumEntryCount,
        int maximumPathDepth,
        long maximumEntryUncompressedBytes,
        long maximumTotalUncompressedBytes,
        double maximumExpansionRatio,
        IEnumerable<string>? allowedExtensions,
        bool allowExtensionlessFiles = false)
    {
        var failures = new List<ArchiveFailure>();
        AddPositiveFailure(maximumArchiveBytes, "maximumArchiveBytes", failures);
        AddPositiveFailure(maximumEntryCount, "maximumEntryCount", failures);
        AddPositiveFailure(maximumPathDepth, "maximumPathDepth", failures);
        AddPositiveFailure(maximumEntryUncompressedBytes, "maximumEntryUncompressedBytes", failures);
        AddPositiveFailure(maximumTotalUncompressedBytes, "maximumTotalUncompressedBytes", failures);

        if (maximumTotalUncompressedBytes < maximumEntryUncompressedBytes)
        {
            failures.Add(Failure(
                "ARCHIVE_POLICY_TOTAL_LIMIT",
                "maximumTotalUncompressedBytes",
                "Total extracted-size limit must be at least the per-entry limit."));
        }

        if (!double.IsFinite(maximumExpansionRatio) || maximumExpansionRatio < 1d)
        {
            failures.Add(Failure(
                "ARCHIVE_POLICY_RATIO",
                "maximumExpansionRatio",
                "Expansion-ratio limit must be finite and at least one."));
        }

        string[] normalizedExtensions = NormalizeExtensions(allowedExtensions, failures);
        if (normalizedExtensions.Length == 0 && !allowExtensionlessFiles)
        {
            failures.Add(Failure(
                "ARCHIVE_POLICY_EXTENSIONS_EMPTY",
                "allowedExtensions",
                "At least one file extension or explicit extensionless-file permission is required."));
        }

        return failures.Count == 0
            ? ArchiveOperationResult.Success<ArchiveSafetyPolicy>(
                new ArchiveSafetyPolicy(
                    maximumArchiveBytes,
                    maximumEntryCount,
                    maximumPathDepth,
                    maximumEntryUncompressedBytes,
                    maximumTotalUncompressedBytes,
                    maximumExpansionRatio,
                    normalizedExtensions,
                    allowExtensionlessFiles))
            : ArchiveOperationResult.Failed<ArchiveSafetyPolicy>([.. failures]);
    }

    public bool AllowsFile(string normalizedRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRelativePath);
        string extension = Path.GetExtension(normalizedRelativePath);
        return extension.Length == 0 ? AllowExtensionlessFiles : _allowedExtensions.Contains(extension);
    }

    private static void AddPositiveFailure(long value, string location, List<ArchiveFailure> failures)
    {
        if (value <= 0)
        {
            failures.Add(Failure(
                "ARCHIVE_POLICY_POSITIVE_LIMIT",
                location,
                "Archive safety limits must be greater than zero."));
        }
    }

    private static string[] NormalizeExtensions(
        IEnumerable<string>? extensions,
        List<ArchiveFailure> failures)
    {
        if (extensions is null)
        {
            failures.Add(Failure(
                "ARCHIVE_POLICY_EXTENSIONS_NULL",
                "allowedExtensions",
                "Extension policy is required."));
            return [];
        }

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;
        foreach (string? extension in extensions)
        {
            string location = $"allowedExtensions[{index}]";
            if (string.IsNullOrWhiteSpace(extension)
                || extension.Length > 32
                || extension[0] != '.'
                || extension.Any(static character =>
                    !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            {
                failures.Add(Failure(
                    "ARCHIVE_POLICY_EXTENSION_INVALID",
                    location,
                    "Allowed extensions must use a leading dot and ASCII letters, digits, dots, underscores, or hyphens."));
            }
            else
            {
                _ = normalized.Add(extension.ToLowerInvariant());
            }

            index++;
        }

        return [.. normalized];
    }

    private static ArchiveFailure Failure(string code, string location, string diagnostic) =>
        new(code, location, diagnostic);
}
