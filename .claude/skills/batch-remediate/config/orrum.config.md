# batch-remediate engagement config — Orrum (PAIWARES) · lean

Copy to `{orrum repo root}/.claude/batch-remediate.config.md` and replace every `{fill}` value
(the run stops on the first one it finds, naming the key). `{check}` values are a best guess from
the known stack; the first run's checks fail loudly if one is wrong. No secrets in this file.

With this file the 4.0 engine runs the **lean** flow: one pass per ticket from story to a draft PR
with tests, no gates, no modes, no certification, no feature flags, evidence from the local/CI
test run. A ticket without enough information gets a comment that @mentions its owner with exactly
what is missing, and the run moves on. Jira Cloud for tickets, GitHub for PRs.

```
# ---- Core ----------------------------------------------------------------------------------------
ENGAGEMENT        = orrum
TICKET_SYSTEM     = jira
BASE_BRANCH       = {fill: develop or main}
GATES             = none
FEATURE_FLAGS     = off
BUILD_CMD         = {fill: e.g. dotnet build <api>.sln && npm --prefix frontend run build}
TEST_CMD          = {fill: e.g. dotnet test <api>.sln && npm --prefix frontend test -- --run}
STANDARDS         = {fill: repo path to Orrum's coding standards; until one exists set references/standards.md (OWASP §2 applies)}
BATCH_ROOT        = ~/orrum-batches

# ---- Engine switches: lean ----------------------------------------------------------------------
MODES             = off              # one pass per ticket: −1 → 0 → 0.25 → 0.5 → 1 → 1.5 → 2 → 3 → 3.5 → 4 → 5 → 6
EVIDENCE          = local            # green TEST_CMD on the PR head proves a requirement; ends at READY-FOR-REVIEW
GATES_SUMMARY     =                  # ignored with GATES = none
GATES_OPTIONAL    =
CERTIFICATION     = off
INTERACTIVE       = off              # never stop to ask in chat; questions go to the ticket
ENRICH_MODE       = rfi              # score the story; gaps become one comment @mentioning the owner
ENRICH_MAX_QUESTIONS = 3
ENRICH_MAX_ROUNDS    = 3
BLAST_GRAPH       = off
BLAST_LOOP_SCAN   = .claude/skills/batch-remediate/assets/blast-radius.sh

# ---- Ticket adapter: Jira Cloud ------------------------------------------------------------------
TICKET_URL        = https://{fill}.atlassian.net
TICKET_PROJECT    = {fill: project key, e.g. PAI}
AC_SOURCE         = description-heading     {check: or field}
AC_FIELD          =                         {check: customfield_NNNNN when AC_SOURCE = field}
AC_HEADING        = Acceptance Criteria
DONE_STATUSES     = Done, Closed, Won't Do
FIELD_WRITEBACK   = off              # the Jira adapter never edits the issue; enrichment lives in comments + ledger
QUEUE             = batch-auto       # label that --queue selects
NEEDS_INFO_LABEL  = batch-auto-needs-info
SKIP_LABEL        = batch-auto-skip  # never picked up by --queue
RUN_TAG           = BATCH-{yyyymmdd}

# ---- Code host: GitHub ---------------------------------------------------------------------------
CODE_HOST         = github                  {check: or ado}
CODE_REPO         = {fill: owner/repo}
REPO_ROOT         = {fill: e.g. ~/src/orrum}
WORKTREE_ROOT     = ~/src/orrum.worktrees   {check}
BRANCH_PREFIX     = feature/                # branch = feature/PAI-123-{slug}
PR_MODE           = draft
EXECUTE_PR        = none                    # no execute mode in the lean flow
REVIEWERS         = {fill: GitHub logins, comma-separated; empty = PRs stay draft}

# ---- Tests and evidence --------------------------------------------------------------------------
UNIT_FRAMEWORK    = {fill: e.g. xUnit + NSubstitute}
UNIT_FILTER       = {fill: e.g. dotnet test <api>.sln --filter "Category=TICKET-{id}"}
JS_TEST_CMD       = {fill: e.g. npm --prefix frontend test -- --run; empty if no JS harness}
TEST_CATEGORY     = TICKET-{id}
KNOWN_RED         = -
TICKET_CID        = TICKET-{id}
LOG_SINK          = {fill: trace it once - e.g. Serilog -> console -> docker logs, or App Insights}
E2E_MODE          = off
INAPP_MODE        = off
TESTPLAN_MODE     = off
RUN_SCRIPT        = off
DEPLOY_ENV        = -
PROMOTION_GATE    = -
VERIFY_CMD        =                  # empty: tickets end at MERGED for a person to confirm.
                                     # Set (with a non-production DEPLOY_CHECK_CMD) to check after merge.

# ---- Feature flags: off --------------------------------------------------------------------------
FLAG_PROVIDER     = -
FLAG_PREFIX       = -
FLAG_TOOL         = -
FLAG_FLIP_ENVS    = -

# ---- Engagement knowledge ------------------------------------------------------------------------
KNOWN_FACTS       = -                # start one once runs learn things the hard way
ENGAGEMENT_RULES  = Every new data path gets a cross-tenant test (a tenant must never read another tenant's rows; TenantAwareJWTMiddleware area).
                    Unit tests must not need the container stack (Postgres, DuckDB, ETL, app, API, frontend, data sync); stack-dependent checks go in the project's integration tests.
                    The skill never runs deploy.sh / deploy.py, Compose or K3s.
SECRET_ENV        = JIRA_BASE_URL, JIRA_EMAIL, JIRA_API_TOKEN (environment or ~/.batch-remediate.env), GH_TOKEN or gh auth login
```

## What lean means for a ticket

| Step | Lean behavior |
|---|---|
| Story review | Scored; gaps go out as one comment that @mentions the assignee (reporter if unassigned). HALT H1–H3 → `NEEDS-INFO`, label added, next ticket |
| Design | 2–3 options laid out; the recommended one is taken and posted as a `decision` comment marked `default (GATES=none)` |
| Build | Worktree, baseline, implement, consumer impact, logging, unit tests, OWASP — one pass |
| PR | Draft PR into `BASE_BRANCH`, verified (head, files, Jira link); `REVIEWERS` added when set |
| Ticket | One `status` comment per pass with the handoff; nothing posted again when nothing changed |
| End | `READY-FOR-REVIEW`; a later pass reads `MERGED`. The skill never transitions the Jira issue and never sets DONE |
