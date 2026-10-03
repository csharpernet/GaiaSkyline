using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Data;
using GaiaSkyline.Infrastructure.Notifications;
using GaiaSkyline.Infrastructure.Payments;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stripe;

namespace GaiaSkyline.Infrastructure;

/// <summary>
/// Composition-root entry point for the Infrastructure layer. Registers the EF Core
/// <see cref="AppDbContext"/> against SQL Server. Interfaces defined in inner layers are
/// implemented here; EF types stay behind this boundary.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration[$"ConnectionStrings:{ConnectionStringName}"]
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IContentReadStore, ContentReadStore>();
        services.AddScoped<ContentSeeder>();
        services.AddScoped<BookingSeeder>();

        // Pricing & booking (Stage 4).
        services.AddOptions<BookingPricingOptions>()
            .Bind(configuration.GetSection(BookingPricingOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<AvailabilityCacheState>();
        services.AddSingleton<IPricingCalculator, PricingCalculator>();
        services.AddSingleton<IBookingReferenceGenerator, BookingReferenceGenerator>();
        services.AddScoped<IPricingReadStore, PricingReadStore>();
        services.AddScoped<IQuoteService, GaiaSkyline.Application.Pricing.QuoteService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingCreationService, BookingCreationService>();
        services.AddScoped<IBookingLifecycleService, BookingLifecycleService>();

        // Payments (Stage 4 / Increment C).
        services.AddOptions<StripeOptions>().Bind(configuration.GetSection(StripeOptions.SectionName));
        services.AddSingleton<IStripeClient>(sp =>
            new StripeClient(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value.SecretKey));
        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddScoped<IRefundService, StripeRefundService>();
        services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();
        services.AddScoped<IBookingExpiryService, BookingExpiryService>();

        // Notification + ICS seams: no-ops now; real email arrives in Increment D, ICS in Stage 5.
        services.AddScoped<IBookingNotificationService, NoOpBookingNotificationService>();
        services.AddSingleton<IIcsCacheInvalidator, NoOpIcsCacheInvalidator>();

        return services;
    }
}
