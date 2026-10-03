using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>EF Core implementation of <see cref="IPricingReadStore"/>. Reads are untracked.</summary>
internal sealed class PricingReadStore(AppDbContext dbContext) : IPricingReadStore
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<PricingRule>> GetPricingRulesAsync(
        DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken)
    {
        var lastNight = checkOut.AddDays(-1);
        return await _dbContext.PricingRules
            .AsNoTracking()
            .Where(r => r.StartDate <= lastNight && r.EndDate >= checkIn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Fees.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<PromoCode?> GetPromoCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _dbContext.PromoCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized, cancellationToken);
    }

    public async Task<CancellationPolicy?> GetCancellationPolicyAsync(CancellationToken cancellationToken) =>
        await _dbContext.CancellationPolicies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
}
