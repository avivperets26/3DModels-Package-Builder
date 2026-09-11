using PackageBuilder.Application.Configuration;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Contracts.Profiles;
using PackageBuilder.Domain.Naming;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Application.Documentation;

/// <summary>One explicit catalog entry. The file is relative to the repository; identity is checked after parsing.</summary>
public sealed record PublisherProfileFile(string Reference, string RelativePath);

/// <summary>Resolves configured profiles through existing bounded UTF-8 I/O and strict schema/domain validation.</summary>
public sealed class PublisherProfileResolver(IConfigurationTextReader reader, IReparsePointInspector inspector)
{
    private readonly IConfigurationTextReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly IReparsePointInspector _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));

    /// <summary>Null selection uses the configured default. Explicit unknown/invalid selections never silently fall back.</summary>
    public DocumentationResult<PublisherProfile> Resolve(string? reference, string? defaultReference,
        IEnumerable<PublisherProfileFile>? profileFiles)
    {
        string? selected = reference ?? defaultReference;
        if (!PublisherRoot.Create(selected).IsValid)
        { return Failure("PUBLISHER_REFERENCE_INVALID"); }
        if (profileFiles is null)
        { return Failure("PUBLISHER_CATALOG_INVALID"); }
        PublisherProfileFile[] catalog = [.. profileFiles.Take(129)];
        if (catalog.Length is 0 or > 128 || catalog.Any(entry => entry is null ||
            !PublisherRoot.Create(entry.Reference).IsValid || !ValidPath(entry.RelativePath)) ||
            catalog.Select(entry => entry.Reference).Distinct(StringComparer.Ordinal).Count() != catalog.Length)
        {
            return Failure("PUBLISHER_CATALOG_INVALID");
        }

        PublisherProfileFile? entry = catalog.SingleOrDefault(item => string.Equals(item.Reference, selected, StringComparison.Ordinal));
        if (entry is null)
        { return Failure("PUBLISHER_NOT_FOUND"); }
        string root = PackageBuilderPathConfigurationLoader.ApprovedProjectRoot;
        string path = Path.GetFullPath(Path.Combine(root, entry.RelativePath));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            _inspector.Inspect(root, Path.GetDirectoryName(path)!, "publisherProfile") is not null)
        {
            return Failure("PUBLISHER_PATH_UNSAFE");
        }

        ConfigurationReadResult read = _reader.Read(path, PublisherProfileJson.MaximumInputCharacters * 4);
        if (!read.IsSuccess)
        { return Failure("PUBLISHER_READ_FAILED"); }
        ProfileJsonDeserializationResult<PublisherProfile> parsed = PublisherProfileJson.Deserialize(read.Content);
        if (!parsed.IsSuccessful)
        { return Failure("PUBLISHER_PROFILE_INVALID"); }
        if (!string.Equals(parsed.Value!.Root.Value, selected, StringComparison.Ordinal))
        { return Failure("PUBLISHER_IDENTITY_MISMATCH"); }
        // Branding remains typed logical source references; resolution does not fetch, execute or package images.
        return DocumentationResult<PublisherProfile>.Success(parsed.Value);
    }

    private static bool ValidPath(string? value) => DocumentationPath.IsValid(value) &&
        value!.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static DocumentationResult<PublisherProfile> Failure(string code) => DocumentationResult<PublisherProfile>.Failure(code);
}
