# Recipe App Monorepo

A learning project for a full-stack recipe and meal-planning application. The repository currently contains an ASP.NET Core API, a React and TypeScript web app, automated tests, and product-requirements documents.

## Current Status

- The registration, login, forgot-password and reset-password pages submit through the typed auth API client. The app checks the current session on startup and routes authenticated users to a temporary landing page with logout.
- The API implements registration, login (secure session cookie, 14-day sliding expiry), `/me`, logout, account lockout (5 failures locks the account for 15 minutes, `423` Problem Details) and password reset (hashed, single-use, one-hour tokens delivered through an `IEmailSender`).
- The API also persists user-owned ingredient categories and departments and serves authenticated supermarket/store reference data from seeded records.
- Ingredient Maintenance now provides authenticated, paged ingredient list and detail APIs, server-side search, archive filtering, and responsive desktop-table/mobile-card list views.
- The [standalone Firestore ingredient migration tool](tools/FirestoreMigration/README.md) defaults to dry-run, retains explicit invalid-item classifications, and applies individually approved updates atomically.
- The [database and referenced-image recovery tool](tools/RecipeBackup/README.md) supports verified export and isolated empty-target restore, with disposable PostgreSQL recovery tests. An [encrypted manual GitHub backup workflow](tools/RecipeBackup/AUTOMATION.md) is implemented; repository configuration and a live recovery rehearsal are still required before enabling weekly scheduling.
- xUnit unit tests and Reqnroll component scenarios (against a Testcontainers PostgreSQL database) cover authentication, lookup and ingredient read API behavior. Playwright specs cover authentication flows and the responsive ingredient list against mocked APIs.
- GitHub Actions runs all of the above on pull requests unless every changed path is under `docs/` (see [Continuous Integration](#continuous-integration)).

## Repository Structure

```text
RecipeApp.slnx
.env.example
compose.yaml
.github/workflows/                      CI workflow
apps/
  api/
    src/RecipeApp.Api/                  ASP.NET Core API (.NET 10)
    tests/RecipeApp.Api.UnitTests/      xUnit unit tests
    tests/RecipeApp.Api.ComponentTests/ Reqnroll API component scenarios
  web/
    src/                                React and TypeScript app
    tests/e2e/                          Playwright browser tests
docs/prd/                               Product requirements documents
tools/FirestoreMigration/               Temporary ingredient migration console tool
tools/RecipeBackup/                     Database and referenced-image recovery tool
tests/performance/                      Reserved for performance tests
```

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Chromium for Playwright browser tests
- Docker Desktop (required for local PostgreSQL and API component tests)

The API's local database and the component-test databases are separate. Reqnroll scenarios start and remove their own PostgreSQL containers; they do not touch the persistent local database.

Create a local environment file from the template, then replace the sample password with a local-only value:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

If port 5432 is already in use, change `POSTGRES_PORT` in `.env` to a free local port and use that port in the User Secrets connection string.

Start PostgreSQL. Compose publishes it only on localhost and stores data in a named volume:

```powershell
docker compose up -d postgres
docker compose ps
```

Initialize .NET User Secrets once, then store the connection string using the same values as `.env`:

```powershell
dotnet user-secrets init --project apps/api/src/RecipeApp.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=recipe_app;Username=recipe_app;Password=replace-this-with-a-local-password" --project apps/api/src/RecipeApp.Api
```

Replace the example password in the command with the one in `.env`. Then restore the local EF CLI tool, apply the migration, and run the API:

```powershell
dotnet tool restore
dotnet ef database update --project apps/api/src/RecipeApp.Api --startup-project apps/api/src/RecipeApp.Api
dotnet run --project apps/api/src/RecipeApp.Api
```

Stop the database with `docker compose down`; the named volume keeps its data for next time. Don’t remove the volume unless you intend to erase the local database.

Create future schema migrations with:

```powershell
dotnet ef migrations add AddFeatureName --project apps/api/src/RecipeApp.Api --startup-project apps/api/src/RecipeApp.Api
```

Ingredient import preparation uses this persistent local database, not the temporary PostgreSQL containers used by component tests. Register the destination account through the app's normal registration flow before importing, then use that account's ID from `/api/auth/me`; an ID from the old Firestore app is not a destination owner ID. Apply schema changes explicitly with EF migrations before running an import—the application and import tool do not silently migrate the database.

This local setup is not a production deployment. Production database and API/web hosting, restricted connectivity, production secret injection, backup/restore verification, and deployment procedures must be completed as separate work before production cutover. Importing the local data does not depend on production hosting.

## Run Locally

### Neon development services

For PRD04 [issue #55](https://github.com/lhjenner/recipe-app-monorepo/issues/55),
Neon can supply both Postgres and private object storage while the API and web
app still run locally. This does **not** host the ASP.NET Core API on Neon.
The linked branch named `production` is currently used for development only.
[neon.ts](neon.ts) declares only the private `recipe-images-dev` bucket and
disables managed Neon Auth; the app keeps its existing authentication.

Use Node.js 22.23 or newer and install root tooling with `npm ci`.
Authenticate using `neon login` (finish the browser sign-in), then inspect the
linked project/branch in the ignored `.neon` file before running these commands:

```powershell
neon config plan
neon deploy
npm run db:migrate
npm run dev:api
```

`config plan` previews infrastructure changes. `deploy` applies the policy and
pulls credentials into the existing ignored root `.env` (otherwise `.env.local`).
Existing non-Neon entries are preserved. The npm scripts expect `.env`; if Neon
created `.env.local`, rename it to `.env` without overwriting an existing file.
Do not commit either environment file or paste their contents into logs/chat.
The launcher explicitly passes the loaded environment to .NET and rejects
missing Neon variables rather than silently using the local database.

`db:migrate` applies the existing EF migrations using `DATABASE_URL_UNPOOLED`;
`dev:api` uses the pooled `DATABASE_URL` with certificate-verified TLS.
The API otherwise retains its existing `ConnectionStrings:DefaultConnection`
configuration when `DATABASE_URL` is absent, so the Docker/User Secrets setup
above remains available. No database or image import happens automatically.
In hosted environments inject secrets as environment variables or through a
secret manager; the API does not parse environment files itself.

In another terminal, `npm run dev:web` starts Vite in `neon` mode and proxies
`/api` to local HTTPS port 7209. This development-only proxy accepts the local
ASP.NET certificate; it does not change Neon TLS verification. The original
frontend command still proxies to HTTP port 5105. For direct HTTPS testing,
`dotnet dev-certs https --trust` trusts the local ASP.NET development certificate
and may request Windows confirmation.

With the API running, `npm run smoke:neon` checks live database migrations,
registration/login/session persistence, .NET private-object writes/reads,
denial of anonymous bucket reads, and authenticated image delivery with checksum,
private caching and conditional-request checks. It uses only uniquely named
synthetic data and removes its account/recipe/association/object in `finally`.
It logs out to remove the test session; cleanup failures are errors.
This explicit manual cloud rehearsal is **not** part of the credential-free
automated test suite. An API/storage outage can require cleanup/retry; do not
run it against a real production environment.

The image route is `GET /api/recipes/{guid}/image`; there is no public upload
endpoint, presigned browser URL, real-image local mirror, or Recipe View UI yet.
Build both apps with `npm run build`. Recovery/export automation, free-plan
quota/exhaustion verification and restoration with matching database/image
associations remain unverified: #55 and AC-40/41 are **not complete**. Keep the
legacy image collection until migration and recovery have been demonstrated.

Run backend tests from the repository root:

```powershell
dotnet test
```

Install frontend dependencies from the repository root:

```powershell
npm --prefix apps/web install
```

Run the web app, browser tests, build, or lint from the repository root:

```powershell
npm --prefix apps/web run dev
npm --prefix apps/web run test:e2e
npm --prefix apps/web run build
npm --prefix apps/web run lint
```

Install Playwright's Chromium browser once, from `apps/web`:

```powershell
cd apps/web
npx playwright install chromium
```

## Continuous Integration

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every pull request:

- **Docs-only detection:** if every changed path is under `docs/`, the test/build jobs and summary comment are skipped. Changes outside `docs/` (including root READMEs and workflow files), or renames crossing that boundary, run all checks. Detection uses the whole PR, not just its latest commit; incomplete or failed detection also runs all checks.
- **Backend:** xUnit unit tests and Reqnroll component tests, with `.trx` reports.
- **Web:** `npm ci`, lint, and build (which includes the TypeScript type-check).
- **Browser:** Playwright tests, with a JUnit report and HTML report.

PRs that run tests get a summary comment with the totals and links. The raw `.trx` files (`backend-trx`) and the Playwright report (`playwright-report`) are artifacts on the workflow run. Visual snapshots are platform-specific: commit both the `-win32` (local) and `-linux` (CI) baselines when the login layout changes.

The workflow still starts for docs-only PRs, so the three required checks keep their existing names and report as skipped rather than remaining pending. No branch-protection change is needed. Manual workflow runs always execute all checks. There is no push trigger.

## Requirements and Tracking

Requirements and acceptance criteria are in [PRD 01: Authentication & Login](docs/prd/01-authentication-and-login.md), [PRD 02: App Shell & Navigation](docs/prd/02-app-shell-and-navigation.md), and [PRD 03: Ingredient Maintenance](docs/prd/03-ingredient-maintenance.md). The PRDs link to their GitHub epic and story issues. Use the acceptance-criterion IDs to keep tests and implementation traceable to those requirements.