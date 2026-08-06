using System.Collections.ObjectModel;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Targets.Portable;

/// <summary>Reports the external texture references observed in one exported asset.</summary>
public sealed class PortableAssetReferenceEvidence
{
    private readonly ReadOnlyCollection<string> _references;

    public PortableAssetReferenceEvidence(string assetFileName, IEnumerable<string> references)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetFileName);
        ArgumentNullException.ThrowIfNull(references);
        string[] values = [.. references];
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("References cannot contain null or blank values.", nameof(references));
        }

        AssetFileName = assetFileName;
        _references = Array.AsReadOnly(values.Order(StringComparer.Ordinal).ToArray());
    }

    public string AssetFileName { get; }

    public IReadOnlyList<string> References => _references;
}

/// <summary>Summarizes one PB-0417 clean-reimport decision for an exported asset.</summary>
public sealed record PortableReimportEvidence(string AssetFileName, bool Passed);

/// <summary>Contains immutable blocking findings for one complete portable target validation.</summary>
public sealed class PortableTargetValidationReport
{
    private readonly ReadOnlyCollection<ValidationFinding> _findings;

    internal PortableTargetValidationReport(IEnumerable<ValidationFinding> findings)
    {
        _findings = Array.AsReadOnly(findings.ToArray());
    }

    public IReadOnlyList<ValidationFinding> Findings => _findings;

    public bool Passed => _findings.All(static finding => !finding.BlocksRelease);
}
