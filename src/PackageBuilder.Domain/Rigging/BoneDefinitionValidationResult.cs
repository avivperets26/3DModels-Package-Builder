namespace PackageBuilder.Domain.Rigging;

/// <summary>Identifies why a bone definition could not be created.</summary>
public enum BoneDefinitionValidationError
{
    None = 0,
    NullIdentity,
    EmptyIdentity,
    WhitespaceOnlyIdentity,
    IdentityEdgeWhitespace,
    IdentityContainsControlCharacter,
    EmptyParentIdentity,
    WhitespaceOnlyParentIdentity,
    ParentIdentityEdgeWhitespace,
    ParentIdentityContainsControlCharacter,
}

/// <summary>Represents expected success or failure when creating a bone definition.</summary>
public sealed class BoneDefinitionValidationResult
{
    private BoneDefinitionValidationResult(
        bool isValid,
        BoneDefinition? value,
        BoneDefinitionValidationError error)
    {
        IsValid = isValid;
        Value = value;
        Error = error;
    }

    public bool IsValid { get; }

    public BoneDefinition? Value { get; }

    public BoneDefinitionValidationError Error { get; }

    internal static BoneDefinitionValidationResult Success(BoneDefinition value) =>
        new(true, value, BoneDefinitionValidationError.None);

    internal static BoneDefinitionValidationResult Failure(BoneDefinitionValidationError error) =>
        new(false, null, error);
}
