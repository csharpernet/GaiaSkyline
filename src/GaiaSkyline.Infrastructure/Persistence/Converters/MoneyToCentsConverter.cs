using GaiaSkyline.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaiaSkyline.Infrastructure.Persistence.Converters;

/// <summary>
/// Persists <see cref="Money"/> as integer minor units (cents) in a single <c>bigint</c> column, per
/// the Stage 4 convention ("stored in minor units internally, presented in euros"). The booking
/// domain is single-currency EUR, so the currency is not stored and is re-attached on read; this also
/// matches the amount Stripe expects. Registered globally in <c>AppDbContext.ConfigureConventions</c>,
/// which covers both <c>Money</c> and <c>Money?</c> properties.
/// </summary>
internal sealed class MoneyToCentsConverter : ValueConverter<Money, long>
{
    private const string Currency = "EUR";

    public MoneyToCentsConverter()
        : base(
            money => (long)Math.Round(money.Amount * 100m, MidpointRounding.AwayFromZero),
            cents => new Money(cents / 100m, Currency))
    {
    }
}
