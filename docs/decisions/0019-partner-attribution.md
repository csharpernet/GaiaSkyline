# 0019 — Partner attribution: code at checkout wins over the referral cookie

Date: 2026-10-06 · Status: accepted · Stage 8 Part A

## Context

Influencer partners drive guests two ways: a referral link (`?ref=CODE` on any public URL) and a promo
code typed at checkout. Both can be present on one booking — a guest may click a partner's link, then type
a different partner's code — so attribution needs a deterministic rule, and the referral parameter must not
leak into search indexes.

## Decision

- `?ref=CODE` on any public GET sets a first-party `gs_ref` cookie (30 days, `SameSite=Lax`, `Secure`,
  value = the partner code), records a `PartnerClick` (landing path, UTC time, random anonymous visitor id —
  **never the IP**), then **301-redirects to the same URL without `ref`**. The parameter therefore never
  appears in a canonical URL, in the sitemap, or in an indexable response; canonical generation already
  ignores query strings by construction.
- At booking creation, **a promo code typed at checkout wins over the cookie**. The typed code is a
  deliberate, latest-in-time expression of who referred the guest; the cookie is a passive 30-day trail.
  The cookie attributes only bookings made without a partner code.
- One `PartnerAttribution` row per booking (unique index), with `Source = Code | Cookie`, recorded at
  creation time so cancellation/refund handling never re-attributes.
- Codes of suspended partners neither set cookies nor attribute; unknown codes are ignored silently.

## Consequences

- A guest who clicks partner A's link but types partner B's code credits partner B — accepted; the typed
  code is the stronger signal.
- The 301 adds one redirect to referral landings; the page itself stays cacheable because the `ref` variant
  is never served or cached.
- Clicks are countable per partner (daily, unique anonymous visitors) with no personal data stored.
