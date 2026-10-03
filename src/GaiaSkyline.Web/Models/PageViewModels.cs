using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Pricing;

namespace GaiaSkyline.Web.Models;

public sealed record HomeViewModel(
    ContentPayload Home,
    ContentPayload Amenities,
    ContentPayload Rules,
    ContentPayload Faq,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<StoryDto> Stories,
    IReadOnlyList<GalleryImageDto> Gallery,
    PropertyDto? Property);

public sealed record GalleryViewModel(IReadOnlyList<GalleryImageDto> Images);

public sealed record StoriesIndexViewModel(IReadOnlyList<StoryDto> Stories);

public sealed record StoryDetailViewModel(StoryDto Story, IReadOnlyList<StoryDto> Related);

public sealed record LegalViewModel(string Page, string Heading, string RegistrationValue);

public sealed record BookViewModel(string Heading, string Message);

/// <summary>The public /book page: calendars, guest picker and live quote (copy from content blocks).</summary>
public sealed record BookPageViewModel(ContentPayload Copy, int MaxGuests);

/// <summary>The /book/checkout page: guest form, re-quoted summary and Stripe Payment Element.</summary>
public sealed record CheckoutPageViewModel(
    ContentPayload Copy,
    QuoteBreakdown Quote,
    string PublishableKey,
    long AmountCents,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Infants,
    string? PromoCode);

/// <summary>The /book/confirmation page: booking summary, Multibanco panel and check-in instructions.</summary>
public sealed record ConfirmationPageViewModel(
    ContentPayload Copy,
    ContentPayload CheckInInfo,
    BookingSummaryDto Booking,
    string Token);
