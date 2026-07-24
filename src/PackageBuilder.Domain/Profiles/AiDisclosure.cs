namespace PackageBuilder.Domain.Profiles;

/// <summary>Represents an explicit disclosure state and optional caller-authored text.</summary>
public sealed class AiDisclosure : IEquatable<AiDisclosure>
{
    public const int MaximumTextLength = 4096;

    private AiDisclosure(AiDisclosureState state, string? text)
    {
        State = state;
        Text = text;
    }

    public AiDisclosureState State { get; }

    public string? Text { get; }

    public static ProfileValidationResult<AiDisclosure> Create(
        AiDisclosureState? state,
        string? text = null)
    {
        if (state is null)
        {
            return Failure(ProfileValidationError.NullAiDisclosureState);
        }

        // Undeclared means no claim has been supplied; attaching prose would contradict it.
        if (state.Equals(AiDisclosureState.Undeclared) && text is not null)
        {
            return Failure(ProfileValidationError.DisclosureTextNotAllowed);
        }

        if (text is not null)
        {
            ProfileValidationError error = ProfileTextValidator.Validate(
                text,
                MaximumTextLength,
                ProfileValidationError.DisclosureTextNotAllowed,
                ProfileValidationError.EmptyDisclosureText,
                ProfileValidationError.WhitespaceOnlyDisclosureText,
                ProfileValidationError.DisclosureTextEdgeWhitespace,
                ProfileValidationError.DisclosureTextContainsControlCharacter,
                ProfileValidationError.DisclosureTextTooLong);
            if (error != ProfileValidationError.None)
            {
                return Failure(error);
            }
        }

        return ProfileValidationResult<AiDisclosure>.Success(new AiDisclosure(state, text));
    }

    public bool Equals(AiDisclosure? other) =>
        other is not null &&
        State.Equals(other.State) &&
        string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AiDisclosure other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create()
            .Add(State.CanonicalIdentifier)
            .Add(Text ?? string.Empty)
            .ToHashCode();

    private static ProfileValidationResult<AiDisclosure> Failure(
        ProfileValidationError error) =>
        ProfileValidationResult<AiDisclosure>.Failure(error);
}
