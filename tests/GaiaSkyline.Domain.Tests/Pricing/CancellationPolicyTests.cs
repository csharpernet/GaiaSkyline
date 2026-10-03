using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Domain.Tests.Pricing;

public class CancellationPolicyTests
{
    // "Full refund until 30 days before; 50% until 14 days; nothing after." (illustrative tiers)
    private static CancellationPolicy Policy() => new(
        CancellationPolicyId.New(),
        [new CancellationTier(30, 100), new CancellationTier(14, 50)]);

    [Theory]
    [InlineData(45, 100)]
    [InlineData(30, 100)]
    [InlineData(29, 50)]
    [InlineData(14, 50)]
    [InlineData(13, 0)]
    [InlineData(0, 0)]
    public void RefundPercentageFor_picks_the_highest_qualifying_tier(int daysBefore, int expectedPct)
    {
        Policy().RefundPercentageFor(daysBefore).Should().Be(expectedPct);
    }

    [Fact]
    public void Tiers_are_ordered_most_generous_first_regardless_of_input_order()
    {
        var policy = new CancellationPolicy(
            CancellationPolicyId.New(),
            [new CancellationTier(14, 50), new CancellationTier(30, 100)]);

        policy.Tiers.Select(t => t.DaysBeforeCheckIn).Should().ContainInOrder(30, 14);
    }

    [Fact]
    public void A_policy_needs_at_least_one_tier()
    {
        var act = () => new CancellationPolicy(CancellationPolicyId.New(), []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Refund_percentages_must_be_within_range()
    {
        var act = () => new CancellationPolicy(
            CancellationPolicyId.New(),
            [new CancellationTier(30, 150)]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
