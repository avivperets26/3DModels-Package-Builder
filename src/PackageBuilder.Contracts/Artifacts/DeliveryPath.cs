using PackageBuilder.Domain.Naming;

namespace PackageBuilder.Contracts.Artifacts;

/// <summary>Conservative generated-delivery paths using the canonical filesystem-segment naming rule.</summary>
public static class DeliveryPath
{
    /// <summary>Rejects rooted paths, traversal, reserved names and alternate separators before archive creation.</summary>
    public static bool IsValid(string? path) => path is { Length: > 0 and <= 1024 }
        && !path.Contains('\\') && path.Split('/').All(segment =>
            segment is not ("" or "." or "..") && !segment.EndsWith('.')
            && segment.Split('.').All(part => ProductFolderName.Create(part).IsValid));
}
