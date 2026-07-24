using PackageBuilder.Domain.Assets;

namespace PackageBuilder.Domain.Profiles;

/// <summary>Associates an approved branding role with an existing image source asset.</summary>
public sealed class BrandingImage : IEquatable<BrandingImage>
{
    private BrandingImage(BrandingImageRole role, SourceAsset source)
    {
        Role = role;
        Source = source;
    }

    public BrandingImageRole Role { get; }

    public SourceAsset Source { get; }

    public static ProfileValidationResult<BrandingImage> Create(
        BrandingImageRole? role,
        SourceAsset? source)
    {
        if (role is null)
        {
            return ProfileValidationResult<BrandingImage>.Failure(
                ProfileValidationError.NullBrandingImageRole);
        }

        if (source is null)
        {
            return ProfileValidationResult<BrandingImage>.Failure(
                ProfileValidationError.NullBrandingSource);
        }

        // Branding declares image intent only; decoding and rendering belong to later tasks.
        return !source.Kind.Equals(SourceAssetKind.Image)
            ? ProfileValidationResult<BrandingImage>.Failure(
                ProfileValidationError.NonImageBrandingSource)
            : ProfileValidationResult<BrandingImage>.Success(new BrandingImage(role, source));
    }

    public bool Equals(BrandingImage? other) =>
        other is not null && Role.Equals(other.Role) && Source.Equals(other.Source);

    public override bool Equals(object? obj) => obj is BrandingImage other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(Role.CanonicalIdentifier)
            .Add(Source.GetHashCode())
            .ToHashCode();
}
