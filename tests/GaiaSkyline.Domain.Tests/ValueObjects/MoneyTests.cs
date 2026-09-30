using FluentAssertions;
using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_normalises_currency_to_upper_invariant()
    {
        var money = new Money(10.50m, "eur");

        money.Amount.Should().Be(10.50m);
        money.Currency.Should().Be("EUR");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_missing_currency(string? currency)
    {
        var act = () => new Money(1m, currency!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("EU")]      // too short
    [InlineData("EURO")]    // too long
    [InlineData("EU1")]     // not all letters
    [InlineData("12")]      // digits
    public void Constructor_rejects_non_iso_currency(string currency)
    {
        var act = () => new Money(1m, currency);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_considers_both_amount_and_currency()
    {
        new Money(10m, "EUR").Should().Be(new Money(10m, "EUR"));
        new Money(10m, "eur").Should().Be(new Money(10m, "EUR")); // normalisation
        new Money(10m, "EUR").Should().NotBe(new Money(11m, "EUR"));
        new Money(10m, "EUR").Should().NotBe(new Money(10m, "USD"));
    }

    [Fact]
    public void Addition_sums_amounts_in_the_same_currency()
    {
        var result = new Money(10m, "EUR") + new Money(5.25m, "EUR");

        result.Should().Be(new Money(15.25m, "EUR"));
    }

    [Fact]
    public void Addition_throws_on_currency_mismatch()
    {
        var act = () => _ = new Money(10m, "EUR") + new Money(5m, "USD");

        act.Should().Throw<CurrencyMismatchException>()
            .Which.Should().Match<CurrencyMismatchException>(e =>
                e.LeftCurrency == "EUR" && e.RightCurrency == "USD");
    }

    [Fact]
    public void Subtraction_throws_on_currency_mismatch()
    {
        var act = () => _ = new Money(10m, "EUR") - new Money(5m, "GBP");

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Multiplication_scales_the_amount()
    {
        (new Money(10m, "EUR") * 3m).Should().Be(new Money(30m, "EUR"));
    }

    [Fact]
    public void Zero_creates_a_zero_amount_in_the_currency()
    {
        Money.Zero("eur").Should().Be(new Money(0m, "EUR"));
    }

    [Fact]
    public void Named_alternates_match_the_operators()
    {
        Money.Add(new Money(1m, "EUR"), new Money(2m, "EUR")).Should().Be(new Money(3m, "EUR"));
        Money.Subtract(new Money(3m, "EUR"), new Money(1m, "EUR")).Should().Be(new Money(2m, "EUR"));
        Money.Multiply(new Money(2m, "EUR"), 2m).Should().Be(new Money(4m, "EUR"));
    }

    [Fact]
    public void ToString_shows_amount_and_currency()
    {
        new Money(12.5m, "EUR").ToString().Should().Be("12.5 EUR");
    }
}
