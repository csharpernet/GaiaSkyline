# 0023 — App Service plan and SQL sizing

Date: 2026-10-06 · Status: accepted · Stage 8 Part B

## Context

Two environments: a `dev` used to validate the pipeline, Bicep and deploys, and a `prod` that serves a
single-property booking site with modest, bursty traffic (a handful of concurrent guests, occasional
checkout). We want the cheapest tier in each that still meets the functional requirements — notably
**Always On** (so Hangfire's recurring jobs run and the app does not cold-start) and a **staging slot** on
production (for zero-downtime slot-swap deploys).

## Decision

- **App Service plan.** Dev: **B1** Linux — the cheapest tier that supports Always On; no slot (dev deploys
  in place). Prod: **P0v3** Linux — the entry Premium v3 tier, which supports Always On, deployment slots
  and the Front-Door-only access restriction, at a much lower price than P1v3. If production load ever
  needs it, the plan scales up to P1v3/P2v3 with no code change; P0v3 is the documented starting point.
- **Azure SQL.** Dev: **Basic** (2 GB, enough for the schema + seed). Prod: **S1** (Standard, 20 DTU) —
  headroom for the booking/availability queries and Hangfire's SQL storage without paying for S2+. PITR is
  7 days on dev and 35 on prod, with weekly long-term retention for 8 weeks.
- **Storage.** LRS on dev, **ZRS** on prod (zone-redundant, since media and invoices are the site's durable
  content). 30-day soft delete on blobs and containers; a lifecycle rule expires `exports/` after 90 days.

## Consequences

- Prod's steady-state cost is dominated by P0v3 + SQL S1; both scale up in place if traffic grows.
- The staging slot shares the prod plan's compute, so a swap does not need extra capacity.
- Basic SQL on dev cannot run some heavier features at scale, but dev only validates correctness and the
  pipeline, not load — load is covered by the k6 smoke test against dev in Part E.
