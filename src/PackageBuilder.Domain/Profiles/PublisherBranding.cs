using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents optional logo and watermark declarations in stable role order.</summary>
public sealed class PublisherBranding : IEquatable<PublisherBranding>
{
    private PublisherBranding(ReadOnlyCollection<BrandingImage> images)
    {
        Images = images;
    }

    public IReadOnlyList<BrandingImage> Images { get; }

    public BrandingImage? Logo =>
        Images.FirstOrDefault(image => image.Role.Equals(BrandingImageRole.Logo));

    public BrandingImage? Watermark =>
        Images.FirstOrDefault(image => image.Role.Equals(BrandingImageRole.Watermark));

    public static ProfileValidationResult<PublisherBranding> Create(
        IEnumerable<BrandingImage?>? images)
    {
        if (images is null)
        {
            return Failure(ProfileValidationError.NullBrandingImages);
        }

        var byRole = new Dictionary<string, BrandingImage>(StringComparer.Ordinal);
        foreach (BrandingImage? image in images)
        {
            if (image is null)
            {
                return Failure(ProfileValidationError.NullBrandingImage);
            }

            if (!byRole.TryAdd(image.Role.CanonicalIdentifier, image))
            {
                return Failure(ProfileValidationError.DuplicateBrandingImageRole);
            }
        }

        if (byRole.Count == 0)
        {
            return Failure(ProfileValidationError.EmptyBrandingImages);
        }

        BrandingImage[] ordered =
        [
            .. BrandingImageRole.All
                .Where(role => byRole.ContainsKey(role.CanonicalIdentifier))
                .Select(role => byRole[role.CanonicalIdentifier]),
        ];
        return ProfileValidationResult<PublisherBranding>.Success(
            new PublisherBranding(Array.AsReadOnly(ordered)));
    }

    public bool Equals(PublisherBranding? other) =>
        other is not null && Images.SequenceEqual(other.Images);

    public override bool Equals(object? obj) => obj is PublisherBranding other && Equals(other);

    public override int GetHashCode()
    {
        var hash = StableProfileHash.Create();
        foreach (BrandingImage image in Images)
        {
            hash = hash.Add(image.GetHashCode());
        }

        return hash.ToHashCode();
    }

    private static ProfileValidationResult<PublisherBranding> Failure(
        ProfileValidationError error) =>
        ProfileValidationResult<PublisherBranding>.Failure(error);
}
