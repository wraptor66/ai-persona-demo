# Refactoring Opportunities

Cumulative across passes and tickets. **Append only — never rewrite, never implement.**

Lives at `{BATCH_ROOT}/{batch-name}/REFACTOR-OPPORTUNITIES.md`, outside the repo, alongside that
batch's ledger.

Everything here is a candidate for its own Azure DevOps work item. Nothing here enters a ticket's
branch: that's the whole point of writing it down instead of doing it. Before adding an entry,
check whether it's already recorded.

---

### RF-{ticket-id}-{n}: {short title}

- **Found while:** {ticket id} — {what you were doing when you noticed}
- **Location:** `{file}:{line}` (list several if the pattern repeats)
- **Observation:** {what's wrong, risky, duplicated, or fragile today — be concrete}
- **Proposed refactor:** {the change, concretely enough that someone could scope it}
- **Benefit:** {what improves — testability, a class of defect removed, duplication collapsed}
- **Risk / blast radius:** {what it would touch; why it wasn't safe to fold into the ticket}
- **Suggested ADO ticket:** {a title that can be pasted straight into a new work item}

---

### RF-{ticket-id}-{n+1}: {short title}

- **Found while:**
- **Location:**
- **Observation:**
- **Proposed refactor:**
- **Benefit:**
- **Risk / blast radius:**
- **Suggested ADO ticket:**

---

## Entry types added in v2

Use the same fields, with one of these titles, whenever the design produces them:

- **Convergence** — `RF-{id}-{n}: Converge isolated {slice} back into {shared component}`. Raised
  by design option C. Observation names what the isolated slice duplicates in *behavior* (never
  in code — copying is not an allowed design) and why it was isolated.
- **Flag removal** — `RF-{id}-{n}: Remove flag {Ticket{id}.{Slug}}`. Raised by every flag.
  Proposed refactor: delete the flag check at the entry point, the off-path, and the flag
  definition, after the story is accepted in Prod.
