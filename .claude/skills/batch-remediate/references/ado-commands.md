# Azure DevOps commands — the exact calls, both hosts

Read before the story review (Phase −1) and before creating test cases (Phase 3). Every operation
below is run **exactly as written** in Claude Code and in GitHub Copilot, so both hosts read and
write a ticket the same way. For these operations do not substitute an MCP tool, `az rest`, or a
different `az` command, even when one looks equivalent: each block was verified against the live
organization, and the alternatives fail in ways listed in §9.

Run the blocks in **PowerShell** (Windows PowerShell 5.1 or `pwsh` 7 on macOS / Linux). Replace
every `{…}` with its value from `config.symphony.md` or the ticket. Run each block as **one**
terminal command, so the developer approves it once.

## 0. Session line — the first line of every block

```powershell
$tok = az account get-access-token --resource {ADO_RESOURCE_ID} --query accessToken -o tsv; $h = @{ Authorization = "Bearer $tok" }; $api = 'https://dev.azure.com/{ADO_ORG}/{ADO_PROJECT_URL}/_apis'
```

`{ADO_PROJECT_URL}` is the project name URL-encoded (`Symphony%20Cloud`). A `401` means the token
expired or `az login` is needed: ask the developer to run `az login`, then re-run the block.

**Bodies always go through a file or a byte array**, never through a command-line argument:

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($path, $json, $utf8)     # write a body file (no BOM)
$text = [string][System.IO.File]::ReadAllText($path)    # read one back (never Get-Content -Raw)
```

## 1. Read the story fields

```powershell
az boards work-item show --id {id} --expand none -o json --fields "System.WorkItemType,System.Rev,System.Title,System.State,System.AreaPath,System.IterationPath,System.Description,Microsoft.VSTS.Common.AcceptanceCriteria,Microsoft.VSTS.TCM.ReproSteps"
```

- `--expand none` is required: without it the command fails with *"The expand parameter can not
  be used with the fields parameter."*
- A field the work item type does not have is left out of the result, not an error. A User Story
  returns `AcceptanceCriteria`; a Bug or Task does not (their criteria live in `System.Description`
  under an `Acceptance Criteria` heading); a Bug returns `ReproSteps`.
- Record `System.Rev`. Every field write (§3) is pinned to it.

## 2. When the description and AC last changed

Field history comes from the **updates** list. Do not use `System.ChangedDate` of the work item:
posting a comment changes it too.

```powershell
$all = @(); $skip = 0
do {
  $p = az devops invoke --area wit --resource updates --route-parameters project="{ADO_PROJECT}" id={id} --query-parameters '$top=200' "`$skip=$skip" --api-version 7.1 -o json | ConvertFrom-Json
  $all += $p.value; $skip += 200
} while ($p.count -eq 200)
$watch = @('System.Description', 'Microsoft.VSTS.Common.AcceptanceCriteria')
$norm = {
  param($html)
  if (-not $html) { return '' }
  $parts = [regex]::Split([string]$html, '(?i)<br\s*/?>|</div>|</p>|</li>')
  $kept = $parts | Where-Object { $_ -notmatch '\?version=GB' }
  (($kept -join ' ') -replace '<[^>]+>', ' ' -replace '&nbsp;', ' ' -replace '\s+', ' ').Trim()
}
$changes = foreach ($u in $all) {
  if (-not $u.fields) { continue }
  foreach ($name in $watch) {
    $f = $u.fields.$name
    if ($f -and ((& $norm $f.oldValue) -ne (& $norm $f.newValue))) {
      [pscustomobject]@{ rev = $u.rev; field = $name; changed = $u.fields.'System.ChangedDate'.newValue; by = $u.revisedBy.uniqueName }
    }
  }
}
$changes | Sort-Object { [datetime]$_.changed } | Select-Object -Last 5 | Format-Table -AutoSize
"LAST CONTENT CHANGE: " + (($changes | Sort-Object { [datetime]$_.changed } | Select-Object -Last 1).changed)
```

- The change time of an update is `fields."System.ChangedDate".newValue`. `revisedDate` is when
  that revision was **superseded** (`9999-01-01` for the current one) — never use it.
- `$norm` drops every line containing `?version=GB` before comparing, so the skill's own
  branch-link line (Phase 5) never counts as a content change. Every other edit, by anyone,
  counts. (Checked on 385198: rev 5, which only appended the branch link, is ignored; revs 1 and
  3, which changed the text, count.)
- `LAST CONTENT CHANGE` is the date the certification check (§4) compares against.

## 3. Rewrite the description and acceptance criteria

Only after the developer approved the **final text in this session** (`enrichment.md` §A, §B).
The approved text is turned into house-rule HTML: plain text, every line escaped, lines joined
with `<br>`, `<b>` only on a heading line. No markdown, no other markup.

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
$toHtml = { param($path) ([System.IO.File]::ReadAllLines($path) | ForEach-Object { $e = [System.Net.WebUtility]::HtmlEncode($_); if ($_.Trim() -eq 'Acceptance Criteria') { "<b>$e</b>" } else { $e } }) -join '<br>' }
$desc = & $toHtml '{scratch}\{id}-description.txt'
$ac   = & $toHtml '{scratch}\{id}-ac.txt'
$patch = @(
  @{ op = 'test';    path = '/rev'; value = {rev} },
  @{ op = 'replace'; path = '/fields/System.Description'; value = $desc },
  @{ op = 'replace'; path = '/fields/Microsoft.VSTS.Common.AcceptanceCriteria'; value = $ac }
)
$body = ConvertTo-Json -InputObject $patch -Depth 5
$r = Invoke-RestMethod -Method Patch -Uri "$api/wit/workitems/{id}?api-version=7.1" -Headers $h -ContentType 'application/json-patch+json; charset=utf-8' -Body $utf8.GetBytes($body)
"written: rev {0}" -f $r.rev
```

- `{scratch}` is a scratch folder outside the repo and outside `{BATCH_ROOT}` (for example the
  system temp folder). The text files hold exactly the text the developer approved.
- **Only the fields being changed go in the patch.** Description only → drop the AC line.
- **Bug and Task**: drop the AC line; the description text carries the criteria under a line that
  reads exactly `Acceptance Criteria`, which `$toHtml` renders bold.
- `test /rev` refuses the write if anyone edited the item since §1
  (`VS403351: Test Operation for path /rev failed`). Re-read §1, show the developer what changed,
  and ask again. Never retry without the test.
- **Recovery**: the field's previous value stays in the work item's History tab. To undo, read the
  old value from the update in §2 (`oldValue`) and write it back with this same block.
- Record the new `rev` in the ledger.

## 4. List comments (the certification check)

```powershell
$all = @(); $token = $null
do {
  $q = @('$top=200', 'order=asc'); if ($token) { $q += "continuationToken=$token" }
  $p = az devops invoke --area wit --resource comments --route-parameters project="{ADO_PROJECT}" workItemId={id} --query-parameters $q --api-version 7.1-preview -o json | ConvertFrom-Json
  $all += $p.comments; $token = $p.continuationToken
} while ($token)
$all | ForEach-Object { [pscustomobject]@{ id = $_.id; created = $_.createdDate; by = $_.createdBy.uniqueName; text = (($_.text -replace '<[^>]+>', ' ') -replace '\s+', ' ').Trim() } }
```

- `--api-version 7.1-preview` exactly. `az devops invoke` rejects `7.1-preview.4`
  (*"could not convert string to float"*).
- Each comment has `id`, `createdDate`, `modifiedDate`, `createdBy`, `text`. The certification
  rule (`enrichment.md` §D) uses `createdDate` — an older comment edited later never certifies.
- Deleted comments are not returned.

## 5. Post a comment

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
$html = [string][System.IO.File]::ReadAllText('{scratch}\{id}-comment.html')
$body = ConvertTo-Json -InputObject @{ text = $html } -Depth 3
$c = Invoke-RestMethod -Method Post -Uri "$api/wit/workItems/{id}/comments?api-version=7.1-preview.4" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($body)
"posted comment {0} at {1}" -f $c.id, $c.createdDate
```

- The comment file is already house-rule HTML (`ado-comments.md` §1) and ends with its marker line.
- Before posting, check §4's list for this pass's marker (idempotent). After posting, record the
  returned `id` in the ledger's `POSTED` list — the certification check ignores those ids.
- Editing the skill's own status comment from this pass: same block with `-Method Patch` and the
  URI `…/comments/{commentId}?api-version=7.1-preview.4`.

## 6. Find or create the story's test suite

Cases go into the **sprint test plan** whose iteration is the story's `System.IterationPath`, in
a static suite named `{TESTCASE_SUITE_NAME}` (`User story-{id}`, `Bug-{id}`, `Task-{id}`) directly
under the plan's root suite — the team's existing convention.

```powershell
$plans = (az devops invoke --area testplan --resource Plans --route-parameters project="{ADO_PROJECT}" --query-parameters filterActivePlans=true --api-version 7.1 -o json | ConvertFrom-Json).value
$plan = @($plans | Where-Object { $_.iteration -eq '{iteration}' -and $_.name -like '{TESTCASE_PLAN_PREFIX}*' })
"plans found: {0}" -f $plan.Count; $plan | ForEach-Object { "{0} | {1} | root {2}" -f $_.id, $_.name, $_.rootSuite.id }
if ($plan.Count -eq 1) {
  $suites = (az devops invoke --area testplan --resource Suites --route-parameters project="{ADO_PROJECT}" planId=$($plan[0].id) --api-version 7.1 -o json | ConvertFrom-Json).value
  $suites | Where-Object { $_.name -eq '{suiteName}' } | ForEach-Object { "suite {0} | {1}" -f $_.id, $_.name }
}
```

- **Exactly one plan** must match. None or several → ask the developer which plan (a decision,
  `milestones.md`); never create a test plan.
- Suite missing → create it under the root suite:

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
$body = ConvertTo-Json -InputObject @{ suiteType = 'staticTestSuite'; name = '{suiteName}'; parentSuite = @{ id = {rootSuiteId} } } -Depth 3
$s = Invoke-RestMethod -Method Post -Uri "$api/testplan/Plans/{planId}/suites?api-version=7.1" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($body)
"suite {0} created: {1}" -f $s.id, $s.name
```

## 7. Create a manual Test Case linked to the story

One block per case. The steps come from `manual-test-cases.md`; each step has an action and,
for a check, the expected result.

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
$esc = { param($s) [System.Security.SecurityElement]::Escape('<p>' + [System.Net.WebUtility]::HtmlEncode($s) + '</p>') }
$steps = @(
  @{ action = '{step 1 action}'; expected = '' },
  @{ action = '{step 2 action}'; expected = '{step 2 expected result}' }
)
$n = 1
$xml = ($steps | ForEach-Object {
  $n++; $type = if ($_.expected) { 'ValidateStep' } else { 'ActionStep' }
  '<step id="' + $n + '" type="' + $type + '"><parameterizedString isformatted="true">' + (& $esc $_.action) + '</parameterizedString><parameterizedString isformatted="true">' + $(if ($_.expected) { & $esc $_.expected } else { '' }) + '</parameterizedString><description/></step>'
}) -join ''
$xml = '<steps id="0" last="' + $n + '">' + $xml + '</steps>'
$patch = @(
  @{ op = 'add'; path = '/fields/System.Title'; value = '{case title}' },
  @{ op = 'add'; path = '/fields/System.AreaPath'; value = '{story AreaPath}' },
  @{ op = 'add'; path = '/fields/System.IterationPath'; value = '{story IterationPath}' },
  @{ op = 'add'; path = '/fields/System.Tags'; value = 'batch-remediate; TICKET-{id}; {requirement id}' },
  @{ op = 'add'; path = '/fields/Microsoft.VSTS.TCM.Steps'; value = $xml },
  @{ op = 'add'; path = '/relations/-'; value = @{ rel = 'Microsoft.VSTS.Common.TestedBy-Reverse'; url = 'https://dev.azure.com/{ADO_ORG}/_apis/wit/workItems/{id}' } }
)
[System.IO.File]::WriteAllText('{scratch}\{id}-tc.json', (ConvertTo-Json -InputObject $patch -Depth 6), $utf8)
$tc = az devops invoke --area wit --resource workItems --route-parameters project="{ADO_PROJECT}" type="Test Case" --http-method POST --media-type "application/json-patch+json" --in-file '{scratch}\{id}-tc.json' --api-version 7.1 -o json | ConvertFrom-Json
"test case {0} created" -f $tc.id
```

- `Microsoft.VSTS.Common.TestedBy-Reverse` on the Test Case is the **Tests** end; the story then
  shows the case under **Tested By**. Confirm with
  `az boards work-item relation show --id {id} -o table` after the last case.
- Step ids start at 2; `last` is the highest id. Each string is the step's HTML, XML-escaped —
  the exact shape testers' cases already use.
- **Idempotent by tag**: before creating, read the suite's cases (§8, first block) and skip a
  requirement whose case already carries `TICKET-{id}; {requirement id}`. A changed requirement
  updates its case (§3's block pattern against the case id, `Microsoft.VSTS.TCM.Steps` only).

## 8. Add the case to the suite, and read the suite

```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
$body = ConvertTo-Json -InputObject @(@{ workItem = @{ id = {testCaseId} } }) -Depth 4
Invoke-RestMethod -Method Post -Uri "$api/testplan/Plans/{planId}/Suites/{suiteId}/TestCase?api-version=7.1" -Headers $h -ContentType 'application/json; charset=utf-8' -Body $utf8.GetBytes($body) | Out-Null
(az devops invoke --area testplan --resource SuiteTestCase --route-parameters project="{ADO_PROJECT}" planId={planId} suiteId={suiteId} --api-version 7.1 -o json | ConvertFrom-Json).value | ForEach-Object { "{0} | {1}" -f $_.workItem.id, $_.workItem.name }
```

- `ConvertTo-Json -InputObject @( … )` — the body must be a JSON **array**. Piping a one-element
  array into `ConvertTo-Json` in Windows PowerShell sends an object and the call fails.
- Several cases → one array with every id, one call.

## 9. Why not the shorter commands

Each of these was tried against `{ADO_ORG}` and failed; do not fall back to them.

| Shortcut | What happens |
|---|---|
| `az devops invoke --area wit --resource workItems … id={id} --http-method PATCH` | Crashes with `KeyError: 'type'`: two locations share that resource name and `invoke` always picks the create-by-type one. Updates must go through §3. |
| `az rest --url "…?a=1&api-version=7.1"` in PowerShell | `az` is `az.cmd`; `cmd.exe` splits the URL at `&` and sends half a request. |
| `az boards work-item update --fields "System.Description=<html>"` | The HTML passes through `cmd.exe`, which treats `<`, `>`, `&`, `|` and quotes as syntax. |
| `az rest --body @file` (comment edit) | Sends an empty `text` (*"You must provide a value for the text parameter"*). |
| `az devops invoke … --api-version 7.1-preview.4` | *"could not convert string to float"*; use `7.1-preview`. |
| `Get-Content -Raw` for a body | Returns a decorated string; `ConvertTo-Json` serialises its metadata. Use `ReadAllText`. |
