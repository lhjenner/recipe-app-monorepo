# Recipe App Monorepo

A learning project for a full-stack recipe and meal-planning application. The repository currently contains an ASP.NET Core API scaffold, a React and TypeScript web app, automated tests, and the authentication PRD.

## Current Status

- The registration page validates input and submits to the API. The login page has client-side validation, but its submit flow is not connected to the login API yet.
- The API registration endpoint persists users through EF Core/PostgreSQL and hashes passwords with BCrypt. Login verifies credentials, stores its authentication ticket server-side, and issues an HttpOnly, Secure, SameSite cookie with a 14-day sliding expiration. `GET /api/auth/me` returns the current user; logout revokes the server-side ticket.
- API component tests use Reqnroll/Gherkin on the xUnit runner. Each auth scenario starts an isolated PostgreSQL container with Testcontainers and calls the API through `WebApplicationFactory`.
- The scenarios cover registration persistence and BCrypt hashing, duplicate-email `409` Problem Details, validation, login, cookie security, `/me`, and logout revocation.
- Running the API locally uses a persistent PostgreSQL container managed by Docker Compose. Component tests use separate disposable Testcontainers databases.

## Repository Structure

```text
RecipeApp.slnx
.env.example
compose.yaml
apps/
  api/
    src/RecipeApp.Api/                  ASP.NET Core API (.NET 10)
    tests/RecipeApp.Api.UnitTests/      xUnit unit tests
    tests/RecipeApp.Api.ComponentTests/ Reqnroll API component scenarios
  web/
    src/                                React and TypeScript app
    tests/e2e/                          Playwright browser tests
docs/prd/                               Product requirements documents
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

## Run Locally

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

## Requirements and Tracking

Authentication requirements and acceptance criteria are in [PRD 01: Authentication & Login](docs/prd/01-authentication-and-login.md). The PRD links to its GitHub epic and story issues. Use the acceptance-criterion IDs to keep tests and implementation traceable to those requirements.