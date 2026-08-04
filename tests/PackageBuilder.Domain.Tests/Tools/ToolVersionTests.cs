using System.Globalization;
using PackageBuilder.Domain.Tools;

namespace PackageBuilder.Domain.Tests.Tools;

public sealed class ToolVersionTests
{
    [Theory]
    [InlineData(ToolKind.DotNet, "10.0.302", ToolReleaseChannel.Stable)]
    [InlineData(ToolKind.DotNet, "11.0.100-preview.1.26104.13", ToolReleaseChannel.Preview)]
    [InlineData(ToolKind.Blender, "4.5.3", ToolReleaseChannel.Stable)]
    [InlineData(ToolKind.Blender, "5.0.0-beta.2", ToolReleaseChannel.Preview)]
    [InlineData(ToolKind.Unity, "6000.3.5f1", ToolReleaseChannel.Stable)]
    [InlineData(ToolKind.Unity, "6000.4.0b2", ToolReleaseChannel.Preview)]
    [InlineData(ToolKind.Unity, "6000.3.5p1", ToolReleaseChannel.Stable)]
    [InlineData(ToolKind.Unreal, "5.7.1", ToolReleaseChannel.Stable)]
    [InlineData(ToolKind.Unreal, "5.8.0-preview.2", ToolReleaseChannel.Preview)]
    public void CreateRecognizesCanonicalVendorVersions(
        ToolKind tool,
        string text,
        ToolReleaseChannel expectedChannel)
    {
        ToolVersion version = ToolTestAssertions.AssertSuccess(ToolVersion.Create(tool, text));

        Assert.Equal(tool, version.Tool);
        Assert.Equal(text, version.Value);
        Assert.Equal(text, version.ToString());
        Assert.Equal(expectedChannel, version.Channel);
    }

    [Theory]
    [InlineData(ToolKind.DotNet, null, ToolModelValidationError.Null)]
    [InlineData(ToolKind.DotNet, "", ToolModelValidationError.Empty)]
    [InlineData(ToolKind.DotNet, "   ", ToolModelValidationError.WhitespaceOnly)]
    [InlineData(ToolKind.DotNet, " 10.0.302", ToolModelValidationError.LeadingOrTrailingWhitespace)]
    [InlineData((ToolKind)99, "1.0.0", ToolModelValidationError.UndefinedTool)]
    [InlineData(ToolKind.DotNet, "10.0", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.DotNet, "10.00.302", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.DotNet, "10.0.302+build", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.DotNet, "10.0.302-preview.01", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.DotNet, "10.0.302-beta.1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.DotNet, "10.0.302-preview.x", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Blender, "4.5.3-preview.1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Blender, "4.5.3-RC.1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Blender, "4.5.3-rc.x", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unity, "6000.3.5", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unity, "6000.3.5F1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unity, "6000.03.5f1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unity, "6000.3.5f01", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unreal, "5.8.0-preview", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unreal, "5.8.0-alpha.1", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unreal, "5.8.0-preview.x", ToolModelValidationError.InvalidVersion)]
    [InlineData(ToolKind.Unreal, "999999999999.0.0", ToolModelValidationError.InvalidVersion)]
    public void CreateRejectsMalformedOrContradictoryVersions(
        ToolKind tool,
        string? text,
        ToolModelValidationError expected) =>
        ToolTestAssertions.AssertFailure(ToolVersion.Create(tool, text), expected);

    [Fact]
    public void ComparisonUsesNumericVendorPrecedenceInsteadOfTextSorting()
    {
        Assert.True(ToolTestAssertions.Version(ToolKind.DotNet, "10.0.100")
            .CompareTo(ToolTestAssertions.Version(ToolKind.DotNet, "9.0.999")) > 0);
        Assert.True(ToolTestAssertions.Version(ToolKind.Blender, "4.10.0")
            .CompareTo(ToolTestAssertions.Version(ToolKind.Blender, "4.9.99")) > 0);
        Assert.True(ToolTestAssertions.Version(ToolKind.Unity, "6000.10.0f1")
            .CompareTo(ToolTestAssertions.Version(ToolKind.Unity, "6000.9.99f9")) > 0);
        Assert.True(ToolTestAssertions.Version(ToolKind.Unreal, "5.10.0")
            .CompareTo(ToolTestAssertions.Version(ToolKind.Unreal, "5.9.99")) > 0);
    }

    [Fact]
    public void PrereleaseAndUnityStageOrderingAreDeterministic()
    {
        ToolVersion blenderAlpha = ToolTestAssertions.Version(ToolKind.Blender, "5.0.0-alpha.2");
        ToolVersion blenderBeta = ToolTestAssertions.Version(ToolKind.Blender, "5.0.0-beta.1");
        ToolVersion blenderRc = ToolTestAssertions.Version(ToolKind.Blender, "5.0.0-rc.1");
        ToolVersion blenderStable = ToolTestAssertions.Version(ToolKind.Blender, "5.0.0");
        Assert.True(blenderAlpha.CompareTo(blenderBeta) < 0);
        Assert.True(blenderBeta.CompareTo(blenderRc) < 0);
        Assert.True(blenderRc.CompareTo(blenderStable) < 0);
        Assert.True(blenderStable.CompareTo(blenderRc) > 0);

        ToolVersion unityAlpha = ToolTestAssertions.Version(ToolKind.Unity, "6000.4.0a9");
        ToolVersion unityBeta = ToolTestAssertions.Version(ToolKind.Unity, "6000.4.0b1");
        ToolVersion unityFinal = ToolTestAssertions.Version(ToolKind.Unity, "6000.4.0f1");
        ToolVersion unityPatch = ToolTestAssertions.Version(ToolKind.Unity, "6000.4.0p1");
        Assert.True(unityAlpha.CompareTo(unityBeta) < 0);
        Assert.True(unityBeta.CompareTo(unityFinal) < 0);
        Assert.True(unityFinal.CompareTo(unityPatch) < 0);
        Assert.True(unityPatch.CompareTo(ToolTestAssertions.Version(ToolKind.Unity, "6000.4.0p2")) < 0);
    }

    [Fact]
    public void SemanticIdentifiersFollowNumericAndLengthRules()
    {
        ToolVersion previewTwo = ToolTestAssertions.Version(ToolKind.DotNet, "11.0.100-preview.2");
        ToolVersion previewTen = ToolTestAssertions.Version(ToolKind.DotNet, "11.0.100-preview.10");
        ToolVersion previewDetailed = ToolTestAssertions.Version(ToolKind.DotNet, "11.0.100-preview.10.1");

        Assert.True(previewTwo.CompareTo(previewTen) < 0);
        Assert.True(previewTen.CompareTo(previewDetailed) < 0);
        Assert.Equal(
            0,
            ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302")
                .CompareTo(ToolTestAssertions.Version(ToolKind.DotNet, "10.0.302")));
    }

    [Fact]
    public void ComparisonOperatorsFollowSemanticOrderingAndNullRules()
    {
        ToolVersion earlier = ToolTestAssertions.Version(ToolKind.DotNet, "10.0.100");
        ToolVersion same = ToolTestAssertions.Version(ToolKind.DotNet, "10.0.100");
        ToolVersion later = ToolTestAssertions.Version(ToolKind.DotNet, "10.0.101");

        Assert.True(earlier == same);
        Assert.False(earlier != same);
        Assert.True(earlier < later);
        Assert.True(later > earlier);
        Assert.True(earlier <= same);
        Assert.True(later >= same);
        bool equalsNull = earlier == null;
        bool differsFromNull = earlier != null;
        Assert.False(equalsNull);
        Assert.True(differsFromNull);
        Assert.True(null < earlier);
        Assert.True(null <= earlier);
        Assert.True(earlier > null);
        Assert.True(earlier >= null);
    }

    [Fact]
    public void EqualityHashingAndOrderingAreCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            ToolVersion first = ToolTestAssertions.Version(ToolKind.Unreal, "5.8.0-rc.2");
            ToolVersion same = ToolTestAssertions.Version(ToolKind.Unreal, "5.8.0-rc.2");
            ToolVersion different = ToolTestAssertions.Version(ToolKind.Unreal, "5.8.0-rc.3");

            Assert.True(first.Equals(same));
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals(different));
            Assert.False(first.Equals((ToolVersion?)null));
            Assert.False(first.Equals("5.8.0-rc.2"));
            Assert.Equal(first.GetHashCode(), same.GetHashCode());
            Assert.Equal(0, first.CompareTo(same));
            Assert.True(first.CompareTo(different) < 0);
            Assert.True(first.CompareTo(null) > 0);
            Assert.True(first.CompareTo(ToolTestAssertions.Version(ToolKind.Blender, "5.8.0-rc.2")) > 0);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
