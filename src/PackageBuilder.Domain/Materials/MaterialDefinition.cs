using System.Collections.ObjectModel;
using PackageBuilder.Domain.Textures;

namespace PackageBuilder.Domain.Materials;

/// <summary>
/// Represents immutable renderer-independent material intent for target-specific compilers.
/// </summary>
public sealed class MaterialDefinition : IEquatable<MaterialDefinition>
{
    private MaterialDefinition(
        double metallicFactor,
        double roughnessFactor,
        double normalScale,
        EmissionProperties emission,
        double ambientOcclusionStrength,
        double heightScale,
        double opacity,
        SurfaceMode surfaceMode,
        double? alphaCutoff,
        UvTransform uvTransform,
        bool isDoubleSided,
        ReadOnlyCollection<TextureAssignment> textureAssignments)
    {
        MetallicFactor = metallicFactor;
        RoughnessFactor = roughnessFactor;
        NormalScale = normalScale;
        Emission = emission;
        AmbientOcclusionStrength = ambientOcclusionStrength;
        HeightScale = heightScale;
        Opacity = opacity;
        SurfaceMode = surfaceMode;
        AlphaCutoff = alphaCutoff;
        UvTransform = uvTransform;
        IsDoubleSided = isDoubleSided;
        TextureAssignments = textureAssignments;
    }

    /// <summary>Gets the metallic factor in the inclusive unit interval.</summary>
    public double MetallicFactor { get; }

    /// <summary>Gets the roughness factor in the inclusive unit interval.</summary>
    public double RoughnessFactor { get; }

    /// <summary>Gets the non-negative normal-map scale.</summary>
    public double NormalScale { get; }

    /// <summary>Gets the validated emission colour and intensity.</summary>
    public EmissionProperties Emission { get; }

    /// <summary>Gets the ambient-occlusion strength in the inclusive unit interval.</summary>
    public double AmbientOcclusionStrength { get; }

    /// <summary>Gets the finite signed displacement scale.</summary>
    public double HeightScale { get; }

    /// <summary>Gets the opacity factor in the inclusive unit interval.</summary>
    public double Opacity { get; }

    /// <summary>Gets the surface transparency mode.</summary>
    public SurfaceMode SurfaceMode { get; }

    /// <summary>Gets the cutout threshold, or null when the surface is not Cutout.</summary>
    public double? AlphaCutoff { get; }

    /// <summary>Gets the renderer-independent UV transform.</summary>
    public UvTransform UvTransform { get; }

    /// <summary>Gets a value indicating whether targets should render both face orientations.</summary>
    public bool IsDoubleSided { get; }

    /// <summary>Gets texture assignments in stable canonical texture-role order.</summary>
    public IReadOnlyList<TextureAssignment> TextureAssignments { get; }

    /// <summary>Validates and creates a renderer-independent material definition.</summary>
    public static MaterialDefinitionValidationResult Create(
        double metallicFactor,
        double roughnessFactor,
        double normalScale,
        EmissionProperties? emission,
        double ambientOcclusionStrength,
        double heightScale,
        double opacity,
        SurfaceMode? surfaceMode,
        double? alphaCutoff,
        UvTransform? uvTransform,
        bool isDoubleSided,
        IEnumerable<TextureAssignment?>? textureAssignments)
    {
        if (!IsUnitInterval(metallicFactor))
        {
            return Failure(MaterialDefinitionValidationError.InvalidMetallicFactor);
        }

        if (!IsUnitInterval(roughnessFactor))
        {
            return Failure(MaterialDefinitionValidationError.InvalidRoughnessFactor);
        }

        if (!IsNonNegativeFinite(normalScale))
        {
            return Failure(MaterialDefinitionValidationError.InvalidNormalScale);
        }

        if (emission is null)
        {
            return Failure(MaterialDefinitionValidationError.NullEmission);
        }

        if (!IsUnitInterval(ambientOcclusionStrength))
        {
            return Failure(MaterialDefinitionValidationError.InvalidAmbientOcclusionStrength);
        }

        if (!double.IsFinite(heightScale))
        {
            return Failure(MaterialDefinitionValidationError.InvalidHeightScale);
        }

        if (!IsUnitInterval(opacity))
        {
            return Failure(MaterialDefinitionValidationError.InvalidOpacity);
        }

        if (surfaceMode is null)
        {
            return Failure(MaterialDefinitionValidationError.NullSurfaceMode);
        }

        MaterialDefinitionValidationError surfaceError =
            ValidateSurface(surfaceMode, opacity, alphaCutoff);
        if (surfaceError != MaterialDefinitionValidationError.None)
        {
            return Failure(surfaceError);
        }

        if (uvTransform is null)
        {
            return Failure(MaterialDefinitionValidationError.NullUvTransform);
        }

        if (textureAssignments is null)
        {
            return Failure(MaterialDefinitionValidationError.NullTextureAssignments);
        }

        var byRole = new Dictionary<TextureRole, TextureAssignment>();
        foreach (TextureAssignment? assignment in textureAssignments)
        {
            if (assignment is null)
            {
                return Failure(MaterialDefinitionValidationError.NullTextureAssignment);
            }

            if (!byRole.TryAdd(assignment.Role, assignment))
            {
                return Failure(MaterialDefinitionValidationError.DuplicateTextureRole);
            }
        }

        var orderedAssignments = new List<TextureAssignment>(byRole.Count);
        foreach (TextureRole role in TextureRole.All)
        {
            if (byRole.TryGetValue(role, out TextureAssignment? assignment))
            {
                orderedAssignments.Add(assignment);
            }
        }

        return MaterialDefinitionValidationResult.Success(
            new MaterialDefinition(
                metallicFactor,
                roughnessFactor,
                normalScale,
                emission,
                ambientOcclusionStrength,
                heightScale,
                opacity,
                surfaceMode,
                alphaCutoff,
                uvTransform,
                isDoubleSided,
                orderedAssignments.AsReadOnly()));
    }

    /// <inheritdoc />
    public bool Equals(MaterialDefinition? other) =>
        other is not null &&
        MetallicFactor.Equals(other.MetallicFactor) &&
        RoughnessFactor.Equals(other.RoughnessFactor) &&
        NormalScale.Equals(other.NormalScale) &&
        Emission.Equals(other.Emission) &&
        AmbientOcclusionStrength.Equals(other.AmbientOcclusionStrength) &&
        HeightScale.Equals(other.HeightScale) &&
        Opacity.Equals(other.Opacity) &&
        SurfaceMode.Equals(other.SurfaceMode) &&
        Nullable.Equals(AlphaCutoff, other.AlphaCutoff) &&
        UvTransform.Equals(other.UvTransform) &&
        IsDoubleSided == other.IsDoubleSided &&
        TextureAssignments.SequenceEqual(other.TextureAssignments);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is MaterialDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        StableMaterialHash hash = StableMaterialHash.Create()
            .Add(MetallicFactor)
            .Add(RoughnessFactor)
            .Add(NormalScale)
            .Add(Emission.Red)
            .Add(Emission.Green)
            .Add(Emission.Blue)
            .Add(Emission.Intensity)
            .Add(AmbientOcclusionStrength)
            .Add(HeightScale)
            .Add(Opacity)
            .Add(SurfaceMode.CanonicalIdentifier)
            .Add(AlphaCutoff.HasValue)
            .Add(AlphaCutoff.GetValueOrDefault())
            .Add(UvTransform.ScaleU)
            .Add(UvTransform.ScaleV)
            .Add(UvTransform.OffsetU)
            .Add(UvTransform.OffsetV)
            .Add(IsDoubleSided);

        foreach (TextureAssignment assignment in TextureAssignments)
        {
            hash = hash
                .Add(assignment.Role.CanonicalIdentifier)
                .Add(assignment.SourceAsset.Kind.CanonicalIdentifier)
                .Add(assignment.SourceAsset.LogicalReference)
                .Add(assignment.SourceAsset.OriginalFileName ?? string.Empty)
                .Add(assignment.ColourSpace.CanonicalIdentifier)
                .Add(assignment.NormalConvention?.CanonicalIdentifier ?? string.Empty);
        }

        return hash.ToHashCode();
    }

    private static MaterialDefinitionValidationResult Failure(
        MaterialDefinitionValidationError error) =>
        MaterialDefinitionValidationResult.Failure(error);

    private static MaterialDefinitionValidationError ValidateSurface(
        SurfaceMode surfaceMode,
        double opacity,
        double? alphaCutoff)
    {
        return surfaceMode.Equals(SurfaceMode.Opaque)
            ? opacity != 1d
                ? MaterialDefinitionValidationError.OpaqueRequiresFullOpacity
                : alphaCutoff.HasValue
                ? MaterialDefinitionValidationError.AlphaCutoffNotApplicable
                : MaterialDefinitionValidationError.None
            : surfaceMode.Equals(SurfaceMode.Cutout)
            ? !alphaCutoff.HasValue
                ? MaterialDefinitionValidationError.CutoutRequiresAlphaCutoff
                : IsUnitInterval(alphaCutoff.Value)
                ? MaterialDefinitionValidationError.None
                : MaterialDefinitionValidationError.InvalidAlphaCutoff
            : alphaCutoff.HasValue
            ? MaterialDefinitionValidationError.AlphaCutoffNotApplicable
            : MaterialDefinitionValidationError.None;
    }

    private static bool IsUnitInterval(double value) =>
        double.IsFinite(value) && value is >= 0d and <= 1d;

    private static bool IsNonNegativeFinite(double value) =>
        double.IsFinite(value) && value >= 0d;
}
