using System.Collections.ObjectModel;

namespace PackageBuilder.Contracts.Migrations;

public enum MigrationRegistryError
{
    None = 0,
    InvalidRegistration,
    DuplicateRegistration,
    Gap,
    Cycle,
    Downgrade,
    AmbiguousPath,
}

public sealed class MigrationRegistryResult
{
    private MigrationRegistryResult(
        MigrationRegistry? registry,
        MigrationRegistryError error,
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> currentVersions)
    {
        Registry = registry;
        Error = error;
        CurrentVersions = currentVersions;
    }

    public bool IsValid => Registry is not null;

    public MigrationRegistry? Registry { get; }

    public MigrationRegistryError Error { get; }

    public IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> CurrentVersions { get; }

    internal static MigrationRegistryResult Success(
        MigrationRegistry registry,
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> currentVersions) =>
        new(registry, MigrationRegistryError.None, currentVersions);

    internal static MigrationRegistryResult Failure(
        MigrationRegistryError error,
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> currentVersions) =>
        new(null, error, currentVersions);
}

/// <summary>
/// Immutable explicit registry. A valid registry contains only unique, forward, single-version
/// steps and one unambiguous contiguous chain per registered family.
/// </summary>
public sealed class MigrationRegistry
{
    private readonly ReadOnlyDictionary<
        (MigrationDocumentFamily Family, int From),
        IJsonMigrationStep> _steps;

    private MigrationRegistry(
        Dictionary<(MigrationDocumentFamily Family, int From), IJsonMigrationStep> steps,
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> currentVersions)
    {
        _steps = new ReadOnlyDictionary<
            (MigrationDocumentFamily Family, int From),
            IJsonMigrationStep>(steps);
        CurrentVersions = currentVersions;
    }

    public IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> CurrentVersions { get; }

    public static MigrationRegistryResult Create(
        IEnumerable<IJsonMigrationStep?>? registrations,
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion>? currentVersions)
    {
        ReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> versions =
            SnapshotVersions(currentVersions);
        if (registrations is null ||
            versions.Count != Enum.GetValues<MigrationDocumentFamily>().Length ||
            versions.Any(
                pair =>
                    !Enum.IsDefined(pair.Key) ||
                    pair.Value is null))
        {
            return MigrationRegistryResult.Failure(
                MigrationRegistryError.InvalidRegistration,
                versions);
        }

        IJsonMigrationStep?[] supplied = [.. registrations];
        if (supplied.Any(step => step is null))
        {
            return MigrationRegistryResult.Failure(
                MigrationRegistryError.InvalidRegistration,
                versions);
        }

        IJsonMigrationStep[] steps = [.. supplied.Cast<IJsonMigrationStep>()];
        if (steps.Any(
                step =>
                    !Enum.IsDefined(step.Family) ||
                    step.FromVersion is null ||
                    step.ToVersion is null ||
                    !versions.ContainsKey(step.Family)))
        {
            return MigrationRegistryResult.Failure(
                MigrationRegistryError.InvalidRegistration,
                versions);
        }

        if (steps.GroupBy(
                step => (step.Family, step.FromVersion.Value, step.ToVersion.Value))
            .Any(group => group.Count() != 1))
        {
            return MigrationRegistryResult.Failure(
                MigrationRegistryError.DuplicateRegistration,
                versions);
        }

        if (steps.GroupBy(step => (step.Family, step.FromVersion.Value))
            .Any(group => group.Select(step => step.ToVersion.Value).Distinct().Count() != 1))
        {
            return MigrationRegistryResult.Failure(
                MigrationRegistryError.AmbiguousPath,
                versions);
        }

        if (HasCycle(steps))
        {
            return MigrationRegistryResult.Failure(MigrationRegistryError.Cycle, versions);
        }

        if (steps.Any(step => step.ToVersion.CompareTo(step.FromVersion) <= 0))
        {
            return MigrationRegistryResult.Failure(MigrationRegistryError.Downgrade, versions);
        }

        if (steps.Any(
                step =>
                    step.ToVersion.Value != step.FromVersion.Value + 1 ||
                    step.ToVersion.CompareTo(versions[step.Family]) > 0))
        {
            return MigrationRegistryResult.Failure(MigrationRegistryError.Gap, versions);
        }

        foreach (IGrouping<MigrationDocumentFamily, IJsonMigrationStep> family in
                 steps.GroupBy(step => step.Family))
        {
            int minimum = family.Min(step => step.FromVersion.Value);
            int current = versions[family.Key].Value;
            var fromVersions = family.Select(step => step.FromVersion.Value).ToHashSet();
            if (Enumerable.Range(minimum, current - minimum)
                .Any(version => !fromVersions.Contains(version)))
            {
                return MigrationRegistryResult.Failure(MigrationRegistryError.Gap, versions);
            }
        }

        Dictionary<(MigrationDocumentFamily Family, int Value), IJsonMigrationStep> map = steps.ToDictionary(
            step => (step.Family, step.FromVersion.Value),
            step => step);
        return MigrationRegistryResult.Success(new MigrationRegistry(map, versions), versions);
    }

    internal bool HasRegistrations(MigrationDocumentFamily family) =>
        _steps.Keys.Any(key => key.Family == family);

    internal bool TryGetStep(
        MigrationDocumentFamily family,
        SchemaVersion fromVersion,
        out IJsonMigrationStep? step) =>
        _steps.TryGetValue((family, fromVersion.Value), out step);

    internal IJsonMigrationStep GetRequiredStep(
        MigrationDocumentFamily family,
        SchemaVersion fromVersion) =>
        _steps[(family, fromVersion.Value)];

    private static ReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion> SnapshotVersions(
        IReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion>? values)
    {
        var snapshot = new Dictionary<MigrationDocumentFamily, SchemaVersion>();
        if (values is not null)
        {
            foreach (KeyValuePair<MigrationDocumentFamily, SchemaVersion> pair in values)
            {
                snapshot[pair.Key] = pair.Value;
            }
        }

        return new ReadOnlyDictionary<MigrationDocumentFamily, SchemaVersion>(snapshot);
    }

    private static bool HasCycle(IReadOnlyCollection<IJsonMigrationStep> steps)
    {
        foreach (IGrouping<MigrationDocumentFamily, IJsonMigrationStep> family in
                 steps.GroupBy(step => step.Family))
        {
            var edges = family.GroupBy(step => step.FromVersion.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(step => step.ToVersion.Value).ToArray());
            var visiting = new HashSet<int>();
            var visited = new HashSet<int>();
            foreach (int version in edges.Keys)
            {
                if (Visit(version, edges, visiting, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Visit(
        int version,
        IReadOnlyDictionary<int, int[]> edges,
        HashSet<int> visiting,
        HashSet<int> visited)
    {
        if (!visiting.Add(version))
        {
            return true;
        }

        if (visited.Contains(version))
        {
            _ = visiting.Remove(version);
            return false;
        }

        if (edges.TryGetValue(version, out int[]? targets) &&
            targets.Any(target => Visit(target, edges, visiting, visited)))
        {
            return true;
        }

        _ = visiting.Remove(version);
        _ = visited.Add(version);
        return false;
    }
}
