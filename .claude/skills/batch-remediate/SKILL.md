---
name: batch-remediate
description: Work tickets from intake to proof with one engine for every engagement. An engagement config (ticket system, base branch, gates, feature flags, build and test commands, standards, batch root) decides the flow. Enterprise flow (Cushman / Symphony on Azure DevOps): four modes (research, execute, tests, results), certification, hard gates, a feature flag per story, a blast-radius call graph and deployed evidence before QA. Lean flow (Orrum on Jira): one pass per ticket, no gates, questions posted to the ticket for the owner, a draft PR with tests. Decisions, questions and status go to the ticket as comments through a swappable ticket adapter (Jira or Azure DevOps). Resumable across runs. Use whenever the user gives ticket numbers or keys to work (361204, PAI-123), says research / execute / tests / results for a story, "work this story", "run the batch", or asks to pick up where a previous run left off, even without naming this skill.
argument-hint: [research|execute|tests|results] [batch-name] [ticket ...]
---

# batch-remediate v4 — one engine, configured per engagement

Guide tickets **from a story to proof**: understand the story, agree the design, build in
reviewable slices, record every decision and question on the ticket, and prove the result before
the story moves on. The **engine** below is the same for every engagement. Everything that
differs between engagements lives in the **engagement config** (§0), and every ticket-system call
goes through the **ticket adapter** (§2). No phase names a tracker, a branch, a command, or a
project by hand: it reads a config key.

It is a **resumable loop**. Each invocation picks up where the last left off, re-reads each
not-done ticket (fields, comments, PRs, deployments), advances what it can, asks at the gates the
config turns on, and turns everything it cannot resolve into a precise, owned question on the
ticket. The next run reads the answers.

**Version:** `4.0.0` · stamp it, `ENGAGEMENT`, and the host you are actually running in into the
ledger every pass. Engine lineage: 3.2.0 (enterprise modes, flags, gates) + 1.x lean pass + the
Jira adapter of batch-remediate-auto 1.1.

---

## 0. Engagement config

This block is the **only** place engagement-specific values live. An engagement installs its own
copy as `{REPO_ROOT}/.claude/batch-remediate.config.md` (examples: `config/cushman.config.md`,
`config/orrum.config.md`). **That file replaces this block key by key**; a key it does not set
keeps the default below. A key whose value still contains `{fill` stops the run before any ticket
is touched, naming the key. Text after `#`, and a trailing `{check …}` note, are not part of a
value. An indented line under a key continues that key's note.

```
# ---- Core: every engagement sets these ---------------------------------------------------------
ENGAGEMENT        = (set)            # short name, stamped in the ledger and every comment marker
TICKET_SYSTEM     = (set)            # ado | jira  -> the ticket adapter (§2)
BASE_BRANCH       = (set)            # branches are cut from, and PRs target, this branch
GATES             = (set)            # hard gates as a list (M0,M1,MC,M2,M6,T0) | none
FEATURE_FLAGS     = (set)            # on | off  -> every story ships behind a flag (§5.2)
BUILD_CMD         = (set)            # run from REPO_ROOT; exit 0 = built
TEST_CMD          = (set)            # run from REPO_ROOT; the full existing suite, exit 0 = green
STANDARDS         = (set)            # coding-standards file (repo-relative or skill-relative)
BATCH_ROOT        = (set)            # ledgers, reports, refactor log - never inside the repo

# ---- Engine switches: the enterprise features, each first-class and each switchable ---------
MODES             = on               # on: research/execute/tests/results with a halt between (§4.1)
                                     # off: one lean pass per ticket, start to PR (§4.2)
EVIDENCE          = deployed         # deployed: only a run on DEPLOY_ENV proves a requirement (§5.3)
                                     # local: a green TEST_CMD run on the PR head proves it; ends at READY-FOR-REVIEW
GATES_SUMMARY     = M3,M5,X1,X2      # shown, work continues; ignored when GATES = none
GATES_OPTIONAL    = M4               # only when asked for in the run
CERTIFICATION     = on               # on: no solutioning without the developer's certification (gate MC)
INTERACTIVE       = auto             # auto (on for 1 ticket, sweep for a queue) | on | off (never ask in chat)
ENRICH_MODE       = interactive      # interactive | rfi (post the questions to the ticket, @owner) | off
ENRICH_MAX_QUESTIONS = 3
ENRICH_MAX_ROUNDS    = 3
BLAST_GRAPH       = off              # path to a call-graph tool (generated regression set, drift check) | off
BLAST_LOOP_SCAN   = assets/blast-radius.sh   # loop-pacing scan for contract changes (.ps1 on Windows)

# ---- Ticket adapter --------------------------------------------------------------------------
TICKET_URL        = (set)            # e.g. https://dev.azure.com/{org}/{project} | https://{site}.atlassian.net
TICKET_PROJECT    = (set)            # ADO project name | Jira project key
AC_SOURCE         = field            # field (ADO AC field / Jira customfield) | description-heading
AC_FIELD          =                  # Microsoft.VSTS.Common.AcceptanceCriteria | customfield_NNNNN
AC_HEADING        = Acceptance Criteria
DONE_STATUSES     = Closed, Done, Removed
FIELD_WRITEBACK   = on               # on: approved story rewrites are written to the fields; off: kept in the ledger + a comment
QUEUE             =                  # how list_queue finds work: an ADO query id | a Jira label | empty (IDs only)
NEEDS_INFO_LABEL  =                  # label set on a ticket waiting on its owner (Jira); empty = none
ADO_RESOURCE_ID   = 499b84ac-1321-427f-aa17-267ca6975798   # Entra resource for ADO tokens (ado only)
BRANCH_LINK_TARGET = development_link   # ado: acceptance_criteria | development_link
RUN_TAG           = BATCH-{yyyymmdd}

# ---- Code host --------------------------------------------------------------------------------
CODE_HOST         = ado              # ado (az repos) | github (gh)
CODE_REPO         = (set)            # ADO repo name | owner/repo
REPO_ROOT         = (set)
WORKTREE_ROOT     = (set)            # one git worktree per ticket
BRANCH_PREFIX     = feature/
PR_MODE           = published        # published (reviewers added) | draft
EXECUTE_PR        = published        # MODES=on only: published behind the flag | draft | none
REVIEWERS         =                  # empty = PR stays draft and the handoff says who should review

# ---- Tests and evidence -----------------------------------------------------------------------
UNIT_FRAMEWORK    = (set)            # e.g. MSTest + hand-written mocks | xUnit + NSubstitute
UNIT_FILTER       =                  # run only this ticket's tests, {id} substituted
JS_TEST_CMD       =                  # empty = no JS harness in this repo
TEST_CATEGORY     = TICKET-{id}
KNOWN_RED         = -                # recorded known-failing baseline; re-measured each run
TICKET_CID        = TICKET-{id}      # log correlation tag
LOG_SINK          = (trace it)       # where log rows land; traced hop by hop on the first ticket if unknown
E2E_MODE          = off              # on: browser spec per UI-observable requirement
INAPP_MODE        = off              # on: in-app scenario on the Admin page (Symphony harness)
TESTPLAN_MODE     = off              # on: manual Test Cases in the ticket system (ADO only)
RUN_SCRIPT        = off              # on: tests mode commits + posts a PowerShell run script
RUN_SCRIPT_PATH   = analysis/{id}-run-tests.ps1
DEPLOY_ENV        = -                # the environment whose runs count as proof (EVIDENCE = deployed)
PROMOTION_GATE    = -                # the environment that requires all evidence green
VERIFY_CMD        =                  # EVIDENCE = local: optional post-merge check; empty = ends at MERGED

# ---- Feature flags (read only when FEATURE_FLAGS = on) ---------------------------------------
FLAG_PROVIDER     = -                # the mechanism the code reads (an existing one always wins)
FLAG_PREFIX       = FF_{id}_         # FF_{id}_{PascalSlug}
FLAG_TOOL         = -                # script that lists / turns a flag on / off per environment
FLAG_FLIP_ENVS    = dev              # the only environments the skill may flip, and only after a yes

# ---- Engagement knowledge ---------------------------------------------------------------------
KNOWN_FACTS       = -                # file of facts earlier runs paid to learn; read every run
ENGAGEMENT_RULES  = -                # standing rules that become a requirement on every ticket
SECRET_ENV        = -                # names of the env vars holding credentials (never values)
```

### 0.1 What the switches turn off

The enterprise behavior is the default. Each switch removes one feature cleanly; nothing else in
the engine changes.

| Switch | Off / lean value | What stops | What still holds |
|---|---|---|---|
| `MODES` | `off` | The four modes, the halts between them, the smoke-test wait (T0), X1/X2 | Every phase runs in one pass, in order (§4.2) |
| `GATES` | `none` | Every stop-and-wait. Summary gates are not shown | Each gate's choice is the **recommended option**, recorded as `default (GATES=none)` in a decision comment; a fact only a person holds becomes an RFI on the ticket |
| `CERTIFICATION` | `off` | Gate MC and the certification staleness check | `SRC-HASH` still detects a changed story and shows the requirement diff |
| `FEATURE_FLAGS` | `off` | Flag design, the flag check, `DOD-flag-off`, flag flips, flag-removal entries | PRs carry the change live, so `execute` PRs (MODES=on) are opened `draft` |
| `EVIDENCE` | `local` | Deploy gates, post-deploy runs, `VERIFIED`, the promotion gate | Tests green on the PR head prove the code; the ticket ends at `READY-FOR-REVIEW`, then `MERGED` |
| `BLAST_GRAPH` | `off` | Generated regression set and drift check | The loop-pacing scan (`BLAST_LOOP_SCAN`) for contract changes |
| `INTERACTIVE` | `off` | Questions in chat | Questions go to the ticket (`ENRICH_MODE` becomes `rfi`, hard gates post a questionnaire) |

### 0.2 Validation (before the first ticket)

Stop, naming the key and the fix, when:
- a core key is unset or still `{fill …}`;
- `TICKET_SYSTEM` is not `ado` or `jira`, or `CODE_HOST` is not `ado` or `github`;
- `GATES` names a gate not in §6 (`none` must stand alone);
- `FEATURE_FLAGS = on` without `FLAG_PROVIDER` and `FLAG_PREFIX`;
- `EVIDENCE = deployed` without `DEPLOY_ENV` and `PROMOTION_GATE`;
- `TESTPLAN_MODE = on` with `TICKET_SYSTEM = jira` (Test Cases are an ADO feature);
- the adapter's session check (§2.4) fails.

Warn and continue when: `MODES = off` with `T0`, `X1` or `X2` in a gate list (ignored);
`STANDARDS` does not exist (use only the OWASP checklist, `references/standards.md` §2, and say so
in the REVIEW).

---

## 1. Before anything else

1. **Host adapter.** Read **one**: `references/host-claude.md` (Claude Code / Claude) or
   `references/host-copilot.md` (GitHub Copilot). It says how to ask, run shell commands, and call
   the ticket system in that host. Every other file describes *actions*, never tools.
2. **Config.** Load §0, then `{REPO_ROOT}/.claude/batch-remediate.config.md` over it. Validate
   (§0.2). Read `KNOWN_FACTS` when set.
3. **Adapter.** Bind the ticket adapter for `TICKET_SYSTEM` and the code host for `CODE_HOST` (§2)
   and run the session check.
4. **Flow.** `MODES = on` → read `references/modes.md` and settle each ticket's mode (§4.1).
   `MODES = off` → the lean pass (§4.2).
5. **Locate the skill files.** References and assets live beside this file; an account-level
   install reads them from `{REPO_ROOT}/.claude/skills/batch-remediate/`. A missing reference for
   a phase the config turns on stops the run — never improvise a phase.

---

## 2. Ticket adapter

Phases never call a ticket system directly. They call the **interface** below; the adapter for
`TICKET_SYSTEM` implements it. Adding a tracker means adding an implementation table, nothing else.

### 2.1 Interface

| Call | Returns / does | Required |
|---|---|---|
| `fetch_ticket(id)` | `Ticket` (below), including all comments oldest first and the description/AC revision history | yes |
| `post_comment(id, Comment)` | Posts, returns the comment id. Idempotent: an identical marker already on the ticket → return that id, post nothing | yes |
| `list_queue(selector)` | Ticket ids for a batch: explicit ids, `QUEUE`, or a selector the user names | yes |
| `edit_comment(id, commentId, Comment)` | Edits a comment **this skill wrote**; never anyone else's | yes |
| `write_fields(id, {description?, ac?}, revision)` | Rewrites fields, pinned to the revision just read | only when `FIELD_WRITEBACK = on` |
| `link_pr(id, pr)` | Links the PR / branch to the ticket | yes |
| `set_label(id, label, add|remove)` | Adds or removes one label, never replacing the others | optional |
| `mention(person)` | The markup that notifies a person in a comment | yes |

```
Ticket  = { id, key, type, title, state, url, description, ac, dod, owner{id,name},
            reporter{id,name}, labels[], links[], comments[], updated, revision,
            ac_changed_at, desc_changed_at, done (state in DONE_STATUSES) }
Comment = { type, ticket, run_tag, body: [blocks] }      # blocks: heading, para, list, numbered,
                                                         # code(lang), link, mention, table
```

Every comment the skill writes ends with the marker line
`[batch-remediate | {ENGAGEMENT} | {type} | {id} | {RUN_TAG}]`. The adapter renders `Comment.body`
into the tracker's markup; the comment **content** for every type (analysis, decision, status,
answer, evidence, solution, test-list, e2e-results, logging, research, execute, tests, results,
needs-info) is defined once in `references/ado-comments.md` §3 and applies to every tracker.
`needs-info` (lean, §4.2) is: the owner's `mention` first, then a numbered list of exactly what is
missing (each with the recommended answer when one exists), then what the next run does once it
is answered.

### 2.2 Implementation — `ado` (Azure DevOps)

Every call is a block in `references/ado-commands.md`, run exactly as written (it records the
traps that cost earlier runs: `az.cmd` splitting URLs at `&`, empty bodies from `--body @file`,
`devops invoke` unable to update a work item).

| Call | Implementation |
|---|---|
| `fetch_ticket` | `ado-commands.md` §1 (fields, `--expand none`), §2 (updates list → `ac_changed_at`, `desc_changed_at`; never `System.ChangedDate`, which a comment changes), §4 (comments). Bug/Task have no AC field: `ac` = the description's `{AC_HEADING}` section |
| `post_comment` / `edit_comment` | §5. Markup: plain text with `<br>` line breaks (`ado-comments.md` §1); markdown renders literally |
| `list_queue` | Explicit ids; or the saved query `QUEUE` via WIQL |
| `write_fields` | §3, `Invoke-RestMethod` PATCH with a `test` op on `/rev`; field history is the recovery path |
| `link_pr` | Native development link; for a User Story with `BRANCH_LINK_TARGET = acceptance_criteria`, also the branch link appended to the AC field |
| `set_label` | Tags (`System.Tags`), append only |
| `mention` | `<a href="#" data-vss-mention="version:2.0,{id}">@{name}</a>` |

### 2.3 Implementation — `jira` (Jira Cloud)

For this demo's intake, first run
`python3 .claude/skills/batch-remediate/assets/jira_ticket.py [ISSUE-KEY]` from the repo root.
An omitted key uses `JIRA_ISSUE_KEY`, then the oldest Story in `TICKET_PROJECT` (or `JIRA_PROJECT`).
Use its live v3 `summary`, ADF-rendered `description`, and `acceptance_criteria` as requirements;
do not substitute BACKLOG.md when Jira succeeds, even if AC are missing. If `source = backlog`,
explicitly report offline fallback, use it only for local work, and never write back to Jira or
claim the requested live issue was read. The adapter reads the config without sourcing it;
`JIRA_API_TOKEN` must come from the environment. Config has only its `${JIRA_API_TOKEN}` placeholder.

Other calls go through `bash assets/jira.sh METHOD PATH [BODY_FILE]` (Python 3 standard library;
site/email from config or environment, token from environment only;
never put credentials in a command, file, comment or log). Bodies are written to `{BATCH_ROOT}/tmp/` with the Write
tool, never inline. Intake uses REST v3 with ADF; supplementary comment/write calls use
REST v2 (wiki markup as plain text, no ADF).

| Call | Implementation |
|---|---|
| `fetch_ticket` | Run `jira_ticket.py` for v3 summary/description/AC intake as above. When `source = jira`, supplement via `GET /rest/api/3/issue/{key}?fields=status,issuetype,assignee,reporter,labels,updated,issuelinks&expand=changelog`; comments `GET /rest/api/2/issue/{key}/comment?startAt={n}&maxResults=100&orderBy=created` until all read. Owner = assignee, else reporter. `*_changed_at` from the changelog |
| `post_comment` | `POST /rest/api/2/issue/{key}/comment` with `{"body": "<wiki markup>"}` |
| `edit_comment` | `PUT /rest/api/2/issue/{key}/comment/{commentId}` — own comments only |
| `list_queue` | `POST /rest/api/3/search/jql` with `project = {TICKET_PROJECT} AND labels = "{QUEUE}" AND statusCategory != Done ORDER BY updated ASC`, following `nextPageToken` |
| `write_fields` | **Not implemented** (`FIELD_WRITEBACK` must be `off`): approved rewrites go into the `analysis` comment and the ledger, which become the requirement list of record |
| `link_pr` | Branch, commits and PR title start with the key (`PAI-123: …`) so the GitHub for Jira app links them; otherwise `POST /rest/api/2/issue/{key}/remotelink` with `globalId = batch-remediate:pr:{prUrl}` (idempotent) |
| `set_label` | `PUT /rest/api/2/issue/{key}` with `{"update":{"labels":[{"add":"{label}"}]}}` — never `set` |
| `mention` | `[~accountid:{accountId}]` |

Markup: `*bold*`, `# numbered`, `* bullet`, `{code:bash}…{code}`, `[text|url]`. In the marker line
escape each `|` as `\|` (Jira reads `[a|b]` as a link); parse markers with either separator.
`401`/`403` on raw REST calls stop the run with nothing posted; intake alone can return the explicitly
marked offline backlog fallback. The skill never transitions, reassigns, or edits a
Jira issue's summary, description or AC.

### 2.4 Session check

`ado`: `az account get-access-token --resource {ADO_RESOURCE_ID}` succeeds. `jira`:
`GET /rest/api/2/myself` succeeds; its `accountId` identifies the skill's own comments.
`github`: `gh auth status` succeeds. Either failure stops the run before any ticket.

### 2.5 Code host

| Operation | `ado` | `github` |
|---|---|---|
| Create / update PR | `az repos pr create/update` (`references/pr-protocol.md`), description ≤ 4,000 **characters** | `gh pr create --base {BASE_BRANCH} --head {branch} [--draft] --body-file …`; existing → `gh pr edit` |
| Verify PR | head commit, changed files, work-item link, reviewers | `gh pr view {n} --json headRefOid,files,isDraft,reviewRequests,url` |
| Merged? | PR `completed`, merge commit on `BASE_BRANCH` | `gh pr view {n} --json state,mergeCommit` = `MERGED` |
| Never | complete a PR, set a vote, delete a remote branch | `gh pr merge`, `gh pr review --approve`, close a PR, delete a branch |

---

## 3. Arguments

`[mode] [batch-name] [ticket ...]` — tickets are numbers (ADO) or keys (Jira, `PAI-123`).

- **Mode** (`MODES = on` only): `research`, `execute`, `tests` or `results` runs that one mode.
  No mode → `auto`: start at the mode the status calls for, run it, then halt and ask before the
  next (`modes.md` §1.1). With `MODES = off` a mode word is refused with "this engagement runs
  one pass; drop the mode".
- The first other token that is not a ticket id is the batch name.
- Batch + tickets → add new tickets to that batch as `NEW`, then run a pass over the batch.
- Batch only → resume it. Tickets only → batch `default`.
- `--queue` → `list_queue(QUEUE)`. Nothing → list the batches under `{BATCH_ROOT}` and ask (or,
  with `INTERACTIVE = off`, stop and say what to pass). Never guess.

```
/batch-remediate 361204                    # Cushman: auto, next mode then a halt
/batch-remediate research 361204           # Cushman: one mode
/batch-remediate sprint-42 PAI-123 PAI-130 # Orrum: one lean pass per ticket
/batch-remediate --queue                   # whatever QUEUE selects
```

---

## 4. Flows

### 4.1 `MODES = on` — the 3.2 mode engine

Four modes in two phases (`references/modes.md`). **Phase A, functionality:** `research` once,
then `execute` as often as the developer needs while they smoke-test on `{DEPLOY_ENV}` and refine
the story. **Phase B, proof:** `tests` once the developer says it works, then `results`.

- **Always a halt between modes.** `auto` runs one mode, then asks; a named mode runs only itself
  and never silently runs an earlier one (`modes.md` §1.2).
- **Tests wait for the smoke test.** After `execute` the run ends with the smoke-test checklist;
  `tests` starts on a later run after T0. After `tests` the halt is the merge and deploy.
- **`execute` writes no new tests; `tests` writes no new functionality.** A test that shows the
  code is wrong sends the story back to `execute`; it is never weakened.
- **Every mode re-checks the story** (staleness, `SRC-HASH`); a change is shown as a requirement
  diff before any work. Tests written before a later code or story change are **stale**.
- `execute` PRs: `EXECUTE_PR = published` merges to `{BASE_BRANCH}` with the change dark behind
  its flag. With `FEATURE_FLAGS = off`, `execute` PRs are `draft` (a live change is not merged
  untested) and the smoke test runs from the branch build.

Phases by mode: `research` −1, 0, 0.25 · `execute` 0.5, 1, 1.5, 2, 3.5, 5 (steps 1–8, 10) ·
`tests` 3, 3.6, 4, 5 (1–10) per `references/tests-mode.md` · `results` the measured results, then
7. Every mode ends with Phase 6.

### 4.2 `MODES = off` — the lean pass

One pass per ticket, start to PR, no halts between phases:

```
−1 (enrich, as ENRICH_MODE says) → 0 → 0.25 (design, recommended option) → 0.5 → 1 → 1.5 → 2
  → 3 (unit tests, + JS) → 3.5 → 4 → 5 (PR_MODE, steps 1–8, 10) → 6
```

- Not enough information to start (enrichment HALT H1–H3, or no actionable requirement at the
  Phase 0 information gate) → one `needs-info` comment that **@mentions the owner** with exactly
  what is missing, the `NEEDS_INFO_LABEL` if set, status `NEEDS-INFO`, next ticket. The next pass
  reads any human comment newer than it.
- Partially actionable → build what is actionable, carry the rest as RFIs in the status comment.
- The pass ends at `READY-FOR-REVIEW` (PR open, tests green) and the ledger waits for a person.
  A later pass reads `MERGED`; with `VERIFY_CMD` set it runs it on the merged build and posts the
  result, else the ticket stays at `MERGED` for a person to confirm.
- Re-running on an unchanged ticket (`SRC-HASH` and PR head unchanged) posts nothing.

### 4.3 Queue sweep

With several tickets and any hard gate on, run the gate-bearing phases (−1, 0, 0.25) for **all**
tickets first so every question is asked up front, then build. M6 runs once at the end of the
pass. With `GATES = none` there is no sweep: tickets run in order.

---

## 5. Invariants

Always true. Re-read after any auto-compaction. **Core** rules hold for every engagement; a rule
tagged with a switch holds when that switch is on.

### 5.1 Core (never switched off)

**Git and scope**
- Never commit to `{BASE_BRANCH}`. Never merge. Never force-push. Never touch another ticket's
  branch. One **git worktree** per ticket under `{WORKTREE_ROOT}`; stage only the slice's files,
  never `git add -A`.
- One branch per PR, cut from a freshly pulled `{BASE_BRANCH}` (or stacked on the previous PR in
  the series). The ledger's recorded branch always wins; never a second PR for the same slice.
- No code lands until `{BUILD_CMD}` and `{TEST_CMD}` are green at base, apart from `KNOWN_RED`
  (re-measured, never copied).
- Resume, don't restart. Read the ledger first every run. Skip done tickets.

**Specification**
- **The specification is AC ∪ DoD**, plus every `ENGAGEMENT_RULES` line that applies. A DoD line
  no AC covers is `DOD-n` and is worked here, never a follow-up ticket.
- **The ticket is the specification of record.** Wiki pages and links are folded into it, never a
  parallel spec. Enrichment clarifies; it never adds scope. Anything the developer did not state
  or explicitly approve is not written.
- Field write-back (`FIELD_WRITEBACK = on`) rewrites only text the developer **explicitly**
  approved in this session, pinned to the revision just read.

**Evidence**
- Every edit traces to a requirement, comment or source line, pinned `file:line`.
- **An assertion is not evidence.** A comment saying a dependency is ready, a PR merged, or a test
  passed is a claim until the artifact is seen.
- **Skipped is never passed. "Not automated" is never passed. A failing test is never loosened**,
  retried until green, or deleted.
- **A PR is not created until it is verified**: remote head = local `HEAD`, `git status` clean,
  changed files = the intended list, ticket linked (`references/pr-protocol.md`). Re-verified
  after every follow-up edit. Never say a PR is ready before the check passes.
- **Never set DONE.** A person confirms acceptance.

**Collaboration**
- **The ticket is the source of truth; the ledger is a cache.** Every gate outcome, decision,
  question and status lands on the ticket through `post_comment`, so anyone can resume the story.
- Missing information is never guessed: a choice among options is a **decision**, a fact only a
  person holds is an **RFI** with an owner (`mention`).
- **Every pass ends with a handoff**: a numbered list of what the developer must do next, in chat
  and on the ticket (`assets/HANDOFF.template.md`).
- **Secrets never enter a comment, log, ledger or commit.** Record where a secret lives, never
  its value. Refuse a pasted secret and say where it belongs.

**Design**
- Overlapping functionality is shared, never duplicated, in code and in test steps.
- Test-aware plumbing, one code path: test-ness travels in an ambient context; business logic
  never branches on it.
- **A contract change owns its callers** (Phase 1.5). Anything in neither AC nor DoD goes to
  Follow-ups or the refactor log.

### 5.2 `FEATURE_FLAGS = on` — the 3.1 flag lifecycle

`references/feature-flags.md`, with `FLAG_PROVIDER`, `FLAG_PREFIX`, `FLAG_TOOL` from the config.
- **Every story ships behind `{FLAG_PREFIX}{PascalSlug}`.** The flag is part of the design at M2,
  not a question; the only exemption is a change that cannot be gated (schema-only SQL, a build
  or pipeline file), confirmed by the developer and recorded on the ticket.
- Checked **once at the entry point**; **off = exactly the old behavior**. An existing flag
  mechanism wins; never add a second one.
- Committed code: off. Lifecycle per environment as in `feature-flags.md` §5. The skill flips a
  flag only in `FLAG_FLIP_ENVS`, and only after the developer says yes; every other environment
  is flipped by a person.
- Tests run flag **on**; one test per entry point proves **flag off = old behavior**
  (`DOD-flag-off`). Removal is `DOD-flag-removal` plus a refactor-log entry.

### 5.3 `EVIDENCE = deployed` — the 3.2 evidence rules

- **Done = every requirement proven in `{DEPLOY_ENV}`.** Unit tests prove the code; only a run on
  the deployed build proves the feature. `source=local` never counts; pipeline and watched runs do.
- **Stale tests never count**: a run on a build older than the latest one containing the story's
  commits, or tests written before a later code or story change.
- `MERGED` needs the merge commit on `{BASE_BRANCH}`; `DEPLOYED` needs it in a succeeded deploy of
  `{DEPLOY_ENV}` (a build is not a deploy). `VERIFIED` needs every layer green on the latest
  build; nothing reaches `{PROMOTION_GATE}` with a failing, missing or stale test.
- A failure after deployment is a **finding** (`FAILED-IN-DEV`), fixed in the next PR.

With `EVIDENCE = local`: a requirement is proven by a named test passing in a `{TEST_CMD}` run on
the PR head commit (or the PR's CI run, when it reports per-test results). The ticket's furthest
state is `READY-FOR-REVIEW`, then `MERGED`; `VERIFIED` is never claimed.

### 5.4 `CERTIFICATION = on`

**No solutioning without a current certification.** Phase 0 onward starts only when the
developer's own certification comment ("I have verified this story is complete and accurate") is
newer than the last description / AC change (`references/enrichment.md` §7–§8). A later change
makes it stale. **The skill never writes the certification sentence.**

### 5.5 Gates on (`GATES` ≠ `none`)

**The developer decides at the hard gates.** No stage after a hard gate starts without a recorded
answer, in chat or on the ticket (`D-{id}-{n}: B`, or `D-{id}: DEFAULTS`). Every gate question
gets its own `decision` comment: the question, every option as shown, the recommendation and its
grounds, the answer as given, and what each alternative would have changed.

---

## 6. Gates

Each gate is one short questionnaire (`assets/QUESTIONNAIRE.template.md`): numbered questions,
2–4 options written as ready-to-use answers, recommended option first with grounds, free text
always allowed. Definitions in `references/milestones.md`. A gate's kind comes **only** from the
config: in `GATES` → hard; in `GATES_SUMMARY` → shown, work continues; in `GATES_OPTIONAL` → only
when asked; in none of them → not shown, recommended option recorded (§0.1).

| Gate | Phase / mode | Asks | Locks |
|---|---|---|---|
| **M0 Description** | −1 step A | Is this description correct and complete? (loop until yes) | Description |
| **M1 Criteria** | −1 steps B, C | Modifications to the AC? Supporting sources? | AC |
| **MC Certify** | −1 step D | Has the developer certified the story? (read from the comments) | Story as spec |
| **M2 Approach** | 0.25 | Reuse / extend / isolate, where it lives, flag, blast radius | Approach |
| **M3 Plan** | 0.25 | The PR series and each PR's manifest | Slicing |
| **M4 Mid-build** | 1, between layers | Continue to the next layer? | — |
| **M5 Pre-PR** | 4 | Diff, test map, OWASP, consumer impact, risks | PR contents |
| **M6 Close-out** | 6 | Every missing item as a question | Next pass's answers |
| **X1 Story changed** | `execute` re-run | Re-certify if stale; requirement diff; design still fits? | — (hard if stale or M2 reopens) |
| **X2 Pre-push** | `execute` | Diff, build, existing suite, smoke-test checklist | — |
| **T0 Ready for tests** | `tests` | Did the smoke test show every requirement working? | Functionality |

Two escalations hold whatever the lists say: a stale certification at X1 (with `CERTIFICATION =
on`) and graph drift outside the M2 regression set (Phase 1.5) are hard stops.

**Unattended** (`INTERACTIVE = off`, or the host cannot ask): the story review is posted as a
`review-request` comment and the ticket waits (`AWAITING-CERTIFICATION` with certification on);
hard gates post their questionnaire as a `decision` comment and the ticket waits at
`NEEDS-INFO (gate Mn)` until an answer arrives on the ticket.

---

## 7. Lifecycle

```
MODES = on
  research:  NEW → ENRICHING → AWAITING-CERTIFICATION¹ → INVESTIGATING → DESIGNING → RESEARCHED
  execute:   → IN-PROGRESS → SMOKE-TEST                  (repeat while the developer iterates)
  tests:     → TESTING → READY-FOR-REVIEW
  results:   → MERGED → DEPLOYED² → VERIFYING² → VERIFIED² → DONE (a person)

MODES = off
  NEW → ENRICHING → INVESTIGATING → IN-PROGRESS → READY-FOR-REVIEW → MERGED → DONE (a person)

side states: NEEDS-INFO (gate or RFI) · BLOCKED (external dependency)
             FAILED-IN-DEV² (a deployed run failed) · BLOCKED-E2E² (nothing could run on the deployed build)
¹ CERTIFICATION = on    ² EVIDENCE = deployed
```

A ticket with a PR series loops `IN-PROGRESS → …` once per PR. A ledger written by 3.x reads
unchanged; a 1.x ledger at `analysis/BATCH-STATE.md` moves to `{BATCH_ROOT}/{batch}/`.

---

# Per-ticket procedure

Read each phase's reference **before** the phase. Each heading says when the phase runs: the mode
(`MODES = on`), `lean` (`MODES = off`), and any switch it needs.

## Phase −1 — Story review · M0, M1, MC · `research`, lean · `ENRICH_MODE ≠ off`
`references/enrichment.md`. Score the story on the R1–R10 rubric behind the scenes; turn each gap
into a prompt.
- **Interactive:** steps A (M0 description), B (M1 AC as a numbered, testable list), C (M1
  supporting sources), each a loop ending on the developer's "done"; approved text goes through
  `write_fields` (or, with `FIELD_WRITEBACK = off`, into the ledger and the `analysis` comment).
  **D** (MC, when `CERTIFICATION = on`): wait for the developer's own certification comment.
- **`rfi`:** post the questions as one comment that **@mentions the owner** (recommended option
  as the proposed answer); write nothing to the fields; carry on with what is clear unless a HALT.
- **HALT** only on H1 (no outcome), H2 (nothing testable), H3 (unresolved conflict) → `NEEDS-INFO`
  with the questions on the ticket. Every other gap is a warning.
- Skip when the ledger's `SRC-HASH` matches. Every later pass runs the staleness check first.

## Phase 0 — Investigate · `research` (later modes re-read), lean
`references/intake.md`. `fetch_ticket`; read all comments (later wins; claims are not evidence).
Build the checklist once: `AC-n`, `DOD-n`, silent prerequisites, `ENGAGEMENT_RULES`, `AC-repro` /
`AC-regress` for bugs. Pin a manifest `file:line` per requirement. Classify each requirement
**code-provable** or **external** (owner, evidencing artifact, RFI); mark code-provable ones
UI-observable or internal. Information gate: actionable items proceed; the rest become decisions
or RFIs. None actionable → lean: `needs-info` (§4.2); modes: `NEEDS-INFO`.

## Phase 0.25 — Design · M2, M3 · `research`, lean
`references/design-gate.md`. Find what exists on `{BASE_BRANCH}` and on every open branch in the
ledger. Lay out 2–3 options (reuse, extend shared code, isolated slice), each with where it lives,
blast radius, flag (`FEATURE_FLAGS = on`), effort and consequences. The blast radius is the
generated call graph when `BLAST_GRAPH` is set (`design-gate.md` §2.1; the developer accepts the
regression set at M2), otherwise callers found by search. Post the `decision` record. Slice into
a PR series; M3 shows it. Lean: one option is chosen by the recommendation, recorded as a
`decision` comment marked `default (GATES=none)`; the series is usually one PR.

## Phase 0.5 — Baseline · `execute`, lean
Create or resume the worktree and branch `{BRANCH_PREFIX}{id}-{slug}` (Jira: the key first,
`{BRANCH_PREFIX}PAI-123-{slug}`). Run `{BUILD_CMD}`, then `{TEST_CMD}`. Red beyond `KNOWN_RED`
→ `BLOCKED`, name the area, next ticket.

## Phase 1 — Implement · `execute`, lean
Start from the latest `research` comment (modes) or the Phase 0 manifest (lean); on an `execute`
re-run, X1 first. Smallest change that satisfies this PR's manifest; follow the touched files'
patterns; reality diverges → stop and re-pin. Apply `{STANDARDS}` and the OWASP checklist
(`references/standards.md` §2). Inputs arrive as a request DTO; irreversible actions go through a
side-effect gate. `FEATURE_FLAGS = on`: the story's flag is checked once at the entry point and
the shared reader is added if `{BASE_BRANCH}` lacks it. Stage large requirements across commits,
never drop them. M4 if asked.

## Phase 1.5 — Consumer impact · `execute`, lean
For each shared symbol changed, declare the contract delta (blocking, sync, throws, ordered,
bounded, returns). Any change → `{BLAST_LOOP_SCAN} <symbol>` and answer the three pacing questions
per loop-nested call site in the REVIEW; unanswered → a test or a recorded risk. Callers are in
scope. `BLAST_GRAPH` set: regenerate at `HEAD` and run the drift check; exit 3 (callers outside the
M2 regression set) reopens the design with a superseding decision, and M5 becomes hard.

## Phase 2 — Logging · `execute`, lean
`references/logging.md`. Trace one real log call from the touched code to `{LOG_SINK}` hop by hop
(`file:line`); the store comes from the trace, never convention. Every new line carries
`{TICKET_CID}`; nothing sensitive; volume paths log an aggregate. The REVIEW and the `logging`
comment carry a query that has been run once and how to exercise the path.

## Phase 3 — Tests · `tests` (after T0), lean
`references/tests-mode.md` (modes) — lean uses its §3–§4 rules without the run script.
`{UNIT_FRAMEWORK}`, every new test in `{TEST_CATEGORY}` with its requirement id first in the name;
`{JS_TEST_CMD}` specs for client changes when set. Every code-provable `AC-*`/`DOD-*` maps to a
named test; edge and failure branches covered; deterministic (no live network, clock, database or
sleep). Bugs: `AC-repro` fails on the pre-fix commit and passes after, measured in a throwaway
worktree. `FEATURE_FLAGS = on`: tests run flag on, plus `DOD-flag-off`. Confirm the test project
actually runs under `{TEST_CMD}`; if not, run it directly and record the risk.
- `E2E_MODE = on`: a browser spec per UI-observable requirement and per regression-set scenario
  (`references/e2e.md` §1).
- `RUN_SCRIPT = on`: generate `{RUN_SCRIPT_PATH}` from `assets/RUN-TESTS.template.ps1`, run the
  new tests through it, commit it, post it.
- `TESTPLAN_MODE = on`: manual Test Cases per user-checkable requirement and scenario
  (`references/manual-test-cases.md`).

## Phase 3.5 — Refactor capture · `execute`, `tests`, lean
Check each deferral against AC ∪ DoD first. Genuine deferrals → `{BATCH_ROOT}/REFACTOR-OPPORTUNITIES.md`.
An isolated design adds a convergence entry; a flag adds a flag-removal entry.

## Phase 3.6 — In-app scenario · `tests` · `INAPP_MODE = on`
`references/inapp-testing.md`. Registered scenario of shared capability steps; blast-radius
capabilities registered; unit-tested. No harness yet → RFI, continue.

## Phase 4 — Verify · M5 · `tests`, lean (`execute`: X2)
`execute` stops at X2: clean build, existing suite green, diff summary, smoke-test checklist, and
(flags on) the flag and its flip command. `tests` and lean: clean `{BUILD_CMD}`, zero new
warnings, `{TEST_CMD}` green, nothing skipped or commented out; M5 shows diff, requirement → test
map, OWASP, consumer impact, risks. Green blocked by an external dependency → push WIP, draft PR
titled `[BLOCKED]`, record the dependency.

## Phase 5 — Commit, push, PR — verified · `execute`, `tests`, lean
`references/pr-protocol.md` with the code host of §2.5, in order: (1) stage only this slice's
files; (2) commit `{id}: {summary}`; (3) push; (4) **remote head = local `HEAD`**; (5) `link_pr`;
(6) write `analysis/REVIEW-{id}.md`, commit and push it **before** the PR exists; (7) create or
update the PR into `{BASE_BRANCH}` — `execute`: `EXECUTE_PR`, titled `… (behind {flag})` when
flags are on; `tests` and lean: `PR_MODE`, reviewers `REVIEWERS` (none → draft); (8) **verify the
PR**; (9) `tests` only: one `solution` comment (`assets/SOLUTION.template.md`) with the test list;
(10) one `logging` comment, edited by later iterations. After any later edit repeat 1–4 and 8, and
edit those comments instead of posting new ones.

## Phase 6 — State and close-out · M6 · every mode, lean
Tick proven requirements (cite the test and the run it passed in); refresh branch, PR, series,
`LAST-PASS`; count over AC ∪ DoD. Post **one** comment for the run: the mode's comment
(`research` / `execute` / `tests` / `results`) or, lean, `status`. It carries the status and the
**handoff**, which names the next step (modes: the next mode). M6 turns every open RFI, missing
document and undefined term into a questionnaire on the ticket. Status: all code-provable proven
and externals evidenced → `READY-FOR-REVIEW`; some waiting on information → `NEEDS-INFO`; not
green on a dependency → `BLOCKED`. **End in chat with the handoff.**

## Phase 7 — After merge: prove · `results` · `EVIDENCE = deployed`
`references/post-deploy.md`, `references/e2e.md` §2–4. Before merge `results` reports only the
measured unit and JS results and stops after step 1.
1. **Merge gate** — merged per §2.5. Else stop for this ticket.
2. **Deploy gate** — merge commit deployed to `{DEPLOY_ENV}` (every required stage). Else stop.
3. **Evidence** — read `evidence` comments; a run counts only when verified (pipeline run and its
   results, or the run's rows) and on the latest deployed build. Unverifiable = `unverified`.
4. **Test Plan** (`TESTPLAN_MODE = on`) — map tester-authored cases; the skill's own manual cases
   stay pending until a tester passes them.
5. **Run what has not run** — show the plan, ask once, run headed with the developer watching.
6. **Browser results and logs** — drift check, E2E gate; failures are findings
   (`FAILED-IN-DEV`), `BLOCKED` is `BLOCKED-E2E`, never a pass; never loosen, retry or re-run to
   green. Post one `e2e-results` comment. Run the `logging` queries over the window; record rows,
   `NO ROWS` (a finding) or `could not query` (an RFI).
7. **Promotion gate** — every layer green on the latest build → `VERIFIED`; else name the
   requirement and the next step. `FEATURE_FLAGS = on`: read the flag's state per environment;
   a run with the flag off proves nothing.

`EVIDENCE = local`, after merge: `MERGED` from §2.5; `VERIFY_CMD` set → run it, post the output as
an `evidence` comment (`source=command`), report it; no `VERIFIED`.

---

# Batch report

Every pass writes `{BATCH_ROOT}/{batch}/BATCH-REPORT-{yyyymmdd-hhmm}.md` from
`assets/BATCH-REPORT.template.md`, consistent with the ledger, branches, PRs and REVIEW files. It
groups tickets by what they wait on — certification, a gate, information, smoke test, review,
deploy — and shows failing-in-Dev and the promotion state when those switches are on. Print the
status table, open gates, issues requiring resolution, and each ticket's handoff — nothing more.

# Files

| File | Read at | Needed when |
|---|---|---|
| `{REPO_ROOT}/.claude/batch-remediate.config.md` | Start of every run | always |
| `references/host-claude.md` / `host-copilot.md` | Start of every run (one) | always |
| `KNOWN_FACTS` (e.g. `references/config.symphony.md`) | Start of every run | when set |
| `references/modes.md`, `references/tests-mode.md` | Start; `tests` | `MODES = on` |
| `references/feature-flags.md`, `FLAG_TOOL` | research → results | `FEATURE_FLAGS = on` |
| `references/milestones.md` | Before the first gate shown | a gate list is not empty |
| `references/enrichment.md` | Phase −1; §8 every later pass | `ENRICH_MODE ≠ off` |
| `references/ado-commands.md` | Every ADO call | `TICKET_SYSTEM = ado` |
| `assets/jira.sh` | Every Jira call | `TICKET_SYSTEM = jira` |
| `references/ado-comments.md` | Any comment (content for every tracker; §1 markup is ADO's) | always |
| `references/intake.md` · `design-gate.md` · `logging.md` | Phases 0 · 0.25 · 2 | always |
| `{STANDARDS}` + `references/standards.md` §2 (OWASP) | Phase 1 | always |
| `references/pr-protocol.md` | Phase 5 and every later edit | always |
| `references/inapp-testing.md` | Phase 3.6 | `INAPP_MODE = on` |
| `references/manual-test-cases.md` | Phases 3, 7 | `TESTPLAN_MODE = on` |
| `references/post-deploy.md`, `references/e2e.md` | Phases 3, 7 | `EVIDENCE = deployed` / `E2E_MODE = on` |
| `assets/*.template.md` | When producing that artifact | always |
| `assets/blast-radius.sh` / `.ps1` | Phase 1.5 | always |
| `assets/blast-radius-graph/`, `assets/e2e-gate.ps1` | Phases 0.25, 1.5, 7 | `BLAST_GRAPH` set / `E2E_MODE = on` |
| `assets/RUN-TESTS.template.ps1` | `tests` | `RUN_SCRIPT = on` |
| `assets/batch-remediate*.prompt.md` | Copilot entry points | host = Copilot |

Nothing under the skill folder is written at runtime. Ledgers, reports, refactor logs and adapter
temp files live in `{BATCH_ROOT}`; only `REVIEW-{id}.md` (and the run script, when on) live in the
repo, on the ticket's branch.
