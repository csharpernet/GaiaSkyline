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
        services.AddScoped<ICheckoutService, GaiaSkyline.Infrastructure.Bookings.CheckoutService>();
        services.AddScoped<IBookingReadStore, BookingReadStore>();
        services.AddScoped<IGuestMagicLinkService, GuestMagicLinkService>();
        services.AddScoped<GaiaSkyline.Application.Partners.IPartnerRefreshTokenStore, GaiaSkyline.Infrastructure.Partners.PartnerRefreshTokenStore>();
        services.AddScoped<IInvoiceService, QuestPdfInvoiceService>();

        // Payments (Stage 4 / Increment C).
        services.AddOptions<StripeOptions>().Bind(configuration.GetSection(StripeOptions.SectionName));
        services.AddSingleton<IStripeClient>(sp =>
            new StripeClient(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StripeOptions>>().Value.SecretKey));
        // Resolve the client lazily so the app boots and serves non-payment endpoints when no Stripe
        // key is configured (e.g. CI). Constructing a StripeClient with an empty key throws, so an
        // actual payment/refund is the first thing that requires a real key — which is correct.
        services.AddSingleton(sp =>
            new Lazy<IStripeClient>(sp.GetRequiredService<IStripeClient>));
        services.AddScoped<IPaymentService, StripePaymentService>();
        services.AddScoped<IRefundService, StripeRefundService>();
        services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();
        services.AddScoped<IBookingExpiryService, BookingExpiryService>();

        // Email (Stage 4 / Increment D). Provider selected by config; dev uses SMTP (smtp4dev).
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        var emailProvider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Smtp";
        if (string.Equals(emailProvider, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SendGridEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }

        services.AddScoped<BookingEmailComposer>();
        services.AddScoped<IBookingEmailDispatcher, BookingEmailDispatcher>();
        services.AddScoped<IBookingTokenService, BookingTokenService>();
        // Inline by default; AddBackgroundJobs overrides with the Hangfire scheduler when enabled.
        services.AddScoped<IEmailJobScheduler, InlineEmailJobScheduler>();
        services.AddScoped<IBookingNotificationService, EmailBookingNotificationService>();

        // ICS cache seam stays a no-op until Stage 5.
        services.AddSingleton<IIcsCacheInvalidator, NoOpIcsCacheInvalidator>();

        return services;
    }
}
