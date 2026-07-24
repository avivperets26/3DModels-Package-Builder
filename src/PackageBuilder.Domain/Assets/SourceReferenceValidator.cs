namespace PackageBuilder.Domain.Assets;

internal static class SourceReferenceValidator
{
    public static SourceAssetValidationError Validate(string? value)
    {
        if (value is null)
        {
            return SourceAssetValidationError.NullLogicalReference;
        }

        if (value.Length == 0)
        {
            return SourceAssetValidationError.EmptyLogicalReference;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return SourceAssetValidationError.WhitespaceOnlyLogicalReference;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return SourceAssetValidationError.ControlCharacter;
            }
        }

        if (IsDriveQualified(value))
        {
            return value.Length >= 3 && value[2] is '/' or '\\'
                ? SourceAssetValidationError.RootedLogicalReference
                : SourceAssetValidationError.DriveRelativeLogicalReference;
        }

        if (value[0] is '/' or '\\')
        {
            return SourceAssetValidationError.RootedLogicalReference;
        }

        if (value.Contains(':', StringComparison.Ordinal))
        {
            return SourceAssetValidationError.UriLikeLogicalReference;
        }

        if (value.Contains('\\', StringComparison.Ordinal))
        {
            return SourceAssetValidationError.InvalidSeparator;
        }

        string[] segments = value.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0)
            {
                return SourceAssetValidationError.EmptySegment;
            }

            if (segment is "." or "..")
            {
                return SourceAssetValidationError.TraversalSegment;
            }

            if (char.IsWhiteSpace(segment[0]) || char.IsWhiteSpace(segment[^1]))
            {
                return SourceAssetValidationError.LeadingOrTrailingWhitespace;
            }
        }

        return SourceAssetValidationError.None;
    }

    public static bool HasCompatibleExtension(SourceAssetKind kind, string value)
    {
        string? requiredExtension = kind.Equals(SourceAssetKind.Fbx)
            ? ".fbx"
            : kind.Equals(SourceAssetKind.Glb)
                ? ".glb"
                : kind.Equals(SourceAssetKind.Archive)
                    ? ".zip"
                    : null;

        if (requiredExtension is null)
        {
            return true;
        }

        string fileName = value[(value.LastIndexOf('/') + 1)..];
        return fileName.Length > requiredExtension.Length &&
            fileName.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDriveQualified(string value) =>
        value.Length >= 2 &&
        value[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z' &&
        value[1] == ':';
}
