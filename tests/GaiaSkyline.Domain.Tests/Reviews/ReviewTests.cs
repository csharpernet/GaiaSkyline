using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Reviews;

namespace GaiaSkyline.Domain.Tests.Reviews;

public class ReviewTests
{
    private static Review Create(
        int rating = 5,
        string? name = "Aicha",
        string? body = "A lovely stay with a wonderful view.",
        string? source = "Airbnb") =>
        new(ReviewId.New(), rating, name!, "Paris, France", body!, source!, new DateOnly(2025, 6, 1), true);

    [Fact]
    public void Valid_review_is_constructed()
    {
        var review = Create();

        review.Rating.Should().Be(5);
        review.GuestFirstName.Should().Be("Aicha");
        review.Source.Should().Be("Airbnb");
        review.IsPublished.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rejects_rating_outside_1_to_5(int rating)
    {
        var act = () => Create(rating: rating);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_guest_name(string? name)
    {
        var act = () => Create(name: name);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_body(string? body)
    {
        var act = () => Create(body: body);

        act.Should().Throw<ArgumentException>();
    }
}
