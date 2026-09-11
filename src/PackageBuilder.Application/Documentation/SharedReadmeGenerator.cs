using System.Globalization;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Application.Documentation;

/// <summary>Builds common sections from manifest identity, resolved publisher configuration and measured target results.</summary>
public static class SharedReadmeGenerator
{
    public static DocumentationResult<ReadmeDocument> Generate(ProductManifest? manifest, PublisherProfile? publisher,
        ReadmeBuildData? build, CancellationToken cancellationToken = default) =>
        Generate(manifest, publisher, build, null, cancellationToken);

    public static DocumentationResult<ReadmeDocument> Generate(ProductManifest? manifest, PublisherProfile? publisher,
        ReadmeBuildData? build, IEnumerable<ReadmeItemMeasurements>? itemMeasurements,
        CancellationToken cancellationToken = default) =>
        Generate(manifest, publisher, build, itemMeasurements, null, cancellationToken);

    /// <summary>Clip metadata must come from inspection of the delivered clips; manifest values are not a fallback.</summary>
    public static DocumentationResult<ReadmeDocument> Generate(ProductManifest? manifest, PublisherProfile? publisher,
        ReadmeBuildData? build, IEnumerable<ReadmeItemMeasurements>? itemMeasurements,
        IEnumerable<AnimationDefinition>? animationMeasurements,
        CancellationToken cancellationToken = default)
    {
        if (manifest is null || publisher is null || build is null)
        { return Failure("DOC_REQUIRED_DATA_MISSING"); }
        if (manifest.Animations.Count > 1024)
        { return Failure("DOC_ANIMATION_LIMIT"); }
        if (!manifest.PublisherProfileReference.Equals(publisher.Root))
        { return Failure("DOC_PUBLISHER_MISMATCH"); }
        if (!manifest.Targets.Contains(build.Target))
        { return Failure("DOC_TARGET_MISMATCH"); }
        ReadmeMeasurements metrics = build.Measurements;
        var sections = new List<(string Heading, string[] Lines)>
        {
            ("Contents and formats", build.Files.Select(file => $"{file.RelativePath} — {file.Format} {file.FormatVersion}").ToArray()),
            ("Engine and pipeline", build.Engine is null ? ["Portable output; no engine or render pipeline required."] :
                [$"{build.Engine.Name} {build.Engine.Version}", build.Engine.Pipeline is null ? "No render pipeline dependency." :
                    $"{build.Engine.Pipeline} {build.Engine.PipelineVersion}"]),
            ("Technical metrics", [$"Width: {Number(metrics.Width)} m", $"Height: {Number(metrics.Height)} m",
                $"Depth: {Number(metrics.Depth)} m", $"Triangles: {metrics.Triangles.ToString(CultureInfo.InvariantCulture)}",
                $"Materials: {metrics.Materials.ToString(CultureInfo.InvariantCulture)}",
                $"Textures: {metrics.Textures.ToString(CultureInfo.InvariantCulture)}",
                $"Scale: {Number(metrics.MetresPerUnit)} m per unit", $"Up axis: {metrics.UpAxis}",
                $"Forward axis: {metrics.ForwardAxis}", $"Pivot: {metrics.Pivot}"]),
            ("Dependencies", build.Dependencies.Count == 0 ? ["None required."] :
                build.Dependencies.Select(value => $"{value.Name} {value.Version}").ToArray()),
            ("Installation and usage", build.Usage.ToArray()),
        };
        sections.AddRange(ReadmeCaseSections.Create(manifest, build.Target));
        var tables = new List<ReadmeTable>();
        if (manifest.Animations.Count > 0)
        {
            DocumentationResult<ReadmeTable> animation = ReadmeTableGenerator.Animations(manifest, animationMeasurements);
            if (!animation.IsSuccess)
            { return Failure(animation.Error!); }
            tables.Add(animation.Value!);
        }
        else if (animationMeasurements is not null && animationMeasurements.Any())
        { return Failure("DOC_UNEXPECTED_ANIMATION_METRICS"); }
        if (manifest.ItemSet is not null || manifest.ItemCollection is not null)
        {
            DocumentationResult<ReadmeTable> inventory = ReadmeTableGenerator.Inventory(manifest, build, itemMeasurements);
            if (!inventory.IsSuccess)
            { return Failure(inventory.Error!); }
            tables.Add(inventory.Value!);
        }
        else if (itemMeasurements is not null && itemMeasurements.Any())
        {
            return Failure("DOC_UNEXPECTED_ITEM_METRICS");
        }

        sections.AddRange(PublisherDocumentation.Sections(publisher));
        string[] identity = [$"Product ID: {manifest.AssetId.Value}", $"Product case: {manifest.ProductCase.CanonicalIdentifier}",
            $"Version: {manifest.Version.Value}", $"Publisher: {publisher.DisplayName.Value} ({publisher.Root.Value})"];
        return !DocumentationText.IsValid(manifest.DisplayName.Value) || identity.Any(line => !DocumentationText.IsValid(line)) ||
            sections.Any(section => section.Lines.Any(line => !DocumentationText.IsValid(line, 4096))) ||
            tables.Any(table => table.Rows.Any(row => row.Any(cell => !DocumentationText.IsValid(cell, 4096))))
            ? Failure("DOC_TEXT_INVALID")
            : DocumentationTemplateEngine.Render(manifest.DisplayName.Value, identity, sections, tables, cancellationToken);
    }

    private static string Number(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
    private static DocumentationResult<ReadmeDocument> Failure(string code) => DocumentationResult<ReadmeDocument>.Failure(code);
}
