using System.Globalization;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Identifiers;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Tests.Identifiers;

namespace PackageBuilder.Domain.Tests.Rigging;

[Trait("Task", "PB-0105")]
public sealed class RigIdentityTests
{
    [Fact]
    public void IdentitiesExposeStableOrderAndDisplayNames()
    {
        Assert.Equal(["generic", "humanoid"], RigType.All.Select(value => value.CanonicalIdentifier));
        Assert.Equal(["Generic", "Humanoid"], RigType.All.Select(value => value.DisplayName));
        Assert.Equal(["once", "loop"], LoopBehavior.All.Select(value => value.CanonicalIdentifier));
        Assert.Equal(["Once", "Loop"], LoopBehavior.All.Select(value => value.DisplayName));
        Assert.Equal(
            ["none", "root-bone"],
            RootMotionStatus.All.Select(value => value.CanonicalIdentifier));
        Assert.Equal(
            ["None", "Root Bone"],
            RootMotionStatus.All.Select(value => value.DisplayName));
    }

    [Fact]
    public void IdentityRegistriesAreImmutable()
    {
        AssertImmutable(RigType.All, RigType.Generic);
        AssertImmutable(LoopBehavior.All, LoopBehavior.Once);
        AssertImmutable(RootMotionStatus.All, RootMotionStatus.None);
    }

    [Fact]
    public void EveryApprovedIdentityParsesExactly()
    {
        foreach (RigType value in RigType.All)
        {
            RigType parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                RigType.TryParse(value.CanonicalIdentifier));
            Assert.Same(value, parsed);
            Assert.Equal(value.CanonicalIdentifier, value.ToString());
        }

        foreach (LoopBehavior value in LoopBehavior.All)
        {
            LoopBehavior parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                LoopBehavior.TryParse(value.CanonicalIdentifier));
            Assert.Same(value, parsed);
            Assert.Equal(value.CanonicalIdentifier, value.ToString());
        }

        foreach (RootMotionStatus value in RootMotionStatus.All)
        {
            RootMotionStatus parsed = CanonicalIdentifierTestAssertions.AssertSuccess(
                RootMotionStatus.TryParse(value.CanonicalIdentifier));
            Assert.Same(value, parsed);
            Assert.Equal(value.CanonicalIdentifier, value.ToString());
        }
    }

    [Theory]
    [InlineData(null, CanonicalIdentifierParseError.Null)]
    [InlineData("", CanonicalIdentifierParseError.Empty)]
    [InlineData(" ", CanonicalIdentifierParseError.WhitespaceOnly)]
    [InlineData("Generic", CanonicalIdentifierParseError.Malformed)]
    [InlineData("custom", CanonicalIdentifierParseError.Unknown)]
    public void IdentitiesRejectNonCanonicalInput(
        string? input,
        CanonicalIdentifierParseError error)
    {
        CanonicalIdentifierTestAssertions.AssertFailure(RigType.TryParse(input), error);
        CanonicalIdentifierTestAssertions.AssertFailure(LoopBehavior.TryParse(input), error);
        CanonicalIdentifierTestAssertions.AssertFailure(RootMotionStatus.TryParse(input), error);
    }

    [Fact]
    public void IdentityEqualityAndHashingAreOrdinalAndCultureIndependent()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        int rigHash = RigType.Generic.GetHashCode();
        int loopHash = LoopBehavior.Loop.GetHashCode();
        int rootHash = RootMotionStatus.RootBone.GetHashCode();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            RigType rig = CanonicalIdentifierTestAssertions.AssertSuccess(
                RigType.TryParse("generic"));
            LoopBehavior loop = CanonicalIdentifierTestAssertions.AssertSuccess(
                LoopBehavior.TryParse("loop"));
            RootMotionStatus root = CanonicalIdentifierTestAssertions.AssertSuccess(
                RootMotionStatus.TryParse("root-bone"));

            AssertIdentityEquality(RigType.Generic, rig, RigType.Humanoid, rigHash);
            AssertIdentityEquality(LoopBehavior.Loop, loop, LoopBehavior.Once, loopHash);
            AssertIdentityEquality(RootMotionStatus.RootBone, root, RootMotionStatus.None, rootHash);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static void AssertIdentityEquality<T>(T value, T same, T different, int hash)
        where T : class, IEquatable<T>
    {
        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.False(value.Equals(different));
        Assert.False(value.Equals((T?)null));
        Assert.False(value.Equals("other"));
        Assert.Equal(hash, same.GetHashCode());
    }

    private static void AssertImmutable<T>(IReadOnlyList<T> values, T value)
    {
        IList<T> list = Assert.IsType<IList<T>>(values, exactMatch: false);
        Assert.True(list.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => list.Add(value));
    }
}
