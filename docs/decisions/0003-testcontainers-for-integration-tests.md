# 0003 — Testcontainers for integration tests (no in-memory provider)

- Status: Accepted
- Date: 2026-09-29
- Deciders: GaiaSkyline maintainers

## Context

Integration tests for the persistence layer must exercise behaviour we actually depend on:
unique constraints, transactions, real SQL types (`time`, `nchar`, `uniqueidentifier`) and
provider-specific translation. The EF Core in-memory provider does **not** enforce unique indexes
or relational semantics, so a green in-memory test can hide a real bug.

## Decision

Use **Testcontainers for .NET** to spin up a real **SQL Server 2022** container per test run
(`mcr.microsoft.com/mssql/server:2022-latest`), apply migrations, and run assertions against it.

- A `SqlServerContainerFixture` (`IAsyncLifetime`) starts the container and exposes its connection
  string.
- Tests are `[SkippableFact]`: when Docker is unavailable locally they **skip** (with a reason)
  instead of failing — **except under CI** (the `CI` env var is set), where a start failure is
  allowed to surface so integration coverage cannot silently vanish.
- CI runs on a runner with Docker available, so these tests execute for real on every push.

We explicitly rejected the in-memory and SQLite providers for persistence tests because they do
not honour the constraints and types this project relies on.

## Consequences

- **+** High-fidelity tests against the same engine used in production (Azure SQL / SQL Server).
- **+** Local runs stay green without Docker; CI still gets full coverage.
- **−** Requires Docker for the full test suite locally and a Docker-capable CI runner.
- **−** Slower than in-memory (container start + image pull on cold cache). Acceptable for the
  confidence gained; the container is shared across a test class via the fixture.
