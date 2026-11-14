# batch-remediate engagement config — ai-persona-demo (Northline Retail) · lean

Read by batch-remediate 4.0.0 from `{REPO_ROOT}/.claude/batch-remediate.config.md`. Keys not set here
keep the defaults in `.claude/skills/batch-remediate/SKILL.md` §0. No secrets in this file.

`TICKET_URL` is left as `{fill}` on purpose: the first run stops and names it until the Jira site
exists. Replace it with your site URL (e.g. `https://northlineretail.atlassian.net`).

```
# ---- Core ----------------------------------------------------------------------------------------
ENGAGEMENT        = demo
TICKET_SYSTEM     = jira
BASE_BRANCH       = main
GATES             = none
FEATURE_FLAGS     = off
BUILD_CMD         = dotnet build
TEST_CMD          = dotnet test
STANDARDS         = references/standards.md
BATCH_ROOT        = ./batches        # demo choice: repo-relative and git-ignored (4.0 recommends outside the repo)

# ---- Engine switches: lean ----------------------------------------------------------------------
MODES             = off              # one pass per ticket, story to draft PR
EVIDENCE          = local            # green TEST_CMD on the PR head proves a requirement; ends at READY-FOR-REVIEW
CERTIFICATION     = off
INTERACTIVE       = off              # questions go to the ticket, never chat
ENRICH_MODE       = rfi              # gaps become one comment @mentioning the owner (story NL-4)
ENRICH_MAX_QUESTIONS = 3
ENRICH_MAX_ROUNDS    = 3
BLAST_GRAPH       = off
BLAST_LOOP_SCAN   = .claude/skills/batch-remediate/assets/blast-radius.sh

# ---- Ticket adapter: Jira Cloud ------------------------------------------------------------------
TICKET_URL        = {fill: https://<your-site>.atlassian.net}
TICKET_PROJECT    = NL
AC_SOURCE         = description-heading
AC_FIELD          =
AC_HEADING        = Acceptance Criteria
DONE_STATUSES     = Done, Closed, Won't Do
FIELD_WRITEBACK   = off
QUEUE             = batch-auto       # label that --queue selects
NEEDS_INFO_LABEL  = batch-auto-needs-info
SKIP_LABEL        = batch-auto-skip
RUN_TAG           = BATCH-{yyyymmdd}

# ---- Code host: GitHub ---------------------------------------------------------------------------
CODE_HOST         = github
CODE_REPO         = wraptor66/ai-persona-demo
REPO_ROOT         = ~/src/ai-persona-demo            {check: where you cloned it}
WORKTREE_ROOT     = ~/src/ai-persona-demo.worktrees
BRANCH_PREFIX     = feature/                         # branch = feature/NL-3-{slug}
PR_MODE           = draft
EXECUTE_PR        = none
REVIEWERS         =                                  # empty = PRs stay draft

# ---- Tests and evidence --------------------------------------------------------------------------
UNIT_FRAMEWORK    = MSTest + EF Core InMemory provider (tests/Northline.Api.Tests)
UNIT_FILTER       = dotnet test --filter "TestCategory=TICKET-{id}"
JS_TEST_CMD       =                  # no JS harness
TEST_CATEGORY     = TICKET-{id}
KNOWN_RED         = -
TICKET_CID        = TICKET-{id}
LOG_SINK          = ASP.NET Core ILogger -> console -> `docker compose logs api`
E2E_MODE          = off
INAPP_MODE        = off
TESTPLAN_MODE     = off
RUN_SCRIPT        = off
DEPLOY_ENV        = -
PROMOTION_GATE    = -
VERIFY_CMD        =

# ---- Feature flags: off --------------------------------------------------------------------------
FLAG_PROVIDER     = -
FLAG_PREFIX       = -

# ---- Engagement knowledge ------------------------------------------------------------------------
KNOWN_FACTS       = -
ENGAGEMENT_RULES  = Unit tests use the EF Core InMemory provider and must not need the SQL Server container.
                    The skill never runs docker compose.
SECRET_ENV        = JIRA_BASE_URL, JIRA_EMAIL, JIRA_API_TOKEN (environment or ~/.batch-remediate.env), GH_TOKEN or gh auth login
```
