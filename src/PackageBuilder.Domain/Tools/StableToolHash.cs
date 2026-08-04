namespace PackageBuilder.Domain.Tools;

/// <summary>Builds deterministic FNV-1a hashes for immutable tool values.</summary>
internal struct StableToolHash
{
    private const uint Offset = 2166136261;
    private const uint Prime = 16777619;
    private uint _value;

    public static StableToolHash Create() => new() { _value = Offset };

    public StableToolHash Add(int value)
    {
        unchecked
        {
            _value = (_value ^ (uint)value) * Prime;
        }

        return this;
    }

    public StableToolHash Add(string value, bool ignoreCase = false)
    {
        foreach (char character in value)
        {
            char normalized = ignoreCase ? char.ToUpperInvariant(character) : character;
            unchecked
            {
                _value = (_value ^ normalized) * Prime;
            }
        }

        return this;
    }

    public readonly int ToHashCode() => unchecked((int)_value);
}
