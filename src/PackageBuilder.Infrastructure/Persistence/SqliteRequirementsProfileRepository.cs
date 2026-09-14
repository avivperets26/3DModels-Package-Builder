using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using PackageBuilder.Contracts.Json;
using PackageBuilder.Contracts.Persistence;

namespace PackageBuilder.Infrastructure.Persistence;

/// <summary>Uses the existing RequirementsProfiles cache and Settings receipts without changing database schema.
/// Versions are immutable; a transaction writes the approval, receipt and current pointer together.</summary>
public sealed class SqliteRequirementsProfileRepository : IRequirementsProfileRepository
{
    private readonly string _databasePath;
    private static readonly JsonSerializerOptions _receiptOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
    };

    private SqliteRequirementsProfileRepository(string databasePath) => _databasePath = databasePath;

    public static RepositoryOperationResult<SqliteRequirementsProfileRepository> Create(string projectRoot, string databasePath)
    {
        if (!SqliteRepositoryPath.TryValidate(projectRoot, databasePath, out string? path))
        {
            return Failure<SqliteRequirementsProfileRepository>("PATH_INVALID");
        }

        try
        {
            var repository = new SqliteRequirementsProfileRepository(path!);
            using SqliteConnection connection = repository.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != SqliteSchema.CurrentVersion)
            {
                return Failure<SqliteRequirementsProfileRepository>("SCHEMA_UNSUPPORTED");
            }

            command.CommandText = "PRAGMA quick_check;";
            if (command.ExecuteScalar() as string != "ok")
            {
                return Failure<SqliteRequirementsProfileRepository>("DATA_INVALID");
            }

            command.CommandText = "SELECT ProfileJson FROM RequirementsProfiles LIMIT 0; SELECT ValueJson FROM Settings LIMIT 0;";
            _ = command.ExecuteNonQuery();
            return RepositoryOperationResult.Success(repository);
        }
        catch (SqliteException)
        {
            return Failure<SqliteRequirementsProfileRepository>("STORAGE_FAILED");
        }
    }

    public Task<RepositoryOperationResult> CacheAsync(CachedRequirementsProfile profile, CancellationToken cancellationToken = default) =>
        Run(() =>
        {
            if (!Valid(profile) || profile.IsApproved)
            {
                return Failure("INPUT_INVALID");
            }

            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            CachedRequirementsProfile? existing = Read(connection, transaction, profile.Marketplace, profile.Version);
            if (existing is not null)
            {
                return existing with { IsApproved = false } == profile
                    ? RepositoryOperationResult.Success() : Failure("VERSION_CONFLICT");
            }

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO RequirementsProfiles
                (RequirementsProfileId,Marketplace,ProfileVersion,EffectiveDate,SourceReference,ProfileJson,Sha256,IsApproved)
                VALUES ($id,$marketplace,$version,$date,$source,$json,$hash,0);
                """;
            Add(command, "$id", $"{profile.Marketplace}:{profile.Version}");
            Add(command, "$marketplace", profile.Marketplace);
            Add(command, "$version", profile.Version);
            Add(command, "$date", profile.EffectiveOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Add(command, "$source", profile.SourceReference);
            Add(command, "$json", profile.Json);
            Add(command, "$hash", profile.Sha256);
            _ = command.ExecuteNonQuery();
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return RepositoryOperationResult.Success();
        }, cancellationToken);

    public Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetAsync(
        string marketplace, string version, CancellationToken cancellationToken = default) => RunValue(() =>
        {
            if (!Identity(marketplace) || !Identity(version))
            {
                return Failure<CachedRequirementsProfile?>("INPUT_INVALID");
            }

            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            CachedRequirementsProfile? profile = Read(connection, transaction, marketplace, version);
            if (profile?.IsApproved == true)
            { _ = ReadApproval(connection, transaction, profile); }
            return RepositoryOperationResult.Success(profile);
        }, cancellationToken);

    public Task<RepositoryOperationResult<CachedRequirementsProfile?>> GetCurrentAsync(
        string marketplace, CancellationToken cancellationToken = default) => RunValue(() =>
        {
            if (!Identity(marketplace))
            {
                return Failure<CachedRequirementsProfile?>("INPUT_INVALID");
            }

            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            return RepositoryOperationResult.Success(Current(connection, transaction, marketplace));
        }, cancellationToken);

    public Task<RepositoryOperationResult> ApproveAsync(RequirementsProfileApproval approval, CancellationToken cancellationToken = default) =>
        Run(() =>
        {
            if (!ValidApproval(approval))
            {
                return Failure("INPUT_INVALID");
            }

            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            CachedRequirementsProfile? candidate = Read(connection, transaction, approval.Marketplace, approval.Version);
            CachedRequirementsProfile? current = Current(connection, transaction, approval.Marketplace);
            if (candidate is null || candidate.IsApproved || candidate.Sha256 != approval.Sha256
                || current?.Sha256 != approval.ExpectedCurrentSha256
                || candidate.EffectiveOn > DateOnly.FromDateTime(approval.ApprovedAtUtc.UtcDateTime))
            {
                return Failure("PROMOTION_CONFLICT");
            }

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE RequirementsProfiles SET IsApproved=1 WHERE Marketplace=$marketplace AND ProfileVersion=$version;";
            Add(command, "$marketplace", approval.Marketplace);
            Add(command, "$version", approval.Version);
            _ = command.ExecuteNonQuery();
            WriteSetting(connection, transaction, ApprovalKey(approval.Marketplace, approval.Version), JsonSerializer.Serialize(approval), approval.ApprovedAtUtc, false);
            WriteSetting(connection, transaction, CurrentKey(approval.Marketplace), JsonSerializer.Serialize(approval.Version), approval.ApprovedAtUtc, true);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return RepositoryOperationResult.Success();
        }, cancellationToken);

    public Task<RepositoryOperationResult<RequirementsProfileApproval?>> GetApprovalAsync(
        string marketplace, string version, CancellationToken cancellationToken = default) => RunValue(() =>
        {
            if (!Identity(marketplace) || !Identity(version))
            {
                return Failure<RequirementsProfileApproval?>("INPUT_INVALID");
            }

            using SqliteConnection connection = Open();
            using SqliteTransaction transaction = connection.BeginTransaction();
            CachedRequirementsProfile? profile = Read(connection, transaction, marketplace, version);
            return RepositoryOperationResult.Success(profile is null ? null : ReadApproval(connection, transaction, profile));
        }, cancellationToken);

    private static CachedRequirementsProfile? Current(SqliteConnection connection, SqliteTransaction transaction, string marketplace)
    {
        string? json = ReadSetting(connection, transaction, CurrentKey(marketplace));
        if (json is null)
        {
            return null;
        }

        string version = JsonSerializer.Deserialize<string>(json) ?? throw new InvalidDataException();
        CachedRequirementsProfile profile = Read(connection, transaction, marketplace, version) ?? throw new InvalidDataException();
        if (!profile.IsApproved)
        { throw new InvalidDataException(); }
        _ = ReadApproval(connection, transaction, profile);
        return profile;
    }

    // Approved cache content and its immutable receipt must agree even after application restart.
    private static RequirementsProfileApproval? ReadApproval(SqliteConnection connection, SqliteTransaction transaction, CachedRequirementsProfile profile)
    {
        string? json = ReadSetting(connection, transaction, ApprovalKey(profile.Marketplace, profile.Version));
        if (json is null && !profile.IsApproved)
        { return null; }
        if (!profile.IsApproved || JsonInputSafeguards.TryParseObject(json, 16_384, out JsonDocument? document) != JsonInputError.None)
        { throw new InvalidDataException(); }
        using (document!)
        {
            RequirementsProfileApproval? receipt = document!.Deserialize<RequirementsProfileApproval>(_receiptOptions);
            return ValidApproval(receipt) && receipt!.Marketplace == profile.Marketplace && receipt.Version == profile.Version
                && receipt.Sha256 == profile.Sha256 && profile.EffectiveOn <= DateOnly.FromDateTime(receipt.ApprovedAtUtc.UtcDateTime)
                ? receipt : throw new InvalidDataException();
        }
    }

    private static bool ValidApproval(RequirementsProfileApproval? approval) => approval is not null
        && Identity(approval.Marketplace) && Identity(approval.Version)
        && CompatibilityEvidenceValidation.IsSha256(approval.Sha256)
        && (approval.ExpectedCurrentSha256 is null || CompatibilityEvidenceValidation.IsSha256(approval.ExpectedCurrentSha256))
        && CompatibilityEvidenceValidation.IsText(approval.Reviewer, 256)
        && CompatibilityEvidenceValidation.IsValid(approval.Evidence, CompatibilitySuiteOutcome.Passed)
        && approval.ApprovedAtUtc.Offset == TimeSpan.Zero && approval.Evidence.CompletedAtUtc <= approval.ApprovedAtUtc;

    private static CachedRequirementsProfile? Read(SqliteConnection connection, SqliteTransaction? transaction, string marketplace, string version)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EffectiveDate,SourceReference,ProfileJson,Sha256,IsApproved FROM RequirementsProfiles WHERE Marketplace=$marketplace AND ProfileVersion=$version;";
        Add(command, "$marketplace", marketplace);
        Add(command, "$version", version);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        if (!DateOnly.TryParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            throw new InvalidDataException();
        }

        var profile = new CachedRequirementsProfile(marketplace, version, date, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4));
        return Valid(profile) ? profile : throw new InvalidDataException();
    }

    private static bool Valid(CachedRequirementsProfile? profile)
    {
        if (profile is null || !Identity(profile.Marketplace) || !Identity(profile.Version) || profile.EffectiveOn == default
            || !CompatibilityEvidenceValidation.IsText(profile.SourceReference, 1024)
            || !CompatibilityEvidenceValidation.IsSha256(profile.Sha256))
        {
            return false;
        }

        JsonInputError error = JsonInputSafeguards.TryParseObject(profile.Json, 262_144, out JsonDocument? document);
        document?.Dispose();
        return error == JsonInputError.None
            && Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(profile.Json))) == profile.Sha256;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
            DefaultTimeout = 5,
        }.ConnectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static string? ReadSetting(SqliteConnection connection, SqliteTransaction? transaction, string key)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ValueJson FROM Settings WHERE SettingKey=$key;";
        Add(command, "$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void WriteSetting(SqliteConnection connection, SqliteTransaction transaction, string key, string json, DateTimeOffset timestamp, bool replace)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO Settings(SettingKey,ValueJson,UpdatedAtUtc) VALUES($key,$json,$time)"
            + (replace ? " ON CONFLICT(SettingKey) DO UPDATE SET ValueJson=excluded.ValueJson,UpdatedAtUtc=excluded.UpdatedAtUtc;" : ";");
        Add(command, "$key", key);
        Add(command, "$json", json);
        Add(command, "$time", timestamp.ToString("O", CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
    }

    private static bool Identity(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');

    private static string CurrentKey(string marketplace) => $"requirements:{marketplace}:current";
    private static string ApprovalKey(string marketplace, string version) => $"requirements:{marketplace}:approval:{version}";
    private static void Add(SqliteCommand command, string key, string value) => _ = command.Parameters.AddWithValue(key, value);

    // SQLite's provider executes locally and synchronously; cancellation is checked before work and before commit.
    private static Task<RepositoryOperationResult> Run(Func<RepositoryOperationResult> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        { return Task.FromResult(operation()); }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException or JsonException)
        { return Task.FromResult(Failure("STORAGE_OR_DATA_INVALID")); }
    }

    private static Task<RepositoryOperationResult<T>> RunValue<T>(Func<RepositoryOperationResult<T>> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        { return Task.FromResult(operation()); }
        catch (Exception exception) when (exception is SqliteException or InvalidDataException or JsonException)
        { return Task.FromResult(Failure<T>("STORAGE_OR_DATA_INVALID")); }
    }

    private static RepositoryOperationResult Failure(string code) => RepositoryOperationResult.Failure($"PROFILE_{code}", "Requirements profile operation could not be completed safely.");
    private static RepositoryOperationResult<T> Failure<T>(string code) => RepositoryOperationResult.Failure<T>($"PROFILE_{code}", "Requirements profile operation could not be completed safely.");
}
