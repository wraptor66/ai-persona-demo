# Design agreement — Phase 0.25 (gates M2, M3)

Read at Phase 0.25, after the Phase 0 checklist and manifest exist. The output is a **decision the
developer made**, recorded with its alternatives, and a **PR series** that can be merged and
deployed one slice at a time.

## 1. Find what already exists

Before proposing anything, search — on `develop` **and on every open branch recorded in any ledger
under `{BATCH_ROOT}`**:

- **Services and methods** that already do part of what the requirements need (name search, then
  read the candidates; pin `file:line`).
- **Capability steps** in `{INAPP_STEPS}` whose capability ID matches what the requirements exercise
  (`area.capability`, lower-kebab).
- **Feature-flag mechanism**: Symphony's is App Service settings read as environment variables,
  through the shared `FeatureFlags.IsEnabled` reader (`feature-flags.md` §1–2). **An existing
  mechanism always wins.** Record it as `FLAG_PROVIDER` in the ledger the first time.
- **Open PRs touching the same files** — a conflict now is cheaper than a conflict at merge.

Record each finding as `reuse`, `modify` (exists; this ticket changes its behavior),
`depends-on {id}` (exists only on another ticket's unmerged branch), or `new`.

## 2. Compute the blast radius

### 2.1 The call graph (L1)

Generate it; never assemble it by hand. `{BLAST_GRAPH}` is `assets/blast-radius-graph/`, a .NET
console tool that needs only the .NET SDK (README there). From a worktree whose `HEAD` is the code
being judged:

```
dotnet run --project {BLAST_GRAPH} -- graph --id {id} --base {base} --head HEAD
dotnet run --project {BLAST_GRAPH} -- graph --id {id} --symbol cs:Type.Member --symbol js:functionName
```

- At Phase 0.25 there is no diff yet: name the members each option would change with `--symbol`
  (`cs:Ns.Type.Member(string)` picks one overload; `cs:Type.Member` picks all of them). After the
  code exists, use `--base` (the merge-base with `develop`) and the tool finds the changed members.
- It writes `blast-radius-{id}.json`: changed symbols → callers → controller routes → front-end
  calls → pages, and one **scenario** per page reached (`BR-{id}-{page}`). C# callers are resolved
  semantically (overloads and same-named members on other types are told apart); a project that
  will not load is read syntax-only and its edges are `low`, never dropped. JS edges are name-based
  (`medium`, or `low` when several files define the name).
- The output is the same on every run for the same commits: sorted, no timestamps, with a `hash`.
  Record the hash in the ledger (`BLAST GRAPH: {hash}`).
- Read every `low` edge before accepting its scenario. Drop a false positive only at M2, by moving
  the scenario to `excluded` with a reason (§2.3). Never edit the graph.
- `assets/blast-radius.ps1` / `.sh` still answer the separate **pacing** question: is any caller
  inside a loop? Run them on each changed shared member; a loop-nested caller is a Phase 1.5
  question now, not later.

### 2.2 From the graph to tests

1. Each scenario is a screen a user can open. It becomes one browser spec test (`e2e.md` §1, title
   starting with the scenario id), one automated Test Case (`manual-test-cases.md` §6), and one
   manual Test Case for the tester (`manual-test-cases.md` §1).
2. Map each caller to a capability ID, and each capability to the in-app scenarios that compose it.
3. Map each capability to Test Plan cases whose automation association names it
   (`AutomatedTestName` = `inapp:{capabilityId}` or an E2E spec in that area).
4. `Internal` in the graph lists changed members that reach no page: those are proven by unit
   tests only, and the REVIEW says so.

### 2.3 The regression set (L2)

```
dotnet run --project {BLAST_GRAPH} -- regression-set --graph blast-radius-{id}.json
```

writes `regression-set-{id}.json`: every scenario and every caller node in the graph, plus the
graph's hash. That is the M2 **proposal**. The developer may move a scenario to `excluded` with a
reason; that edit is the only one allowed, and the decision record lists each exclusion. The
accepted file is committed with the story's first PR (`{E2E_DIR}/regression-sets/`), so the
post-deploy gate and later re-checks read the same set. It goes into the decision record and the
ledger, and each of its scenarios becomes a plain-language manual Test Case for the tester in
Phase 3 (`manual-test-cases.md` §1).

### 2.4 Drift reopens M2 (mechanical)

The 2.x rule "a caller outside the agreed regression set reopens M2" is checked, not remembered.
At Phase 3 before the PR, at Phase 5 before each later slice's PR, and in Phase 7 before the
gate, regenerate the graph at the current head and run:

```
dotnet run --project {BLAST_GRAPH} -- check --graph blast-radius-{id}.json --set {E2E_DIR}/regression-sets/regression-set-{id}.json
```

Exit 0: no drift. **Exit 3: drift.** A new scenario, a new caller node or a new changed member
appeared after M2 was agreed. Stop that slice, show the `NEW` lines, and reopen M2 with the
grown blast radius. Never regenerate the set to make the check pass. A scenario that is no longer
reached is informational, and stays in the set until M2 drops it.

## 3. The options

Offer 2–3 genuinely viable options. Never pad with a straw option.

| Option | Fits when | Blast radius | Post-deploy obligation |
|--------|-----------|--------------|------------------------|
| **A. Reuse as-is** — call existing code unchanged | Existing behavior already satisfies the requirement | New path only | This ticket's scenario |
| **B. Extend shared code** — change an existing member | The requirement is a variation of existing behavior | Every caller of the changed members | Scenario + full regression set |
| **C. Isolated slice** — new code beside the old; shared code untouched | Shared code is fragile, untested, has many callers, or the change is experimental | New path only (by construction) | Scenario + a convergence item in the refactor log |

**Isolated never means copied.** Option C calls shared code for anything it needs; it adds new
code only for the new behavior. Copying a shared method into the slice violates the
"shared, never duplicated" invariant and is not an option to offer.

### Recommendation rules (in order)

1. A requirement already met by existing code → **A**.
2. The change is additive for every existing caller (new optional parameter, new overload, new
   branch reached only by the new path) → **B**.
3. The shared member has no unit tests, more than ~10 callers, a loop-nested caller, or a recorded
   incident → **C**, and say which condition decided it.
4. Otherwise → **B**, with the regression set as the price stated plainly.

Record the deciding rule in the recommendation's grounds.

## 4. Feature flag — every story has one (3.1)

Every story is designed behind a flag (`feature-flags.md`). It is not a question at M2; M2 shows
it. The only exemption is a change that **cannot** be gated (a schema-only SQL change, a build or
pipeline file); the developer confirms the exemption at M2 and the reason goes on the ticket.

- **Name** `{FLAG_PREFIX}{PascalSlug}` — e.g. `FF_361204_MailboxStatsColumns`.
- **App Services**: every one that runs the gated code (web, DataAPI, Symphony-api, monitoring).
- **Defaults** off everywhere in committed code; on in Dev after the `execute` merge; QA, UAT and
  Prod turned on by a person (`feature-flags.md` §5).
- **Placement**: the entry point only — controller action, menu registration, scenario
  registration, a DI registration choosing the implementation. Never `if (flag)` deep in business
  logic, data access, or validation.
- **Tests**: every requirement's tests run flag on; one test per entry point proves flag off = the
  old behavior (`DOD-flag-off`); the in-app scenario runs with the flag
  forced **on** for its own `OperationContext`, so a dark feature can be proven in any environment.
- **Removal**: add `DOD-flag-removal` to the checklist as an external item (owner: the developer,
  evidence: the removal PR after acceptance) and a flag-removal entry in the refactor log.

## 5. Gate M2

Present the options as one table and ask (see `milestones.md` §3):

```
M2 · Approach — {id}
      Where                     Blast radius            Flag                       Effort  Risk
  A   {file / class}            new path only           no                         S       {one line}
  B   {file / class}            3 capabilities, 7 sc.   Ticket{id}.{Slug} (rec.)   M       {one line}
  C   {new class, calls X}      new path only           Ticket{id}.{Slug}          M       {one line}
Recommended: B — {deciding rule and evidence}
```

Then post the decision record (`assets/DECISION.template.md`) with all three options and what each
would have changed. Record `DESIGN: {letter} (D-{id}-{n})`, `FLAG`, and `REGRESSION SET` in the
ledger.

## 6. Slicing — the PR series (gate M3)

Testing is only meaningful in a deployed environment, so the story is delivered as a series of
PRs that each merge and deploy safely on their own.

Typical slices, in order (drop any that don't apply):

1. **Skeleton** — flag registration (off), DTOs, interfaces, scenario registration with its
   parameter schema, no behavior change.
2. **Service / data** — the core logic and its unit tests; scenario steps that exercise it.
3. **UI** — the user-facing surface behind the flag; jest specs.
4. **Cleanup** — anything the earlier slices left (removing a fallback, tightening validation).

Rules:

- Each slice states why it is safe to deploy alone with the flag off.
- Each slice lists the requirements it proves **after its own deployment** — a requirement may be
  proven by a later slice, never by none.
- Stack slices (`PR-2` cut from `PR-1`'s branch) only when the second cannot compile without the
  first; otherwise cut each from `develop` after the previous merges.
- A small story is one slice. Do not invent slices.

Record the series in the ledger (`PRS`), show it at M3, and carry it in the status comment.
