# Config — Symphony Cloud

Read at the start of every run. Everything project-specific lives here so the rest of the skill
can move to another project by swapping this one file.

## Tracker

```
ADO_ORG            = cwtechsolutions
ADO_PROJECT        = Symphony Cloud
ADO_REPO           = Symphony Cloud
ADO_RESOURCE_ID    = 499b84ac-1321-427f-aa17-267ca6975798   # Entra resource for ADO tokens
BRANCH_LINK_TARGET = acceptance_criteria    # User Stories; Bugs and Tasks always use development_link
REVIEWERS          = (set the team or people who review Symphony PRs, e.g. "[Symphony Cloud]\Symphony Cloud Team")
                     # branch-policy required reviewers are added by ADO regardless
ADO_PROJECT_URL    = Symphony%20Cloud                         # the project name URL-encoded (ado-commands.md)
ENRICH_SCOPE       = all                 # story review for every type (2.2: user-story only)
ENRICH_DESC_FIELD  = System.Description
ENRICH_AC_FIELD    = Microsoft.VSTS.Common.AcceptanceCriteria   # User Story only; Bug / Task use the
                                                                # "Acceptance Criteria" section of the description
TESTCASE_PLAN_PREFIX = Test cases of     # the sprint plan: "Test cases of Sprint-0926", iteration = the story's
TESTCASE_SUITE_NAME  = {User story|Bug|Task}-{id}   # static suite under the plan root, e.g. "User story-334075"
```

Branch link:
`https://dev.azure.com/cwtechsolutions/Symphony%20Cloud/_git/Symphony%20Cloud?version=GB{branch}`

## Workspace

```
BASE_BRANCH        = develop
REPO_ROOT          = ~/source/repos/Symphony Cloud
WORKTREE_ROOT      = ~/source/repos/Symphony Cloud.worktrees      # one worktree per ticket
BATCH_ROOT         = ~/symphony-batches                            # ledgers, reports, refactor log
```

## Evidence

```
UNIT_TESTS_CS      = MSTest, hand-written mocks — no Moq, no xUnit
UNIT_TESTS_JS      = jest — symphonyModernization/tests/unit (jest.config.js, package.json; testMatch **/*.spec.js)
JS_UNIT_DIR        = symphonyModernization/tests/unit     # specs named {slug}-{id}.spec.js
TEST_CATEGORY      = TICKET-{id}                          # [TestCategory] on every new C# test
RUN_SCRIPT_PATH    = analysis/{id}-run-tests.ps1          # committed by tests mode; posted in the tests comment
RESULTS_DIR        = analysis/test-results/{id}           # run-script output; gitignored, never committed

FLAG_PROVIDER      = App Service setting read as an environment variable, via FeatureFlags.IsEnabled
                     (SharedClassLibrary/FeatureFlags/FeatureFlags.cs; references/feature-flags.md)
FLAG_PREFIX        = FF_{id}_                             # FF_361204_MailboxStatsColumns
FLAG_TOOL          = .claude/skills/batch-remediate/assets/Set-FeatureFlag.ps1   # -List / -On / -Off
FLAG_APPS_DEV      = web=symphonywebdev                   # fill dataapi, api, monitoring once (and QA/UAT/Prod) in the tool's $AppMap
E2E_ROOT           = symphonyModernization/tests
E2E_DIR            = symphonyModernization/tests/e2e
E2E_RUN            = npm run test:e2e:strict          # from E2E_ROOT
E2E_WATCH          = npm run test:watch -- e2e/{id}   # headed, slowed, Edge - tester watches
E2E_AUTH           = npm run auth                     # human SSO refresh; never scripted
E2E_EVIDENCE       = node tools/post-evidence.js      # from E2E_ROOT; --dry-run to preview
POSTDEPLOY_PIPELINE = Dev PostDeploy Browser Tests     # azure_pipelines/Dev PostDeploy Browser Tests.yml (queued by the Dev release)
POSTDEPLOY_POOL    = AMR-Devops                       # self-hosted, inside the network; Dev has no public access
POSTDEPLOY_AUTH_FILE = symphony-e2e-auth-state.json   # ADO secure file = the saved SSO state
E2E_GATE           = .claude/skills/batch-remediate/assets/e2e-gate.ps1   # read-only; exit 0/10/11/12
E2E_LIST           = node tools/list-tests.js         # from E2E_ROOT; --id {id} --markers = the test list
E2E_PENDING        = node tools/run-pending.js        # from E2E_ROOT; --id {id} --plan, then run headed; exit 3 = not deployed, 4 = nothing could run
BLAST_GRAPH        = .claude/skills/batch-remediate/assets/blast-radius-graph   # dotnet run --project … -- graph|regression-set|check
SANDBOX_ID         = (ask the developer; never a real story) # hardening checks and untested ADO writes
TESTER_PROMPT      = /verify-ticket id={id}           # .github/prompts/verify-ticket.prompt.md (VS Code Copilot)
```

## In-app harness

```
INAPP_PAGE         = (locate in Phase 0 on the first ticket; pin file:line; never guess)
INAPP_STEPS        = (locate in Phase 0; capability step library root; never guess)
INAPP_API          = /api/admin/ticket-tests
INAPP_TABLE        = TicketTestRuns
INAPP_PK           = TICKET-{id}-{slug}
INAPP_SUITE_PK     = SUITE-{capability}
INAPP_PROD         = declared-only        # declared-only | off | all
```

## Pipelines and environments

```
DEPLOY_ENV         = dev
DEPLOY_PIPELINE_ID = 1559                     # "Symphony Cloud Dev" (azure_pipelines/Dev Build.yml) - builds only
DEPLOY_RELEASE     = SymphonyCloudDev (definition 1)   # classic release; this is what deploys Dev
DEPLOY_STAGES      = ^Deploy (WebApp|DataAPI|Symphony-API)$   # all must succeed for "deployed"
PIPELINES          = azure_pipelines/{Dev|QA|UAT|Prod} Build.yml
PROMOTION_BRANCHES = develop → qa → uat → prod   # verify the prod branch name before relying on it
```

The build pipelines restore `**/*.sln`, publish five csprojs (DataAPI, SymphonyWebApplication,
DataRelayIsolated, EnterpriseMonitoringWorkerService, Symphony-api), and **run no tests and deploy
no database objects**. A green pipeline proves compilation only. SQL changes need their own named
deploy step and an owner in the REVIEW.

## Known facts — learned the hard way

Each one cost a pass. Do not relearn them.

**Work item fields**
- **Bug and Task have no Acceptance Criteria field.** User Story does. A Bug's or Task's criteria
  live in `System.Description` under an `Acceptance Criteria` heading (the description is empty by
  default on a Bug; ReproSteps holds the symptom). Never advise "populate the AC field" on a Bug,
  and never create a work item just to get the field.
- `az boards work-item show --fields` leaves out a field the type doesn't have, so one read works
  for every type. It also needs `--expand none`, or it fails.
- Posting a comment changes the work item's `System.ChangedDate`. To know when the description or
  AC changed, read the updates list (`ado-commands.md` §2), never the item's changed date.

**Test Plans**
- Testers keep one plan per sprint (`Test cases of Sprint-0926`, iteration `Sprint 0926`) with a
  static suite per story named `User story-{id}` / `Bug-{id}` under the root. Per-feature plans
  (`384557 Test Plan`, `QA_Test Plan_…`) also exist; the skill uses the sprint plan.
- Testers' own Test Cases are linked to the story as **Child** (`Hierarchy-Reverse`), not Tested By.
  To find them, read the story's suite **and** its child Test Cases **and** Tested By links.
  The skill's own cases use Tested By (`ado-commands.md` §7).
- To test whether a type has a field, read the type's field list. WIQL `[AcceptanceCriteria] <> ''`
  fails on long-text fields for an operator reason, not a field-existence reason.

**Telemetry**
- The server log sink `DataProcessing.SaveLogToBlob` (SharedClassLibrary/DataProcessing.cs)
  **always** writes a row to Azure Table `{LOG_TABLE}`: `PartitionKey` = the CorrelationId
  argument, `EventName`, `ActionName`, and the message in `JsonObject`. It **also** calls
  `TrackTrace` only when `DataProcessing` was constructed with a `TelemetryClient`. As of
  2026-11-04 that is 2 call sites (HibpPasswordCheckClient, UserRiskService), against 141 that use
  `new DataProcessing()` and reach the table only. So for most server code the query goes to the
  table, not to App Insights. Check the constructor at the call you touched (`logging.md` §1).
- When it does reach App Insights, properties are `{EventName, MethodName, CorrelationId,
  ActionName}`. **There is no `Cid` customDimension.** The ticket tag travels in the message text.
- Every trace from that sink lands at `severityLevel = Information`, errors included. Filter on
  `customDimensions.EventName == "Error"`, never on severity.
- Client UI logs: `sendUILog` in `mainpage.html` posts `{typename, payload}` to
  `/api/symphonyweb`. `SymphonywebController.Post` writes it twice: a raw row with
  `PartitionKey = rawpayload`, `EventName = SymphonyWebController-Received-raw`, and a UI-log row
  with `PartitionKey = ui-log` (or the CorrelationID when the payload has one) and
  `EventName = SymphonyWebController-UILog`. The typename and payload are inside `JsonObject`.
  This is Table only; the controller uses `new DataProcessing()`.
- App Insights from this machine: `az rest` to api.applicationinsights.io fails behind the TLS
  proxy. Query through ARM instead: `POST management.azure.com/.../components/{name}/query?api-version=2018-04-20`.

```
LOG_TABLE          = Logs062625                       # SaveLogToBlob's table (DataProcessing.cs)
LOG_TABLE_ACCOUNT  = dev: symphonyfilestoragedev · qa: cwamrsymphonysaqa   # both hold Logs062625 (listed 2026-11-04)
                     # uat/prod: not recorded yet. Never record the connection string.
                     # Reading rows needs "Storage Table Data Reader" on the account (--auth-mode login);
                     # without it, the logging comment says "could not query" and raises an RFI.
```

**Repo and tests**
- There **is** a JS test harness (`symphonyModernization/tests/unit`, jest 29, `npm ci` there first).
  Browser specs live separately in `symphonyModernization/tests/e2e` (Playwright).
- Windows PowerShell 5.1 can swallow `--` when calling `npm` through its `.ps1` shim. Scripts call
  `node <package>/bin/...` directly (jest, Playwright) instead of `npm run x -- args`.
- Client-side changes get jest
  specs. "No JS harness, manual verification" is not an acceptable closure.
- `SymphonyWebApplication.Tests` is not in `symphonyModernization.sln`; tests there do not run in
  a solution build. Run that project directly and say so in the REVIEW.
- Pre-existing red baseline (record the current set in the ledger; re-measure, don't copy):
  19 in SymphonyWebApplication.Tests (GraphApiIntegration, PasswordSelfServiceReset,
  Security.LinkPastingAttack, Security.ReplayAttack — environment-dependent), and several in
  SharedClassLibrary.Tests. `AzureBlobTables.Tests` does not compile (CS0017).
- The client request path injects `CorrelationID` in `parent.GetFromServer`
  (`mainpage.html`). A hand-built worker request without it makes `MethodRouting` throw before
  its first log write — HTTP 500 with no server log. Always go through `GetFromServer`.
- Since the 2025-04 async rewrite, front-end loops lost the pacing a synchronous XHR used to give
  them. Any change to a shared client helper's timing is a Phase 1.5 contract change.

**ADO mechanics**
- PR descriptions: 4,000 **characters** maximum. Measure characters, not bytes (em-dashes and
  arrows inflate byte counts).
- Comments and HTML fields: plain text with `<br>` line breaks; markdown renders literally.
- Every ADO read and write the skill makes is in `ado-commands.md`, verified against this
  organization. The traps it avoids, each found the hard way:
  - `az devops invoke` cannot update a work item (`KeyError: 'type'`) — updates go through
    `Invoke-RestMethod` with a bearer token from `az account get-access-token --resource
    {ADO_RESOURCE_ID}`;
  - `az` is `az.cmd`: in PowerShell, `cmd.exe` splits an `az rest` URL at `&`, and mangles HTML in an
    `az boards --fields` argument;
  - `az rest --body @file` posts an empty comment text;
  - `az devops invoke` rejects `--api-version 7.1-preview.4` — use `7.1-preview`;
  - `Get-Content -Raw` returns a decorated object that serialises its metadata — read bodies with
    `[System.IO.File]::ReadAllText()` and cast `[string]`;
  - a one-element array piped into `ConvertTo-Json` in Windows PowerShell becomes an object — use
    `ConvertTo-Json -InputObject @(…)`.
- Completing a PR with zero reviewers is refused by the guardrail. Do not work around it.

**Secrets**
- Settings come from Key Vault. Never write a secret value into code, config, a comment, a ledger,
  or a log. A historical credential in the repo is a rotation item, not a history rewrite.
