using System.Collections.ObjectModel;
using System.Text;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Targets.Portable;

/// <summary>Identifies one expected PB-0504 input or rendering failure.</summary>
public enum PortableReadmeError
{
    None = 0,
    NullManifest,
    NullPublisherProfile,
    NullNamingProfile,
    NullDimensions,
    NullTextureReceipts,
    NullTextureReceipt,
    NullUsageInstructions,
    NullUsageInstruction,
    EmptyUsageInstructions,
    TooManyUsageInstructions,
    InvalidUsageInstruction,
    ManifestDoesNotTargetPortable,
    ManifestNamingMismatch,
    PublisherProfileMismatch,
    TextureRoleCollision,
    DuplicateTextureReceipt,
    MissingTextureReceipt,
    UnexpectedTextureReceipt,
}

/// <summary>Represents measured positive model extents in metres.</summary>
public sealed class PortableModelDimensions
{
    private PortableModelDimensions(double width, double height, double depth)
    {
        Width = width;
        Height = height;
        Depth = depth;
    }

    public double Width { get; }

    public double Height { get; }

    public double Depth { get; }

    public static PortableValidationResult<PortableModelDimensions> Create(
        double width,
        double height,
        double depth) =>
        !IsPositiveFinite(width) || !IsPositiveFinite(height) || !IsPositiveFinite(depth)
            ? PortableValidationResult<PortableModelDimensions>.Failure(
                PortableValidationError.InvalidArtifactQualifier)
            : PortableValidationResult<PortableModelDimensions>.Success(
                new PortableModelDimensions(width, height, depth));

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
}

/// <summary>Contains the fully validated typed inputs used to render one portable README.</summary>
public sealed class PortableReadmeRequest
{
    private PortableReadmeRequest(
        ProductManifest manifest,
        PublisherProfile publisherProfile,
        PortableNamingProfile namingProfile,
        PortableModelDimensions dimensions,
        PortableGlbVariant? glbVariant,
        ReadOnlyCollection<PortableTextureCopyReceipt> textureReceipts,
        ReadOnlyCollection<string> usageInstructions)
    {
        Manifest = manifest;
        PublisherProfile = publisherProfile;
        NamingProfile = namingProfile;
        Dimensions = dimensions;
        GlbVariant = glbVariant;
        TextureReceipts = textureReceipts;
        UsageInstructions = usageInstructions;
    }

    public ProductManifest Manifest { get; }

    public PublisherProfile PublisherProfile { get; }

    public PortableNamingProfile NamingProfile { get; }

    public PortableModelDimensions Dimensions { get; }

    public PortableGlbVariant? GlbVariant { get; }

    public IReadOnlyList<PortableTextureCopyReceipt> TextureReceipts { get; }

    public IReadOnlyList<string> UsageInstructions { get; }

    /// <summary>Validates cross-contract identity, texture coverage, and bounded usage prose.</summary>
    public static PortableReadmeResult<PortableReadmeRequest> Create(
        ProductManifest? manifest,
        PublisherProfile? publisherProfile,
        PortableNamingProfile? namingProfile,
        PortableModelDimensions? dimensions,
        PortableGlbVariant? glbVariant,
        IEnumerable<PortableTextureCopyReceipt?>? textureReceipts,
        IEnumerable<string?>? usageInstructions)
    {
        if (manifest is null)
        {
            return Failure(PortableReadmeError.NullManifest);
        }

        if (publisherProfile is null)
        {
            return Failure(PortableReadmeError.NullPublisherProfile);
        }

        if (namingProfile is null)
        {
            return Failure(PortableReadmeError.NullNamingProfile);
        }

        if (dimensions is null)
        {
            return Failure(PortableReadmeError.NullDimensions);
        }

        if (textureReceipts is null)
        {
            return Failure(PortableReadmeError.NullTextureReceipts);
        }

        if (usageInstructions is null)
        {
            return Failure(PortableReadmeError.NullUsageInstructions);
        }

        if (!manifest.Targets.Any(target => target.Equals(PackageBuilder.Domain.Targets.BuildTarget.Portable)))
        {
            return Failure(PortableReadmeError.ManifestDoesNotTargetPortable);
        }

        if (!manifest.AssetId.Equals(namingProfile.AssetId) ||
            !manifest.FolderName.Equals(namingProfile.FolderName))
        {
            return Failure(PortableReadmeError.ManifestNamingMismatch);
        }

        if (!manifest.PublisherProfileReference.Equals(publisherProfile.Root))
        {
            return Failure(PortableReadmeError.PublisherProfileMismatch);
        }

        PortableTextureCopyReceipt?[] receipts = [.. textureReceipts];
        if (receipts.Any(static receipt => receipt is null))
        {
            return Failure(PortableReadmeError.NullTextureReceipt);
        }

        string?[] instructions = [.. usageInstructions];
        if (instructions.Any(static instruction => instruction is null))
        {
            return Failure(PortableReadmeError.NullUsageInstruction);
        }

        if (instructions.Length == 0)
        {
            return Failure(PortableReadmeError.EmptyUsageInstructions);
        }

        if (instructions.Length > 32)
        {
            return Failure(PortableReadmeError.TooManyUsageInstructions);
        }

        if (instructions.Any(static instruction => !IsValidInstruction(instruction!)))
        {
            return Failure(PortableReadmeError.InvalidUsageInstruction);
        }

        PortableReadmeError textureError = ValidateTextures(manifest, receipts!);
        if (textureError != PortableReadmeError.None)
        {
            return Failure(textureError);
        }

        PortableTextureCopyReceipt[] orderedReceipts =
        [
            .. receipts.Cast<PortableTextureCopyReceipt>().OrderBy(
                static receipt => receipt.Role.CanonicalIdentifier,
                StringComparer.Ordinal),
        ];
        return PortableReadmeResult<PortableReadmeRequest>.Success(
            new PortableReadmeRequest(
                manifest,
                publisherProfile,
                namingProfile,
                dimensions,
                glbVariant,
                Array.AsReadOnly(orderedReceipts),
                Array.AsReadOnly(instructions.Cast<string>().ToArray())));
    }

    private static PortableReadmeError ValidateTextures(
        ProductManifest manifest,
        IReadOnlyCollection<PortableTextureCopyReceipt> receipts)
    {
        var expected = new Dictionary<string, PackageBuilder.Domain.Textures.TextureAssignment>(
            StringComparer.Ordinal);
        foreach (PackageBuilder.Domain.Textures.TextureAssignment assignment in
                 manifest.Materials.SelectMany(static material => material.Definition.TextureAssignments)
                     .Where(static assignment => IsPortableRole(assignment.Role)))
        {
            if (expected.TryGetValue(assignment.Role.CanonicalIdentifier, out TextureAssignment? existing) &&
                !existing.SourceAsset.Equals(assignment.SourceAsset))
            {
                return PortableReadmeError.TextureRoleCollision;
            }

            expected[assignment.Role.CanonicalIdentifier] = assignment;
        }

        var actual = new Dictionary<string, PortableTextureCopyReceipt>(StringComparer.Ordinal);
        foreach (PortableTextureCopyReceipt receipt in receipts)
        {
            if (!actual.TryAdd(receipt.Role.CanonicalIdentifier, receipt))
            {
                return PortableReadmeError.DuplicateTextureReceipt;
            }

            if (!expected.TryGetValue(receipt.Role.CanonicalIdentifier, out TextureAssignment? assignment) ||
                !assignment.Equals(receipt.Assignment))
            {
                return PortableReadmeError.UnexpectedTextureReceipt;
            }
        }

        return expected.Keys.Any(key => !actual.ContainsKey(key))
            ? PortableReadmeError.MissingTextureReceipt
            : PortableReadmeError.None;
    }

    private static bool IsPortableRole(PackageBuilder.Domain.Textures.TextureRole role) =>
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.Albedo) ||
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.Normal) ||
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.Metallic) ||
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.Roughness) ||
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.Emission) ||
        role.Equals(PackageBuilder.Domain.Textures.TextureRole.AmbientOcclusion);

    private static bool IsValidInstruction(string value) =>
        value.Length is > 0 and <= 512 &&
        !char.IsWhiteSpace(value[0]) &&
        !char.IsWhiteSpace(value[^1]) &&
        !value.Any(char.IsControl);

    private static PortableReadmeResult<PortableReadmeRequest> Failure(PortableReadmeError error) =>
        PortableReadmeResult<PortableReadmeRequest>.Failure(error);
}

/// <summary>Contains deterministic LF text and its UTF-8 representation without a byte-order mark.</summary>
public sealed class PortableReadmeDocument
{
    internal PortableReadmeDocument(string text)
    {
        Text = text;
        Utf8Bytes = Array.AsReadOnly(Encoding.UTF8.GetBytes(text));
    }

    public string Text { get; }

    public IReadOnlyList<byte> Utf8Bytes { get; }
}

/// <summary>Returns either one typed README value or one stable expected-input error.</summary>
public sealed class PortableReadmeResult<T>
{
    private PortableReadmeResult(bool isValid, T? value, PortableReadmeError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public T? Value { get; }

    public PortableReadmeError Error { get; }

    internal static PortableReadmeResult<T> Success(T value) =>
        new(true, value, PortableReadmeError.None);

    internal static PortableReadmeResult<T> Failure(PortableReadmeError error) =>
        new(false, default, error);
}
