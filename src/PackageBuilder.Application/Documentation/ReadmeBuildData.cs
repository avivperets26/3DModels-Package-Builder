using PackageBuilder.Domain.Targets;

namespace PackageBuilder.Application.Documentation;

/// <summary>One delivered file and its actual format/version, supplied by the target build result.</summary>
public sealed record ReadmeFile(string RelativePath, string Format, string FormatVersion);

/// <summary>Measured geometry in metres; counts describe the delivered product, not preview geometry.</summary>
public sealed record ReadmeMeasurements(double Width, double Height, double Depth, long Triangles,
    int Materials, int Textures, double MetresPerUnit, string UpAxis, string ForwardAxis, string Pivot);

/// <summary>Actual tested engine and optional render-pipeline versions; absent only for portable output.</summary>
public sealed record ReadmeEngine(string Name, string Version, string? Pipeline, string? PipelineVersion);

/// <summary>Explicit dependency requirement; an empty dependency collection means none are required.</summary>
public sealed record ReadmeDependency(string Name, string Version);

/// <summary>Validated immutable build evidence. Missing measurements/versions cannot become zero or blank prose.</summary>
public sealed class ReadmeBuildData
{
    private ReadmeBuildData(BuildTarget target, ReadmeFile[] files, ReadmeMeasurements measurements,
        ReadmeEngine? engine, ReadmeDependency[] dependencies, string[] usage)
    {
        Target = target;
        Files = Array.AsReadOnly(files);
        Measurements = measurements;
        Engine = engine;
        Dependencies = Array.AsReadOnly(dependencies);
        Usage = Array.AsReadOnly(usage);
    }

    public BuildTarget Target { get; }
    public IReadOnlyList<ReadmeFile> Files { get; }
    public ReadmeMeasurements Measurements { get; }
    public ReadmeEngine? Engine { get; }
    public IReadOnlyList<ReadmeDependency> Dependencies { get; }
    public IReadOnlyList<string> Usage { get; }

    /// <summary>Snapshots bounded inputs and rejects duplicate/unsafe paths and contradictory target metadata.</summary>
    public static DocumentationResult<ReadmeBuildData> Create(BuildTarget? target, IEnumerable<ReadmeFile>? files,
        ReadmeMeasurements? measurements, ReadmeEngine? engine,
        IEnumerable<ReadmeDependency>? dependencies, IEnumerable<string>? usage)
    {
        if (target is null || files is null || measurements is null || dependencies is null || usage is null)
        {
            return Failure("DOC_REQUIRED_DATA_MISSING");
        }

        ReadmeFile[] fileArray = [.. files.Take(1025)];
        ReadmeDependency[] dependencyArray = [.. dependencies.Take(129)];
        string[] instructions = [.. usage.Take(65)];
        if (fileArray.Length is 0 or > 1024 || dependencyArray.Length > 128 || instructions.Length is 0 or > 64)
        {
            return Failure("DOC_COLLECTION_LIMIT");
        }

        if (fileArray.Any(file => file is null || !DocumentationPath.IsValid(file.RelativePath) ||
                !DocumentationText.IsValid(file.Format, 64) || !DocumentationText.IsValid(file.FormatVersion, 64)) ||
            fileArray.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fileArray.Length)
        {
            return Failure("DOC_FILES_INVALID");
        }

        if (!ValidMeasurements(measurements))
        { return Failure("DOC_METRICS_INVALID"); }
        bool portable = target.Equals(BuildTarget.Portable);
        bool invalidEngine = portable ? engine is not null : engine is null ||
            !DocumentationText.IsValid(engine.Name, 64) || !DocumentationText.IsValid(engine.Version, 64) ||
            (engine.Pipeline is null) != (engine.PipelineVersion is null) ||
            engine.Pipeline is not null && (!DocumentationText.IsValid(engine.Pipeline, 64) ||
                !DocumentationText.IsValid(engine.PipelineVersion, 64)) ||
            !string.Equals(engine.Name, target.CanonicalIdentifier, StringComparison.OrdinalIgnoreCase);
        return invalidEngine
            ? Failure("DOC_ENGINE_INVALID")
            : dependencyArray.Any(value => value is null || !DocumentationText.IsValid(value.Name, 256) ||
                !DocumentationText.IsValid(value.Version, 64)) ||
            dependencyArray.Select(value => value.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != dependencyArray.Length ||
            instructions.Any(value => !DocumentationText.IsValid(value))
            ? Failure("DOC_USAGE_OR_DEPENDENCIES_INVALID")
            : DocumentationResult<ReadmeBuildData>.Success(new(target, fileArray, measurements, engine, dependencyArray, instructions));
    }

    private static bool ValidMeasurements(ReadmeMeasurements value) =>
        double.IsFinite(value.Width) && value.Width >= 0 && double.IsFinite(value.Height) && value.Height >= 0 &&
        double.IsFinite(value.Depth) && value.Depth >= 0 && Math.Max(value.Width, Math.Max(value.Height, value.Depth)) > 0 &&
        value.Triangles > 0 && value.Materials >= 0 && value.Textures >= 0 &&
        double.IsFinite(value.MetresPerUnit) && value.MetresPerUnit > 0 &&
        ValidAxis(value.UpAxis) && ValidAxis(value.ForwardAxis) && value.UpAxis[^1] != value.ForwardAxis[^1] &&
        DocumentationText.IsValid(value.Pivot, 256);

    private static bool ValidAxis(string? value) => value is "+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z";

    private static DocumentationResult<ReadmeBuildData> Failure(string code) => DocumentationResult<ReadmeBuildData>.Failure(code);
}
