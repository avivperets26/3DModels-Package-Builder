using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Validation;

internal static class FindingValueValidator
{
    public static ValidationFindingError ValidateCode(string? value)
    {
        if (value is null)
        {
            return ValidationFindingError.NullCode;
        }

        if (value.Length == 0)
        {
            return ValidationFindingError.EmptyCode;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationFindingError.WhitespaceOnlyCode;
        }

        bool expectsLetter = true;
        foreach (char character in value)
        {
            if (character is >= 'A' and <= 'Z')
            {
                expectsLetter = false;
                continue;
            }

            if (character is >= '0' and <= '9' && !expectsLetter)
            {
                continue;
            }

            if (character == '_' && !expectsLetter)
            {
                expectsLetter = true;
                continue;
            }

            return ValidationFindingError.MalformedCode;
        }

        return expectsLetter
            ? ValidationFindingError.MalformedCode
            : ValidationFindingError.None;
    }

    public static ValidationFindingError ValidateSource(string? value) =>
        CanonicalIdentifierParser.Validate(value) switch
        {
            CanonicalIdentifierParseError.None => ValidationFindingError.None,
            CanonicalIdentifierParseError.Null => ValidationFindingError.NullSource,
            CanonicalIdentifierParseError.Empty => ValidationFindingError.EmptySource,
            CanonicalIdentifierParseError.WhitespaceOnly =>
                ValidationFindingError.WhitespaceOnlySource,
            _ => ValidationFindingError.MalformedSource,
        };

    public static ValidationFindingError ValidateText(
        string? value,
        ValidationFindingError nullError,
        ValidationFindingError emptyError,
        ValidationFindingError whitespaceError,
        ValidationFindingError edgeWhitespaceError,
        ValidationFindingError controlError)
    {
        return value is null
            ? nullError
            : value.Length == 0
            ? emptyError
            : string.IsNullOrWhiteSpace(value)
            ? whitespaceError
            : char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
            ? edgeWhitespaceError
            : value.Any(char.IsControl)
            ? controlError
            : ValidationFindingError.None;
    }
}
