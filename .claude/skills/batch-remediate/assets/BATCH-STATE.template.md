# Batch State Ledger — {batch-name}

Lives at `{BATCH_ROOT}/{batch-name}/BATCH-STATE.md`, **outside the repo**, so checkouts and rebases
never touch it. One ledger per batch.

**A cache of the tickets, not the truth.** Every gate outcome, decision, RFI, and evidence item here
also exists as a marked comment on the ticket. If the two disagree, the ticket wins — re-read it
and correct the ledger. Read first, updated last, every run.

```
BATCH:      {batch-name}
CREATED:    {date}
LAST-PASS:  {RUN_TAG}
SKILL:      batch-remediate {version} · host {claude|copilot}
TICKETS:    {n} total — {n} VERIFIED · {n} READY-FOR-REVIEW · {n} NEEDS-INFO · {n} FAILED-IN-DEV · …
KNOWN-RED:  {test names failing on develop before any change, re-measured {date}}
FLAG_PROVIDER: {mechanism found in Phase 0.25, file:line}
INAPP_PAGE: {file:line}   INAPP_STEPS: {path}
```

On a version change since the last stamp: warn once at the top of the pass, name the old and new
versions, then re-stamp. Never silently reinterpret state written under different rules.

From 2.2 or earlier: a ticket with no CERTIFICATION block has never been certified. If it is past
`ENRICHING`, it goes to `AWAITING-CERTIFICATION` (show the story, ask for certification) before any
further phase runs; work already pushed stays as it is.

---

## {ticket-id} — {title}

```
TYPE:          Bug | User Story | Task
STATUS:        NEW | ENRICHING | AWAITING-CERTIFICATION | INVESTIGATING | DESIGNING | RESEARCHED
               | IN-PROGRESS | SMOKE-TEST | TESTING | READY-FOR-REVIEW | MERGED | DEPLOYED | VERIFYING | VERIFIED | DONE
               | NEEDS-INFO ({reason}) | BLOCKED | FAILED-IN-DEV | BLOCKED-E2E ({reason})
SPEC SOURCE:   story rev {n} — AC field (User Story) | Description "Acceptance Criteria" (Bug, Task)
WORKTREE:      {path}
LOG CID:       TICKET-{id}      EVENTIDS: {range}
LAST-PASS:     {RUN_TAG}

MODE           {research | execute | tests | results} · last run {RUN_TAG}
RESEARCH       comment {id} · SRC-HASH {hash} · supersedes {id | -}
ITERATIONS     execute {n} (last {RUN_TAG}, comment {id}, head {sha})
SMOKE          {developer's report at T0, as given | not reported}
TESTS          comment {id} · written at {sha} · SRC-HASH {hash} · {current | stale: {why}}
SCRIPT         {RUN_SCRIPT_PATH} at {sha}
RESULTS        comment {id} · {pre-merge | build {n}} · {PASSED | FAILED | INCOMPLETE}

GATES          M0 {open | done desc rev n} · M1 {open B | open C | done AC rev n} · MC {open | certified}
               M2 {letter} (D-{id}-{n}) · M3 {n} PRs · M4 {off | shown} · M5 {shown | hard: reason}
               M6 {answered n / routed n}

STORY REVIEW
  STEP:        {A | B | C | D | done}
  READINESS:   {before}/20 -> {after}/20
  WRITTEN:     description rev {n} · AC rev {n}   (approved by {developer}, {RUN_TAG})
  PROMPTS:     {R7 -> A (AC-5) · R6 -> skipped ×1 · …}
  SOURCES:     {url -> folded into AC-6 | none}
  WARNINGS:    R{n} {one line}

CERTIFICATION
  STATE:       {current | stale since rev {n} ({date}) | awaiting}
  COMMENT:     {commentId} by {createdBy} at {createdDate}
  CHECKED:     last content change {date} (rev {n}) — {RUN_TAG}
  ACCEPTED:    analysis comment {id}
POSTED:        {every comment id the skill posted on this ticket — never accepted as certification}

DESIGN
  CHOICE:      {A reuse | B extend | C isolated} — D-{id}-{n}
  FLAG:        {Ticket{id}.{Slug} — off Prod/UAT, on Dev, {QA} | none}
  CAPABILITIES: {id} reuse · {id} modify (consumers: …) · {id} new · {id} depends-on {ticket}
  REGRESSION SET: {capabilities} -> {n} scenarios, {n} Test Plan cases
  BLAST GRAPH: blast-radius-{id}.json hash {hash} at {head sha} · {n} scenarios · {n} hubs · set {E2E_DIR}/regression-sets/regression-set-{id}.json
  DRIFT:       {none at {sha} | DRIFT at {sha}: M2 reopened ({new ids})}

PRS            (the series; one line per slice)
  PR-1  {title}  {branch}  {PR url}  {published | draft: reason}  head {sha} verified {time}  {status: open | merged {date} | deployed build {n} | verified}
  PR-2  …

REQUIREMENTS   (AC ∪ DoD — counted together)
  [x] AC-1   {text}  [UI|internal] [in-app] [PR-1] [src: orig|Q{n}|inferred]  unit: {Test} · dev: run {id} PASS
  [ ] AC-2   {text}  [PR-2]  → NEEDS-INFO: RFI-{id}-1
  [ ] DOD-1  {text}  [external] owner {name} · evidence needed: {artifact}
  [ ] DOD-flag-removal  [external] owner {developer} · after acceptance

DECISIONS
  D-{id}-1  [M0]  ANSWERED  A  — {one line} — chat {RUN_TAG}
  D-{id}-2  [M2]  AWAITING  —  posted comment {id}

OPEN RFIs
  RFI-{id}-1: {question} — owner {name|TBD} — blocks AC-2

IN-APP         scenario Ticket{id}Scenario · params {names} · gates {actions} · ProdSafe {y/n}
               state: built (not yet run) | run: pass ({runId}, build {n}) | run: fail ({step})
TEST PLAN      suite {id}: {n} tester cases — {n} automated · {n} manual awaiting tester · {n} unmapped
TEST CASES     plan {id} suite {id} — skill-authored: {caseId} AC-1 pending · {caseId} Regression {screen} passed {date} by {tester} · …
E2E            {E2E_DIR}/{id}-{slug}.spec.js (in PR-{n}) — {n} tests, {n} e2e:n/a
               last run: {PASSED | FAILED | INCOMPLETE | BLOCKED} build {n} ({source: pipeline | watched}, comment {id})
E2E GATE       {VERIFIED | FAILED-IN-DEV: {ids} | BLOCKED-E2E: {reason} | NEEDS-INFO: {reason}} on build {n} ({RUN_TAG})
HARDENING      8.1 {proven {date} | open: deployed half} · 8.2 … · 8.6 …
PROMOTION      {green on build {n} | red: {reason} | not evaluated}

REFACTOR ENTRIES: RF-{id}-1 …
NOTES: {one or two lines of durable context for the next pass}
```

---

## Status meanings

| Status | Meaning |
|--------|---------|
| `NEW` | Queued. |
| `ENRICHING` | Phase −1 steps A–C: description, AC and sources under review (M0/M1 open). |
| `AWAITING-CERTIFICATION` | Story reviewed; waiting for the developer's certification comment, or the certification went stale after a description / AC change. No solutioning. |
| `INVESTIGATING` | Phase 0. |
| `DESIGNING` | Phase 0.25: M2/M3 open. |
| `RESEARCHED` | `research` done: research comment posted on a current certification, M2 answered. Next: `execute`. |
| `IN-PROGRESS` | `execute` building the current slice, or `tests` found a functional defect. |
| `SMOKE-TEST` | `execute` built and pushed the slice (draft PR); the developer smoke-tests. Next: `execute` again or `tests`. |
| `TESTING` | `tests` writing and running the slice's tests. |
| `READY-FOR-REVIEW` | `tests` done: PR published; every new test ran green here. Next: review, then `results`. |
| `MERGED` | Slice's PR completed, merge commit on `develop`. |
| `DEPLOYED` | Merge commit in a completed `{DEPLOY_ENV}` pipeline run. |
| `VERIFYING` | Collecting deployed evidence for the slice. |
| `VERIFIED` | Last slice green on the latest build in every required layer; promotion status green. |
| `DONE` | A person accepted the story. **Never set by the skill.** |
| `NEEDS-INFO` | Blocked on a gate answer, decision, or RFI — reason in brackets. |
| `BLOCKED` | Can't reach green on an external dependency; WIP pushed. |
| `FAILED-IN-DEV` | A deployed run failed; the fix is the next slice. |
| `BLOCKED-E2E` | ({reason}) Nothing could run against the deployed build: Dev unreachable from the agent, the build not fully deployed, the saved sign-in refused before any step, or the runner failed before any spec ran. Not a pass and not a story defect. A spec that ran and failed is `FAILED-IN-DEV`. (Reinstated after the 2.2 retirement with this narrower meaning; a 2.1 ledger's `BLOCKED-E2E` still reads as `FAILED-IN-DEV`.) |
