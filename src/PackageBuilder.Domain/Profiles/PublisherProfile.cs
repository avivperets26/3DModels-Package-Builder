using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Profiles;

/// <summary>
/// Represents immutable publisher configuration independently from marketplace identity.
/// </summary>
public sealed class PublisherProfile : IEquatable<PublisherProfile>
{
    private PublisherProfile(
        PublisherRoot root,
        PublisherDisplayName displayName,
        SupportContact supportContact,
        CopyrightNotice copyright,
        AiDisclosure aiDisclosure,
        PublisherBranding? branding)
    {
        Root = root;
        DisplayName = displayName;
        SupportContact = supportContact;
        Copyright = copyright;
        AiDisclosure = aiDisclosure;
        Branding = branding;
    }

    public PublisherRoot Root { get; }

    public PublisherDisplayName DisplayName { get; }

    public SupportContact SupportContact { get; }

    public CopyrightNotice Copyright { get; }

    public AiDisclosure AiDisclosure { get; }

    public PublisherBranding? Branding { get; }

    public static ProfileValidationResult<PublisherProfile> Create(
        PublisherRoot? root,
        PublisherDisplayName? displayName,
        SupportContact? supportContact,
        CopyrightNotice? copyright,
        AiDisclosure? aiDisclosure,
        PublisherBranding? branding = null)
    {
        return root is null
            ? Failure(ProfileValidationError.NullPublisherRoot)
            : displayName is null
            ? Failure(ProfileValidationError.NullPublisherProfileDisplayName)
            : supportContact is null
            ? Failure(ProfileValidationError.NullPublisherSupportContact)
            : copyright is null
            ? Failure(ProfileValidationError.NullPublisherCopyright)
            : aiDisclosure is null
            ? Failure(ProfileValidationError.NullPublisherAiDisclosure)
            : ProfileValidationResult<PublisherProfile>.Success(
            new PublisherProfile(
                root,
                displayName,
                supportContact,
                copyright,
                aiDisclosure,
                branding));
    }

    public bool Equals(PublisherProfile? other) =>
        other is not null &&
        Root.Equals(other.Root) &&
        DisplayName.Equals(other.DisplayName) &&
        SupportContact.Equals(other.SupportContact) &&
        Copyright.Equals(other.Copyright) &&
        AiDisclosure.Equals(other.AiDisclosure) &&
        Equals(Branding, other.Branding);

    public override bool Equals(object? obj) => obj is PublisherProfile other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(Root.Value)
            .Add(DisplayName.Value)
            .Add(SupportContact.GetHashCode())
            .Add(Copyright.GetHashCode())
            .Add(AiDisclosure.GetHashCode())
            .Add(Branding?.GetHashCode() ?? 0)
            .ToHashCode();

    private static ProfileValidationResult<PublisherProfile> Failure(
        ProfileValidationError error) =>
        ProfileValidationResult<PublisherProfile>.Failure(error);
}
