# Recipe & Grocery List Monorepo

A modern, containerized full-stack application for managing recipes and automatically compiling aggregate grocery lists. Built with a Clean Architecture C# API, a TypeScript web frontend, and containerized local development workflows.

---

## 🏗 System Architecture & Monorepo Structure

This project is organized as a monorepo containing application services, automated testing suites, infrastructure configuration, and technical specifications:

<pre>
recipe-app-monorepo/
├── apps/
│   ├── api/                        # .NET 8 Web API
│   │   ├── src/                    # Application & Domain Logic (Clean Architecture)
│   │   └── tests/                  # Backend Automated Test Suites
│   │       ├── Api.UnitTests/      # xUnit Unit Tests (Fast, Isolated)
│   │       └── Api.ComponentTests/ # Reqnroll BDD Feature Specs (Gherkin)
│   │
│   └── web/                        # TypeScript Web Application
│       ├── src/                    # Application & UI Components
│       └── tests/                  # Frontend Automated Test Suites
│           ├── e2e/                # Playwright Browser Automation (TypeScript)
│           └── unit/               # Component & Helper Unit Tests
│
├── tests/
│   └── performance/                # Apache JMeter (.jmx) Load & Stress Tests
│
├── docs/
│   └── prd/                        # Product Requirements Documents (Docs-as-Code)
│
├── .github/                        # Copilot Rules & CI/CD Actions Workflows
├── scripts/                        # Local Dev & Automation PowerShell Scripts
├── docker-compose.yml              # Local Multi-Container Setup (API + PostgreSQL)
└── README.md
</pre>


## 🛠 Tech Stack & Tooling

* **Backend:** C# / .NET 8 Web API, Entity Framework Core, PostgreSQL
* **Frontend:** TypeScript, Modular CSS Architecture
* **Testing & Quality Assurance:**
  * **Unit & Integration:** xUnit, FluentAssertions, Moq
  * **Component & BDD:** Reqnroll (Gherkin feature specs)
  * **End-to-End (E2E):** Playwright (TypeScript)
  * **Performance & Load:** Apache JMeter
* **DevOps & Infrastructure:** Docker, Docker Compose, PowerShell, GitHub Actions CI/CD


## 🚀 Quickstart & Local Development

### Prerequisites
- Docker Desktop
- .NET 8 SDK
- Node.js & pnpm
- PowerShell 7+

### Environment Setup
1. Clone the repository:
   <pre>git clone https://github.com/YOUR_USERNAME/recipe-app-monorepo.git
   cd recipe-app-monorepo</pre>

2. Configure Environment Variables:
   Copy the .env.example template to create your local environment file:
   <pre>cp .env.example .env </pre>

3. Orchestrate Local Services:
   Run the PowerShell setup script to launch local containerized services (API & PostgreSQL database):
   <pre>./scripts/start-dev.ps1</pre>

---

## 🧪 Running Automated Tests

### Backend Automation
- Unit Tests (xUnit):
  <pre>dotnet test apps/api/tests/Api.UnitTests</pre>
- BDD Component Tests (Reqnroll):
  <pre>dotnet test apps/api/tests/Api.ComponentTests</pre>

### Frontend Automation
- E2E Browser Tests (Playwright):
  <pre>cd apps/web && pnpm test:e2e</pre>

### Performance & Load Testing
- JMeter Load Execution:
  <pre>jmeter -n -t tests/performance/api-load-test.jmx -l tests/performance/results.jtl</pre>

---

## 📄 Product Requirements & Specifications

All product requirements, user stories, and acceptance criteria are managed using a Docs-as-Code approach and version-controlled under `/docs/prd/`. Feature implementation progress is tracked via GitHub Issues and GitHub Projects.