namespace PackageBuilder.Domain.Profiles;

/// <summary>Identifies why PB-0107 publisher or marketplace profile data was rejected.</summary>
public enum ProfileValidationError
{
    None = 0,
    NullPublisherDisplayName,
    EmptyPublisherDisplayName,
    WhitespaceOnlyPublisherDisplayName,
    PublisherDisplayNameEdgeWhitespace,
    PublisherDisplayNameContainsControlCharacter,
    PublisherDisplayNameTooLong,
    NullSupportContactValue,
    EmptySupportContactValue,
    WhitespaceOnlySupportContactValue,
    SupportContactContainsWhitespace,
    SupportContactContainsControlCharacter,
    SupportContactTooLong,
    MalformedSupportEmail,
    MalformedSupportUrl,
    UnsafeSupportUrlScheme,
    SupportUrlContainsCredentials,
    NullCopyrightHolder,
    EmptyCopyrightHolder,
    WhitespaceOnlyCopyrightHolder,
    CopyrightHolderEdgeWhitespace,
    CopyrightHolderContainsControlCharacter,
    CopyrightHolderTooLong,
    NullCopyrightYearPolicyKind,
    MissingCopyrightYear,
    InvalidCopyrightYear,
    MissingCopyrightStartYear,
    UnexpectedCopyrightStartYear,
    InvalidCopyrightYearRange,
    NullAiDisclosureState,
    DisclosureTextNotAllowed,
    EmptyDisclosureText,
    WhitespaceOnlyDisclosureText,
    DisclosureTextEdgeWhitespace,
    DisclosureTextContainsControlCharacter,
    DisclosureTextTooLong,
    NullBrandingImageRole,
    NullBrandingSource,
    NonImageBrandingSource,
    NullBrandingImages,
    EmptyBrandingImages,
    NullBrandingImage,
    DuplicateBrandingImageRole,
    NullMarketplaceIdentifier,
    EmptyMarketplaceIdentifier,
    WhitespaceOnlyMarketplaceIdentifier,
    MalformedMarketplaceIdentifier,
    NullMarketplaceProfileIdentifier,
    EmptyMarketplaceProfileIdentifier,
    WhitespaceOnlyMarketplaceProfileIdentifier,
    MalformedMarketplaceProfileIdentifier,
    NullPublisherRoot,
    NullPublisherProfileDisplayName,
    NullPublisherSupportContact,
    NullPublisherCopyright,
    NullPublisherAiDisclosure,
    NullMarketplaceProfileMarketplace,
    NullMarketplaceProfileIdentity,
}

/// <summary>Represents task-local expected validation for PB-0107 values.</summary>
public sealed class ProfileValidationResult<T>
    where T : class
{
    private ProfileValidationResult(bool isValid, T? value, ProfileValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public ProfileValidationError Error { get; }

    internal static ProfileValidationResult<T> Success(T value) =>
        new(true, value, ProfileValidationError.None);

    internal static ProfileValidationResult<T> Failure(ProfileValidationError error) =>
        new(false, null, error);
}
