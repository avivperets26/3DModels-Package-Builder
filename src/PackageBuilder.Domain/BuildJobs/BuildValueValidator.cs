using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.BuildJobs;

internal static class BuildValueValidator
{
    public static BuildModelValidationError ValidateIdentity(string? value) =>
        value is null
            ? BuildModelValidationError.NullIdentity
            : value.Length == 0
            ? BuildModelValidationError.EmptyIdentity
            : string.IsNullOrWhiteSpace(value)
            ? BuildModelValidationError.WhitespaceOnlyIdentity
            : char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
            ? BuildModelValidationError.IdentityEdgeWhitespace
            : value.Any(char.IsControl)
            ? BuildModelValidationError.IdentityContainsControlCharacter
            : BuildModelValidationError.None;

    public static BuildModelValidationError ValidateCanonicalIdentifier(
        string? value,
        BuildModelValidationError nullError,
        BuildModelValidationError emptyError,
        BuildModelValidationError whitespaceError,
        BuildModelValidationError malformedError) =>
        CanonicalIdentifierParser.Validate(value) switch
        {
            CanonicalIdentifierParseError.None => BuildModelValidationError.None,
            CanonicalIdentifierParseError.Null => nullError,
            CanonicalIdentifierParseError.Empty => emptyError,
            CanonicalIdentifierParseError.WhitespaceOnly => whitespaceError,
            _ => malformedError,
        };

    public static BuildModelValidationError ValidateLogicalReference(string? value)
    {
        if (value is null)
        {
            return BuildModelValidationError.NullLogicalReference;
        }

        if (value.Length == 0)
        {
            return BuildModelValidationError.EmptyLogicalReference;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return BuildModelValidationError.WhitespaceOnlyLogicalReference;
        }

        if (value[0] == '/' ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains(':', StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            return BuildModelValidationError.UnsafeLogicalReference;
        }

        string[] segments = value.Split('/');
        return segments.Any(
            segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                char.IsWhiteSpace(segment[0]) ||
                char.IsWhiteSpace(segment[^1]))
            ? BuildModelValidationError.UnsafeLogicalReference
            : BuildModelValidationError.None;
    }

    public static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

    public static BuildModelValidationResult<ReadOnlyCollection<string>> SnapshotReferences(
        IEnumerable<string?>? references)
    {
        if (references is null)
        {
            return BuildModelValidationResult<ReadOnlyCollection<string>>.Failure(
                BuildModelValidationError.NullMetadataReferences);
        }

        var values = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? reference in references)
        {
            if (reference is null)
            {
                return BuildModelValidationResult<ReadOnlyCollection<string>>.Failure(
                    BuildModelValidationError.NullMetadataReference);
            }

            BuildModelValidationError error = ValidateIdentity(reference);
            if (error != BuildModelValidationError.None)
            {
                return BuildModelValidationResult<ReadOnlyCollection<string>>.Failure(error);
            }

            if (!unique.Add(reference))
            {
                return BuildModelValidationResult<ReadOnlyCollection<string>>.Failure(
                    BuildModelValidationError.DuplicateMetadataReference);
            }

            values.Add(reference);
        }

        values.Sort(StringComparer.Ordinal);
        return BuildModelValidationResult<ReadOnlyCollection<string>>.Success(
            new ReadOnlyCollection<string>(values));
    }
}
