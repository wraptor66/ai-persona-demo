# Modes — research, execute, tests, results

Read at the start of every run, right after the config. A run does **one mode** per ticket. The
mode decides which phases run, which gates are asked, which comment ends the run, and which status
the ticket lands in.

Why there are modes: developers do not finish a story in one pass. They run the skill, smoke-test
what it built, change the story, and run it again, often several times, before the functionality
is right. Writing unit tests and browser specs on every one of those iterations is wasted work, and
tests written against a moving story go stale. So the work is split into two phases:

- **Phase A: build the functionality.** `research` once, then `execute` as many times as needed.
  The developer smoke-tests between runs and edits the story when it is wrong.
- **Phase B: prove it.** `tests` once the developer says the functionality is complete, then
  `results` whenever there are new results to report (before merge, and again after deploy).

```
             Phase A: functionality                     Phase B: proof
  research ──► execute ──► merge + flag on in Dev ──► smoke test ──► tests ──► merge ──► results (Dev)
                 ▲              │                                                         │
                 └─ edit story ─┘                         fix needed ◄── FAILED-IN-DEV ◄──┘
                                                          (execute, then tests again)
```

## 1. Choosing the mode

The first argument token that is one of `research`, `execute`, `tests`, `results` (any case) is
the mode. Everything else is parsed as before (batch name, then IDs).

```
/batch-remediate research 361204
/batch-remediate execute 361204
/batch-remediate tests 361204
/batch-remediate results 361204
/batch-remediate 361204                 # no mode -> auto: the next mode, then a halt (§1.1)
/batch-remediate execute sprint-42      # a mode applies to every ticket in the batch
```

### 1.1 `auto` (no mode given) — one mode, then a halt

Start at the mode the ticket's status calls for (table below): a story with nothing done starts at
`research`. Run that mode to its end, then **halt** and ask permission to go on:

```
research is done for 393491 (research comment 5641234).
Next: execute - build it behind FF_393491_… (or the recorded exemption).
  A. (Recommended) Continue to execute now.
  B. Stop here. Run /batch-remediate 393491 when you are ready.
```

On **A** the next mode runs in the same session; on **B**, or no answer, the run ends and the next
`/batch-remediate {id}` picks up from the ticket. There is **always** a halt between modes; no
argument skips it.

**The halt between `execute` and `tests` is the smoke test, and it cannot be answered for the
developer.** Tests are written only after the developer has merged the `execute` PR, turned the
flag on in Dev (or let the exempt change deploy) and smoke-tested there. So after `execute` the
halt does not offer "continue": it gives the smoke-test checklist and ends the run. The next run
starts at `SMOKE-TEST` and asks T0 (`tests-mode.md` §2): **A.** it works, write the tests ·
**B.** not yet, `execute` again.

Between `tests` and `results` the halt is the merge and deploy, which a person does: the run ends
with the tests PR ready for review, and the next run starts at `results`.

`auto` starts at:

| Status | Starts at |
|--------|----------------|
| none, `NEW`, `ENRICHING`, `AWAITING-CERTIFICATION`, `INVESTIGATING`, `DESIGNING` | `research` |
| `RESEARCHED` | `execute` |
| `SMOKE-TEST` | T0: **A.** `tests` (the smoke test on Dev passed) · **B.** `execute` again (story changed or something is not working yet) |
| `TESTING`, `READY-FOR-REVIEW`, `MERGED`, `DEPLOYED`, `VERIFYING`, `FAILED-IN-DEV`, `BLOCKED-E2E`, `VERIFIED` | `results` (`FAILED-IN-DEV` also offers `execute` for the fix) |
| `NEEDS-INFO`, `BLOCKED` | the mode that was interrupted (ledger `MODE`) |

Within a mode, the hard gates (M0, M1, MC, M2, M6) still stop and wait as always.

### 1.2 Entry checks (every mode)

1. Read the ledger and the ticket (fields, all comments newer than `LAST-PASS`).
2. Run the **staleness check** (`enrichment.md` §8) and compute `SRC-HASH`. If the story
   changed since the latest `research` or `execute` comment, every mode handles it explicitly
   (below). A change after the developer's certification also makes the certification stale, and
   2.6's rule still holds: **no solutioning without a current certification**. Silently working to
   an old version of the story is a defect.
3. A **named** mode that needs an earlier one first says so and stops for that ticket. For
   example, `execute` on a `NEW` ticket says "run research first, or run `/batch-remediate {id}`
   to start at the right mode". It never silently runs the earlier mode.
4. Stamp `MODE` and the version in the ledger.

## 2. `research` — understand and agree, no code

**Runs:** Phase −1 (guided story review: M0 description, M1 criteria and sources, MC
certification), Phase 0 (investigate), Phase 0.25 (design and generated blast radius, M2, M3).
**Gates:** M0, M1, MC, M2 hard · M3 summary.
**Writes:** the approved description and AC rewrites (2.6 rules, explicit approval only), the
`analysis` comment on certification, one decision comment per question answered, and **one
`research` comment** that concludes the run (`ado-comments.md` §3.10). No branch, no worktree, no code.
**Ends at:** `RESEARCHED`; `AWAITING-CERTIFICATION` when the developer has not certified yet (the
run stops there, as in 2.6); or `NEEDS-INFO` when a hard gate or an RFI is still open.

This is the research-only mode specified for 2.4, built here: it stops before any branch.

The `research` comment is the **contract the later modes reuse**: the goal, the numbered
requirement list (AC ∪ DoD) with tags, the chosen design (by decision ID), the flag, the PR series,
the file manifest, the blast-radius set, a **smoke-test plan** (how the developer checks each
requirement by hand once it is built), and the open items. A later `execute` reads it instead of
redoing the research. Any developer can pick the story up from that comment.

Re-running `research` on a researched story: if `SRC-HASH` is unchanged, say so and stop (nothing
to do). If it changed, show what changed in the story, re-run only the affected analysis, ask the
gates whose answers the change touches, and post a new `research` comment that **supersedes** the
old one by comment ID. Never edit the old one.

## 3. `execute` — build the functionality, iterate

**Runs:** Phase 0.5 (worktree, baseline), Phase 1 (implement), Phase 1.5 (consumer impact and
the graph drift check), Phase 2 (logging), Phase 3.5 (refactor capture), Phase 5 steps 1–8 with
the change **behind the story's feature flag** (`EXECUTE_PR = published`, `feature-flags.md`), and
step 10, the `logging` comment, posted on the first iteration
and edited on later ones, so the developer can watch the logs while smoke-testing.
**Does not run:** Phase 3 (unit tests, browser spec, manual and automated Test Cases), Phase 3.6
(in-app scenario), Phase 5 step 9 (the `solution` comment with the test list). Those are `tests`.
**Gates:** X1 story-change gate (`milestones.md`), M4 optional, X2 pre-push summary.
**Writes:** one `execute` comment per run (`ado-comments.md` §3.11), carrying the iteration number.
**Ends at:** `SMOKE-TEST`, or `BLOCKED` / `NEEDS-INFO`.

Rules:

1. **Start from the latest `research` comment.** Its requirement list, design and PR series are
   the plan. No `research` comment → stop and say so (§1.2).
2. **Story changed since the last `research` or `execute`** → X1. First the certification:
   if the change made it stale, show the change and ask the developer to add a new certification
   comment themselves (2.6 rule: the skill never writes it), and wait. Then show the requirement
   diff (added, changed, removed, with the source line), say whether the agreed design still fits,
   and ask. A wanted change the developer describes in chat goes through the 2.6 story review
   (`enrichment.md`, rewrite on explicit approval) before it is built. The design no longer fits → M2 reopens as a hard gate and a superseding decision
   record is posted. The requirements changed but the design fits → X1 is a summary and the delta
   is built. Removed requirements: their code is removed in this iteration, not left behind.
3. **The existing suite must stay green.** Run the baseline and, after the change, the full
   existing suite (C# and jest). A test this change breaks is a finding: fix the code, or, when
   the existing test asserted behavior the story deliberately changes, say so at X2 and update
   that test in this iteration with the developer's agreement. Never delete or skip a test to get
   green. **New tests are not written here.**
4. **Bugs.** Reproduce first, by hand or with a scratch script that is not committed, and record
   how in the `execute` comment. The committed `AC-repro` test is written in `tests` and proven
   to fail against the pre-fix commit there.
5. **Smoke-test checklist.** The `execute` comment and the chat handoff give the developer a
   numbered checklist: per requirement, where to go, what to do, and what they should see. Use
   the research comment's smoke-test plan, updated for this iteration. Point to the `logging`
   comment's queries (traced to the real store, `logging.md` §1) so they can see the new log lines
   while they click.
6. **Flag and PR.** The change is built behind the story's flag (`feature-flags.md` §2–4; the
   shared reader is added if `develop` lacks it), off by default. Push and create or update the
   PR titled `{id}: {summary} (behind FF_{id}_{Slug})` (`pr-protocol.md`), published with
   reviewers, so it can merge and deploy to Dev while staying dark everywhere else. The story's
   open PR is updated in place; once one has merged, the next iteration cuts a new branch from a
   fresh `develop` (`{branch}-{n}`), a new PR in the series. `EXECUTE_PR = draft` keeps the 3.0
   behavior; `none` pushes the branch only. The REVIEW file marks the tests sections
   `pending (tests mode)`.
6a. **Dev flip.** The handoff gives the exact command to turn the flag on in Dev once the merge is
   deployed (`Set-FeatureFlag.ps1 -Environment dev -Name FF_… -On -Apps …`). The skill may run it
   itself, on Dev only, after the developer says yes in chat; never QA, UAT or Prod.
7. **Idempotent.** No story change and no code change since the last `execute` → rebuild, re-run
   the suite, report "no change since iteration n", post nothing new. Never stack identical
   commits or comments.
8. **Tests already written** (a `tests` comment exists) and the code changes now → the tests are
   **stale**. Say so at X2, record `TESTS: stale` in the ledger, and end the handoff with "run
   `tests` again when this works". `results` refuses to report green on stale tests.

The developer's next move after `execute`: merge the PR, let it deploy to Dev, turn the flag on in
Dev, and smoke-test with the checklist. Not right → edit the story (or reply with what is wrong)
and run `execute` again. Right → run `tests`.

## 4. `tests` — generate the proof, in its own PR

`references/tests-mode.md` has the full procedure. In short:

**Runs:** T0 entry gate, Phase 3 (unit tests, jest specs, browser spec with its `BR-` scenario
tests and Playwright needs annotations, manual Test Cases for the tester and one automated case per
spec test), Phase 3.6 (in-app scenario, when `INAPP_MODE = on`), Phase 4 (verify, M5), Phase 5 (the tests
PR — the open `execute` PR when it has not merged yet, else a new branch `{branch}-tests` from a
fresh `develop`, step 9 `solution` comment with the test list, step 10 `logging` comment
edited if logging changed), the run script.
**Gates:** T0 hard (functionality complete?), M5 summary (hard on the usual triggers).
**Writes:** one `tests` comment (`ado-comments.md` §3.12) that lists every test by requirement and
**contains the PowerShell script that runs them** (`assets/RUN-TESTS.template.ps1`). The same
script is committed on the branch as `{RUN_SCRIPT_PATH}`.
Every requirement's tests run **flag on**; one test per entry point proves **flag off = the old
behavior** (`DOD-flag-off`).
**Ends at:** `READY-FOR-REVIEW` (published PR, every new test run here and green), or
`IN-PROGRESS` when a test exposes a functional defect (back to `execute`).

A new test that fails because the **code** is wrong is a finding. Report it, set `IN-PROGRESS`,
and send the developer back to `execute`. Never weaken the test to make it pass.

## 5. `results` — update the story with what the tests showed

**Runs:** the measured unit and jest results for the ticket's tests (run here, or read from the
run script's output), then Phase 7 when the PR is merged: merge and deploy gates, evidence, Test
Plan, **run what has not run** (`run-pending.js`, headed, the developer watching), the drift check
and `e2e-gate.ps1`, the `e2e-results` comment, confirming the `logging` comment's queries, and the
promotion gate.
**Gates:** none, unless a result needs a decision (M6 for open items).
**Writes:** one `results` comment per new result set (`ado-comments.md` §3.13): requirement ×
layer (unit, jest, in-app, browser, manual) with outcome, source, build and commit, plus the
promotion-gate state and exactly what is still missing.
**Ends at:** unchanged before merge (results posted), `MERGED`, `DEPLOYED`, `VERIFYING`,
`VERIFIED`, `FAILED-IN-DEV`, or `NEEDS-INFO`.

Rules:

1. **Measured, never asserted.** Run the script's `-Layer unit,js` here when this host can, and
   report the counts it printed. When it can't, read the result files the developer's run wrote
   (`{RESULTS_DIR}`) and label them `source=developer` with machine, time and commit. A developer
   saying "they pass" is a claim.
2. **Stale tests** (the code changed after the `tests` comment, or the story changed after it)
   → report the results but mark the story **not provable**, and the handoff says to run
   `execute`/`tests` again.
3. **Browser evidence** counts only from the post-deploy pipeline or a watched run (a tester's
   `/verify-ticket`, or the skill's own headed `run-pending.js`) on the latest Dev build (`e2e.md`
   §3, `post-deploy.md` §6.1). The developer's local browser run is `source=local` and is
   shown as information, never as proof.
4. **Idempotent.** The same build, commit and outcomes as the latest `results` comment → post
   nothing; say "no new results since {comment}". New results → a new comment that names the one
   it supersedes.
5. **Never `DONE`.** `results` can set `VERIFIED`; a person sets `DONE`.
6. **Flag state.** Before reading any Dev result, check the story's flag is on in Dev
   (`Set-FeatureFlag.ps1 -Environment dev -List`). Off → every Dev layer is `NOT RUN (flag off)`, and
   the handoff gives the flip command. When the story is green, the `results` comment names the
   flag and says it must be turned on in QA, by a person, for the tester.

## 6. Phase map

| Phase | research | execute | tests | results |
|-------|:--------:|:-------:|:-----:|:-------:|
| −1 Story review (M0, M1, MC) | ● | staleness + delta (X1) | staleness | staleness |
| 0 Investigate | ● | delta only | re-read | re-read |
| 0.25 Design, blast-radius graph (M2, M3) | ● | only if X1 or drift reopens M2 | — | — |
| 0.5 Worktree + baseline | — | ● | ● (resume) | ● (resume) |
| 1 Implement / 1.5 Consumer impact / 2 Logging | — | ● | — | — |
| 3 Unit tests, jest, browser spec, Test Cases | — | — | ● | — |
| 3.5 Refactor capture | — | ● | ● | — |
| 3.6 In-app scenario | — | — | ● | — |
| 4 Verify (M5) | — | X2 (build + existing suite) | ● | — |
| 5 Commit, push, PR (steps 1–8) | — | ● behind the flag | ● tests PR | — |
| Feature flag (`feature-flags.md`) | ● designed | ● built, Dev flip | ● both states tested | ● state checked |
| 5 step 9 `solution` comment (test list) | — | — | ● | — |
| 5 step 10 `logging` comment | — | ● post / edit | edit if changed | — |
| 6 State, close-out (M6) | ● | ● | ● | ● |
| 7 Merge, deploy, evidence, run pending, gate, promotion | — | — | — | ● |

Every mode ends with Phase 6: ledger, the mode's comment (it replaces the per-pass status
comment), M6 if anything is open, and the **developer handoff** naming the next mode.

## 7. Queue mode with modes

A mode given with a batch applies to every ticket in it. A ticket not ready for that mode (for
example `tests` on a ticket still at `RESEARCHED`) is skipped with a one-line reason in the batch
report. `research` on a queue keeps the gate sweep: every question is asked up front.

## 8. Ledger

```
MODE        {research | execute | tests | results} · last run {RUN_TAG}
RESEARCH    comment {id} · SRC-HASH {hash} · supersedes {id | -}
ITERATIONS  execute 3 (last {RUN_TAG}, comment {id}, head {sha})
SMOKE       {developer's report from the last iteration, as given | not reported}
TESTS       comment {id} · written at {sha} · SRC-HASH {hash} · {current | stale: code changed at {sha} | stale: story changed}
SCRIPT      {RUN_SCRIPT_PATH} at {sha}
RESULTS     comment {id} · build {n | pre-merge} · {PASSED | FAILED | INCOMPLETE}
```
