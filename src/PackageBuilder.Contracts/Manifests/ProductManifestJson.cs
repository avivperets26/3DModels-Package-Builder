using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Json.Schema;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Materials;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Textures;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Contracts.Manifests;

/// <summary>Strict, deterministic, offline JSON contract for product-manifest schema version 1.</summary>
public static class ProductManifestJson
{
    public const int MaximumInputCharacters = 1_048_576;
    public const int MaximumDepth = 64;
    public const string SchemaIdentifier =
        "https://schemas.packagebuilder.dev/product-manifest/v1";
    public const string SchemaDraft = "https://json-schema.org/draft/2020-12/schema";

    private static readonly Lazy<string> _schemaText = new(ReadSchemaText);
    private static readonly Lazy<JsonSchema> _schema = new(
        () => JsonSchema.FromText(
            _schemaText.Value,
            CreateBuildOptions()));

    public static string SchemaText => _schemaText.Value;

    public static ProductManifestSchemaValidationResult ValidateSchemaDefinition() =>
        ValidateSchemaDefinition(SchemaText);

    public static ProductManifestSchemaValidationResult ValidateSchemaDefinition(
        string? schemaText)
    {
        if (schemaText is null)
        {
            return new ProductManifestSchemaValidationResult(false, "Schema JSON is null.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                schemaText,
                new JsonDocumentOptions { MaxDepth = MaximumDepth });
            if (!document.RootElement.TryGetProperty("$id", out JsonElement id))
            {
                return new ProductManifestSchemaValidationResult(
                    false,
                    "The schema identifier is missing.");
            }

            if (!document.RootElement.TryGetProperty("$schema", out JsonElement draft))
            {
                return new ProductManifestSchemaValidationResult(
                    false,
                    "The schema dialect is missing.");
            }

            if (!string.Equals(
                    id.GetString(),
                    SchemaIdentifier,
                    StringComparison.Ordinal))
            {
                return new ProductManifestSchemaValidationResult(
                    false,
                    "The schema identifier is not approved.");
            }

            if (!string.Equals(
                    draft.GetString(),
                    SchemaDraft,
                    StringComparison.Ordinal))
            {
                return new ProductManifestSchemaValidationResult(
                    false,
                    "The schema dialect is not approved.");
            }

            _ = JsonSchema.FromText(
                schemaText,
                CreateBuildOptions());
            return new ProductManifestSchemaValidationResult(true, null);
        }
        catch (JsonException exception)
        {
            return new ProductManifestSchemaValidationResult(false, exception.Message);
        }
    }

    public static ProductManifestSchemaValidationResult ValidateJsonAgainstSchema(string? json)
    {
        if (json is null)
        {
            return new ProductManifestSchemaValidationResult(false, "JSON is null.");
        }

        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { MaxDepth = MaximumDepth });
            EvaluationResults results = _schema.Value.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            return new ProductManifestSchemaValidationResult(
                results.IsValid,
                results.IsValid ? null : JsonSerializer.Serialize(results));
        }
        catch (JsonException exception)
        {
            return new ProductManifestSchemaValidationResult(false, exception.Message);
        }
    }

    public static ProductManifestSerializationResult Serialize(ProductManifest? manifest)
    {
        if (manifest is null)
        {
            return ProductManifestSerializationResult.Failure(
                ProductManifestJsonError.NullManifest);
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString(
                "publisherProfileReference",
                manifest.PublisherProfileReference.Value);
            WriteProduct(writer, manifest);
            writer.WritePropertyName("targets");
            writer.WriteStartArray();
            foreach (BuildTarget target in manifest.Targets)
            {
                writer.WriteStringValue(target.CanonicalIdentifier);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("sourceAssets");
            writer.WriteStartArray();
            foreach (SourceAsset source in manifest.SourceAssets)
            {
                WriteSource(writer, source);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("materials");
            writer.WriteStartArray();
            foreach (ManifestMaterial material in manifest.Materials)
            {
                WriteMaterial(writer, material);
            }

            writer.WriteEndArray();
            if (manifest.Rig is not null)
            {
                writer.WritePropertyName("rig");
                WriteRig(writer, manifest.Rig);
            }

            writer.WritePropertyName("animations");
            writer.WriteStartArray();
            foreach (AnimationDefinition animation in manifest.Animations)
            {
                WriteAnimation(writer, animation);
            }

            writer.WriteEndArray();
            if (manifest.ItemSet is not null)
            {
                writer.WritePropertyName("itemSet");
                WriteItemGroup(writer, manifest.ItemSet);
            }

            if (manifest.ItemCollection is not null)
            {
                writer.WritePropertyName("itemCollection");
                WriteItemGroup(writer, manifest.ItemCollection);
            }

            if (manifest.MarketplaceProfileReference is not null)
            {
                writer.WritePropertyName("marketplaceProfileReference");
                writer.WriteStartObject();
                writer.WriteString(
                    "marketplace",
                    manifest.MarketplaceProfileReference.Marketplace.Value);
                writer.WriteString("profile", manifest.MarketplaceProfileReference.Identity.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return ProductManifestSerializationResult.Success(
            Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    public static ProductManifestDeserializationResult Deserialize(string? json)
    {
        if (json is null)
        {
            return Failure(ProductManifestJsonError.NullJson);
        }

        if (json.Length == 0)
        {
            return Failure(ProductManifestJsonError.EmptyJson);
        }

        if (json.Length > MaximumInputCharacters)
        {
            return Failure(ProductManifestJsonError.InputTooLarge);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumDepth,
                });
        }
        catch (JsonException)
        {
            return Failure(ProductManifestJsonError.MalformedJson);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(ProductManifestJsonError.RootMustBeObject);
            }

            if (ContainsDuplicateProperty(document.RootElement))
            {
                return Failure(ProductManifestJsonError.DuplicateProperty);
            }

            EvaluationResults schemaResult = _schema.Value.Evaluate(
                document.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.Flag });
            if (!schemaResult.IsValid)
            {
                return Failure(ProductManifestJsonError.SchemaViolation);
            }

            var context = new ParseContext();
            ProductManifest? manifest = ParseManifest(document.RootElement, context);
            return context.IsInvalid || manifest is null
                ? Failure(ProductManifestJsonError.DomainViolation, context.Findings)
                : ProductManifestDeserializationResult.Success(manifest);
        }
    }

    private static ProductManifest? ParseManifest(JsonElement root, ParseContext context)
    {
        JsonElement product = root.GetProperty("product");
        PublisherRoot? publisher = Value(PublisherRoot.Create(S(root, "publisherProfileReference")), context);
        ProductDisplayName? displayName = Value(ProductDisplayName.Create(S(product, "displayName")), context);
        InternalAssetId? assetId = Value(InternalAssetId.Create(S(product, "assetId")), context);
        ProductFolderName? folderName = Value(ProductFolderName.Create(S(product, "folderName")), context);
        ProductCase? productCase = Value(ProductCase.TryParse(S(product, "case")), context);
        ProductVersion? version = Value(ProductVersion.Create(S(product, "version")), context);
        BuildTarget?[] targets =
        [
            .. root.GetProperty("targets").EnumerateArray()
                .Select(element => Value(BuildTarget.TryParse(element.GetString()), context)),
        ];
        SourceAsset?[] sources =
        [
            .. root.GetProperty("sourceAssets").EnumerateArray()
                .Select(element => ParseSource(element, context)),
        ];
        ManifestMaterial?[] materials =
        [
            .. root.GetProperty("materials").EnumerateArray()
                .Select(element => ParseMaterial(element, context)),
        ];
        RigDefinition? rig = root.TryGetProperty("rig", out JsonElement rigElement)
            ? ParseRig(rigElement, context)
            : null;
        AnimationDefinition?[] animations =
        [
            .. root.GetProperty("animations").EnumerateArray()
                .Select(element => ParseAnimation(element, rig, context)),
        ];
        ItemSetDefinition? itemSet = root.TryGetProperty("itemSet", out JsonElement setElement)
            ? ParseItemSet(setElement, context)
            : null;
        ItemCollectionDefinition? itemCollection =
            root.TryGetProperty("itemCollection", out JsonElement collectionElement)
                ? ParseItemCollection(collectionElement, context)
                : null;
        MarketplaceProfile? marketplace =
            root.TryGetProperty("marketplaceProfileReference", out JsonElement marketElement)
                ? ParseMarketplace(marketElement, context)
                : null;
        if (context.IsInvalid)
        {
            return null;
        }

        ProductManifestValidationResult result = ProductManifest.Create(
            root.GetProperty("schemaVersion").GetInt32(),
            publisher,
            displayName,
            assetId,
            folderName,
            productCase,
            version,
            targets,
            sources,
            materials,
            rig,
            animations,
            itemSet,
            itemCollection,
            marketplace);
        if (!result.IsValid)
        {
            context.Add(result.Findings);
        }

        return result.Value;
    }

    private static SourceAsset? ParseSource(JsonElement element, ParseContext context)
    {
        SourceAssetKind? kind = Value(SourceAssetKind.TryParse(S(element, "kind")), context);
        SourceAssetValidationResult result = SourceAsset.Create(
            kind,
            S(element, "logicalReference"),
            element.TryGetProperty("originalFileName", out JsonElement original)
                ? original.GetString()
                : null);
        return Value(result, context);
    }

    private static ManifestMaterial? ParseMaterial(JsonElement element, ParseContext context)
    {
        InternalAssetId? id = Value(InternalAssetId.Create(S(element, "id")), context);
        JsonElement emissionElement = element.GetProperty("emission");
        EmissionProperties? emission = Value(
            EmissionProperties.Create(
                D(emissionElement, "red"),
                D(emissionElement, "green"),
                D(emissionElement, "blue"),
                D(emissionElement, "intensity")),
            context);
        JsonElement uvElement = element.GetProperty("uvTransform");
        UvTransform? uv = Value(
            UvTransform.Create(
                D(uvElement, "scaleU"),
                D(uvElement, "scaleV"),
                D(uvElement, "offsetU"),
                D(uvElement, "offsetV")),
            context);
        SurfaceMode? surface = Value(SurfaceMode.TryParse(S(element, "surfaceMode")), context);
        TextureAssignment?[] assignments =
        [
            .. element.GetProperty("textureAssignments").EnumerateArray()
                .Select(value => ParseTexture(value, context)),
        ];
        MaterialDefinitionValidationResult definition = MaterialDefinition.Create(
            D(element, "metallicFactor"),
            D(element, "roughnessFactor"),
            D(element, "normalScale"),
            emission,
            D(element, "ambientOcclusionStrength"),
            D(element, "heightScale"),
            D(element, "opacity"),
            surface,
            element.TryGetProperty("alphaCutoff", out JsonElement cutoff)
                ? cutoff.GetDouble()
                : null,
            uv,
            element.GetProperty("doubleSided").GetBoolean(),
            assignments);
        MaterialDefinition? material = Value(definition, context);
        return Value(ManifestMaterial.Create(id, material), context);
    }

    private static TextureAssignment? ParseTexture(JsonElement element, ParseContext context)
    {
        SourceAsset? source = ParseSource(element.GetProperty("source"), context);
        TextureRole? role = Value(TextureRole.TryParse(S(element, "role")), context);
        ColourSpace? colour = Value(ColourSpace.TryParse(S(element, "colourSpace")), context);
        NormalConvention? convention =
            element.TryGetProperty("normalConvention", out JsonElement conventionElement)
                ? Value(NormalConvention.TryParse(conventionElement.GetString()), context)
                : null;
        return Value(TextureAssignment.Create(source, role, colour, convention), context);
    }

    private static RigDefinition? ParseRig(JsonElement element, ParseContext context)
    {
        RigType? type = Value(RigType.TryParse(S(element, "type")), context);
        BoneDefinition?[] bones =
        [
            .. element.GetProperty("bones").EnumerateArray().Select(
                value => Value(
                    BoneDefinition.Create(
                        S(value, "identity"),
                        value.TryGetProperty("parentIdentity", out JsonElement parent)
                            ? parent.GetString()
                            : null),
                    context)),
        ];
        SkeletonDefinition? skeleton = Value(SkeletonDefinition.Create(bones), context);
        BonePose?[] poses =
        [
            .. element.GetProperty("referencePose").EnumerateArray().Select(
                value => Value(
                    BonePose.Create(
                        S(value, "boneIdentity"),
                        ParseTransform(value.GetProperty("transform"), context)),
                    context)),
        ];
        PoseDefinition? pose = Value(PoseDefinition.Create(skeleton, poses), context);
        return Value(RigDefinition.Create(type, skeleton, pose), context);
    }

    private static RigTransform? ParseTransform(JsonElement element, ParseContext context) =>
        Value(
            RigTransform.Create(
                D(element, "translationX"),
                D(element, "translationY"),
                D(element, "translationZ"),
                D(element, "rotationX"),
                D(element, "rotationY"),
                D(element, "rotationZ"),
                D(element, "rotationW"),
                D(element, "scaleX"),
                D(element, "scaleY"),
                D(element, "scaleZ")),
            context);

    private static AnimationDefinition? ParseAnimation(
        JsonElement element,
        RigDefinition? rig,
        ParseContext context) =>
        Value(
            AnimationDefinition.Create(
                S(element, "name"),
                element.GetProperty("startFrame").GetInt64(),
                element.GetProperty("endFrame").GetInt64(),
                D(element, "framesPerSecond"),
                Value(LoopBehavior.TryParse(S(element, "loopBehavior")), context),
                Value(RootMotionStatus.TryParse(S(element, "rootMotionStatus")), context),
                element.TryGetProperty("rootMotionBoneIdentity", out JsonElement root)
                    ? root.GetString()
                    : null,
                rig),
            context);

    private static ItemSetDefinition? ParseItemSet(JsonElement element, ParseContext context)
    {
        ItemData data = ParseItemData(element, context);
        AssembledSetRules? rules =
            element.TryGetProperty("assembledSetRules", out JsonElement rulesElement)
                ? ParseAssembledRules(rulesElement, context)
                : null;
        return Value(
            ItemSetDefinition.Create(data.Items, data.Relationships, data.SharedAssets, rules),
            context);
    }

    private static ItemCollectionDefinition? ParseItemCollection(
        JsonElement element,
        ParseContext context)
    {
        ItemData data = ParseItemData(element, context);
        return Value(
            ItemCollectionDefinition.Create(data.Items, data.Relationships, data.SharedAssets),
            context);
    }

    private static ItemData ParseItemData(JsonElement element, ParseContext context)
    {
        ItemDefinition?[] items =
        [
            .. element.GetProperty("items").EnumerateArray().Select(
                value =>
                {
                    InternalAssetId? id = Value(InternalAssetId.Create(S(value, "id")), context);
                    ItemCategory?[] categories =
                    [
                        .. value.GetProperty("categories").EnumerateArray().Select(
                            category => Value(ItemCategory.Create(category.GetString()), context)),
                    ];
                    AttachmentSlot? slot =
                        value.TryGetProperty("attachmentSlot", out JsonElement slotElement)
                            ? Value(AttachmentSlot.Create(slotElement.GetString()), context)
                            : null;
                    InternalAssetId?[] references =
                    [
                        .. value.GetProperty("sharedAssetReferences").EnumerateArray().Select(
                            reference => Value(
                                InternalAssetId.Create(reference.GetString()),
                                context)),
                    ];
                    return Value(ItemDefinition.Create(id, categories, slot, references), context);
                }),
        ];
        ItemRelationship?[] relationships =
        [
            .. element.GetProperty("relationships").EnumerateArray().Select(
                value => Value(
                    ItemRelationship.Create(
                        Value(InternalAssetId.Create(S(value, "firstItemId")), context),
                        Value(InternalAssetId.Create(S(value, "secondItemId")), context)),
                    context)),
        ];
        SharedAssetDefinition?[] shared =
        [
            .. element.GetProperty("sharedAssets").EnumerateArray().Select(
                value => Value(
                    SharedAssetDefinition.Create(
                        Value(InternalAssetId.Create(S(value, "id")), context),
                        ParseSource(value.GetProperty("source"), context)),
                    context)),
        ];
        return new ItemData(items, relationships, shared);
    }

    private static AssembledSetRules? ParseAssembledRules(
        JsonElement element,
        ParseContext context)
    {
        AssembledSetMember?[] members =
        [
            .. element.GetProperty("members").EnumerateArray().Select(
                value => Value(
                    AssembledSetMember.Create(
                        Value(InternalAssetId.Create(S(value, "itemId")), context),
                        value.TryGetProperty("attachmentSlot", out JsonElement slot)
                            ? Value(AttachmentSlot.Create(slot.GetString()), context)
                            : null),
                    context)),
        ];
        CompatibilityMetadataEntry?[] metadata =
        [
            .. element.GetProperty("compatibilityMetadata").EnumerateArray().Select(
                value => Value(
                    CompatibilityMetadataEntry.Create(
                        Value(InternalAssetId.Create(S(value, "key")), context),
                        S(value, "value")),
                    context)),
        ];
        return Value(
            AssembledSetRules.Create(
                members,
                element.GetProperty("requireUniqueAttachmentSlots").GetBoolean(),
                metadata),
            context);
    }

    private static MarketplaceProfile? ParseMarketplace(
        JsonElement element,
        ParseContext context) =>
        Value(
            MarketplaceProfile.Create(
                Value(MarketplaceIdentifier.Create(S(element, "marketplace")), context),
                Value(MarketplaceProfileIdentifier.Create(S(element, "profile")), context)),
            context);

    private static void WriteProduct(Utf8JsonWriter writer, ProductManifest manifest)
    {
        writer.WritePropertyName("product");
        writer.WriteStartObject();
        writer.WriteString("displayName", manifest.DisplayName.Value);
        writer.WriteString("assetId", manifest.AssetId.Value);
        writer.WriteString("folderName", manifest.FolderName.Value);
        writer.WriteString("case", manifest.ProductCase.CanonicalIdentifier);
        writer.WriteString("version", manifest.Version.Value);
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

    private static void WriteMaterial(Utf8JsonWriter writer, ManifestMaterial material)
    {
        MaterialDefinition value = material.Definition;
        writer.WriteStartObject();
        writer.WriteString("id", material.Id.Value);
        writer.WriteNumber("metallicFactor", value.MetallicFactor);
        writer.WriteNumber("roughnessFactor", value.RoughnessFactor);
        writer.WriteNumber("normalScale", value.NormalScale);
        writer.WritePropertyName("emission");
        writer.WriteStartObject();
        writer.WriteNumber("red", value.Emission.Red);
        writer.WriteNumber("green", value.Emission.Green);
        writer.WriteNumber("blue", value.Emission.Blue);
        writer.WriteNumber("intensity", value.Emission.Intensity);
        writer.WriteEndObject();
        writer.WriteNumber("ambientOcclusionStrength", value.AmbientOcclusionStrength);
        writer.WriteNumber("heightScale", value.HeightScale);
        writer.WriteNumber("opacity", value.Opacity);
        writer.WriteString("surfaceMode", value.SurfaceMode.CanonicalIdentifier);
        if (value.AlphaCutoff.HasValue)
        {
            writer.WriteNumber("alphaCutoff", value.AlphaCutoff.Value);
        }

        writer.WritePropertyName("uvTransform");
        writer.WriteStartObject();
        writer.WriteNumber("scaleU", value.UvTransform.ScaleU);
        writer.WriteNumber("scaleV", value.UvTransform.ScaleV);
        writer.WriteNumber("offsetU", value.UvTransform.OffsetU);
        writer.WriteNumber("offsetV", value.UvTransform.OffsetV);
        writer.WriteEndObject();
        writer.WriteBoolean("doubleSided", value.IsDoubleSided);
        writer.WritePropertyName("textureAssignments");
        writer.WriteStartArray();
        foreach (TextureAssignment assignment in value.TextureAssignments)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("source");
            WriteSource(writer, assignment.SourceAsset);
            writer.WriteString("role", assignment.Role.CanonicalIdentifier);
            writer.WriteString("colourSpace", assignment.ColourSpace.CanonicalIdentifier);
            if (assignment.NormalConvention is not null)
            {
                writer.WriteString(
                    "normalConvention",
                    assignment.NormalConvention.CanonicalIdentifier);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRig(Utf8JsonWriter writer, RigDefinition rig)
    {
        writer.WriteStartObject();
        writer.WriteString("type", rig.RigType.CanonicalIdentifier);
        writer.WritePropertyName("bones");
        writer.WriteStartArray();
        foreach (BoneDefinition bone in rig.Skeleton.Bones)
        {
            writer.WriteStartObject();
            writer.WriteString("identity", bone.Identity);
            if (bone.ParentIdentity is not null)
            {
                writer.WriteString("parentIdentity", bone.ParentIdentity);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("referencePose");
        writer.WriteStartArray();
        foreach (BonePose pose in rig.ReferencePose.Bones)
        {
            writer.WriteStartObject();
            writer.WriteString("boneIdentity", pose.BoneIdentity);
            writer.WritePropertyName("transform");
            WriteTransform(writer, pose.Transform);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteTransform(Utf8JsonWriter writer, RigTransform value)
    {
        writer.WriteStartObject();
        writer.WriteNumber("translationX", value.TranslationX);
        writer.WriteNumber("translationY", value.TranslationY);
        writer.WriteNumber("translationZ", value.TranslationZ);
        writer.WriteNumber("rotationX", value.RotationX);
        writer.WriteNumber("rotationY", value.RotationY);
        writer.WriteNumber("rotationZ", value.RotationZ);
        writer.WriteNumber("rotationW", value.RotationW);
        writer.WriteNumber("scaleX", value.ScaleX);
        writer.WriteNumber("scaleY", value.ScaleY);
        writer.WriteNumber("scaleZ", value.ScaleZ);
        writer.WriteEndObject();
    }

    private static void WriteAnimation(Utf8JsonWriter writer, AnimationDefinition value)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteNumber("startFrame", value.StartFrame);
        writer.WriteNumber("endFrame", value.EndFrame);
        writer.WriteNumber("framesPerSecond", value.FramesPerSecond);
        writer.WriteString("loopBehavior", value.LoopBehavior.CanonicalIdentifier);
        writer.WriteString("rootMotionStatus", value.RootMotionStatus.CanonicalIdentifier);
        if (value.RootMotionBoneIdentity is not null)
        {
            writer.WriteString("rootMotionBoneIdentity", value.RootMotionBoneIdentity);
        }

        writer.WriteEndObject();
    }

    private static void WriteItemGroup(
        Utf8JsonWriter writer,
        ItemSetDefinition group) =>
        WriteItemGroup(
            writer,
            group.Items,
            group.Relationships,
            group.SharedAssets,
            group.AssembledSetRules);

    private static void WriteItemGroup(
        Utf8JsonWriter writer,
        ItemCollectionDefinition group) =>
        WriteItemGroup(
            writer,
            group.Items,
            group.Relationships,
            group.SharedAssets,
            null);

    private static void WriteItemGroup(
        Utf8JsonWriter writer,
        IReadOnlyList<ItemDefinition> items,
        IReadOnlyList<ItemRelationship> relationships,
        IReadOnlyList<SharedAssetDefinition> shared,
        AssembledSetRules? assembled)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("items");
        writer.WriteStartArray();
        foreach (ItemDefinition item in items)
        {
            writer.WriteStartObject();
            writer.WriteString("id", item.Id.Value);
            writer.WritePropertyName("categories");
            writer.WriteStartArray();
            foreach (ItemCategory category in item.Categories)
            {
                writer.WriteStringValue(category.CanonicalIdentifier);
            }

            writer.WriteEndArray();
            if (item.AttachmentSlot is not null)
            {
                writer.WriteString("attachmentSlot", item.AttachmentSlot.CanonicalIdentifier);
            }

            writer.WritePropertyName("sharedAssetReferences");
            writer.WriteStartArray();
            foreach (InternalAssetId reference in item.SharedAssetReferences)
            {
                writer.WriteStringValue(reference.Value);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("relationships");
        writer.WriteStartArray();
        foreach (ItemRelationship relationship in relationships)
        {
            writer.WriteStartObject();
            writer.WriteString("firstItemId", relationship.FirstItemId.Value);
            writer.WriteString("secondItemId", relationship.SecondItemId.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("sharedAssets");
        writer.WriteStartArray();
        foreach (SharedAssetDefinition asset in shared)
        {
            writer.WriteStartObject();
            writer.WriteString("id", asset.Id.Value);
            writer.WritePropertyName("source");
            WriteSource(writer, asset.Source);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (assembled is not null)
        {
            writer.WritePropertyName("assembledSetRules");
            writer.WriteStartObject();
            writer.WritePropertyName("members");
            writer.WriteStartArray();
            foreach (AssembledSetMember member in assembled.Members)
            {
                writer.WriteStartObject();
                writer.WriteString("itemId", member.ItemId.Value);
                if (member.AttachmentSlot is not null)
                {
                    writer.WriteString(
                        "attachmentSlot",
                        member.AttachmentSlot.CanonicalIdentifier);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean(
                "requireUniqueAttachmentSlots",
                assembled.RequireUniqueAttachmentSlots);
            writer.WritePropertyName("compatibilityMetadata");
            writer.WriteStartArray();
            foreach (CompatibilityMetadataEntry entry in assembled.CompatibilityMetadata)
            {
                writer.WriteStartObject();
                writer.WriteString("key", entry.Key.Value);
                writer.WriteString("value", entry.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static bool ContainsDuplicateProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || ContainsDuplicateProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (ContainsDuplicateProperty(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string ReadSchemaText()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "PackageBuilder.Contracts.Schemas.product-manifest.schema.json")!;
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static BuildOptions CreateBuildOptions() =>
        new()
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry(),
        };

    private static string S(JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    private static double D(JsonElement element, string property) =>
        element.GetProperty(property).GetDouble();

    private static T? Value<T>(
        NamingValidationResult<T> result,
        ParseContext context)
        where T : class
        => Value(result.IsValid, result.Value, context);

    private static T? Value<T>(
        CanonicalIdentifierParseResult<T> result,
        ParseContext context)
        where T : class
        => Value(result.IsValid, result.Value, context);

    private static T? Value<T>(
        ItemValidationResult<T> result,
        ParseContext context)
        where T : class
        => Value(result.IsValid, result.Value, context);

    private static T? Value<T>(
        ProfileValidationResult<T> result,
        ParseContext context)
        where T : class
        => Value(result.IsValid, result.Value, context);

    private static SourceAsset? Value(
        SourceAssetValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static AnimationDefinition? Value(
        AnimationDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static EmissionProperties? Value(
        EmissionPropertiesValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static UvTransform? Value(
        UvTransformValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static MaterialDefinition? Value(
        MaterialDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static BonePose? Value(
        BonePoseValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static BoneDefinition? Value(
        BoneDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static PoseDefinition? Value(
        PoseDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static RigDefinition? Value(
        RigDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static RigTransform? Value(
        RigTransformValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static SkeletonDefinition? Value(
        SkeletonDefinitionValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static TextureAssignment? Value(
        TextureAssignmentValidationResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static ManifestMaterial? Value(
        ManifestMaterialResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static ProductVersion? Value(
        ProductVersionResult result,
        ParseContext context) =>
        Value(result.IsValid, result.Value, context);

    private static T? Value<T>(
        bool isValid,
        T? value,
        ParseContext context)
        where T : class
    {
        if (!isValid)
        {
            context.InvalidValue();
            return null;
        }

        return value;
    }

    private static ProductManifestDeserializationResult Failure(
        ProductManifestJsonError error,
        IReadOnlyList<ValidationFinding>? findings = null) =>
        ProductManifestDeserializationResult.Failure(error, findings);

    private sealed class ParseContext
    {
        private readonly List<ValidationFinding> _findings = [];

        public bool IsInvalid => _findings.Count != 0;

        public IReadOnlyList<ValidationFinding> Findings => _findings;

        public void InvalidValue()
        {
            if (_findings.Count != 0)
            {
                return;
            }

            FindingCode code = FindingCode.Create("MANIFEST_VALUE_INVALID").Value!;
            FindingExplanation explanation =
                FindingExplanation.Create("A manifest value violates its typed domain contract.").Value!;
            FindingSourceComponent source =
                FindingSourceComponent.Create("manifest-contract").Value!;
            CorrectiveAction action =
                CorrectiveAction.Create("Correct the invalid value and validate the manifest again.").Value!;
            _findings.Add(
                ValidationFinding.Create(
                    code,
                    FindingSeverity.Error,
                    explanation,
                    source,
                    null,
                    action,
                    true).Value!);
        }

        public void Add(IEnumerable<ValidationFinding> findings) =>
            _findings.AddRange(findings);
    }

    private sealed record ItemData(
        ItemDefinition?[] Items,
        ItemRelationship?[] Relationships,
        SharedAssetDefinition?[] SharedAssets);
}
