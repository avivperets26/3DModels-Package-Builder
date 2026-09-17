using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Marketplaces.Fab;
using static PackageBuilder.App.Wpf.Tests.FabValidatorFixtures;

namespace PackageBuilder.App.Wpf.Tests;

/// <summary>Maps the owned native delivery harness into Fab evidence only after checking ZIP, extraction and native receipts.</summary>
internal static class FabUnrealLiveObservation
{
    internal static (FabUnrealProjectInspection Inspection, FabReleaseSource Source, string WorkerVersion) Read(
        string repository, string job, FabValidationContext context, string sourceHash)
    {
        job = Path.GetFullPath(job);
        Assert.StartsWith(Path.Combine(repository, "artifacts", "ue") + Path.DirectorySeparatorChar, job, StringComparison.OrdinalIgnoreCase);
        using var proof = JsonDocument.Parse(ReadFile(job, "fab-native-evidence.json"));
        JsonElement native = proof.RootElement;
        Assert.True(native.GetProperty("passed").GetBoolean());
        Assert.True(native.GetProperty("realEngineRun").GetBoolean());
        Assert.Equal(sourceHash, native.GetProperty("sourceSha256").GetString());
        Assert.Equal(Identity(ReadFile(job, "input/Asymmetric.fbx")).Sha256.Value,
            native.GetProperty("normalizedSourceSha256").GetString());
        string[] operations = [.. native.GetProperty("operations").EnumerateArray().Select(o => o.GetString()!)];
        foreach (string operation in new[] { "prepare-unreal-delivery", "render-unreal-previews", "fresh-reopen", "fresh-preview-play", "restored-reopen", "reject-missing-map", "reject-source-path", "reject-extra-descriptor-field" })
        { Assert.Contains(operation, operations); }
        byte[] bytes = ReadFile(job, "delivery.zip");
        ArtifactContentIdentity identity = Identity(bytes);
        Assert.Equal(identity.Sha256.Value, native.GetProperty("archiveSha256").GetString());
        using var inventory = JsonDocument.Parse(ReadFile(job, "output/delivery/inventory.json"));
        JsonElement root = inventory.RootElement;
        string project = root.GetProperty("projectName").GetString()!;
        Assert.Equal("unreal-clean-project-v1", root.GetProperty("profile").GetString());
        var files = new List<FabUnrealFile>();
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Equal(root.GetProperty("files").GetArrayLength(), zip.Entries.Count);
        foreach (JsonElement entry in root.GetProperty("files").EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            Assert.True(UnrealContentDeliveryPolicy.Allows(project, path));
            ArtifactContentIdentity content = Identity(ReadFile(job, "fresh/project/" + path));
            Assert.Equal(entry.GetProperty("sha256").GetString(), content.Sha256.Value);
            Assert.Equal(entry.GetProperty("byteCount").GetInt64(), content.Bytes);
            using Stream stream = zip.GetEntry(project + "/" + path)!.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Assert.Equal(content, Identity(buffer.ToArray()));
            // Native validate_overview rejects redirectors, unused packages and unexpected physical files.
            files.Add(new(path, content, true, false));
        }
        var inspection = new FabUnrealProjectInspection(Id("unreal"), identity, "StoneArch_Unreal.zip", project,
            native.GetProperty("engineVersion").GetString()!, [.. files.Select(file => file.Path)], [.. files],
            Evidence(context, Id("unreal"), identity, "unreal-project"), Evidence(context, Id("unreal"), identity, "unreal-clean-reopen"),
            Evidence(context, Id("unreal"), identity, "unreal-logs"));
        return (inspection, FabReleaseFixtures.Source(context, "unreal", "unreal", inspection.FileName, bytes), native.GetProperty("workerVersion").GetString()!);
    }

    private static byte[] ReadFile(string root, string path)
    {
        Assert.True(DeliveryPath.IsValid(path));
        string full = Path.GetFullPath(path, root);
        Assert.StartsWith(root + Path.DirectorySeparatorChar, full, StringComparison.OrdinalIgnoreCase);
        for (string? parent = full; parent is not null; parent = Path.GetDirectoryName(parent))
        { Assert.Equal((FileAttributes)0, File.GetAttributes(parent) & FileAttributes.ReparsePoint); }
        Assert.InRange(new FileInfo(full).Length, 1, 100_000_000);
        return File.ReadAllBytes(full);
    }
}
