---
name: batch-remediate-tests
description: The functionality works: write the unit tests, browser spec and Test Cases, run them, publish the PR, and post a comment with the PowerShell script that runs the tests.
argument-hint: "id=work-item-id"
agent: agent
---

Use the **batch-remediate** skill, version **3.2.0** (`.claude/skills/batch-remediate/SKILL.md`).

Run **tests** mode for work item ${input:id:work item ID, e.g. 361204}.

First ask me gate T0 (did my smoke test show every requirement working?) and end your turn. Then
write the tests per requirement (MSTest with `[TestCategory("TICKET-<id>")]`, jest specs, the browser
spec with its BR- scenario tests and needs annotations, manual and automated Test Cases; every
requirement with the story's flag on, plus one flag-off test per entry point), generate
the run script from `assets/RUN-TESTS.template.ps1`, run the new tests **through that script**, and
report the measured counts. A test that shows the code is wrong is a finding: do not change the
test, tell me to run execute. When everything is green: open the tests PR, verify it, post the
solution comment with the test list, and post the tests comment with the test map and the full
PowerShell script.

Before anything else, read these files from `.claude/skills/batch-remediate/`, in this order:
`SKILL.md`, `references/host-copilot.md`, `references/config.symphony.md`, `references/modes.md`, `references/tests-mode.md`.
Confirm `SKILL.md` says version 3.2.0; if it does not, stop and tell me to pull `develop`.

Rules that always apply:

1. Ask every gate question in chat as numbered questions with lettered options.
2. At every hard gate (T0, M6) and at **every** step of the story review (description review, AC
   review, supporting sources, certification): ask, then **end your turn**. Do not proceed until I
   reply.
3. For every Azure DevOps operation listed in `references/ado-commands.md`, run that block exactly
   as written in the terminal. Do not use an MCP tool for those operations.
4. I may paste long updates as a plain chat reply. Treat the pasted text as my update.
5. Never write anything to a work item until I approve the final text you showed me.
