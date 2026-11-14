# Solution comment template

Posted to the work item **once per PR**, at Phase 5 step 9, after the PR is verified
(`references/pr-protocol.md`). It is the one complete record of what this slice analyzed, decided
and did, written for a reviewer who was not in the run, and the developer's cue to review the
diffs and approve the merge and the deploy to Dev. A later fix to the same PR **edits** this
comment (`rev` +1); it is never posted twice.

Plain text with `<br>` line breaks (`references/ado-comments.md` §1). No markdown. Never the
certification sentence, never a secret.

```
SOLUTION - {id}: {title} - PR-{n} of {m} - {RUN_TAG}
PR: {url} ({published | draft - reason}) · head {short sha}, verified {time}
Specification: the story as certified in comment {commentId} on {date} (rev {n})

WHAT WAS ANALYZED
- Requirements in this slice: AC-{n} … , DOD-{n} …
- Code read and where each requirement is implemented: AC-1 -> {file}:{line} · …
- Existing code reused or changed: {service / step} ({reuse | modify | new})
- Blast radius: {n} callers across {n} files -> {screens, reports or jobs a user would notice}
- Consumer impact: {no contract change | {symbol}: {delta}, {n} loop-nested sites answered}

WHAT WAS DECIDED
- Approach: {B - extend MailboxStatsService} (D-{id}-{n}, chosen by {developer})
- Feature flag: {Ticket{id}.{Slug} - off in Prod/UAT, on in Dev | none}
- Other decisions: D-{id}-{n} {one line} · …

WHAT WAS DONE
- {file}: {what changed, one line} (AC-{n})
- …
- Logging: TICKET-{id}, EventIds {range}; where it lands and how to query it: logging comment {commentId}

HOW IT IS TESTED
- Unit tests: {n} new, all passing; fail-before measured for {AC-repro | AC-n}
- Full suite: {pass}/{fail} (the {n} failures are the recorded pre-existing set)
- Browser spec: {E2E_DIR}/{id}-{slug}.spec.js - {n} tests, runs after the Dev deploy
- Test list (what the next pass runs after the deploy; from {E2E_LIST} --id {id} --markers):
  {AC-1} - {test title} - needs: {env:…, dev-setting:…, account:throwaway | none}{ - DESTRUCTIVE}
  …
  [batch-remediate | test | {id} | case={AC-1} | spec=e2e/{file} | needs={…} | destructive={yes|no} | title={test title}]
  …
- In-app scenario: "{button label}" on the Admin page | not applicable
- Manual Test Cases for the tester: {n} in {plan} / {suite} ({case ids}) - pending
- Security review: {no risks | risk raised: …}

WHAT IS LEFT
- {open RFI or decision, with how to answer it}
- After merge and deploy: run /batch-remediate {id} again. It finds the latest Dev build, runs
  every test above that has no result on it (you watch the browser), and posts the results here.

[batch-remediate | solution | {batch} | {RUN_TAG} | pr={prId} | slice={n}/{m} | rev={r}]
```
