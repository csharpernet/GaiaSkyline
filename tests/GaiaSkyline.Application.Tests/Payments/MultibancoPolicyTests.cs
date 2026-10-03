using FluentAssertions;
using GaiaSkyline.Application.Payments;

namespace GaiaSkyline.Application.Tests.Payments;

public class MultibancoPolicyTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    [Theory]
    [InlineData(10, true)]   // exactly the lead time — allowed
    [InlineData(20, true)]
    [InlineData(9, false)]   // under the lead time — excluded
    [InlineData(0, false)]
    public void Multibanco_requires_the_minimum_lead_time(int daysAhead, bool expected)
    {
        var checkIn = Today.AddDays(daysAhead);

        MultibancoPolicy.IsAllowed(checkIn, Today, minLeadDays: 10).Should().Be(expected);
    }
}
