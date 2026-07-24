namespace PackageBuilder.Domain.Materials;

/// <summary>Identifies why a material definition could not be created.</summary>
public enum MaterialDefinitionValidationError
{
    /// <summary>The material definition is valid.</summary>
    None = 0,

    /// <summary>The metallic factor was outside the inclusive unit interval or not finite.</summary>
    InvalidMetallicFactor,

    /// <summary>The roughness factor was outside the inclusive unit interval or not finite.</summary>
    InvalidRoughnessFactor,

    /// <summary>The normal scale was negative or not finite.</summary>
    InvalidNormalScale,

    /// <summary>Emission properties were not supplied.</summary>
    NullEmission,

    /// <summary>The ambient-occlusion strength was outside the unit interval or not finite.</summary>
    InvalidAmbientOcclusionStrength,

    /// <summary>The signed height scale was not finite.</summary>
    InvalidHeightScale,

    /// <summary>The opacity was outside the inclusive unit interval or not finite.</summary>
    InvalidOpacity,

    /// <summary>The surface mode was not supplied.</summary>
    NullSurfaceMode,

    /// <summary>An opaque material did not have full opacity.</summary>
    OpaqueRequiresFullOpacity,

    /// <summary>A cutout material did not supply an alpha cutoff.</summary>
    CutoutRequiresAlphaCutoff,

    /// <summary>An opaque or transparent material supplied an alpha cutoff.</summary>
    AlphaCutoffNotApplicable,

    /// <summary>The alpha cutoff was outside the inclusive unit interval or not finite.</summary>
    InvalidAlphaCutoff,

    /// <summary>A UV transform was not supplied.</summary>
    NullUvTransform,

    /// <summary>The texture-assignment collection was null.</summary>
    NullTextureAssignments,

    /// <summary>The texture-assignment collection contained null.</summary>
    NullTextureAssignment,

    /// <summary>More than one texture assignment used the same canonical role.</summary>
    DuplicateTextureRole,
}

/// <summary>Represents expected success or failure when creating a material definition.</summary>
public sealed class MaterialDefinitionValidationResult
{
    private MaterialDefinitionValidationResult(
        bool isValid,
        MaterialDefinition? value,
        MaterialDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether validation succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable material definition, or null on failure.</summary>
    public MaterialDefinition? Value { get; }

    /// <summary>Gets the expected-input rejection reason.</summary>
    public MaterialDefinitionValidationError Error { get; }

    internal static MaterialDefinitionValidationResult Success(MaterialDefinition value) =>
        new(true, value, MaterialDefinitionValidationError.None);

    internal static MaterialDefinitionValidationResult Failure(
        MaterialDefinitionValidationError error) =>
        new(false, null, error);
}
