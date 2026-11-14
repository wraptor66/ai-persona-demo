# Milestone gates

Read before the first gate of a run. The gates exist so the developer **owns the decisions** and
the skill does the legwork. Every gate is short: a developer should clear a hard gate in under two
minutes. A gate that takes longer is a sign the story needs a conversation, and the skill says so.

## 1. Gate kinds

| Kind | Gates | Behavior |
|------|-------|----------|
| **Hard** | M0, M1, MC, M2, M6 | Ask, then stop. Nothing after the gate starts until an answer is recorded. M0, M1 and MC are review loops: they repeat until the developer gives the step's "done" answer. |
| **Summary** | M3, M5 | Show the content and keep working in the same turn. A change the developer sends later is applied at the next stop, before anything it affects is pushed. |
| **Optional** | M4 | Off unless the developer asks for it at M3 or in the command. |

The developer may promote any summary gate to hard for the run ("stop at every gate").

## 2. Every gate, the same shape

Use `assets/QUESTIONNAIRE.template.md` and the host adapter's question mechanics.

- **≤ 3 questions per round.** Highest downstream cost first.
- **2–4 options per question, lettered A–D**, each a ready-to-use answer (a criterion, a decision, a
  value). The same letters are used in chat and on the ticket (`D-{id}-{n}: B`).
- **Recommended option first**, with one line of grounds pinned to `file:line`, a wiki section, or
  a comment. Thin grounds → say so: `(Recommended — weak evidence: no existing convention)`.
- **Never recommend new scope.** An option that adds behavior may be offered, never recommended.
- Free text is always allowed.
- **Pre-resolve from the repo first.** Never ask a question the code can answer.

## 3. The gates

### M0 — Description (hard, loop) · Phase −1 step A

`enrichment.md` §3. Show the current description cleanly, the skill's one-line reading of the
goal, every description ↔ AC conflict, and the step-A rubric prompts. Ask **"Is this description
correct and complete?"** — `A. Yes` / `B. No — I will paste updates`. On B: rewrite, show the final
text, write it only on explicit approval, re-read, ask again. Ends on A.

### M1 — Criteria and sources (hard, loop) · Phase −1 steps B and C

`enrichment.md` §4–§5. Step B: show the AC as a numbered list (the DoD read-only below it) with the
step-B prompts. Ask **"Are any modifications required?"** — `A. No` / `B. Yes`. On B: rewrite
(numbered, each testable, numbering preserved), show, write on explicit approval, ask again. Then
step C: ask for a wiki page or other link with supporting information; propose ready-to-paste
additions citing the source; fold the approved ones into the story the same way. Ends when the
developer has no more sources. There is no "use for this run only" or "reject": the story is the
specification, so what is built is what the story says.

### MC — Certify (hard) · Phase −1 step D

`enrichment.md` §7. Tell the developer to add the certification comment themselves (the sentence is
given in `enrichment.md` §7.1 only), then ask **"Have you reviewed and certified the story for
completeness and accuracy?"** — `A. Yes` / `B. Not yet`. On A, check the ticket's comments; accept
only a developer comment newer than the last description / AC change. Ends on acceptance. Not asked
while H1–H3 hold (`enrichment.md` §6).

### M2 — Approach (hard) · after Phase 0.25 option analysis

Show 2–3 options from `design-gate.md` §3 as a table: option, where it lives, blast radius
(capabilities and scenario count), flag, effort, risk. Ask:

1. Which approach?
2. Where should it live, if more than one place is viable?
3. The story's flag (shown, not asked): `FF_{id}_{Slug}`, its entry point(s) and App Services.
   Asked only when the change cannot be gated: "Exempt from the flag because {reason}? A. yes ·
   B. no, find a way to gate it".

Post the decision record (`ado-comments.md` §3.3) with every alternative and its consequence.

### M3 — Plan (summary) · after slicing

Show the PR series:
```
PR-1  {title}  — {requirements}  — {files}  — deploy-safe alone because {reason}
PR-2  …
```
Each PR must be deployable on its own with the flag off. Ask only if a slice is uncertain.
Name the browser spec each PR adds (`{E2E_DIR}/{id}-{slug}.spec.js`, its tests by requirement ID)
and the requirements the tester will check by hand after deploy.

### M3 note for modes
In `research`, M3 also shows the **smoke-test plan**: per requirement, where the developer goes
and what they should see once `execute` has built it. It goes into the `research` comment.

### M4 — Mid-build (optional) · between layers in Phase 1

"Service layer done: {n} files, {n} tests green. Continue to {next layer}?" Use it for large
stories or when the developer asked to watch.

### M5 — Pre-PR (summary) · after Phase 4, before push

Show: diff summary (files, +/−), requirement → unit test → browser test map, OWASP outcome with any risk raised,
consumer-impact table, residual risks, the in-app scenario's parameters. Becomes hard if any OWASP
item is **risk raised**, any loop-nested call site is unanswered, or Phase 1.5 superseded the M2
decision.

## 3.1 Mode gates

### X1 — Story changed (summary; hard when M2 reopens) · start of an `execute` re-run

Only when the story changed since the latest `research` or `execute` comment (staleness check,
`enrichment.md` §8, and `SRC-HASH`). **First**, if the change made the certification stale: show
the change and ask the developer to add a new certification comment themselves (hard; the skill
never writes it). Then show:
```
X1 · Story changed since iteration {n} - {id}
ADDED    AC-5 {text}                       (source: AC field, rev {n})
CHANGED  AC-2 {old} -> {new}
REMOVED  DOD-3 {text}                      (its code is removed in this iteration)
DESIGN   still fits D-{id}-{n} ({reason}) | does not fit: {why} -> M2 reopens
```
Ask only when something is ambiguous (a changed criterion that could mean two things), or when
the design no longer fits; then M2 is asked again as a hard gate and a superseding decision record
is posted. Each answer gets its decision comment.

### X2 — Pre-push (summary) · end of `execute`, before the push

Show: diff summary (files, +/−), build result, existing suite counts (and any existing test this
change had to update, with why), requirements this iteration implements, the **smoke-test
checklist**, and whether existing tests from an earlier `tests` run are now stale. Becomes hard if
an existing test had to change its expected behavior, or an OWASP item is risk raised.

### T0 — Ready for tests (hard) · start of `tests`

`tests-mode.md` §2. "Did your smoke test show every requirement working?" A = write the tests ·
B = not yet (back to `execute`) · C = the story needs a change first. Plus: which requirements
did you check by hand. Never skipped: Phase B starts only on the developer's word.

### M6 — Close-out (hard) · end of Phase 6

Turn **every** open item into a question the developer can answer now or route:

| Open item | Question shape |
|-----------|----------------|
| RFI with a known owner | "Do you know the answer? (type it) · or I'll ask {owner} on the ticket" |
| Missing documentation | "Paste the link to {doc}, or name who writes it" |
| Missing secret / key | "Where is {name} stored? (Key Vault name + secret name) — never paste the value" |
| Missing description / example | "Describe the expected {output} for {input}, or pick: …" |
| External dependency | "Who owns {artifact}, and is there a work item for it?" |

Post answers as an answer comment (`ado-comments.md` §3.5); post anything unanswered as an RFI. The
next pass reads both. A pasted secret is refused, not stored, and the developer is told where it
belongs.

## 4. When nobody answers

- **Interactive run, hard gate, no reply:** stay stopped. Do not time out into a default.
- **`INTERACTIVE = off`, M0 / M1 / MC:** post one `review-request` comment and set
  `AWAITING-CERTIFICATION` (`enrichment.md` §9). Nothing is written to the story; the developer
  edits it in ADO and certifies. `DEFAULTS` never answers these gates.
- **`INTERACTIVE = off`, other hard gates:** post the gate as a decision record with the
  recommended default and set `NEEDS-INFO (gate Mn)`. The developer can answer on the ticket with `D-{id}-{n}: B` or
  `D-{id}: DEFAULTS`; the next pass reads it.
- **Queue mode:** clear the hard gates for every ticket in the gate sweep before any build phase
  starts. A ticket whose gate is still open is skipped for this pass; the others proceed.

## 5. Decision answer syntax (ticket replies)

```
D-361204-2: B
D-361204-3: A -- use 30 days, not 60
D-361204: DEFAULTS        all open decisions on this ticket take their default
BATCH: DEFAULTS           all open decisions in the batch take their default
```

Scan comments newer than `LAST-PASS`. Text after `--` constrains the chosen option; it never adds
one. A letter that isn't offered, or two answers to one ID in a comment, is unanswered — re-ask and
quote the ambiguity. `DEFAULTS` on a hard gate is recorded separately in the report so the
acceptance is visible.

## 5.5 Every answer goes on the ticket

Each question answered at M2 and later is posted as its own `decision` comment right away, before
the next question round or phase (`ado-comments.md` §3.3). The Phase −1 review loops (M0, M1, MC)
are recorded in the `analysis` comment instead — every prompt, the option chosen, and the field
revision it produced (`enrichment.md` §10). Summary gates get a decision comment for each question the developer
actually answered or changed; content they only viewed goes in the status line.

## 6. Ledger

Record each gate per ticket:
```
GATES  M0 done (desc rev 14) · M1 done (AC rev 16, 1 source) · MC certified comment 5581234
       M2 B (D-361204-2) · M3 3 PRs · X1 iter 3: +AC-5 ~AC-2 · X2 shown · T0 A (smoke: "all")
       M5 shown · M6 answered 2 / routed 1
```
