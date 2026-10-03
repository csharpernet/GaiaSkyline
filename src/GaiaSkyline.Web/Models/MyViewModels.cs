using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;

namespace GaiaSkyline.Web.Models;

/// <summary>Detail view for /my/booking/{ref}: the booking, the refund preview and the cancel state.</summary>
public sealed record MyBookingViewModel(
    BookingSummaryDto Booking,
    int RefundPct,
    decimal RefundAmount,
    bool CanCancel,
    bool ShowCheckin,
    ContentPayload Checkin);
