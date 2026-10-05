using GaiaSkyline.Application.Payments;

namespace GaiaSkyline.Web.Models;

/// <summary>/admin/payments: Stripe status, the event browser, refunds, disputes and Multibanco holds.</summary>
public sealed record PaymentsAdminViewModel(
    StripeStatusDto Status,
    StripeEventPageDto Events,
    IReadOnlyList<RefundRowDto> Refunds,
    IReadOnlyList<DisputeRowDto> Disputes,
    IReadOnlyList<MultibancoRowDto> Multibanco,
    string? TypeFilter,
    bool FailedOnly);
