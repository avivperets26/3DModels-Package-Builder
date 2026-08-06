using System.Collections.ObjectModel;

namespace PackageBuilder.Targets.Portable;

/// <summary>Chooses whether a portable GLB name carries the approved rigged suffix.</summary>
public sealed class PortableGlbVariant : IEquatable<PortableGlbVariant>
{
    private PortableGlbVariant(string identifier, string fileSuffix)
    {
        Identifier = identifier;
        FileSuffix = fileSuffix;
    }

    public static PortableGlbVariant Standard { get; } = new("standard", string.Empty);

    public static PortableGlbVariant Rigged { get; } = new("rigged", "_rigged");

    private static readonly ReadOnlyCollection<PortableGlbVariant> _all =
        Array.AsReadOnly([Standard, Rigged]);

    public static IReadOnlyList<PortableGlbVariant> All => _all;

    public string Identifier { get; }

    internal string FileSuffix { get; }

    public bool Equals(PortableGlbVariant? other) =>
        other is not null && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PortableGlbVariant other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Identifier);

    public override string ToString() => Identifier;
}
