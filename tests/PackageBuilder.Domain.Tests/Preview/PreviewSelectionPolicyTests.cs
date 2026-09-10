using PackageBuilder.PreviewContract;

namespace PackageBuilder.Domain.Tests.Preview;

[Trait("Task", "PB-0808")]
public sealed class PreviewSelectionPolicyTests
{
    [Theory]
    [InlineData(0, -1, 1, true, -1)]
    [InlineData(1, -1, -1, true, 0)]
    [InlineData(1, 0, 1, true, 0)]
    [InlineData(3, -1, 1, true, 0)]
    [InlineData(3, -1, -1, true, 2)]
    [InlineData(3, 0, -1, true, 2)]
    [InlineData(3, 2, 1, true, 0)]
    [InlineData(3, 0, -1, false, 0)]
    [InlineData(3, 2, 1, false, 2)]
    public void SharedTransitionsCoverOverviewEmptySingleAndBoundaryStates(int count, int current, int direction, bool wrap, int expected) =>
        Assert.Equal(expected, PreviewSelectionPolicy.Move(count, current, direction, wrap));

    [Fact]
    public void InitialSelectionAndInvalidRequestsAreExplicit()
    {
        Assert.Equal(-1, PreviewSelectionPolicy.InitialIndex(0, false));
        Assert.Equal(-1, PreviewSelectionPolicy.InitialIndex(3, true));
        Assert.Equal(0, PreviewSelectionPolicy.InitialIndex(3, false));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSelectionPolicy.InitialIndex(-1, true));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSelectionPolicy.Move(-1, -1, 1, true));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSelectionPolicy.Move(3, -2, 1, true));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSelectionPolicy.Move(3, 3, 1, true));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSelectionPolicy.Move(3, 0, 0, true));
    }
}
