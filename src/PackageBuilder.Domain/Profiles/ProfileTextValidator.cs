namespace PackageBuilder.Domain.Profiles;

internal static class ProfileTextValidator
{
    public static ProfileValidationError Validate(
        string? value,
        int maximumLength,
        ProfileValidationError nullError,
        ProfileValidationError emptyError,
        ProfileValidationError whitespaceError,
        ProfileValidationError edgeWhitespaceError,
        ProfileValidationError controlError,
        ProfileValidationError tooLongError)
    {
        if (value is null)
        {
            return nullError;
        }

        if (value.Length == 0)
        {
            return emptyError;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return whitespaceError;
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            return edgeWhitespaceError;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                return controlError;
            }
        }

        return value.Length > maximumLength ? tooLongError : ProfileValidationError.None;
    }

    public static ProfileValidationError ValidateExtensibleIdentifier(
        string? value,
        ProfileValidationError nullError,
        ProfileValidationError emptyError,
        ProfileValidationError whitespaceError,
        ProfileValidationError malformedError)
    {
        if (value is null)
        {
            return nullError;
        }

        if (value.Length == 0)
        {
            return emptyError;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return whitespaceError;
        }

        bool expectsLetterOrDigit = false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is >= 'a' and <= 'z' ||
                index > 0 && character is >= '0' and <= '9')
            {
                expectsLetterOrDigit = false;
                continue;
            }

            if (character == '-' && index > 0 && !expectsLetterOrDigit)
            {
                expectsLetterOrDigit = true;
                continue;
            }

            return malformedError;
        }

        return expectsLetterOrDigit ? malformedError : ProfileValidationError.None;
    }
}
