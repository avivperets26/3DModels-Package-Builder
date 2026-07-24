using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.Rigging;

/// <summary>Represents one immutable validated bone hierarchy.</summary>
public sealed class SkeletonDefinition : IEquatable<SkeletonDefinition>
{
    private SkeletonDefinition(
        BoneDefinition root,
        ReadOnlyCollection<BoneDefinition> bones)
    {
        Root = root;
        Bones = bones;
    }

    /// <summary>Gets the hierarchy's single validated root bone.</summary>
    public BoneDefinition Root { get; }

    /// <summary>
    /// Gets bones in deterministic root-first depth-first order, with siblings ordered by exact
    /// ordinal identity.
    /// </summary>
    public IReadOnlyList<BoneDefinition> Bones { get; }

    /// <summary>Validates and creates a hierarchy without imposing an engine bone-count limit.</summary>
    public static SkeletonDefinitionValidationResult Create(
        IEnumerable<BoneDefinition?>? bones)
    {
        if (bones is null)
        {
            return SkeletonDefinitionValidationResult.Failure(
                SkeletonDefinitionValidationError.NullBones);
        }

        var byIdentity = new Dictionary<string, BoneDefinition>(StringComparer.Ordinal);
        foreach (BoneDefinition? bone in bones)
        {
            if (bone is null)
            {
                return SkeletonDefinitionValidationResult.Failure(
                    SkeletonDefinitionValidationError.NullBone);
            }

            if (!byIdentity.TryAdd(bone.Identity, bone))
            {
                return SkeletonDefinitionValidationResult.Failure(
                    SkeletonDefinitionValidationError.DuplicateBone);
            }
        }

        foreach (BoneDefinition bone in byIdentity.Values.OrderBy(
            item => item.Identity,
            StringComparer.Ordinal))
        {
            if (string.Equals(bone.Identity, bone.ParentIdentity, StringComparison.Ordinal))
            {
                return SkeletonDefinitionValidationResult.Failure(
                    SkeletonDefinitionValidationError.SelfParenting);
            }

            if (bone.ParentIdentity is not null &&
                !byIdentity.ContainsKey(bone.ParentIdentity))
            {
                return SkeletonDefinitionValidationResult.Failure(
                    SkeletonDefinitionValidationError.OrphanedParent);
            }
        }

        BoneDefinition[] roots =
        [
            .. byIdentity.Values
                .Where(bone => bone.ParentIdentity is null)
                .OrderBy(bone => bone.Identity, StringComparer.Ordinal),
        ];
        if (roots.Length == 0)
        {
            return SkeletonDefinitionValidationResult.Failure(
                SkeletonDefinitionValidationError.MissingRoot);
        }

        if (roots.Length > 1)
        {
            return SkeletonDefinitionValidationResult.Failure(
                SkeletonDefinitionValidationError.MultipleRoots);
        }

        var children = byIdentity.Values
            .Where(bone => bone.ParentIdentity is not null)
            .GroupBy(bone => bone.ParentIdentity!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(bone => bone.Identity, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var ordered = new List<BoneDefinition>(byIdentity.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // Rooted depth-first traversal defines retained order. With exactly one parent per bone,
        // any cycle is disconnected from the single root and is detected by the unvisited count.
        Visit(roots[0], children, visited, ordered);
        return visited.Count != byIdentity.Count
            ? SkeletonDefinitionValidationResult.Failure(
                SkeletonDefinitionValidationError.HierarchyCycle)
            : SkeletonDefinitionValidationResult.Success(
            new SkeletonDefinition(roots[0], ordered.AsReadOnly()));
    }

    /// <summary>Returns whether an exact ordinal bone identity belongs to this hierarchy.</summary>
    public bool ContainsBone(string identity) =>
        Bones.Any(bone => string.Equals(bone.Identity, identity, StringComparison.Ordinal));

    /// <inheritdoc />
    public bool Equals(SkeletonDefinition? other) =>
        other is not null && Bones.SequenceEqual(other.Bones);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SkeletonDefinition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = StableRigHash.Create();
        foreach (BoneDefinition bone in Bones)
        {
            hash = hash.Add(bone.Identity).Add(bone.ParentIdentity ?? string.Empty);
        }

        return hash.ToHashCode();
    }

    private static void Visit(
        BoneDefinition bone,
        IReadOnlyDictionary<string, BoneDefinition[]> children,
        ISet<string> visited,
        ICollection<BoneDefinition> ordered)
    {
        _ = visited.Add(bone.Identity);
        ordered.Add(bone);
        if (children.TryGetValue(bone.Identity, out BoneDefinition[]? descendants))
        {
            foreach (BoneDefinition child in descendants)
            {
                Visit(child, children, visited, ordered);
            }
        }
    }
}
