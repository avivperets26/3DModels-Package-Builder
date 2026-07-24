namespace PackageBuilder.Domain.Materials;

internal struct StableMaterialHash
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;
    private uint _value;

    public static StableMaterialHash Create() => new() { _value = OffsetBasis };

    public StableMaterialHash Add(bool value) => Add(value ? 1L : 0L);

    public StableMaterialHash Add(double value)
    {
        // Equality treats both signed zero values as equal, so hashing must do the same.
        double canonicalValue = value == 0d ? 0d : value;
        return Add(BitConverter.DoubleToInt64Bits(canonicalValue));
    }

    public StableMaterialHash Add(string value)
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

    private StableMaterialHash Add(long value)
    {
        ulong bits = unchecked((ulong)value);
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte((byte)(bits >> shift));
        }

        return this;
    }

    private void AddByte(byte value)
    {
        _value ^= value;
        _value *= Prime;
    }
}
