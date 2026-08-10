namespace PackageBuilder.Domain.Preview;

/// <summary>
/// Provides task-local deterministic value hashing for preview objects until a shared domain hash
/// primitive is approved.
/// </summary>
internal struct StablePreviewHash
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;
    private uint _value;

    public static StablePreviewHash Create() => new() { _value = OffsetBasis };

    public StablePreviewHash Add(double value)
    {
        double canonicalValue = value == 0d ? 0d : value;
        return Add(BitConverter.DoubleToInt64Bits(canonicalValue));
    }

    public StablePreviewHash Add(int value) => Add((long)value);

    public StablePreviewHash Add(string value)
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

    private StablePreviewHash Add(long value)
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
