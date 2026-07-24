using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Assets;

/// <summary>Identifies a supported source-asset kind without filesystem or engine behavior.</summary>
public sealed class SourceAssetKind : IEquatable<SourceAssetKind>
{
    private SourceAssetKind(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    /// <summary>Gets the FBX source kind.</summary>
    public static SourceAssetKind Fbx { get; } = new("fbx");

    /// <summary>Gets the binary glTF source kind.</summary>
    public static SourceAssetKind Glb { get; } = new("glb");

    /// <summary>Gets the source archive kind.</summary>
    public static SourceAssetKind Archive { get; } = new("archive");

    /// <summary>Gets the source image kind.</summary>
    public static SourceAssetKind Image { get; } = new("image");

    private static readonly ReadOnlyCollection<SourceAssetKind> _all =
        Array.AsReadOnly<SourceAssetKind>([Fbx, Glb, Archive, Image]);

    /// <summary>Gets every supported source kind in stable canonical order.</summary>
    public static IReadOnlyList<SourceAssetKind> All => _all;

    /// <summary>Gets the lowercase canonical identifier.</summary>
    public string CanonicalIdentifier { get; }

    /// <summary>Parses an exact ordinal canonical identifier.</summary>
    public static CanonicalIdentifierParseResult<SourceAssetKind> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<SourceAssetKind>.Failure(error);
        }

        foreach (SourceAssetKind kind in All)
        {
            if (string.Equals(identifier, kind.CanonicalIdentifier, StringComparison.Ordinal))
            {
                return CanonicalIdentifierParseResult<SourceAssetKind>.Success(kind);
            }
        }

        return CanonicalIdentifierParseResult<SourceAssetKind>.Failure(
            CanonicalIdentifierParseError.Unknown);
    }

    /// <inheritdoc />
    public bool Equals(SourceAssetKind? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourceAssetKind other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        CanonicalIdentifierParser.GetStableOrdinalHashCode(CanonicalIdentifier);

    /// <inheritdoc />
    public override string ToString() => CanonicalIdentifier;
}
