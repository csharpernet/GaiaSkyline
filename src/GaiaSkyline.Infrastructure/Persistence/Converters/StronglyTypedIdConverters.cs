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
