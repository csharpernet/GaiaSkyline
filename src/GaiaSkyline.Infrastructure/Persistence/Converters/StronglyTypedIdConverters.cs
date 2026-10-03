using GaiaSkyline.Domain.Identifiers;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GaiaSkyline.Infrastructure.Persistence.Converters;

// EF Core value converters for the strongly-typed ids introduced in Stage 2. Registered in
// AppDbContext.ConfigureConventions, which also covers the nullable (TId?) usages.

internal sealed class ContentBlockIdConverter : ValueConverter<ContentBlockId, Guid>
{
    public ContentBlockIdConverter()
        : base(id => id.Value, value => ContentBlockId.From(value))
    {
    }
}

internal sealed class ContentTranslationIdConverter : ValueConverter<ContentTranslationId, Guid>
{
    public ContentTranslationIdConverter()
        : base(id => id.Value, value => ContentTranslationId.From(value))
    {
    }
}

internal sealed class MediaAssetIdConverter : ValueConverter<MediaAssetId, Guid>
{
    public MediaAssetIdConverter()
        : base(id => id.Value, value => MediaAssetId.From(value))
    {
    }
}

internal sealed class MediaCollectionIdConverter : ValueConverter<MediaCollectionId, Guid>
{
    public MediaCollectionIdConverter()
        : base(id => id.Value, value => MediaCollectionId.From(value))
    {
    }
}

internal sealed class MediaCollectionItemIdConverter : ValueConverter<MediaCollectionItemId, Guid>
{
    public MediaCollectionItemIdConverter()
        : base(id => id.Value, value => MediaCollectionItemId.From(value))
    {
    }
}

internal sealed class ReviewIdConverter : ValueConverter<ReviewId, Guid>
{
    public ReviewIdConverter()
        : base(id => id.Value, value => ReviewId.From(value))
    {
    }
}

internal sealed class StoryIdConverter : ValueConverter<StoryId, Guid>
{
    public StoryIdConverter()
        : base(id => id.Value, value => StoryId.From(value))
    {
    }
}

internal sealed class StoryTranslationIdConverter : ValueConverter<StoryTranslationId, Guid>
{
    public StoryTranslationIdConverter()
        : base(id => id.Value, value => StoryTranslationId.From(value))
    {
    }
}

// Stage 4 (booking) identities.

internal sealed class BookingIdConverter : ValueConverter<BookingId, Guid>
{
    public BookingIdConverter()
        : base(id => id.Value, value => BookingId.From(value))
    {
    }
}

internal sealed class PricingRuleIdConverter : ValueConverter<PricingRuleId, Guid>
{
    public PricingRuleIdConverter()
        : base(id => id.Value, value => PricingRuleId.From(value))
    {
    }
}

internal sealed class FeeIdConverter : ValueConverter<FeeId, Guid>
{
    public FeeIdConverter()
        : base(id => id.Value, value => FeeId.From(value))
    {
    }
}

internal sealed class CancellationPolicyIdConverter : ValueConverter<CancellationPolicyId, Guid>
{
    public CancellationPolicyIdConverter()
        : base(id => id.Value, value => CancellationPolicyId.From(value))
    {
    }
}

internal sealed class PromoCodeIdConverter : ValueConverter<PromoCodeId, Guid>
{
    public PromoCodeIdConverter()
        : base(id => id.Value, value => PromoCodeId.From(value))
    {
    }
}

internal sealed class PartnerIdConverter : ValueConverter<PartnerId, Guid>
{
    public PartnerIdConverter()
        : base(id => id.Value, value => PartnerId.From(value))
    {
    }
}

internal sealed class ExternalCalendarBlockIdConverter : ValueConverter<ExternalCalendarBlockId, Guid>
{
    public ExternalCalendarBlockIdConverter()
        : base(id => id.Value, value => ExternalCalendarBlockId.From(value))
    {
    }
}

internal sealed class StripeEventLogIdConverter : ValueConverter<StripeEventLogId, Guid>
{
    public StripeEventLogIdConverter()
        : base(id => id.Value, value => StripeEventLogId.From(value))
    {
    }
}

// Stage 6 (identity / auditing).

internal sealed class AuditEventIdConverter : ValueConverter<AuditEventId, Guid>
{
    public AuditEventIdConverter()
        : base(id => id.Value, value => AuditEventId.From(value))
    {
    }
}
