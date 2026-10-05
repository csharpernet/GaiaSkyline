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
        services.AddScoped<GaiaSkyline.Application.Content.IAdminContentReadService, GaiaSkyline.Infrastructure.Content.AdminContentReadService>();
        services.AddSingleton<GaiaSkyline.Application.Content.IHtmlContentSanitizer, GaiaSkyline.Infrastructure.Content.HtmlContentSanitizer>();
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
        services.AddScoped<GaiaSkyline.Application.Pricing.IDailyRateService, GaiaSkyline.Infrastructure.Pricing.DailyRateService>();
        // Prices admin (Stage 7 §8).
        services.AddScoped<GaiaSkyline.Application.Pricing.IPricingAdminService, GaiaSkyline.Infrastructure.Pricing.PricingAdminService>();
        services.AddScoped<GaiaSkyline.Application.Pricing.IPricingAdminReadService, GaiaSkyline.Infrastructure.Pricing.PricingAdminReadService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<GaiaSkyline.Application.Availability.IOwnerBlockService, GaiaSkyline.Infrastructure.Availability.OwnerBlockService>();

        // External calendar import (Stage 5 item 1b; dormant until a source is configured).
        services.AddOptions<GaiaSkyline.Application.Availability.ExternalCalendarsOptions>()
            .Bind(configuration.GetSection(GaiaSkyline.Application.Availability.ExternalCalendarsOptions.SectionName));
        services.AddScoped<GaiaSkyline.Infrastructure.Availability.ExternalCalendarUrlProtector>();
        services.AddScoped<GaiaSkyline.Infrastructure.Availability.ExternalCalendarSourceSeeder>();
        services.AddHttpClient<GaiaSkyline.Application.Availability.IExternalCalendarImporter,
            GaiaSkyline.Infrastructure.Availability.ExternalCalendarImporter>(client =>
                client.Timeout = TimeSpan.FromSeconds(35));
        // Owner-editable runtime settings (Stage 7 §12): DB rows override configuration via options
        // post-configuration. Consumers that must see live values use IOptionsSnapshot (per scope).
        services.AddSingleton<GaiaSkyline.Infrastructure.Settings.SiteSettingsProtector>();
        services.AddSingleton<GaiaSkyline.Application.Settings.ISiteSettings, GaiaSkyline.Infrastructure.Settings.SiteSettingsSnapshot>();
        services.AddScoped<GaiaSkyline.Application.Settings.ISiteSettingsWriter, GaiaSkyline.Infrastructure.Settings.SiteSettingsWriter>();
        services.AddScoped<GaiaSkyline.Application.Settings.ISettingsAdminService, GaiaSkyline.Infrastructure.Settings.SettingsAdminService>();
        services.AddHttpClient("ics-test-fetch");

        // Automatic nightly-price import (Stage 5 item 2; dormant until a provider is configured).
        // No real PriceLabs/Hostify adapter ships yet — see ADR 0014 — so no IRateProvider is
        // registered here. When one exists, register it and set Pricing:Provider to activate the job.
        services.AddOptions<PricingProviderOptions>()
            .Bind(configuration.GetSection(PricingProviderOptions.SectionName))
            .PostConfigure<GaiaSkyline.Application.Settings.ISiteSettings>((options, settings) =>
                GaiaSkyline.Infrastructure.Settings.SettingsOverrides.Apply(options, settings));
        services.AddScoped<GaiaSkyline.Application.Pricing.IRateSyncService, GaiaSkyline.Infrastructure.Pricing.RateSyncService>();

        services.AddScoped<IBookingCreationService, BookingCreationService>();
        services.AddScoped<IBookingLifecycleService, BookingLifecycleService>();
        services.AddScoped<ICheckoutService, GaiaSkyline.Infrastructure.Bookings.CheckoutService>();
        services.AddScoped<IBookingReadStore, BookingReadStore>();
        services.AddScoped<IAdminBookingReadService, AdminBookingReadService>();
        services.AddScoped<IAdminBookingService, AdminBookingService>();
        services.AddScoped<IGuestMagicLinkService, GuestMagicLinkService>();
        services.AddScoped<GaiaSkyline.Application.Partners.IPartnerRefreshTokenStore, GaiaSkyline.Infrastructure.Partners.PartnerRefreshTokenStore>();
        // Partner-application review (Stage 7 §11).
        services.AddScoped<GaiaSkyline.Application.Partners.IPartnerApplicationsAdminService, GaiaSkyline.Infrastructure.Partners.PartnerApplicationsAdminService>();
        services.AddScoped<IInvoiceService, QuestPdfInvoiceService>();

        // Admin dashboard + manual Hostify-sync to-do (Stage 7C) + calendar (Stage 7 §7).
        services.AddScoped<GaiaSkyline.Application.Admin.IDashboardService, GaiaSkyline.Infrastructure.Admin.DashboardService>();
        services.AddScoped<GaiaSkyline.Application.Admin.IManualSyncReminderService, GaiaSkyline.Infrastructure.Admin.ManualSyncReminderService>();
        services.AddScoped<GaiaSkyline.Application.Admin.ICalendarAdminService, GaiaSkyline.Infrastructure.Admin.CalendarAdminService>();

        // Reviews admin (Stage 7 §10).
        services.AddScoped<GaiaSkyline.Application.Reviews.IReviewsAdminService, GaiaSkyline.Infrastructure.Content.ReviewsAdminService>();

        // Owner write APIs (Stage 6E).
        services.AddScoped<GaiaSkyline.Application.Content.IAdminContentService, GaiaSkyline.Infrastructure.Content.AdminContentService>();
        services.AddScoped<GaiaSkyline.Application.Content.IAdminStoryService, GaiaSkyline.Infrastructure.Content.AdminStoryService>();
        services.AddScoped<GaiaSkyline.Application.Content.IAdminStoryReadService, GaiaSkyline.Infrastructure.Content.AdminStoryReadService>();
        services.AddScoped<GaiaSkyline.Application.Content.IStorySlugRedirectResolver, GaiaSkyline.Infrastructure.Content.StorySlugRedirectResolver>();

        // SEO redirects (Stage 7 §5): the resolver index is a singleton (cached, revision-invalidated); the
        // admin service is scoped.
        services.AddSingleton<GaiaSkyline.Application.Seo.IRedirectResolver, GaiaSkyline.Infrastructure.Seo.RedirectIndex>();
        services.AddScoped<GaiaSkyline.Application.Seo.IRedirectAdminService, GaiaSkyline.Infrastructure.Seo.RedirectAdminService>();
        services.AddScoped<GaiaSkyline.Application.Seo.IPageMetaResolver, GaiaSkyline.Infrastructure.Seo.PageMetaResolver>();
        services.AddScoped<GaiaSkyline.Application.Seo.IPageMetaAdminService, GaiaSkyline.Infrastructure.Seo.PageMetaAdminService>();
        services.AddScoped<GaiaSkyline.Application.Seo.ISeoWarningsService, GaiaSkyline.Application.Seo.SeoWarningsService>();
        services.AddScoped<GaiaSkyline.Infrastructure.Media.IImageRenditionService, GaiaSkyline.Infrastructure.Media.ImageRenditionService>();
        services.AddScoped<GaiaSkyline.Application.Media.IAdminMediaService, GaiaSkyline.Infrastructure.Media.AdminMediaService>();
        services.AddScoped<GaiaSkyline.Application.Media.IAdminMediaReadService, GaiaSkyline.Infrastructure.Media.AdminMediaReadService>();
        services.AddScoped<GaiaSkyline.Application.Media.IMediaAliasResolver, GaiaSkyline.Infrastructure.Media.MediaAliasResolver>();

        // Hero background video (Stage 7E-4; transcoder swappable per ADR 0017). The job scheduler defaults to a
        // no-op warning here and is overridden by Hangfire when background processing is enabled.
        services.AddOptions<GaiaSkyline.Application.Media.VideoTranscodingOptions>()
            .Bind(configuration.GetSection(GaiaSkyline.Application.Media.VideoTranscodingOptions.SectionName));
        services.AddSingleton<GaiaSkyline.Application.Media.IVideoTranscoder, GaiaSkyline.Infrastructure.Media.FfmpegVideoTranscoder>();
        services.AddScoped<GaiaSkyline.Application.Media.IHeroVideoService, GaiaSkyline.Infrastructure.Media.HeroVideoService>();
        services.AddScoped<GaiaSkyline.Application.Media.IHeroVideoReadService, GaiaSkyline.Infrastructure.Media.HeroVideoReadService>();
        services.AddScoped<GaiaSkyline.Application.Media.IHeroVideoTranscodeJob, GaiaSkyline.Infrastructure.Media.HeroVideoTranscodeJob>();
        services.AddScoped<GaiaSkyline.Application.Media.IHeroVideoJobScheduler, GaiaSkyline.Infrastructure.Media.LoggingHeroVideoJobScheduler>();

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
        // Payments admin (Stage 7 §9).
        services.AddScoped<IPaymentsAdminReadService, PaymentsAdminReadService>();
        services.AddScoped<IBookingExpiryService, BookingExpiryService>();

        // Email (Stage 4 / Increment D). Provider selected by config; dev uses SMTP (smtp4dev).
        // Owner-email + PM-recipient overrides come from the §12 settings store (IOptionsSnapshot consumers only).
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .PostConfigure<GaiaSkyline.Application.Settings.ISiteSettings>((options, settings) =>
                GaiaSkyline.Infrastructure.Settings.SettingsOverrides.Apply(options, settings));
        services.AddOptions<PropertyManagerOptions>()
            .Bind(configuration.GetSection(PropertyManagerOptions.SectionName))
            .PostConfigure<GaiaSkyline.Application.Settings.ISiteSettings>((options, settings) =>
                GaiaSkyline.Infrastructure.Settings.SettingsOverrides.Apply(options, settings));
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
        services.AddSingleton<GaiaSkyline.Infrastructure.Availability.IcsCacheInvalidator>();
        services.AddSingleton<IIcsCacheInvalidator>(sp =>
            sp.GetRequiredService<GaiaSkyline.Infrastructure.Availability.IcsCacheInvalidator>());
        services.AddScoped<IIcsExportService, GaiaSkyline.Infrastructure.Availability.IcsExportService>();

        return services;
    }
}
