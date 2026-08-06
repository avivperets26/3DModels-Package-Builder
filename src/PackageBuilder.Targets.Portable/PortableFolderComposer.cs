namespace PackageBuilder.Targets.Portable;

/// <summary>Produces a fail-closed logical folder plan without copying or modifying source artifacts.</summary>
public static class PortableFolderComposer
{
    /// <summary>Composes only the explicitly supplied validated artifacts into the two approved roots.</summary>
    public static PortableValidationResult<PortableFolderLayout> Compose(
        PortableNamingProfile? naming,
        IEnumerable<PortableCompositionArtifact?>? artifacts)
    {
        if (naming is null)
        {
            return Failure(PortableValidationError.NullNamingProfile);
        }

        if (artifacts is null)
        {
            return Failure(PortableValidationError.NullArtifactCollection);
        }

        List<PortableCompositionArtifact?> candidates = [.. artifacts];
        if (candidates.Any(static value => value is null))
        {
            return Failure(PortableValidationError.NullArtifactEntry);
        }

        List<PortableCompositionArtifact> values = [.. candidates.Cast<PortableCompositionArtifact>()];
        if (HasDuplicate(values.Select(static value => value.Record.Artifact.Id.Value)))
        {
            return Failure(PortableValidationError.DuplicateArtifactId);
        }

        if (HasDuplicate(
                values.Where(static value => value.TextureRole is not null)
                    .Select(static value => value.TextureRole!.CanonicalIdentifier)))
        {
            return Failure(PortableValidationError.DuplicateTextureRole);
        }

        if (HasDuplicate(
                values.Where(static value => value.MediaView is not null)
                    .Select(static value => value.MediaView!.Identifier)))
        {
            return Failure(PortableValidationError.DuplicateMediaView);
        }

        foreach (PortableArtifactPurpose required in new[]
                 {
                     PortableArtifactPurpose.Fbx,
                     PortableArtifactPurpose.FbxArchive,
                     PortableArtifactPurpose.FlatReadme,
                     PortableArtifactPurpose.ProductReadme,
                 })
        {
            if (values.All(value => !value.Purpose.Equals(required)))
            {
                return Failure(PortableValidationError.MissingRequiredArtifact);
            }
        }

        foreach (PortableArtifactPurpose singleton in new[]
                 {
                     PortableArtifactPurpose.Fbx,
                     PortableArtifactPurpose.Glb,
                     PortableArtifactPurpose.FbxArchive,
                     PortableArtifactPurpose.FlatReadme,
                     PortableArtifactPurpose.ProductReadme,
                 })
        {
            if (values.Count(value => value.Purpose.Equals(singleton)) > 1)
            {
                return Failure(PortableValidationError.DuplicateArtifactPurpose);
            }
        }

        var flatEntries = new List<PortableFolderEntry>();
        var productEntries = new List<PortableFolderEntry>();
        foreach (PortableCompositionArtifact value in values)
        {
            string name = ResolveName(naming, value);

            if (value.Purpose.Equals(PortableArtifactPurpose.Fbx) ||
                value.Purpose.Equals(PortableArtifactPurpose.FlatReadme) ||
                value.Purpose.Equals(PortableArtifactPurpose.Texture))
            {
                flatEntries.Add(new PortableFolderEntry(value.Record, name));
            }
            else
            {
                string relativePath = value.Purpose.Equals(PortableArtifactPurpose.Media)
                    ? $"Media/{name}"
                    : name;
                productEntries.Add(new PortableFolderEntry(value.Record, relativePath));
            }
        }

        flatEntries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        productEntries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return PortableValidationResult<PortableFolderLayout>.Success(
            new PortableFolderLayout(
                naming.FlatFbxFolderName,
                naming.ProductFolderName,
                flatEntries,
                productEntries));
    }

    private static string ResolveName(
        PortableNamingProfile naming,
        PortableCompositionArtifact value) =>
        value.Purpose.Equals(PortableArtifactPurpose.Fbx)
            ? naming.FbxFileName
            : value.Purpose.Equals(PortableArtifactPurpose.Glb)
            ? naming.GetGlbFileName(value.GlbVariant).Value!
            : value.Purpose.Equals(PortableArtifactPurpose.FbxArchive)
            ? naming.FbxArchiveFileName
            : value.Purpose.Equals(PortableArtifactPurpose.FlatReadme)
            ? naming.FlatReadmeFileName
            : value.Purpose.Equals(PortableArtifactPurpose.ProductReadme)
            ? naming.ProductReadmeFileName
            : value.Purpose.Equals(PortableArtifactPurpose.Texture)
            ? naming.GetTextureFileName(value.TextureRole, value.Extension).Value!
            : naming.GetMediaFileName(value.MediaView, value.Extension).Value!;

    private static bool HasDuplicate(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return values.Any(value => !seen.Add(value));
    }

    private static PortableValidationResult<PortableFolderLayout> Failure(PortableValidationError error) =>
        PortableValidationResult<PortableFolderLayout>.Failure(error);
}
