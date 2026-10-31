using SubscriberService;
using Xunit;

namespace SubscriberService.Tests;

public class ReleaseRolloutTests
{
    [Theory]
    [InlineData(false, 100)]
    [InlineData(true, 0)]
    public void DisabledRolloutNeverAllowsSubscriptions(bool enabled, int percentage)
    {
        Assert.False(new ReleaseRollout(enabled, percentage).Allows("reader@example.com"));
    }

    [Fact]
    public void FullRolloutAllowsSubscriptions()
    {
        Assert.True(new ReleaseRollout(true, 100).Allows("reader@example.com"));
    }

    [Fact]
    public void PartialRolloutKeepsTheSameEmailInTheSameGroup()
    {
        var rollout = new ReleaseRollout(true, 35);
        Assert.Equal(rollout.Allows("Reader@Example.com"), rollout.Allows(" reader@example.com "));
    }

    [Fact]
    public void PartialRolloutNeverChangesBetweenEvaluations()
    {
        var rollout = new ReleaseRollout(true, 25);
        var first = rollout.Allows("reader@example.com");

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first, rollout.Allows("reader@example.com"));
        }
    }
}
