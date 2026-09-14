using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using PackageBuilder.Contracts.BuildLocks;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Source revision dates are separate from the local review/adoption date.</summary>
public sealed record FabRequirementSource(string Id, string Url, DateOnly? UpdatedOn, DateOnly ReviewedOn);

/// <summary>A sourced assertion, numeric bound or explicit unresolved rule. Local defaults are labelled policy.</summary>
public sealed record FabRequirementRule(
    string Id, string Section, string Origin, string Source, string Status, string Summary,
    long? Limit, string? Unit, string? Comparison);

/// <summary>Allowed download formats per listing category, plus Package Builder companion defaults.</summary>
public sealed record FabListingRequirements(
    string Category, ImmutableArray<string> Formats, bool DocumentationRequired, bool MediaRequired,
    string Source, string CompanionPolicy);

/// <summary>Strict transport shape; only validated snapshots are used by the resolver or updater.</summary>
public sealed record FabRequirementsDocument(
    int SchemaVersion, string Marketplace, string Profile, string Version, DateOnly EffectiveOn,
    string EffectiveDateBasis, ImmutableArray<FabRequirementSource> Sources,
    ImmutableArray<FabRequirementRule> Rules, ImmutableArray<FabListingRequirements> Listings);

/// <summary>Immutable validated canonical UTF-8 snapshot. Identity binds content, not local file locations.</summary>
public sealed class FabRequirementsProfile
{
    internal FabRequirementsProfile(FabRequirementsDocument document, string canonicalJson)
    {
        Document = document;
        CanonicalJson = canonicalJson;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }

    public FabRequirementsDocument Document { get; }
    public string CanonicalJson { get; }
    public string Sha256 { get; }

    /// <summary>Fits the existing build.lock v1 version field and preserves both revision and exact digest.</summary>
    public BuildLockMarketplaceProfile BuildLockIdentity => new(
        Document.Marketplace, Document.Profile, $"{Document.Version}+sha256.{Sha256}");

    public FabRequirementRule Rule(string id) => Document.Rules.Single(rule => rule.Id == id);
}
