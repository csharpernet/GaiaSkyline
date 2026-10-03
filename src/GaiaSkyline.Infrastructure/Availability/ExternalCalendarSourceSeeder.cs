using GaiaSkyline.Application.Availability;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>
/// Upserts external calendar sources from configuration so an iCal URL can be added without code
/// changes. The URL is encrypted before storage. No configured sources → nothing happens (manual mode).
/// </summary>
public sealed class ExternalCalendarSourceSeeder(
    AppDbContext dbContext,
    ExternalCalendarUrlProtector urlProtector,
    IOptions<ExternalCalendarsOptions> options,
    TimeProvider clock)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var config in options.Value.Sources)
        {
            if (string.IsNullOrWhiteSpace(config.Name) || string.IsNullOrWhiteSpace(config.IcsUrl))
            {
                continue;
            }

            var existing = await dbContext.ExternalCalendarSources
                .FirstOrDefaultAsync(s => s.Name == config.Name, cancellationToken);
            if (existing is null)
            {
                dbContext.ExternalCalendarSources.Add(new ExternalCalendarSource(
                    ExternalCalendarSourceId.New(), config.Name, urlProtector.Protect(config.IcsUrl),
                    config.IsEnabled, clock.GetUtcNow().UtcDateTime));
            }
            else
            {
                existing.UpdateUrl(urlProtector.Protect(config.IcsUrl));
                existing.SetEnabled(config.IsEnabled);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
