# ai-persona-demo — Northline Retail

Minimal .NET 8 Web API + React 18 app used to demo the `batch-remediate` skill against a Jira backlog.

## Run it

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| React frontend | http://localhost:3000 |
| API + Swagger | http://localhost:8080/swagger |
| SQL Server | `localhost,1433` (sa / `Northline!Dev123`) |

The API creates and seeds the `Northline` database on startup. On Apple Silicon, enable
"Use Rosetta for x86_64/amd64 emulation" in Docker Desktop (SQL Server is amd64-only).

## Build and test

```bash
dotnet build
dotnet test
```

Tests use the EF Core in-memory provider, so no database is needed. CI runs both on every push.

## Layout

```
src/Northline.Api/          API: Customers, Orders, Products controllers, EF Core, seed data
tests/Northline.Api.Tests/  MSTest: happy path + one failure case per controller
frontend/                   React 18 (Vite), served by nginx, proxies /api to the API
BACKLOG.md                  Five Jira-style stories (NL-1 … NL-5) for the demo
references/standards.md     Coding standards the skill enforces
.claude/                    batch-remediate config and skill
```
