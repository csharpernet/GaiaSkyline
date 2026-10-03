# 0011 — Stripe on checkout: CSP, Payment Element, page rules

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

The public site runs under a strict CSP (ADR 0008) and `payment=()` Permissions-Policy. Stripe's
Payment Element loads `js.stripe.com`, talks to `api.stripe.com`, renders `js.stripe.com`/
`hooks.stripe.com` iframes, and Apple Pay / Google Pay need the Payment Request API — all blocked by
the baseline policy. We also must keep `/book` fast and indexable while the payment step is private.

## Decision

- **Scope Stripe allowances to the payment pages only.** `SecurityHeadersMiddleware` detects
  `/book/checkout` and `/book/confirmation` and, only there, adds `script-src https://js.stripe.com`,
  `frame-src https://js.stripe.com https://hooks.stripe.com`, `connect-src https://api.stripe.com`
  and `Permissions-Policy: payment=(self "https://js.stripe.com")`. Every other page keeps the strict
  baseline (and `frame-src 'none'`).
- **`/book` is public, indexable, and loads no Stripe JS** — only the calendars (Flatpickr) + the
  quote/availability APIs — so its SEO and Core Web Vitals budgets hold. Stripe.js loads on
  `/book/checkout` only.
- **Checkout and confirmation are `noindex` and never output-cached** (`OutputCache(NoStore)`), and the
  layout skips canonical/hreflang when `PageMeta.NoIndex` is set.
- **Checkout flow:** the guest fills the form, the Payment Element is mounted in deferred mode with the
  re-quoted amount; on submit the server re-quotes (never trusting the client price), creates the
  booking + PaymentIntent, and returns the client secret; the Payment Element confirms and Stripe
  redirects to the confirmation page. The webhook remains the source of truth (ADR 0010).
- **Confirmation links require a signed token** (data-protection) in addition to the human reference,
  so they can't be enumerated.
- **Security:** antiforgery on the checkout POST (token sent as a header by the fetch), FluentValidation
  server-side, and per-IP rate limits (checkout 5 / 10 min, quote 60 / min). The secret key never
  reaches the client; no card data touches our server.

## Consequences

- **+** The strict baseline CSP is preserved everywhere except the two pages that genuinely need Stripe.
- **+** `/book` stays SEO-100 / within CWV budgets; the payment surface is private and uncacheable.
- **−** Multibanco exclusion for last-minute (<10-day) stays is enforced on the server PaymentIntent;
  in deferred Elements mode the method list isn't filtered client-side, so a last-minute guest could
  see Multibanco and have confirmation fail. Acceptable (rare; Multibanco needs lead time anyway) and
  revisited if it matters.
