using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Targets.Portable.Tests;

public sealed class PortableTargetValidatorTests
{
    [Fact]
    public async Task CompletePackagePassesWithoutFindings()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [scenario.TextureReceipt],
                scenario.Readme,
                scenario.References(),
                scenario.Reimports(),
                TestContext.Current.CancellationToken);

            Assert.True(report.Passed);
            Assert.Empty(report.Findings);
            Assert.Equal(0, archive.Position);
            _ = Assert.Throws<NotSupportedException>(
                () => ((IList<PackageBuilder.Domain.Validation.ValidationFinding>)report.Findings).Clear());
        }
    }

    [Theory]
    [InlineData("naming", "PORTABLE_NAMING_INVALID")]
    [InlineData("archive", "PORTABLE_ARCHIVE_INVALID")]
    [InlineData("textures", "PORTABLE_TEXTURES_INVALID")]
    [InlineData("readme", "PORTABLE_README_INVALID")]
    [InlineData("references", "PORTABLE_REFERENCES_INVALID")]
    [InlineData("reimport", "PORTABLE_REIMPORT_FAILED")]
    public async Task EachRequiredValidationAreaProducesBlockingFailure(string area, string expectedCode)
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                area == "naming" ? PortableTestValues.Naming("Other", "Other_Folder") : scenario.Naming,
                receipt,
                area == "archive" ? new MemoryStream([1, 2, 3]) : archive,
                area == "textures" ? [] : [scenario.TextureReceipt],
                area == "readme" ? null : scenario.Readme,
                area == "references" ? [] : scenario.References(),
                area == "reimport" ? [new PortableReimportEvidence(scenario.Naming.FbxFileName, false)] : scenario.Reimports(),
                TestContext.Current.CancellationToken);

            Assert.False(report.Passed);
            Assert.Contains(report.Findings, finding => finding.Code.Value == expectedCode && finding.BlocksRelease);
        }
    }

    [Fact]
    public async Task MissingCorePlanFailsClosedWithoutReadingOtherInputs()
    {
        PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.False(report.Passed);
        _ = Assert.Single(report.Findings);
        Assert.Equal("PORTABLE_NAMING_INVALID", report.Findings[0].Code.Value);
    }

    [Fact]
    public async Task MissingCollectionsAndArchiveEvidenceAllFailClosed()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
            scenario.Layout,
            scenario.Naming,
            null,
            null,
            null,
            scenario.Readme,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.False(report.Passed);
        Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_NAMING_INVALID");
        Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_ARCHIVE_INVALID");
        Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_TEXTURES_INVALID");
        Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_REFERENCES_INVALID");
        Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_REIMPORT_FAILED");
    }

    [Fact]
    public void ReferenceEvidenceIsValidatedSortedAndImmutable()
    {
        var evidence = new PortableAssetReferenceEvidence("Model.fbx", ["B.png", "A.png"]);
        Assert.Equal(["A.png", "B.png"], evidence.References);
        _ = Assert.Throws<NotSupportedException>(() => ((IList<string>)evidence.References).Add("C.png"));
        _ = Assert.Throws<ArgumentException>(() => new PortableAssetReferenceEvidence(" ", []));
        _ = Assert.Throws<ArgumentNullException>(() => new PortableAssetReferenceEvidence("Model.fbx", null!));
        _ = Assert.Throws<ArgumentException>(() => new PortableAssetReferenceEvidence("Model.fbx", [""]));
    }

    [Fact]
    public async Task PackageWithoutOptionalGlbStillPasses()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync(includeGlb: false);
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        {
            PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [scenario.TextureReceipt],
                scenario.Readme,
                scenario.References(),
                scenario.Reimports(),
                TestContext.Current.CancellationToken);
            Assert.True(report.Passed);
        }
    }

    [Fact]
    public async Task AlteredArchiveMetadataAndReadmeAreBlocking()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        (MemoryStream original, PortableFbxArchiveReceipt originalReceipt) = await scenario.BuildAsync();
        using (original)
        using (var altered = new MemoryStream())
        {
            original.Position = 0;
            using (var source = new ZipArchive(original, ZipArchiveMode.Read, leaveOpen: true))
            using (var destination = new ZipArchive(altered, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (ZipArchiveEntry sourceEntry in source.Entries.Reverse())
                {
                    ZipArchiveEntry destinationEntry = destination.CreateEntry(sourceEntry.FullName, CompressionLevel.NoCompression);
                    destinationEntry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using Stream input = sourceEntry.Open();
                    using Stream output = destinationEntry.Open();
                    await input.CopyToAsync(output, TestContext.Current.CancellationToken);
                }
            }

            byte[] alteredBytes = altered.ToArray();
            ArtifactContentIdentity identity = ArtifactContentIdentity.Create(
                alteredBytes.LongLength,
                Sha256Digest.Create(Convert.ToHexString(SHA256.HashData(alteredBytes)).ToLowerInvariant()).Value).Value!;
            PortableFbxArchiveReceipt alteredReceipt = CreateReceipt(originalReceipt, identity);
            PortablePackageTestScenario other = await PortablePackageTestScenario.CreateAsync(includeGlb: false);
            altered.Position = 0;

            PortableTargetValidationReport report = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                alteredReceipt,
                altered,
                [scenario.TextureReceipt],
                other.Readme,
                scenario.References(),
                scenario.Reimports(),
                TestContext.Current.CancellationToken);

            Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_ARCHIVE_INVALID");
            Assert.Contains(report.Findings, finding => finding.Code.Value == "PORTABLE_README_INVALID");
        }
    }

    [Fact]
    public async Task CancellationAndIoFailureBecomeBlockingArchiveFindings()
    {
        PortablePackageTestScenario scenario = await PortablePackageTestScenario.CreateAsync();
        (MemoryStream archive, PortableFbxArchiveReceipt receipt) = await scenario.BuildAsync();
        using (archive)
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            PortableTargetValidationReport cancelled = await PortableTargetValidator.ValidateAsync(
                scenario.Layout,
                scenario.Naming,
                receipt,
                archive,
                [scenario.TextureReceipt],
                scenario.Readme,
                scenario.References(),
                scenario.Reimports(),
                cancellation.Token);
            Assert.Contains(cancelled.Findings, finding => finding.Code.Value == "PORTABLE_ARCHIVE_INVALID");
        }

        using var failing = new ThrowingArchiveStream(receipt.ArchiveIdentity.Bytes);
        PortableTargetValidationReport failed = await PortableTargetValidator.ValidateAsync(
            scenario.Layout,
            scenario.Naming,
            receipt,
            failing,
            [scenario.TextureReceipt],
            scenario.Readme,
            scenario.References(),
            scenario.Reimports(),
            TestContext.Current.CancellationToken);
        Assert.Contains(failed.Findings, finding => finding.Code.Value == "PORTABLE_ARCHIVE_INVALID");
    }

    private static PortableFbxArchiveReceipt CreateReceipt(
        PortableFbxArchiveReceipt source,
        ArtifactContentIdentity identity) =>
        (PortableFbxArchiveReceipt)Activator.CreateInstance(
            typeof(PortableFbxArchiveReceipt),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [source.ArchiveFileName, source.Entries, identity, source.LogicalIdentity],
            culture: null)!;

    private sealed class ThrowingArchiveStream(long length) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get; set; }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Synthetic read failure.");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Synthetic read failure."));
        public override long Seek(long offset, SeekOrigin origin) => Position = offset;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
