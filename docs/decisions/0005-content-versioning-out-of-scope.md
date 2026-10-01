# 0005 — Content versioning out of scope for v1

- Status: Accepted
- Date: 2026-10-01
- Deciders: GaiaSkyline maintainers

## Context

The content model (ContentBlock + ContentTranslation) is editable by the owner from the backoffice
in five languages. A natural question is whether we keep a full version history of every edit
(who changed what, when, with rollback).

## Decision

**No content version history in v1.** We keep only the current value of each translation, plus
lightweight audit stamps (`UpdatedAtUtc`, `UpdatedBy`) on `ContentBlock` and `ContentTranslation`.
Draft/publish is modelled by the single `ContentBlock.IsPublished` flag: admin edits write new
`ContentTranslation` values without flipping publish, and the public API returns only published
blocks.

## Consequences

- **+** Much simpler schema and admin UI; no history tables, diffing or rollback to build or test.
- **+** `UpdatedAtUtc`/`UpdatedBy` still answer "when was this last touched, and by whom".
- **−** No rollback or audit trail of previous values. If the owner overwrites copy, the prior text
  is gone (mitigated operationally by database backups).
- If full versioning is needed later, it is an additive change (a history/audit table plus admin
  UI) that would supersede this ADR.
