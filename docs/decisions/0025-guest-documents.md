# 0025 — Branded guest documents (not tax invoices) and PDF caching

- Status: Accepted
- Date: 2026-10-06
- Deciders: GaiaSkyline maintainers
- Supersedes the "invoice" framing of ADR 0012 (the PDF library choice stands)

## Context

Stage 8 adds a family of branded guest PDFs — booking **confirmation & receipt**, **payment receipt** and
**cancellation & refund receipt** — plus a restyled partner payout statement. Three forces shape the design:

1. **These are not tax documents.** Gaia Skyline issues the Portuguese *fatura* through its accounting
   software, not this site. A guest document that looked like a *fatura* (or *invoice* / *factura* /
   *facture* / *Rechnung*) would be legally misleading. ADR 0012 originally called the artefact an
   "invoice"; that was never correct for the guest's copy.
2. **They carry payment data.** Card last-4, wallet type, Multibanco entity/reference, Stripe payment-intent
   ids and refund amounts all appear on the page. The documents must not be reachable by anyone but the
   booking's owner.
3. **They should be cheap to serve.** A confirmation is downloaded from the confirmation page, the guest
   area and the admin, and is attached to guest emails — the same bytes, many times.

## Decision

**Naming.** Guest documents are *confirmations* and *receipts*, never invoices. Every document carries a
footer line — in the guest's language — stating *"This document is a booking confirmation/receipt and is
not a tax invoice (fatura)."* The codebase was audited for the forbidden labels (`invoice`, `fatura`,
`factura`, `facture`, `Rechnung`); the only remaining occurrences are this disclaimer and the test that
enforces its absence everywhere else. The old `IInvoiceService`/`QuestPdfInvoiceService` were removed.

**Rendering.** A shared `DocumentTheme` registers the QuestPDF Community licence (ADR 0012) and the
OFL-licensed **Fraunces** and **Inter** fonts, embedded as assembly resources so output is identical with
no CDN or file-system dependency. The guest renderer and the partner statement both draw from the theme
(palette, wordmark, fonts), so everything Gaia Skyline issues looks of a piece. All copy comes from the
`document.*` content blocks, seeded in all five languages (EN/PT/ES/FR/DE) with culture-correct dates and
amounts, and is owner-editable like any other content — English defaults render if a block is cleared.

**QR code.** Each document embeds a QR to the *magic-link request* page, prefilled with the booking
reference only. It never embeds a login token — scanning it starts the normal e-mail verification.

**Caching — database, not `IMediaStorage`.** Rendered bytes are cached in a private `BookingDocuments`
table keyed by `(reference, documentType, language)`, regenerated only when a SHA-256 fingerprint of
everything the document renders (the issue date excepted) changes — a price, a status, a refund, or an
edited label. We deliberately do **not** store these through `IMediaStorage`: that seam is write-only and
backed by the public `wwwroot/media` folder, so a receipt would sit at a guessable URL that bypasses the
booking-access cookie and leaks payment data. The database copy is private (served only through the
authenticated / cookie-gated controller actions) and readable back in-process for the e-mail attachments.

## Consequences

- Documents regenerate automatically after any booking or content change; an unchanged booking serves the
  stored bytes, even on a later day.
- Guest confirmation, refund and cancellation e-mails attach the matching branded PDF.
- Review samples for every language plus the Multibanco, refunded and partner-statement variants live in
  `docs/samples/pdfs/`.
- Document bytes live in the operational database (a few tens of KB each). If that ever becomes a concern,
  a private blob container with signed, time-limited URLs — not the public media seam — would be the move.
