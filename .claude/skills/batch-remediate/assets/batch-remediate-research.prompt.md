---
name: batch-remediate-research
description: Review and certify a story with me and agree the design - story review, certification, design options, PR plan. No code. Ends with a research comment on the work item.
argument-hint: "id=work-item-id"
agent: agent
---

Use the **batch-remediate** skill, version **3.2.0** (`.claude/skills/batch-remediate/SKILL.md`).

Run **research** mode for work item ${input:id:work item ID, e.g. 361204}.

Do Phase -1 (story review and certification), Phase 0 and Phase 0.25 only. Do not create a branch
or change any code. Finish by posting the research comment (goal, numbered requirements, chosen
design, the story's feature flag, PR series, smoke-test plan, open items), and tell me the next step:
`/batch-remediate-execute id=<id>`.

Before anything else, read these files from `.claude/skills/batch-remediate/`, in this order:
`SKILL.md`, `references/host-copilot.md`, `references/config.symphony.md`, `references/modes.md`.
Confirm `SKILL.md` says version 3.2.0; if it does not, stop and tell me to pull `develop`.

Rules that always apply:

1. Ask every gate question in chat as numbered questions with lettered options.
2. At every hard gate (M0, M1, MC, M2, M6) and at **every** step of the story review (description review, AC
   review, supporting sources, certification): ask, then **end your turn**. Do not proceed until I
   reply.
3. For every Azure DevOps operation listed in `references/ado-commands.md`, run that block exactly
   as written in the terminal. Do not use an MCP tool for those operations.
4. I may paste long updates as a plain chat reply. Treat the pasted text as my update.
5. Never write anything to a work item until I approve the final text you showed me.
