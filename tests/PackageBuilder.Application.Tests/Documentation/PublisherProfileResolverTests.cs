using PackageBuilder.Application.Documentation;
using PackageBuilder.Contracts.Configuration;
using PackageBuilder.Domain.Profiles;

namespace PackageBuilder.Application.Tests.Documentation;

public sealed class PublisherProfileResolverTests
{
    private static readonly PublisherProfileFile[] _catalog = [new("ExampleStudio", "profiles/publishers/example.json")];

    [Fact]
    public void ResolvesExplicitAndDefaultProfilesIncludingBrandingAndDisclosure()
    {
        var io = new ProfileIo();
        var resolver = new PublisherProfileResolver(io, io);
        DocumentationResult<PublisherProfile> explicitResult = resolver.Resolve("ExampleStudio", "OtherStudio", _catalog);
        DocumentationResult<PublisherProfile> defaultResult = resolver.Resolve(null, "ExampleStudio", _catalog);
        Assert.True(explicitResult.IsSuccess, explicitResult.Error);
        Assert.Equal(explicitResult.Value, defaultResult.Value);
        Assert.Equal("ExampleStudio", defaultResult.Value!.Root.Value);
        Assert.Equal("Élan Studio 🪶", defaultResult.Value.DisplayName.Value);
        Assert.Equal("branding/logo.png", Assert.Single(defaultResult.Value.Branding!.Images).Source.LogicalReference);
        Assert.Equal("Concept only.", defaultResult.Value.AiDisclosure.Text);
        Assert.EndsWith("profiles\\publishers\\example.json", io.LastReadPath);
        Assert.DoesNotContain(".json", io.LastInspectedPath);
    }

    [Theory]
    [InlineData("OtherStudio", "PUBLISHER_NOT_FOUND")]
    [InlineData("examplestudio", "PUBLISHER_NOT_FOUND")]
    [InlineData("../ExampleStudio", "PUBLISHER_REFERENCE_INVALID")]
    [InlineData("", "PUBLISHER_REFERENCE_INVALID")]
    public void ExplicitSelectionNeverFallsBack(string reference, string error)
    {
        var io = new ProfileIo();
        Assert.Equal(error, new PublisherProfileResolver(io, io).Resolve(reference, "ExampleStudio", _catalog).Error);
        Assert.Null(io.LastReadPath);
    }

    [Theory]
    [InlineData("../profile.json")]
    [InlineData("/profile.json")]
    [InlineData("C:/profile.json")]
    [InlineData("profiles/link:stream.json")]
    [InlineData("profiles/CON.json")]
    [InlineData("profiles/COM¹.json")]
    public void RejectsUnsafeConfigurationBeforeIo(string path)
    {
        var io = new ProfileIo();
        Assert.Equal("PUBLISHER_CATALOG_INVALID", new PublisherProfileResolver(io, io).Resolve("ExampleStudio", null, [new("ExampleStudio", path)]).Error);
        Assert.Null(io.LastReadPath);
    }

    [Fact]
    public void FailsClosedForLinksMissingFilesInvalidJsonAndMismatchedIdentity()
    {
        var io = new ProfileIo { RejectPath = true };
        var resolver = new PublisherProfileResolver(io, io);
        Assert.Equal("PUBLISHER_PATH_UNSAFE", resolver.Resolve(null, "ExampleStudio", _catalog).Error);
        Assert.Null(io.LastReadPath);
        io.RejectPath = false;
        io.Json = null;
        Assert.Equal("PUBLISHER_READ_FAILED", resolver.Resolve(null, "ExampleStudio", _catalog).Error);
        io.Json = "{}";
        Assert.Equal("PUBLISHER_PROFILE_INVALID", resolver.Resolve(null, "ExampleStudio", _catalog).Error);
        io.Json = DocumentationTests.ProfileJson.Replace("ExampleStudio", "OtherStudio", StringComparison.Ordinal);
        Assert.Equal("PUBLISHER_IDENTITY_MISMATCH", resolver.Resolve(null, "ExampleStudio", _catalog).Error);
        Assert.Equal("PUBLISHER_CATALOG_INVALID", resolver.Resolve(null, "ExampleStudio", [.. _catalog, .. _catalog]).Error);
    }

    private sealed class ProfileIo : IConfigurationTextReader, IReparsePointInspector
    {
        public string? Json { get; set; } = DocumentationTests.ProfileJson;
        public bool RejectPath { get; set; }
        public string? LastReadPath { get; private set; }
        public string? LastInspectedPath { get; private set; }
        public ConfigurationReadResult Read(string configurationFilePath, int maximumBytes)
        {
            LastReadPath = configurationFilePath;
            Assert.True(maximumBytes > 0);
            return Json is null ? ConfigurationReadResult.Failed(new("CONFIG_FILE_MISSING", "$", "Missing.")) : ConfigurationReadResult.Success(Json);
        }

        public ConfigurationFailure? Inspect(string projectRoot, string configuredRoot, string logicalProperty)
        {
            LastInspectedPath = configuredRoot;
            return RejectPath ? new("PATH_REPARSE_POINT", logicalProperty, "Rejected.") : null;
        }
    }
}
