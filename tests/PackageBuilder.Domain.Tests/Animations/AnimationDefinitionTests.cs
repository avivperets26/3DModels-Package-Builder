using System.Globalization;
using System.Reflection;
using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Rigging;
using PackageBuilder.Domain.Tests.Rigging;

namespace PackageBuilder.Domain.Tests.Animations;

[Trait("Task", "PB-0105")]
public sealed class AnimationDefinitionTests
{
    [Theory]
    [InlineData(null, AnimationDefinitionValidationError.NullName)]
    [InlineData("", AnimationDefinitionValidationError.EmptyName)]
    [InlineData(" ", AnimationDefinitionValidationError.WhitespaceOnlyName)]
    [InlineData(" Clip", AnimationDefinitionValidationError.NameEdgeWhitespace)]
    [InlineData("Clip ", AnimationDefinitionValidationError.NameEdgeWhitespace)]
    [InlineData("Cl\u0000ip", AnimationDefinitionValidationError.NameContainsControlCharacter)]
    public void ClipRejectsInvalidNames(
        string? name,
        AnimationDefinitionValidationError error) =>
        AssertInvalid(error, name: name);

    [Fact]
    public void ClipPreservesUnicodeNameNegativeFramesAndInclusiveSemantics()
    {
        AnimationDefinition clip = CreateValid(
            name: "İ Bow Shot",
            startFrame: -10,
            endFrame: -5,
            framesPerSecond: 2d);

        Assert.Equal("İ Bow Shot", clip.Name);
        Assert.Equal(-10, clip.StartFrame);
        Assert.Equal(-5, clip.EndFrame);
        Assert.Equal(6m, clip.InclusiveFrameCount);
        Assert.Equal(2.5d, clip.DurationSeconds);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(long.MinValue, long.MinValue, 1)]
    [InlineData(long.MaxValue, long.MaxValue, 1)]
    public void FrameRangeAcceptsInclusiveNumericBoundaries(
        long start,
        long end,
        double expectedCount)
    {
        AnimationDefinition clip = CreateValid(startFrame: start, endFrame: end);
        Assert.Equal((decimal)expectedCount, clip.InclusiveFrameCount);
    }

    [Fact]
    public void FrameCountSupportsTheCompleteLongSourceRangeWithoutArbitraryLimit()
    {
        AnimationDefinition clip = CreateValid(
            startFrame: long.MinValue,
            endFrame: long.MaxValue);
        Assert.Equal(18446744073709551616m, clip.InclusiveFrameCount);
    }

    [Fact]
    public void ClipRejectsReversedFrameRange() =>
        AssertInvalid(
            AnimationDefinitionValidationError.ReversedFrameRange,
            startFrame: 1,
            endFrame: 0);

    [Theory]
    [InlineData(0d)]
    [InlineData(-double.Epsilon)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ClipRejectsNonPositiveAndNonFiniteFps(double fps) =>
        AssertInvalid(
            AnimationDefinitionValidationError.InvalidFramesPerSecond,
            framesPerSecond: fps);

    [Fact]
    public void ClipAcceptsEveryFinitePositiveFpsBoundary()
    {
        Assert.Equal(double.MaxValue, CreateValid(framesPerSecond: double.MaxValue).FramesPerSecond);
        Assert.Equal(
            double.Epsilon,
            CreateValid(startFrame: 0, endFrame: 0, framesPerSecond: double.Epsilon)
                .FramesPerSecond);
    }

    [Fact]
    public void ClipRejectsDurationThatCannotBeRepresentedFinitely() =>
        AssertInvalid(
            AnimationDefinitionValidationError.DurationNotFinite,
            startFrame: long.MinValue,
            endFrame: long.MaxValue,
            framesPerSecond: double.Epsilon);

    [Fact]
    public void ClipRequiresLoopRootMotionAndRigMetadata()
    {
        RigDefinition rig = RigTestAssertions.CreateRig();
        RigTestAssertions.AssertFailure(
            AnimationDefinition.Create(
                "Clip",
                0,
                1,
                30d,
                null,
                RootMotionStatus.None,
                null,
                rig),
            AnimationDefinitionValidationError.NullLoopBehavior);
        RigTestAssertions.AssertFailure(
            AnimationDefinition.Create(
                "Clip",
                0,
                1,
                30d,
                LoopBehavior.Once,
                null,
                null,
                rig),
            AnimationDefinitionValidationError.NullRootMotionStatus);
        RigTestAssertions.AssertFailure(
            AnimationDefinition.Create(
                "Clip",
                0,
                1,
                30d,
                LoopBehavior.Once,
                RootMotionStatus.None,
                null,
                null),
            AnimationDefinitionValidationError.NullRig);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryLoopBehaviorIsPreserved(int index) =>
        Assert.Same(
            LoopBehavior.All[index],
            CreateValid(loopBehavior: LoopBehavior.All[index]).LoopBehavior);

    [Fact]
    public void RootMotionMustReferenceTheExactValidatedRoot()
    {
        RigDefinition rig = RigTestAssertions.CreateRig();
        AnimationDefinition none = CreateValid(rig: rig);
        AnimationDefinition root = CreateValid(
            rootMotionStatus: RootMotionStatus.RootBone,
            rootMotionBoneIdentity: rig.Skeleton.Root.Identity,
            rig: rig);

        Assert.Same(RootMotionStatus.None, none.RootMotionStatus);
        Assert.Null(none.RootMotionBoneIdentity);
        Assert.Same(RootMotionStatus.RootBone, root.RootMotionStatus);
        Assert.Equal("Root", root.RootMotionBoneIdentity);

        AssertInvalid(
            AnimationDefinitionValidationError.UnexpectedRootMotionBone,
            rootMotionBoneIdentity: "Root",
            rig: rig);
        AssertInvalid(
            AnimationDefinitionValidationError.RootMotionBoneMismatch,
            rootMotionStatus: RootMotionStatus.RootBone,
            rootMotionBoneIdentity: null,
            rig: rig);
        AssertInvalid(
            AnimationDefinitionValidationError.RootMotionBoneMismatch,
            rootMotionStatus: RootMotionStatus.RootBone,
            rootMotionBoneIdentity: "root",
            rig: rig);
    }

    [Fact]
    public void EqualityAndHashingIncludeEveryFieldAndAreCultureIndependent()
    {
        RigDefinition rig = RigTestAssertions.CreateRig();
        AnimationDefinition first = CreateValid(
            name: "İClip",
            startFrame: -1,
            endFrame: 2,
            framesPerSecond: 24d,
            loopBehavior: LoopBehavior.Loop,
            rootMotionStatus: RootMotionStatus.RootBone,
            rootMotionBoneIdentity: "Root",
            rig: rig);
        AnimationDefinition same = CreateValid(
            name: "İClip",
            startFrame: -1,
            endFrame: 2,
            framesPerSecond: 24d,
            loopBehavior: LoopBehavior.Loop,
            rootMotionStatus: RootMotionStatus.RootBone,
            rootMotionBoneIdentity: "Root",
            rig: RigTestAssertions.CreateRig());
        CultureInfo previous = CultureInfo.CurrentCulture;
        int hash = first.GetHashCode();
        _ = CreateValid(rig: rig).GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(first.Equals(same));
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals(CreateValid(name: "iClip", rig: rig)));
            Assert.False(first.Equals(CreateValid(startFrame: 0, rig: rig)));
            Assert.False(first.Equals(CreateValid(endFrame: 3, rig: rig)));
            Assert.False(first.Equals(CreateValid(framesPerSecond: 25d, rig: rig)));
            Assert.False(first.Equals(CreateValid(loopBehavior: LoopBehavior.Once, rig: rig)));
            Assert.False(first.Equals(CreateValid(rig: rig)));
            Assert.False(first.Equals(CreateValid(
                rootMotionStatus: RootMotionStatus.RootBone,
                rootMotionBoneIdentity: "Root",
                rig: RigTestAssertions.CreateRig(RigType.Humanoid))));
            Assert.False(first.Equals((AnimationDefinition?)null));
            Assert.False(first.Equals("clip"));
            Assert.Equal(hash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DomainAssemblyRetainsEngineRendererAndInfrastructureIndependence()
    {
        Assembly assembly = typeof(AnimationDefinition).Assembly;
        string[] references =
        [
            .. assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty),
        ];
        string[] forbidden =
        [
            "Blender",
            "Unity",
            "Unreal",
            "WPF",
            "Presentation",
            "PackageBuilder.Targets",
            "PackageBuilder.Marketplaces",
            "PackageBuilder.Infrastructure",
            "System.IO.FileSystem",
            "System.Net",
        ];
        Assert.DoesNotContain(
            references,
            reference => forbidden.Any(
                token => reference.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertInvalid(
        AnimationDefinitionValidationError error,
        string? name = "Clip",
        long startFrame = 0,
        long endFrame = 1,
        double framesPerSecond = 30d,
        LoopBehavior? loopBehavior = null,
        RootMotionStatus? rootMotionStatus = null,
        string? rootMotionBoneIdentity = null,
        RigDefinition? rig = null) =>
        RigTestAssertions.AssertFailure(
            AnimationDefinition.Create(
                name,
                startFrame,
                endFrame,
                framesPerSecond,
                loopBehavior ?? LoopBehavior.Once,
                rootMotionStatus ?? RootMotionStatus.None,
                rootMotionBoneIdentity,
                rig ?? RigTestAssertions.CreateRig()),
            error);

    private static AnimationDefinition CreateValid(
        string name = "Clip",
        long startFrame = 0,
        long endFrame = 1,
        double framesPerSecond = 30d,
        LoopBehavior? loopBehavior = null,
        RootMotionStatus? rootMotionStatus = null,
        string? rootMotionBoneIdentity = null,
        RigDefinition? rig = null) =>
        RigTestAssertions.AssertSuccess(
            AnimationDefinition.Create(
                name,
                startFrame,
                endFrame,
                framesPerSecond,
                loopBehavior ?? LoopBehavior.Once,
                rootMotionStatus ?? RootMotionStatus.None,
                rootMotionBoneIdentity,
                rig ?? RigTestAssertions.CreateRig()));
}
