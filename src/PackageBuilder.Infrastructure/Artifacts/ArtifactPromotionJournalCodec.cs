using System.Text.Json;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Infrastructure.Artifacts;

/// <summary>Persists the minimal recovery intent needed to finish one interrupted promotion.</summary>
internal sealed record ArtifactPromotionJournal(
    string JobId,
    string ArtifactId,
    string ReleaseRelativePath,
    int CollisionVersion,
    long Bytes,
    string Sha256);

/// <summary>Reads and writes strict version-one promotion journals.</summary>
internal static class ArtifactPromotionJournalCodec
{
    private static readonly HashSet<string> _properties =
    [
        "schemaVersion",
        "jobId",
        "artifactId",
        "releaseRelativePath",
        "collisionVersion",
        "bytes",
        "sha256",
    ];

    public static byte[] Write(ArtifactPromotionJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("jobId", journal.JobId);
            writer.WriteString("artifactId", journal.ArtifactId);
            writer.WriteString("releaseRelativePath", journal.ReleaseRelativePath);
            writer.WriteNumber("collisionVersion", journal.CollisionVersion);
            writer.WriteNumber("bytes", journal.Bytes);
            writer.WriteString("sha256", journal.Sha256);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public static bool TryRead(ReadOnlyMemory<byte> json, out ArtifactPromotionJournal? journal)
    {
        journal = null;
        try
        {
            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!_properties.Contains(property.Name) || !seen.Add(property.Name))
                {
                    return false;
                }
            }

            if (seen.Count != _properties.Count ||
                !TryInt32(document.RootElement, "schemaVersion", out int schemaVersion) ||
                schemaVersion != 1 ||
                !TryString(document.RootElement, "jobId", out string? jobId) ||
                !TryString(document.RootElement, "artifactId", out string? artifactId) ||
                !TryString(document.RootElement, "releaseRelativePath", out string? releaseRelativePath) ||
                !TryInt32(document.RootElement, "collisionVersion", out int collisionVersion) ||
                !TryInt64(document.RootElement, "bytes", out long bytes) ||
                !TryString(document.RootElement, "sha256", out string? sha256))
            {
                return false;
            }

            journal = new ArtifactPromotionJournal(
                jobId!,
                artifactId!,
                releaseRelativePath!,
                collisionVersion,
                bytes,
                sha256!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(JsonElement root, string name, out string? value)
    {
        JsonElement property = root.GetProperty(name);
        value = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        return value is not null;
    }

    private static bool TryInt32(JsonElement root, string name, out int value)
    {
        value = default;
        JsonElement property = root.GetProperty(name);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }

    private static bool TryInt64(JsonElement root, string name, out long value)
    {
        value = default;
        JsonElement property = root.GetProperty(name);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value);
    }
}
