# Ticket comments — the shared memory

Read before any write to a work item. The ticket is the source of truth; the ledger is a cache of
it. Anything a future developer or a future pass needs goes on the ticket, in a form a person can
read and the skill can parse.

## 1. Format rules (ADO comments and HTML fields)

- Plain text. **No markdown** — `**`, `#`, backticks, and pipes render as literal characters.
- Convert every newline to `<br>`. `<b>` for a few labels; hyphen bullets otherwise.
- Short lines; the comment pane is narrow.
- Every comment **ends with a marker line** in one family:
  ```
  [batch-remediate | {type} | {batch} | {RUN_TAG} | {key=value ...}]
  ```
  Find earlier comments by the stable prefix `[batch-remediate | {type} |`, never by `{RUN_TAG}`.

## 2. Volume

At most, per ticket per pass:

| Type | How many |
|------|----------|
| `analysis` | Once per accepted certification — the first, and again after each re-certification |
| `review-request` | Once per story version, unattended only (`ENRICH_MODE = rfi`) |
| `solution` | One per PR in the series, after the PR is verified; edited, never re-posted, on later fixes. Carries the story's test list |
| `logging` | One per PR, right after its `solution` comment; edited on later fixes and once in Phase 7 to record what the queries returned on the deployed build |
| `test-list` | Once, in Phase 7, only when no `solution` comment carries a test list (stories from before 2.5); a later list goes in the next `solution` comment |
| `e2e-results` | One per deployed build of the story, after the post-deploy E2E run is verified |
| `decision` | **One per question asked** at M2 and later, and any decision raised later (Phase 1.5, M5). Posted as soon as the question is answered, not batched to the end of the pass. Phase −1 review prompts go in the `analysis` comment |
| `research` · `execute` · `tests` · `results` | One per mode run that changed something (3.0, §3.10–3.13). Each carries that run's status, RFIs, warnings and the developer handoff, so it **replaces** the `status` comment for that run. An unchanged re-run posts nothing |
| `status` | One per pass **without a mode comment** (a run resumed from a 2.x ledger) — carries RFIs, warnings, summary-gate outcomes, and the developer handoff |
| `answer` | One per M6 close-out with answers |
| `evidence` | One per run, posted by `{E2E_EVIDENCE}` — from the pipeline, a tester's `/verify-ticket`, or the skill's own Phase 7 run of the pending tests (`{E2E_PENDING}`, `post-deploy.md` §2.1). In-app layers: by the Symphony web app, or by the skill only when `RESULTS_PUBLISHER = manual` |

Never post a fragment. Never repeat an earlier question; reference it (`RFI-361204-1 — still open`).

## 3. Types

### 3.1 `analysis` — Phase −1, on accepted certification

Posted by `enrichment.md` §7.4. Never contains the certification sentence — it names the
certifying comment by id and date only.

```
STORY REVIEW - CERTIFICATION ACCEPTED - {RUN_TAG}
Certified by {createdBy} in comment {commentId} on {createdDate}.
Story certified at rev {n} (description last changed rev {n}, criteria last changed rev {n}).
Solution building is starting.

GOAL: {one sentence}
READINESS: {before}/20 -> {after}/20
WRITTEN IN THIS REVIEW: description rev {n} · acceptance criteria rev {n} (approved by {developer})
PROMPTS: {R7 error behavior -> A, added AC-5 · R6 out of scope -> skipped · ...}
SOURCES FOLDED IN: {wiki "Mailbox stats" §Agreed changes -> AC-6 · none}
CONFLICTS RESOLVED: {source A vs source B -> chosen, by {developer}}
GAPS STILL OPEN: R{n} {one line}
CHECKLIST: AC-1 … AC-n, DOD-1 … DOD-n
Earlier text of both fields is in the work item History tab.
[batch-remediate | analysis | {batch} | {RUN_TAG} | readiness={after}/20 | certified={commentId}]
```

### 3.2 `review-request` — Phase −1, unattended only

`enrichment.md` §9. The description and numbered AC as read, every rubric prompt with its
ready-to-paste options, the sources found, and the request to edit the fields in ADO and then add
the certification comment **as described in the batch-remediate README, "Certifying a story"**. It
never contains the certification sentence.

```
STORY REVIEW NEEDED - {RUN_TAG}
Before any work starts on this {type}, please review its description and acceptance criteria.

DESCRIPTION AS READ (rev {n})
{plain lines}

ACCEPTANCE CRITERIA AS READ
AC-1 {text}
...

SUGGESTED IMPROVEMENTS (copy any you want into the story; ignore the rest)
- R7 error behavior: AC-5 Each stats column shows "—" until the first collection completes.
- ...

SOURCES FOUND: {links, or none}

WHAT TO DO
1. Edit the Description and Acceptance Criteria in Azure DevOps as needed.
2. Then add the certification comment described in the batch-remediate README,
   section "Certifying a story". The next run checks it and starts the work.
[batch-remediate | review-request | {batch} | {RUN_TAG} | rev={n}]
```

### 3.3 `decision` — one per question asked

Use `assets/DECISION.template.md`. One comment per question, at M2 and every later gate. (The
Phase −1 review loops change the story itself; their prompts and answers are listed in the
`analysis` comment, §3.1, next to the field revisions they produced.) Every record carries:

- the gate, the question exactly as asked, and the context line shown with it;
- **every option offered, word for word as presented**, lettered, the recommended one marked, with
  its grounds (`file:line`, wiki section, or comment);
- **the answer as given**: the letter, and the developer's own words when they typed more than a
  letter, quoted exactly (for `DEFAULTS`, say so);
- who answered and where (chat in this run, or a ticket reply);
- **effect**: which AC, DoD item, design choice, or regression set changed because of this answer;
- for each option **not** chosen, what it would have changed (scope, design, blast radius, effort,
  risk);
- **revisit if**: the condition that would reopen it.

**Check before posting.** The comment body must contain every option letter offered, a
`RECOMMENDED:` line, an `ANSWER:` line, an `EFFECT:` line, and an `IF WE HAD CHOSEN OTHERWISE`
section with one line per unchosen option. If any is missing, the comment is incomplete: fix it,
don't post it. A one-line "M2 answered: B" is a status line, never a decision record.

Several questions answered in one round are still one comment each. Post them in order, before
the next phase starts.

A changed decision is a **new** record that names the one it supersedes. Never edit an old one.

### 3.4 `status` — once per pass

**3.0:** each mode posts its own comment (§3.10–3.13), and that comment carries the status. Where
this skill says "the status comment", read "the run's mode comment". A separate `status` comment is
posted only for a run without a mode comment.

`assets/TICKET-COMMENT.template.md`. Requirement status over AC ∪ DoD, branch / PR series, design
choice (by decision ID), feature flag, in-app scenario state, promotion-gate state, outstanding
RFIs and decisions, deferred items.

### 3.5 `answer` — M6 close-out

```
CLOSE-OUT ANSWERS - {RUN_TAG} - from {developer}
RFI-361204-2: {answer as given}
DOC: {name} -> {link}
SECRET LOCATION: {setting name} -> Key Vault {vault} secret {secret} (value not recorded)
EXAMPLE: {input} -> {expected output}
ROUTED: RFI-361204-3 -> {owner} (no answer available)
[batch-remediate | answer | {batch} | {RUN_TAG} | answered={n} routed={n}]
```

The next pass treats each line as an answer to the named item — **still a claim** where it asserts
an external dependency (verify the artifact).

### 3.6 `evidence` — post-deploy (written by the web app)

`assets/EVIDENCE.template.md`. Run ID, environment, build number and commit, layer (scenario,
blast-radius suite, Test Plan, E2E), requirement → outcome, failing step and message. The skill
reads these in Phase 7; it posts one itself only when `RESULTS_PUBLISHER = manual`, quoting the
run summary the developer pasted, and labels it `source=manual`.

Browser evidence (`layer=e2e`) is posted through `{E2E_EVIDENCE}` by the post-deploy pipeline
(`source=pipeline`), by a tester's `/verify-ticket` session (`source=watched`), and by
`{E2E_PENDING}` when the skill runs the story's pending tests in Phase 7 — headed, with the
developer watching, `source=watched` (decision D-392552-1). A re-run for the same run ID is
skipped unless forced. `source=local` (a developer's own headless run) and `source=replay` (a
results file re-posted to a sandbox item, hardening check 1b) never count toward a pass, though a
failure they report still blocks. The skill never writes or edits an evidence comment by hand; it
summarizes them in `e2e-results` (§3.8).

A run of only the pending tests names the story's other required ids under "Not in this run", so
a partial run never reads as complete. The gate merges every evidence comment on the latest build
(newest outcome per id), so one id's pass in an earlier run is not lost by a later partial run.

The e2e evidence comment ends with two marker lines, which `assets/e2e-gate.ps1` parses:

```
[batch-remediate | evidence | - | - | run={run} | env=dev | build={number} | buildid={id} | commit={sha9} | pipeline={run id} | result={PASSED|FAILED|INCOMPLETE|NOT RUN|BLOCKED} | source={pipeline|watched|local|replay} | layer=e2e | req={passed}/{total}]
[batch-remediate | e2e-cases | run={run} | AC-1=PASS; BR-386561-ai-dynamicreporting=FAIL; ...]
```

`buildid` and `commit` let the gate refuse stale evidence; `pipeline` lets it check the run's
Tests tab. `result=BLOCKED` means nothing ran (the comment says why, after `BLOCKED -`), and the
cases line is `none`. Comments posted before this change have neither `buildid` nor the cases
line. The gate matches them on `build`, and with no case line it can name no failing scenario.

### 3.7 `solution` — end of solutioning, one per PR

`assets/SOLUTION.template.md`. Posted at Phase 5 step 9, after the PR is verified, so it carries
the PR link. One complete record of the slice: what was analyzed, what was decided (by decision
id), what was done, how it is tested, what is left. A later fix to the same PR edits this comment
(same marker, `rev` incremented) instead of posting a second one.

**Test list.** Under HOW IT IS TESTED, one line per browser test of the story, written by
`{E2E_LIST} --id {id} --markers` and read back after the deploy by `{E2E_PENDING}` and the gate:

```
[batch-remediate | test | {id} | case={AC-1} | spec=e2e/{file} | needs={a,b | none | unknown} | destructive={yes|no|unknown} | title={test title}]
```

`needs` comes from the test's annotations (`e2e.md` §1.6): `env:NAME=value`,
`dev-setting:NAME=value`, `account:throwaway`. `unknown` means the spec declares none; the skill
reads the spec and fixes the annotations in the same PR rather than posting `unknown`. The title is
last and must not contain `|` or `]`. The newest comment carrying test lines is the story's list.

### 3.7a `test-list` — Phase 7 backfill, once

For a story merged before 2.5, whose `solution` comment (if any) carries no test list. Built by
`{E2E_PENDING} --post-list`, with needs the skill read from the spec and showed the developer
(`--list`). A list with `needs=unknown` is never posted. Same test lines as §3.7, then
`[batch-remediate | test-list | {id} | tests={n}]`.

### 3.8 `e2e-results` — after the post-deploy E2E run, promotion evidence for QA

`e2e.md` §3.5. Built from the **verified** `evidence` comment of the latest deployed build: each
test that ran, the requirement it is for, what it checks in plain words, and its outcome; then
the manual Test Cases still pending for the tester. It reports a run; it is never itself evidence,
and a run that is unverified or stale is reported as such.

```
END-TO-END TEST RESULTS - Dev build {n} - {PASSED | FAILED | INCOMPLETE | BLOCKED}
Run: {pipeline run id} on {date} · Evidence: comment {id}

WHAT RAN
- AC-1 {title}: {what it checks, plain words} - PASS
- AC-3 {title}: {what it checks} - FAIL at step "{step}" - {observed}
- Regression {story}/AC-2 {title}: {what it checks} - PASS
- BR-{id}-{page} {screen}: {what it checks} - PASS

AUTOMATED TEST PLAN CASES
- Outcomes recorded in test run {run id}: {n} passed, {n} failed, {n} not executed

NOT COVERED BY E2E
- AC-4: e2e:n/a - {reason}

MANUAL TEST CASES FOR THE TESTER
- {n} pending in {plan} / {suite}: {case ids}

[batch-remediate | e2e-results | {batch} | {RUN_TAG} | build={n} | result={RESULT} | req={x}/{y}]
```

### 3.9 `logging` — with each PR, how to see the change running

`assets/LOGGING.template.md`, rules in `logging.md` §8. Posted at Phase 5 step 10. In 3.0 it is
first posted by `execute` (PR behind the flag, no `solution` comment yet), so the developer can see the logs
while smoke-testing; `tests` edits it to name the `solution` comment by id once that is posted. The `solution` comment says what was built and how it is
tested; this one says what is logged, which store it lands in (traced in code, `logging.md` §1),
where it is silent (flags, environments, console-only), how to make it happen (at volume too), the
ready-to-paste query per store, what good and bad results look like, and what the logs cannot
show.

It ends with:

```
[batch-remediate | logging | {batch} | {RUN_TAG} | pr={prId} | slice={n}/{m} | rev={r} | stores={…} | confirmed={no|build-{n}|none-seen-{n}|no-access}]
```

Later fixes to the PR edit it (`rev` +1). Phase 7 step 6 edits `CONFIRMED IN {DEPLOY_ENV}` and
`confirmed=` once the path has been exercised on the latest deployed build. Not counted as evidence:
rows in a log support a test result, they never replace one.

### 3.10 `research` — end of `research` mode

The contract the later modes reuse (`modes.md` §2). Plain text, `<br>` line breaks.
```
RESEARCH COMPLETE - {id} - {RUN_TAG} - {RESEARCHED | NEEDS-INFO}
GOAL: {one sentence}
CERTIFIED: by {developer}, comment {id} ({date}) - story rev {n}
REQUIREMENTS ({y}):
- AC-1 {text} [UI | internal] [in-app?] [PR-1]
- DOD-1 {text} [external: owner]
DESIGN: {option} - D-{id}-{n} (chosen by {developer})
FLAG: FF_{id}_{Slug} on {App Services}, checked at {entry point(s)} | exempt: {reason}, confirmed by {developer}
  Dev on: Set-FeatureFlag.ps1 -Environment dev -Name FF_{id}_{Slug} -On -Apps {roles}
WHERE: {files / services, file:line}
BLAST RADIUS: {capabilities} -> {n} scenarios · {n} callers
PR SERIES: PR-1 {title} · PR-2 {title}
SMOKE-TEST PLAN (after execute builds it):
1. AC-1: {where to go} -> {what to do} -> {what you should see}
2. ...
OPEN: RFI-{id}-n ({owner}) {question} · D-{id}-n awaiting
SUPERSEDES: {comment id | -}
NEXT: /batch-remediate execute {id}
[batch-remediate | research | {batch} | {RUN_TAG} | status={STATUS} | req={y} | src={SRC-HASH}]
```

### 3.11 `execute` — end of each `execute` iteration

```
BUILD ITERATION {n} - {id} - {RUN_TAG} - SMOKE-TEST
STORY: {unchanged since iteration {n-1} | changed: +AC-5 ~AC-2 -DOD-3 (X1)}
BUILT THIS ITERATION: {one or two lines}
REQUIREMENTS IMPLEMENTED: AC-1, AC-2, DOD-1 ({x}/{y}) · NOT YET: AC-5 ({why})
BRANCH: {branch} · PR !{n} (behind FF_{id}_{Slug}) head {sha} (verified) · {n} files +{a} -{d}
FLAG: off in every environment until turned on · Dev on: Set-FeatureFlag.ps1 -Environment dev -Name FF_{id}_{Slug} -On -Apps {roles}
BUILD: green · EXISTING TESTS: {passed}/{total} (known-red {n} unchanged)
{EXISTING TEST UPDATED: {name} - {why, agreed by developer in X2}}
{TESTS: stale - written at {sha}; run tests again once this works}
BUG REPRO (Bugs): {how it was reproduced before the fix}
SMOKE TEST - please check:
1. AC-1: {where} -> {do} -> {expect}
2. ...
Logs while you click: the queries in the logging comment ({comment id})
NEXT: not right? edit the story or reply, then /batch-remediate execute {id}
      works? /batch-remediate tests {id}
[batch-remediate | execute | {batch} | {RUN_TAG} | iter={n} | head={sha} | req={x}/{y} | src={SRC-HASH}]
```

### 3.12 `tests` — end of `tests` mode

Defined in `tests-mode.md` §7. The one comment in this protocol with a `<pre>` block: the run
script, HTML-escaped, real line breaks inside the block. Read it back after posting and fall back
to `<br>` lines if ADO stripped the block.
```
TESTS WRITTEN - {id} - {RUN_TAG} - {n} tests for {x}/{y} requirements
PR !{n} published, reviewers {names}, head {sha} (verified) · solution comment {id} (test list)
TEST MAP:
- AC-1 {text}: unit AC1_{Name} · browser "AC-1 · {title}"
- AC-2 {text}: jest {slug}-{id}.spec.js "AC-2 …"
- AC-repro: unit ACrepro_{Name} (fails on {base sha}: 1 failed; passes on {sha}: 1 passed)
- AC-4 {text}: manual - {reason} (tester, /verify-ticket {id})
MEASURED HERE: unit {p}/{t} · jest {p}/{t} · browser listed {n} (runs after the Dev deploy)
FULL SUITE: {passed}/{total} (known-red {n} unchanged)
HOW TO RUN THEM (PowerShell, from anywhere in your clone):
1. Save the script below as {id}-run-tests.ps1 (or use {RUN_SCRIPT_PATH} on the branch).
2. Before merge:  powershell -ExecutionPolicy Bypass -File .\{id}-run-tests.ps1
3. After Dev deploy:  ... -Layer e2e    ·  tester watching:  ... -Layer watch
4. Post the results:  /batch-remediate results {id}
<pre>{the script, HTML-escaped}</pre>
[batch-remediate | tests | {batch} | {RUN_TAG} | tests={n} | req={x}/{y} | head={sha} | script={RUN_SCRIPT_PATH}]
```

### 3.13 `results` — `results` mode

```
TEST RESULTS - {id} - {RUN_TAG} - {PASSED | FAILED | INCOMPLETE} - {pre-merge | Dev build {n}}
Commit: {sha} · Tests: {current | STALE - {why}}
REQUIREMENT      UNIT     JEST     IN-APP   BROWSER (Dev)   MANUAL
- AC-1 {short}:  pass     -        -        pass (pipeline run {id})  -
- AC-2 {short}:  -        FAIL     -        not yet deployed          -
- AC-4 {short}:  -        -        -        -                         awaiting tester
SOURCES: unit/jest {here | developer {machine} {time}} · browser {pipeline | watched | none} · e2e-results comment {id}
GATE: e2e-gate.ps1 {exit code and verdict} · LOGS: {confirmed build {n} | NO ROWS | could not query}
{FAILED: AC-2 - {test} - {message, one line}}
PROMOTION TO QA: {GREEN on build {n} | BLOCKED - {what is missing}}
SUPERSEDES: {comment id | -}
NEXT: {exact next action}
[batch-remediate | results | {batch} | {RUN_TAG} | result={RESULT} | build={n|pre-merge} | head={sha} | req={x}/{y}]
```
Use hyphen-led lines, not a table: the column words are padding for reading, not pipes.
`source=developer` results prove the code at that commit; browser results count only from the
pipeline or a watched run (`e2e.md` §3). After the deploy, the `results` comment is the run's
status comment; the `e2e-results` comment (§3.8) stays the promotion evidence and is not
repeated.

## 4. Reading comments

On every pass, read all comments chronologically; later wins. Classify each new one:

| Comment | Treated as |
|---------|------------|
| A `decision` answer `D-{id}-{n}: X` | Gate answer (`milestones.md` §5) |
| An `answer` line naming an RFI | Answer to that RFI |
| A person's reply quoting `RFI-…` or `D-…` | Answer, if unambiguous |
| An `evidence` comment | Run evidence for Phase 7 — check the build and the run rows |
| A `research` / `execute` / `tests` / `results` comment | The latest of each is that mode's state; its `src=` and `head=` decide whether later work is stale |
| "It's deployed / merged / ready / fixed" | A claim — record it, require the artifact |
| Anything else | Context; may reopen R10 consistency if it changes behavior |

## 5. Writing safely

- Read the latest comments before posting; skip if this pass's marker already exists (idempotent).
  Every write uses `ado-commands.md` (§5 post, §3 fields).
- Record every comment id the skill posts in the ledger's `POSTED` list.
- **Field writes (Description, AC) rewrite in place**: only the final text the developer explicitly
  approved in this session, pinned to the revision just read, in the house HTML rules (§1). No
  marker, no appended block: the field holds exactly the approved text (plus, for a User Story's
  AC, the Phase 5 branch-link line). ADO's field history is the recovery path.
- Edits to an existing comment are allowed only for this skill's own `status` comment from the
  same pass, and its own `solution` and `logging` comments for the same PR. Never edit a person's comment or a
  `decision` record.
- **Never write the certification sentence** (`enrichment.md` §7.1) in any comment or field — check
  every body before posting.
- No secrets, tokens, connection strings, or personal data in any comment.
