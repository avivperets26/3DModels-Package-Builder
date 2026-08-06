using System.Collections.ObjectModel;
using PackageBuilder.Contracts.Artifacts;

namespace PackageBuilder.Targets.Portable;

/// <summary>Maps one validated source artifact to one root-relative portable destination.</summary>
public sealed record PortableFolderEntry(ArtifactStoreRecord Source, string RelativePath);

/// <summary>Contains immutable, deterministic flat-FBX and top-level product layout plans.</summary>
public sealed class PortableFolderLayout
{
    private readonly ReadOnlyCollection<PortableFolderEntry> _flatFbxEntries;
    private readonly ReadOnlyCollection<PortableFolderEntry> _productEntries;

    internal PortableFolderLayout(
        string flatFbxFolderName,
        string productFolderName,
        IEnumerable<PortableFolderEntry> flatFbxEntries,
        IEnumerable<PortableFolderEntry> productEntries)
    {
        FlatFbxFolderName = flatFbxFolderName;
        ProductFolderName = productFolderName;
        _flatFbxEntries = Array.AsReadOnly(flatFbxEntries.ToArray());
        _productEntries = Array.AsReadOnly(productEntries.ToArray());
    }

    public string FlatFbxFolderName { get; }

    public string ProductFolderName { get; }

    public IReadOnlyList<PortableFolderEntry> FlatFbxEntries => _flatFbxEntries;

    public IReadOnlyList<PortableFolderEntry> ProductEntries => _productEntries;
}
