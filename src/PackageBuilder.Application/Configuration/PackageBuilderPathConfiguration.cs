using System.Collections.ObjectModel;

namespace PackageBuilder.Application.Configuration;

public sealed class PackageBuilderPathConfiguration
{
    private readonly ReadOnlyCollection<ConfiguredPathRoot> _roots;

    internal PackageBuilderPathConfiguration(IReadOnlyDictionary<PathRootKind, ConfiguredPathRoot> values)
    {
        Repository = values[PathRootKind.Repository];
        Tools = values[PathRootKind.Tools];
        Downloads = values[PathRootKind.Downloads];
        Data = values[PathRootKind.Data];
        SourceAssets = values[PathRootKind.SourceAssets];
        Jobs = values[PathRootKind.Jobs];
        Cache = values[PathRootKind.Cache];
        Temp = values[PathRootKind.Temp];
        Templates = values[PathRootKind.Templates];
        Builds = values[PathRootKind.Builds];
        Artifacts = values[PathRootKind.Artifacts];
        Logs = values[PathRootKind.Logs];
        _roots = Array.AsReadOnly(
        [
            Repository,
            Tools,
            Downloads,
            Data,
            SourceAssets,
            Jobs,
            Cache,
            Temp,
            Templates,
            Builds,
            Artifacts,
            Logs,
        ]);
    }

    public ConfiguredPathRoot Repository { get; }

    public ConfiguredPathRoot Tools { get; }

    public ConfiguredPathRoot Downloads { get; }

    public ConfiguredPathRoot Data { get; }

    public ConfiguredPathRoot SourceAssets { get; }

    public ConfiguredPathRoot Jobs { get; }

    public ConfiguredPathRoot Cache { get; }

    public ConfiguredPathRoot Temp { get; }

    public ConfiguredPathRoot Templates { get; }

    public ConfiguredPathRoot Builds { get; }

    public ConfiguredPathRoot Artifacts { get; }

    public ConfiguredPathRoot Logs { get; }

    public IReadOnlyList<ConfiguredPathRoot> Roots => _roots;
}
