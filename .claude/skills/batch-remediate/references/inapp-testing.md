# In-app scenario and blast-radius suite — Phase 3.6

Read at Phase 3.6. Runs when `INAPP_MODE = on`. The goal: once a PR deploys to **any** environment,
an admin clicks one button on the Azure Admin page, supplies the values it asks for, and gets a
pass/fail walk through the whole process **inside the deployed App Service** — real VNet, private
endpoints, managed identities — queryable afterwards by ticket alone. This is product code, so it
ships in the ticket's PR, as the browser spec does (`e2e.md` §1).

## 1. The harness (built once, shared by every ticket)

Check for it before writing a scenario. It consists of:

- **`ITicketScenario`** — `TicketId`, `TicketName`, `PartitionKey`, `Title`, `Parameters` (schema),
  `ProdSafe`, `Capabilities` (IDs it composes), `ExecuteAsync(ScenarioRun run, JsonElement payload)`.
  Registered by DI discovery, so a new ticket adds a class and nothing else.
- **Catalog-driven Admin panel.** A "Ticket Tests" section on `{INAPP_PAGE}` reads `GET {INAPP_API}`
  and renders one **Run** button per registered scenario. Buttons are never hand-added to markup —
  parallel PRs would conflict on every merge.
- **Parameter form.** Generated from the schema: typed inputs, required markers, defaults, sensitive
  fields masked. Empty schema → confirm and run.
- **Run endpoint** `POST {INAPP_API}/{ticketId}/runs`: admin role enforced server-side, deny by
  default; payload validated against the schema before anything runs; production refused unless
  `INAPP_PROD` allows and the scenario is `ProdSafe`; one active run per ticket; `InitiatedBy` from
  the authenticated principal; `OperationContext.IsTestRun = true`; returns `runId`, runs in the
  background.
- **Status endpoint** `GET {INAPP_API}/{ticketId}/runs/{runId}` — polled by the panel for live steps.
- **Run log.** Each step writes a row to Azure Table `{INAPP_TABLE}` and an App Insights trace:
  - `PartitionKey = {INAPP_PK}` (slug lower-kebab, stripped of `/ \ # ?` and control characters)
  - `RowKey = {invertedTicks}-{runId}-{seq:D3}`
  - `RunId, Step, CapabilityId, Requirement (AC-n / DOD-n), Outcome (Pass|Fail|Skipped|Simulated),
    DurationMs, Environment, BuildNumber, Commit, InitiatedBy, Message, ParametersRedacted`
  - sensitive parameter values are **never** written — names and `***` only.
- **Results publisher** (`post-deploy.md` §4) — called when a run completes; posts the evidence
  comment to the work item and sets the promotion status.

**Admin role.** If the project has no admin role or `[Authorize]` convention yet, do not invent one
and do not repurpose an approval list. Raise an RFI for the definition (precedent: RFI-381871-1) and
implement authenticated-only with the gap recorded as an OWASP risk.

**No harness yet** → do not build it inside a story's branch. Record the scenario requirement
`NEEDS-INFO` with an RFI for a harness work item, and continue the rest of the ticket. When the
harness work item is in the queue it goes first, with this file as its specification.

## 2. Test-aware plumbing (every function on a scenario's path)

- **Inputs arrive as a payload.** Each new or changed entry point takes a request DTO — the same DTO
  the scenario builds from the form. Controller and scenario call the same service method. No
  test-only overload.
- **Context is ambient.** `OperationContext` (`CorrelationId`, `TicketCid`, `TestRunId`, `IsTestRun`,
  `InitiatedBy`, `Environment`, `ForcedFlags`) is resolved from DI — never an extra `bool isTest`.
- **One code path.** Read `OperationContext` only in logging and in **side-effect gates**: an
  injected `ISideEffectGate` around each irreversible external action. With `effects: simulate` it
  records what it would have done and returns the same result shape. An ungated irreversible action
  on a scenario path is an OWASP risk.
- **Flags.** The scenario forces its ticket's flag on through `ForcedFlags`, so a dark feature can be
  proven without being enabled for users.
- **Test data is identifiable.** Anything a scenario writes is tagged `TestRunId`; the final step
  removes it unless the scenario is read-only.

## 3. Capability steps (shared across tickets)

Live under `{INAPP_STEPS}`, one file per capability, path derived from the ID:
`reports.load-source` → `{INAPP_STEPS}/Reports/LoadSourceStep.cs`.

- **`ICapabilityStep`** — `CapabilityId`, `Parameters` (namespaced: `reports.load-source.sourceName`),
  `ProdSafe`, `RunAsync(StepContext ctx)` → Pass / Fail / Simulated with a message. Calls real service
  methods with the payload DTO.
- **Deterministic path is the enforcement.** Two unmerged PRs creating the same capability collide
  as an add/add conflict on the second merge, so overlap cannot land silently. Never rename a step
  file to dodge it.
- **Assertions are the union** of the requirements the step proves, each tagged with the owning
  ticket and requirement (`360836/AC-2`), so a failure in a shared step names whose requirement broke.

Per the Phase 0.25 finding for each capability:

- `reuse` — compose it; add this ticket's assertion if its requirement adds a check.
- `modify` — change it in this PR; re-run every consuming scenario's unit tests. **A change that
  contradicts another ticket's assertion is never this ticket's call alone.** Stop that requirement
  and raise a decision on **both** tickets, addressed to both story owners: keep the old assertion,
  supersede it (citing this ticket's requirement), or split the behavior. Only after both owners
  answer does the step record `supersedes: {otherId}/AC-n — {reason, decision D-…}` and the REVIEW
  list it. An assertion is never loosened to make another ticket pass.
- `depends-on {id}` — stack on the other branch and note it in both PRs; or write at the same
  deterministic path and let the conflict force reconciliation. Never a parallel copy.
- `new` — create it.

## 4. The ticket's scenario

1. `Ticket{id}Scenario` beside the feature code, implementing `ITicketScenario` with the Phase 0
   parameter schema.
2. Compose it from capability steps in process order, each mapped to the `AC-*` / `DOD-*` it proves.
   Steps run the **real** services end to end — no mocks, no in-memory substitutes.
3. A step that cannot reach a dependency **fails**, naming it. The only `Skipped` is a step the
   payload explicitly opted out of.
4. Irreversible actions go through `ISideEffectGate`; `effects: simulate | execute`, default
   `simulate`; `execute` offered only outside Prod unless `INAPP_PROD = all`.
5. Last step cleans up everything tagged with `TestRunId`.
6. `ProdSafe = true` only when every step is read-only or simulated.
7. **Unit-test** the scenario and each new or modified step (Phase 3 style): schema validation rejects
   bad payloads; a failing dependency yields a Fail row; sensitive values never reach the log writer;
   the gate is honored; the partition key is `{INAPP_PK}`.

## 5. Blast-radius suite (new in v2)

The regression set from Phase 0.25 becomes runnable from the same panel:

- The scenario declares `RegressionCapabilities` — every capability whose code this ticket changed
  or whose callers it touched (from `design-gate.md` §2).
- The panel shows, beside the ticket's **Run** button, **Run with blast radius**: the ticket's
  scenario, then every scenario that consumes any of those capabilities, each step executed once per
  distinct parameter set and fanned out to its consuming scenarios.
- Rows carry `SuiteRunId` and `CapabilityId`; one summary row per capability under `{INAPP_SUITE_PK}`.
  So "did 361204 pass?" and "is `reports.*` healthy after this merge?" are each one query.
- A failure in another ticket's assertion is reported against **this** ticket, naming both.

Parameters for other tickets' scenarios come from their read-only fixtures (`effects: simulate`);
a consuming scenario with no fixture is listed as **not run** — never as passed.

## 6. Record

Ledger: scenario class, parameters, gated actions, `ProdSafe`, regression capabilities, and
`INAPP: built (not yet run)`. Building it is not the same as it passing. It is reported as passing
only when evidence rows from a deployed run show it (`post-deploy.md`).

REVIEW and PR description: the button label, parameter schema (sensitive fields flagged),
requirement → step map, every side-effect gate, `ProdSafe`, the regression capabilities and the
scenarios they pull in, and the queries:

```kusto
traces | where customDimensions.PartitionKey == "{INAPP_PK}"
       | project timestamp, RunId = tostring(customDimensions.TestRunId), message, customDimensions
       | order by timestamp desc
```
```
Azure Table {INAPP_TABLE}:  PartitionKey eq '{INAPP_PK}'
```
