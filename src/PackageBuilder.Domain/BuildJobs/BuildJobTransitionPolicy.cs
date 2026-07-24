using System.Collections.ObjectModel;

namespace PackageBuilder.Domain.BuildJobs;

/// <summary>
/// The single authorization source for job transitions. Only edges present in the architecture
/// state diagram appear here; cancellation, retry, pause, and resume edges are never inferred.
/// </summary>
public static class BuildJobTransitionPolicy
{
    private static readonly Dictionary<BuildJobState, ReadOnlyCollection<BuildJobState>>
        _approvedTargets =
            new()
            {
                [BuildJobState.Queued] = Array.AsReadOnly(
                    [BuildJobState.Preflight, BuildJobState.Cancelled]),
                [BuildJobState.Preflight] = Array.AsReadOnly(
                    [
                        BuildJobState.Inspecting,
                        BuildJobState.Failed,
                        BuildJobState.Cancelled,
                    ]),
                [BuildJobState.Inspecting] = Array.AsReadOnly(
                    [
                        BuildJobState.AwaitingReview,
                        BuildJobState.Normalizing,
                        BuildJobState.Failed,
                    ]),
                [BuildJobState.AwaitingReview] = Array.AsReadOnly(
                    [BuildJobState.Inspecting, BuildJobState.Cancelled]),
                [BuildJobState.Normalizing] = Array.AsReadOnly(
                    [BuildJobState.BuildingTargets, BuildJobState.Failed]),
                [BuildJobState.BuildingTargets] = Array.AsReadOnly(
                    [BuildJobState.RenderingPreviews, BuildJobState.Failed]),
                [BuildJobState.RenderingPreviews] = Array.AsReadOnly(
                    [BuildJobState.Validating, BuildJobState.Failed]),
                [BuildJobState.Validating] = Array.AsReadOnly(
                    [BuildJobState.PackagingMarketplace, BuildJobState.Failed]),
                [BuildJobState.PackagingMarketplace] = Array.AsReadOnly(
                    [BuildJobState.CleanReimport, BuildJobState.Failed]),
                [BuildJobState.CleanReimport] = Array.AsReadOnly(
                    [BuildJobState.Completed, BuildJobState.Failed]),
                [BuildJobState.Completed] = Array.AsReadOnly(Array.Empty<BuildJobState>()),
                [BuildJobState.Failed] = Array.AsReadOnly(Array.Empty<BuildJobState>()),
                [BuildJobState.Cancelled] = Array.AsReadOnly(Array.Empty<BuildJobState>()),
            };

    public static IReadOnlyList<BuildJobState> GetApprovedTargets(BuildJobState state) =>
        _approvedTargets[state];

    public static bool IsApproved(BuildJobState from, BuildJobState to) =>
        GetApprovedTargets(from).Contains(to);
}
