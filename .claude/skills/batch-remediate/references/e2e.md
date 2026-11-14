# Browser specs — written with the PR, run after deploy

Read in **`tests` mode** (Phase 3, writing the spec) and **`results` mode** (Phase 7, reading
its results). Runs when `E2E_MODE = on`. Never written in `execute`: the spec is written once the
developer confirms the functionality works (T0).

Symphony runs inside a VNet with private endpoints and managed identities, so only the deployed
Dev site proves a UI requirement. The spec is code like any other: it is **written with the
story's PR and reviewed with it**, but it **runs only after the merge is deployed** — headless in
the post-deploy pipeline, or replayed in a visible browser for the tester (`/verify-ticket`).
It adds no new specification: it walks the same Phase 0 checklist one level above the unit tests.
Its steps follow the **smoke-test checklist** from the `execute` comment: the spec automates what
the developer checked by hand.

**Read `{E2E_DIR}/README.md`, `{E2E_DIR}/000000-reference.spec.js` and
`{E2E_DIR}/000001-demo-postdeploy.spec.js` before writing anything.**

## 1. Write the spec (`tests` mode, Phase 3, same PR)

1. **Select.** Every code-provable, UI-observable requirement gets a test, and **every scenario in
   the agreed regression set gets a test** (`design-gate.md` §2.3). Each requirement not covered
   gets `e2e:n/a` with a one-line reason in the REVIEW; a regression-set scenario can only leave
   the set by an M2 exclusion, never by having no test. Silent omission is a defect.
   - A scenario test is titled with its id: `test('BR-386561-ai-dynamicreporting · filters still
     narrow the rows on Dynamic Reports', …)`. It opens that screen and exercises the path the
     graph found (the scenario's `paths` entry), with the same plain `app.step` titles.
   - A story still in flight ships its spec in its own PR. A story already merged (a retrofit,
     like 386561) gets its spec on `test/e2e-{id}` off `develop`, test files only, merged on its
     own; it runs from the next Dev deploy.
2. **File** `{E2E_DIR}/{id}-{slug}.spec.js`; one `test()` per requirement, the title **starting
   with the requirement ID** — `test('AC-3 · mailbox stats append as columns on the mailbox row',
   async ({ app }) => …)`. The ID survives into junit, the Test Plan association and the evidence
   comment, which is how a result is tied to a line of the story.
3. **Steps a tester can follow.** Wrap each visible action in `app.step('<what you will see>',
   async () => …)`. Step titles are what the tester watches for and what the ticket lists; each
   step ends with the screenshot that goes on the ticket. Write them for a tester, not a developer:
   "Open Leases › Add and leave Effective Date blank", not "fill #effDt".
4. **Fixture and page objects.** Take `{ app }` from `./helpers/fixtures`; interact through
   `app.symphony` (three-level shell: `default.html` → launcher → module iframe). A module with no
   entry in `helpers/symphony.page.js` gets one — never guess a launcher key, never inline frame
   juggling.
5. **Definition-of-Done checks** use `app.evidence.consoleErrors()` and
   `app.evidence.failedRequests()` with an explicit, commented list of known-benign patterns.
6. **Determinism.** Web-first assertions and `expect.poll`; **no `waitForTimeout`**. Set up and clean
   up your own data, or read a read-only fixture. Anything that changes data on Dev is named in
   the step title so the tester can see it coming.
   **Declare what each test needs** as annotations, so the test list (`ado-comments.md` §3.7) says
   it and Phase 7 can check it before running — a test that silently skips proves nothing:
   ```js
   test('AC-1 · …', { annotation: [
       { type: 'needs', description: 'env:ALLOW_DESTRUCTIVE_PWRESET=true' },  // set for the run
       { type: 'needs', description: 'dev-setting:EnableHibpPasswordCheck=true' }, // checked on the Dev web app
       { type: 'needs', description: 'account:throwaway' },                    // the test may change the signed-in account
       { type: 'destructive', description: "changes the signed-in account's AD password" }
   ] }, async ({ app }) => { … });
   ```
   **Every test in a story spec carries at least one annotation.** A test that needs nothing says so
   with `{ type: 'needs-nothing' }`. A test with no annotations lists as `needs=unknown`, and
   Phase 7 will not run it until someone reads the spec and declares its needs.
7. **In-app scenario.** When the ticket ships one (`inapp-testing.md`), add
   `INAPP · {id} scenario passes from the Admin page`: open `{INAPP_PAGE}`, click the ticket's Run
   button, fill the form from a read-only fixture with `effects: simulate`, poll to the verdict,
   assert every step Pass.
8. **Check it compiles and lists** — `npx playwright test --config playwright.e2e.config.js --list
   e2e/{id}` from `{E2E_ROOT}`. You cannot run it against Dev before the merge; say so in the
   REVIEW rather than claiming a pass.
9. **M3 / M5.** The M3 plan names the spec and its tests; the M5 summary lists requirement → unit
   test → browser test → manual Test Case.
10. **Manual Test Cases.** In the same step, create the tester's manual cases for every
    user-checkable requirement and every blast-radius scenario (`manual-test-cases.md`). Their steps
    start from this spec's `app.step('…')` titles, written as plain user actions.

## 2. After the merge — how it runs

- **Post-deploy pipeline** (`{POSTDEPLOY_PIPELINE}`): after a `Dev Build` run is deployed, it runs
  **every** spec headless with the saved sign-in — this story's and every earlier story's, which
  is the browser-level blast radius — and posts one `evidence` comment per work item in the build
  (`layer=e2e`, `source=pipeline`), plus one on any other story whose spec failed. The comment
  carries the outcome per requirement, the failing step, console errors, failed requests, a
  screenshot per test, the server-log window, and a zip of traces and videos.
- **Tester-watched** (`/verify-ticket {id}` in VS Code Copilot): replays the spec in Edge, slowed
  down, while the tester watches; drives the requirements without a spec through the Playwright
  MCP tools; records the tester's verdicts and posts them (`source=watched`).
- **Skill, Phase 7** (`{E2E_PENDING}`, `post-deploy.md` §2.1): when the developer comes back after
  the deploy, the skill runs the story's tests that have no result on the latest build, headed
  while the developer watches, and posts them (`source=watched`). This works before and alongside
  the pipeline.
- **Local**: `{E2E_RUN}` — for the developer, never counted as evidence.

## 3. Reading the results (Phase 7)

1. Find the `evidence` comments with `layer=e2e` for the latest deployed build (§1.3 of
   `post-deploy.md`). Several can exist — the pipeline's, and the skill's run of only the pending
   tests; the gate merges them, newest outcome per id.
2. **Verify it** — it is a report, not the run. `source=pipeline`: the pipeline run named in the
   comment exists, ran on that build, and its Tests tab shows the same counts (`az pipelines runs
   show`, test results API). `source=watched`: the comment names the tester and the zip is
   attached. A comment you cannot verify is `unverified` and does not count.
3. **Outcome.**
   - `result=PASSED` → the E2E layer is green for this build.
   - `result=FAILED` on this story's spec → `FAILED-IN-DEV`; the fix is the next PR in the series.
     A failure is a finding: never loosen the assertion, add retries, or re-run until green.
   - `result=FAILED` on another story's spec after this merge → a regression this story caused:
     `FAILED-IN-DEV` for **this** story, naming the other story's requirement.
   - `result=BLOCKED` (Dev unreachable, build not deployed, sign-in refused before any step, or
     the runner failed before any spec ran) → nothing proven. `BLOCKED-E2E ({reason})`; when the
     reason is the sign-in, ask for it to be refreshed (`{E2E_AUTH}`, then re-upload the secure
     file) and the pipeline re-run.
   - `result=INCOMPLETE` or `NOT RUN` (a skip) → not passed. `NEEDS-INFO` naming the skipped ids.
   - The decision is made by `{E2E_GATE}` (`post-deploy.md` §6.1), not by reading the comment.
4. **Auth.** The suite runs on a saved Entra SSO state that a person refreshes with `{E2E_AUTH}`.
   Never script the login, never commit the state file, never type a password.

## 3.5 Post the results to the story — promotion evidence for QA

After the merge is deployed, the developer runs the end-to-end tests: the post-deploy pipeline
(`{POSTDEPLOY_PIPELINE}`), which runs on deploy or is queued by the developer. Once its `evidence`
comment for the **latest** deployed build is verified (§3.2), post one `e2e-results` comment to the
story (`ado-comments.md` §3.8):

- **what ran** — every test of this story's spec, and every other story's test that failed in the
  same run;
- **what each test was for** — its requirement id and, in plain words, what it checks (from the
  test title and its `app.step` titles);
- **the outcome** — PASS, FAIL with the failing step and what was seen, or NOT RUN with the reason;
- the requirements marked `e2e:n/a` and why, and the manual Test Cases still pending for the
  tester.

The comment summarizes verified evidence; it never upgrades it. An unverified or stale run is
reported as unverified or stale, and a developer's local run (`source=local`) is not summarized as
proof. Post it once per build; a later build gets its own.

## 4. Test Plan

Map each Test Plan case to its browser test where the test's assertions cover the case's expected
results (`post-deploy.md` §3): `AutomatedTestName` = `{id}-{slug}.spec.js › {test title}`,
`AutomatedTestStorage` = the spec file, `AutomatedTestType` = `Playwright`. Partial coverage stays
manual — the tester checks it in the watched session.
