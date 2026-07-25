using System.Collections.ObjectModel;
using PackageBuilder.Contracts.Configuration;

namespace PackageBuilder.Application.Configuration;

public sealed class PackageBuilderPathConfigurationResult
{
    private PackageBuilderPathConfigurationResult(
        PackageBuilderPathConfiguration? configuration,
        IReadOnlyList<ConfigurationFailure> failures)
    {
        Configuration = configuration;
        Failures = new ReadOnlyCollection<ConfigurationFailure>(failures.ToArray());
    }

    public PackageBuilderPathConfiguration? Configuration { get; }

    public IReadOnlyList<ConfigurationFailure> Failures { get; }

    public bool IsSuccess => Configuration is not null && Failures.Count == 0;

    internal static PackageBuilderPathConfigurationResult Success(PackageBuilderPathConfiguration configuration) => new(configuration, []);

    internal static PackageBuilderPathConfigurationResult Failed(IEnumerable<ConfigurationFailure> failures) => new(null, failures.ToArray());
}
