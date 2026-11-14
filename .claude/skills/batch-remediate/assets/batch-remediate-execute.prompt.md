---
name: batch-remediate-execute
description: Build the story's functionality behind its feature flag (no new tests yet) so I can merge it, turn the flag on in Dev and smoke-test it. Run it again after I change the story.
argument-hint: "id=work-item-id"
agent: agent
---

Use the **batch-remediate** skill, version **3.2.0** (`.claude/skills/batch-remediate/SKILL.md`).

Run **execute** mode for work item ${input:id:work item ID, e.g. 361204}.

Start from the work item's latest research comment. If the story changed since the last research
or execute comment, show me the change first (gate X1). If my certification is stale, ask me to
re-certify and wait. Build in the story's worktree, keep the existing tests green, and do **not**
write new unit tests, browser specs or Test Cases - that is tests mode. Build the change behind the story's feature flag
(`references/feature-flags.md`, off by default). Push, create or update the PR (published, titled
"behind FF_<id>_<Slug>"), verify it, post or edit the logging comment, and post the execute comment
with the Dev flag command and a numbered smoke-test checklist. Turn the flag on in Dev only if I say
yes. End with what I do next: merge, turn the flag on in Dev, smoke-test there, then either
change the story and run `/batch-remediate-execute` again, or run `/batch-remediate-tests`.

Before anything else, read these files from `.claude/skills/batch-remediate/`, in this order:
`SKILL.md`, `references/host-copilot.md`, `references/config.symphony.md`, `references/modes.md`.
Confirm `SKILL.md` says version 3.2.0; if it does not, stop and tell me to pull `develop`.

Rules that always apply:

1. Ask every gate question in chat as numbered questions with lettered options.
2. At every hard gate (M6, and X1 when my certification is stale or the design reopens) and at **every** step of the story review (description review, AC
   review, supporting sources, certification): ask, then **end your turn**. Do not proceed until I
   reply.
3. For every Azure DevOps operation listed in `references/ado-commands.md`, run that block exactly
   as written in the terminal. Do not use an MCP tool for those operations.
4. I may paste long updates as a plain chat reply. Treat the pasted text as my update.
5. Never write anything to a work item until I approve the final text you showed me.
