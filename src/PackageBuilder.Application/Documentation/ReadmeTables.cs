using System.Globalization;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Items;
using PackageBuilder.Domain.Manifests;
using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Application.Documentation;

/// <summary>Target-measured per-item geometry with the exact delivered model reference.</summary>
public sealed record ReadmeItemMeasurements(InternalAssetId ItemId, string File, ReadmeMeasurements Measurements);

/// <summary>Immutable table data; only the renderer supplies Markdown delimiters.</summary>
public sealed class ReadmeTable
{
    internal ReadmeTable(string heading, string[] headers, IEnumerable<string[]> rows)
    {
        Heading = heading;
        Headers = Array.AsReadOnly(headers);
        Rows = Array.AsReadOnly(rows.Select(row => (IReadOnlyList<string>)Array.AsReadOnly(row)).ToArray());
    }

    public string Heading { get; }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
}

/// <summary>Generates declaration-ordered tables from canonical inspected animation data and measured item results.</summary>
public static class ReadmeTableGenerator
{
    public static DocumentationResult<ReadmeTable> Animations(ProductManifest? manifest,
        IEnumerable<AnimationDefinition>? measurements)
    {
        if (manifest is null || manifest.Animations.Count == 0 || measurements is null)
        { return Failure("DOC_ANIMATION_METRICS_REQUIRED"); }
        AnimationDefinition[] measured = [.. measurements.Take(1025)];
        if (measured.Length > 1024 || measured.Length != manifest.Animations.Count ||
            measured.Any(clip => clip is null || !clip.Rig.Equals(manifest.Rig) || !double.IsFinite(clip.DurationSeconds)) ||
            measured.Select(clip => clip.Name).Distinct(StringComparer.Ordinal).Count() != measured.Length ||
            manifest.Animations.Any(clip => !measured.Any(row => row.Name == clip.Name)))
        { return Failure("DOC_ANIMATION_METRICS_INVALID"); }
        Dictionary<string, AnimationDefinition> byName = measured.ToDictionary(clip => clip.Name, StringComparer.Ordinal);
        return DocumentationResult<ReadmeTable>.Success(new("Animations", ["Clip", "Start frame", "End frame", "Duration (s)", "FPS", "Loop", "Root motion", "Root bone"],
            manifest.Animations.Select(clip => byName[clip.Name]).Select(clip => new[] { clip.Name, Integer(clip.StartFrame), Integer(clip.EndFrame),
                Number(clip.DurationSeconds), Number(clip.FramesPerSecond), clip.LoopBehavior.CanonicalIdentifier,
                clip.RootMotionStatus.CanonicalIdentifier, clip.RootMotionBoneIdentity ?? "None" })));
    }

    /// <summary>Rejects incomplete/duplicate/foreign measurements; never infers per-item counts from product totals.</summary>
    public static DocumentationResult<ReadmeTable> Inventory(ProductManifest? manifest, ReadmeBuildData? build,
        IEnumerable<ReadmeItemMeasurements>? measurements)
    {
        IReadOnlyList<ItemDefinition>? items = manifest?.ItemSet?.Items ?? manifest?.ItemCollection?.Items;
        if (items is null || build is null || measurements is null)
        { return Failure("DOC_ITEM_METRICS_REQUIRED"); }
        ReadmeItemMeasurements[] measured = [.. measurements.Take(1025)];
        if (measured.Length > 1024 || measured.Length != items.Count || measured.Any(item => item is null || item.ItemId is null ||
            item.Measurements is null || !ReadmeBuildData.ValidMeasurements(item.Measurements) ||
            !DocumentationPath.IsValid(item.File) || !build.Files.Any(file => file.RelativePath == item.File)) ||
            measured.Select(item => item.ItemId).Distinct().Count() != items.Count ||
            measured.Select(item => item.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count ||
            items.Any(item => !measured.Any(row => row.ItemId.Equals(item.Id))))
        {
            return Failure("DOC_ITEM_METRICS_INVALID");
        }

        Dictionary<InternalAssetId, ReadmeItemMeasurements> byId = measured.ToDictionary(item => item.ItemId);
        return DocumentationResult<ReadmeTable>.Success(new("Item inventory",
            ["Item", "Delivered file", "Width (m)", "Height (m)", "Depth (m)", "Triangles", "Materials", "Textures", "Slot"],
            items.Select(item =>
            {
                ReadmeItemMeasurements row = byId[item.Id];
                ReadmeMeasurements value = row.Measurements;
                return new[] { item.Id.Value, row.File, Number(value.Width), Number(value.Height), Number(value.Depth),
                    Integer(value.Triangles), Integer(value.Materials), Integer(value.Textures), item.AttachmentSlot?.CanonicalIdentifier ?? "None" };
            })));
    }

    internal static string Number(double value) => value == 0 ? "0" : value.ToString("G17", CultureInfo.InvariantCulture);
    private static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static DocumentationResult<ReadmeTable> Failure(string code) => DocumentationResult<ReadmeTable>.Failure(code);
}
