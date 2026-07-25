using System.Buffers;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Contracts.Profiles;

/// <summary>Strict generic marketplace-identity JSON contract for schema version 1.</summary>
public static class MarketplaceProfileJson
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumInputCharacters = ProfileJsonInfrastructure.MaximumInputCharacters;
    public const int MaximumDepth = ProfileJsonInfrastructure.MaximumDepth;
    public const string SchemaIdentifier =
        "https://schemas.packagebuilder.dev/marketplace-profile/v1";
    public const string SchemaDraft = ProfileJsonInfrastructure.SchemaDraft;

    private const string SchemaResource =
        "PackageBuilder.Contracts.Schemas.marketplace-profile.schema.json";
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

    public static ProfileJsonSerializationResult Serialize(MarketplaceProfile? profile)
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
            writer.WriteString("marketplace", profile.Marketplace.Value);
            writer.WriteString("profile", profile.Identity.Value);
            writer.WriteEndObject();
        }

        return ProfileJsonSerializationResult.Success(
            Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    public static ProfileJsonDeserializationResult<MarketplaceProfile> Deserialize(string? json)
    {
        ProfileJsonError error =
            ProfileJsonInfrastructure.TryParse(json, _schema.Value, out JsonDocument? document);
        if (error != ProfileJsonError.None)
        {
            return ProfileJsonDeserializationResult<MarketplaceProfile>.Failure(error);
        }

        using (document!)
        {
            JsonElement root = document!.RootElement;
            MarketplaceIdentifier marketplace =
                MarketplaceIdentifier.Create(root.GetProperty("marketplace").GetString()).Value!;
            MarketplaceProfileIdentifier identity =
                MarketplaceProfileIdentifier.Create(root.GetProperty("profile").GetString()).Value!;
            MarketplaceProfile profile =
                MarketplaceProfile.Create(marketplace, identity).Value!;
            return ProfileJsonDeserializationResult<MarketplaceProfile>.Success(profile);
        }
    }
}
