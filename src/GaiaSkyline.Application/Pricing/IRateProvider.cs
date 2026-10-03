namespace GaiaSkyline.Application.Pricing;

/// <summary>A rate from an external pricing provider (PriceLabs/Hostify) for one date.</summary>
public sealed record ProviderRate(DateOnly Date, decimal Price, int? MinNights);

/// <summary>
/// Pulls rates from an external pricing provider. No real adapter ships yet (we have no verified API
/// access — see ADR 0014); only the interface, a fake for tests, and the sync job's rules exist.
/// </summary>
public interface IRateProvider
{
    Task<IReadOnlyList<ProviderRate>> GetRatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
