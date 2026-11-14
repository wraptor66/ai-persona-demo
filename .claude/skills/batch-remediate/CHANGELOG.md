# Changelog

Entries are tagged `[ledger-affecting]` (changes how existing ledger or ticket state is read) or
`[additive]`. On resume, a version change is announced once and these entries are listed.

## 4.0.0 — 2026-11-12

One engine for every engagement: the Cushman enterprise build (3.2.0) and the Orrum lean build
(1.x pass + the Jira adapter of batch-remediate-auto 1.1) merged.

- `[additive]` **Engagement config** at the top of SKILL.md (§0): ENGAGEMENT, TICKET_SYSTEM,
  BASE_BRANCH, GATES (list | none), FEATURE_FLAGS (on | off), BUILD_CMD, TEST_CMD, STANDARDS,
  BATCH_ROOT, plus engine switches MODES, EVIDENCE, CERTIFICATION. A repo installs its values as
  `.claude/batch-remediate.config.md`; examples in `config/`. Phases read keys, never literals.
- `[additive]` **Ticket adapter** (§2): fetch_ticket, post_comment, list_queue (+ edit_comment,
  write_fields, link_pr, set_label, mention), implemented for `ado` (ado-commands.md) and `jira`
  (`assets/jira.sh`, REST v2 wiki markup). Code host `ado` or `github`.
- `[additive]` **Lean flow** (`MODES = off`): one pass per ticket to a verified draft PR; missing
  information → a `needs-info` comment that @mentions the owner, then the next ticket.
- `[ledger-affecting]` Comment marker gains the engagement: `[batch-remediate | {ENGAGEMENT} | {type} | …]`.
  3.x markers without it are read as the engagement in the config.
- With `config/cushman.config.md` the behavior is 3.2.0 unchanged.

## 3.2.0 — 2026-11-10 — no mode: the next mode, then a halt

- `[ledger-affecting]` **`/batch-remediate {id}` with no mode starts at the mode the story needs**
  (research when nothing is done), runs it, then **halts and asks permission** to continue to the
  next mode in the same session (`references/modes.md` §1.1). 3.0/3.1 suggested a mode and stopped
  after it; now a "yes" carries on.
- `[ledger-affecting]` **There is always a halt between modes; no argument skips it.** After
  `execute` the halt is the developer's smoke test on Dev: the run ends, and `tests` starts on a
  later run after T0. After `tests` the halt is the merge and deploy.

## 3.1.0 — 2026-11-09 — every story ships behind a feature flag

So any change can be turned on and off per environment without a deploy, and the `execute` PR can
merge to Dev for the smoke test while it stays dark everywhere else.

- `[ledger-affecting]` **Flags are required** (`references/feature-flags.md`). Every story gets
  `FF_{id}_{Slug}`, designed in `research` (M2 shows it; it is no longer a yes/no question),
  checked once at the entry point, off = the old behavior. Exempt only when a change cannot be
  gated (schema-only SQL, a pipeline file), confirmed by the developer at M2 and recorded.
- `[additive]` **Mechanism = what Symphony already uses**: App Service settings read as
  environment variables (as `EnableHibpPasswordCheck` does), through one shared
  `FeatureFlags.IsEnabled` reader that the first flagged story adds. The Symphony releases set no
  app settings, so a flag survives every deploy. Slot settings only (the staging slot runs as
  Production).
- `[ledger-affecting]` **`execute` PRs are published, behind the flag** (`EXECUTE_PR = published`),
  so they can merge and deploy to Dev; the developer turns the flag on in Dev and smoke-tests
  there. 3.0's draft-only rule is `EXECUTE_PR = draft`. After a merge, the next iteration and the
  tests go in new branches of the story's PR series. The QA promotion gate is unchanged.
- `[additive]` **`tests` covers both states**: requirement tests run flag on; one test per entry
  point proves flag off = old behavior (`DOD-flag-off`). Browser specs report `NOT RUN (flag off)`
  when Dev has the flag off.
- `[additive]` **`assets/Set-FeatureFlag.ps1`**: `-List` the flags per environment, `-On` / `-Off`
  a story's flag on the right App Services as slot settings, read back; UAT and Prod need the
  environment name typed. The skill flips only Dev, only after the developer says yes.
- `[additive]` `research` and `execute` comments carry the flag and its Dev command; config adds
  `FLAG_MODE`, `FLAG_PREFIX = FF_{id}_`, `FLAG_TOOL`, `FLAG_APPS_DEV`.

## 3.0.0 — 2026-11-08 — two phases, four modes (stacked on 2.6.0)

Developers iterate on the functionality first and write the tests once it works. In the first
weeks of use, people ran the skill, smoke-tested, changed the story and ran it again several times,
and every iteration regenerated tests against a story that was still moving.

What you will see differently:

- `[ledger-affecting]` **Mode argument**: `/batch-remediate {research|execute|tests|results} {id}`
  (`references/modes.md`). No mode → `auto`: suggests the next mode from the status and asks. A
  mode never silently runs another. A 2.x invocation without a mode works through `auto`.
- `[ledger-affecting]` **New statuses** `RESEARCHED`, `SMOKE-TEST`, `TESTING`. A 2.x ticket at
  `IN-PROGRESS` resumes as `SMOKE-TEST` once its branch is pushed; one at `READY-FOR-REVIEW` or
  later keeps its status (its tests were written the 2.x way).
- `[additive]` **research** — Phase −1 (story review, certification), 0 and 0.25 only, no branch;
  ends with a `research` comment (goal, requirements, design, PR series, smoke-test plan, open
  items) that later modes reuse. This is the research-only mode specified for 2.4.
- `[additive]` **execute** — builds the functionality without new tests, keeps the existing suite
  green, pushes a draft PR `[WIP - tests pending]`, posts (or edits) the `logging` comment, and
  posts an `execute` comment with a numbered smoke-test checklist. A re-run after a story change
  runs the staleness check, asks for re-certification when needed, shows the requirement diff
  (gate X1) and builds only the delta. An unchanged re-run posts nothing.
- `[additive]` **tests** — after gate T0 (the developer confirms the smoke test) writes the MSTest
  tests (`[TestCategory("TICKET-{id}")]`), jest specs, the browser spec with `BR-` scenarios and
  needs annotations, and the manual and automated Test Cases; proves `AC-repro` fail-before /
  pass-after in a throwaway worktree; runs everything **through a generated PowerShell script**
  (`assets/RUN-TESTS.template.ps1` → `analysis/{id}-run-tests.ps1`); publishes the PR; posts the
  `solution` comment with the test list and a `tests` comment with the test map and the full script.
- `[additive]` **results** — runs or reads the script's `summary.json` (unit, jest) before merge;
  after the merge, Phase 7 as in 2.6 (run what has not run, gate, `e2e-results`, logging
  confirmation, promotion). Posts a `results` comment only when something changed.
- `[ledger-affecting]` Tests written before a later code or story change are **stale**, and stale
  tests never count toward `VERIFIED`.
- `[additive]` The run script: Windows PowerShell 5.1, finds the repo itself, layers
  `unit | js | e2e | watch | all`, pre-flight with a fix for each problem (tools, packages, a
  checkout without the tests, a non-Dev `BASE_URL`, a sign-in older than 8 hours), writes TRX,
  jest and Playwright JSON plus `summary.json`, never posts and never signs in. Its own browser runs
  are `source=local`.
- `[additive]` Copilot prompt files per mode: `/batch-remediate-research`, `-execute`, `-tests`,
  `-results`, with the 2.6 hard-gate rules in each.
- `[additive]` Comment types `research`, `execute`, `tests`, `results` (`ado-comments.md`
  §3.10–3.13) replace the per-pass status comment; gates X1, X2, T0 (`milestones.md` §3.1);
  config `JS_UNIT_DIR`, `TEST_CATEGORY`, `RUN_SCRIPT_PATH`, `RESULTS_DIR`, `EXECUTE_PR`, `MODE`.
- Fixed: config pointed the jest harness at `symphonyModernization/tests`; it is
  `symphonyModernization/tests/unit`.

## 2.6.0 — 2026-11-04 — a logging comment on the ticket for every PR (stacked on 2.5.0)

Prompted by WI 382392. Its tests were listed on the ticket, but nothing on the ticket said what
was logged or where. The handoff told QA to look in Application Insights, and the rows only ever
reached Table Storage.

What you will see differently:

- `[additive]` **`logging` comment** (`ado-comments.md` §3.9, `assets/LOGGING.template.md`),
  posted at Phase 5 step 10 straight after the `solution` comment. It covers the logging, not the
  tests: where each row lands, where the path is silent (flag, environment, console-only), how to
  make it run (also at volume, for a load test), a ready-to-paste query per store, what good and
  bad results look like, and a required `NOT LOGGED` list. It is edited on later fixes, and posted
  even when a slice adds no logging.
- `[additive]` **Phase 7 step 6 confirms the logs.** After the path is exercised on the latest
  build, the skill runs the comment's queries and edits its `CONFIRMED IN` line (rows found,
  `NO ROWS` as a finding, or `could not query` as an RFI). This is reported, not gated.
- `[ledger-affecting]` **Trace the sink before writing a query** (`logging.md` §1). The store
  comes from following one real log call to its store, never from convention.
- `[ledger-affecting]` **Sink correction** (`config.symphony.md` → Telemetry). `SaveLogToBlob`
  always writes Table `Logs062625`, but reaches App Insights only when `DataProcessing` has a
  `TelemetryClient`: 2 call sites, against 141 using `new DataProcessing()`. The default
  `traces | where message has "TICKET-…"` query in REVIEW files written before 2.6 returns nothing
  for most server code. Client `sendUILog` rows are Table only (`PartitionKey = ui-log`).
- `[additive]` Volume logging guidance (`logging.md` §3): peak/high-water mark, totals that
  survive between samples, a session or run id.

## 2.5.0 — 2026-11-02 — the skill runs the story's tests after the deploy (392552; stacked on the hardening)

2.4 is reserved for research-only mode (spec written, not built). This version comes back to a
merged story and finishes it: the developer runs `/batch-remediate {id}` after the deploy, and the
skill runs whatever has not run yet and posts the results to the story.

What you will see differently:

- `[additive]` **Test list at PR time.** The `solution` comment lists every browser test of the
  story with what it needs to run (`ado-comments.md` §3.7), from `tools/list-tests.js`. Specs
  declare needs as Playwright annotations: `env:`, `dev-setting:`, `account:throwaway`,
  `destructive` (`e2e.md` §1.6). A test with none lists as `needs=unknown` and is not run.
- `[additive]` **Phase 7 step 5, run what has not run** (`post-deploy.md` §2.1,
  `tools/run-pending.js`). Plan → ask once → run headed while the developer watches → one
  evidence comment → the gate's verdict, in the same pass. Each test is RUN, DONE or HOLD with a
  reason; nothing is skipped silently. A test that failed on the latest build is not re-run on it.
  Running again with nothing new deployed posts nothing.
- `[ledger-affecting]` **A skill-run watched test counts** (decision D-392552-1): evidence from
  `run-pending.js` headed is `source=watched`, which the gate already counts. Headless is
  `source=local` and does not.
- `[ledger-affecting]` **The gate merges every evidence comment on the latest build** (newest
  outcome per id) and adds the test list to the required ids; `-Json` reports `pending`. One
  comment per build reads exactly as before.
- `[additive]` **`test-list` comment** for stories from before 2.5 (once, in Phase 7, after the
  skill read the spec; never with `needs=unknown`).
- `[additive]` Publisher: a partial run names the ids it left out ("Not in this run"), so it never
  reads "every browser-provable requirement passed". `tools/ado.js` is the shared ADO client.
- `[additive]` Hardening check 8.7.

## Unreleased — blast-radius E2E hardening (387107; stacked on 2.3.0)

The blast radius is now re-run against the deployed build and turned into a gate. Every link fails
loudly when it is missing.

What you will see differently:

- `[additive]` **Call graph (L1).** `assets/blast-radius-graph/` (needs only the .NET SDK) writes
  `blast-radius-{id}.json`: changed symbols → callers → routes → front-end calls → pages, with one
  scenario `BR-{id}-{page}` per page. C# is resolved semantically (overloads and same-named members
  told apart); projects that will not load fall back to syntax-only with `low` edges. Same output,
  same hash, on every run. Members with more than 40 callers are listed as **hubs** and not
  expanded. `blast-radius.ps1/.sh` remain for the loop-pacing question.
- `[ledger-affecting]` **Regression set is generated and checked (L2).** M2 accepts
  `regression-set-{id}.json` from the graph; `check` exits 3 when a caller or scenario appears
  after M2, and that reopens M2 (`design-gate.md` §2.3–2.4). The rule existed in 2.x; it is now
  mechanical.
- `[additive]` **Automated Test Cases (L3/L6).** One per spec test, tagged `automated` + the
  requirement or scenario id. The post-deploy publisher records their outcomes in an ADO test
  run. **The 2.3 rule is unchanged** for manual cases: the skill never sets their outcome and the
  publisher can never select them (`manual-test-cases.md` §4, §6).
- `[additive]` **Browser specs per blast-radius scenario (L4)**, titled with the scenario id.
  Retrofits for merged stories go on `test/e2e-{id}`.
- `[ledger-affecting]` **Post-deploy runner moved (L5).** Dev has public access disabled, so
  `Dev PostDeploy Browser Tests` runs on the self-hosted `AMR-Devops` pool. It is queued by the
  `SymphonyCloudDev` release after `Deploy WebApp` (a build finishing is not a deploy), with the
  build id as a parameter, and waits for every required deploy stage. It installs nothing globally
  and deletes the sign-in state at the end. `Dev PostDeploy Agent Probe.yml` checks an agent first.
- `[ledger-affecting]` **Evidence marker carries `buildid`, `commit`, `pipeline`** and a second
  `e2e-cases` line with one outcome per id (`ado-comments.md` §3.6). Older comments still parse.
- `[ledger-affecting]` **Gate (L7).** `assets/e2e-gate.ps1` (read-only) decides the E2E layer:
  missing → unverified, older build → stale, BLOCKED → `BLOCKED-E2E`, any FAIL → `FAILED-IN-DEV`
  naming the scenario, a skip → not passed, anything but a verified pipeline/watched run →
  unverified (`post-deploy.md` §6.1). It counts only PRs merged into `develop`, so promotion PRs
  linked to a story are ignored.
- `[ledger-affecting]` **`BLOCKED-E2E` is back — a reversal of its 2.2 retirement**, with a
  narrower meaning: nothing could run (Dev unreachable, build not fully deployed, sign-in refused
  before any step, runner failed before any spec ran). A spec that ran and failed is still
  `FAILED-IN-DEV`. A 2.1 ledger's `BLOCKED-E2E` still reads as `FAILED-IN-DEV`.
- `[additive]` **Six hardening checks** as repeatable steps (`post-deploy.md` §8). The deployed
  half of check 1 (a broken caller on shared Dev) is deliberately open until a slot or an agreed
  window exists. It is proven locally and by replay to the sandbox instead.

## 2.3.0 — 2026-10-21

The story becomes the specification, reviewed and certified by the developer before any work.

What you will see differently:

- `[ledger-affecting]` **Guided story review** replaces the readiness questionnaire. The skill
  shows the description ("Is this description correct and complete?"), then the AC as a numbered
  list ("Are any modifications required?"), then asks for wiki pages or links to fold in. You paste
  updates as a plain reply; the skill rewrites, shows the final text, and writes it only when you
  say so. Each step repeats until you say it is done. Gaps (no error behavior, untestable wording)
  are offered as suggestions inside these steps.
- `[ledger-affecting]` **You certify the story.** Add the comment from the README ("Certifying a
  story") on the story in ADO. Work starts only when it is newer than the last description / AC
  change; a later change stops the work until you certify again. New status
  `AWAITING-CERTIFICATION`. A 2.2 ticket past `ENRICHING` asks for certification on its next pass.
- `[ledger-affecting]` **Fields are rewritten in place**, not appended: no enrichment block. The
  previous text is in the work item's History tab. **The story is the spec for Bugs and Tasks
  too** (criteria under an `Acceptance Criteria` heading in the description); the wiki is a source
  folded into the story, not a spec of its own.
- `[ledger-affecting]` Gates: M0 = description, M1 = AC + sources, new MC = certify; all three
  hard. Phase −1 prompts are recorded in one analysis comment, not a decision comment each.
- `[additive]` **Manual Test Cases** for the tester, created with the browser spec: one per
  requirement and per blast-radius screen, plain user steps, in the story's suite in the sprint
  plan (`User story-{id}`), linked Tested By. Pending until the tester passes them.
- `[additive]` One **solution** comment per PR after it is verified; one **E2E results** comment
  per Dev build after the post-deploy run.
- `[additive]` `references/ado-commands.md`: every ADO read and write as an exact, verified
  command. Claude Code and Copilot run the same ones.
- `[additive]` Copilot: every review step and hard gate ends the turn; expect terminal approval
  prompts (the skill says so up front).

### Copilot smoke test (one test story)

1. `git pull` on `develop`. Open the repo in VS Code; Copilot Chat in **agent** mode.
2. `az login`, then `az account show` succeeds in the VS Code terminal.
3. Type `/batch-remediate smoke-23 {test story id}`.
4. Check: the first message says `batch-remediate 2.3.0` and warns about approval prompts.
5. Step A shows the description and asks `Q1` — Copilot must stop there. Reply `Q1: B` and paste
   one changed sentence. It shows the proposed text and stops. Reply `A`. Check the story's
   History tab: one new revision with your text, no markdown symbols.
6. Step B shows `AC-1…`; reply `Q1: B` and add one criterion; approve. The new one takes the next
   number; the others keep theirs.
7. Step C: reply `Q1: A`.
8. Step D: before commenting, reply `A` — it must refuse (no comment). Add the README comment in
   ADO, reply `A` — it accepts, names your comment, posts the analysis comment, and moves on.
9. Edit the AC in ADO, start the next pass — it must stop at `AWAITING-CERTIFICATION`.
10. Search the story's comments: none written by the skill contains the certification sentence.

## 2.2.0 — 2026-10-17

Browser proof after deploy, for the tester and the pipeline.

- `[ledger-affecting]` **The browser spec is written with the story's PR** (Phase 3,
  `references/e2e.md` §1), reviewed with the code, and run only after the merge is deployed. 2.1
  wrote it after the merge on a separate `test/e2e-{id}` branch. The ledger `E2E` line now names
  the spec file and its last run. `BLOCKED-E2E` is retired: a failing browser spec is
  `FAILED-IN-DEV`; 2.1 ledgers that say `BLOCKED-E2E` are read that way.
- `[additive]` **Post-deploy pipeline** (`azure_pipelines/Dev PostDeploy Browser Tests.yml`):
  after a Dev build is deployed, runs every browser spec headless with the saved sign-in and posts
  one `evidence` comment per work item (`layer=e2e`, `source=pipeline`) — outcome per requirement,
  failing step, a screenshot per test, console errors, failed requests, the server-log window,
  and a zip of traces and videos. A failing spec of another story is posted to that story too.
- `[additive]` **Tester-watched verification** (`/verify-ticket {id}` in VS Code Copilot): replays
  the spec in Edge, slowed down; drives untested requirements one narrated step at a time through
  the Playwright MCP server (`.vscode/mcp.json`); posts the tester's verdicts (`source=watched`).
- `[additive]` Specs use `app.step('…')` (named step + screenshot) and `app.evidence`
  (console errors, failed requests). Requests to the app carry `X-Symphony-Test-Run` and
  `X-Symphony-Test-Case` headers for server-log correlation.
- `[additive]` REVIEW §5.6 (browser spec) and §6.5 browser row; EVIDENCE template documents the
  browser variant; config adds `E2E_WATCH`, `E2E_EVIDENCE`, `POSTDEPLOY_PIPELINE`,
  `POSTDEPLOY_AUTH_FILE`, `TESTER_PROMPT`.

## 2.1.0 — 2026-10-16

From the first developer runs (248825 in Claude Code; 289884 in Copilot).

- `[additive]` **Verified PR protocol** (`references/pr-protocol.md`): stage only the slice's
  files, commit, push, check the remote branch equals local HEAD, commit the REVIEW before the PR,
  create the PR, then check the PR head commit, changed files, work-item link and reviewers.
  "Created / updated / ready" may only be said after that check, and it re-runs after every
  follow-up edit. Fixes PRs created before the push (empty PR) and follow-up edits never pushed.
- `[ledger-affecting]` `PR_MODE` default is now `published` with reviewers (`REVIEWERS` in
  config). Drafts only with a stated reason. The ledger `PRS` line records the verified head SHA.
- `[additive]` **Developer handoff** (`assets/HANDOFF.template.md`) ends every pass, in chat and
  in the ticket status comment: numbered actions with links and "done when", then what others
  owe, then when the story is finished.
- `[additive]` **One decision comment per question asked**, including the Phase −1 gap questions,
  posted as soon as it is answered: the options word for word, the recommendation and grounds,
  the answer as given, its effect on AC and design, and what each alternative would have changed.
  A content check before posting rejects one-line summaries.

## 2.0.0 — 2026-10-07

Merges the two 1.x lines — the repo copy (Phase −1 enrichment, Phase 1.5 consumer impact, ticket
status comment) and the account copy (in-app Admin-page scenario, capability steps, post-merge E2E)
— and adds the developer-guided flow.

- `[ledger-affecting]` New statuses `DESIGNING`, `DEPLOYED`, `VERIFYING`, `VERIFIED`,
  `FAILED-IN-DEV`. `DONE` is now set only by a person after `VERIFIED`. A 1.x ticket at
  `READY-FOR-REVIEW` with a merged PR resumes at `MERGED`.
- `[ledger-affecting]` Ledgers move to `{BATCH_ROOT}/{batch}/` (`~/symphony-batches`), and the first
  non-numeric argument is the batch name. A 1.x ledger at `analysis/BATCH-STATE.md` must be moved.
- `[ledger-affecting]` Requirement counts are over AC ∪ DoD everywhere; the status marker uses
  `req=` (1.x used `ac=`; both are read).
- `[ledger-affecting]` A story can be delivered as a **PR series**; the ledger's `PRS` block tracks
  each slice through merge, deploy, and verification.
- `[additive]` Seven milestone gates (M0–M6); hard stops at M0, M2, M6 by default. Unattended runs
  post hard gates as decisions answered with `D-{id}-{n}: X`.
- `[additive]` Phase 0.25 design agreement: reuse / extend / isolate options with blast radius,
  feature-flag rules, and a recorded decision with alternatives.
- `[additive]` Ticket comment protocol: `analysis`, `decision`, `status`, `answer`, `evidence`
  comment types in one marker family. The ticket is the source of truth; the ledger is a cache.
- `[additive]` M6 close-out questionnaire turns open items into questions; answers are posted for
  the next pass. Secrets are asked for by location, never value.
- `[additive]` Post-deploy proof: evidence comments, Test Plan case mapping with automation
  association, blast-radius suite, and the QA promotion gate (required PR status + environment
  check). Specifications for the web-app publisher and VNet UI runner.
- `[additive]` Host adapters (`host-claude.md`, `host-copilot.md`) and `config.symphony.md`; the
  same folder runs in Claude Code and GitHub Copilot.
- `[additive]` `blast-radius.ps1` — PowerShell port covering C#, JS, and HTML, with JSON output.
- `[additive]` Known facts captured in config: Bug/Task have no AC field; the server log sink has no
  `Cid` property and logs every trace at Information; the repo has a jest harness;
  `SymphonyWebApplication.Tests` is outside the solution; ADO comment/PR mechanics.
- Fixed: 1.x repo copy referenced `BATCH_ROOT`, batch names, and a Phase 6.5 comment its
  `SKILL.md` never defined. Fixed: KQL templates filtered on `customDimensions.Cid`, which matches
  nothing in this repo.
