# 0021 — Monthly payouts with manual money movement

Date: 2026-10-06 · Status: accepted · Stage 8 Part A

## Context

Partners need predictable payouts with statements, but automating SEPA transfers would put payment
credentials and money-movement liability into a one-property booking site.

## Decision

- A monthly job on the **5th** (Europe/Lisbon) groups each partner's `Payable` commissions for everything
  payable to date. Below the **minimum payout (€50, owner-editable in Settings)** nothing is created and the
  amount carries over to the next month.
- Each payout gets a QuestPDF **statement** (per-booking lines: dates, basis, percentage, amount) emailed to
  the partner, and the included commissions move to `Paid`.
- **Money moves manually**: the owner transfers from their own bank using the partner's IBAN (validated
  with mod-97 at onboarding) and then marks the payout **Settled** in the admin. The system never initiates
  transfers and stores no banking credentials of the owner.
- The run is idempotent per partner + period (unique index), and the owner can also trigger it on demand
  from the admin payouts page.

## Consequences

- Zero payment-provider integration for payouts; the liability stays a human-sized monthly task (a handful
  of transfers) backed by auditable statements.
- A partner below €50 for months accumulates silently — visible on their dashboard, so expectations stay
  clear.
