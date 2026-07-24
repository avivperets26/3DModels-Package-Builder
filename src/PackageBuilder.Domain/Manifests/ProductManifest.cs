using System.Collections.ObjectModel;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Assets;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Products;
using PackageBuilder.Domain.Profiles;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Targets;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Domain.Manifests;

/// <summary>Immutable schema-version-1 product manifest intent.</summary>
public sealed class ProductManifest
{
    public const int CurrentSchemaVersion = 1;

    private ProductManifest(
        PublisherRoot publisherProfileReference,
        ProductDisplayName displayName,
        InternalAssetId assetId,
        ProductFolderName folderName,
        ProductCase productCase,
        ProductVersion version,
        ReadOnlyCollection<BuildTarget> targets,
        ReadOnlyCollection<SourceAsset> sourceAssets,
        ReadOnlyCollection<ManifestMaterial> materials,
        RigDefinition? rig,
        ReadOnlyCollection<AnimationDefinition> animations,
        ItemSetDefinition? itemSet,
        ItemCollectionDefinition? itemCollection,
        MarketplaceProfile? marketplaceProfileReference)
    {
        SchemaVersion = CurrentSchemaVersion;
        PublisherProfileReference = publisherProfileReference;
        DisplayName = displayName;
        AssetId = assetId;
        FolderName = folderName;
        ProductCase = productCase;
        Version = version;
        Targets = targets;
        SourceAssets = sourceAssets;
        Materials = materials;
        Rig = rig;
        Animations = animations;
        ItemSet = itemSet;
        ItemCollection = itemCollection;
        MarketplaceProfileReference = marketplaceProfileReference;
    }

    public int SchemaVersion { get; }

    public PublisherRoot PublisherProfileReference { get; }

    public ProductDisplayName DisplayName { get; }

    public InternalAssetId AssetId { get; }

    public ProductFolderName FolderName { get; }

    public ProductCase ProductCase { get; }

    public ProductVersion Version { get; }

    public IReadOnlyList<BuildTarget> Targets { get; }

    public IReadOnlyList<SourceAsset> SourceAssets { get; }

    public IReadOnlyList<ManifestMaterial> Materials { get; }

    public RigDefinition? Rig { get; }

    public IReadOnlyList<AnimationDefinition> Animations { get; }

    public ItemSetDefinition? ItemSet { get; }

    public ItemCollectionDefinition? ItemCollection { get; }

    public MarketplaceProfile? MarketplaceProfileReference { get; }

    public static ProductManifestValidationResult Create(
        int schemaVersion,
        PublisherRoot? publisherProfileReference,
        ProductDisplayName? displayName,
        InternalAssetId? assetId,
        ProductFolderName? folderName,
        ProductCase? productCase,
        ProductVersion? version,
        IEnumerable<BuildTarget?>? targets,
        IEnumerable<SourceAsset?>? sourceAssets,
        IEnumerable<ManifestMaterial?>? materials,
        RigDefinition? rig,
        IEnumerable<AnimationDefinition?>? animations,
        ItemSetDefinition? itemSet,
        ItemCollectionDefinition? itemCollection,
        MarketplaceProfile? marketplaceProfileReference = null)
    {
        var findings = new List<ValidationFinding>();
        AddRequiredFindings(
            findings,
            schemaVersion,
            publisherProfileReference,
            displayName,
            assetId,
            folderName,
            productCase,
            version,
            targets,
            sourceAssets,
            materials,
            animations);
        if (findings.Count != 0)
        {
            return ProductManifestValidationResult.Failure(findings);
        }

        List<BuildTarget> targetValues = [.. targets!.Cast<BuildTarget>()];
        List<SourceAsset> sourceValues = [.. sourceAssets!.Cast<SourceAsset>()];
        List<ManifestMaterial> materialValues = [.. materials!.Cast<ManifestMaterial>()];
        List<AnimationDefinition> animationValues = [.. animations!.Cast<AnimationDefinition>()];

        AddCollectionFindings(
            findings,
            targetValues,
            sourceValues,
            materialValues,
            animationValues);
        AddReferenceFindings(findings, sourceValues, materialValues, itemSet, itemCollection);
        AddCaseFindings(
            findings,
            productCase!,
            rig,
            animationValues,
            itemSet,
            itemCollection);
        if (findings.Count != 0)
        {
            return ProductManifestValidationResult.Failure(findings);
        }

        targetValues.Sort(
            (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.CanonicalIdentifier,
                    right.CanonicalIdentifier));
        sourceValues.Sort(
            (left, right) =>
                StringComparer.Ordinal.Compare(left.LogicalReference, right.LogicalReference));
        materialValues.Sort(
            (left, right) => StringComparer.Ordinal.Compare(left.Id.Value, right.Id.Value));
        animationValues.Sort(
            (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));

        return ProductManifestValidationResult.Success(
            new ProductManifest(
                publisherProfileReference!,
                displayName!,
                assetId!,
                folderName!,
                productCase!,
                version!,
                targetValues.AsReadOnly(),
                sourceValues.AsReadOnly(),
                materialValues.AsReadOnly(),
                rig,
                animationValues.AsReadOnly(),
                itemSet,
                itemCollection,
                marketplaceProfileReference));
    }

    private static void AddRequiredFindings(
        List<ValidationFinding> findings,
        int schemaVersion,
        PublisherRoot? publisher,
        ProductDisplayName? displayName,
        InternalAssetId? assetId,
        ProductFolderName? folderName,
        ProductCase? productCase,
        ProductVersion? version,
        IEnumerable<BuildTarget?>? targets,
        IEnumerable<SourceAsset?>? sources,
        IEnumerable<ManifestMaterial?>? materials,
        IEnumerable<AnimationDefinition?>? animations)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            findings.Add(Finding("MANIFEST_SCHEMA_VERSION_UNKNOWN", "Schema version 1 is required."));
        }

        if (publisher is null || displayName is null || assetId is null || folderName is null ||
            productCase is null || version is null)
        {
            findings.Add(Finding("MANIFEST_REQUIRED_VALUE_MISSING", "A required manifest value is missing."));
        }

        if (targets is null || sources is null || materials is null || animations is null)
        {
            findings.Add(Finding("MANIFEST_REQUIRED_COLLECTION_MISSING", "A required manifest collection is missing."));
            return;
        }

        if (targets.Any(value => value is null) ||
            sources.Any(value => value is null) ||
            materials.Any(value => value is null) ||
            animations.Any(value => value is null))
        {
            findings.Add(Finding("MANIFEST_NULL_COLLECTION_ENTRY", "Manifest collections cannot contain null."));
        }
    }

    private static void AddCollectionFindings(
        List<ValidationFinding> findings,
        List<BuildTarget> targets,
        IReadOnlyCollection<SourceAsset> sources,
        IReadOnlyCollection<ManifestMaterial> materials,
        IReadOnlyCollection<AnimationDefinition> animations)
    {
        if (targets.Count == 0)
        {
            findings.Add(Finding("MANIFEST_TARGET_REQUIRED", "At least one build target is required."));
        }

        AddDuplicate(findings, targets.Select(value => value.CanonicalIdentifier), "MANIFEST_TARGET_DUPLICATE");
        AddDuplicate(findings, sources.Select(value => value.LogicalReference), "MANIFEST_SOURCE_DUPLICATE");
        AddDuplicate(findings, materials.Select(value => value.Id.Value), "MANIFEST_MATERIAL_DUPLICATE");
        AddDuplicate(findings, animations.Select(value => value.Name), "MANIFEST_ANIMATION_DUPLICATE");
        if (!sources.Any(
                source =>
                    source.Kind.Equals(SourceAssetKind.Fbx) ||
                    source.Kind.Equals(SourceAssetKind.Glb) ||
                    source.Kind.Equals(SourceAssetKind.Archive)))
        {
            findings.Add(Finding("MANIFEST_MODEL_SOURCE_REQUIRED", "At least one FBX, GLB, or archive source is required."));
        }
    }

    private static void AddReferenceFindings(
        List<ValidationFinding> findings,
        IReadOnlyCollection<SourceAsset> sources,
        IReadOnlyCollection<ManifestMaterial> materials,
        ItemSetDefinition? itemSet,
        ItemCollectionDefinition? itemCollection)
    {
        var declared = new HashSet<SourceAsset>(sources);
        if (materials.SelectMany(value => value.Definition.TextureAssignments)
            .Any(assignment => !declared.Contains(assignment.SourceAsset)))
        {
            findings.Add(Finding("MANIFEST_SOURCE_REFERENCE_UNKNOWN", "A material references an undeclared source asset."));
        }

        IEnumerable<SharedAssetDefinition> shared =
            (itemSet?.SharedAssets ?? []).Concat(itemCollection?.SharedAssets ?? []);
        if (shared.Any(value => !declared.Contains(value.Source)))
        {
            findings.Add(Finding("MANIFEST_SOURCE_REFERENCE_UNKNOWN", "An item group references an undeclared source asset."));
        }
    }

    private static void AddCaseFindings(
        List<ValidationFinding> findings,
        ProductCase productCase,
        RigDefinition? rig,
        List<AnimationDefinition> animations,
        ItemSetDefinition? itemSet,
        ItemCollectionDefinition? itemCollection)
    {
        bool hasAnimations = animations.Count != 0;
        if (productCase.Equals(ProductCase.Static))
        {
            AddForbidden(findings, rig is not null || hasAnimations || itemSet is not null || itemCollection is not null);
        }
        else if (productCase.Equals(ProductCase.Rigged))
        {
            if (rig is null)
            {
                findings.Add(Finding("MANIFEST_RIG_REQUIRED", "The rigged case requires a rig."));
            }

            AddForbidden(findings, hasAnimations || itemSet is not null || itemCollection is not null);
        }
        else if (productCase.Equals(ProductCase.RiggedAnimated))
        {
            if (rig is null)
            {
                findings.Add(Finding("MANIFEST_RIG_REQUIRED", "The rigged-animated case requires a rig."));
            }

            if (!hasAnimations)
            {
                findings.Add(Finding("MANIFEST_ANIMATION_REQUIRED", "The rigged-animated case requires an animation clip."));
            }

            if (rig is not null && animations.Any(value => !value.Rig.Equals(rig)))
            {
                findings.Add(Finding("MANIFEST_ANIMATION_RIG_MISMATCH", "Every animation must use the declared rig."));
            }

            AddForbidden(findings, itemSet is not null || itemCollection is not null);
        }
        else if (productCase.Equals(ProductCase.ItemSet))
        {
            if (itemSet is null)
            {
                findings.Add(Finding("MANIFEST_ITEM_SET_REQUIRED", "The item-set case requires an item-set definition."));
            }

            AddForbidden(findings, itemCollection is not null || rig is not null || hasAnimations);
        }
        else
        {
            if (itemCollection is null)
            {
                findings.Add(Finding("MANIFEST_ITEM_COLLECTION_REQUIRED", "The item-collection case requires an item-collection definition."));
            }

            AddForbidden(findings, itemSet is not null || rig is not null || hasAnimations);
        }
    }

    private static void AddForbidden(List<ValidationFinding> findings, bool condition)
    {
        if (condition)
        {
            findings.Add(Finding("MANIFEST_CASE_SECTION_CONTRADICTORY", "A section contradicts the selected product case."));
        }
    }

    private static void AddDuplicate(
        List<ValidationFinding> findings,
        IEnumerable<string> values,
        string code)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        if (values.Any(value => !unique.Add(value)))
        {
            findings.Add(Finding(code, "A manifest identity is duplicated."));
        }
    }

    private static ValidationFinding Finding(string code, string explanation)
    {
        FindingCode findingCode = FindingCode.Create(code).Value!;
        FindingExplanation findingExplanation = FindingExplanation.Create(explanation).Value!;
        FindingSourceComponent source = FindingSourceComponent.Create("manifest-validator").Value!;
        CorrectiveAction action =
            CorrectiveAction.Create("Correct the manifest and validate it again.").Value!;
        return ValidationFinding.Create(
            findingCode,
            FindingSeverity.Error,
            findingExplanation,
            source,
            null,
            action,
            blocksRelease: true).Value!;
    }
}

public sealed class ProductManifestValidationResult
{
    private ProductManifestValidationResult(
        bool isValid,
        ProductManifest? value,
        ReadOnlyCollection<ValidationFinding> findings)
    {
        IsValid = isValid;
        Value = value;
        Findings = findings;
    }

    public bool IsValid { get; }

    public ProductManifest? Value { get; }

    public IReadOnlyList<ValidationFinding> Findings { get; }

    internal static ProductManifestValidationResult Success(ProductManifest value) =>
        new(true, value, Array.AsReadOnly(Array.Empty<ValidationFinding>()));

    internal static ProductManifestValidationResult Failure(
        IEnumerable<ValidationFinding> findings) =>
        new(false, null, Array.AsReadOnly(findings.ToArray()));
}
