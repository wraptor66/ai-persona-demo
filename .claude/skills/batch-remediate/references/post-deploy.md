# After merge — deploy, prove, gate promotion (Phase 7)

Read in `results` mode, at Phase 7 (after the merge). A fix for `FAILED-IN-DEV` goes `execute`
→ smoke test → `tests` (the failing test must now pass) → `results`. Symphony is a closed system: private endpoints, VNet, managed identities. **Only a
run inside a deployed environment proves the feature.** Before deployment the skill ships the tests
with the code — unit tests, the in-app scenario, and the browser spec; after deployment the
post-deploy pipeline runs the browser specs headless, the Admin page runs the in-app scenarios, and
both write their results to the ticket. This file is also the **specification** for the web-app pieces (§4–§6) when they are worked
as their own stories.

## 1. Merge and deploy gates

For the current slice (`PRS` in the ledger):

1. **Merged** — ADO shows the PR `completed` into `develop` and its merge commit is on `develop`.
   A comment saying it merged is a claim. Promotion PRs (`develop`→`qa`, `qa`→`uat` …) linked to the
   story are not its deployable code; ignore them here. Not merged → leave the status, next
   ticket. First observation → `MERGED`.
2. **Deployed** — the merge commit is contained in a succeeded `{DEPLOY_ENV}` build
   (`{DEPLOY_PIPELINE_ID}`; compare with `git merge-base --is-ancestor`) **and** the classic
   release `{DEPLOY_RELEASE}` for that build has every `{DEPLOY_STAGES}` stage succeeded. A build is
   not a deploy. Record the build ID, number and release → `DEPLOYED`. Not yet → next ticket.
3. **Latest build** — the newest such deployed build. It must contain **all** of the story's
   merged commits. Evidence from an older build is **stale**.

`assets/e2e-gate.ps1` does steps 1–3 the same way every time; read its `latestBuild` and
`storyCommits` instead of recomputing them.

## 2. Collect evidence → `VERIFYING`

Read `evidence` comments on the ticket (`ado-comments.md` §3.6) newer than the slice's deploy.
For each, check: environment = `{DEPLOY_ENV}`; build = latest build (§1.3); and **the run rows
exist** — App Insights by `TestRunId` (`az monitor app-insights query`), or `{INAPP_TABLE}` by
`PartitionKey` and `RunId`. An `evidence` comment is the publisher's *report* of a run, not the
run: it counts only once its rows are seen (for `layer=e2e`: the pipeline run and its test results,
`e2e.md` §3). Rows unreachable from this host → the layer is
`unverified`, does not count toward `VERIFIED`, and an RFI asks who can confirm the rows.
(The promotion gate itself is fed by the publisher from the rows, so it does not depend on this
host's reach.)

Required, per slice:

| Layer | Required when | Source |
|-------|---------------|--------|
| Ticket scenario | the slice proves any in-app requirement | Admin page → Run |
| Blast-radius suite | design option B, or any `modify` capability | Admin page → Run with blast radius |
| Test Plan cases | a Test Plan suite is linked to the story | Admin page → Test Plan tab |
| Browser specs (E2E) | the slice proves any UI-observable requirement, **or the regression set has any scenario** | post-deploy pipeline `evidence` comment, `layer=e2e` (`e2e.md` §3), judged by §6.1 |
| Manual cases | a Test Plan case has no automation, or the skill authored it (`manual-test-cases.md`) | Tester's watched session (`/verify-ticket`) or recorded test-point result |

### 2.1 Run what has not run (browser specs)

When the developer comes back after the deploy, the skill runs the story's browser tests itself
instead of waiting for evidence someone else posts. Same commands in Claude Code and Copilot, from
`{E2E_ROOT}`:

1. **Plan** — `{E2E_PENDING} --id {id} --plan`. It reads the gate (latest deployed build, every
   evidence comment on it merged) and the story's **test list** (the newest `solution` or
   `test-list` comment, `ado-comments.md` §3.7; else the spec files). Each test is **RUN** (no
   counted result on this build, needs met), **DONE** (passed on this build, `pipeline` or
   `watched`), or **HOLD** with the reason: failed on this build (a finding — never re-run on the
   same build), needs unknown, a Dev setting off, a throwaway account required, destructive, or
   the sign-in stale. Exit 3 = not merged or not deployed yet: say so and stop.
2. **Needs unknown** (a story from before 2.5, spec with no annotations): read the spec, write the
   needs per test into a JSON file (`list-tests.js --json` shape), show them to the developer, and
   plan again with `--list {file}`. Never guess a need, and never run a test whose skip condition
   you have not read.
3. **Ask** — show the plan. Ask once for everything it needs: *"Run these {n} tests on Dev build
   {n} now? You watch the browser."* plus, only if listed, *"{ids} are destructive: {reason}. Run
   them?"* and *"{ids} need a throwaway test account signed in. Is the signed-in account one?"*
   A stale sign-in → the developer runs `{E2E_AUTH}` first. Never run `{E2E_AUTH}` for them.
4. **Run** — `{E2E_PENDING} --id {id}` with `--allow-destructive` / `--account-confirmed` only
   for what was agreed, and `--list {file} --post-list` when the list came from step 2 (the
   `test-list` comment is posted first, so the next pass has it). It runs the RUN tests headed,
   posts one `evidence` comment (`source=watched`, D-392552-1) naming the ids left out, and prints
   the gate's verdict. `--headless` is for a developer's own check only: `source=local`, never
   counts.
5. **Read** the verdict into §7 like any other evidence. Tests still on HOLD are listed in the
   status comment with their reason and owner (e.g. RFI for the throwaway account).

Run it again any time: a test with a counted result on the latest build is not run again, and a
pass where everything is DONE posts nothing.

`RESULTS_PUBLISHER = manual` (until §4 is built): ask the developer to run each layer from the Admin
page and paste the panel's **Copy evidence** summary (it includes the `runId`). Verify the rows as
above before posting it as an `evidence` comment with `source=manual`. A pasted summary whose rows
cannot be found is a claim, not evidence.

## 3. Test Plan integration

Three kinds of case reach the story's suite:

- **Skill-authored manual** — created in Phase 3 (`manual-test-cases.md` §1–§4), tagged
  `batch-remediate; TICKET-{id}; {requirement id | Regression}`. They stay manual and **pending**
  until the tester passes them. Never map them to automation and never set their outcome.
- **Skill-authored automated** — created in the same step (`manual-test-cases.md` §6), tagged
  `automated` plus the requirement or scenario id, Automation status *Automated*. The post-deploy
  publisher records their outcomes (§4); the skill sets none.
- **Tester-authored** — everything else. Testers in this project link their cases to the story as
  **Child** (not Tested By), and keep them in the story's suite in the sprint plan (`config`
  *Test Plans*). Find them through all three: the suite, the story's child Test Cases, and its
  Tested By links.

On each pass in Phase 7, for the **tester-authored** cases:

1. Read the cases and their steps.
2. Map each case to automation, in this order of preference:
   - an existing capability step or ticket scenario that performs and asserts the same thing →
     `inapp:{capabilityId}` or `inapp:Ticket{id}Scenario`;
   - an E2E spec (`{id}-{slug}.spec.js`, test name) for UI flows;
   - **none** → the case stays manual.
   Never mark a case automated because it is *similar* to a step; the step's assertions must cover
   the case's expected results. Partial coverage → manual, with the gap named.
3. Propose the mapping to the developer as a summary (it is reversible), then write the association
   on each case: `Microsoft.VSTS.TCM.AutomatedTestName`, `AutomatedTestStorage`
   (`SymphonyAdmin.InApp` or the spec file), `AutomatedTestType` (`InApp` / `Playwright`),
   `AutomatedTestId` (a stable GUID per mapping), and `AutomationStatus = Automated`.
4. Cases left manual — tester-authored ones with no automation, and every skill-authored manual
   case — are listed in the status comment and the `e2e-results` comment as **awaiting a manual
   pass** by the tester. A manual case counts only when the tester records a passing outcome on the
   test point.

## 4. Results publisher

**Browser specs (built):** `{E2E_EVIDENCE}` (`tests/tools/post-evidence.js`), run by
`{POSTDEPLOY_PIPELINE}`:

- Posts one `evidence` comment per work item in the build, edited (never duplicated) on a re-run
  for the same run: build ID, number and commit; each requirement's and each blast-radius
  scenario's pass / fail / skip with the failing step; links to the pipeline run, its Tests tab,
  screenshots, and a zip of traces and videos. Its two marker lines are the gate's input
  (`ado-comments.md` §3.6).
- When nothing could run (§5), posts `result=BLOCKED` with the reason instead. Never a pass.
- Records outcomes on the story's **automated** Test Plan cases (`manual-test-cases.md` §6): one
  ADO test run per plan, linked to the Dev build. Manual cases and their test points are never
  selected.

**In-app layers (web app — specification):** `IAdoResultsPublisher`, invoked when any Admin-page
run completes:

- Posts one `evidence` comment to the work item (`assets/EVIDENCE.template.md`): run ID, environment,
  build number, commit, layer, requirement → outcome, failing step and message, who started it.
- For Test Plan runs, creates an ADO test run against the plan and sets each automated test
  point's outcome, so the Test Plan view shows the result natively.
- Sets the **promotion status** (§6) for the story.
- **Auth**: the App Service managed identity, added to the ADO organization with access to the
  project. A PAT only if a managed identity is impossible, stored in Key Vault.
- **Network**: outbound HTTPS to `dev.azure.com` through the VNet egress (NAT gateway or firewall
  rule) — outbound only.
- **Reliability**: idempotent on `runId`; failures queue and retry; a run whose results never
  reached ADO shows **not published** on the Admin page.
- **Never** posts parameter values marked sensitive, connection strings, or tokens.

## 5. Browser runner

`{DEPLOY_ENV}` has public network access disabled; the site is reachable only through its private
endpoint. A Microsoft-hosted agent cannot reach it.

- **Pipeline:** `{POSTDEPLOY_PIPELINE}` runs on the self-hosted pool `{POSTDEPLOY_POOL}` (inside the
  network, the same pool that deploys Dev). It is **queued by the release**: the last task of the
  `Deploy WebApp` stage of `{DEPLOY_RELEASE}` passes the deployed build's id and number
  (`azure_pipelines/release-dev-queue-browser-tests.md`). The run then confirms every
  `{DEPLOY_STAGES}` stage succeeded for that build, checks the site answers (302 to SSO), and only
  then runs every spec headless with the saved SSO state (`{POSTDEPLOY_AUTH_FILE}`, refreshed by a
  person with `{E2E_AUTH}`). It is self-contained on the shared agent: Node from the tool cache,
  npm and browsers under the agent work folder, the sign-in state deleted at the end.
- **Tester-watched:** `{E2E_WATCH}` in VS Code, headed and slowed down in Edge, from a machine on the
  corporate network. This is also the fallback if the pool is unavailable; its evidence is
  `source=watched`.
- **Developer-watched, from the skill:** `{E2E_PENDING}` (§2.1) — the same headed run, limited to
  the tests with no counted result on the latest build, started by the skill in Phase 7 with the
  developer watching. `source=watched`.
- **BLOCKED** (the pipeline posts it, the gate reads it as `BLOCKED-E2E`): the build is not fully
  deployed, the site is unreachable from the agent, the saved sign-in is refused before any step,
  or the runner failed before any spec ran (install, no results file). A spec that ran and failed
  is never BLOCKED.
- **Agent check:** `azure_pipelines/Dev PostDeploy Agent Probe.yml` reports Node, egress (npm,
  Chromium) and the Dev site's status from the agent. Run it after any change to the pool.

## 6. The promotion gate — no story reaches QA untested

`{DEPLOY_ENV}` is the proving environment; `{PROMOTION_GATE}` is where the test engineer confirms.
A story may not be promoted while **any** of its required layers (§2) is failing, not run,
not automated-and-not-manually-passed, or run on a stale build.

### 6.1 The E2E layer — `assets/e2e-gate.ps1`

```
powershell -File {E2E_GATE} -Id {id} -RegressionSet {E2E_DIR}/regression-sets/regression-set-{id}.json -RepoPath .
```

Read-only. Run it at every Phase 7 pass; never decide the E2E layer by reading the comment
yourself. Checks 5–9 read **every** evidence comment for the latest build merged, oldest to
newest, newest outcome per id; the required ids are those ids, the story's test list
(`ado-comments.md` §3.7) and the regression set. With `-Json` it also reports `pending` — required
ids with no counted pass on the latest build and no failure on it — which is what §2.1 runs. It
applies these checks in order, and the first one that applies decides (exit code in brackets):

| # | Check | Outcome |
|---|-------|---------|
| 1 | No PR merged into `develop` is linked to the story | `NEEDS-INFO` (12) |
| 2 | No deployed build, or the latest deployed build lacks a story commit | `NEEDS-INFO` (12): wait for the deploy |
| 3 | No e2e evidence comment | `NEEDS-INFO` (12): layer **unverified**, never passed |
| 4 | Newest evidence is for an older build than the latest deployed one | `NEEDS-INFO` (12): **stale**, names both builds |
| 5 | Newest evidence `result=BLOCKED` and no run on this build reported any result | `BLOCKED-E2E` (11), with the reason from the comment |
| 6 | Any requirement or scenario `FAIL` | `FAILED-IN-DEV` (10), naming each failing id, scenarios first |
| 7 | Any required id (the comment's ids plus every regression-set scenario) is `SKIP`, `BLOCKED` or absent | `NEEDS-INFO` (12): a skip is **not passed** |
| 8 | Any id's pass comes from a source other than `pipeline`/`watched`, or a contributing pipeline run is missing, tested another build, or its Tests tab counts differ from its comment | `NEEDS-INFO` (12): **unverified** |
| 9 | Otherwise | `VERIFIED` (0) for the E2E layer |

Unverified evidence can only make the verdict redder. A failure reported by a replay or a local
run still blocks (check 6 runs before check 8), but only a verified pipeline run, or a tester's
watched run, turns the layer green. Before running the gate, run the drift check
(`design-gate.md` §2.4); exit 3 reopens M2 and the gate is not run.

The gate's verdict is the E2E layer only. The story is `VERIFIED` (§7) when every required layer
in §2 is green.

### 6.2 Enforcement on promotion

Enforced in two places (both configured once, by the platform owner):

1. **PR status on promotion PRs into `{PROMOTION_GATE}`.** For each work item linked to the PR, the
   publisher posts a PR status `batch-remediate/tests` (genre `batch-remediate`, name `tests`):
   `succeeded` only when that story is green on the latest `{DEPLOY_ENV}` build. A branch policy on
   `{PROMOTION_GATE}` requires the status. Promotion PRs bundle many stories, so the status
   description names every blocking story and its failing step; **one red story blocks the PR**.
   The fix is to pull that story's commits out of the promotion, not to bypass the gate.
   *Not built yet*: until then the gate's verdict is reported in the status comment and the
   `e2e-results` comment, and promotion is the test engineer's call on that evidence.
2. **Environment check on the `{PROMOTION_GATE}` release stage** — an Azure Pipelines check that
   calls the same endpoint before deploying, catching anything that arrives without a PR.

Bypass exists only through ADO's own audited policy-override permission. The skill never requests
or uses it.

## 7. Outcome

- All required layers green on the latest build **for the last slice**, promotion status succeeded →
  `VERIFIED`. Update this pass's status comment (edit it if already posted; never a second one). Only a person moves the story to `DONE`.
- Green for this slice, more slices remain → back to Phase 0.5 for the next slice.
- A layer failed → `FAILED-IN-DEV`: name the requirement or scenario, the step, the observed
  behavior, and whether it is this story's defect or a regression in another story's assertion
  (blast radius). The fix becomes the next slice. **Never loosen a test to make it pass.**
- Nothing could run → `BLOCKED-E2E ({reason})`: the environment, deploy or runner problem named by
  the gate. Not a defect in the story and not a pass; it clears when a re-run on the latest build
  produces evidence.
- Evidence missing, stale, unverified, or a required id not passed → `NEEDS-INFO`, naming exactly
  which layer to run on which build.

## 8. Hardening checks — every one must go red

Run these when the gate, the publisher, the pipeline or the graph changes, and before relying on
them for a new environment. Each step says what to run and what must be seen. A check that comes
out green has **failed**. Record each run in the ledger (`HARDENING: {n} {date} {result}`).

Use the **sandbox** work item `{SANDBOX_ID}` for anything that writes to ADO, never a real story.
Show each write to the developer before it happens.

### 8.1 A broken caller turns the gate red, naming the scenario

Never deploy a deliberately broken build to shared Dev. Two halves:

1. **Local, headed.** In a throwaway worktree off the story's merge commit, break one blast-radius
   caller (the page's own code, so nothing else is touched), and run that scenario's spec against
   a **local** build of the web app:
   `BASE_URL=https://localhost:{port} npm run test:watch -- e2e/{id}`. The spec must **fail**, and
   its title must start with that scenario's `BR-{id}-…` id.
2. **Replay to the gate.** Post that failing results file to the sandbox item as if it were the
   latest Dev build:
   ```
   EVIDENCE_SOURCE=replay EVIDENCE_WORK_ITEM_MAP={id}={SANDBOX_ID} UPSTREAM_BUILD_NUMBER={latest} UPSTREAM_BUILD_ID={latestId} \
     node tools/post-evidence.js --only {SANDBOX_ID} --report {failing results.json}
   powershell -File {E2E_GATE} -Id {SANDBOX_ID} -StoryCommits {story merge commit}
   ```
   Must exit **10, `FAILED-IN-DEV`**, naming that `BR-…` id.
3. Delete the throwaway worktree. **The deployed version of this check stays open** until there is
   a safe deploy path (a deployment slot, or an agreed Dev window). Record it as open in the
   ledger.

### 8.2 Stale evidence is refused

With evidence on the sandbox for build N (from 8.1), run the gate when a newer build N+1 has
deployed and contains the commits, or simulate it by replaying with an older
`UPSTREAM_BUILD_NUMBER`/`UPSTREAM_BUILD_ID` than the latest. Must exit **12, `NEEDS-INFO`**, with
"Evidence is stale" naming both builds. A stale FAIL is not reported as `FAILED-IN-DEV`.

### 8.3 A skipped spec is not a pass

Replay a results file in which every test passed except one marked skipped (`test.skip` in a
throwaway copy of the spec, run locally). The comment must read `INCOMPLETE` and its cases line
`…=SKIP`. The gate must exit **12, `NEEDS-INFO`**, listing `{id}=SKIP`. In the pipeline, the
"Fail the run unless every test passed" step must be red (`E2E_NOT_PASSED`).

### 8.4 Missing evidence reads unverified

Run the gate on a story (or the sandbox) that has a deployed merge and **no** e2e evidence
comment. Must exit **12**, "E2E layer unverified: no post-deploy evidence comment". Then replay
a passing file with `EVIDENCE_SOURCE=local`: still **12**, "unverified … does not count toward a
pass".

### 8.5 Call-graph drift reopens M2

Without touching product code, on the tool's fixture:

1. Copy `{BLAST_GRAPH}/fixture/App` into an empty folder, `git init`, commit.
2. `graph --id 900001 --root App --symbol "cs:Fixture.Shared.Formatter.Format(string)"`, then
   `regression-set`, then `check`: exit **0**. Run `graph` twice; the files must be identical.
3. Copy `fixture/drift/AuditController.cs` into `App/Controllers/` and `fixture/drift/audit.html`
   into `App/wwwroot/pages/`; commit; `graph` again; `check` against the step-2 set: exit **3**,
   with `NEW SCENARIO BR-900001-audit` and the new caller lines.

On a real story, the same check runs at every later slice (`design-gate.md` §2.4).

### 8.6 Dev unreachable → `BLOCKED-E2E`, not a pass

1. Queue `{POSTDEPLOY_PIPELINE}` by hand with `baseUrlOverride` set to an address that does not
   answer (e.g. `https://symphonywebdev-unreachable.invalid/`), `workItemIds={SANDBOX_ID}`,
   `waitForDeploy` unticked.
2. The reachability step must set `E2E_BLOCKED`, no spec runs, the sandbox gets a
   `result=BLOCKED` comment with the reason, and the run is red.
3. The gate on the sandbox must exit **11, `BLOCKED-E2E`**, quoting the reason.

Without a pipeline run, the publisher half can be checked locally:
`node tools/post-evidence.js --blocked "Dev site unreachable (check 8.6)" --only {SANDBOX_ID}`,
then the gate.

### 8.7 Only what has not run, runs — and never silently skips

On a story with a test list and a deployed merge:

1. `{E2E_PENDING} --id {id} --plan` with one id already passed `watched` on the latest build: that
   id must read **DONE**, and the plan must not list it to run.
2. Give one test a need that is not met (a `dev-setting` the Dev app does not have, or
   `account:throwaway` without `--account-confirmed`): it must read **HOLD** with that reason,
   and a run must print it under `NOT RUN`, never pass over it.
3. Run once; then run again with nothing new deployed: the second run must post **nothing** and
   print that every test already has a counted pass (or name the HOLD reasons).
4. With a test that FAILED on the latest build: the plan must show HOLD "a finding; not re-run on
   the same build", and the gate must stay `FAILED-IN-DEV`.
