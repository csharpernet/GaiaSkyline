using GaiaSkyline.Application.Pricing;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>
/// A stand-in rate provider for tests and local experimentation. No real PriceLabs/Hostify adapter
/// exists yet (we have no verified API access — see ADR 0014); this returns the rates it was given.
/// </summary>
public sealed class FakeRateProvider(IReadOnlyList<ProviderRate> rates) : IRateProvider
{
    public Task<IReadOnlyList<ProviderRate>> GetRatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderRate>>(rates.Where(r => r.Date >= from && r.Date < to).ToList());
}
