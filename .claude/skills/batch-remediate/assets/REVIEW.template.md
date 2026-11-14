# REVIEW — {ticket-id}: {title}

**Branch:** `feature/{id}-{slug}` · **PR:** {url} · **Slice:** PR-{n} of {m} · **Pass:** {RUN_TAG}
**Flag:** `{Ticket{id}.{Slug}}` (off in Prod/UAT) | none

Cumulative across slices and passes: append a section per slice; never rewrite an earlier one.

> This document must match the actual diff. A REVIEW that contradicts the diff is worse than no
> REVIEW — it becomes a liability in review and audit.

## 0. Story review and certification (Phase -1)

- **Readiness:** {before}/20 → {after}/20
- **Written to the story:** description rev {n} · acceptance criteria rev {n} (approved by
  {developer})
- **Prompts and answers:** R{n} {gap} → {chosen option | skipped} (→ AC-{n})
- **Sources folded in:** {url §section → AC-{n} | none}
- **Certified:** comment {id} by {developer} on {date}, story rev {n}
- **Open warnings carried into implementation:** R{n} {gap} — {how the implementation handled it}

*Omit this section only when `ENRICH_MODE = off`.*

## 0.5 Design agreement (Phase 0.25)

- **Decision:** D-{id}-{n} — {A reuse | B extend | C isolated}, chosen by {developer} ({chat | comment id})
- **Alternatives and what they would have changed:** {letter} — {one line} · {letter} — {one line}
- **Capabilities:** | Capability | Disposition | Consumers affected | Supersedes |
  |---|---|---|---|
  | `{area.capability}` | reuse / modify / new / depends-on {id} | {scenarios} | {otherId/AC-n — reason, or —} |
- **Regression set:** {n} scenarios, {n} Test Plan cases — must pass after deploy
- **PR series:** PR-1 {title} · PR-2 {title} — this REVIEW covers PR-{n}

A `modify` with no affected-consumer list is incomplete.

## 1. Specification source

- **Type:** User Story | Bug | Task
- **Spec of record:** the certified story, rev {n} — AC field (User Story) | Description
  "Acceptance Criteria" section (Bug, Task)
- Any conflict found between the story and the reproduction steps or a source, and how it was
  resolved in the story review: {…}

## 2. Acceptance criteria

| AC | Statement | Status | Proven by |
|----|-----------|--------|-----------|
| AC-1 | {text} | Satisfied | `{TestName}` |
| AC-2 | {text} | NEEDS-INFO | — (RFI-{id}-1) |
| AC-repro | Documented reproduction no longer reproduces | Satisfied | `{TestName}` |
| AC-regress | Surrounding behavior unchanged | Satisfied | Existing suite green |

## 3. Diff summary

| File | Change | Satisfies |
|------|--------|-----------|
| `{path}:{line}` | {what changed} | AC-{n} / wiki §{x} |

Anything considered and deliberately **not** changed, with the reason: {…}

## 4. Tests added

| Test | Covers | Fail-before / pass-after |
|------|--------|--------------------------|
| `{TestName}` | AC-{n} | Fails against pre-change code (confirmed), passes after |

Edge and failure branches covered: {null/empty, zero-row, exception path, …}

## 4.5 Consumer impact (Phase 1.5)

**Shared symbols modified by this diff:** {list, or "none — no symbol called from outside its
defining file was changed"}

| Symbol | Contract delta | Call sites | Loop-nested |
|--------|----------------|-----------|-------------|
| `{name}` | none / blocking→non-blocking / sync→async / ordered→unordered / bounded→unbounded / throws→swallows | {n} across {m} files | {n} |

If no contract property changed, state `no contract change` and stop here.

For each **loop-nested** call site, all three answers are required:

| Site | Pacing independent of this symbol? | Concurrent ops at realistic size | Anything else assuming per-iteration cost? |
|------|-----------------------------------|----------------------------------|--------------------------------------------|
| `{file}:{line}` | Yes/No — {what supplies it} | {n} | {progress UI / completion counting / unblock / ordering / none} |

Any site without all three answered carries a test or a recorded risk — name it here:
{test name, or risk, repeated in §7}

## 5. Logging

- **Correlation ID:** `TICKET-{id}`
- **EventId range reserved:** {range}
- **Log points added:** {entry / decision branches / outcome / failures}
- **Sink, traced in code** (`references/logging.md` §1): {caller} → {function} ({file}:{line}) →
  {sink} ({file}:{line}) → {store}. Not in: {e.g. App Insights traces — no TelemetryClient at
  {file}:{line}}. Silent in: {environments or flag states}.
- **Verification query, one per store** (run at least once, or marked `not yet run`):

```
{store 1, e.g. Azure Table Logs062625: az storage entity query … --filter "Timestamp ge … and EventName eq '…'", then JsonObject contains TICKET-{id}}
```
```kql
{store 2 only if the trace reaches App Insights, e.g. traces | where message has "TICKET-{id}" …}
```
(`SaveLogToBlob`: no `Cid` property, severity always Information — filter errors with
`EventName == "Error"`. See `references/logging.md` §7.)
- **Not logged:** {limits a reader should know, or "none found"}
- **Logging comment:** {comment id} on the ticket (Phase 5 step 10)

- **How to exercise post-deploy:** {screen or endpoint, and the action}
- **Flag check:** the entry point logs `{flag}={state}` once per request.

## 5.5 In-app test (Phase 3.6)

- **Admin page button:** "{label}" on `{INAPP_PAGE}` · **Run with blast radius** pulls in {n} scenarios
- **Parameters:** | Name | Type | Required | Sensitive | Default |
- **Requirement → step:** AC-1 → `{area.capability}` · AC-2 → `{step}` (ticket-specific)
- **Side-effect gates:** {action → gate} · default `effects: simulate`
- **ProdSafe:** yes / no — {why}
- **Queries:** `PartitionKey eq '{INAPP_PK}'` (Table `{INAPP_TABLE}`) · `customDimensions.PartitionKey == "{INAPP_PK}"`
- **State:** built (not yet run) — never reported as passing until deployed evidence exists

## 5.6 Browser spec (tests mode — runs after deploy)

- **File:** `{E2E_DIR}/{id}-{slug}.spec.js` — lists with `--list`; **not run yet** (needs Dev)
- **Requirement → browser test:** AC-1 → `AC-1 · {title}` ({n} steps) · DOD-1 → console/network check
- **e2e:n/a:** AC-{n} — {one-line reason}
- **Changes data on Dev:** none | {step title that does, and how it cleans up}
- **After deploy:** `{POSTDEPLOY_PIPELINE}` runs it and posts evidence; the tester can watch it with
  `{TESTER_PROMPT}`

## 5.7 Manual Test Cases for the tester (Phase 3)

- **Plan / suite:** {plan name} ({id}) / {suite name} ({id})
- **Cases:** {caseId} {title} → AC-{n} · {caseId} {title} → Regression {screen or report}
- **manual:n/a:** AC-{n} — internal, proven by `{TestName}`
- **State:** pending until the tester records a pass; never mapped to automation

## 5.8 Run script (tests mode)

- **File:** `{RUN_SCRIPT_PATH}` — also posted in the `tests` comment ({comment id})
- **Measured here:** `-Layer unit,js` → unit {p}/{t}, jest {p}/{t}; `-Layer e2e -ListOnly` → {n} listed
- **Before merge:** `powershell -ExecutionPolicy Bypass -File {RUN_SCRIPT_PATH}`
- **After Dev deploy:** `… -Layer e2e` (headless, `source=local`) or `… -Layer watch` (tester watching)

## 6. Security review (OWASP)

| Item | Outcome | Note |
|------|---------|------|
| Broken access control | Addressed / N/A / **Risk raised** | {how, or why not applicable} |
| Injection | | |
| XSS (front end) | | |
| Sensitive data exposure | | |
| Security misconfiguration | | |
| CSRF / state-changing endpoints | | |
| SSRF / deserialization | | |
| Dependencies | | |

Any **risk raised** is repeated in the batch report. Never leave a risk recorded only here.

## 6.5 Deployed evidence (Phase 7 — appended after deploy)

| Layer | Build | Run | Result | Notes |
|-------|-------|-----|--------|-------|
| Ticket scenario | {n} | {runId} | PASS / FAIL at {step} | |
| Blast-radius suite | {n} | {suiteRunId} | {n}/{n} scenarios | {failing capability → other ticket} |
| Test Plan {suite} | {n} | {runId} | {n} passed · {n} manual awaiting | |
| Browser (E2E) | {n} | {pipeline run / watched} | {n}/{n} · {n} e2e:n/a | evidence comment {id} |

**Promotion status:** green on build {n} | red — {story-level reason}

## 7. Residual risk

{What could still go wrong, what wasn't covered, what to watch after deploy.}

## 8. Follow-ups

- Deferred scope (out of this ticket, needs its own work item): {…}
- Refactoring opportunities raised: RF-{id}-{n} → see `{BATCH_ROOT}/{batch}/REFACTOR-OPPORTUNITIES.md`
- Flag removal: `DOD-flag-removal` — after acceptance
- Convergence (isolated design only): RF-{id}-{n}
- Open RFIs still blocking completion: RFI-{id}-{n}
