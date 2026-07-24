using PackageBuilder.Domain.BuildJobs;
using PackageBuilder.Domain.Identifiers;

namespace PackageBuilder.Domain.Tests.BuildJobs;

[Trait("Task", "PB-0108")]
public sealed class BuildJobStateTransitionTests
{
    public static TheoryData<string, string> ApprovedTransitions =>
        new()
        {
            { "queued", "preflight" },
            { "queued", "cancelled" },
            { "preflight", "inspecting" },
            { "preflight", "failed" },
            { "preflight", "cancelled" },
            { "inspecting", "awaiting-review" },
            { "inspecting", "normalizing" },
            { "inspecting", "failed" },
            { "awaiting-review", "inspecting" },
            { "awaiting-review", "cancelled" },
            { "normalizing", "building-targets" },
            { "normalizing", "failed" },
            { "building-targets", "rendering-previews" },
            { "building-targets", "failed" },
            { "rendering-previews", "validating" },
            { "rendering-previews", "failed" },
            { "validating", "packaging-marketplace" },
            { "validating", "failed" },
            { "packaging-marketplace", "clean-reimport" },
            { "packaging-marketplace", "failed" },
            { "clean-reimport", "completed" },
            { "clean-reimport", "failed" },
        };

    [Theory]
    [MemberData(nameof(ApprovedTransitions))]
    public void EveryApprovedTransitionSucceedsIndividually(
        string fromIdentifier,
        string toIdentifier)
    {
        BuildJobState from = BuildJobState.TryParse(fromIdentifier).Value!;
        BuildJobState to = BuildJobState.TryParse(toIdentifier).Value!;
        BuildJob job = Reach(from);

        BuildJobTransitionResult result =
            job.TryTransition(to, BuildTestAssertions.CreatedAt.AddHours(1));

        Assert.True(result.IsSuccessful);
        Assert.Equal(to, result.Value!.State);
        Assert.Equal(job.TransitionHistory.Count + 1, result.Value.TransitionHistory.Count);
        BuildJobTransition transition = result.Value.TransitionHistory[^1];
        Assert.Equal(job.TransitionHistory.Count + 1, transition.Ordinal);
        Assert.Equal(from, transition.From);
        Assert.Equal(to, transition.To);
        Assert.Equal(BuildTestAssertions.CreatedAt.AddHours(1), transition.OccurredAtUtc);
        Assert.True(BuildJobTransitionPolicy.IsApproved(from, to));
        Assert.Contains(to, BuildJobTransitionPolicy.GetApprovedTargets(from));
    }

    [Fact]
    public void CompleteTransitionMatrixRejectsEveryUnapprovedEdge()
    {
        foreach (BuildJobState from in BuildJobState.All)
        {
            BuildJob job = Reach(from);
            foreach (BuildJobState to in BuildJobState.All)
            {
                if (BuildJobTransitionPolicy.IsApproved(from, to))
                {
                    continue;
                }

                BuildJobTransitionResult result =
                    job.TryTransition(to, BuildTestAssertions.CreatedAt.AddHours(2));
                Assert.False(result.IsSuccessful);
                Assert.Null(result.Value);
                BuildJobTransitionError expected = from.Equals(to)
                    ? BuildJobTransitionError.SameState
                    : from.IsTerminal
                    ? BuildJobTransitionError.TerminalState
                    : BuildJobTransitionError.TransitionNotApproved;
                Assert.Equal(expected, result.Error);
            }
        }
    }

    [Fact]
    public void TransitionRejectsNullAndInvalidTimestampsWithoutChangingOriginal()
    {
        BuildJob queued = BuildTestAssertions.Job();
        AssertTransitionFailure(
            queued.TryTransition(null, BuildTestAssertions.CreatedAt),
            BuildJobTransitionError.NullTargetState);
        AssertTransitionFailure(
            queued.TryTransition(
                BuildJobState.Preflight,
                BuildTestAssertions.CreatedAt.ToOffset(TimeSpan.FromHours(1))),
            BuildJobTransitionError.TimestampNotUtc);
        AssertTransitionFailure(
            queued.TryTransition(
                BuildJobState.Preflight,
                BuildTestAssertions.CreatedAt.AddTicks(-1)),
            BuildJobTransitionError.TimestampBeforeJobCreation);

        BuildJob preflight = BuildTestAssertions.Transition(
            queued,
            BuildJobState.Preflight,
            BuildTestAssertions.CreatedAt.AddMinutes(2));
        AssertTransitionFailure(
            preflight.TryTransition(
                BuildJobState.Inspecting,
                BuildTestAssertions.CreatedAt.AddMinutes(1)),
            BuildJobTransitionError.TimestampBeforePreviousTransition);
        Assert.Equal(BuildJobState.Queued, queued.State);
        Assert.Empty(queued.TransitionHistory);
    }

    [Fact]
    public void HistoryIsDeterministicImmutableAndValueComparable()
    {
        BuildJob first = BuildTestAssertions.Transition(
            BuildTestAssertions.Transition(
                BuildTestAssertions.Job(),
                BuildJobState.Preflight,
                BuildTestAssertions.CreatedAt),
            BuildJobState.Inspecting,
            BuildTestAssertions.CreatedAt.AddMinutes(1));
        BuildJob second = BuildTestAssertions.Transition(
            BuildTestAssertions.Transition(
                BuildTestAssertions.Job(),
                BuildJobState.Preflight,
                BuildTestAssertions.CreatedAt),
            BuildJobState.Inspecting,
            BuildTestAssertions.CreatedAt.AddMinutes(1));

        Assert.Equal([1, 2], first.TransitionHistory.Select(value => value.Ordinal));
        Assert.Equal(first, second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.False(first.Equals((BuildJob?)null));
        Assert.False(first.Equals(new object()));
        Assert.Equal(first.TransitionHistory[0], second.TransitionHistory[0]);
        Assert.True(
            first.TransitionHistory[0].Equals((object)second.TransitionHistory[0]));
        Assert.Equal(
            first.TransitionHistory[0].GetHashCode(),
            second.TransitionHistory[0].GetHashCode());
        Assert.False(first.TransitionHistory[0].Equals((BuildJobTransition?)null));
        Assert.False(first.TransitionHistory[0].Equals(new object()));
        BuildTestAssertions.AssertImmutable(
            first.TransitionHistory,
            first.TransitionHistory[0]);
    }

    [Fact]
    public void StatesAreExplicitClassifiedParseableAndImmutable()
    {
        Assert.Equal(13, BuildJobState.All.Count);
        Assert.Equal(BuildJobStateCategory.Active, BuildJobState.Queued.Category);
        Assert.Equal(BuildJobStateCategory.ReviewWaiting, BuildJobState.AwaitingReview.Category);
        Assert.Equal(BuildJobStateCategory.TerminalSuccess, BuildJobState.Completed.Category);
        Assert.Equal(BuildJobStateCategory.TerminalFailure, BuildJobState.Failed.Category);
        Assert.Equal(BuildJobStateCategory.TerminalCancelled, BuildJobState.Cancelled.Category);
        Assert.False(BuildJobState.Queued.IsExecutionStage);
        Assert.True(BuildJobState.Preflight.IsExecutionStage);
        Assert.True(BuildJobState.Completed.IsTerminal);
        Assert.False(BuildJobState.Preflight.IsTerminal);
        Assert.Equal("clean-reimport", BuildJobState.CleanReimport.ToString());
        Assert.Same(
            BuildJobState.CleanReimport,
            BuildJobState.TryParse("clean-reimport").Value);
        Assert.Equal(
            CanonicalIdentifierParseError.Unknown,
            BuildJobState.TryParse("unknown").Error);
        Assert.Equal(
            CanonicalIdentifierParseError.Malformed,
            BuildJobState.TryParse("Clean-Reimport").Error);
        Assert.Equal(
            BuildJobState.Completed.GetHashCode(),
            BuildJobState.TryParse("completed").Value!.GetHashCode());
        Assert.False(BuildJobState.Completed.Equals((BuildJobState?)null));
        Assert.False(BuildJobState.Completed.Equals(new object()));
        BuildTestAssertions.AssertImmutable(BuildJobState.All, BuildJobState.Queued);
        Assert.Empty(BuildJobTransitionPolicy.GetApprovedTargets(BuildJobState.Completed));
    }

    private static BuildJob Reach(BuildJobState state)
    {
        BuildJob job = BuildTestAssertions.Job();
        if (state.Equals(BuildJobState.Queued))
        {
            return job;
        }

        BuildJobState[] path = state.Equals(BuildJobState.AwaitingReview)
            ? [BuildJobState.Preflight, BuildJobState.Inspecting, BuildJobState.AwaitingReview]
            : state.Equals(BuildJobState.Cancelled)
            ? [BuildJobState.Cancelled]
            : state.Equals(BuildJobState.Failed)
            ? [BuildJobState.Preflight, BuildJobState.Failed]
            : state.Equals(BuildJobState.Completed)
            ? [
                BuildJobState.Preflight,
                BuildJobState.Inspecting,
                BuildJobState.Normalizing,
                BuildJobState.BuildingTargets,
                BuildJobState.RenderingPreviews,
                BuildJobState.Validating,
                BuildJobState.PackagingMarketplace,
                BuildJobState.CleanReimport,
                BuildJobState.Completed,
            ]
            :
            [
                .. BuildJobState.All
                                .Skip(1)
                                .TakeWhile(candidate => !candidate.Equals(state))
                                .Where(candidate => candidate.IsExecutionStage)
,
                state,
            ];
        for (int index = 0; index < path.Length; index++)
        {
            job = BuildTestAssertions.Transition(
                job,
                path[index],
                BuildTestAssertions.CreatedAt.AddMinutes(index));
        }

        return job;
    }

    private static void AssertTransitionFailure(
        BuildJobTransitionResult result,
        BuildJobTransitionError error)
    {
        Assert.False(result.IsSuccessful);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Value);
    }
}
