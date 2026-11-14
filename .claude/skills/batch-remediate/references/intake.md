# Intake: finding the specification and building the AC checklist

Read this at Phase 0. Phase 0 starts only when the story has a **current certification**
(`enrichment.md` §7–§8): the story review has already made the story complete, so intake reads the
story — it does not go looking for a specification elsewhere.

## 1. Locating the specification

**The certified story is the specification of record, for every work item type.**

| Type | Description | Acceptance criteria |
|------|-------------|---------------------|
| User Story | `System.Description` | `Microsoft.VSTS.Common.AcceptanceCriteria` |
| Bug | `System.Description` (`ReproSteps` holds the symptom — context only) | the `Acceptance Criteria` section of `System.Description` |
| Task | `System.Description` | the `Acceptance Criteria` section of `System.Description` |

- Read the fields with `ado-commands.md` §1. Ignore the skill's branch-link line.
- **Wiki pages and other links are not a specification.** Anything they contributed was folded
  into the story in step C. A wiki page that now says something the story does not is a new
  source: it is an RFI to the story owner, never an extra requirement.
- **Field facts.** Bug and Task work items in this project have **no Acceptance Criteria field**.
  Never "populate the AC field" on them, and never create a work item just to get one. Task 380749
  is the precedent.
- A Bug's repro steps describe what breaks, not what "correct" is. If the story's criteria
  contradict the repro steps, that is an R10 conflict for the story review, not a judgment call
  here.

## 2. Building the AC checklist

The checklist is the definition of done and lives in the ledger. Number items `AC-1`, `AC-2`, …

- **Every type** — take the certified criteria as numbered in the story review. The numbers are
  already fixed (`enrichment.md` §4.4); never renumber. A compound criterion was split there.
- **Bug** — then always add these two whether or not the story states them:
  - **`AC-repro`** — the documented reproduction no longer reproduces, proven by a test that fails
    against pre-change code.
  - **`AC-regress`** — surrounding behavior is unchanged; existing tests stay green.

Each item carries its own status and, once implemented, the name of the test that proves it.

### Classification (recorded per item, used by every later phase)

```
AC-2  code-provable | external     UI-observable | internal     in-app: yes | no     slice: PR-n
```

- **external** — infrastructure, identity, RBAC, credential, or deployed-environment work no repo
  change performs. Record owner, the artifact that would evidence it, and an RFI.
- **UI-observable** — a user or public API can see it → a browser spec, written in Phase 3 with the
  PR and run after deploy (`e2e.md`).
- **in-app: yes** — the Admin-page scenario can prove it in a live environment → list its runtime
  parameters (name, type, required, sensitive, safe default) and any irreversible side effect.
- **slice** — filled in at Phase 0.25 when the PR series is set. Every item lands in some slice.

## 3. The information gate

Evaluate **each AC item independently**:

- **Actionable** — unambiguous, solution known or derivable from the story, comments and code, and
  edits pinned to `file:line`. Proceeds to implementation.
- **Not actionable** — mark `NEEDS-INFO` and raise an RFI. Do not guess.

A ticket proceeds to Phase 0.5 if **at least one** AC item is actionable; partially-blocked
tickets still make progress. If no item is actionable, record the RFIs and move to the next ticket.

## 3.5 Decision or RFI?

If you can write two defensible options, the unknown is a **decision** — lettered options, a
recommended default, pinned evidence (`assets/DECISION.template.md`). Ask it at the next gate, or
post it when unattended. If you cannot — the value is a fact only a person or system holds (a
client ID, an endpoint, a business rule nobody has stated) — it is an **RFI**. Never write an
open-ended question where options can be enumerated.

## 4. Information request (RFI) format

Specific enough to be answered without a meeting:

```
ID:        RFI-{ticket}-{n}
Blocks:    AC-{n} (and any others)
Question:  {one answerable question}
Unblocked by: {the artifact or decision needed}
Owner:     {name | TBD}
```

- **Question** — the precise unknown, as a single answerable question.
  - Weak: "requirements unclear."
  - Strong: "For AC-2, when the group lookup returns zero rows, should the endpoint return 404 or
    200 with an empty array? The story doesn't specify and the two call sites differ."
- **Unblocked by** — a story edit (which then needs re-certification), a comment answer, a
  published view, a product decision.
- **Owner** — derive from the ticket or comments (assignee, area path, whoever raised the point).
  If genuinely unknown, `TBD` — never invent a name.

Record every RFI in the ledger and the batch report. RFIs reach the ticket inside the
**Outstanding** section of the pass's status comment, and again as questions at gate M6 — so the
developer running the skill gets the first chance to answer before it is routed to the owner. Never repeat a question
already asked in an earlier comment; reference it (`RFI-{id}-1 — still open`).
