# 0004 — No Docker; SQL Server LocalDB for integration tests

- Status: Accepted (supersedes [ADR 0003](0003-testcontainers-for-integration-tests.md))
- Date: 2026-09-30
- Deciders: GaiaSkyline maintainers

## Context

Stage 1 shipped with Docker artifacts (Dockerfile, docker-compose) and Testcontainers-backed
integration tests. In practice this project is developed on **Windows with Visual Studio 2026**,
and we want to keep the toolchain small. Requiring Docker Desktop for a normal `dotnet test` run
adds friction (install, licensing, daemon running) for no benefit we actually use.

## Decision

Adopt a **Windows-only, no-Docker workflow**:

- Remove the `Dockerfile`, `.dockerignore` and `docker-compose.yml`.
- Remove the `Testcontainers.*` packages and the SQL Server container fixture.
- Run integration tests against **SQL Server LocalDB** via a `LocalDbFixture` that, per test
  class, creates a uniquely-named database (`GaiaSkyline_Tests_{Guid:N}`), applies EF Core
  migrations (`Database.Migrate()`), and drops it on dispose. It is consumed as an
  `IClassFixture<T>`, so each class gets an isolated database while tests within a class share one.
- If LocalDB is not reachable the fixture **throws a clear, actionable error** (pointing at the
  Visual Studio Installer / `sqllocaldb.exe`). There are **no silent skips** — the integration
  tests run everywhere.
- CI moves to **`windows-latest`**, where LocalDB is pre-installed (verified with a
  `sqllocaldb info` step before the tests).

## Rationale

- **LocalDB is SQL Server.** It is the same database engine as our production target (Azure SQL /
  SQL Server) and honours the behaviour we test for — unique constraints, transactions and SQL
  types (`time`, `nchar`, `uniqueidentifier`). For our needs it behaves identically to Azure SQL.
- **Smaller toolchain.** No Docker daemon, images or compose to install and keep running. VS 2026
  already installs LocalDB.
- **Tests always run.** Removing the Docker gate removes the previous "skip when Docker is absent"
  branch, so integration coverage is never silently lost.

## Consequences

- **+** One-step onboarding on Windows; `dotnet test` runs the whole suite with no extra services.
- **+** No Docker licensing/daemon requirement.
- **−** CI is Windows-only (no Linux container parity). Acceptable for a Windows-first shop.
- **−** LocalDB is Windows-only, so contributors are effectively on Windows. This matches the
  team's reality (Visual Studio 2026).

## Notes

An important footgun surfaced during this change: `<InvariantGlobalization>true</InvariantGlobalization>`
must **not** be set — `Microsoft.Data.SqlClient` requires ICU globalization and throws
`NotSupportedException: Globalization Invariant Mode is not supported` when opening a connection.
It was removed solution-wide.
