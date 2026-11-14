# `tests` mode — generate the proof and the script that runs it

Read when the mode is `tests`. The developer has built the functionality with `execute`,
smoke-tested it, and says it works. This mode writes the tests that prove it, runs them, publishes
the PR, and posts a `tests` comment that contains a PowerShell script anyone can paste to run
those tests again.

It adds no functionality. If a test shows the code is wrong, that is a finding for `execute`.

## 1. Entry checks

1. The ticket has a `research` comment and at least one `execute` comment, and the branch exists
   with the `execute` head on the remote. Otherwise stop and name the missing mode.
2. `SRC-HASH` equals the one the latest `execute` comment recorded. If the story changed after
   the last `execute`, stop: "The story changed after the last build ({what}). Run `execute`
   first, so the tests match what is built." Tests are never written against a story the code
   does not implement.
3. The worktree is clean and its `HEAD` equals the remote branch head (`pr-protocol.md` step 4).

## 2. T0 — functionality complete? (hard gate)

Ask once, with the requirement list from the latest `execute` comment:

```
T0 · Ready for tests - {id}
These are the requirements the last build ({sha}, iteration {n}) implements:
  AC-1 {text}   AC-2 {text}   DOD-1 {text} ...
Q1. Did your smoke test show every one working?
  A. (Recommended) Yes, all of them. Write the tests.
  B. Some are not right yet. I'll say which, and run execute again.
  C. Yes, but the story needs a change first. I'll edit it, then run execute again.
Q2. Which requirements did you check by hand? (type IDs, or "all")
```

- **A** → continue. Record Q2 as the `SMOKE` line (the developer's words). It is context, not
  evidence.
- **B / C** → stop, status stays `SMOKE-TEST`, and the handoff says run `execute`. Post the
  decision record (one per question, as always).

## 3. Write the tests (Phase 3)

Same rules as before. They now live here instead of in the build.

1. **Map first.** For each requirement, decide its layers and write the map before any test:
   `AC-n → unit (C# MSTest | jest) · browser (UI-observable) · in-app (when shipped) · manual
   (tester only)`. Every code-provable requirement gets at least one automated test. Anything
   left manual gets a one-line reason.
2. **C# unit tests.** MSTest with hand-written mocks (no Moq, no xUnit). Every new test method
   carries `[TestCategory("{TEST_CATEGORY}")]` and starts its name with the requirement ID:
   `AC2_MailboxStats_AppendsColumnsToRow`. The category is how the run script finds the ticket's
   tests in a shared project. Confirm the test project is in the solution; if it is not (for example
   `SymphonyWebApplication.Tests`), the script runs that project directly and the comment says so.
3. **Client-side (jest).** `{JS_UNIT_DIR}/{slug}-{id}.spec.js`. One `describe` per requirement,
   titled with its ID. Use the jsdom docblock only where a DOM is needed. Behavior first; a source
   contract test only where behavior cannot be loaded (say so in the test).
4. **Bugs.** `AC-repro` must **fail against the pre-fix commit and pass after**, measured: create
   a throwaway worktree at the merge-base with `develop`, copy only the new test files in, run
   them there and expect the failure, then delete that worktree. Record both runs' counts.
5. **Browser spec** (`E2E_MODE = on`). `{E2E_DIR}/{id}-{slug}.spec.js`, per `e2e.md` §1: one
   test per UI-observable requirement and one per regression-set scenario (`BR-{id}-…`), titles
   starting with the ID, every visible action an `app.step('…')` a tester can follow, and the
   Playwright needs annotations (`e2e.md` §1.6) so no test lists as `needs=unknown`. It cannot run until the merge reaches Dev, so check it compiles and lists
   (`--list`) and say exactly that.
5a. **Flag states** (`feature-flags.md` §6). Every requirement's tests run with the story's flag
   **on** (set with `Environment.SetEnvironmentVariable` in setup, cleared in cleanup). One test
   per entry point proves **flag off = the old behavior**, numbered `DOD-flag-off`. The browser
   spec checks the flag is on in Dev before its steps and reports `NOT RUN (flag off)` otherwise.
6. **Test Cases** (`TESTPLAN_MODE = on`): the manual cases for the tester and one automated case
   per spec test, per `manual-test-cases.md`.
7. **In-app scenario** (`INAPP_MODE = on`): Phase 3.6, `inapp-testing.md`.
8. **Deterministic.** No live network, clock, database, or sleep in unit tests; no
   `waitForTimeout` in browser specs.

## 4. Run them here (measured)

Generate the run script (§5) first, then **run it** from the worktree, not hand-typed commands, so
the script itself is proven:

```powershell
powershell -ExecutionPolicy Bypass -File {RUN_SCRIPT_PATH} -Layer unit,js
powershell -ExecutionPolicy Bypass -File {RUN_SCRIPT_PATH} -Layer e2e -ListOnly
```

Report the counts it printed. Then run the **full** existing suite (Phase 4) for regressions.

- A new test fails because the code is wrong → finding. Don't touch the test. Status
  `IN-PROGRESS`, the handoff says what failed and to run `execute`. The tests are still committed
  (on the branch, the tests PR stays draft) so the next `execute` has a target.
- A new test fails because the **test** is wrong (bad setup, wrong selector) → fix the test and
  say so in the comment. Never change an assertion's expected value to match what the code does
  without checking it against the requirement text.
- Skipped is never passed. A test that cannot run on this host is `not run here`, with the reason.

## 5. The run script

`assets/RUN-TESTS.template.ps1` → `{RUN_SCRIPT_PATH}` on the branch. Fill every placeholder from
what this mode actually wrote. The template has the rules; the important ones:

- **Windows PowerShell 5.1 compatible** (the developers' default shell). No `&&`, no ternaries,
  no `-Parallel`. ASCII only.
- **Runs from anywhere**: it finds the repo root itself (`git rev-parse --show-toplevel` from the
  script's folder, or `-RepoRoot`).
- **Layers**: `unit` (C# by `TestCategory`), `js` (jest by ticket ID), `e2e` (headless, Dev, after
  deploy), `watch` (headed in Edge, for a tester), or `all` (unit + js). Default: `unit,js`.
- **Pre-flight, with a fix for each failure**: tools on PATH (`dotnet`, `node`, `npm`), packages
  installed (offers the `npm ci` command), the branch or `develop` actually contains the test
  commit, and for `e2e` and `watch` the saved sign-in exists and is under 8 hours old
  (`{E2E_AUTH}`). It never signs in, never stores a secret, and only ever targets Dev.
- **Results**: writes TRX, jest JSON and Playwright JSON under `{RESULTS_DIR}` and a
  `summary.json` (`ticket`, `commit`, `machine`, `startedAt`, per-layer `passed/failed/skipped`,
  per-requirement outcome). `results` mode reads that file. It prints a short per-layer table
  and exits non-zero when anything failed.
- **Never posts to the ticket.** Posting is `results` mode's job (or the pipeline's). A developer's
  run is evidence of the code, labeled `source=developer`. For the browser layer it is
  `source=local` and never proof.

## 6. Publish the PR (Phase 4, Phase 5)

M5 as usual (diff, requirement → test map, OWASP, consumer impact, residual risks). Then
`pr-protocol.md` with the tests and the script committed: update the REVIEW (§4 tests, §5.5, §5.6,
the run-script section). The tests go in the story's open PR when the `execute` PR has not merged
yet; otherwise in a new branch `{branch}-tests` from a fresh `develop` and a PR titled
`{id}: tests - {summary}`. Published with reviewers, verified (step 8). Then step 9, the `solution`
comment with the test list (`{E2E_LIST} --id {id} --markers`), naming the `tests` comment for the
script; and step 10, edit the `logging` comment if logging changed. Published only when every new
test ran green here; otherwise it stays draft with the reason.

## 7. The `tests` comment

`ado-comments.md` §3.12. Plain text with `<br>` except the script block. Contents, in order:

1. Header: `TESTS WRITTEN - {id} - {n} tests for {x}/{y} requirements`, PR link and head.
2. The map: one line per requirement: unit test name(s), jest spec, browser test title, in-app
   scenario, or `manual - {reason}`.
3. Measured here: per-layer counts from §4, the `AC-repro` fail-before/pass-after counts, and
   `e2e: listed {n}, runs after deploy`.
4. **HOW TO RUN THEM**: three short lines (save as, run before merge, run after Dev deploy),
   then the full script in a `<pre>` block, HTML-escaped (`&` `<` `>` become `&amp;` `&lt;`
   `&gt;`). Real line breaks inside `<pre>`, no `<br>`.
5. What happens next: reviewers, then `results` before merge (optional) and after deploy.
6. The marker:
   `[batch-remediate | tests | {batch} | {RUN_TAG} | tests={n} | req={x}/{y} | head={sha} | script={RUN_SCRIPT_PATH}]`

**Check after posting**: read the comment back and count the lines between `<pre>` and `</pre>`.
If ADO stripped the block (one long line, or none), edit this comment (your own, same pass) to the
fallback: each script line ends with `<br>`, leading spaces become `&nbsp;`. Read it back again.
The script on the branch is the same file, so the comment also names `{RUN_SCRIPT_PATH}` as the
copy to use when pasting goes wrong.

Very long scripts: ADO comments hold well over 100 KB, so the script normally fits. Keep it under
16 KB anyway by putting the test names in `-Filter` lists, not one command per test.

## 8. Ledger and handoff

`TESTS` and `SCRIPT` lines (`modes.md` §8). Handoff:

```
1. Get PR !{n} reviewed - {reviewers}. Done when: merged into develop.
2. (Optional, before merge) Run the tests yourself: paste the script from the tests comment, or
   powershell -ExecutionPolicy Bypass -File {RUN_SCRIPT_PATH}
   then /batch-remediate results {id} to post them to the story.
3. After the Dev deploy: /batch-remediate results {id}. It runs whatever browser test has not
   run on the latest build, headed while you watch, posts the evidence, and reads the gate.
   Done when: the results comment says VERIFIED.
```
