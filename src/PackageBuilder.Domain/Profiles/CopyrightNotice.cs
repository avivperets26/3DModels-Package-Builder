namespace PackageBuilder.Domain.Profiles;

/// <summary>Combines a validated holder with explicit copyright-year policy.</summary>
public sealed class CopyrightNotice : IEquatable<CopyrightNotice>
{
    private CopyrightNotice(CopyrightHolder holder, CopyrightYearPolicy yearPolicy)
    {
        Holder = holder;
        YearPolicy = yearPolicy;
    }

    public CopyrightHolder Holder { get; }

    public CopyrightYearPolicy YearPolicy { get; }

    public static ProfileValidationResult<CopyrightNotice> Create(
        CopyrightHolder? holder,
        CopyrightYearPolicy? yearPolicy)
    {
        return holder is null
            ? ProfileValidationResult<CopyrightNotice>.Failure(
                ProfileValidationError.NullCopyrightHolder)
            : yearPolicy is null
            ? ProfileValidationResult<CopyrightNotice>.Failure(
                ProfileValidationError.NullCopyrightYearPolicyKind)
            : ProfileValidationResult<CopyrightNotice>.Success(
            new CopyrightNotice(holder, yearPolicy));
    }

    public bool Equals(CopyrightNotice? other) =>
        other is not null &&
        Holder.Equals(other.Holder) &&
        YearPolicy.Equals(other.YearPolicy);

    public override bool Equals(object? obj) => obj is CopyrightNotice other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(Holder.Value)
            .Add(YearPolicy.GetHashCode())
            .ToHashCode();
}
