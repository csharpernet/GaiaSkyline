using GaiaSkyline.Application.Content;

namespace GaiaSkyline.Web.Models;

public sealed record HomeViewModel(
    ContentPayload Home,
    ContentPayload Amenities,
    ContentPayload Rules,
    ContentPayload Faq,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<StoryDto> Stories,
    IReadOnlyList<GalleryImageDto> Gallery);

public sealed record GalleryViewModel(IReadOnlyList<GalleryImageDto> Images);

public sealed record StoriesIndexViewModel(IReadOnlyList<StoryDto> Stories);

public sealed record StoryDetailViewModel(StoryDto Story, IReadOnlyList<StoryDto> Related);

public sealed record LegalViewModel(string Page, string Heading, string RegistrationValue);

public sealed record BookViewModel(string Heading, string Message);
