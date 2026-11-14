# Coding standards and security review

Read this at Phase 1. **Repo convention wins wherever it differs from this document** — the point
is consistency with the surrounding code, not conformance to a generic list.

---

## 1. Coding standards

### Structure

- New behavior goes in its **own class behind an interface** where a seam exists, rather than
  swelling an existing class. This is what makes the change testable without a live dependency.
- Single responsibility per class and method.
- Constructor injection over `new`; no static mutable state.

### Async

- Async all the way down. No `.Result`, no `.Wait()`, no `async void` (except event handlers) —
  these deadlock under load and hide failures.
- Pass `CancellationToken` where the surrounding signature offers one.
- `ConfigureAwait` per repo convention.

### Failure handling

- **No swallowed exceptions.** Never `catch { }`, and never catch-and-return-default without
  logging the cause and either rethrowing or returning an explicit, typed failure.
- A silent no-op is a defect, not a fallback. If an operation can fail in a way the caller must
  know about, the signature should say so.

### Guards and clarity

- Validate arguments at entry; fail fast with a message naming the offending argument.
- No magic strings or numbers — named constants.
- Intention-revealing names. Comments explain *why*, not *what*.
- Delete dead code rather than commenting it out; version control is the archive.

### Data access

- **Parameterized queries only** (Dapper / ADO.NET). No string-concatenated SQL, ever.
- Dispose every connection, command, and reader (`using`).
- Handle the zero-row and null-column cases explicitly rather than relying on mapping luck.

### Nullability and configuration

- Handle null explicitly at boundaries; don't push the burden to the caller.
- **No secrets in source** — configuration / Key Vault only. Never a credential, connection
  string, token, or key literal in code, tests, or logs.

### Feature flags

- **Every story has a flag** (`references/feature-flags.md`). Use the project's existing
  mechanism (`FLAG_PROVIDER`: App Service settings through `FeatureFlags.IsEnabled`). Never
  introduce a second one.
- The flag is read at the **entry point only** — controller action, menu or scenario registration,
  DI choice of implementation. Never inside business logic, data access, validation, or
  authorization.
- Name `{FLAG_PREFIX}{PascalSlug}` (`FF_{id}_{Slug}`); absent = off; nothing committed turns it on.
- Both states have a unit test at the entry point. Flag-off must be the exact pre-change behavior.
- Every flag ships with a removal item (`DOD-flag-removal` + a refactor-log entry).

### Pacing in client loops

- A loop that issues requests must have **explicit** pacing — a concurrency cap, `await` per
  iteration, or a shared pacing utility. Never rely on a call inside the loop being slow.
- **`setTimeout(…, 0)` is not pacing.** It yields the task queue but introduces no delay and does
  not bound in-flight work — a loop that only has this is unpaced.
- **An async conversion of any loop body must add explicit pacing in the same change** if the loop
  issues requests. Converting a loop from sync to async can silently delete pacing that was never
  declared — it was a side effect of the call being synchronous. The change that removes the
  synchronous call is the change that must add the replacement pacing; do not defer it.
- Changing a shared client helper's timing (sync → async, blocking → fire-and-forget) is a contract
  change: run Phase 1.5. Worked example: WI 370037 / commit fc6fa9755 (the async rewrite that
  silently removed pacing from every affected loop at once) and WI 382392 (the follow-up that
  generalized the fix and closed the recurrence gap).

### Client requests

- Build requests through `parent.GetFromServer`, which injects the mandatory `CorrelationID`. A
  hand-built worker request omits it and fails server-side before any log is written.

---

## 2. OWASP security review

Walk this list against your diff **every change**. Record the outcome in `REVIEW-{id}.md` under
**Security review**, marking each item:

- **Addressed** — with a one-line note on how.
- **Not applicable** — with a one-line reason. ("No data access in this diff.")
- **Risk raised** — the change can't satisfy it without exceeding scope. Escalate to the REVIEW
  file and the batch report. Never silently ship an unaddressed risk.

### Checklist

**Broken access control**
Every authorization decision is enforced **server-side** (`HttpContext.User` / the server-side
principal). A check that exists only in client-side JavaScript is not a control — it's a UI
convenience that an attacker skips entirely. Never trust client-decoded token fields or
client-supplied identity/role values.

**Injection**
SQL parameterized; no dynamic SQL built from user input; no command or LDAP injection paths.
Validate and whitelist input at the boundary.

**XSS (front end)**
In jQuery views, prefer `.text()` over `.html()` for any value originating from user input or an
API response. Where HTML is genuinely required, encode explicitly. Attribute and URL contexts need
their own encoding — `.text()` doesn't cover an `href`.

**Sensitive data exposure**
No credentials, tokens, connection strings, or PII in logs, error messages, or client responses.
Surface a correlation ID to the user, never a stack trace.

**Security misconfiguration**
No auth, validation, CORS, TLS, or cookie flag weakened to make something pass. If a test needs
auth relaxed, the test is wrong.

**CSRF / state-changing endpoints**
State-changing operations use the correct HTTP verb and the repo's existing anti-forgery
protection.

**SSRF and deserialization**
No user-controlled URLs fetched server-side. No deserialization of untrusted payloads into
arbitrary types.

**Admin and test endpoints**
Anything reachable from the Azure Admin page enforces an admin role server-side, deny by default.
Test-run endpoints validate the payload against the scenario's schema before executing, refuse
production unless the scenario is declared `ProdSafe` and config allows it, never echo sensitive
parameters, and record who started the run.

**Dependencies**
No new package added casually. If the change requires one, name it and its justification in the
REVIEW file so it can be reviewed on its own merits.

**Client loop pacing (availability)**
Any new or modified front-end loop that issues requests per iteration is checked against the
"Pacing in client loops" rule above: explicit pacing present, `setTimeout(…, 0)` not relied on as
pacing, and an async conversion of the loop body carries its own replacement pacing in the same
change. Worked example: WI 370037 / commit fc6fa9755 / WI 382392.
