# batch-remediate engagement config — Cushman (Symphony Cloud) · enterprise · = 3.2.0

Copy to `{Symphony Cloud repo}/.claude/batch-remediate.config.md`. With this file the 4.0 engine
behaves exactly as batch-remediate 3.2.0: four modes with a halt between them, guided story review
and certification, hard gates M0 M1 MC M2 M6 and T0, a feature flag on every story, the generated
blast-radius call graph, deployed evidence on Dev before QA, Azure DevOps for tickets and PRs.

Every value below is the 3.2.0 value (SKILL.md configuration block + `references/config.symphony.md`).
The two command lines (`BUILD_CMD`, `TEST_CMD`) were implicit in 3.2 ("build; run the full suite",
plus the known facts about which projects the solution leaves out); they are written out here.
The project facts that are prose, not values, stay in `references/config.symphony.md` (`KNOWN_FACTS`).

```
# ---- Core ----------------------------------------------------------------------------------------
ENGAGEMENT        = cushman
TICKET_SYSTEM     = ado
BASE_BRANCH       = develop
GATES             = M0,M1,MC,M2,M6,T0
FEATURE_FLAGS     = on
BUILD_CMD         = dotnet build symphonyModernization/symphonyModernization.sln
TEST_CMD          = dotnet test symphonyModernization/symphonyModernization.sln ; dotnet test symphonyModernization/SymphonyWebApplication.Tests ; node symphonyModernization/tests/unit/node_modules/jest/bin/jest.js --rootDir symphonyModernization/tests/unit
                    # SymphonyWebApplication.Tests is not in the .sln, so it runs on its own;
                    # jest is called through node, not npm (PowerShell 5.1 swallows "--" in the npm shim)
STANDARDS         = references/standards.md
BATCH_ROOT        = ~/symphony-batches

# ---- Engine switches ----------------------------------------------------------------------------
MODES             = on
EVIDENCE          = deployed
GATES_SUMMARY     = M3,M5,X1,X2      # X1 turns hard on a stale certification or when M2 reopens
GATES_OPTIONAL    = M4
CERTIFICATION     = on
INTERACTIVE       = auto
ENRICH_MODE       = interactive
ENRICH_MAX_QUESTIONS = 3
ENRICH_MAX_ROUNDS    = 3
ENRICH_SCOPE      = all              # story review for every work item type
BLAST_GRAPH       = .claude/skills/batch-remediate/assets/blast-radius-graph   # dotnet run --project … -- graph|regression-set|check
BLAST_LOOP_SCAN   = .claude/skills/batch-remediate/assets/blast-radius.ps1

# ---- Ticket adapter: Azure DevOps ----------------------------------------------------------------
TICKET_URL        = https://dev.azure.com/cwtechsolutions/Symphony%20Cloud
TICKET_PROJECT    = Symphony Cloud
ADO_ORG           = cwtechsolutions
ADO_PROJECT       = Symphony Cloud
ADO_PROJECT_URL   = Symphony%20Cloud
ADO_RESOURCE_ID   = 499b84ac-1321-427f-aa17-267ca6975798
AC_SOURCE         = field            # User Story; Bug and Task use the description heading below
AC_FIELD          = Microsoft.VSTS.Common.AcceptanceCriteria
AC_HEADING        = Acceptance Criteria
ENRICH_DESC_FIELD = System.Description
DONE_STATUSES     = Closed, Removed
FIELD_WRITEBACK   = on               # rewrite in place, explicit approval only, pinned to the revision read
BRANCH_LINK_TARGET = acceptance_criteria   # User Stories; Bugs and Tasks use the development link
QUEUE             =                  # ids on the command line, or a batch name
NEEDS_INFO_LABEL  =
POST_STATUS       = comment          # one mode comment per ticket per run
POST_DECISIONS    = comment
POST_RFI          = comment
RUN_TAG           = BATCH-{yyyymmdd}
TESTCASE_PLAN_PREFIX = Test cases of
TESTCASE_SUITE_NAME  = {User story|Bug|Task}-{id}

# ---- Code host: Azure Repos ----------------------------------------------------------------------
CODE_HOST         = ado
CODE_REPO         = Symphony Cloud
REPO_ROOT         = ~/source/repos/Symphony Cloud
WORKTREE_ROOT     = ~/source/repos/Symphony Cloud.worktrees
BRANCH_PREFIX     = feature/
PR_MODE           = published
EXECUTE_PR        = published        # merges to develop with the change dark behind its flag
REVIEWERS         = (set the team or people who review Symphony PRs, e.g. "[Symphony Cloud]\Symphony Cloud Team")
                    # branch-policy required reviewers are added by ADO regardless

# ---- Tests and evidence --------------------------------------------------------------------------
UNIT_FRAMEWORK    = MSTest, hand-written mocks - no Moq, no xUnit
UNIT_FILTER       = dotnet test {project} --filter "TestCategory=TICKET-{id}"
JS_TEST_CMD       = node symphonyModernization/tests/unit/node_modules/jest/bin/jest.js --rootDir symphonyModernization/tests/unit
JS_UNIT_DIR       = symphonyModernization/tests/unit      # specs named {slug}-{id}.spec.js
TEST_CATEGORY     = TICKET-{id}
KNOWN_RED         = re-measure each run (KNOWN_FACTS: 19 in SymphonyWebApplication.Tests, several in SharedClassLibrary.Tests; AzureBlobTables.Tests does not compile)
TICKET_CID        = TICKET-{id}
LOG_SINK          = DataProcessing.SaveLogToBlob -> Azure Table Logs062625 (App Insights only where constructed with a TelemetryClient)
LOG_TABLE         = Logs062625
LOG_TABLE_ACCOUNT = dev: symphonyfilestoragedev · qa: cwamrsymphonysaqa
RUN_SCRIPT        = on
RUN_SCRIPT_PATH   = analysis/{id}-run-tests.ps1
RESULTS_DIR       = analysis/test-results/{id}            # gitignored
E2E_MODE          = on
E2E_ROOT          = symphonyModernization/tests
E2E_DIR           = symphonyModernization/tests/e2e
E2E_RUN           = npm run test:e2e:strict
E2E_WATCH         = npm run test:watch -- e2e/{id}
E2E_AUTH          = npm run auth                          # human SSO refresh; never scripted
E2E_EVIDENCE      = node tools/post-evidence.js
E2E_GATE          = .claude/skills/batch-remediate/assets/e2e-gate.ps1
E2E_LIST          = node tools/list-tests.js
E2E_PENDING       = node tools/run-pending.js
POSTDEPLOY_PIPELINE  = Dev PostDeploy Browser Tests
POSTDEPLOY_POOL      = AMR-Devops
POSTDEPLOY_AUTH_FILE = symphony-e2e-auth-state.json
TESTER_PROMPT     = /verify-ticket id={id}
SANDBOX_ID        = (ask the developer; never a real story)
INAPP_MODE        = on
INAPP_PAGE        = (locate in Phase 0 on the first ticket; pin file:line; never guess)
INAPP_STEPS       = (locate in Phase 0; never guess)
INAPP_API         = /api/admin/ticket-tests
INAPP_TABLE       = TicketTestRuns
INAPP_PK          = TICKET-{id}-{slug}
INAPP_SUITE_PK    = SUITE-{capability}
INAPP_PROD        = declared-only
TESTPLAN_MODE     = on
DEPLOY_ENV        = dev
DEPLOY_PIPELINE_ID = 1559
DEPLOY_RELEASE    = SymphonyCloudDev (definition 1)
DEPLOY_STAGES     = ^Deploy (WebApp|DataAPI|Symphony-API)$
PROMOTION_GATE    = qa
PROMOTION_BRANCHES = develop → qa → uat → prod
RESULTS_PUBLISHER = symphony
VERIFY_CMD        =                  # not used with EVIDENCE = deployed

# ---- Feature flags -------------------------------------------------------------------------------
FLAG_PROVIDER     = App Service setting read as an environment variable, via FeatureFlags.IsEnabled (SharedClassLibrary/FeatureFlags/FeatureFlags.cs)
FLAG_PREFIX       = FF_{id}_                              # FF_361204_MailboxStatsColumns
FLAG_TOOL         = .claude/skills/batch-remediate/assets/Set-FeatureFlag.ps1   # -List / -On / -Off
FLAG_APPS_DEV     = web=symphonywebdev                    # fill dataapi, api, monitoring (and QA/UAT/Prod) in the tool's $AppMap
FLAG_FLIP_ENVS    = dev

# ---- Engagement knowledge ------------------------------------------------------------------------
KNOWN_FACTS       = references/config.symphony.md         # fields, Test Plans, telemetry, repo, ADO mechanics, secrets
ENGAGEMENT_RULES  = -
SECRET_ENV        = (none: az login token; settings come from Key Vault)
```

## Equivalence with 3.2.0

| 3.2.0 | 4.0 with this file |
|---|---|
| `GATES_HARD = M0,M1,MC,M2,M6`; T0 hard (mode gate) | `GATES = M0,M1,MC,M2,M6,T0` |
| `GATES_SUMMARY = M3,M5`; X1, X2 summary (mode gates) | `GATES_SUMMARY = M3,M5,X1,X2` |
| `FLAG_MODE = required` | `FEATURE_FLAGS = on` |
| `MODE = auto`, four modes | `MODES = on` |
| Deployed evidence, `VERIFIED`, QA promotion gate | `EVIDENCE = deployed`, `DEPLOY_ENV = dev`, `PROMOTION_GATE = qa` |
| Certification before solutioning | `CERTIFICATION = on` |
| `ado-commands.md` blocks for every ADO call | `TICKET_SYSTEM = ado` adapter (SKILL.md §2.2) |
| Comment marker `[batch-remediate \| {type} \| …]` | `[batch-remediate \| cushman \| {type} \| …]` — readers accept both |
