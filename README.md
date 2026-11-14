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

## Live Jira intake

The Northline Retail demo tenant is configured as `JIRA_PROJECT=NORTHLINE`; its live
Jira project key is `KAN` (`TICKET_PROJECT`). The low-stock endpoint story is `KAN-4`.
The Atlassian account email is `mitchell@aipersona.dev`. No real token belongs in config or Git.

On macOS, with only the API token currently on the clipboard, run this in the terminal that
will launch Claude Code (do not paste the token into a command or enable shell tracing):

```bash
set +x
export JIRA_API_TOKEN="$(pbpaste)"
python3 .claude/skills/batch-remediate/assets/jira_ticket.py KAN-4
claude
```

In Claude Code, run `/batch-remediate KAN-4`. For an interactive hidden shell entry instead
of the clipboard (macOS zsh or bash):

```bash
set +x
printf 'Jira API token: '
read -r -s JIRA_API_TOKEN
printf '\n'
export JIRA_API_TOKEN
```

Run the adapter without an issue key to select the oldest Story in the project, or set
`export JIRA_ISSUE_KEY=KAN-4`. It calls Jira REST v3 and emits JSON containing `source`,
`key`, `summary`, `description`, and `acceptance_criteria`. It reads the token only from
the environment; `${JIRA_API_TOKEN}` in the config is a placeholder, not shell code.
Environment variables must be set before launching Claude Code; an already-running instance
will not inherit later shell exports.

Jira failure (including absent/expired credentials) produces a warning and an explicitly
marked `source: backlog` result containing BACKLOG.md. This is offline data, not a successful
read of KAN-4; the skill must not write back to Jira in fallback mode. Missing acceptance
criteria in a successful Jira response never trigger a backlog substitution.
The adapter does not transition or modify issues. Raw REST calls through
`bash .claude/skills/batch-remediate/assets/jira.sh METHOD PATH [BODY_FILE]`
fail closed with no fallback and do not follow redirects.

When done, `unset JIRA_API_TOKEN`. Clear the clipboard separately if it still holds the token.
Adapter tests require only Python 3 and do not use network access or real credentials:

```bash
python3 -m unittest discover -s tests/jira_adapter -v
```
