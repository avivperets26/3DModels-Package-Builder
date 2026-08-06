using System.IO.Compression;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;
using PackageBuilder.Domain.Validation;

namespace PackageBuilder.Targets.Portable;

/// <summary>Performs a fail-closed, read-only validation of one complete portable target.</summary>
public static class PortableTargetValidator
{
    private static readonly FindingSourceComponent _source =
        FindingSourceComponent.Create("portable-target-validator").Value!;

    /// <summary>Validates archive bytes, assets, textures, README, names, references, and reimports.</summary>
    public static async Task<PortableTargetValidationReport> ValidateAsync(
        PortableFolderLayout? layout,
        PortableNamingProfile? naming,
        PortableFbxArchiveReceipt? archiveReceipt,
        Stream? archiveStream,
        IEnumerable<PortableTextureCopyReceipt?>? textureReceipts,
        PortableReadmeDocument? readme,
        IEnumerable<PortableAssetReferenceEvidence?>? referenceEvidence,
        IEnumerable<PortableReimportEvidence?>? reimportEvidence,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<ValidationFinding>();
        if (layout is null || naming is null)
        {
            Add(findings, "PORTABLE_NAMING_INVALID", "The portable folder or naming plan is missing.");
            return new PortableTargetValidationReport(findings);
        }

        PortableTextureCopyReceipt?[]? textures = textureReceipts?.ToArray();
        PortableAssetReferenceEvidence?[]? references = referenceEvidence?.ToArray();
        PortableReimportEvidence?[]? reimports = reimportEvidence?.ToArray();

        ValidateNames(layout, naming, archiveReceipt, findings);
        await ValidateArchiveAsync(layout, archiveReceipt, archiveStream, findings, cancellationToken)
            .ConfigureAwait(false);
        ValidateTextures(layout, textures, findings);
        ValidateReadme(layout, archiveReceipt, readme, findings);
        ValidateReferences(layout, naming, textures, references, findings);
        ValidateReimports(layout, naming, reimports, findings);
        return new PortableTargetValidationReport(findings);
    }

    private static void ValidateNames(
        PortableFolderLayout layout,
        PortableNamingProfile naming,
        PortableFbxArchiveReceipt? receipt,
        List<ValidationFinding> findings)
    {
        bool rootsMatch = string.Equals(layout.FlatFbxFolderName, naming.FlatFbxFolderName, StringComparison.Ordinal) &&
            string.Equals(layout.ProductFolderName, naming.ProductFolderName, StringComparison.Ordinal);
        bool fbxMatches = layout.FlatFbxEntries.Count(entry =>
            string.Equals(entry.RelativePath, naming.FbxFileName, StringComparison.Ordinal)) == 1;
        bool archiveMatches = receipt is not null && string.Equals(
            receipt.ArchiveFileName,
            naming.FbxArchiveFileName,
            StringComparison.Ordinal);
        if (!rootsMatch || !fbxMatches || !archiveMatches)
        {
            Add(findings, "PORTABLE_NAMING_INVALID", "Portable roots, filenames, or collision rules do not match the naming plan.");
        }

    }

    private static async Task ValidateArchiveAsync(
        PortableFolderLayout layout,
        PortableFbxArchiveReceipt? receipt,
        Stream? stream,
        List<ValidationFinding> findings,
        CancellationToken cancellationToken)
    {
        if (receipt is null || stream is null || !stream.CanRead || !stream.CanSeek)
        {
            Add(findings, "PORTABLE_ARCHIVE_INVALID", "The FBX archive or its build receipt is missing or unreadable.");
            return;
        }

        try
        {
            if (stream.Length != receipt.ArchiveIdentity.Bytes ||
                !string.Equals(await HashAsync(stream, cancellationToken).ConfigureAwait(false),
                    receipt.ArchiveIdentity.Sha256.Value,
                    StringComparison.Ordinal))
            {
                Add(findings, "PORTABLE_ARCHIVE_INVALID", "The FBX archive content identity does not match its build receipt.");
                return;
            }

            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            string[] expectedNames =
            [
                .. layout.FlatFbxEntries.Select(entry => $"{layout.FlatFbxFolderName}/{entry.RelativePath}"),
            ];
            bool receiptCountMatches = receipt.Entries.Count == layout.FlatFbxEntries.Count;
            bool receiptNamesMatch = receipt.Entries.Select(static entry => entry.RelativePath)
                .SequenceEqual(expectedNames, StringComparer.Ordinal);
            bool receiptEntriesMatch = receipt.Entries.Zip(layout.FlatFbxEntries).All(pair =>
                    pair.First.ContentIdentity.Equals(pair.Second.Source.ContentIdentity) &&
                    pair.First.SourceRecord.Artifact.Id.Equals(pair.Second.Source.Artifact.Id) &&
                    pair.First.TimestampUtc == PortableFbxArchiveBuilder.EntryTimestampUtc);
            bool archiveNamesMatch = archive.Entries.Select(static entry => entry.FullName)
                .SequenceEqual(expectedNames, StringComparer.Ordinal);
            bool archiveTimestampsMatch = archive.Entries.All(entry =>
                entry.LastWriteTime == PortableFbxArchiveBuilder.EntryTimestampUtc);
            if (!receiptCountMatches || !receiptNamesMatch || !receiptEntriesMatch ||
                !archiveNamesMatch || !archiveTimestampsMatch)
            {
                Add(findings, "PORTABLE_ARCHIVE_INVALID", "The FBX archive order, names, or timestamps are not deterministic.");
                return;
            }

        }
        catch (OperationCanceledException)
        {
            Add(findings, "PORTABLE_ARCHIVE_INVALID", "Portable archive validation was cancelled.");
        }
        catch (IOException)
        {
            Add(findings, "PORTABLE_ARCHIVE_INVALID", "The FBX archive could not be read safely.");
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
    }

    private static void ValidateTextures(
        PortableFolderLayout layout,
        PortableTextureCopyReceipt?[]? textures,
        List<ValidationFinding> findings)
    {
        string[] expected =
        [
            .. layout.FlatFbxEntries.Where(static entry =>
                    entry.RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    entry.RelativePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    entry.RelativePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .Select(static entry => entry.RelativePath)
                .Order(StringComparer.Ordinal),
        ];
        string[] actual = textures is null || textures.Any(static texture => texture is null)
            ? []
            : [.. textures.Cast<PortableTextureCopyReceipt>().Select(static texture => texture.FileName).Order(StringComparer.Ordinal)];
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            Add(findings, "PORTABLE_TEXTURES_INVALID", "Portable texture files or receipts do not match the folder manifest.");
        }
    }

    private static void ValidateReadme(
        PortableFolderLayout layout,
        PortableFbxArchiveReceipt? receipt,
        PortableReadmeDocument? readme,
        List<ValidationFinding> findings)
    {
        PortableFolderEntry? entry = layout.FlatFbxEntries.SingleOrDefault(static candidate =>
            candidate.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        if (entry is null || readme is null || receipt is null)
        {
            Add(findings, "PORTABLE_README_INVALID", "The portable README or its manifest entry is missing.");
            return;
        }

        byte[] bytes = [.. readme.Utf8Bytes];
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        bool lengthMatches = entry.Source.ContentIdentity.Bytes == bytes.LongLength;
        bool hashMatches = string.Equals(entry.Source.ContentIdentity.Sha256.Value, hash, StringComparison.Ordinal);
        if (!lengthMatches || !hashMatches)
        {
            Add(findings, "PORTABLE_README_INVALID", "The generated README does not match its manifest content identity.");
        }
    }

    private static void ValidateReferences(
        PortableFolderLayout layout,
        PortableNamingProfile naming,
        PortableTextureCopyReceipt?[]? textures,
        PortableAssetReferenceEvidence?[]? references,
        List<ValidationFinding> findings)
    {
        if (textures is null || textures.Any(static texture => texture is null) ||
            references is null || references.Any(static evidence => evidence is null))
        {
            Add(findings, "PORTABLE_REFERENCES_INVALID", "Portable asset reference evidence is incomplete.");
            return;
        }

        string[] expectedTextures =
        [
            .. textures.Cast<PortableTextureCopyReceipt>().Select(static texture => texture.FileName)
                .Order(StringComparer.Ordinal),
        ];
        PortableAssetReferenceEvidence[] fbxMatches =
        [
            .. references.Cast<PortableAssetReferenceEvidence>()
                .Where(evidence => string.Equals(evidence.AssetFileName, naming.FbxFileName, StringComparison.Ordinal)),
        ];
        if (fbxMatches.Length != 1 || !fbxMatches[0].References.SequenceEqual(expectedTextures, StringComparer.Ordinal))
        {
            Add(findings, "PORTABLE_REFERENCES_INVALID", "FBX texture references do not match the canonical portable texture files.");
        }

        string? glbName = layout.ProductEntries.Select(static entry => entry.RelativePath)
            .SingleOrDefault(static path => path.EndsWith(".glb", StringComparison.Ordinal));
        if (glbName is not null)
        {
            PortableAssetReferenceEvidence[] glbMatches =
            [
                .. references.Cast<PortableAssetReferenceEvidence>()
                    .Where(evidence => string.Equals(evidence.AssetFileName, glbName, StringComparison.Ordinal)),
            ];
            if (glbMatches.Length != 1 || glbMatches[0].References.Count != 0)
            {
                Add(findings, "PORTABLE_REFERENCES_INVALID", "GLB must be present and self-contained without external references.");
            }
        }
    }

    private static void ValidateReimports(
        PortableFolderLayout layout,
        PortableNamingProfile naming,
        PortableReimportEvidence?[]? reimports,
        List<ValidationFinding> findings)
    {
        string[] expected =
        [
            naming.FbxFileName,
            .. layout.ProductEntries.Select(static entry => entry.RelativePath)
                .Where(static path => path.EndsWith(".glb", StringComparison.Ordinal)),
        ];
        if (reimports is null || reimports.Any(static evidence => evidence is null))
        {
            Add(findings, "PORTABLE_REIMPORT_FAILED", "Clean-reimport evidence is incomplete.");
            return;
        }

        PortableReimportEvidence[] values = [.. reimports.Cast<PortableReimportEvidence>()];
        if (expected.Any(name => values.Count(value => string.Equals(value.AssetFileName, name, StringComparison.Ordinal)) != 1) ||
            values.Any(static value => !value.Passed) ||
            values.Any(value => !expected.Contains(value.AssetFileName, StringComparer.Ordinal)))
        {
            Add(findings, "PORTABLE_REIMPORT_FAILED", "FBX or GLB clean-reimport validation did not pass exactly once.");
        }
    }

    private static async Task<string> HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Add(List<ValidationFinding> findings, string code, string explanation)
    {
        ValidationFinding finding = ValidationFinding.Create(
            FindingCode.Create(code).Value,
            FindingSeverity.Error,
            FindingExplanation.Create(explanation).Value,
            _source,
            null,
            CorrectiveAction.Create("Correct the portable package and run validation again.").Value,
            blocksRelease: true).Value!;
        findings.Add(finding);
    }
}
