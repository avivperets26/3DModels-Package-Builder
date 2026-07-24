namespace PackageBuilder.Domain.Items;

internal struct StableItemHash
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;
    private uint _value;

    public static StableItemHash Create() => new() { _value = OffsetBasis };

    public StableItemHash Add(bool value) => Add(value ? 1 : 0);

    public StableItemHash Add(int value)
    {
        uint bits = unchecked((uint)value);
        for (int shift = 0; shift < 32; shift += 8)
        {
            AddByte((byte)(bits >> shift));
        }

        return this;
    }

    public StableItemHash Add(string value)
    {
        foreach (char character in value)
        {
            AddByte((byte)character);
            AddByte((byte)(character >> 8));
        }

        AddByte(0xff);
        return this;
    }

    public readonly int ToHashCode() => unchecked((int)_value);

    private void AddByte(byte value)
    {
        _value ^= value;
        _value *= Prime;
    }
}
