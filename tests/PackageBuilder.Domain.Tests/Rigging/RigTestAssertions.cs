using PackageBuilder.Domain.Animations;
using PackageBuilder.Domain.Rigging;

namespace PackageBuilder.Domain.Tests.Rigging;

internal static class RigTestAssertions
{
    public static RigTransform AssertSuccess(RigTransformValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(RigTransformValidationError.None, result.Error);
        return Assert.IsType<RigTransform>(result.Value);
    }

    public static BoneDefinition AssertSuccess(BoneDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(BoneDefinitionValidationError.None, result.Error);
        return Assert.IsType<BoneDefinition>(result.Value);
    }

    public static SkeletonDefinition AssertSuccess(SkeletonDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(SkeletonDefinitionValidationError.None, result.Error);
        return Assert.IsType<SkeletonDefinition>(result.Value);
    }

    public static BonePose AssertSuccess(BonePoseValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(BonePoseValidationError.None, result.Error);
        return Assert.IsType<BonePose>(result.Value);
    }

    public static PoseDefinition AssertSuccess(PoseDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(PoseDefinitionValidationError.None, result.Error);
        return Assert.IsType<PoseDefinition>(result.Value);
    }

    public static RigDefinition AssertSuccess(RigDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(RigDefinitionValidationError.None, result.Error);
        return Assert.IsType<RigDefinition>(result.Value);
    }

    public static AnimationDefinition AssertSuccess(AnimationDefinitionValidationResult result)
    {
        Assert.True(result.IsValid);
        Assert.Equal(AnimationDefinitionValidationError.None, result.Error);
        return Assert.IsType<AnimationDefinition>(result.Value);
    }

    public static void AssertFailure(
        RigTransformValidationResult result,
        RigTransformValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        BoneDefinitionValidationResult result,
        BoneDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        SkeletonDefinitionValidationResult result,
        SkeletonDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        BonePoseValidationResult result,
        BonePoseValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        PoseDefinitionValidationResult result,
        PoseDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        RigDefinitionValidationResult result,
        RigDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static void AssertFailure(
        AnimationDefinitionValidationResult result,
        AnimationDefinitionValidationError error)
    {
        Assert.False(result.IsValid);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }

    public static RigTransform CreateTransform(
        double translationX = 0d,
        double translationY = 0d,
        double translationZ = 0d,
        double rotationX = 0d,
        double rotationY = 0d,
        double rotationZ = 0d,
        double rotationW = 1d,
        double scaleX = 1d,
        double scaleY = 1d,
        double scaleZ = 1d) =>
        AssertSuccess(
            RigTransform.Create(
                translationX,
                translationY,
                translationZ,
                rotationX,
                rotationY,
                rotationZ,
                rotationW,
                scaleX,
                scaleY,
                scaleZ));

    public static BoneDefinition CreateBone(string identity, string? parent = null) =>
        AssertSuccess(BoneDefinition.Create(identity, parent));

    public static SkeletonDefinition CreateSkeleton(params BoneDefinition[] bones) =>
        AssertSuccess(
            SkeletonDefinition.Create(
                bones.Length == 0
                ?
                [
                    CreateBone("Root"),
                    CreateBone("Child", "Root"),
                ]
                : bones));

    public static PoseDefinition CreatePose(
        SkeletonDefinition skeleton,
        bool reverse = false,
        RigTransform? rootTransform = null)
    {
        IEnumerable<BoneDefinition> ordered = reverse
            ? skeleton.Bones.Reverse()
            : skeleton.Bones;
        return AssertSuccess(
            PoseDefinition.Create(
                skeleton,
                ordered.Select(
                    bone => AssertSuccess(
                        BonePose.Create(
                            bone.Identity,
                            string.Equals(
                                bone.Identity,
                                skeleton.Root.Identity,
                                StringComparison.Ordinal)
                                ? rootTransform ?? CreateTransform()
                                : CreateTransform())))));
    }

    public static RigDefinition CreateRig(
        RigType? type = null,
        SkeletonDefinition? skeleton = null)
    {
        SkeletonDefinition actualSkeleton = skeleton ?? CreateSkeleton();
        return AssertSuccess(
            RigDefinition.Create(
                type ?? RigType.Generic,
                actualSkeleton,
                CreatePose(actualSkeleton)));
    }
}
