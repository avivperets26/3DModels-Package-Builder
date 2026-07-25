using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Contracts.Profiles;

/// <summary>Strict publisher-profile JSON contract for schema version 1.</summary>
public static class PublisherProfileJson
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumInputCharacters = ProfileJsonInfrastructure.MaximumInputCharacters;
    public const int MaximumDepth = ProfileJsonInfrastructure.MaximumDepth;
    public const string SchemaIdentifier =
        "https://schemas.packagebuilder.dev/publisher-profile/v1";
    public const string SchemaDraft = ProfileJsonInfrastructure.SchemaDraft;

    private const string SchemaResource =
        "PackageBuilder.Contracts.Schemas.publisher-profile.schema.json";
    private static readonly Lazy<string> _schemaText =
        new(() => ProfileJsonInfrastructure.ReadSchemaText(SchemaResource));
    private static readonly Lazy<JsonSchema> _schema =
        new(() => ProfileJsonInfrastructure.BuildSchema(_schemaText.Value));

    public static string SchemaText => _schemaText.Value;

    public static ProfileJsonSchemaValidationResult ValidateSchemaDefinition() =>
        ValidateSchemaDefinition(SchemaText);

    public static ProfileJsonSchemaValidationResult ValidateSchemaDefinition(string? schemaText) =>
        ProfileJsonInfrastructure.ValidateSchemaDefinition(schemaText, SchemaIdentifier);

    public static ProfileJsonSchemaValidationResult ValidateJsonAgainstSchema(string? json) =>
        ProfileJsonInfrastructure.ValidateJsonAgainstSchema(json, _schema.Value);

    public static ProfileJsonSerializationResult Serialize(PublisherProfile? profile)
    {
        if (profile is null)
        {
            return ProfileJsonSerializationResult.Failure(ProfileJsonError.NullProfile);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("root", profile.Root.Value);
            writer.WriteString("displayName", profile.DisplayName.Value);
            WriteSupport(writer, profile.SupportContact);
            WriteCopyright(writer, profile.Copyright);
            WriteDisclosure(writer, profile.AiDisclosure);
            if (profile.Branding is not null)
            {
                writer.WritePropertyName("branding");
                writer.WriteStartObject();
                writer.WritePropertyName("images");
                writer.WriteStartArray();
                foreach (BrandingImage image in profile.Branding.Images)
                {
                    writer.WriteStartObject();
                    writer.WriteString("role", image.Role.CanonicalIdentifier);
                    writer.WritePropertyName("source");
                    WriteSource(writer, image.Source);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return ProfileJsonSerializationResult.Success(
            Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    public static ProfileJsonDeserializationResult<PublisherProfile> Deserialize(string? json)
    {
        ProfileJsonError error =
            ProfileJsonInfrastructure.TryParse(json, _schema.Value, out JsonDocument? document);
        if (error != ProfileJsonError.None)
        {
            return ProfileJsonDeserializationResult<PublisherProfile>.Failure(error);
        }

        using (document!)
        {
            JsonElement root = document!.RootElement;
            PublisherRoot publisherRoot =
                PublisherRoot.Create(root.GetProperty("root").GetString()).Value!;
            PublisherDisplayName displayName =
                PublisherDisplayName.Create(root.GetProperty("displayName").GetString()).Value!;

            ProfileValidationResult<SupportContact> support =
                ParseSupport(root.GetProperty("supportContact"));
            if (!support.IsValid)
            {
                return DomainFailure(support.Error.ToString());
            }

            (CopyrightNotice? copyright, ProfileValidationError copyrightError) =
                ParseCopyright(root.GetProperty("copyright"));
            if (copyright is null)
            {
                return DomainFailure(copyrightError.ToString());
            }

            AiDisclosure disclosure = ParseDisclosure(root.GetProperty("aiDisclosure"));

            string? detail = null;
            PublisherBranding? branding = root.TryGetProperty(
                "branding",
                out JsonElement brandingElement)
                ? ParseBranding(brandingElement, out detail)
                : null;
            if (root.TryGetProperty("branding", out _) && branding is null)
            {
                return DomainFailure(detail!);
            }

            PublisherProfile profile = PublisherProfile.Create(
                publisherRoot,
                displayName,
                support.Value,
                copyright,
                disclosure,
                branding).Value!;
            return ProfileJsonDeserializationResult<PublisherProfile>.Success(profile);
        }
    }

    private static ProfileValidationResult<SupportContact> ParseSupport(JsonElement element)
    {
        string kind = element.GetProperty("kind").GetString()!;
        string? value = element.GetProperty("value").GetString();
        return kind == "email"
            ? SupportContact.CreateEmail(value)
            : SupportContact.CreateSecureUrl(value);
    }

    private static (CopyrightNotice? Value, ProfileValidationError Error) ParseCopyright(
        JsonElement element)
    {
        CopyrightHolder holder =
            CopyrightHolder.Create(element.GetProperty("holder").GetString()).Value!;
        JsonElement policyElement = element.GetProperty("yearPolicy");
        CopyrightYearPolicyKind kind =
            CopyrightYearPolicyKind.TryParse(
                policyElement.GetProperty("kind").GetString()).Value!;
        ProfileValidationResult<CopyrightYearPolicy> policy = CopyrightYearPolicy.Create(
            kind,
            policyElement.GetProperty("year").GetInt32(),
            policyElement.TryGetProperty("startYear", out JsonElement start)
                ? start.GetInt32()
                : null);
        if (!policy.IsValid)
        {
            return (null, policy.Error);
        }

        ProfileValidationResult<CopyrightNotice> notice =
            CopyrightNotice.Create(holder, policy.Value);
        return (notice.Value, notice.Error);
    }

    private static AiDisclosure ParseDisclosure(JsonElement element)
    {
        AiDisclosureState state =
            AiDisclosureState.TryParse(element.GetProperty("state").GetString()).Value!;
        return AiDisclosure.Create(
            state,
            element.TryGetProperty("text", out JsonElement text)
                ? text.GetString()
                : null).Value!;
    }

    private static PublisherBranding? ParseBranding(JsonElement element, out string? detail)
    {
        var images = new List<BrandingImage?>();
        foreach (JsonElement imageElement in element.GetProperty("images").EnumerateArray())
        {
            BrandingImageRole role =
                BrandingImageRole.TryParse(
                    imageElement.GetProperty("role").GetString()).Value!;

            SourceAsset? source = ParseSource(imageElement.GetProperty("source"), out detail);
            if (source is null)
            {
                return null;
            }

            images.Add(BrandingImage.Create(role, source).Value!);
        }

        detail = null;
        return PublisherBranding.Create(images).Value!;
    }

    private static SourceAsset? ParseSource(JsonElement element, out string? detail)
    {
        SourceAssetKind kind =
            SourceAssetKind.TryParse(element.GetProperty("kind").GetString()).Value!;
        SourceAssetValidationResult source = SourceAsset.Create(
            kind,
            element.GetProperty("logicalReference").GetString(),
            element.TryGetProperty("originalFileName", out JsonElement original)
                ? original.GetString()
                : null);
        detail = source.IsValid ? null : source.Error.ToString();
        return source.Value;
    }

    private static void WriteSupport(Utf8JsonWriter writer, SupportContact support)
    {
        writer.WritePropertyName("supportContact");
        writer.WriteStartObject();
        writer.WriteString(
            "kind",
            support.Kind == SupportContactKind.Email ? "email" : "secure-url");
        writer.WriteString("value", support.Value);
        writer.WriteEndObject();
    }

    private static void WriteCopyright(Utf8JsonWriter writer, CopyrightNotice copyright)
    {
        writer.WritePropertyName("copyright");
        writer.WriteStartObject();
        writer.WriteString("holder", copyright.Holder.Value);
        writer.WritePropertyName("yearPolicy");
        writer.WriteStartObject();
        writer.WriteString("kind", copyright.YearPolicy.Kind.CanonicalIdentifier);
        if (copyright.YearPolicy.StartYear.HasValue)
        {
            writer.WriteNumber("startYear", copyright.YearPolicy.StartYear.Value);
        }

        writer.WriteNumber("year", copyright.YearPolicy.Year);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteDisclosure(Utf8JsonWriter writer, AiDisclosure disclosure)
    {
        writer.WritePropertyName("aiDisclosure");
        writer.WriteStartObject();
        writer.WriteString("state", disclosure.State.CanonicalIdentifier);
        if (disclosure.Text is not null)
        {
            writer.WriteString("text", disclosure.Text);
        }

        writer.WriteEndObject();
    }

    private static void WriteSource(Utf8JsonWriter writer, SourceAsset source)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", source.Kind.CanonicalIdentifier);
        writer.WriteString("logicalReference", source.LogicalReference);
        if (source.OriginalFileName is not null)
        {
            writer.WriteString("originalFileName", source.OriginalFileName);
        }

        writer.WriteEndObject();
    }

    private static ProfileJsonDeserializationResult<PublisherProfile> DomainFailure(
        string detail) =>
        ProfileJsonDeserializationResult<PublisherProfile>.Failure(
            ProfileJsonError.DomainViolation,
            $"Publisher profile violates the Domain contract: {detail}.");
}
