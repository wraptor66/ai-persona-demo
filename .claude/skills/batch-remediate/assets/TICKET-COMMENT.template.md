# Ticket status comment template

**3.0:** each mode posts its own comment (`ado-comments.md` §3.10–3.13) and that comment carries
the status content below in condensed form. This full template is used for a run without a mode
comment (a 2.x ledger resumed) and as the source for the sections the mode comments reuse
(OUTSTANDING, WHAT YOU NEED TO DO, the variants).

Posted to the Azure DevOps work item **once per pass** — at Phase 6, or at Phase 7 when only the
deployed state moved. If Phase 7 changes the state after this pass's comment is posted, edit that
comment instead of posting another. Written for someone who wasn't in the run: what state is this in, what
was decided, what's left, and what do I need to do? Other comment types — analysis, decision,
answer, evidence — are defined in `references/ado-comments.md` and their own templates.

## Formatting rules

Azure DevOps comments are an **HTML field**. Write plain text with simple structure:

- **No markdown tables** — pipes render as literal pipes.
- **No markdown syntax** — no `**bold**`, no `#` headings, no backtick code fences. They render
  as raw characters.
- **Line breaks:** if posting via the REST API, convert each newline to `<br>`, since plain
  newlines collapse in an HTML field. Use `<b>` for the few labels worth emphasising and `<ul>`/
  `<li>` for lists if the client accepts them; otherwise hyphen bullets read fine.
- Keep lines short. This is read in a narrow comment pane.

Omit sections that don't apply rather than writing "N/A" — except OUTSTANDING, which always
appears, even if only to say nothing is blocking.

---

## Template

```
Batch run {RUN_TAG} - {STATUS} - acceptance criteria {x}/{y} satisfied

DELIVERED THIS PASS
{One or two lines on what actually changed. If nothing changed, say so and why.}
{If the story review wrote to the story this pass: "Description and acceptance criteria
rewritten with {developer} (rev {n}); earlier text is in the History tab."}

ACCEPTANCE CRITERIA
- AC-1 {text}
  SATISFIED - proven by {TestName}
- AC-2 {text}
  OUTSTANDING - see RFI-{id}-1 below
- AC-repro Documented reproduction no longer reproduces
  SATISFIED - {TestName} fails against pre-change code, passes after
- AC-regress Surrounding behaviour unchanged
  SATISFIED - existing suite green

DESIGN
Approach: {B - extend MailboxStatsService} (decision D-{id}-{n}, chosen by {developer})
Flag: {Ticket{id}.{Slug}} - off in Prod/UAT, on in Dev
Regression set: {n} scenarios across {capabilities}

BRANCH / PR SERIES
- PR-1 {title}: {url} - merged, deployed to Dev build {n}, verified
- PR-2 {title}: {url} - published, reviewers {names}, head {sha} verified   <- this pass
- PR-3 {title}: not started

TEST IN DEV (after deploy)
Admin page: Ticket Tests > "{button label}" (Run, or Run with blast radius)
Parameters you will be asked for: {names; sensitive ones marked}
Test Plan: {n} cases automated, {n} awaiting a manual pass by {tester}
Promotion to QA: {GREEN on build {n} | BLOCKED - {reason} | not yet evaluated}

VERIFICATION QUERY
Correlation ID: TICKET-{id}
Exercise via: {screen or endpoint, and the action}
Query: traces | where message has "TICKET-{id}"

OUTSTANDING
- RFI-{id}-1 ({owner}): {the specific question}
  Blocks AC-{n}. Needs: {artifact or decision}.
- D-{id}-{n} (awaiting): {question} - reply "D-{id}-{n}: B" to answer
- Dependency: {e.g. stored procedure prX not yet deployed in DEV}
  Blocks green build. Owner: {name}.

WHAT YOU NEED TO DO
{the developer handoff, from assets/HANDOFF.template.md - numbered actions, links,
"done when" for each, then "waiting on others", then "the story is finished when"}

GATES THIS PASS
M3 plan shown: {n} PRs · M5 pre-PR shown: {no risks | risk raised: …}

DEFERRED - NEEDS ITS OWN WORK ITEM
- {follow-up or refactor item, with the suggested ticket title}

NOTE: this ticket has not been marked complete. The skill sets VERIFIED
only when every requirement passes in Dev on the latest build; a person
moves it to Done.

[batch-remediate | status | {batch-name} | {RUN_TAG} | status={STATUS} | req={x}/{y} | slice={n}/{m}]
```

The marker counts requirements over AC ∪ DoD (`req=`), not acceptance criteria alone. Older 1.x
comments used `ac=`; read both.

The final bracketed line is the idempotency marker — read on later passes to decide whether
anything changed. Plain text on purpose: it stays visible and readable whatever the field does
with it, and a human scanning the ticket can see which run produced the comment.

---

## Variants

### AWAITING-CERTIFICATION, no work started or resumed

Lead with the ask. Drop BRANCH/PR and VERIFICATION. Never quote the certification sentence; point
to the README.

```
Batch run {RUN_TAG} - AWAITING-CERTIFICATION - no work started

DELIVERED THIS PASS
No implementation - the story has not been certified{, or it changed after
it was certified (rev {n}, {date}, by {who})}.

OUTSTANDING
- {developer}: review the description and acceptance criteria, then add the
  certification comment described in the batch-remediate README, section
  "Certifying a story". The next run checks it and starts the work.

NOTE: no branch or code changes were made this pass.

[batch-remediate | status | {batch-name} | {RUN_TAG} | status=AWAITING-CERTIFICATION | req=0/{y}]
```

### NEEDS-INFO, stopped at the readiness gate (Phase -1 HALT)

Lead with the decision needed. Offer the proposed answer so a one-word reply unblocks it.

```
Batch run {RUN_TAG} - NEEDS-INFO - readiness gate, no work started

DELIVERED THIS PASS
No implementation - the story does not yet say what done looks like.

OUTSTANDING
- RFI-{id}-1 ({owner}): {the specific question}
  Proposed answer: {the recommended option, as criterion text}
  Reply "yes" to accept it, or give the behavior you want.

NOTE: no branch or code changes were made.

[batch-remediate | status | {batch-name} | {RUN_TAG} | status=NEEDS-INFO | req=0/{y}]
```

### BLOCKED, partial work pushed

Keep BRANCH/PR. Name the dependency precisely.

```
Batch run {RUN_TAG} - BLOCKED - acceptance criteria 2/4 satisfied

DELIVERED THIS PASS
Implementation complete as far as the dependency allows, pushed to
feature/{id}-{slug} (draft PR marked [BLOCKED]). Build cannot go green
until {dependency} exists in {environment}.
```

### FAILED-IN-DEV, a deployed run failed

Lead with what failed and whose requirement it is. Never soften it.

```
Batch run {RUN_TAG} - FAILED-IN-DEV - Dev build {n}

WHAT FAILED
- AC-{n} {text}: failed at step {step} - {observed behavior}
  Evidence: run {runId} (comment {id})
- Blast radius: {capability} now fails {otherId}/AC-{n} in Ticket{otherId}Scenario

NEXT
The fix will be PR-{n+1} in this story's series. Promotion to QA is blocked
until it passes in Dev.

[batch-remediate | status | {batch-name} | {RUN_TAG} | status=FAILED-IN-DEV | req={x}/{y} | slice={n}/{m}]
```
