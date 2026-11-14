---
name: batch-remediate-results
description: Post the story's test results to the work item: unit and jest results now; after the Dev deploy, run what has not run, read the gate, and confirm the logs.
argument-hint: "id=work-item-id"
agent: agent
---

Use the **batch-remediate** skill, version **3.2.0** (`.claude/skills/batch-remediate/SKILL.md`).

Run **results** mode for work item ${input:id:work item ID, e.g. 361204}.

Run the story's run script (`-Layer unit,js`) or read its latest `summary.json`, and report only
measured counts. If the PR is merged, first check the story's flag is on in Dev
(`assets/Set-FeatureFlag.ps1 -Environment dev -List`; a run with it off proves nothing), then do Phase 7: merge and deploy gates, evidence, run what has
not run (show me the plan and wait), the gate, the e2e-results comment, and the logging comment's
queries. Post one results comment (skip it if nothing changed since the last one) and tell me
exactly what is still missing. Never set Done.

Before anything else, read these files from `.claude/skills/batch-remediate/`, in this order:
`SKILL.md`, `references/host-copilot.md`, `references/config.symphony.md`, `references/modes.md`.
Confirm `SKILL.md` says version 3.2.0; if it does not, stop and tell me to pull `develop`.

Rules that always apply:

1. Ask every gate question in chat as numbered questions with lettered options.
2. At every hard gate (M6, and the run-pending plan) and at **every** step of the story review (description review, AC
   review, supporting sources, certification): ask, then **end your turn**. Do not proceed until I
   reply.
3. For every Azure DevOps operation listed in `references/ado-commands.md`, run that block exactly
   as written in the terminal. Do not use an MCP tool for those operations.
4. I may paste long updates as a plain chat reply. Treat the pasted text as my update.
5. Never write anything to a work item until I approve the final text you showed me.
