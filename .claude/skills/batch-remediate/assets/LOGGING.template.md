# Logging comment template

Posted to the work item **once per PR**, at Phase 5 step 10, right after the `solution` comment
(`references/pr-protocol.md`). The `solution` comment says what was built and how it is tested.
This one is for a reader who wants to see the change **running**: what is logged, where it is
stored, how to make it happen, and the exact query that shows it. It covers the logging, not the
tests.

Written for someone who was not in the run and does not know the code: a tester, a reviewer, or
whoever runs a load test. Every store, table, resource and query must come from the sink you
traced in Phase 2 (`references/logging.md` §1), never from a convention or from memory.

A later fix to the same PR **edits** this comment (`rev` +1). Phase 7 edits it once more to record
what the queries returned on the deployed build (`references/logging.md` §8). It is never posted
twice for the same PR.

Plain text with `<br>` line breaks (`references/ado-comments.md` §1). No markdown. Queries go in as
literal lines: escape `<` and `>` as `&lt;` `&gt;` and `&` as `&amp;`, and leave `|` as it is. No
secrets, connection strings, keys or personal data. Name a storage account or App Insights
resource, never its key.

```
LOGGING - {id}: {title} - PR-{n} of {m} - {RUN_TAG}
How to see this change working in the logs. The tests are in the solution comment {commentId};
this comment covers the logging that runs whenever the code runs, including during a load test.

WHERE IT LOGS
- Active in: {all environments | Dev only - flag {Ticket{id}.Slug} | only when {condition}}
- Path, traced in code: {caller} -> {function} ({file}:{line}) -> {sink} ({file}:{line}) -> {store}
- Store: {Azure Table {table} on storage account {account} | App Insights {resource}, table {traces|requests|customEvents} | browser console only}
- Not in: {App Insights traces - this sink has no TelemetryClient ({file}:{line}) | none}

WHAT IS LOGGED
- {message prefix or event name} - written when {trigger} - {store} - shows {AC-n: plain words}
  Fields: {EventName=…, PartitionKey=…, payload fields and what each means}
- …
- Browser console only, not stored anywhere: "{message}" - when {trigger}
- Already on this path before this change, still useful: {event name} - {what it shows}

HOW TO MAKE IT HAPPEN
- {screen or endpoint}: {action}. Note the time in UTC before you start.
- For volume or load: {which page or action drives the path at scale; what to keep open, e.g. DevTools console with Preserve log}

HOW TO QUERY IT
{Store 1 name}
{query, line by line, ready to paste, with {from}/{to} as the only placeholders}
{one line: what to filter on the client side, if the store cannot, e.g. Table Storage has no contains filter}
{Store 2 name, if there is one}
{query}

WHAT GOOD LOOKS LIKE
- {rows expected, with field values and cadence, e.g. one row every 30s, inFlight 6 or lower, dropped all 0}
WHAT A PROBLEM LOOKS LIKE
- {e.g. any EventName=Error row with TICKET-{id} · a gap in the 30s rows · dropped above 0}

NOT LOGGED - limits a reader should know
- {signal that is not recorded} - {what you therefore cannot prove from the logs}
- …

CONFIRMED IN {DEPLOY_ENV}
{not yet - checked after the deploy, by the next /batch-remediate {id} run
 | build {n} ({date}): {k} rows from the queries above, {window}, after {what was exercised}
 | build {n}: NO ROWS after {what was exercised} - finding recorded in the status comment
 | build {n}: could not query - {reason}, e.g. no read access to {store}}

[batch-remediate | logging | {batch} | {RUN_TAG} | pr={prId} | slice={n}/{m} | rev={r} | stores={table:{name},traces,console} | confirmed={no|build-{n}|none-seen-{n}|no-access}]
```

## Filling it in

- **One line per signal**, not one per log statement: group statements that a reader would query
  together (the entry, branch and outcome lines of one method share a prefix).
- **Every query was run at least once** against a real store before posting, or the line says
  `(not yet run - {reason})`. A query that has never returned a row is a guess, and the comment
  says so.
- **Several stores**, e.g. a server row plus a browser console message: one `HOW TO QUERY IT`
  block for each store, in the order a reader would check them.
- **Flagged code**: say which environments emit nothing, in `WHERE IT LOGS`. A load test in an
  environment where the flag is off produces no rows for this story, and the reader must know that
  before they run it.
- **NOT LOGGED is required.** If nothing is missing, write `none found`. Typical entries: a
  snapshot with no high-water mark, a failure that only reaches the console or an `alert()`, no user
  or session id on the row, a path that bypasses the logged function.
- **No logging added by this slice** (a pure refactor, a doc change): post the comment anyway,
  with `WHAT IS LOGGED` listing the existing logs on the changed path, or saying `none - {path} has
  no logging`. Then put that gap in `NOT LOGGED`.
