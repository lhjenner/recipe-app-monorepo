# Recipe App Monorepo

A learning project for a full-stack recipe and meal-planning application. The repository currently contains an ASP.NET Core API scaffold, a React and TypeScript web app, automated tests, and the authentication PRD.

## Current Status

- The login page implements client-side form validation for AC-01 to AC-03. Login is not connected to an API yet.
- The API registration endpoint persists users through EF Core/PostgreSQL and hashes passwords with BCrypt. Login is not connected to an API yet.
- API component tests use Reqnroll/Gherkin on the xUnit runner. Each registration scenario starts an isolated PostgreSQL container with Testcontainers and calls the API through `WebApplicationFactory`.
- The registration scenarios cover successful persistence and BCrypt hashing, duplicate-email `409` Problem Details, and malformed input validation.
- Running the API against a persistent local database requires a PostgreSQL connection string supplied through .NET User Secrets or an environment variable. Component tests provide their own container connection string.

## Repository Structure

```text
RecipeApp.slnx
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
- Docker Desktop (required for PostgreSQL-backed API component tests)

The component test suite starts and removes its own PostgreSQL containers; no manually running test database is needed.

To run the API against a persistent local PostgreSQL instance, initialize User Secrets and set its connection string:

```powershell
dotnet user-secrets init --project apps/api/src/RecipeApp.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=recipe_app;Username=postgres;Password=<local-password>" --project apps/api/src/RecipeApp.Api
```

The API project includes an EF Core migration. Restore the repository-local EF CLI tool with `dotnet tool restore` before running migration commands.

After configuring a local PostgreSQL connection string, restore the tool and apply migrations with:

```powershell
dotnet tool restore
dotnet ef database update --project apps/api/src/RecipeApp.Api --startup-project apps/api/src/RecipeApp.Api
```

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