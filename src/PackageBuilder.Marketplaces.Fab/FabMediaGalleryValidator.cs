using System.Collections.Immutable;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Contracts.Media;
using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Preview;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Final image bytes and manifest-derived view associations. A thumbnail designates a gallery
/// image; it is counted once. Associations come from capture evidence, never inferred from a filename.</summary>
public sealed record FabGalleryImage(BuildArtifactId ArtifactId, ArtifactContentIdentity Content,
    string FileName, ReadOnlyMemory<byte> Bytes, string ProductKey, PreviewViewKind View,
    ImmutableArray<string> ItemKeys, bool IsThumbnail);

/// <summary>Validates final encoded images against the exact profile and product view requirements.
/// Codec work is shared with the existing WPF imaging adapter; this class owns only Fab rules.</summary>
public sealed class FabMediaGalleryValidator(IEncodedImageInspector inspector)
{
    /// <summary>Hashes and decodes owned copies of final image bytes; does not read files or upload media.</summary>
    public FabArtifactValidation Validate(FabRequirementsProfile profile, FabValidationContext? context,
        ImmutableArray<FabGalleryImage> images, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = new FabValidation(profile);
        if (!validation.Context(context))
        { return validation.Result(); }
        if (images.IsDefaultOrEmpty || images.Length > 256 || images.Any(image => image is null))
        {
            validation.Add("FAB_GALLERY_MISSING", "A bounded gallery with a representative thumbnail is required.", "Generate the product gallery and select its thumbnail.");
            return validation.Result();
        }
        validation.RequireRule("image-formats", "media");
        validation.RequireRule("gallery-representation", "media");
        validation.RequireRule("gallery-delivery", "media");
        if (images.Count(image => image.IsThumbnail) != 1)
        { validation.Add("FAB_THUMBNAIL_INVALID", "Select exactly one gallery image as the representative thumbnail.", "Choose one validated product image."); }
        if (images.Select(image => image.ArtifactId).Distinct().Count() != images.Length
            || images.Select(image => image.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != images.Length)
        { validation.Add("FAB_GALLERY_DUPLICATE", "Gallery artifacts or filenames are duplicated.", "Use distinct delivery images."); }
        long total = 0;
        var covered = new HashSet<string>(StringComparer.Ordinal);
        var views = new HashSet<PreviewViewKind>();
        bool overview = false;
        foreach (FabGalleryImage image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (image.ArtifactId is null || image.Content is null || !FabContentPath.Valid(image.FileName)
                || image.FileName.Contains('/') || image.ItemKeys.IsDefaultOrEmpty
                || image.ItemKeys.Any(string.IsNullOrWhiteSpace) || image.ItemKeys.Distinct().Count() != image.ItemKeys.Length)
            { validation.Add("FAB_MEDIA_EVIDENCE_INVALID", "An image has incomplete identity or capture associations.", "Regenerate image and capture evidence."); continue; }
            BuildArtifactId id = image.ArtifactId;
            total += image.Bytes.Length;
            _ = validation.Bound("image-bytes", "media", "bytes", image.Bytes.Length, id);
            if (image.Bytes.Length is < 8 or > 32_000_000)
            { validation.Add("FAB_IMAGE_INVALID", "Image input is outside the supported decoder safety budget.", "Generate a bounded image file.", id); continue; }
            // Own the input while hashing and decoding so caller mutation cannot split these observations.
            byte[] bytes = image.Bytes.ToArray();
            if (image.Content.Bytes != bytes.Length || image.Content.Sha256.Value != Convert.ToHexStringLower(SHA256.HashData(bytes)))
            { validation.Add("FAB_CONTENT_CHANGED", "Image bytes differ from their artifact receipt.", "Regenerate the final image receipt.", id); continue; }
            if (image.ProductKey != context!.ProductKey || image.ItemKeys.Any(item => !context.ItemKeys.Contains(item, StringComparer.Ordinal))
                || image.View is null || !PreviewPresentationSpecification.IsViewAllowed(context.Listing.ProductCase, image.View))
            { validation.Add("FAB_MEDIA_IRRELEVANT", "Image capture evidence does not represent this listing.", "Capture views of the included product items.", id); continue; }
            try
            {
                EncodedImageInfo info = inspector.Inspect(bytes);
                if (info is null || !Enum.IsDefined(info.Format) || info.Width <= 0 || info.Height <= 0)
                { throw new InvalidDataException("Invalid decoder observation."); }
                string extension = Path.GetExtension(image.FileName);
                if (info.Format == EncodedImageFormat.Png ? !extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    : !new[] { ".jpg", ".jpeg" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                { validation.Add("FAB_IMAGE_FORMAT_INVALID", "Image filename does not match its decoded PNG/JPEG container.", "Use the correct filename extension.", id); }
                bool dimensions = validation.Bound("image-width", "media", "pixels", info.Width, id)
                    & validation.Bound("image-height", "media", "pixels", info.Height, id);
                if (dimensions)
                {
                    covered.UnionWith(image.ItemKeys);
                    _ = views.Add(image.View);
                    overview |= (image.View.Equals(PreviewViewKind.SetOverview) || image.View.Equals(PreviewViewKind.CollectionOverview))
                        && context.ItemKeys.All(item => image.ItemKeys.Contains(item, StringComparer.Ordinal));
                }
            }
            catch (InvalidDataException)
            { validation.Add("FAB_IMAGE_INVALID", "The final file is not a complete supported PNG/JPEG image.", "Re-encode and validate the image.", id); }
        }
        _ = validation.Bound("gallery-bytes", "media", "bytes", total);
        if (!context!.ItemKeys.All(covered.Contains) || context.ItemKeys.Length > 1 && !overview
            || PreviewPresentationSpecification.RequiredViewError(context.Listing.ProductCase, views) != PreviewPresentationValidationError.None)
        { validation.Add("FAB_GALLERY_COVERAGE_MISSING", "Gallery views do not cover every item or the required multi-item overview.", "Include a validated overview showing all included items."); }
        validation.Review(["media"]);
        cancellationToken.ThrowIfCancellationRequested();
        return validation.Result();
    }
}
