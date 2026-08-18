# ATIP — Autonomous AI Test Intelligence Platform

An enterprise, AI-first platform that behaves like an autonomous QA engineer:
upload product documentation, point it at your application, and let AI agents
explore the app, generate scenarios, build a reusable knowledge graph, and run
self-healing tests — without manual locator maintenance.

> **Status:** Two vertical slices are complete and runnable end-to-end:
> 1. **Foundation** — multi-tenant auth, project & environment management, Clean
>    Architecture backend (.NET 10), React 19 enterprise UI.
> 2. **AI vertical** — Requirement Intelligence (document upload + AI extraction of
>    modules/features/user stories), AI Scenario Generation, and an interactive
>    Knowledge Graph. Powered by a LiteLLM (OpenAI-compatible) adapter that falls
>    back to a deterministic offline mock, so it runs with **no API key**.
>
> The remaining modules (Playwright MCP Explorer, Element Discovery, Execution,
> Self-Healing, Reporting, Agent framework) build on these same patterns.

---

## Architecture

Clean Architecture with CQRS (MediatR). Dependencies point inward only.

```
src/
  ATIP.Domain          # Entities, enums, domain rules (no dependencies)
  ATIP.Application     # CQRS commands/queries, DTOs, validators, interfaces
  ATIP.Infrastructure  # EF Core (PostgreSQL), identity, security, persistence
  ATIP.Api             # ASP.NET Core Web API, controllers, auth, Swagger
tests/
  ATIP.Application.Tests
frontend/              # React 19 + TypeScript + Vite + MUI + React Query + Zustand
```

Key backend building blocks:

- **Multi-tenancy** — every tenant-scoped entity is filtered by a global EF Core
  query filter; tenants cannot see each other's data.
- **Soft delete** — a `SaveChanges` interceptor converts deletes to soft deletes
  and stamps audit columns automatically.
- **Auth** — JWT bearer tokens carrying `sub`, `tenant_id` and role claims;
  BCrypt password hashing; RBAC-ready (`SystemRole` + project-scoped `ProjectRole`).
- **Secrets** — credential secrets are encrypted at rest with AES-256-GCM and a
  rotatable key id (envelope encryption).
- **Validation** — FluentValidation runs in a MediatR pipeline behavior and maps
  to RFC 7807 problem details.

## Tech stack

| Layer     | Technology |
|-----------|------------|
| Backend   | .NET 10, ASP.NET Core, MediatR, FluentValidation, EF Core, Serilog |
| Database  | PostgreSQL |
| Frontend  | React 19, TypeScript, Vite, MUI, TanStack Query, Zustand, React Router |
| AI (planned) | LiteLLM (OpenAI-compatible) adapter, Semantic Kernel |
| Automation (planned) | Playwright, Playwright MCP |

---

## Prerequisites

- .NET SDK 10.x
- Node.js 20+ and npm
- PostgreSQL 14+ (local) **or** Docker

## Getting started

### 1. Start PostgreSQL

**Option A — Docker (recommended for a clean environment):**

```bash
docker compose up -d
# Postgres is exposed on host port 5433 to avoid clashing with a local install.
# Update ConnectionStrings:Postgres to Port=5433 if you use this option.
```

**Option B — existing local PostgreSQL** (default connection uses port 5432):

```bash
psql postgres -c "CREATE ROLE atip LOGIN PASSWORD 'atip';"
psql postgres -c "CREATE DATABASE atip OWNER atip;"
```

### 2. Run the backend

```bash
dotnet run --project src/ATIP.Api
```

- API: http://localhost:5080 (Swagger UI at `/swagger`)
- Migrations are applied automatically on startup in Development.

Development secrets (JWT signing key + AES key) live in
`src/ATIP.Api/appsettings.Development.json`. **Replace these in any shared or
production environment** and source them from a secret store.

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

- App: http://localhost:5173 (proxies `/api` and `/health` to the backend).

### 4. Try it

1. Open http://localhost:5173/register and create an organization.
2. Create a project; a URL-safe key is generated automatically.
3. Open the project and add an environment with your application's base URL.

---

## API overview

All endpoints are versioned under `/api/v1`. Authenticate with
`Authorization: Bearer <token>` obtained from register/login.

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/auth/register` | Create tenant + first admin, returns JWT |
| POST | `/auth/login` | Authenticate, returns JWT |
| GET  | `/projects` | Paged, searchable, sortable project list |
| POST | `/projects` | Create project (caller becomes Owner) |
| GET  | `/projects/{id}` | Get a project |
| PUT  | `/projects/{id}` | Update project |
| DELETE | `/projects/{id}` | Soft-delete project |
| GET  | `/projects/{id}/environments` | List environments |
| POST | `/projects/{id}/environments` | Create environment |
| DELETE | `/projects/{id}/environments/{envId}` | Delete environment |
| GET/POST | `/projects/{id}/requirements` | List / upload (multipart) requirement docs |
| POST | `/projects/{id}/requirements/{rid}/analyze` | AI-extract modules/features/stories |
| GET  | `/projects/{id}/scenarios` | List scenarios (optionally `?featureId=`) |
| POST | `/projects/{id}/scenarios/generate?featureId=` | AI-generate scenarios for a feature |
| GET  | `/projects/{id}/knowledge-graph` | Knowledge graph nodes & edges |

## AI provider (LiteLLM / OpenAI-compatible)

By default the platform runs with a **deterministic offline mock** — no API key
required. To use a real model, point it at any OpenAI-compatible endpoint
(LiteLLM proxy, OpenAI, Azure OpenAI, etc.) via configuration:

```jsonc
// appsettings.Development.json
"Llm": {
  "BaseUrl": "http://localhost:4000/v1",   // your LiteLLM proxy
  "ApiKey": "sk-...",
  "Model": "gpt-4o-mini"
}
```

If the endpoint is unset or a call fails, the mock is used automatically so the
pipeline always works.

## Database migrations

```bash
# Requires: dotnet tool install --global dotnet-ef --version 10.0.10
dotnet ef migrations add <Name> \
  --project src/ATIP.Infrastructure \
  --startup-project src/ATIP.Api \
  --output-dir Persistence/Migrations
```

## Tests

```bash
dotnet test
```

---

## Roadmap

- Requirement Intelligence (PDF/DOCX/Swagger/Jira ingestion + AI extraction)
- AI Scenario Generation (positive/negative/boundary/security/a11y/API)
- Application Explorer via Playwright MCP (pages, DOM, a11y tree, screenshots)
- Element Discovery + semantic Locator Intelligence + Self-Healing engine
- Knowledge Graph (React Flow visualization)
- Execution engine (trace/video/network/console capture)
- AI failure analysis and interactive reporting
- Specialized agent framework (Requirement/Scenario/Planner/Explorer/Locator/
  Execution/Healing/Reporting agents)
- Docker/Kubernetes/Helm deployment and CI/CD pipelines
```
