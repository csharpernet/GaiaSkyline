# 0020 — Commission basis and the 30-day payable window

Date: 2026-10-06 · Status: accepted · Stage 8 Part A

## Context

Partners earn a percentage of the bookings they drive. The booking total mixes revenue the owner keeps with
pass-through amounts (tourist tax is remitted to the municipality; the cleaning fee covers a cost), and
bookings can be cancelled or refunded after confirmation.

## Decision

- **Basis = Total − TouristTax − CleaningFee − refunded amounts**, floored at zero. Commission = basis ×
  the partner's percentage (default 10%, per-partner editable), rounded to cents away from zero.
- A commission is created `Pending` when the attributed booking is **confirmed** (and self-healed daily for
  any confirmation path that missed the hook). **Self-referral guard:** no commission when the guest email
  equals the partner's email.
- `Pending → Payable` **30 days after check-out** — past the stay and the realistic refund window, so paid
  commissions rarely need clawing back.
- Cancellation or a full refund → `Void`. A partial refund recalculates the basis (and amount) while the
  commission is still unpaid; `Paid` commissions are final. To know refunded amounts the `Booking` now
  accumulates `RefundedAmount` from the admin refund/cancel flows and the Stripe `charge.refunded` webhook.

## Consequences

- Partners are paid on owner revenue, not on pass-through amounts — statements are defensible.
- The 30-day window delays payouts by up to a month after check-out; combined with the monthly run
  (ADR 0021) a commission reaches the partner 1–2 months after the stay. Accepted for launch.
- A refund that lands after a commission was paid is NOT clawed back automatically; the owner adjusts
  manually if ever needed (expected to be rare given the window).
