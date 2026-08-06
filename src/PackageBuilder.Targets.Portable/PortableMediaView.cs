using System.Collections.ObjectModel;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one approved top-level marketplace-neutral media view.</summary>
public sealed class PortableMediaView : IEquatable<PortableMediaView>
{
    private PortableMediaView(string identifier, string nameToken)
    {
        Identifier = identifier;
        NameToken = nameToken;
    }

    public static PortableMediaView Cover { get; } = new("cover", "Cover");

    public static PortableMediaView Front { get; } = new("front", "Front");

    public static PortableMediaView Back { get; } = new("back", "Back");

    public static PortableMediaView Left { get; } = new("left", "Left");

    public static PortableMediaView Right { get; } = new("right", "Right");

    private static readonly ReadOnlyCollection<PortableMediaView> _all =
        Array.AsReadOnly([Cover, Front, Back, Left, Right]);

    public static IReadOnlyList<PortableMediaView> All => _all;

    public string Identifier { get; }

    internal string NameToken { get; }

    public bool Equals(PortableMediaView? other) =>
        other is not null && string.Equals(Identifier, other.Identifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PortableMediaView other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Identifier);

    public override string ToString() => Identifier;
}
