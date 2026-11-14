# Manual test cases for the tester — Phase 3 (written), Phase 7 (tracked)

Read at Phase 3, when the browser spec is written (`e2e.md` §1), and at Phase 7. Runs when
`TESTPLAN_MODE = on`. In the same phase that writes the automated tests, the skill creates ADO
**Test Case** work items whose manual steps let the tester repeat **all** of the testing for the
feature **and its blast radius**, by hand, without reading code.

## 1. Which cases

1. **One case per requirement** in the checklist (`AC-n`, `DOD-n`) that a person can check in the
   app. A requirement only a test can observe (internal) gets no manual case; the REVIEW lists it
   as `manual:n/a — internal, proven by {test}`.
2. **One case per blast-radius scenario** — every regression scenario from Phase 0.25
   (`design-gate.md` §2) and every consuming screen or report found in Phase 1.5 (the
   `blast-radius` script's call sites, mapped to where a user sees them).
3. Nothing else. The cases cover exactly what the automated tests and the regression set cover —
   they add no requirement.

## 2. How they are written

The tester needs to know **what to do and what to see**, not why.

- **Title**: `{id} · {requirement id} · {what is checked}` — `386091 · AC-3 · Totals match the
  source for a filtered report`. Blast-radius cases: `{id} · Regression · {screen or report}`.
- **Steps** are user actions in the app, one per step, in order, each with its expected result
  when it is a check: `Open Reports › Dynamic Reports` · `Run report "Lease Summary" for property
  "1 Main St"` → `The Total row matches the sum of the rows above it`.
- **Plain user language only.** Never mention internals: no method, class, file, table,
  stored procedure, endpoint, shared component, dependency, call site, flag name, or the reason a
  screen is in the blast radius. "Run report X for property Y; confirm totals match" — not "verify
  the change to BuildFilterExpression did not break its callers".
- Setup the tester needs (a role, a property with data, a feature turned on in Dev) is the first
  step, stated as what to have, not how it is built.
- Anything that changes data says so in the step: `Save the lease (this creates a test record)`.
- The same steps the browser spec automates (`app.step('…')` titles) are the starting point, so
  the manual and automated runs check the same things.

## 3. Where they go

- **Plan**: the sprint test plan whose iteration is the story's iteration, named
  `{TESTCASE_PLAN_PREFIX}…` (`ado-commands.md` §6). Exactly one must match; otherwise ask the
  developer which plan — never create a plan.
- **Suite**: a static suite `{TESTCASE_SUITE_NAME}` under the plan's root, created if missing.
- **Link**: each case **Tests** the story, so the story lists it under **Tested By**.
- **Tags**: `batch-remediate; TICKET-{id}; {requirement id | Regression}`. The tags make the run
  idempotent: a re-run updates the case for a requirement instead of adding a second one.
- **Commands**: `ado-commands.md` §6 (plan and suite), §7 (create, one per case), §8 (add all of
  them to the suite in one call). Show the developer the list of case titles and their steps first,
  as a summary (work continues), then create them; M5 lists them again with their ids.

## 4. Status — pending until the tester passes them

- A skill-authored case is **pending** until the tester records a passing outcome on its test
  point in the Test Plan, or posts a `watched` verdict for it through `{TESTER_PROMPT}`. The tester
  certifies their own pass; **the skill never sets a test point outcome** and never marks a manual
  case passed.
- **Exception: automated cases (§6) only.** The post-deploy publisher records outcomes on the
  separate *automated* cases, the ones tagged `automated` with Automation status *Automated*, from
  the pipeline run that tested them. It never selects, edits or records anything on a manual case
  or its test points. The selection filter is the tag plus the status, so a manual case cannot
  match. The tester's manual cases stay pending until the tester passes them.
- Skill-authored cases are **never mapped to automation** (`post-deploy.md` §3). They repeat by
  hand what the automation already checks; mapping them would hide them from the tester.
- A failed manual case is a finding: `FAILED-IN-DEV`, naming the case and the step.
- Promotion (`post-deploy.md` §6) needs every skill-authored case passed on the latest build, the
  same as a tester-authored manual case.

## 5. Record

Ledger `TEST CASES` line: plan, suite, each case id with its requirement and state
(`pending | passed {date} by {tester} | failed {date}`). Status comment: the count pending and the
link to the suite. REVIEW §5.7: the case list.

## 6. Automated cases: where the pipeline's outcomes land

Created in the same step as the manual cases, one per browser-spec test: each `AC-n` / `DOD-n` the
spec proves, and each blast-radius scenario `BR-{id}-{page}` (`design-gate.md` §2.2). They exist so
the Test Plan view shows the post-deploy result natively. They are **not** the tester's cases.

- **Title**: `{id} · {requirement or scenario id} · automated · {what the test checks}`.
- **Tags**: `batch-remediate; TICKET-{id}; automated; {requirement or scenario id}`. The id tag is
  how the publisher matches a case to the test whose title starts with that id.
- **Automation**: `AutomationStatus = Automated`, `AutomatedTestName` = `{id}-{slug}.spec.js ›
  {test title}`, `AutomatedTestStorage` = the spec file, `AutomatedTestType` = `Playwright`,
  `AutomatedTestId` = a stable GUID per case.
- **Steps**: none needed; the description names the spec and test.
- **Same plan, suite and link** as §3 (Tests the story; in the story's suite).
- **Outcomes**: written only by `tools/post-evidence.js` from a post-deploy run (`post-deploy.md`
  §4): one ADO test run per plan, `Passed` / `Failed` / `NotExecuted` (a skip, or no test in the
  run) / `Blocked` (nothing could run). The skill itself still sets no outcome on any case.
- Show the developer the list (titles and tags) with the manual cases before creating them; the
  same `ado-commands.md` §7–§8 commands create them, with the extra fields above.
