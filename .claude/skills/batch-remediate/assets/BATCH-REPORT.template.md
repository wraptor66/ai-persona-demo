# Batch Remediation Report — {batch-name} — {yyyymmdd-hhmm}

**Batch:** {batch-name} · **Run tag:** {RUN_TAG} · **Base branch:** `develop` · **Tickets:** {ids}

**Skill:** batch-remediate {version} · host {claude|copilot}

## Verdict

```
Waiting on certification:      {n}  {ids}   (never certified, or stale after a story change)
Waiting on a developer gate:   {n}  {ids}
Waiting on information (RFI):  {n}  {ids}
Waiting on review / merge:     {n}  {ids}
Waiting on deploy to Dev:      {n}  {ids}
Waiting on evidence in Dev:    {n}  {ids}
FAILED IN DEV:                 {n}  {ids}   <- read §3.5
Blocked (external):            {n}  {ids}
VERIFIED (promotion green):    {n}  {ids}
Requirements proven in Dev:    {x} / {y}  (AC ∪ DoD)
Decisions taken this pass:     {n}  (by developer {n} · by DEFAULTS {n})
```

**Status counts:** {n} VERIFIED · {n} READY-FOR-REVIEW · {n} IN-PROGRESS · {n} NEEDS-INFO · {n} BLOCKED · {n} FAILED-IN-DEV
**Requirement completion:** {x} / {y} items proven by unit test · {x} / {y} proven in Dev

> Snapshot of this pass. Must match `BATCH-STATE.md` and the actual branches, PRs, and REVIEW
> files. The Verdict, §2.7 open gates, and §7 are printed to the console at the end of the run.

---

## 1. Status table

| Ticket | Type | Status | Readiness | AC done | Branch | PR | Summary |
|--------|------|--------|-----------|---------|--------|----|---------|
| {id} | Bug | NEEDS-INFO | 11→17/20 | 2/5 | `feature/{id}-{slug}` | {url} | {one line} |

## 2. Changed since last pass

- **Advanced:** {what moved forward}
- **Newly cleared:** {information that arrived and unblocked something}
- **Newly blocked:** {what stalled and why}

*(First pass: state that this is the initial run.)*

## 2.5 Story review and certification (Phase -1)

| Ticket | Before → after | Step | Written | Certification | HALT | Open warnings |
|--------|----------------|------|---------|---------------|------|---------------|
| {id} | 9 → 18 /20 | done | desc rev 14 · AC rev 16 | comment 5581234, 10-21 | — | R6 out-of-scope not stated |

Tickets halted here (H1–H3) also appear in §7 with their RFIs. Any ticket whose story changed after
certification is listed here as *certification stale — rev {n} by {who} on {date}*; it does no work
until re-certified.

## 2.7 Gates and decisions

| Ticket | M0 | M1 | MC | M2 (design) | M3 (slices) | M5 | M6 |
|--------|----|----|----|-------------|-------------|----|----|
| {id} | done | done | certified | B extend · flag Ticket{id}.{Slug} | 3 PRs | shown | 2 answered / 1 routed |

Decisions resolved by `DEFAULTS` are listed separately here so the acceptance is on the record.

## 2.8 Capability map

| Capability | Tickets consuming | Disposition this pass | Open `depends-on` (merge order) | Supersessions |
|------------|-------------------|-----------------------|----------------------------------|---------------|

## 3. Per-ticket detail

### {ticket-id} — {title}

- **Spec source:** {wiki URL, or ticket AC field}
- **AC checklist:** {x/y} — AC-1 ✔ `{TestName}` · AC-2 ✖ blocked by RFI-{id}-1
- **Files touched:** {paths}
- **Correlation ID / EventIds:** `TICKET-{id}` / {range}
- **Blocker (if NEEDS-INFO or BLOCKED):** {precise, one line}

## 3.5 Deployed evidence and promotion gate

| Ticket | Slice | Dev build | Scenario | Blast radius | Test Plan | E2E | Promotion to QA |
|--------|-------|-----------|----------|--------------|-----------|-----|-----------------|
| {id} | 2/3 | {n} | pass ({runId}) | 7/7 | 5 auto pass · 2 manual awaiting | 4/4 | red — manual cases |

Skipped, not run, and not automated are reported in their own words — never folded into passed.
A **FAILED IN DEV** row names the requirement, the step, the observed behavior, and the next slice.

## 4. Post-deploy verification

How to confirm each change actually fired once merged and exercised.

| Ticket | Correlation ID | How to exercise | Store and query (from the `logging` comment) |
|--------|----------------|-----------------|-----|
| {id} | `TICKET-{id}` | {screen/endpoint + action} | `message has "TICKET-{id}"` |

## 5. Security review summary

Any OWASP item marked **risk raised** across the batch:

| Ticket | Item | Risk | Recommendation |
|--------|------|------|----------------|

*If nothing was raised, say so explicitly — an empty section reads as an omission.*

## 6. Refactoring opportunities raised this pass

| ID | Title | Suggested ADO ticket |
|----|-------|----------------------|
| RF-{id}-1 | {title} | {paste-ready work item title} |

Candidates for new work items — not work in flight.

## 7. Issues requiring resolution

**The point of this document.** Every open information request and dependency, ordered by how many
AC items and tickets it blocks. A Bug missing its wiki page ranks above everything else — nothing
can proceed without it.

| ID | Ticket / AC | Question or dependency | What's needed to resolve | Likely owner |
|----|-------------|------------------------|--------------------------|--------------|
| RFI-{id}-1 | {id} / AC-2 | {specific, answerable} | {artifact or decision} | {name \| TBD} |

Be specific and answerable. "AC-2 doesn't define the empty-result response; needs a product
decision" beats "unclear requirements."

## 8. Deferred follow-ups

Scope-creep items pulled from the REVIEW files, so nothing found mid-run is lost. Not part of any
current branch.

- {ticket} — {item} → needs its own work item
