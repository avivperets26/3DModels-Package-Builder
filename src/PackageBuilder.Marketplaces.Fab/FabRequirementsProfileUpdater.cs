using System.Collections.Immutable;
using System.Text.Json;
using PackageBuilder.Contracts.BuildLocks;
using PackageBuilder.Contracts.Persistence;

namespace PackageBuilder.Marketplaces.Fab;

/// <summary>Bounded canonical differences for review; values are data and must be escaped by presentation layers.</summary>
public sealed record FabProfileChange(string Path, string? Before, string? After);

/// <summary>Trusted local fixture runner; the updater binds returned evidence to the exact tested candidate.</summary>
public interface IFabRequirementsCompatibilitySuite
{
    Task<PersistedCompatibilitySuiteResult> RunAsync(FabRequirementsProfile candidate, CancellationToken cancellationToken);
}

/// <summary>Unforgeable within the public API: a candidate, its tested current base, and complete suite evidence.</summary>
public sealed class TestedFabRequirementsCandidate
{
    internal TestedFabRequirementsCandidate(FabRequirementsProfile profile, string? currentHash,
        PersistedCompatibilitySuiteResult evidence)
    { Profile = profile; ExpectedCurrentSha256 = currentHash; Evidence = evidence; }

    public FabRequirementsProfile Profile { get; }
    public string? ExpectedCurrentSha256 { get; }
    public PersistedCompatibilitySuiteResult Evidence { get; }
    public bool Passed => CompatibilityEvidenceValidation.IsValid(Evidence, CompatibilitySuiteOutcome.Passed);
}

/// <summary>Offline candidate import, review, test and explicit promotion. Existing build pins never follow current.</summary>
public sealed class FabRequirementsProfileUpdater(IRequirementsProfileRepository repository)
{
    public async Task<RepositoryOperationResult<FabRequirementsProfile>> CacheAsync(string json, CancellationToken cancellationToken = default)
    {
        RepositoryOperationResult<FabRequirementsProfile> loaded = FabRequirementsProfileJson.Load(json);
        if (!loaded.IsSuccess)
        { return loaded; }
        FabRequirementsProfile profile = loaded.Value!;
        RepositoryOperationResult cached = await repository.CacheAsync(new CachedRequirementsProfile(
            "fab", profile.Document.Version, profile.Document.EffectiveOn, profile.Document.Sources[0].Url,
            profile.CanonicalJson, profile.Sha256, false), cancellationToken);
        return cached.IsSuccess ? loaded : Forward<FabRequirementsProfile>(cached.Error!);
    }

    public async Task<RepositoryOperationResult<FabRequirementsProfile?>> LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        RepositoryOperationResult<CachedRequirementsProfile?> stored = await repository.GetCurrentAsync("fab", cancellationToken);
        return LoadStored(stored, true);
    }

    public async Task<RepositoryOperationResult<FabRequirementsProfile?>> LoadCachedAsync(string version, CancellationToken cancellationToken = default) =>
        LoadStored(await repository.GetAsync("fab", version, cancellationToken), false);

    /// <summary>Captures the current digest before running the suite so a concurrent approval invalidates this review.</summary>
    public async Task<RepositoryOperationResult<TestedFabRequirementsCandidate>> TestAsync(
        string version, IFabRequirementsCompatibilitySuite suite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suite);
        RepositoryOperationResult<FabRequirementsProfile?> candidate = await LoadCachedAsync(version, cancellationToken);
        if (!candidate.IsSuccess)
        { return Forward<TestedFabRequirementsCandidate>(candidate.Error!); }
        if (candidate.Value is null)
        { return Invalid<TestedFabRequirementsCandidate>("Candidate is not cached."); }
        RepositoryOperationResult<FabRequirementsProfile?> current = await LoadCurrentAsync(cancellationToken);
        if (!current.IsSuccess)
        { return Forward<TestedFabRequirementsCandidate>(current.Error!); }
        PersistedCompatibilitySuiteResult evidence = await suite.RunAsync(candidate.Value, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return !CompatibilityEvidenceValidation.IsValid(evidence, CompatibilitySuiteOutcome.Passed)
            && !CompatibilityEvidenceValidation.IsValid(evidence, CompatibilitySuiteOutcome.Failed)
            ? Invalid<TestedFabRequirementsCandidate>("The suite returned incomplete evidence.")
            : RepositoryOperationResult.Success(new TestedFabRequirementsCandidate(candidate.Value, current.Value?.Sha256, evidence));
    }

    /// <summary>Requires an affirmative review, a passing complete suite, and a still-current comparison base.</summary>
    public Task<RepositoryOperationResult> ApproveAsync(TestedFabRequirementsCandidate candidate, bool explicitlyApproved,
        string reviewer, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return !explicitlyApproved || !candidate.Passed
            ? Task.FromResult(RepositoryOperationResult.Failure("FAB_APPROVAL_REQUIRED", "Explicit review and passing compatibility evidence are required."))
            : repository.ApproveAsync(new RequirementsProfileApproval("fab", candidate.Profile.Document.Version,
            candidate.Profile.Sha256, candidate.ExpectedCurrentSha256, reviewer, approvedAtUtc, candidate.Evidence), cancellationToken);
    }

    /// <summary>Loads only approved immutable content named in an existing build.lock entry, including its digest.</summary>
    public async Task<RepositoryOperationResult<FabRequirementsProfile>> LoadPinnedAsync(
        BuildLockMarketplaceProfile pin, CancellationToken cancellationToken = default)
    {
        const string Separator = "+sha256.";
        if (pin is null || pin.Marketplace != "fab" || pin.Profile != "asset-listing" || pin.Version is null)
        { return Invalid<FabRequirementsProfile>("Invalid Fab build pin."); }
        int separator = pin.Version.IndexOf(Separator, StringComparison.Ordinal);
        if (separator < 0 || !FabRequirementsProfileJson.IsVersion(pin.Version[..separator])
            || !CompatibilityEvidenceValidation.IsSha256(pin.Version[(separator + Separator.Length)..]))
        { return Invalid<FabRequirementsProfile>("The Fab build pin must include its version and SHA-256."); }
        RepositoryOperationResult<FabRequirementsProfile?> loaded = LoadStored(
            await repository.GetAsync("fab", pin.Version[..separator], cancellationToken), true);
        return !loaded.IsSuccess
            ? Forward<FabRequirementsProfile>(loaded.Error!)
            : loaded.Value?.BuildLockIdentity == pin ? RepositoryOperationResult.Success(loaded.Value)
            : Invalid<FabRequirementsProfile>("The approved pinned profile is missing or its digest differs.");
    }

    /// <summary>Compares every canonical field, including source dates and companion policy; order-only edits disappear.</summary>
    public static ImmutableArray<FabProfileChange> Compare(FabRequirementsProfile? before, FabRequirementsProfile after)
    {
        ArgumentNullException.ThrowIfNull(after);
        using var next = JsonDocument.Parse(after.CanonicalJson);
        using var previous = JsonDocument.Parse(before?.CanonicalJson ?? "{}");
        ImmutableArray<FabProfileChange>.Builder changes = ImmutableArray.CreateBuilder<FabProfileChange>();
        CompareObject("", previous.RootElement, next.RootElement, changes);
        return changes.ToImmutable();
    }

    private static void CompareObject(string path, JsonElement before, JsonElement after, ImmutableArray<FabProfileChange>.Builder changes)
    {
        foreach (string key in before.EnumerateObject().Select(property => property.Name)
            .Union(after.EnumerateObject().Select(property => property.Name), StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            bool had = before.TryGetProperty(key, out JsonElement oldValue);
            bool has = after.TryGetProperty(key, out JsonElement newValue);
            string? oldJson = had ? oldValue.GetRawText() : null;
            string? newJson = has ? newValue.GetRawText() : null;
            if (oldJson == newJson)
            { continue; }
            if (had && has && oldValue.ValueKind == JsonValueKind.Object && newValue.ValueKind == JsonValueKind.Object)
            { CompareObject($"{path}/{key}", oldValue, newValue, changes); }
            else
            { changes.Add(new FabProfileChange($"{path}/{key}", oldJson, newJson)); }
        }
    }

    private static RepositoryOperationResult<FabRequirementsProfile?> LoadStored(
        RepositoryOperationResult<CachedRequirementsProfile?> stored, bool requireApproved)
    {
        if (!stored.IsSuccess)
        { return Forward<FabRequirementsProfile?>(stored.Error!); }
        if (stored.Value is null)
        { return RepositoryOperationResult.Success<FabRequirementsProfile?>(null); }
        CachedRequirementsProfile cached = stored.Value;
        RepositoryOperationResult<FabRequirementsProfile> parsed = FabRequirementsProfileJson.Load(cached.Json);
        return parsed.IsSuccess && parsed.Value!.Sha256 == cached.Sha256 && parsed.Value.Document.Version == cached.Version
            && cached.Marketplace == "fab" && parsed.Value.Document.EffectiveOn == cached.EffectiveOn
            && (!requireApproved || cached.IsApproved)
            ? RepositoryOperationResult.Success<FabRequirementsProfile?>(parsed.Value)
            : Invalid<FabRequirementsProfile?>("Cached profile metadata, approval or content is invalid.");
    }

    private static RepositoryOperationResult<T> Invalid<T>(string message) => RepositoryOperationResult.Failure<T>("FAB_PROFILE_INVALID", message);
    private static RepositoryOperationResult<T> Forward<T>(RepositoryOperationError error) => RepositoryOperationResult.Failure<T>(error.Code, error.Message);
}
