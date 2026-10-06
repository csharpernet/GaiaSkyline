using System.ComponentModel.DataAnnotations;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Pricing;

namespace GaiaSkyline.Web.Models;

/// <summary>The /admin/bookings list plus the active filter values (so the form stays populated).</summary>
public sealed record BookingsAdminIndexViewModel(
    IReadOnlyList<BookingAdminListItemDto> Bookings,
    string? Status,
    string? Query,
    DateOnly? From,
    DateOnly? To,
    string? PaymentMethod,
    string? Synced);

/// <summary>The booking detail plus its audit trail (newest first).</summary>
public sealed record BookingAdminDetailViewModel(
    BookingAdminDetailDto Booking,
    IReadOnlyList<AuditLogEntry> Audit);

/// <summary>The manual (phone/walk-in) booking form. Mirrors <see cref="ManualBookingCommand"/>.</summary>
public sealed class ManualBookingForm
{
    [Required]
    public DateOnly? CheckIn { get; set; }

    [Required]
    public DateOnly? CheckOut { get; set; }

    [Range(1, 10)]
    public int Adults { get; set; } = 2;

    [Range(0, 10)]
    public int Children { get; set; }

    [Range(0, 10)]
    public int Infants { get; set; }

    [Required]
    [StringLength(200)]
    public string GuestName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string GuestEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string GuestPhone { get; set; } = string.Empty;

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string GuestCountry { get; set; } = "PT";

    [Required]
    public string GuestLanguage { get; set; } = "en";

    [Required]
    public string PaymentMethod { get; set; } = "cash";

    [StringLength(2000)]
    public string? SpecialRequests { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool SendGuestConfirmation { get; set; } = true;

    /// <summary>A promo/partner code the guest cited on the phone — attributes the booking (Stage 8 Part A).</summary>
    [StringLength(20)]
    public string? PromoCode { get; set; }
}

/// <summary>The manual-booking page: the form plus, after "Preview price", the quoted breakdown.</summary>
public sealed record ManualBookingViewModel(ManualBookingForm Form, QuoteBreakdown? Quote, string? QuoteError);
