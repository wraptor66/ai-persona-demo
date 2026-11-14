# Decision record template

Posted to the work item **once per question asked**, in every gate including the Phase −1 gap
questions, as soon as the question is answered. Also posted for any decision raised when
unattended, with the answer pending. Plain text with `<br>` line breaks (`references/ado-comments.md` §1). Append-only: a
changed decision is a new record that names the one it supersedes.

```
DECISION D-{id}-{n} - {Mn} {gate name} - {RUN_TAG}
CONTEXT: {the one or two lines shown with the question: what was found, file:line / wiki / comment}
QUESTION: {exactly as asked}

OPTIONS (as presented)
A) {option text, word for word}
   Would mean: {scope / design / where it lives / blast radius / flag / effort / risk}
B) {option text, word for word}
   Would mean: {...}
C) {option text, word for word}
   Would mean: {...}

RECOMMENDED: {letter} - {grounds, pinned to evidence}
ANSWER: {letter} by {developer} - {in this run's chat | reply comment {id} on {date} | DEFAULTS}
ANSWER AS GIVEN: "{the developer's exact words, if they typed more than the letter}"
WHY: {one line, in the developer's words where they gave them}
EFFECT: {what changed because of this answer - AC-n reworded / added / removed, DOD-n, design
        choice, regression set, flag; or "confirms existing AC-n, no change"}

IF WE HAD CHOSEN OTHERWISE
- {letter}: {how the solution, tests, and regression set would have differed}
- {letter}: {…}

CONSEQUENCES OF {chosen}
- Regression set: {capabilities} -> {n} scenarios, {n} Test Plan cases
- Flag: {Ticket{id}.{Slug} default off in Prod/UAT | none}
- Follow-ups: {convergence item | flag-removal item | none}

REVISIT IF: {the condition that would reopen this}
SUPERSEDES: {D-{id}-{n} | -}

[batch-remediate | decision | {batch} | {RUN_TAG} | id=D-{id}-{n} | answer={letter}]
```

## Unattended variant (awaiting an answer)

Same body with `ANSWER: AWAITING` and these lines before the marker:

```
TO ANSWER: reply on this ticket with
D-{id}-{n}: B
(or D-{id}: DEFAULTS to accept every recommendation on this ticket)
Text after "--" adds a constraint to your choice, e.g. D-{id}-{n}: B -- 30 days not 60
```
