using System.Globalization;
using PackageBuilder.Domain.Rigging;

namespace PackageBuilder.Domain.Tests.Rigging;

[Trait("Task", "PB-0105")]
public sealed class PoseAndRigDefinitionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Bone")]
    [InlineData("Bone ")]
    [InlineData("Bo\u0000ne")]
    public void BonePoseRejectsInvalidBoneIdentity(string? identity) =>
        RigTestAssertions.AssertFailure(
            BonePose.Create(identity, RigTestAssertions.CreateTransform()),
            BonePoseValidationError.InvalidBoneIdentity);

    [Fact]
    public void BonePoseRejectsNullTransform() =>
        RigTestAssertions.AssertFailure(
            BonePose.Create("Root", null),
            BonePoseValidationError.NullTransform);

    [Fact]
    public void PoseIsCompleteCanonicallyOrderedImmutableSnapshot()
    {
        SkeletonDefinition skeleton = RigTestAssertions.CreateSkeleton(
            RigTestAssertions.CreateBone("Root"),
            RigTestAssertions.CreateBone("Zed", "Root"),
            RigTestAssertions.CreateBone("Alpha", "Root"));
        var input = skeleton.Bones
            .Reverse()
            .Select(
                bone => (BonePose?)RigTestAssertions.AssertSuccess(
                    BonePose.Create(bone.Identity, RigTestAssertions.CreateTransform())))
            .ToList();
        PoseDefinition pose = RigTestAssertions.AssertSuccess(
            PoseDefinition.Create(skeleton, input));
        input.Clear();

        Assert.Same(skeleton, pose.Skeleton);
        Assert.Equal(
            skeleton.Bones.Select(bone => bone.Identity),
            pose.Bones.Select(bone => bone.BoneIdentity));
        IList<BonePose> retained = Assert.IsType<IList<BonePose>>(
            pose.Bones,
            exactMatch: false);
        Assert.True(retained.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(
            () => retained.Add(
                RigTestAssertions.AssertSuccess(
                    BonePose.Create("Root", RigTestAssertions.CreateTransform()))));
    }

    [Fact]
    public void PoseRejectsNullUnknownDuplicateAndMissingBones()
    {
        SkeletonDefinition skeleton = RigTestAssertions.CreateSkeleton();
        BonePose root = RigTestAssertions.AssertSuccess(
            BonePose.Create("Root", RigTestAssertions.CreateTransform()));
        BonePose unknown = RigTestAssertions.AssertSuccess(
            BonePose.Create("Unknown", RigTestAssertions.CreateTransform()));

        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(null, []),
            PoseDefinitionValidationError.NullSkeleton);
        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(skeleton, null),
            PoseDefinitionValidationError.NullBones);
        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(skeleton, [null]),
            PoseDefinitionValidationError.NullBone);
        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(skeleton, [unknown]),
            PoseDefinitionValidationError.UnknownBone);
        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(skeleton, [root, root]),
            PoseDefinitionValidationError.DuplicateBone);
        RigTestAssertions.AssertFailure(
            PoseDefinition.Create(skeleton, [root]),
            PoseDefinitionValidationError.MissingBone);
    }

    [Fact]
    public void RigRequiresTypeSkeletonAndMatchingCompleteReferencePose()
    {
        SkeletonDefinition skeleton = RigTestAssertions.CreateSkeleton();
        PoseDefinition pose = RigTestAssertions.CreatePose(skeleton);

        RigTestAssertions.AssertFailure(
            RigDefinition.Create(null, skeleton, pose),
            RigDefinitionValidationError.NullRigType);
        RigTestAssertions.AssertFailure(
            RigDefinition.Create(RigType.Generic, null, pose),
            RigDefinitionValidationError.NullSkeleton);
        RigTestAssertions.AssertFailure(
            RigDefinition.Create(RigType.Generic, skeleton, null),
            RigDefinitionValidationError.NullReferencePose);

        SkeletonDefinition other = RigTestAssertions.CreateSkeleton(
            RigTestAssertions.CreateBone("Other"));
        RigTestAssertions.AssertFailure(
            RigDefinition.Create(RigType.Generic, skeleton, RigTestAssertions.CreatePose(other)),
            RigDefinitionValidationError.ReferencePoseSkeletonMismatch);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryApprovedRigTypeMustBeSelectedExplicitly(int index)
    {
        RigDefinition rig = RigTestAssertions.CreateRig(RigType.All[index]);
        Assert.Same(RigType.All[index], rig.RigType);
        Assert.Same(rig.Skeleton, rig.ReferencePose.Skeleton);
    }

    [Fact]
    public void PoseBoneAndRigEqualityHashingAreCultureIndependentAndComplete()
    {
        RigTransform transform = RigTestAssertions.CreateTransform(translationX: 1d);
        RigTransform otherTransform = RigTestAssertions.CreateTransform(translationX: 2d);
        BonePose bone = RigTestAssertions.AssertSuccess(BonePose.Create("Root", transform));
        BonePose sameBone = RigTestAssertions.AssertSuccess(BonePose.Create("Root", transform));
        BonePose differentIdentity = RigTestAssertions.AssertSuccess(
            BonePose.Create("Other", transform));
        BonePose differentTransform = RigTestAssertions.AssertSuccess(
            BonePose.Create("Root", otherTransform));
        SkeletonDefinition skeleton = RigTestAssertions.CreateSkeleton(
            RigTestAssertions.CreateBone("Root"));
        PoseDefinition pose = RigTestAssertions.AssertSuccess(
            PoseDefinition.Create(skeleton, [bone]));
        PoseDefinition samePose = RigTestAssertions.AssertSuccess(
            PoseDefinition.Create(
                RigTestAssertions.CreateSkeleton(RigTestAssertions.CreateBone("Root")),
                [sameBone]));
        RigDefinition rig = RigTestAssertions.AssertSuccess(
            RigDefinition.Create(RigType.Generic, skeleton, pose));
        RigDefinition sameRig = RigTestAssertions.AssertSuccess(
            RigDefinition.Create(RigType.Generic, samePose.Skeleton, samePose));
        CultureInfo previous = CultureInfo.CurrentCulture;
        int boneHash = bone.GetHashCode();
        int poseHash = pose.GetHashCode();
        int rigHash = rig.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(bone.Equals(sameBone));
            Assert.True(bone.Equals((object)sameBone));
            Assert.False(bone.Equals(differentIdentity));
            Assert.False(bone.Equals(differentTransform));
            Assert.False(bone.Equals((BonePose?)null));
            Assert.False(bone.Equals("pose"));
            Assert.Equal(boneHash, sameBone.GetHashCode());

            Assert.True(pose.Equals(samePose));
            Assert.True(pose.Equals((object)samePose));
            Assert.False(pose.Equals(
                RigTestAssertions.CreatePose(
                    skeleton,
                    rootTransform: RigTestAssertions.CreateTransform(translationX: 2d))));
            Assert.False(pose.Equals((PoseDefinition?)null));
            Assert.False(pose.Equals("pose"));
            Assert.Equal(poseHash, samePose.GetHashCode());

            Assert.True(rig.Equals(sameRig));
            Assert.True(rig.Equals((object)sameRig));
            Assert.False(rig.Equals(
                RigTestAssertions.AssertSuccess(
                    RigDefinition.Create(RigType.Humanoid, skeleton, pose))));
            Assert.False(rig.Equals(
                RigTestAssertions.CreateRig(
                    skeleton: RigTestAssertions.CreateSkeleton(
                        RigTestAssertions.CreateBone("Other")))));
            Assert.False(rig.Equals((RigDefinition?)null));
            Assert.False(rig.Equals("rig"));
            Assert.Equal(rigHash, sameRig.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
