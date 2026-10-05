using GaiaSkyline.Application.Bookings;

namespace GaiaSkyline.Web.Models;

/// <summary>The /admin/bookings list plus the active filter values (so the form stays populated).</summary>
public sealed record BookingsAdminIndexViewModel(
    IReadOnlyList<BookingAdminListItemDto> Bookings,
    string? Status,
    string? Query,
    DateOnly? From,
    DateOnly? To);
