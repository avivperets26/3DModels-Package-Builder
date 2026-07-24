namespace PackageBuilder.Domain.Profiles;

/// <summary>Identifies the validated support-contact representation.</summary>
public enum SupportContactKind
{
    Email = 0,
    SecureUrl = 1,
}

/// <summary>Represents either a validated email address or an HTTPS support URL.</summary>
public sealed class SupportContact : IEquatable<SupportContact>
{
    public const int MaximumEmailLength = 254;
    public const int MaximumUrlLength = 2048;

    private SupportContact(SupportContactKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    public SupportContactKind Kind { get; }

    public string Value { get; }

    public static ProfileValidationResult<SupportContact> CreateEmail(string? value)
    {
        ProfileValidationError commonError = ValidateCommon(value, MaximumEmailLength);
        if (commonError != ProfileValidationError.None)
        {
            return ProfileValidationResult<SupportContact>.Failure(commonError);
        }

        // Validation is intentionally syntactic and offline: no DNS or delivery check occurs.
        return !IsValidEmail(value!)
            ? ProfileValidationResult<SupportContact>.Failure(
                ProfileValidationError.MalformedSupportEmail)
            : ProfileValidationResult<SupportContact>.Success(
            new SupportContact(SupportContactKind.Email, value!));
    }

    public static ProfileValidationResult<SupportContact> CreateSecureUrl(string? value)
    {
        ProfileValidationError commonError = ValidateCommon(value, MaximumUrlLength);
        if (commonError != ProfileValidationError.None)
        {
            return ProfileValidationResult<SupportContact>.Failure(commonError);
        }

        // Only an absolute HTTPS URI without embedded credentials is an approved support URL.
        return !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            string.IsNullOrEmpty(uri.Host)
            ? ProfileValidationResult<SupportContact>.Failure(
                ProfileValidationError.MalformedSupportUrl)
            : !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? ProfileValidationResult<SupportContact>.Failure(
                ProfileValidationError.UnsafeSupportUrlScheme)
            : uri.UserInfo.Length != 0
            ? ProfileValidationResult<SupportContact>.Failure(
                ProfileValidationError.SupportUrlContainsCredentials)
            : ProfileValidationResult<SupportContact>.Success(
                new SupportContact(SupportContactKind.SecureUrl, value));
    }

    public bool Equals(SupportContact? other) =>
        other is not null &&
        Kind == other.Kind &&
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SupportContact other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create().Add((int)Kind).Add(Value).ToHashCode();

    public override string ToString() => Value;

    private static ProfileValidationError ValidateCommon(string? value, int maximumLength)
    {
        if (value is null)
        {
            return ProfileValidationError.NullSupportContactValue;
        }

        if (value.Length == 0)
        {
            return ProfileValidationError.EmptySupportContactValue;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return ProfileValidationError.WhitespaceOnlySupportContactValue;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return ProfileValidationError.SupportContactContainsControlCharacter;
            }

            if (char.IsWhiteSpace(character))
            {
                return ProfileValidationError.SupportContactContainsWhitespace;
            }
        }

        return value.Length > maximumLength
            ? ProfileValidationError.SupportContactTooLong
            : ProfileValidationError.None;
    }

    private static bool IsValidEmail(string value)
    {
        int at = value.IndexOf('@');
        if (at is <= 0 or > 64 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> local = value.AsSpan(0, at);
        ReadOnlySpan<char> domain = value.AsSpan(at + 1);
        if (local[0] == '.' || local[^1] == '.')
        {
            return false;
        }

        bool previousDot = false;
        foreach (char character in local)
        {
            if (character == '.')
            {
                if (previousDot)
                {
                    return false;
                }

                previousDot = true;
                continue;
            }

            previousDot = false;
            if (!IsEmailLocalCharacter(character))
            {
                return false;
            }
        }

        int labelLength = 0;
        bool labelStartsWithHyphen = false;
        char previousDomainCharacter = '\0';
        foreach (char character in domain)
        {
            if (character == '.')
            {
                if (labelLength is 0 or > 63 ||
                    labelStartsWithHyphen ||
                    previousDomainCharacter == '-')
                {
                    return false;
                }

                labelLength = 0;
                labelStartsWithHyphen = false;
                continue;
            }

            if (!IsAsciiLetterOrDigit(character) && character != '-')
            {
                return false;
            }

            if (labelLength == 0)
            {
                labelStartsWithHyphen = character == '-';
            }

            labelLength++;
            previousDomainCharacter = character;
        }

        return labelLength is > 0 and <= 63 &&
            !labelStartsWithHyphen &&
            domain[^1] != '-';
    }

    private static bool IsEmailLocalCharacter(char value) =>
        IsAsciiLetterOrDigit(value) ||
        value is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '/' or '=' or
            '?' or '^' or '_' or '`' or '{' or '|' or '}' or '~';

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
