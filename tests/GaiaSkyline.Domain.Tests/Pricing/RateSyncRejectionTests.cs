using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Domain.Tests.Pricing;

public sealed class RateSyncRejectionTests
{
    private static RateSyncRejection Rejection() => new(
        RateSyncRejectionId.New(), new DateOnly(2030, 5, 1), 240m, 2, "PriceLabs", "above ceiling €200",
        new DateTime(2030, 4, 1, 8, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Starts_open_and_accept_or_dismiss_settles_it_exactly_once()
    {
        var accepted = Rejection();
        accepted.Status.Should().Be(RateSyncRejectionStatus.Open);

        var at = new DateTime(2030, 4, 2, 9, 0, 0, DateTimeKind.Utc);
        accepted.Accept(at);
        accepted.Status.Should().Be(RateSyncRejectionStatus.Accepted);
        accepted.ResolvedAtUtc.Should().Be(at);
        var again = () => accepted.Dismiss(at);
        again.Should().Throw<InvalidOperationException>();

        var dismissed = Rejection();
        dismissed.Dismiss(at);
        dismissed.Status.Should().Be(RateSyncRejectionStatus.Dismissed);
    }

    [Fact]
    public void A_newer_offer_updates_an_open_rejection_but_never_a_settled_one()
    {
        var rejection = Rejection();
        var later = new DateTime(2030, 4, 3, 8, 0, 0, DateTimeKind.Utc);

        rejection.UpdateOffer(260m, 3, "above ceiling €200", later);
        rejection.OfferedPriceEur.Should().Be(260m);
        rejection.OfferedMinNights.Should().Be(3);
        rejection.DetectedAtUtc.Should().Be(later);

        rejection.Dismiss(later);
        var update = () => rejection.UpdateOffer(280m, null, "above ceiling €200", later);
        update.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rejects_blank_provider_or_non_positive_offers()
    {
        var blankProvider = () => new RateSyncRejection(
            RateSyncRejectionId.New(), new DateOnly(2030, 5, 1), 100m, null, " ", "reason", DateTime.UtcNow);
        blankProvider.Should().Throw<ArgumentException>();

        var zeroPrice = () => new RateSyncRejection(
            RateSyncRejectionId.New(), new DateOnly(2030, 5, 1), 0m, null, "PriceLabs", "reason", DateTime.UtcNow);
        zeroPrice.Should().Throw<ArgumentOutOfRangeException>();
    }
}
