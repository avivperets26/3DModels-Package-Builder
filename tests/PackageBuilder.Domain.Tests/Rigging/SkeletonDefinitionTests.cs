using System.Globalization;
using PackageBuilder.Domain.Rigging;

namespace PackageBuilder.Domain.Tests.Rigging;

[Trait("Task", "PB-0105")]
public sealed class SkeletonDefinitionTests
{
    [Theory]
    [InlineData(null, null, BoneDefinitionValidationError.NullIdentity)]
    [InlineData("", null, BoneDefinitionValidationError.EmptyIdentity)]
    [InlineData(" ", null, BoneDefinitionValidationError.WhitespaceOnlyIdentity)]
    [InlineData(" Root", null, BoneDefinitionValidationError.IdentityEdgeWhitespace)]
    [InlineData("Root ", null, BoneDefinitionValidationError.IdentityEdgeWhitespace)]
    [InlineData("Ro\u0000ot", null, BoneDefinitionValidationError.IdentityContainsControlCharacter)]
    [InlineData("Root", "", BoneDefinitionValidationError.EmptyParentIdentity)]
    [InlineData("Root", " ", BoneDefinitionValidationError.WhitespaceOnlyParentIdentity)]
    [InlineData("Root", " Parent", BoneDefinitionValidationError.ParentIdentityEdgeWhitespace)]
    [InlineData("Root", "Parent ", BoneDefinitionValidationError.ParentIdentityEdgeWhitespace)]
    [InlineData("Root", "Par\u0000ent", BoneDefinitionValidationError.ParentIdentityContainsControlCharacter)]
    public void BoneCreationRejectsInvalidIdentity(
        string? identity,
        string? parent,
        BoneDefinitionValidationError error) =>
        RigTestAssertions.AssertFailure(BoneDefinition.Create(identity, parent), error);

    [Fact]
    public void BoneIdentityPreservesUnicodeCasingAndInteriorSpaces()
    {
        BoneDefinition bone = RigTestAssertions.AssertSuccess(
            BoneDefinition.Create("Spine İ", "Root Bone"));
        Assert.Equal("Spine İ", bone.Identity);
        Assert.Equal("Root Bone", bone.ParentIdentity);
    }

    [Fact]
    public void SkeletonCreatesSingleRootAndDeterministicHierarchyOrder()
    {
        var input = new List<BoneDefinition?>
        {
            RigTestAssertions.CreateBone("Zed", "Root"),
            RigTestAssertions.CreateBone("Grandchild", "Alpha"),
            RigTestAssertions.CreateBone("Root"),
            RigTestAssertions.CreateBone("Alpha", "Root"),
        };
        SkeletonDefinition skeleton = RigTestAssertions.AssertSuccess(
            SkeletonDefinition.Create(input));
        input.Clear();

        Assert.Equal("Root", skeleton.Root.Identity);
        Assert.Equal(
            ["Root", "Alpha", "Grandchild", "Zed"],
            skeleton.Bones.Select(bone => bone.Identity));
        Assert.True(skeleton.ContainsBone("Alpha"));
        Assert.False(skeleton.ContainsBone("alpha"));

        IList<BoneDefinition> retained = Assert.IsType<IList<BoneDefinition>>(
            skeleton.Bones,
            exactMatch: false);
        Assert.True(retained.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(
            () => retained.Add(RigTestAssertions.CreateBone("Other")));
    }

    [Fact]
    public void SkeletonRejectsNullCollectionAndElement()
    {
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(null),
            SkeletonDefinitionValidationError.NullBones);
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create([null]),
            SkeletonDefinitionValidationError.NullBone);
    }

    [Fact]
    public void SkeletonRejectsDuplicateBonesUsingOrdinalIdentity()
    {
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("Root"),
                    RigTestAssertions.CreateBone("Root"),
                ]),
            SkeletonDefinitionValidationError.DuplicateBone);

        SkeletonDefinition distinctCase = RigTestAssertions.AssertSuccess(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("Root"),
                    RigTestAssertions.CreateBone("root", "Root"),
                ]));
        Assert.Equal(2, distinctCase.Bones.Count);
    }

    [Fact]
    public void SkeletonRejectsSelfParentMissingParentAndRootFailures()
    {
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create([RigTestAssertions.CreateBone("Root", "Root")]),
            SkeletonDefinitionValidationError.SelfParenting);
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create([RigTestAssertions.CreateBone("Child", "Missing")]),
            SkeletonDefinitionValidationError.OrphanedParent);
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("A", "B"),
                    RigTestAssertions.CreateBone("B", "A"),
                ]),
            SkeletonDefinitionValidationError.MissingRoot);
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("A"),
                    RigTestAssertions.CreateBone("B"),
                ]),
            SkeletonDefinitionValidationError.MultipleRoots);
    }

    [Fact]
    public void SkeletonRejectsDirectAndIndirectDisconnectedCycles()
    {
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("Root"),
                    RigTestAssertions.CreateBone("A", "B"),
                    RigTestAssertions.CreateBone("B", "A"),
                ]),
            SkeletonDefinitionValidationError.HierarchyCycle);
        RigTestAssertions.AssertFailure(
            SkeletonDefinition.Create(
                [
                    RigTestAssertions.CreateBone("Root"),
                    RigTestAssertions.CreateBone("A", "C"),
                    RigTestAssertions.CreateBone("B", "A"),
                    RigTestAssertions.CreateBone("C", "B"),
                ]),
            SkeletonDefinitionValidationError.HierarchyCycle);
    }

    [Fact]
    public void BoneAndSkeletonEqualityHashingAreOrdinalCultureIndependentValues()
    {
        BoneDefinition bone = RigTestAssertions.CreateBone("İ", "Root");
        BoneDefinition sameBone = RigTestAssertions.CreateBone("İ", "Root");
        SkeletonDefinition first = RigTestAssertions.CreateSkeleton(
            RigTestAssertions.CreateBone("Root"),
            bone);
        SkeletonDefinition same = RigTestAssertions.CreateSkeleton(
            sameBone,
            RigTestAssertions.CreateBone("Root"));
        CultureInfo previous = CultureInfo.CurrentCulture;
        int boneHash = bone.GetHashCode();
        _ = RigTestAssertions.CreateBone("Root").GetHashCode();
        int skeletonHash = first.GetHashCode();

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(bone.Equals(sameBone));
            Assert.True(bone.Equals((object)sameBone));
            Assert.False(bone.Equals(RigTestAssertions.CreateBone("i", "Root")));
            Assert.False(bone.Equals(RigTestAssertions.CreateBone("İ", "Other")));
            Assert.False(bone.Equals((BoneDefinition?)null));
            Assert.False(bone.Equals("bone"));
            Assert.Equal(boneHash, sameBone.GetHashCode());

            Assert.True(first.Equals(same));
            Assert.True(first.Equals((object)same));
            Assert.False(first.Equals(RigTestAssertions.CreateSkeleton(
                RigTestAssertions.CreateBone("Other"))));
            Assert.False(first.Equals((SkeletonDefinition?)null));
            Assert.False(first.Equals("skeleton"));
            Assert.Equal(skeletonHash, same.GetHashCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
