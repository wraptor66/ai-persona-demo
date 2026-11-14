# Story review — Phase −1 (gates M0, M1, MC)

Read this at Phase −1, and the staleness rule (§8) at the start of every later pass. **The story is
the specification of record for every work item type.** Phase −1 makes the story itself complete
and correct, with the developer, in four steps, then has the developer certify it. Nothing is
built from a story that is not certified.

The goal is **the fewest implementation iterations**, not better prose. A story that reads well
but leaves the empty-result behavior undefined is not ready; a terse story whose every criterion
is testable is.

## 0. The four steps

| Step | Asks the developer | Gate | Status while open |
|------|--------------------|------|-------------------|
| **A. Description review** (§3) | "Is this description correct and complete?" | M0 (hard) | `ENRICHING` |
| **B. AC review** (§4) | "Are any modifications required?" | M1 (hard) | `ENRICHING` |
| **C. Supplemental sources** (§5) | "Is there a wiki page or other link with supporting information?" | M1 (hard) | `ENRICHING` |
| **D. Certification** (§7) | "Have you reviewed and certified the story for completeness and accuracy?" | MC (hard) | `AWAITING-CERTIFICATION` |

Rules for every step:

1. **One step at a time.** Finish A before B, B before C, C before D. Never put two steps in one
   message.
2. **Every step is a loop**: show → ask → (the developer gives updates) → rewrite → show the final
   text → ask to write → write → re-read → ask the step's question again. The step ends only when
   the developer answers the step's question with its "done" option.
3. **Ask, then stop.** After asking, wait for the developer's reply. Do not continue to the next
   step, read more, or write anything until the reply arrives. The host adapter says how
   (`host-claude.md` / `host-copilot.md`, *Story review*).
4. **Explicit approval** to write means the developer chose the *write* option, or replied "write",
   "approve" or "yes, write it", to the **final text shown in this session**. Anything else —
   silence, "looks fine" to a draft, approval of an earlier draft — is not approval.
5. **Enrichment clarifies; it never adds scope.** Write only what the developer stated or
   explicitly approved. A suggestion the developer did not accept is not written.

The ADO calls for every step are in `ado-commands.md` — read fields §1, field history §2, write §3,
comments §4 and §5. Run them exactly as written.

## 1. Inputs

Read, in one command (`ado-commands.md` §1, §2, §4):

- **The story fields** — description, AC (User Story), repro steps (Bug), the Definition of Done
  (field or team template), the revision number.
- **Every comment.** Later wins; a claim in a comment is not evidence.
- **Everything attached or linked** — attachments (open them: mockups, spreadsheets, sample
  files), linked work items (parent feature, related stories, predecessors), linked PRs and
  branches, a Test Plan suite, and every wiki URL in the Links tab or in a comment
  (`…/_wiki/wikis/…`). These are offered as sources in step C.

**Show text, not HTML.** Convert field HTML to plain lines for the developer: one line per
paragraph or list item, numbered lists kept as `1.` `2.`, headings on their own line. Drop the
skill's own branch-link line (any line containing `?version=GB`) and any earlier
`[batch-remediate | enrichment | …]` block marker — show what a person wrote.

**Pre-resolve from the repository before asking anything.** If the story names "the mailbox
table", find it and pin `file:line`. The developer is asked about intent and decisions only —
never a fact the code already answers.

## 2. Readiness rubric — behind the scenes

The rubric does not run its own question rounds. It runs **silently** on every draft, and each
gap it finds becomes a **prompt** inside the step that owns it, so the developer closes it while
reviewing that part of the story.

Score each dimension **0** (missing), **1** (present but ambiguous), **2** (sufficient). The total
(/20) is reported for trend; it never gates. Only §6 gates.

| # | Dimension | Scores 2 when | Prompted in |
|---|---|---|---|
| R1 | Outcome | One sentence states who, what changes, and where it is seen | A |
| R2 | Verifiable AC | Every criterion has an observable pass/fail; no "properly", "fast", "user-friendly", "as expected" | B |
| R3 | Named data / entities | Tables, views, endpoints, screens, fields are named **and resolved in the repo** with `file:line` | A |
| R4 | Observability | Each criterion is marked **UI-observable** (a user or public API can see it) or **internal** | B |
| R5 | Side effects | Every irreversible external action (sends mail, writes to AD / a warehouse / another system) is named with its target | A |
| R6 | Out of scope | Near-neighbor behavior that is **not** changing is stated | A |
| R7 | Error and edge behavior | Empty / zero-row, unauthorized, and dependency-unavailable behavior is defined | B |
| R8 | External dependencies | Infrastructure, identity, RBAC, or data a repo change cannot create is named, each with an owner | A |
| R9 | Worked example | At least one concrete case: given this input / state, expect this output | B |
| R10 | Consistency | Description, AC, DoD, sources, and comments agree; later comments reconciled | A (description ↔ AC), C (sources) |

Scoring notes:

- **R2** is scored per criterion, then the dimension takes the *lowest* criterion score. A compound
  criterion ("X and Y") is split first — each half scored on its own.
- **R3** scores 1 when names are given but do not resolve in the repo. That is a prompt, not a
  guess: the story may mean a new object.
- **R5 / R8** score 2 when the story explicitly says there are none and nothing in the repo
  contradicts it. Absence of mention is 1, not 2.
- **R8** answers are **claims**. Phase 0's claims-are-not-evidence rule still requires the
  artifact. The review never converts a claim into evidence.
- **R10** scores 0 only when two sources give **incompatible behavior** for the same case. Wording
  differences are not conflicts. Check it line by line: for each AC, find the description sentence
  it implements; for each description sentence that promises behavior, find the AC that proves it.

### Writing a prompt

At most `{ENRICH_MAX_QUESTIONS}` prompts per message, in this order: R1, R10, R2 (the ones that can
HALT), then R8 → R5 → R7 → R9 → R3 → R4 → R6. A gap the developer has skipped
`{ENRICH_MAX_ROUNDS}` times is not prompted again; it becomes a warning.

- **Name the gap in one line**, then offer **ready-to-paste text**: "The AC has no error behavior.
  Add one?" followed by options that *are* the criterion text.
- **Recommended option first**, with one line of grounds citing the source (`file:line`, a comment,
  a source section). Thin grounds → say so: `(Recommended — weak evidence: no existing convention
  found)`.
- **Never recommend scope.** An option that adds behavior the story does not already imply may be
  offered, never recommended.
- **Always offer "skip"** — the developer may leave the gap open. A skipped gap is a warning in the
  ledger and REVIEW §0, never an assumption.
- An accepted prompt goes into the **next draft** of the step, which the developer still approves
  as a whole before anything is written.

Example, inside step B:

```
Gap (R7): the AC does not say what a row shows when a mailbox has no statistics yet.
  A. (Recommended — existing report grids render nulls as "—", ReportGrid.js:212)
     AC-5 Each stats column shows "—" until the first collection completes.
  B. AC-5 Each stats column shows 0.
  C. Skip — leave it undefined.
```

## 3. Step A — description review (gate M0)

1. **Show** the current description as plain text, under the heading `CURRENT DESCRIPTION — {id}
   (rev {n})`. Above it, one line: `My reading of the goal: {who, what changes, where it is seen}`.
   - Bug: also show the repro steps under `REPRO STEPS (shown for context — not changed by this
     review)`.
   - Bug or Task: the description's `Acceptance Criteria` section is reviewed in step B, not here.
     Show the description text above that heading only.
   - Description empty: say so — `This {type} has no description yet.`
2. **Show every R10 conflict** between the description and the AC, one line each, with the two
   sources quoted, and ask which one is right. Then any other step-A prompts (§2).
3. **Ask**: `Is this description correct and complete?`
   - `A. Yes — it is correct and complete.`
   - `B. No — I will paste updates.`
   Then stop and wait.
4. **On B**, the developer pastes updates as a plain chat reply — any length, any format. Rewrite
   the description:
   - integrate the updates; keep the developer's own wording;
   - keep every existing sentence that is still right, word for word;
   - include accepted prompts; include nothing the developer did not state or approve;
   - plain text lines only — the house HTML rules are applied when writing (`ado-commands.md` §3).
5. **Show the final text** exactly as it will be written, under `PROPOSED DESCRIPTION — {id}`, and
   ask: `Write this to the story?`
   - `A. Write it.`
   - `B. Change something — I will say what.`
   Then stop and wait. On B, revise and show the final text again.
6. **On A**, write the description (`ado-commands.md` §3, description only, pinned to the rev read
   in §1). Re-read §1. Say `Written — {id} is now rev {n}.` Then go back to item 3 with the new
   text. Step A ends when the developer answers item 3 with **A**.

## 4. Step B — acceptance criteria review (gate M1)

1. **Show** the current criteria as a numbered list under `CURRENT ACCEPTANCE CRITERIA — {id}`:
   - User Story: the AC field. Bug or Task: the `Acceptance Criteria` section of the description.
   - Number them `AC-1`, `AC-2`, … exactly as Phase 0 will. If the field already numbers them, keep
     those numbers. Split a compound criterion into separate numbered items and say which.
   - None yet: say `This {type} has no acceptance criteria yet.` For a Bug, offer criteria drawn
     from the description and repro steps as a **proposal** — the developer still approves them.
   - Below, the Definition of Done as `DOD-1 …`, marked **read-only — the DoD is never rewritten**.
2. **Show step-B prompts** (§2): untestable wording (R2), missing error and edge behavior (R7), no
   worked example (R9), observability not stated (R4).
3. **Ask**: `Are any modifications required?`
   - `A. No — the acceptance criteria are complete.`
   - `B. Yes — I will give additional or changed criteria.`
   Then stop and wait.
4. **On B**, rewrite the criteria:
   - **numbered, each one testable** — an observable pass/fail;
   - **numbering is preserved**: a surviving criterion keeps its number; a reworded one keeps its
     number; a new one takes the next free number; a removed one leaves its number unused and the
     list says `AC-4 removed`. Never renumber — `AC-3` on the ticket is `AC-3` in the ledger, the
     REVIEW, the tests and the Test Cases for the life of the story;
   - include accepted prompts; include nothing the developer did not state or approve.
5. **Show the final list** under `PROPOSED ACCEPTANCE CRITERIA — {id}` and ask `Write this to the
   story?` (`A. Write it.` / `B. Change something`). Stop and wait.
6. **On A**, write (`ado-commands.md` §3, pinned to the current rev):
   - User Story → the AC field.
   - Bug or Task → the description: the description text approved in step A, then a line reading
     exactly `Acceptance Criteria`, then the numbered criteria. Bug and Task have no AC field —
     never create one, never create a work item to get one.
   Re-read, say the new rev, go back to item 3. Step B ends on **A**.

## 5. Step C — supplemental sources (gate M1)

1. **List the sources already found** (wiki links, attachments, linked items — §1), and **ask**:
   `Is there a wiki page or other link with supporting information for this story?`
   - `A. No — nothing else.`
   - `B. Yes — I will paste the link(s).`
   - `C. Use the ones you found: {list}.`
   Then stop and wait.
2. **Read each source end to end.** For each piece that would enrich the description or the AC,
   propose **ready-to-paste text** that cites its origin — `(from wiki "Mailbox stats" §Agreed
   changes)`. Propose only what the source says. A source that **contradicts** the story is shown as
   an R10 conflict, with both texts quoted, and the developer decides.
3. **Ask** which proposals to fold in, one letter each, plus `none`. Stop and wait.
4. **Fold the accepted ones into the story**: build the new description and / or AC text (same rules
   as §3.4 and §4.4, numbering preserved), show the final text, ask to write, write on **A**.
   **The source is input to the story, never a parallel specification.** The story must read
   completely without it: never write "see the wiki" in place of the content.
5. Go back to item 1. Step C ends when the developer answers **A**.

## 6. HALT conditions — after step C, before step D

Evaluated on the story **as written now**, never before the steps — a gap the developer can close in
one answer is not a reason to stop.

1. **H1 — no outcome.** R1 is still 0. Nobody can state what done looks like.
2. **H2 — nothing testable.** No criterion scores 2 on R2.
3. **H3 — unresolved conflict.** R10 is still 0 on behavior a criterion depends on, and the
   developer did not choose between the sources.

On HALT: do **not** ask for certification. The ticket goes `NEEDS-INFO` with reason
`enrich-halt H{n}`. Each open gap becomes an RFI (`intake.md` §4) with

```
Blocks:       whole ticket (Phase -1 H{n})
Unblocked by: {the text the story needs}
Owner:        {story author / product owner from the ticket; TBD if unknown}
Proposed answer: {the recommended prompt option, as criterion text}
```

No code, no branch. Move to the next ticket. **Everything that is not H1–H3 warns and continues.**

## 7. Step D — certification (gate MC)

Status `AWAITING-CERTIFICATION`.

1. **Tell the developer**, in chat, in these words:

   > Please open {id} in Azure DevOps and add a comment that contains exactly this sentence:
   > **I have verified this story is complete and accurate**
   > Add it yourself, after your last change to the description or acceptance criteria.

   This item is the only place the skill states the sentence, and chat is the only place it is
   shown. Everywhere else it is called **the certification sentence (§7.1)**.
2. **Ask**: `Have you reviewed and certified the story for completeness and accuracy?`
   - `A. Yes — I have posted the comment.`
   - `B. Not yet.`
   Then stop and wait. On B, stay at this step; nothing else on this ticket proceeds.
3. **On A, check** — run `ado-commands.md` §2 and §4 in one command. Accept the certification only
   if **one comment meets every rule**:
   1. its text, with HTML removed and whitespace collapsed, contains the certification sentence
      (§7.1), compared case-insensitively;
   2. its `createdDate` is **later** than `LAST CONTENT CHANGE` from §2 (the latest change to the
      description or AC text, branch-link edits excluded);
   3. its `id` is not in the ledger's `POSTED` list, and it contains no `[batch-remediate |` marker
      — a comment the skill posted can never certify.

   Say which comment matched: `Certified by {createdBy} in comment {id} at {createdDate}.` If none
   matched, say exactly which rule failed — for example `The comment at 10:02 is older than the AC
   change at 10:15 (rev 31). Please add a new one.` — and go back to item 1.
4. **On acceptance**, post the `analysis` comment (`ado-comments.md` §3.1). It says the
   certification was accepted, names the certifying comment and its date, and says solution
   building is starting. Record the CERTIFICATION block in the ledger. Status → `INVESTIGATING`.
5. **The skill never writes the certification sentence** — not in a comment, a field, the ledger,
   a report, the REVIEW, a commit, a PR, a log, or a template; not quoted, not in part. Comments the
   skill posts carry the developer's identity, so writing it anywhere on the ticket would certify
   the story with nobody having checked it. Before every write, check the text: if it contains the
   sentence, remove it and do not post until it is gone.

## 8. Staleness — every pass, before any later phase

At the start of every pass, for every ticket past step D, before Phase 0 or any later phase runs:

1. Run `ado-commands.md` §2.
2. If `LAST CONTENT CHANGE` is later than the certifying comment's `createdDate`, **the
   certification is stale**. Edits to the skill's own branch-link line do not count.
3. A stale ticket goes back to `AWAITING-CERTIFICATION`. **No solutioning starts or resumes** — no
   build, no commit, no push, no PR, no comment except the pass's status comment — until the
   developer re-certifies.
4. Show the developer what changed — field, rev, who, when, and the old and new text as plain
   lines — and offer steps A–C again for anything they want to adjust. Then run step D.
5. On re-certification: rebuild the checklist with the §4.4 numbering rules. A requirement whose
   text changed loses its proven tick until it is proven again. Note the change in the ledger and
   the status comment.

## 9. Unattended — `ENRICH_MODE = rfi`

When nobody can be asked (`INTERACTIVE = off`, or no way to ask in this host), the skill **writes
nothing to the story fields**. Instead:

1. Run the rubric and form the step A–C prompts as usual.
2. Post one `review-request` comment (`ado-comments.md` §3.2): the description and numbered AC as
   read, every prompt with its ready-to-paste options, the sources found, and this request: *edit
   the Description and Acceptance Criteria in Azure DevOps as needed, then add the certification
   comment described in the batch-remediate README, section "Certifying a story".* The comment
   never contains the certification sentence itself.
3. Status `AWAITING-CERTIFICATION`. The ticket waits.
4. On a later pass, run §7.3. Accepted (and H1–H3 clear on the text as it is now) → §7.4 and
   continue. Not accepted → the ticket keeps waiting; do not post the request again.

## 10. Records and resume

Ledger (`assets/BATCH-STATE.template.md`): the STORY REVIEW block (readiness before/after, the rev
written by each step, prompts offered / accepted / skipped, sources used, open warnings), the
CERTIFICATION block, and `POSTED` (every comment id the skill posted on this ticket).

The prompts and their answers are recorded in the `analysis` comment (§7.4), not as separate
`decision` comments: the story's own field history holds every version, and the analysis comment
lists each prompt, the option chosen, and the criterion it created or changed.

Resume:

- `ENRICHING` → re-read the fields (they may have changed outside the skill) and restart the open
  step (A, B or C, recorded in the ledger).
- `AWAITING-CERTIFICATION` → run §7.3 first; ask again only if no comment matches.
- `INVESTIGATING` or later → §8.
