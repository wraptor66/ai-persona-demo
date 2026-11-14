# Host adapter — Claude Code / Claude

Read at the start of every run. Only this file and `host-copilot.md` name host mechanics; every
other file describes actions.

## Starting a mode

`/batch-remediate research 361204` · `execute` · `tests` · `results`. No mode → auto (suggests the
next one and asks). `$ARGUMENTS` holds the tokens; parse them per `modes.md` §1.

## Asking questions (the gates)

- Use the **AskUserQuestion** tool: up to 4 questions per call, 2–4 options each, recommended option
  first with `(Recommended)` in its label and its grounds in the description. "Other" is added by
  the tool — do not add your own.
- Put the ready-to-use answer text in each option's description, so picking it is writing the
  criterion or the decision.
- Option labels start with their letter (`A. Extend MailboxStatsService`) so chat answers and
  ticket answers (`D-{id}-{n}: A`) use the same letters.
- Hard gates (M0, M1, MC, M2, M6): ask, then **end the turn** until the developer answers.
- Summary gates (M3, M5) are shown as a message and **work continues in the same turn** — do
  not end the turn on them. If the developer sends a change later, apply it before anything it
  affects is pushed.
- If AskUserQuestion is unavailable (headless or scheduled run), behave as `INTERACTIVE = off`.

## Story review (Phase −1 steps A–D)

The four steps in `enrichment.md` are hard gates and loops. In this host:

- **Show the text in a message, then ask with AskUserQuestion** — the current or proposed
  description / AC goes in the message body (plain lines, never inside an option), and the step's
  question goes in AskUserQuestion with the step's lettered options. One step per call.
- **Pasted updates come back as a plain chat reply.** When the developer picks the "I will paste
  updates" / "I will give criteria" / "I will paste the link(s)" option, **end the turn** with one
  line — `Paste your updates as your next message.` — and wait. Do not open another
  AskUserQuestion for the paste; long text does not belong in the "Other" box.
- **End the turn after every step question** until the developer answers. A review loop never
  continues in the same turn as its question.
- **Certification (step D)**: show the instruction from `enrichment.md` §7.1 as a message, then ask
  §7.2 with AskUserQuestion. Nothing in this file or any host file restates the sentence; it lives
  only in `enrichment.md` §7.1.
- Rubric prompts are AskUserQuestion questions in the same call as the step question (up to 4
  questions per call): each prompt's options are its ready-to-paste texts plus `Skip`.

## Azure DevOps

- **For every operation listed in `ado-commands.md`** — reading the story fields and their
  history, rewriting the description / AC, listing and posting comments, creating Test Cases,
  linking them, and adding them to a suite — run the block **exactly as written** in the
  PowerShell tool, one block per call. Do not use the MCP server or a different `az` command for
  these, even when connected.
- For anything else, prefer, in order:
  1. The **Azure DevOps MCP server** tools when connected (repos, PRs, pipelines, wiki reads).
  2. The `az` CLI with the `azure-devops` extension (`az repos`, `az pipelines`).
  3. REST with a bearer token from `az account get-access-token --resource {ADO_RESOURCE_ID}`. On
     Windows use `Invoke-RestMethod` (see the config's ADO mechanics for the traps).

Read before you write. Every write is idempotent by its marker (`ado-comments.md`).

## Browser

The repo configures the playwright MCP server for Copilot in `.vscode/mcp.json`; Claude Code does
not read that file. For a watched replay, run `{E2E_WATCH}` in the terminal; to drive the browser
step by step, add the same server to Claude Code (`claude mcp add`) with the arguments from
`.vscode/mcp.json`. Claude in
Chrome may be used read-only to look at Dev when diagnosing a Phase 7 failure — never to change
data, sign in, or open QA, UAT or Prod. Testers use the Copilot `{TESTER_PROMPT}` flow
(`host-copilot.md`); the evidence format is the same either way.

**Phase 7, run what has not run** (`post-deploy.md` §2.1): run `{E2E_PENDING} --id {id} --plan`
with the Bash/PowerShell tool from `{E2E_ROOT}`, show the plan, ask with one `AskUserQuestion`
(run now · destructive ids · throwaway account), then run it in the foreground — the browser opens
on the developer's screen, so tell them to watch it before the call. Node missing → stop and say
how to install it; never download a runtime into the repo.

## Shell

- Windows hosts: PowerShell by default; `sh` is available through Git for Windows. Run
  `assets/blast-radius.ps1` in PowerShell, or `assets/blast-radius.sh` in `sh`.
- Long builds and test runs: run them, wait for them, and report the measured counts — never
  estimate a test result.
- One worktree per ticket under `{WORKTREE_ROOT}`. Never run Phase 5 in a tree with unrelated
  unstaged changes.

## Delivering changes

- After every follow-up request that changes a file, run `pr-protocol.md` steps 1-4 and 8 before
  replying, and quote the new PR head commit. Local edits are not a PR update.
- Step 9 (the `solution` comment) is posted with `ado-commands.md` §5 once step 8 passes; after a
  follow-up edit, edit that comment (same section, `-Method Patch`) — never post a second one.
- Step 10 (the `logging` comment) is posted the same way, straight after step 9, naming the
  solution comment's id; edit it the same way when a later change touches logging.

## Context

- Read a reference at the start of its phase, not up front.
- After an auto-compaction, re-read `SKILL.md` §Invariants and the ticket's ledger block before
  doing anything else.
- Sub-agents may be used for read-only fan-out (finding callers, searching branches for a
  capability ID). Decisions and writes stay in the main thread.
