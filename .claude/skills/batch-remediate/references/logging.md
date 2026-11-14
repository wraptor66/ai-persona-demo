# Ticket-correlated logging

Read this at Phase 2, at Phase 5 step 10 (the `logging` comment), and at Phase 7 step 6 (confirm
the logs on the deployed build).

**The goal:** once this branch merges to `develop` and someone exercises the application, you can
prove the new code path ran — and observe the acceptance criteria being satisfied — by querying
the logs for **this ticket alone**. The correlation ID is what makes that query possible. The
`logging` comment (§8) is how a reader who was not in the run finds out what to query, where, and
what a good result looks like.

## 1. Trace the sink before anything else

Before writing the first log line or the first query, follow one real log call from the code you
touched **to the store where the row ends up**, hop by hop, and record each hop as `file:line`.
Never take the store from a convention, from this file, or from an earlier ticket. WI 382392 told
QA to look in Application Insights, but its rows only ever reached Table Storage.

At each hop, check:

- **Is the hop wired in this call?** A sink that forwards "when configured" forwards nothing when
  it is not. Read the constructor or DI registration the caller actually uses.
- **Is it gated?** By a feature flag, an environment or hostname check, a sampling rate, a log
  level or an `if (enabled)`. Write down where the path is silent.
- **Does it leave the browser?** `console.*` and `alert()` are seen only by whoever has DevTools
  open at the time. They are not logs a reader can query later.
- **What key does the row carry?** The partition key, event name, or property you can filter on
  server-side, and what is only searchable inside the message text.

The traced path goes into the REVIEW file (§7) and into the `logging` comment's `WHERE IT LOGS`
section (§8).

## 2. The correlation ID

Every log statement added by this ticket carries `{TICKET_CID}` (e.g. `TICKET-333923`). **How** it
is carried depends on the sink you traced in §1:

- **`ILogger` with message templates** — as a **structured property**, not concatenated, so it is
  filterable and survives message rewording. Declare it once per touched class:

  ```csharp
  private const string TicketCid = "TICKET-333923";

  _logger.LogInformation(
      new EventId(3421),
      "{Cid} Membership modify resolved {Count} accounts for {GroupKey}",
      TicketCid, resolved.Count, groupKey);
  ```

- **The project's `SaveLogToBlob` sink** (most server code) — its fields are fixed
  (`EventName, MethodName, CorrelationId, ActionName`), so the CID goes **in the message text**,
  first, in a fixed form: `"TICKET-333923 Membership modify resolved 4 accounts"`. Do not add a
  parallel logging path just to get a structured property. Where that row lands depends on how
  `DataProcessing` was constructed; see `config.symphony.md` → Telemetry.

- **Client-side code** — through the page's existing UI-log function (in this repo `sendUILog`,
  which posts to the server and is stored like any other UI log), with `{TICKET_CID}` or a
  `Ticket{id}_…` event name. A `console.*` line is not a log for this purpose (§1).

## 3. Where to log

- Entry to the changed path.
- Each decision branch the fix depends on — especially the branch that was previously wrong.
- The outcome (what changed, how many, for whom).
- Every failure branch, with the exception attached.

Enough that the acceptance criteria can be **observed** in the logs, not inferred. Log the *why*,
not just the *what*: "fell back to X because Y returned zero rows" beats "fallback".

**For anything that can run at volume** (bulk actions, loops, queues, background refresh): log
the aggregate, and make the aggregate useful under load. A peak or high-water mark since the
last row, totals that cannot be lost between samples, and a stable session or run identifier,
so that rows from several browsers or instances can be told apart. A periodic snapshot of
current values alone cannot prove a limit was never exceeded between snapshots.

## 4. EventIds

- Allocate from the repo's existing convention for the surrounding service — match what
  `DataProcessing` / `DataRefreshService` and neighbors already use.
- Reserve a **contiguous block** for this ticket and record the range in the REVIEW file, so the
  ticket's events are greppable as a group.
- Never reuse an existing EventId for new meaning. Downstream queries and any alerting depend on
  an EventId meaning one thing.

## 5. Levels

- **Information** — happy-path milestones that prove the AC.
- **Warning** — handled degradation, fallbacks, unexpected-but-survivable states.
- **Error** — failures, with the exception object passed to the logger.
- Nothing the AC depends on may be `Debug`-only; `Debug` is typically off in production, so the
  evidence wouldn't exist when you need it.
- Don't log inside tight loops — log the aggregate (§3).

## 6. Never log

Credentials, tokens, full principals, or PII. Log identifiers, counts, and outcomes. If you need
to correlate to a user, log a stable non-sensitive identifier, not their mail or UPN.

## 7. Deliver the verification query

Put both of these in `REVIEW-{id}.md`, in the batch report's post-deploy verification table, and
in the `logging` comment (§8):

**1. A ready query for each store the traced path writes to** (§1). Write it for that store, and
run it once against a real store before you ship it. If you cannot, say `not yet run` next to it.

- **`SaveLogToBlob` → Azure Table** (always written; `config.symphony.md` → Telemetry has the
  table and account). Table Storage filters only on keys and plain properties, so filter
  server-side on `Timestamp`, `EventName` and, where known, `PartitionKey`, then search
  `JsonObject` for `{TICKET_CID}` on the client:

  ```
  az storage entity query --account-name {LOG_TABLE_ACCOUNT} --table-name {LOG_TABLE} --auth-mode login
    --filter "Timestamp ge datetime'{from}' and Timestamp le datetime'{to}' and EventName eq '{event}'"
    --select PartitionKey EventName ActionName JsonObject Timestamp
  ```
  Then keep the rows whose `JsonObject` contains `TICKET-{id}`. Errors: `EventName eq 'Error'`.

- **`SaveLogToBlob` → App Insights `traces`** — only when the traced call constructs
  `DataProcessing` with a `TelemetryClient` (§1). Properties are
  `{EventName, MethodName, CorrelationId, ActionName}`. There is **no `Cid` property**, and
  severity is always `Information`, so filter errors by `EventName`:

  ```kql
  traces
  | where timestamp between (datetime({from}) .. datetime({to}))
  | where message has "TICKET-333923"
  | extend EventName = tostring(customDimensions.EventName),
           MethodName = tostring(customDimensions.MethodName),
           CorrelationId = tostring(customDimensions.CorrelationId)
  | project timestamp, EventName, MethodName, CorrelationId, message
  | order by timestamp asc
  ```
  Errors only: add `| where EventName == "Error"` — never filter on `severityLevel`.

- **`ILogger` with a message template** (`"{Cid} …"`): the property lands as
  `customDimensions.Cid`. Confirm with one real trace before shipping a query that depends on it.

- **Client-side UI logs** (`sendUILog`): stored as a server UI-log row, keyed as described in
  `config.symphony.md` → Telemetry. The event name you chose and its payload are inside
  `JsonObject`.

- **In-app scenario runs** also stamp `PartitionKey` and `TestRunId`; see `inapp-testing.md`.

**Also log**, on the entry point of any flagged path: the flag name and its evaluated state, once
per request, so a trace shows whether the new path was even reachable. And log the build number
(or commit) at startup if the service does not already — post-deploy evidence is checked against it.

**2. How to exercise the path**, in one line: the screen or endpoint, and the action that triggers
it. Someone who didn't write the change should be able to follow it and see the correlation ID
appear. If the path can run at volume, also say which screen and action drive it at scale.

## 8. The `logging` comment

**When.** Phase 5 step 10: one per PR, posted right after the `solution` comment, from
`assets/LOGGING.template.md` (`ado-comments.md` §3.9). It is posted even when the slice adds no
logging. It then lists the existing logs on the changed path, or says there are none, which is
itself the finding a reader needs.

**What it must let a reader do,** without opening the code:

1. Know where the logs are, and where they are **not**. Name the environments where the path is
   silent, e.g. because of a flag, and say which store is not used, e.g. "not in App Insights
   traces".
2. Make the code run: the screen and action, and for volume or load testing, which one drives it
   at scale.
3. Paste a query and get rows: one block per store, with only `{from}` and `{to}` left to fill in.
4. Tell a good result from a bad one: expected values and cadence, and what an error, a gap or a
   bad counter looks like.
5. Know what the logs cannot show: the `NOT LOGGED` section, e.g. console-only failures, no
   high-water mark, no session id.

**Keep it true.** A later fix that changes logging edits the comment (`rev` +1). If the REVIEW
file and the comment disagree, the comment is wrong until it is edited.

**Confirm it after the deploy** (Phase 7 step 6). Once browser tests or a person have exercised
the path on the latest deployed build, run the comment's queries over that window and edit
`CONFIRMED IN {DEPLOY_ENV}`:

- rows found: the build, the window, the row count, and what was exercised;
- no rows although the path ran: `NO ROWS`, plus a finding in the status comment. Logging that
  never appears means the sink trace (§1) or the code is wrong, and the next slice fixes it;
- no read access to the store: `could not query`, the store, and an RFI naming who has access.

This confirmation is reported, not gated: it does not change the promotion state by itself. A
`NO ROWS` finding is the developer's call at the next gate.
