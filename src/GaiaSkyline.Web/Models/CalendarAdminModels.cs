using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Availability;

namespace GaiaSkyline.Web.Models;

/// <summary>The /admin/calendar page: the period grid plus navigation state.</summary>
public sealed record CalendarAdminViewModel(
    CalendarPeriod Period,
    string View,
    DateOnly Anchor,
    DateOnly PrevAnchor,
    DateOnly NextAnchor);

/// <summary>ExternalBooking blocks whose dates exactly match an imported iCal block (bulk-deletable).</summary>
public sealed record CalendarDuplicatesViewModel(IReadOnlyList<OwnerBlockDto> Duplicates);
