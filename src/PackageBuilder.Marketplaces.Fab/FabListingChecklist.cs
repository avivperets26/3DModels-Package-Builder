using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Customer-facing dependency disclosure; external downloads require a public HTTPS link.
/// Exact installed package versions are independently checked against Unity inspection by the composer.</summary>
public sealed record FabListingDependency(string Name, string Version, string Instructions, string? Url);

/// <summary>Explicit publisher choices. CreatedWithAi is not inferred from generic assistance or filenames.</summary>
public sealed record FabListingDraft(string ProductKey, string Title, string Description, string Usage,
    string TechnicalDetails, ImmutableArray<string> Subcategories, ImmutableArray<string> Platforms,
    ImmutableArray<FabListingDependency> Dependencies, PublisherProfile Publisher, bool? CreatedWithAi,
    string? UnityAssetStoreUrl = null);

/// <summary>Bounded UTF-8 listing data and a human checklist, with all manual steps deliberately unchecked.</summary>
public sealed record FabListingChecklist(string ListingJson, string ChecklistText, FabArtifactValidation Validation);

/// <summary>Prepares metadata without making network requests, creating listings or selecting prices/licences.</summary>
public static class FabListingChecklistGenerator
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public const string GuidanceUrl = "https://dev.epicgames.com/documentation/en-us/fab/publishing-assets-for-sale-or-free-download-in-fab";

    /// <summary>Validates explicit inputs and derives formats/versions from the selected listing and exact build lock.
    /// JSON encodes all user text as data; the checklist contains fixed prose and no executable markup.</summary>
    public static FabListingChecklist Generate(FabRequirementsProfile profile, FabValidationContext context,
        FabListingDraft? draft, BuildLock? versions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = new FabValidation(profile);
        if (!validation.Context(context))
        { return new("", "", validation.Result()); }
        if (draft is null || draft.ProductKey != context.ProductKey || !Text(draft.Title, 128) || draft.Title.Any(char.IsControl)
            || !Text(draft.Description, 16_384) || !Text(draft.Usage, 8192) || !Text(draft.TechnicalDetails, 16_384)
            || !Strings(draft.Subcategories, 16) || !Strings(draft.Platforms, 32)
            || draft.Dependencies.IsDefault || draft.Dependencies.Length > 256
            || draft.Dependencies.Any(item => item is null || !Text(item.Name, 128) || !Text(item.Version, 128)
                || !Text(item.Instructions, 2048) || item.Url is not null && !PublicUrl(item.Url))
            || draft.Dependencies.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != draft.Dependencies.Length
            || draft.Publisher is null || draft.Publisher.AiDisclosure.State.Equals(AiDisclosureState.Undeclared)
            || draft.CreatedWithAi is null || draft.CreatedWithAi == true && draft.Publisher.AiDisclosure.State.Equals(AiDisclosureState.NoAiAssistance)
            || draft.UnityAssetStoreUrl is not null && (!PublicUrl(draft.UnityAssetStoreUrl)
                || new Uri(draft.UnityAssetStoreUrl).Host != "assetstore.unity.com"))
        {
            validation.Add("FAB_LISTING_METADATA_INVALID", "Listing data, dependency disclosures or AI choices are incomplete.",
                "Provide bounded product text, categories, platforms, publisher disclosures and public dependency links.");
            return new("", "", validation.Result());
        }
        if (!BuildLockJson.Serialize(versions).IsSuccessful || !Equals(versions!.JobId, context.JobId)
            || !versions.MarketplaceProfiles.Contains(profile.BuildLockIdentity))
        {
            validation.Add("FAB_LISTING_VERSIONS_INVALID", "Listing versions do not match this job and requirements profile.", "Use the job's exact build.lock.");
            return new("", "", validation.Result());
        }
        string json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            productKey = draft.ProductKey,
            draft.Title,
            draft.Description,
            draft.Usage,
            draft.TechnicalDetails,
            category = context.Listing.Category,
            subcategories = draft.Subcategories.Order(StringComparer.Ordinal),
            formats = context.Listing.Formats.Order(StringComparer.Ordinal),
            platforms = draft.Platforms.Order(StringComparer.Ordinal),
            engineVersions = new[] { new { engine = "unity", version = versions.Unity.Value }, new { engine = "unreal", version = versions.Unreal.Value } }
                .Where(item => context.Listing.Formats.Contains(item.engine)),
            dependencies = draft.Dependencies.OrderBy(item => item.Name, StringComparer.Ordinal),
            publisher = draft.Publisher.DisplayName.Value,
            support = draft.Publisher.SupportContact.Value,
            aiAssistance = draft.Publisher.AiDisclosure.State.CanonicalIdentifier,
            aiDisclosure = draft.Publisher.AiDisclosure.Text,
            createdWithAi = draft.CreatedWithAi.Value,
            unityAssetStoreUrl = draft.UnityAssetStoreUrl,
            requirementsProfile = profile.BuildLockIdentity,
            guidance = GuidanceUrl,
        }, _json) + "\n";
        string checklist = "Fab manual-upload checklist\n\n"
            + "[ ] Review listing.json: title, description, usage, technical details, categories and platforms.\n"
            + "[ ] Keep the title clear and concise; Fab recommends about 30 characters or fewer.\n"
            + "[ ] Confirm dependency versions, setup instructions and public external download links.\n"
            + "[ ] Put dependencies and external links in the description; check English, tags and technical metrics.\n"
            + "[ ] Review third-party notices, support channels, maturity and paid promotional content.\n"
            + "[ ] Inspect model scale, pivots, topology, materials, colliders and representative gallery views.\n"
            + "[ ] Confirm AI creation/assistance disclosures and any Unity Asset Store listing link.\n"
            + "[ ] Review rights, content policy, licence, pricing, promotional and audience declarations in Fab.\n"
            + "[ ] Create or edit the listing; upload each selected inner delivery from release-manifest.json.\n"
            + (context.Listing.Formats.Contains("unreal") ? "[ ] Host the Unreal ZIP at a download link accessible without login; enter it as the Project File Link and retain it until the product is live.\n" : "")
            + "[ ] Add the gallery and designated thumbnail; check automatically imported technical fields.\n"
            + "[ ] Preview the listing and resolve all missing fields and validation findings.\n"
            + "[ ] Submit for Fab review and explicitly choose manual or automatic activation.\n\n"
            + "The outer release archive is a local handoff, not an additional listing download.\n"
            + "These steps are not completed by Package Builder; Fab approval remains external.\n"
            + "Guidance: " + GuidanceUrl + "\n";
        return new(json, checklist, validation.Result());
    }

    private static bool Strings(ImmutableArray<string> values, int limit) => !values.IsDefaultOrEmpty && values.Length <= limit
        && values.All(value => Text(value, 128)) && values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Length;

    private static bool Text(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim()
            || value.Any(character => char.IsControl(character) && character is not ('\n' or '\r')))
        { return false; }
        try
        { _ = new UTF8Encoding(false, true).GetByteCount(value); return true; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool PublicUrl(string value) => value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && !uri.IsLoopback
        && uri.HostNameType == UriHostNameType.Dns && uri.Host.Contains('.') && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
}
