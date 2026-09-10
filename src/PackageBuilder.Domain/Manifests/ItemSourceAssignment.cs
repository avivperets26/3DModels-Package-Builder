using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Domain.Manifests;

/// <summary>
/// Records explicit file-level item ownership. References are validated against the manifest
/// when assignments are attached; filenames never generate or rename stable item IDs.
/// </summary>
/// <param name="ItemId">The existing stable item identity selected by the caller.</param>
/// <param name="SourceReference">The exact source-relative reference already declared in the manifest.</param>
public sealed record ItemSourceAssignment(InternalAssetId ItemId, string SourceReference);
