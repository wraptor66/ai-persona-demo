# batch-remediate v3

**Version 2.6.0.** Guides Azure DevOps stories and bugs from intake to **verified in Dev**: a
guided review that makes the story complete and has the developer certify it, a design agreement
with the developer, milestone gates, a PR series, unit tests, manual Test Cases for the tester, an
in-app test on the Azure Admin page, decisions written to the ticket, a close-out questionnaire,
and post-deploy proof that gates promotion to QA. Resumable across runs, sessions, machines, and
developers.

Works in **Claude Code** and **GitHub Copilot** (VS Code agent mode, Visual Studio 2026 18.5+,
Copilot CLI) from one folder.

## Install — one copy, both tools

Commit this folder to the repo at:

```
<repo root>/.claude/skills/batch-remediate/
```

Both tools read that location: Claude Code natively, Copilot because it scans `.github/skills/`,
`.claude/skills/`, and `.agents/skills/`. Keep **one** copy:

- Remove any personal copy at `~/.claude/skills/batch-remediate/` or `~/.copilot/skills/batch-remediate/`
  — a personal skill with the same name silently overrides the repo copy. That includes a
  `batch-remediate` skill added in claude.ai (it syncs to `~/.claude/skills/synced/…`): replace it
  with a pointer to this folder or remove it.
- Do not also install the Copilot-only package into `.github/skills/` in the same repo; Copilot
  would see two skills with one name.

Copilot: the `/batch-remediate` entry is committed at `.github/prompts/batch-remediate.prompt.md`
(a copy of `assets/batch-remediate.prompt.md` — change both together). Pulling `develop` is the
only setup.

## Use — two phases, four modes (3.0)

```
Phase A - get the functionality right
  /batch-remediate research 361204    review and certify the story, agree the design. No code.
  /batch-remediate execute 361204     build it (draft PR, logging comment). Smoke-test.
                                      Change the story. Run execute again.
Phase B - prove it
  /batch-remediate tests 361204       it works: unit tests, browser spec, Test Cases; publish the
                                      PR; post a comment with the PowerShell script that runs them
  /batch-remediate results 361204     before merge: unit + jest results. After the Dev deploy:
                                      run what has not run, the gate, the logs, promotion to QA

/batch-remediate 361204               no mode: suggests the next one and asks
```

Copilot: `/batch-remediate-research id=361204` (and `-execute`, `-tests`, `-results`). Start a new
chat for each run; the work item's comments carry everything forward. In Visual Studio, say
"batch-remediate execute: work item 361204" in agent mode, or run the prompt file.

Every story ships behind a feature flag (`FF_{id}_{Slug}`, `references/feature-flags.md`). `execute`
keeps the existing tests green, writes no new ones, and opens a PR that can merge: the change is dark
until the flag is turned on. Turn it on in Dev to smoke-test
(`assets/Set-FeatureFlag.ps1 -Environment dev -Name FF_… -On`); QA, UAT and Prod are turned on by a
person. Nothing untested reaches QA: the promotion gate still needs every test green on Dev.

## What the developer sees

| Gate | When | Developer does |
|------|------|----------------|
| M0 Description | First minutes | Says whether the description is right; pastes updates; approves the rewrite before it is written |
| M1 Criteria | Next | Says whether the AC need changes; gives them; approves the numbered rewrite; offers wiki pages or links to fold in |
| MC Certify | Then | Adds the certification comment on the story in ADO (below), and says so |
| M2 Approach | After investigation | Chooses reuse / extend / isolate, location, feature flag |
| M3 Plan | Same sitting | Glances at the PR series |
| M5 Pre-PR | Before push | Glances at the diff, tests, manual Test Cases, risks |
| M6 Close-out | End of pass | Answers missing docs, key locations, examples — or routes them |
| X1 Story changed | execute re-run | Re-certifies if needed; confirms the requirement diff |
| X2 Pre-push | end of execute | Glances at the diff and the smoke-test checklist |
| T0 Ready for tests | start of tests | Says the smoke test showed every requirement working |

Hard gates (M0, M1, MC, M2, T0, M6) wait for an answer. The others show a summary and continue.

At the end of solutioning the story gets one **solution** comment and the PR to review. After the
Dev deploy, run the end-to-end tests; the next run posts their results to the story for QA.

## Certifying a story

No work starts on a story until you certify it. After the story review (or after editing the
description and acceptance criteria yourself in ADO), open the story in Azure DevOps and add a
comment that contains exactly:

**I have verified this story is complete and accurate**

Add it yourself, after your last change to the description or acceptance criteria. The skill
accepts it only if it is newer than the last change to either field, and **never** writes that
sentence itself — every comment it posts carries your name, so it could otherwise certify the
story with nobody having checked it. If either field changes later, the certification is stale
and the skill stops until you add a new one. (Edits the skill makes to its own branch-link line
don't count.)

## After the merge — browser proof (2.2)

The story's PR includes a browser spec (`symphonyModernization/tests/e2e/{id}-{slug}.spec.js`).
Once the merge is deployed to Dev:

- **You** (2.5) run `/batch-remediate {id}` again. The skill finds the latest Dev build, lists the
  story's tests that have no result on it (from the test list in its `solution` comment), asks you
  once, runs them in Edge while you watch, posts the evidence to the story and reads the gate.
  Needs Node, `npm install` and a fresh `npm run auth` in `symphonyModernization/tests`.
- **The pipeline** `Dev PostDeploy Browser Tests` runs every spec headless and posts an evidence
  comment to each story in the build: result per requirement, screenshots, console errors, failed
  requests, server-log window, and a zip of traces and videos.
- **The tester** runs `/verify-ticket {id}` in VS Code Copilot: Edge replays the spec slowly while
  they watch, Copilot drives anything without a spec one narrated step at a time, they give the
  verdicts, and they are posted to the story.
- **Manual Test Cases** (2.3): the skill writes one ADO Test Case per requirement and per
  blast-radius screen, in plain user steps, in the story's suite in the sprint test plan, linked
  to the story (Tested By). They stay pending until the tester passes them.

A failed or missing result blocks promotion to QA. Setup: `symphonyModernization/tests/e2e/README.md`.

## Contents

```
batch-remediate/
├── SKILL.md                         conductor: config, invariants, gates, lifecycle, phases
├── README.md · CHANGELOG.md
├── references/                      read at the phase that needs them
│   ├── host-claude.md               how to ask / call ADO / run shell in Claude
│   ├── host-copilot.md              the same for Copilot
│   ├── config.symphony.md           org, fields, paths, and known facts about this repo
│   ├── modes.md                     research / execute / tests / results: phases, gates, comments
│   ├── tests-mode.md                tests mode: T0, writing the tests, the run script, publishing
│   ├── milestones.md                the eight gates, answer syntax, unattended behavior
│   ├── enrichment.md                Phase −1: guided story review A–D, rubric, certification, staleness
│   ├── ado-commands.md              every ADO read/write, verified; both hosts run these exactly
│   ├── manual-test-cases.md         Phase 3: the tester's manual Test Cases, in the sprint plan
│   ├── intake.md                    Phase 0: spec discovery, checklist, decision vs RFI
│   ├── design-gate.md               Phase 0.25: reuse/extend/isolate, flags, blast radius, PR series
│   ├── standards.md                 coding standards, flag rules, pacing, OWASP
│   ├── logging.md                   trace the sink, correlation IDs, EventIds, queries, the logging comment
│   ├── inapp-testing.md             Phase 3.6: Admin-page scenario, capability steps, blast-radius suite
│   ├── ado-comments.md              the ticket comment protocol
│   ├── pr-protocol.md               commit, push, create and VERIFY the PR (Phase 5 and every follow-up)
│   ├── post-deploy.md               Phase 7: deploy gate, evidence, Test Plans, publisher, QA gate
│   └── e2e.md                       browser spec: written with the PR, run after deploy
└── assets/
    ├── BATCH-STATE / BATCH-REPORT / REVIEW / TICKET-COMMENT / REFACTOR-OPPORTUNITIES templates
    ├── QUESTIONNAIRE / DECISION / EVIDENCE / HANDOFF templates (new in v2)
    ├── SOLUTION template (new in 2.3) · LOGGING template (new in 2.6)
    ├── batch-remediate.prompt.md                          Copilot entry (copy in .github/prompts/)
    ├── RUN-TESTS.template.ps1                             the per-story run script (tests mode)
    ├── blast-radius.sh                                    JS/HTML, sh (loop pacing)
    ├── blast-radius.ps1                                   C#/JS/HTML, PowerShell (loop pacing)
    ├── blast-radius-graph/                                call graph + regression-set drift (.NET SDK)
    └── e2e-gate.ps1                                       E2E layer of the promotion gate (read-only)
```

## Where run output goes

| Output | Location |
|--------|----------|
| `REVIEW-{id}.md`, `{id}-run-tests.ps1` | `analysis/` in the repo, on the ticket's branch |
| Run-script results (`summary.json`, TRX, JSON) | `analysis/test-results/{id}/` (gitignored) |
| `BATCH-STATE.md`, `BATCH-REPORT-*.md`, `REFACTOR-OPPORTUNITIES.md` | `~/symphony-batches/{batch}/` |
| Gate outcomes, decisions, RFIs, answers, status, evidence | Comments on the work item |

## Depends on work outside the skill

The skill specifies these; each is its own story:

1. **In-app test harness** (Admin page "Ticket Tests", run/status endpoints, run log) — `inapp-testing.md` §1.
2. **Results publisher** — Symphony posts evidence comments and promotion status to ADO — `post-deploy.md` §4.
3. **Promotion gate** — required PR status on `qa` + environment check — `post-deploy.md` §6.
4. **UI runner in the VNet** (Container Apps job) — `post-deploy.md` §5.

Until 2 exists, set `RESULTS_PUBLISHER = manual`; the developer pastes the Admin page's evidence
summary and the skill posts it.

## Before the first run

- Trial on one low-risk story. Read the analysis comment, the decision record, the REVIEW, and the
  batch report before handing it a queue.
- Confirm `INAPP_PAGE` and `INAPP_STEPS` are found and pinned on the first ticket.
- Re-measure the known-failing baseline and record it in the ledger.
