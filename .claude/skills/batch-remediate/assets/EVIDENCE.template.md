# Evidence comment template

Posted by the Symphony web app's results publisher when an Admin-page run completes
(`references/post-deploy.md` §4) — or by the skill with `source=manual` from a summary the
developer pasted. Plain text with `<br>` line breaks. Never contains sensitive parameter values.

**Browser (E2E) evidence** is posted by `{E2E_EVIDENCE}` — from the post-deploy pipeline
(`source=pipeline`) or a tester's watched session (`source=watched`); `source=local` is a
developer's own run and never counts. Its body differs
(one screenshot per test, console errors, failed requests, the server-log window, an attached zip
of traces and videos, and the tester's verdict per requirement when watched), and its marker adds
`layer=e2e` and `req={passed}/{total}`:

`[batch-remediate | evidence | - | - | run={runId} | env=dev | build={n} | result={RESULT} | source=pipeline | layer=e2e | req={x}/{y}]`

Its `result` is `PASSED`, `FAILED`, `INCOMPLETE`, or `NOT RUN` (sign-in or environment — proves
nothing). A tester's `fail` verdict makes the result `FAILED`; a requirement the tester left
without a pass or fail keeps it `INCOMPLETE`. Every comment for a story in the build also lists
other stories' specs that failed in the same run — a possible regression from this build.

```
TEST EVIDENCE - {layer} - {RESULT: PASSED | FAILED | INCOMPLETE}
Environment: {dev} · Build: {build number} · Commit: {short sha}
Run: {runId} · Started by: {principal} · {start} -> {end} ({duration})
Layer: ticket scenario | blast-radius suite | Test Plan {plan}/{suite} | E2E

REQUIREMENTS
- AC-1 {short text}: PASS ({step name})
- AC-2 {short text}: FAIL at step {n} {step name} - {message}
- DOD-1 {short text}: PASS
- AC-4: NOT RUN - {reason}

BLAST RADIUS (suite runs only)
- {capabilityId}: {n}/{n} scenarios passed
- {capabilityId}: FAIL in Ticket{otherId}Scenario - asserts {otherId}/AC-{n}

TEST PLAN (Test Plan runs only)
- Case {caseId} {title}: PASSED (automated: {AutomatedTestName})
- Case {caseId} {title}: NOT AUTOMATED - awaiting manual pass

Query: {INAPP_TABLE} PartitionKey eq '{INAPP_PK}' and RunId eq '{runId}'

[batch-remediate | evidence | - | - | run={runId} | env={env} | build={build} | result={RESULT} | source={publisher|manual}]
```

The batch and run-tag slots are `-` because the web app posts outside any batch; with
`source=manual` the skill fills them with the batch and `{RUN_TAG}`.

`INCOMPLETE` = any requirement NOT RUN or any case NOT AUTOMATED without a manual pass. Only
`PASSED` on the latest build counts toward the promotion gate.
