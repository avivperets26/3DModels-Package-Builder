using System.Collections.ObjectModel;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Profiles;

/// <summary>Identifies the caller-declared AI-assistance disclosure state.</summary>
public sealed class AiDisclosureState : IEquatable<AiDisclosureState>
{
    private AiDisclosureState(string canonicalIdentifier)
    {
        CanonicalIdentifier = canonicalIdentifier;
    }

    public static AiDisclosureState Undeclared { get; } = new("undeclared");

    public static AiDisclosureState NoAiAssistance { get; } = new("no-ai-assistance");

    public static AiDisclosureState AiAssisted { get; } = new("ai-assisted");

    private static readonly ReadOnlyCollection<AiDisclosureState> _all =
        Array.AsReadOnly([Undeclared, NoAiAssistance, AiAssisted]);

    public static IReadOnlyList<AiDisclosureState> All => _all;

    public string CanonicalIdentifier { get; }

    public static CanonicalIdentifierParseResult<AiDisclosureState> TryParse(string? identifier)
    {
        CanonicalIdentifierParseError error = CanonicalIdentifierParser.Validate(identifier);
        if (error != CanonicalIdentifierParseError.None)
        {
            return CanonicalIdentifierParseResult<AiDisclosureState>.Failure(error);
        }

        AiDisclosureState? value = All.FirstOrDefault(
            item => string.Equals(
                item.CanonicalIdentifier,
                identifier,
                StringComparison.Ordinal));
        return value is null
            ? CanonicalIdentifierParseResult<AiDisclosureState>.Failure(
                CanonicalIdentifierParseError.Unknown)
            : CanonicalIdentifierParseResult<AiDisclosureState>.Success(value);
    }

    public bool Equals(AiDisclosureState? other) =>
        other is not null &&
        string.Equals(CanonicalIdentifier, other.CanonicalIdentifier, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AiDisclosureState other && Equals(other);

    public override int GetHashCode() =>
        StableProfileHash.Create().Add(CanonicalIdentifier).ToHashCode();

    public override string ToString() => CanonicalIdentifier;
}
