using System.Collections.ObjectModel;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one allowed input position in the portable folder plan.</summary>
public sealed class PortableArtifactPurpose : IEquatable<PortableArtifactPurpose>
{
    private PortableArtifactPurpose(string identifier, string artifactRole)
    {
        Identifier = identifier;
        ArtifactRole = artifactRole;
    }

    public static PortableArtifactPurpose Fbx { get; } = new("fbx", "normalized-fbx");

    public static PortableArtifactPurpose Glb { get; } = new("glb", "normalized-glb");

    public static PortableArtifactPurpose FbxArchive { get; } = new("fbx-archive", "portable-fbx-archive");

    public static PortableArtifactPurpose FlatReadme { get; } = new("flat-readme", "portable-readme");

    public static PortableArtifactPurpose ProductReadme { get; } = new("product-readme", "portable-readme");

    public static PortableArtifactPurpose Texture { get; } = new("texture", "portable-texture");

    public static PortableArtifactPurpose Media { get; } = new("media", "portable-media");

    private static readonly ReadOnlyCollection<PortableArtifactPurpose> _all =
        Array.AsReadOnly([Fbx, Glb, FbxArchive, FlatReadme, ProductReadme, Texture, Media]);

    public static IReadOnlyList<PortableArtifactPurpose> All => _all;

    public string Identifier { get; }

    internal string ArtifactRole { get; }

    public bool Equals(PortableArtifactPurpose? other) =>
        other is not null && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PortableArtifactPurpose other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Identifier);

    public override string ToString() => Identifier;
}
