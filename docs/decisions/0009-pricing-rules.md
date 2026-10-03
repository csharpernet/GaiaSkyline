# 0009 — Pricing rules: discounts, tourist tax, minimum nights

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

Stage 4 adds the quote engine behind direct booking. Prices mirror the owner's Airbnb listing, but
there is no reliable way to pull them automatically (no public Airbnb pricing API for a listing;
iCal carries availability only; scraping is brittle and against Airbnb's ToS). So **our backend is
the source of truth** for prices, kept aligned with Airbnb by the owner (and, optionally in future,
by syncing from a channel manager's API). Several pricing rules need pinning down and testing.

## Decision

### Source of truth
Nightly rates, fees, length-of-stay discounts and promo codes are **database rows**, edited via the
Stage 7 admin. The seeder only provides initial values. No prices are hardcoded in code.

### Per-night rates (seasons)
Each night is priced from the `PricingRule` covering that date, so a stay spanning seasons is summed
per night. A night with no covering rule is a hard error (`NoPriceForDateException`).

### Discounts — never stacked
A booking gets **one** discount: the larger of the applicable length-of-stay discount (weekly for
7+ nights, monthly for 28+ nights) and any valid promo code. They never stack. The discount applies
to the **nightly subtotal only** (not to the cleaning fee or tourist tax). Percentages come from the
check-in night's rule (length-of-stay) and the promo code row. Length-of-stay discounts and promo
codes are created and edited in the backend; both are seeded empty initially.

### Tourist tax
Modelled as a `Fee(TouristTax)`: **per adult, per night, capped** at the fee's `MaxNights`. Children
and infants are exempt. Because bookings record adult/child/infant counts (not exact ages),
`MinAgeExempt` documents the municipal policy but the charge is driven by the adult count. If no
tourist-tax fee is configured the tax is zero (an all-inclusive price, matching a simple Airbnb
price); it can be switched on later by adding the fee row.

### Minimum nights
Standard minimum is `PricingRule.MinNights` (seeded to 3). A **last-minute exception** applies: when
check-in is within a configurable window (7 days) of today (Europe/Lisbon), the minimum drops to a
configurable value (1 night) so last-minute gaps can be filled. Availability is enforced separately,
so "and the dates are still open" is automatic.

### Rounding
Money is held and persisted as integer cents (ADR 0008 / Stage 4). Percentage discounts are rounded
to the cent, away from zero. All other amounts are exact sums.

## Consequences

- **+** Pricing is deterministic and heavily unit-testable (the calculator is pure; the caller loads
  the context from storage).
- **+** The owner controls prices in one place; the direct site can match or undercut Airbnb (no
  platform fee), and a channel-manager sync can be added later behind the same storage seam.
- **−** Keeping parity with Airbnb is a manual step until/unless a channel manager is adopted.
- **−** Length-of-stay discount percentages are taken from the check-in night's rule; a stay that
  spans rules with different discount percentages uses the check-in rule's. Acceptable because
  discounts are managed centrally and are normally uniform.
