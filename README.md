# Recipe App Monorepo

A learning project for a full-stack recipe and meal-planning application. The repository currently contains an ASP.NET Core API scaffold, a React and TypeScript web app, automated tests, and the authentication PRD.

## Current Status

- The login page implements client-side form validation for AC-01 to AC-03. Login is not connected to an API yet.
- The API has a registration endpoint and service logic for duplicate checking, but its tests inject substitute repository and password-hasher implementations. Production persistence, password hashing, and account registration are not wired up yet.
- EF Core migrations, PostgreSQL, Docker Compose, and Reqnroll BDD tests are planned, but are not configured in the repository yet.
- The current API component tests use xUnit and `WebApplicationFactory` with NSubstitute.

## Repository Structure

```text
RecipeApp.slnx
apps/
  api/
    src/RecipeApp.Api/                  ASP.NET Core API (.NET 10)
    tests/RecipeApp.Api.UnitTests/      xUnit unit tests
    tests/RecipeApp.Api.ComponentTests/ xUnit HTTP component tests
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

Docker will be needed when the planned PostgreSQL-backed development and test environments are added.

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