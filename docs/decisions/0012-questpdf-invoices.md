# 0012 — Invoices with QuestPDF (Community licence)

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

Confirmed bookings need a branded PDF invoice, linked from the confirmation page and email. We want a
.NET-native PDF library with a clean, free licence for a small business.

## Decision

Use **QuestPDF** under its **Community licence**. QuestPDF is MIT-with-a-Community-tier model: the
Community licence is free for individuals and organisations with **annual gross revenue under US $1M**
(and non-profits/FOSS). GaiaSkyline is a single short-term rental well under that threshold, so the
Community licence applies. We set `QuestPDF.Settings.License = LicenseType.Community` at startup, as the
licence requires. If revenue ever crosses the threshold, a paid Professional/Enterprise licence is
required — tracked here.

- `IInvoiceService` (Application) → `QuestPdfInvoiceService` (Infrastructure) builds the invoice from the
  booking read model (reference, guest, dates, line items, total) and returns PDF bytes. It only
  produces an invoice for a **paid** booking (Confirmed/CheckedIn/Completed/Refunded/PartiallyRefunded).
- Served on demand at `/{lang}/book/confirmation/{ref}/invoice.pdf`, gated by the same signed token as
  the confirmation page, and linked from the confirmation page (and the confirmation email).
- Storage goes through **`IMediaStorage`** — now implemented as `LocalDiskMediaStorage` (wwwroot/media)
  for dev; Azure Blob replaces it in Stage 8 (ADR 0006). The invoice is generated on demand; a
  persistent stored copy/retention policy is finalised with Blob in Stage 8.

## Consequences

- **+** Free, high-quality, .NET-native PDF generation; no external service.
- **+** Invoices are token-gated (not enumerable) and only exist for paid bookings.
- **−** The Community licence is revenue-gated; revisit if GaiaSkyline's revenue approaches US $1M.
- **−** On-demand generation re-renders per request; acceptable (invoices are small and rarely fetched),
  and a cached/stored copy can be added with the Blob storage in Stage 8.
