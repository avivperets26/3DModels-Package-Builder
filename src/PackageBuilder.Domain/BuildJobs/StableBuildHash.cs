namespace PackageBuilder.Domain.BuildJobs;

internal struct StableBuildHash
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;
    private uint _value;

    public static StableBuildHash Create() => new() { _value = OffsetBasis };

    public StableBuildHash Add(int value) => Add((long)value);

    public StableBuildHash Add(long value)
    {
        ulong bits = unchecked((ulong)value);
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte((byte)(bits >> shift));
        }

        return this;
    }

    public StableBuildHash Add(string value)
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
