---
name: batch-remediate
description: Work Azure DevOps stories with the batch-remediate skill (3.2.0) in four modes - research, execute, tests, results. With no mode it starts at the mode the story needs, runs it, and asks before continuing to the next.
argument-hint: "[research|execute|tests|results] [batch-name] work-item-id ..."
agent: agent
---

Use the **batch-remediate** skill, version **3.2.0** (`.claude/skills/batch-remediate/SKILL.md`).

Mode, batch and work items: ${input:items:optional mode, then batch name and/or work item IDs, e.g. 361204, or execute 361204}

Before anything else, read these files from `.claude/skills/batch-remediate/`, in this order:
`SKILL.md`, `references/host-copilot.md`, `references/config.symphony.md`, `references/modes.md`.
Confirm `SKILL.md` says version 3.2.0; if it does not, stop and tell me to pull `develop`.
If no mode is given (`modes.md` §1.1), start at the mode the story needs, run it, then halt and ask
me whether to continue to the next mode. After execute, stop: tests wait until I have smoke-tested
on Dev and confirm it at T0 on a later run. The per-mode commands are `/batch-remediate-research`, `-execute`, `-tests` and
`-results`.

Rules that always apply:

1. Ask every gate question in chat as numbered questions with lettered options.
2. At every hard gate (M0, M1, MC, M2, T0, M6) and at **every** step of the story review (description review, AC
   review, supporting sources, certification): ask, then **end your turn**. Do not proceed until I
   reply.
3. For every Azure DevOps operation listed in `references/ado-commands.md`, run that block exactly
   as written in the terminal. Do not use an MCP tool for those operations.
4. I may paste long updates as a plain chat reply. Treat the pasted text as my update.
5. Never write anything to a work item until I approve the final text you showed me.
