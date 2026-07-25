namespace PackageBuilder.Application.Configuration;

public sealed class ConfiguredPathRoot : IEquatable<ConfiguredPathRoot>
{
    internal ConfiguredPathRoot(PathRootKind kind, string fullPath)
    {
        Kind = kind;
        FullPath = fullPath;
    }

    public PathRootKind Kind { get; }

    public string FullPath { get; }

    public bool Equals(ConfiguredPathRoot? other)
    {
        return other is not null
            && Kind == other.Kind
            && StringComparer.OrdinalIgnoreCase.Equals(FullPath, other.FullPath);
    }

    public override bool Equals(object? obj) => Equals(obj as ConfiguredPathRoot);

    public override int GetHashCode()
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)Kind) * 16777619u;

            foreach (char character in FullPath)
            {
                hash = (hash ^ char.ToUpperInvariant(character)) * 16777619u;
            }

            return (int)hash;
        }
    }

    public override string ToString() => FullPath;
}
