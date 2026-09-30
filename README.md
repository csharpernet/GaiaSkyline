# GaiaSkyline

Direct-booking website for a single short-term rental in **Vila Nova de Gaia, Portugal**.

This repository is **Stage 1 of 8** — the foundations. It boots to a placeholder landing page
and establishes the architecture, persistence, tests, security posture and CI that later stages
build on. There are no booking features yet.

---

## Tech stack

| Concern            | Choice                                                            |
| ------------------ | ----------------------------------------------------------------- |
| Framework          | ASP.NET Core MVC on **.NET 10**, C# 13                            |
| Architecture       | Clean Architecture (Web → Application → Domain; Infrastructure implements) |
| Persistence        | EF Core 10 (code-first, checked-in migrations), **SQL Server**    |
| Background jobs     | Hangfire (wired up, dormant by default)                          |
| Styling            | Tailwind CSS v3, compiled at build time (see [ADR 0001](docs/decisions/0001-tailwind-build-strategy.md)) |
| Logging            | Serilog → console (App Insights placeholder)                      |
| Tests              | xUnit · FluentAssertions · Moq · Bogus · Testcontainers (SQL Server 2022) |
| CI                 | GitHub Actions                                                    |

---

## Get productive in under 15 minutes

### 1. Prerequisites

- **.NET 10 SDK** — https://dotnet.microsoft.com/download (the repo pins the version in `global.json`)
- **Docker Desktop** — required to run the app via compose and to run the integration tests
- **Node.js 20 LTS** — https://nodejs.org (build-time only, for the Tailwind CSS step)

Check them:

```bash
dotnet --version   # 10.0.x
docker --version
node --version     # v20.x
```

### 2. Clone & restore

```bash
git clone <your-fork-url> GaiaSkyline
cd GaiaSkyline
dotnet tool restore     # restores the local dotnet-ef tool
dotnet restore
```

### 3. Build

```bash
dotnet build -c Release
```

The build compiles Tailwind automatically (an MSBuild target runs `npm install` + `npm run
build:css` in `src/GaiaSkyline.Web`). The build treats warnings **and analyzer diagnostics** as
errors — a clean build has zero warnings.

### 4. Test

```bash
dotnet test
```

- **Unit tests** (Domain, Application, Web) run everywhere.
- **Integration tests** (`GaiaSkyline.Infrastructure.Tests`) start a real **SQL Server 2022**
  container via Testcontainers. Without Docker running they **skip** (they do not fail); with
  Docker they execute for real. See [ADR 0003](docs/decisions/0003-testcontainers-for-integration-tests.md).

### 5. Run the site

**Option A — everything in Docker (SQL Server + web):**

```bash
docker compose up --build
```

Open **http://localhost:8080** → the placeholder page renders **“Gaia Skyline”** in Fraunces on
the stone background.

**Option B — just the web app locally** (uses SQL Server LocalDB from `appsettings.json`):

```bash
dotnet run --project src/GaiaSkyline.Web
```

The landing page and `/health/live` work without a database. `/health/ready` reports the SQL
Server dependency and only turns healthy once a database is reachable.

Front-end iteration (live Tailwind rebuilds):

```bash
cd src/GaiaSkyline.Web && npm run watch:css
```

---

## Solution layout

```
GaiaSkyline.sln
├─ src/
│  ├─ GaiaSkyline.Web             ASP.NET Core MVC (composition root, security, health, Serilog)
│  ├─ GaiaSkyline.Application     use-cases, DTOs, MediatR, FluentValidation
│  ├─ GaiaSkyline.Domain          entities, value objects, domain events (no dependencies)
│  ├─ GaiaSkyline.Infrastructure  EF Core, migrations, external services
│  └─ GaiaSkyline.BackgroundJobs  Hangfire jobs (empty for now, wired up)
├─ tests/
│  ├─ GaiaSkyline.Domain.Tests
│  ├─ GaiaSkyline.Application.Tests
│  ├─ GaiaSkyline.Infrastructure.Tests   (Testcontainers)
│  └─ GaiaSkyline.Web.Tests              (WebApplicationFactory)
├─ docs/decisions/                ADRs (numbered)
└─ .github/workflows/ci.yml
```

**Dependency rule:** `Web → Application → Domain`. `Infrastructure` implements interfaces defined
in the inner layers. **EF Core types never leak** into Application or Domain.

---

## Endpoints

| Path            | Purpose                                                      |
| --------------- | ----------------------------------------------------------- |
| `/`             | Placeholder landing page                                    |
| `/health/live`  | Liveness — is the process up (no dependency checks)          |
| `/health/ready` | Readiness — dependencies (SQL Server) are reachable          |

---

## Security posture (Stage 1)

- HSTS (non-dev), HTTPS redirection
- Content-Security-Policy with a **per-request nonce** (`SecurityHeadersMiddleware`)
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
  `X-Frame-Options: DENY`, locked-down `Permissions-Policy`
- Antiforgery configured; cookies `SameSite=Lax`, `Secure` in production

---

## Design tokens

Defined once, exposed two ways so a Razor partial can use either:

- **Tailwind utilities** via `src/GaiaSkyline.Web/tailwind.config.js` (e.g. `bg-stone`, `font-display`)
- **CSS variables** via `src/GaiaSkyline.Web/wwwroot/css/tokens.css` (e.g. `var(--color-stone)`)

Palette: ink `#0F1417` · stone `#F5F1EA` · clay `#B85D3A` · river `#2E4F60` · fog `#D9D2C5` ·
white `#FFFFFF`. Fonts: Fraunces (display) + Inter (body), variable, `display=swap`, preloaded.
Radii 6/12/24 px · 4px spacing grid · motion 200–300 ms ease-out · respects
`prefers-reduced-motion`.

---

## Database & migrations

```bash
# add a migration
dotnet dotnet-ef migrations add <Name> --project src/GaiaSkyline.Infrastructure

# apply migrations to a database
dotnet dotnet-ef database update --project src/GaiaSkyline.Infrastructure
```

The initial migration `0001_Init` creates the `Properties` table. A design-time
`AppDbContextFactory` lets the EF tools run without booting the web host.

---

## Continuous integration

`.github/workflows/ci.yml` runs on every push/PR:

restore → build (analyzers/warnings as errors) → `dotnet format --verify-no-changes` → unit tests
→ integration tests (Testcontainers) → coverage report (Coverlet + ReportGenerator) → vulnerable
package scan (`dotnet list package --vulnerable`, fails on High/Critical).

---

## Architecture decisions

- [0001 — Tailwind build strategy](docs/decisions/0001-tailwind-build-strategy.md)
- [0002 — Strongly-typed identifiers](docs/decisions/0002-strongly-typed-ids.md)
- [0003 — Testcontainers for integration tests](docs/decisions/0003-testcontainers-for-integration-tests.md)
