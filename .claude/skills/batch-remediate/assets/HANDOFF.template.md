# Developer handoff template

Ends **every pass**, for every ticket: the last message in chat, and a section of the pass's status
comment on the ticket. A developer who reads only this must know exactly what to do next and how
to tell when the story is finished. Concrete actions, numbered, with links. No "consider" or "you
may want to".

```
WHAT YOU NEED TO DO - {id}: {title}
Status: {STATUS}   PR: {url} ({published | draft - reason}) head {short sha}, verified {time}

1. {Action} - {link or exact command}
   Why: {one line}.  Done when: {observable condition}.
2. ...

Waiting on others (no action from you unless they ask):
- {owner}: {what} - {RFI / dependency id}

The story is finished when: {merged, deployed to Dev, every requirement's Dev evidence green,
promotion gate green}. Next run: /batch-remediate {batch} picks up from here.
```

## Every handoff names the next mode

The last numbered item is always the next command, with its condition:

| After | The last item says |
|-------|--------------------|
| `research` | "Run `/batch-remediate execute {id}`." (or: answer D-/RFI- first) |
| `execute` | "Merge PR !{n} (the change is dark behind FF_{id}_{Slug}), turn the flag on in Dev once it deploys, and smoke-test with the checklist above. Not right: edit the story (or tell me what is wrong) and run `/batch-remediate execute {id}`. Works: run `/batch-remediate tests {id}`." |
| `tests` | "Get PR !{n} reviewed. Optional before merge: run the script, then `/batch-remediate results {id}`. After the Dev deploy: `/batch-remediate results {id}` (it runs what has not run, headed, while you watch)." |
| `results` | pre-merge: "Merge, then after deploy `/batch-remediate results {id}`." · `FAILED-IN-DEV`: "`/batch-remediate execute {id}` to fix {req}." · `VERIFIED`: "Move it to QA / Done when you accept it." |

## Execute handoff example

```
WHAT YOU NEED TO DO - 361204: Mailbox statistics columns
Status: SMOKE-TEST (iteration 2)   PR: !74910 (behind FF_361204_MailboxStatsColumns) head 4be21c0, verified 09:12

1. Get PR !74910 reviewed and merged. It is dark behind FF_361204_MailboxStatsColumns, so it changes
   nothing until the flag is on. Done when: the Dev release has deployed it.
2. Turn the flag on in Dev (or say "yes, turn it on" and I will run it):
   .claude\skills\batch-remediate\assets\Set-FeatureFlag.ps1 -Environment dev -Name FF_361204_MailboxStatsColumns -On -Apps web,dataapi
3. Smoke-test on Dev:
   AC-1 Admin > Mailboxes > pick a tenant -> the grid shows Size and Item count columns.
   AC-2 Sort by Size -> largest first; the paging footer still shows the total.
   DOD-1 Watch the console: no new errors.  Logs: the queries in the logging comment.
4. Not right? Edit the story's acceptance criteria (or reply with what is wrong), then
   /batch-remediate execute 361204
5. All working? /batch-remediate tests 361204  - writes the unit tests (flag on and off) and the
   browser spec, opens the tests PR, and posts a PowerShell script that runs them.
```

## What goes in the list, in this order

1. **Certify the story**, when it is `AWAITING-CERTIFICATION`: "Add the certification comment on
   {id} (README, *Certifying a story*), then run /batch-remediate {batch}." Never quote the
   sentence here — this handoff is also posted to the ticket.
2. **Answer open questions**: hard-gate decisions or RFIs only the developer can answer, each with
   the exact reply syntax (`D-{id}-{n}: B`).
3. **The PR**: "Review the diffs in PR {n} (see the solution comment on {id}) and approve the merge
   and the deploy to Dev", or "Publish draft PR {n} once {reason} is resolved". Name the reviewers
   added, or say none could be added and ask who.
4. **Anything the skill could not do on this machine**, with the command: run a test project
   outside the solution, refresh SSO with `npm run auth`, install a missing runtime.
5. **After merge**: run the end-to-end tests (the post-deploy pipeline runs the story's browser
   spec on deploy, or queue it) — the next run posts the results to the story as promotion
   evidence. Say where to look and what "green" means. Name the manual Test Cases the tester must
   pass (plan / suite / ids) and the requirements they check by hand with `/verify-ticket {id}`.
   Then any Admin-page button, manual steps or KQL still needed, and how each is recorded.
6. **Later slices**, if the story is a PR series: "PR-2 ({title}) starts on the next run after
   PR-1 is merged and verified in Dev."

If nothing is needed from the developer right now, say so in one line and say what the story is
waiting on.

## Example

```
WHAT YOU NEED TO DO - 248825: Hide non-clickable buttons on criteria-managed groups
Status: READY-FOR-REVIEW   PR: !74522 (published) head 9cfe627, verified 05:04

1. Get PR !74522 reviewed - reviewers added: Atul Kumar. Approve or comment on the PR.
   Done when: the PR is completed into develop.
2. After the Dev deploy, the post-deploy pipeline runs e2e/248825-criteria-managed-buttons.spec.js
   and posts its evidence comment on 248825. Done when: that comment says PASSED for the latest
   Dev build. If it says FAILED, the next run of /batch-remediate starts the fix.
   The tester checks AC-3 (User Managed group unchanged) by hand: /verify-ticket 248825.
3. Nothing else needed from you.

Waiting on others:
- Dev environment owner: Node runtime on build agents (RFI-ENV-1) - jest specs don't run in CI.

The story is finished when the PR is merged, the Dev check in step 2 passes, and Poulami's QA
task 345785 is done.
```
