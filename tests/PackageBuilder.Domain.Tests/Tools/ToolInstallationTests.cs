using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Domain.Tests.Tools;

public sealed class ToolInstallationTests
{
    private const string ToolsRoot = @"C:\Dev\PackageBuilder\tools";

    [Theory]
    [InlineData(ToolKind.DotNet, "10.0.302", @"C:\Dev\PackageBuilder\tools\dotnet\10.0.302\dotnet.exe")]
    [InlineData(ToolKind.Blender, "4.5.3", @"C:\Dev\PackageBuilder\tools\blender\4.5.3\blender.exe")]
    [InlineData(ToolKind.Unity, "6000.3.5f1", @"C:\Dev\PackageBuilder\tools\unity\6000.3.5f1\Editor\Unity.exe")]
    [InlineData(ToolKind.Unreal, "5.7.1", @"C:\Dev\PackageBuilder\tools\unreal\5.7.1\Engine\Binaries\Win64\UnrealEditor.exe")]
    public void ContainedInstallationsCanBeSelected(
        ToolKind tool,
        string versionText,
        string executablePath)
    {
        ToolInstallation installation = ToolTestAssertions.AssertSuccess(
            ToolInstallation.Create(
                tool,
                ToolTestAssertions.Version(tool, versionText),
                executablePath,
                ToolsRoot,
                isSelected: true));

        Assert.Equal(tool, installation.Tool);
        Assert.Equal(versionText, installation.Version.Value);
        Assert.Equal(executablePath, installation.ExecutablePath);
        Assert.Equal(ToolsRoot, installation.ApprovedToolsRoot);
        Assert.Equal(ToolInstallationContainment.Contained, installation.Containment);
        Assert.True(installation.CanBeSelected);
        Assert.True(installation.IsSelected);
    }

    [Fact]
    public void ExternalInstallationIsInformationalAndCannotBeSelected()
    {
        ToolInstallation external = ToolTestAssertions.AssertSuccess(
            ToolInstallation.Create(
                ToolKind.Blender,
                ToolTestAssertions.Version(ToolKind.Blender, "4.5.3"),
                @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe",
                ToolsRoot,
                isSelected: false));

        Assert.Equal(ToolInstallationContainment.External, external.Containment);
        Assert.False(external.CanBeSelected);
        Assert.False(external.IsSelected);
        ToolTestAssertions.AssertFailure(external.Select(), ToolModelValidationError.SelectionOutsideToolsRoot);
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(
                ToolKind.Blender,
                external.Version,
                external.ExecutablePath,
                ToolsRoot,
                isSelected: true),
            ToolModelValidationError.SelectionOutsideToolsRoot);
    }

    [Fact]
    public void SelectionProducesImmutableCopies()
    {
        ToolInstallation initial = ToolTestAssertions.AssertSuccess(
            ToolInstallation.Create(
                ToolKind.DotNet,
                ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302"),
                @"C:\Dev\PackageBuilder\tools\dotnet\10.0.302\dotnet.exe",
                ToolsRoot + "\\",
                isSelected: false));
        ToolInstallation selected = ToolTestAssertions.AssertSuccess(initial.Select());
        ToolInstallation deselected = selected.Deselect();

        Assert.False(initial.IsSelected);
        Assert.True(selected.IsSelected);
        Assert.False(deselected.IsSelected);
        Assert.Equal(ToolsRoot, initial.ApprovedToolsRoot);
        Assert.Equal(initial, deselected);
        Assert.NotEqual(initial, selected);
    }

    [Theory]
    [InlineData(null, ToolModelValidationError.Null)]
    [InlineData("", ToolModelValidationError.Empty)]
    [InlineData("   ", ToolModelValidationError.WhitespaceOnly)]
    [InlineData(" C:\\Dev\\PackageBuilder\\tools\\dotnet.exe", ToolModelValidationError.LeadingOrTrailingWhitespace)]
    [InlineData("dotnet.exe", ToolModelValidationError.PathNotRooted)]
    [InlineData("C:/Dev/PackageBuilder/tools/dotnet.exe", ToolModelValidationError.PathNotRooted)]
    [InlineData("C:\\Dev\\PackageBuilder\\tools\\dotnet\\..\\dotnet.exe", ToolModelValidationError.PathNotCanonical)]
    [InlineData("C:\\Dev\\PackageBuilder\\tools\\dotnet\\", ToolModelValidationError.PathNotCanonical)]
    public void CreateRejectsMalformedExecutablePaths(string? path, ToolModelValidationError expected) =>
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(
                ToolKind.DotNet,
                ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302"),
                path,
                ToolsRoot,
                isSelected: false),
            expected);

    [Fact]
    public void CreateRejectsPathCharactersThatWindowsCannotCanonicalize()
    {
        string pathWithNull = "C:\\Dev\\PackageBuilder\\tools\\dotnet\0.exe";

        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(
                ToolKind.DotNet,
                ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302"),
                pathWithNull,
                ToolsRoot,
                isSelected: false),
            ToolModelValidationError.PathNotCanonical);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\Dev\\PackageBuilder\\runtime-data")]
    [InlineData("tools")]
    public void CreateRejectsInvalidApprovedToolsRoot(string? root)
    {
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(
                ToolKind.DotNet,
                ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302"),
                @"C:\Dev\PackageBuilder\tools\dotnet.exe",
                root,
                isSelected: false),
            ToolModelValidationError.InvalidToolsRoot);
    }

    [Fact]
    public void CreateRejectsUndefinedToolMissingVersionAndToolMismatch()
    {
        ToolVersion dotnet = ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302");
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create((ToolKind)99, dotnet, @"C:\Dev\PackageBuilder\tools\dotnet.exe", ToolsRoot, false),
            ToolModelValidationError.UndefinedTool);
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(ToolKind.DotNet, null, @"C:\Dev\PackageBuilder\tools\dotnet.exe", ToolsRoot, false),
            ToolModelValidationError.MissingVersion);
        ToolTestAssertions.AssertFailure(
            ToolInstallation.Create(ToolKind.Blender, dotnet, @"C:\Dev\PackageBuilder\tools\blender.exe", ToolsRoot, false),
            ToolModelValidationError.VersionToolMismatch);
    }

    [Fact]
    public void EqualityAndHashingUseOrdinalIgnoreCaseWindowsPaths()
    {
        ToolVersion version = ToolTestAssertions.Version(ToolKind.Blender, "4.5.3");
        ToolInstallation first = ToolTestAssertions.AssertSuccess(
            ToolInstallation.Create(
                ToolKind.Blender,
                version,
                @"C:\Dev\PackageBuilder\tools\Blender\blender.exe",
                ToolsRoot,
                false));
        ToolInstallation same = ToolTestAssertions.AssertSuccess(
            ToolInstallation.Create(
                ToolKind.Blender,
                ToolTestAssertions.Version(ToolKind.Blender, "4.5.3"),
                @"c:\dev\packagebuilder\tools\blender\BLENDER.exe",
                @"c:\dev\packagebuilder\tools",
                false));

        Assert.True(first.Equals(same));
        Assert.True(first.Equals((object)same));
        Assert.False(first.Equals((ToolInstallation?)null));
        Assert.False(first.Equals("Blender"));
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
    }
}
