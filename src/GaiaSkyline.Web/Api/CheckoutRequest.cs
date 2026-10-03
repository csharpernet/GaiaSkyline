using FluentValidation;

namespace GaiaSkyline.Web.Api;

/// <summary>The checkout POST body: the quote selection plus guest details. Prices are never sent.</summary>
public sealed record CheckoutRequest(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    string GuestCountry,
    string? PromoCode = null,
    string? ArrivalEstimateLocal = null,
    string? SpecialRequests = null,
    bool CreateAccount = false,
    string Language = "en");

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    private const int MaxOccupancy = 6;

    public CheckoutRequestValidator()
    {
        RuleFor(x => x.CheckOut).GreaterThan(x => x.CheckIn).WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.Adults).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Children).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Infants).InclusiveBetween(0, 1);
        RuleFor(x => x).Must(x => x.Adults + x.Children <= MaxOccupancy)
            .WithMessage($"This apartment sleeps up to {MaxOccupancy} guests (infants excluded).");

        RuleFor(x => x.GuestName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GuestEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.GuestPhone).NotEmpty().MaximumLength(40);
        RuleFor(x => x.GuestCountry).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SpecialRequests).MaximumLength(2000);
    }
}
