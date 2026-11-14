# PR protocol — commit, push, create, verify (Phase 5)

Read at Phase 5 (`execute`: PR behind the flag; `tests`: the tests PR), and again after **any** later edit to a slice: a follow-up request from the
developer, a review fix, a wording change. A PR is the developer's view of the work. If the PR
does not show the change, the work is not delivered, however correct the local files are.

## The rule

> **"Created", "updated", "ready" and "fixed" are claims about the remote PR. Make them only after
> step 8 passes.** Local files, a local commit, or a pushed branch are not a PR.

Known failure this prevents (Copilot run on 289884): the PR was created before the commits were
pushed, so it showed an empty diff. Then a follow-up wording change was made locally but never
pushed while the developer was told the PR was updated.

## Steps — in order, none skipped

Run from the ticket's worktree. Commands are PowerShell + `az`. Use the ADO MCP equivalents where
the host prefers them, but the checks are the same. Every `az repos` / `az devops` call takes
`--org https://dev.azure.com/{ADO_ORG} -p "{ADO_PROJECT}"` unless `az devops configure --defaults`
already sets them. Check once with `az devops configure --list`.

**1. Clean, scoped working tree**
```powershell
git status --porcelain
```
Stage only this slice's files by path (`git add -- <paths>`), never `git add -A`. Anything else
modified is listed to the developer and left alone. If nothing is staged and nothing is ahead of
the remote, there is nothing to deliver: say so, don't create an empty PR.

**2. Commit** — `git commit -m "{id}: {summary}"`.

**3. Push** — `git push -u origin {branch}`.

**4. Verify the remote branch**
```powershell
$local  = git rev-parse HEAD
$remote = (git ls-remote origin "refs/heads/{branch}").Split("`t")[0]
if ($local -ne $remote) { throw "Remote branch is at $remote, local HEAD is $local - push did not land." }
git status --porcelain   # must be empty for the slice's files
```

**5. Branch link** on the ticket (`BRANCH_LINK_TARGET`; Bugs and Tasks use the development link).

**6. REVIEW file before the PR.** Write `analysis/REVIEW-{id}.md`, commit it, push it, and repeat
step 4. The PR must open with the REVIEW already in it.

**7. Create or update the PR**
```powershell
$existing = az repos pr list -r "{ADO_REPO}" -s "{branch}" --status active --query "[0].pullRequestId" -o tsv
# none -> create
az repos pr create -r "{ADO_REPO}" -s "{branch}" -t develop `
  --title "{id}: {summary}" --description @descLines `
  --work-items {id} --reviewers {REVIEWERS} --draft {false|true}
# exists -> update title/description if needed; never open a second PR for the same slice
```
- **`execute` mode → published, behind the story's flag** (`EXECUTE_PR = published`), titled
  `{id}: {summary} (behind FF_{id}_{Slug})`, description starting "Functionality iteration {n},
  dark behind FF_{id}_{Slug} (off unless turned on per environment). Tests follow in batch-remediate
  tests mode." It may merge: the flag keeps it off outside Dev, and the QA promotion gate still
  needs the story's tests green. The story's open PR is updated in place; after a merge the next
  iteration is a new branch `{branch}-{n}` from a fresh `develop`. `EXECUTE_PR = draft` → the 3.0
  draft behavior; `none` → steps 1–4 only.
- **`tests` mode → the tests PR**: the open `execute` PR when it has not merged (retitle to
  `{id}: {summary}`, update the description with the test map and run-script path), else a new
  branch `{branch}-tests` and a PR `{id}: tests - {summary}`. Never a second open PR for one branch.
- **Published by default** (`PR_MODE = published`) in `tests` mode. Create it as a **draft only** when a reason
  exists, and state the reason in the PR description and the handoff: build or suite not green
  (`[BLOCKED]`), a hard gate still open, an OWASP risk raised and not accepted, or the developer
  asked for a draft.
- **Reviewers**: `{REVIEWERS}` from config. Branch-policy required reviewers are added by ADO
  automatically. If neither yields a reviewer, say so in the handoff and ask the developer who.
- **Description**: 4,000 characters maximum, counted as characters. Pass multi-line text as an
  array of lines (`--description @lines`). Blank lines become `" "`, because Windows PowerShell
  drops empty native arguments.

**8. Verify the PR — the gate for saying "done"**
```powershell
$pr = az repos pr show --id $prId -o json | ConvertFrom-Json
$prHead = $pr.lastMergeSourceCommit.commitId
if ($prHead -ne (git rev-parse HEAD)) { throw "PR head $prHead != local HEAD - PR is stale." }
# changed files on the PR vs the intended list
$iter = az devops invoke --area git --resource pullRequestIterations `
          --route-parameters project="{ADO_PROJECT}" repositoryId="{ADO_REPO}" pullRequestId=$prId `
          -o json | ConvertFrom-Json
$last = ($iter.value | Sort-Object id | Select-Object -Last 1).id
$chg  = az devops invoke --area git --resource pullRequestIterationChanges `
          --route-parameters project="{ADO_PROJECT}" repositoryId="{ADO_REPO}" pullRequestId=$prId iterationId=$last `
          -o json | ConvertFrom-Json
$chg.changeEntries.item.path   # compare with: git diff --name-only origin/develop...HEAD
```
Also confirm the work item is linked (`az repos pr work-item list --id $prId`), reviewers are
present, and the draft state is as intended. ADO updates the PR head a few seconds after a push.
If the head lags, wait and re-check up to 3 times before treating it as a failure.

Any mismatch → fix (push, re-link, add reviewer) → run step 8 again. Report the PR only when
every check passes, and quote the verified head commit (short SHA) in the handoff.

**9. The solution comment.** Once step 8 passes, post **one complete `solution` comment** to the
story (`assets/SOLUTION.template.md`, posted with `ado-commands.md` §5): everything analyzed,
decided and done for this slice, the tests and the manual Test Cases, and the verified PR link
with its head commit. This is the developer's cue to review the diffs and approve the merge and
the deploy to Dev. After a later edit to the slice, re-run steps 1–4 and 8, then **edit** this
comment (`rev` +1) — never post a second one for the same PR.

**10. The logging comment.** Straight after step 9, post **one `logging` comment**
(`assets/LOGGING.template.md`, `logging.md` §8, posted with `ado-commands.md` §5) naming the
solution comment by id. It covers what this slice logs, the store each row lands in (traced in
code), where it is silent, how to make it run, the ready-to-paste query per store, what good and
bad results look like, and what the logs cannot show. Post it even when the slice adds no logging.
After a later edit that changes logging, **edit** it (`rev` +1) alongside the solution comment.

## After any later edit

A follow-up request ("change the wording", "rename that method") is a new delivery. Run steps
1–4 and 8 again, update the REVIEW if it no longer matches the diff, edit the step 9 comment (and the step 10 comment if logging changed), and
only then say the PR is updated, quoting the new head commit.

## Record

Ledger `PRS` line: PR id, state (published/draft and why), verified head SHA, verified-at time.
