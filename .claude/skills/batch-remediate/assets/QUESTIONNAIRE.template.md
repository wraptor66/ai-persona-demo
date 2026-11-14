# Questionnaire template — every gate

One shape for every gate, so a developer learns it once. Rendered through the host adapter
(see `references/host-claude.md` or `references/host-copilot.md`). Letters are used in chat and
on the ticket alike, so `Q1: B` in chat and `D-{id}-{n}: B` on the ticket mean the same thing.

```
{Mn} · {Gate name} — {ticket id}: {title}
{One or two lines of context: what was found, what this answer decides.}

Q1. {One decision, one sentence.}
  A. (Recommended — {grounds, pinned: file:line | wiki § | comment date})
     {Ready-to-use answer: the criterion, value, or decision text exactly as it will be recorded.}
  B. {Ready-to-use answer}
  C. {Ready-to-use answer}            (only if genuinely viable)
  D. Other — type it.
  Affects: {AC-n, the design, the regression set, …}

Q2. …

{Hard gate:}    Reply per question, e.g. "Q1: A, Q2: C". Nothing continues until you answer.
{Summary gate:} Shown while work continues. Reply any time to change something before it is pushed.
```

Rules (from `references/milestones.md` §2):

- ≤ 3 questions per round; highest downstream cost first.
- One decision per question; options mutually exclusive; no straw options.
- Recommended first, with honest grounds — weak evidence is labelled weak.
- Never recommend added scope.
- Never ask what the repository can answer.
- M6 secret questions ask **where** a value is stored, never the value.

## Recording the answers

Ledger (`GATES` line) always. Ticket: M0 / M1 / MC story-review loops → the `analysis` comment on
certification (`references/ado-comments.md` §3.1); M2 and later hard gates → `decision` comment
(`assets/DECISION.template.md`); M6 → `answer` comment; summary gates → a line in the pass's status
comment.

The story-review loops use this same shape with their own questions and headings
(`references/enrichment.md` §3–§7): the step question is always `Q1`, its done option is always `A`.
