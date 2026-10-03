using GaiaSkyline.Domain.Auditing;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Payments;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for GaiaSkyline. Also the ASP.NET Core Identity store (Stage 6), keyed by Guid
/// to match the domain's strongly-typed ids. Entity mappings live in <c>Persistence/Configurations</c>
/// and are applied by convention from this assembly. EF-specific types never leak past this project
/// (Clean Architecture boundary).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Property> Properties => Set<Property>();

    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaCollection> MediaCollections => Set<MediaCollection>();

    public DbSet<MediaCollectionItem> MediaCollectionItems => Set<MediaCollectionItem>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Story> Stories => Set<Story>();

    public DbSet<StoryTranslation> StoryTranslations => Set<StoryTranslation>();

    // Stage 4 — booking & payments.
    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingDateOccupancy> BookingDateOccupancies => Set<BookingDateOccupancy>();

    public DbSet<GuestMagicLink> GuestMagicLinks => Set<GuestMagicLink>();

    public DbSet<PricingRule> PricingRules => Set<PricingRule>();

    public DbSet<Fee> Fees => Set<Fee>();

    public DbSet<CancellationPolicy> CancellationPolicies => Set<CancellationPolicy>();

    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public DbSet<StripeEventLog> StripeEventLogs => Set<StripeEventLog>();

    public DbSet<ExternalCalendarBlock> ExternalCalendarBlocks => Set<ExternalCalendarBlock>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Strongly-typed ids introduced in Stage 2 (covers both TId and nullable TId? properties).
        configurationBuilder.Properties<ContentBlockId>().HaveConversion<ContentBlockIdConverter>();
        configurationBuilder.Properties<ContentTranslationId>().HaveConversion<ContentTranslationIdConverter>();
        configurationBuilder.Properties<MediaAssetId>().HaveConversion<MediaAssetIdConverter>();
        configurationBuilder.Properties<MediaCollectionId>().HaveConversion<MediaCollectionIdConverter>();
        configurationBuilder.Properties<MediaCollectionItemId>().HaveConversion<MediaCollectionItemIdConverter>();
        configurationBuilder.Properties<ReviewId>().HaveConversion<ReviewIdConverter>();
        configurationBuilder.Properties<StoryId>().HaveConversion<StoryIdConverter>();
        configurationBuilder.Properties<StoryTranslationId>().HaveConversion<StoryTranslationIdConverter>();

        // Stage 4 identities.
        configurationBuilder.Properties<BookingId>().HaveConversion<BookingIdConverter>();
        configurationBuilder.Properties<PricingRuleId>().HaveConversion<PricingRuleIdConverter>();
        configurationBuilder.Properties<FeeId>().HaveConversion<FeeIdConverter>();
        configurationBuilder.Properties<CancellationPolicyId>().HaveConversion<CancellationPolicyIdConverter>();
        configurationBuilder.Properties<PromoCodeId>().HaveConversion<PromoCodeIdConverter>();
        configurationBuilder.Properties<PartnerId>().HaveConversion<PartnerIdConverter>();
        configurationBuilder.Properties<ExternalCalendarBlockId>().HaveConversion<ExternalCalendarBlockIdConverter>();
        configurationBuilder.Properties<StripeEventLogId>().HaveConversion<StripeEventLogIdConverter>();

        // Stage 6 identities.
        configurationBuilder.Properties<AuditEventId>().HaveConversion<AuditEventIdConverter>();

        // Money persists as integer minor units (cents); EUR is re-attached on read.
        configurationBuilder.Properties<Money>().HaveConversion<MoneyToCentsConverter>();
    }
}
