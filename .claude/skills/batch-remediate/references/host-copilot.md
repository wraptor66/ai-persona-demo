# Host adapter — GitHub Copilot (VS Code agent mode, Visual Studio 2026, Copilot CLI)

Read at the start of every run. Only this file and `host-claude.md` name host mechanics; every
other file describes actions.

**Copilot may run this skill on any model.** Follow these instructions literally: do the steps in
the order written, stop where it says stop, and do not skip a stop because the next step looks
obvious. When this file says **END THE TURN**, send the message and do nothing else — no tool call,
no file read, no command — until the developer replies.

## First message of every run

Before running any command, tell the developer, in one short message:

```
batch-remediate 3.2.0 — {mode} {batch} {ids}
I will run Azure DevOps and git commands in the terminal. VS Code may ask you to approve each
one; that is expected. I group related calls into one command per step to keep the prompts few.
```

Then continue with the run. (This message is not a question; do not wait for a reply.)

## Starting the skill

- **VS Code**: type `/batch-remediate` in Copilot Chat (agent mode). That runs the committed prompt
  file `.github/prompts/batch-remediate.prompt.md` (a copy of `assets/batch-remediate.prompt.md`),
  which points here and to `SKILL.md`. VS Code also discovers the skill itself in
  `.claude/skills/`; either entry reads the same files, so pulling `develop` is the only setup.
- **Visual Studio 2026 (18.5+)**: skills are picked by description in agent mode. Start with
  "batch-remediate: work items 361204 361210", or run the prompt file.
- There is no `$ARGUMENTS`. Read the mode, batch name and work item IDs from the user's message:
  a first token of `research`, `execute`, `tests` or `results` is the mode; the first other token
  that is not all digits is the batch name; all-digit tokens are IDs.
- **One prompt file per mode** (in `.github/prompts/`, copies in `assets/`), so a developer types
  the mode as the command:
  ```
  /batch-remediate-research id=361204
  /batch-remediate-execute  id=361204
  /batch-remediate-tests    id=361204
  /batch-remediate-results  id=361204
  /batch-remediate items=361204          (auto: suggests the next mode)
  ```
- **One chat session per mode run.** Start a new chat for each `execute` iteration and for
  `tests`; the ticket's comments and the ledger carry everything forward. Long sessions lose
  context and agent mode pauses on long runs.


## Asking questions (the gates)

Copilot has no multiple-choice question tool. Ask in chat, in this exact shape, and **stop and wait
for the reply** — never continue past a hard gate in the same turn:

```
M2 · Approach — 361204
Q1. Where should the mailbox statistics columns come from?
  A. (Recommended — MailboxStatsService already scopes by tenant, MailboxStatsService.cs:88)
     Extend MailboxStatsService behind flag Ticket361204.MailboxStatsColumns.
  B. New isolated MailboxColumnsService that calls the existing tenant scoping.
  C. Other — type it.
Reply with the letter per question, e.g. "Q1: A".
```

- At most 3 questions per message.
- Accept `Q1: B`, `B`, or free text. Anything ambiguous → re-ask, quoting the ambiguity. The same
  letters work as ticket answers (`D-{id}-{n}: B`).
- **Hard gates (M0, M1, MC, M2, M6): ask, then END THE TURN.** Do not proceed until the developer
  replies.
- Summary gates (M3, M5): show them and **keep working in the same turn**. If the developer
  sends a change later, apply it before anything it affects is pushed.
- No way to ask (Copilot CLI in a non-interactive run, cloud agent): behave as `INTERACTIVE = off`.

## Story review (Phase −1 steps A–D) — HARD RULE

Copilot has no question tool, so every step of the story review is a numbered text question with
lettered options, and **every one of them ends the turn**. This applies to every message in steps
A, B, C and D (`enrichment.md` §3–§7): the description review, the AC review, the supplemental
sources question, every "Write this to the story?" question, and the certification question.

For each step, do exactly this:

1. Show the text (current or proposed) as plain lines under its heading from `enrichment.md`.
2. Show the rubric prompts for the step, if any, as numbered questions (`Q2.`, `Q3.`) with lettered
   options and a `Skip` option.
3. Ask the step's question as `Q1.` with its lettered options, then the reply line:
   ```
   Reply with a letter per question, e.g. "Q1: B, Q2: A". To paste updates, reply
   "Q1: B" and paste your text in the same message, or send it as your next message.
   ```
4. **END THE TURN.** Do not proceed until the developer replies.
5. When the reply arrives: if it contains pasted text, that text is the update — any length, any
   format, as a plain chat reply. If it says `B` with no text, reply `Paste your updates as your
   next message.` and **END THE TURN** again.
6. Never write to the story in the same turn as showing a proposed text. Write only after a reply
   that chooses `A. Write it.` for the text shown in the previous message.
7. **Do not proceed to the next step until** the developer has answered the current step's
   question with its done option (`A`).

Certification (step D): show the instruction from `enrichment.md` §7.1, ask §7.2 as `Q1.`, then
**END THE TURN**. Do not run the comment check until the developer answers `A`. The certification
sentence is stated only in `enrichment.md` §7.1; never type it into a comment, a field, a file, a
commit, or a command.

## Azure DevOps

- **For every operation listed in `ado-commands.md`** — reading the story fields and their
  history, rewriting the description / AC, listing and posting comments, creating Test Cases,
  linking them, and adding them to a suite — run the block **exactly as written** in the terminal
  (PowerShell). Do not use an Azure DevOps MCP tool for these, even though `.vscode/mcp.json`
  provides one and the workspace instructions suggest checking it: the shared commands are what
  make Copilot and Claude Code write the same thing to the ticket.
- Batch the reads: steps A–D each start with **one** command that runs `ado-commands.md` §1, §2
  and §4 together (paste the three blocks into one terminal command, one session line at the
  top). Each write (§3, §5, §7, §8) is its own command, run only after the developer's approval.
- For anything else, prefer, in order:
  1. The **Azure DevOps MCP server** configured for the workspace (`.vscode/mcp.json` in VS Code,
     or the MCP settings in Visual Studio), loaded with the `core`, `work`, `work-items`,
     `repositories`, `wiki`, `pipelines`, and `test-plans` domains.
  2. The `az` CLI with the `azure-devops` extension in the terminal tool.
  3. REST from PowerShell with `Invoke-RestMethod` and a bearer token from
     `az account get-access-token --resource {ADO_RESOURCE_ID}`.

Read before you write. Every write is idempotent by its marker (`ado-comments.md`).

## Browser (tester-watched verification)

- The workspace `.vscode/mcp.json` adds the **playwright** MCP server (Edge, visible, signed in with
  the saved `auth-state.json`, video and trace tools enabled). No per-developer setup beyond
  `npm install` and `npm run auth` in `{E2E_ROOT}`.
- The tester runs `{TESTER_PROMPT}` (`.github/prompts/verify-ticket.prompt.md`): it replays the
  story's spec with `{E2E_WATCH}`, drives requirements without a spec through the playwright tools
  one narrated step at a time, asks the tester's verdict per requirement, and posts the evidence.
- **Phase 7, run what has not run** (`post-deploy.md` §2.1): run `{E2E_PENDING} --id {id} --plan`
  in the terminal, show the plan, and **end the turn** with the questions (run now · destructive
  ids · throwaway account). On the answer, run `{E2E_PENDING} --id {id}` with the agreed flags as
  one terminal command; Edge opens on the developer's screen, so say so first. Expect one
  terminal approval prompt for it.
- When the skill itself needs to look at Dev (Phase 7, to understand a failure), use the same
  tools read-only: navigate, snapshot, screenshot, console and network. Never change data, never
  sign in, never open QA, UAT or Prod.

## Shell

- The terminal is PowerShell on Windows; `sh`/`bash` on macOS and Linux. Use
  `assets/blast-radius.ps1` (needs `pwsh` off Windows) or `assets/blast-radius.sh` to match. The
  `ado-commands.md` blocks are PowerShell: off Windows, run them in `pwsh`.
- Terminal commands may ask the developer for approval. Batch related commands into one script per
  step so a gate is not lost among approval prompts.
- Build and test commands can run for many minutes: run them, wait, and report the measured
  counts. Never estimate a result.
- One worktree per ticket under `{WORKTREE_ROOT}`; open that folder, or `cd` into it in every
  command.

## Delivering changes (Copilot-specific traps)

- Agent-mode edits land on disk at once; Keep / Undo in the chat panel is a review aid, not a
  commit. Nothing reaches the PR until it is committed **and pushed**.
- After **every** follow-up request that changes a file, run the PR protocol's steps 1-4 and 8
  (`pr-protocol.md`) before replying. Quote the new PR head commit in the reply. "Updated the
  files" is not "updated the PR".
- Don't create the PR in the same command batch as the first push. Run step 4 (remote branch
  verified) first, then step 7.
- Post step 9 (the `solution` comment, `ado-commands.md` §5) only after step 8 has passed, as its
  own command. After a follow-up edit, edit that comment (same section, `-Method Patch`) — never
  post a second one.
- Post step 10 (the `logging` comment) as its own command straight after step 9, naming the
  solution comment's id; edit it the same way when a later change touches logging.

## Context

- Read each reference at the start of its phase and quote what you rely on, so the developer can
  see which rule applied.
- Long runs lose earlier context. At the start of every phase, re-read the ticket's ledger block
  in `{BATCH_ROOT}/{batch}/BATCH-STATE.md` and the §Invariants of `SKILL.md`.
- Agent mode pauses after a number of requests and asks to continue, so long unattended queues
  stop mid-pass. Prefer one ticket per chat session; for a queue, run the gate sweep in one session
  and the build for each ticket in its own.
