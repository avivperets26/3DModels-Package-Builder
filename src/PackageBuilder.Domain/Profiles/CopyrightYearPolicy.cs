namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents explicit copyright-year intent without consulting the system clock.</summary>
public sealed class CopyrightYearPolicy : IEquatable<CopyrightYearPolicy>
{
    private CopyrightYearPolicy(
        CopyrightYearPolicyKind kind,
        int year,
        int? startYear)
    {
        Kind = kind;
        Year = year;
        StartYear = startYear;
    }

    public CopyrightYearPolicyKind Kind { get; }

    /// <summary>Gets the explicit single, publication, or range-ending year.</summary>
    public int Year { get; }

    /// <summary>Gets the range start, or null for non-range policies.</summary>
    public int? StartYear { get; }

    /// <summary>
    /// Creates an explicit policy. Publication-year values are supplied by the caller and are
    /// never inferred from the system clock.
    /// </summary>
    public static ProfileValidationResult<CopyrightYearPolicy> Create(
        CopyrightYearPolicyKind? kind,
        int? year,
        int? startYear = null)
    {
        if (kind is null)
        {
            return Failure(ProfileValidationError.NullCopyrightYearPolicyKind);
        }

        if (year is null)
        {
            return Failure(ProfileValidationError.MissingCopyrightYear);
        }

        if (!IsValidYear(year.Value))
        {
            return Failure(ProfileValidationError.InvalidCopyrightYear);
        }

        if (kind.Equals(CopyrightYearPolicyKind.YearRange))
        {
            if (startYear is null)
            {
                return Failure(ProfileValidationError.MissingCopyrightStartYear);
            }

            if (!IsValidYear(startYear.Value) || startYear.Value >= year.Value)
            {
                return Failure(ProfileValidationError.InvalidCopyrightYearRange);
            }
        }
        else if (startYear is not null)
        {
            return Failure(ProfileValidationError.UnexpectedCopyrightStartYear);
        }

        return ProfileValidationResult<CopyrightYearPolicy>.Success(
            new CopyrightYearPolicy(kind, year.Value, startYear));
    }

    public bool Equals(CopyrightYearPolicy? other) =>
        other is not null &&
        Kind.Equals(other.Kind) &&
        Year == other.Year &&
        StartYear == other.StartYear;

    public override bool Equals(object? obj) =>
        obj is CopyrightYearPolicy other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(Kind.CanonicalIdentifier)
            .Add(Year)
            .Add(StartYear ?? 0)
            .ToHashCode();

    private static bool IsValidYear(int year) => year is >= 1 and <= 9999;

    private static ProfileValidationResult<CopyrightYearPolicy> Failure(
        ProfileValidationError error) =>
        ProfileValidationResult<CopyrightYearPolicy>.Failure(error);
}
