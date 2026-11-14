<#
.SYNOPSIS
  e2e-gate.ps1 - the promotion gate's E2E layer (batch-remediate L7). Reads the story's evidence
  comment and decides VERIFIED, FAILED-IN-DEV, BLOCKED-E2E or NEEDS-INFO. Read-only: it never writes
  to ADO.

.DESCRIPTION
  post-deploy.md §6 is the specification; this script is the mechanical part of it, so the six
  hardening checks are repeatable. The checks run in this order, and the first one that applies decides:

    1. Story merged?            its PRs' merge commits (or -StoryCommits)           -> NEEDS-INFO
    2. Latest deployed build    newest Dev build whose release deployed every required stage,
                                and it contains every story commit                  -> NEEDS-INFO
    3. Evidence present?        newest e2e evidence comment; none                   -> NEEDS-INFO (unverified)
    4. Evidence current?        its build is the latest deployed build; else stale  -> NEEDS-INFO (stale)
    5. Blocked?                 result=BLOCKED (nothing ran)                        -> BLOCKED-E2E
    6. Failed?                  any requirement or scenario FAIL                    -> FAILED-IN-DEV, naming it
    7. Complete?                every required id PASS: a SKIP, BLOCKED or missing
                                id (regression set) is not passed                   -> NEEDS-INFO
    8. Verified?                source=pipeline and the pipeline run's test counts
                                match the comment; anything else is unverified      -> NEEDS-INFO (unverified)
    9. Otherwise                                                                     -> VERIFIED (E2E layer)

  Unverified evidence can only make the verdict redder: a reported failure blocks even from a replay
  or a local run, but only a verified pipeline run or a watched run can turn the layer green.

  Several evidence comments can report on the same build (the pipeline runs everything;
  run-pending.js runs only what had no result yet). Checks 5-9 read them merged, oldest to newest,
  newest outcome per id. The required ids are the merged ids, the story's test list (the
  `[batch-remediate | test | {id} | ...]` lines of its newest solution or test-list comment) and the
  regression set. -Json also reports `pending`: required ids with no counted pass on the latest
  build and no failure on it - what run-pending.js runs next.

.EXAMPLE
  powershell -File assets/e2e-gate.ps1 -Id 386561 -RegressionSet regression-set-386561.json
.EXAMPLE
  # Hardening check 1b: a replayed failure on the sandbox item, against 386561's merge commit
  powershell -File assets/e2e-gate.ps1 -Id 391500 -StoryCommits 9fb18e750

.OUTPUTS
  Exit 0 VERIFIED · 10 FAILED-IN-DEV · 11 BLOCKED-E2E · 12 NEEDS-INFO · 2 usage/access error.
  -Json writes the full decision as JSON to stdout.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$Id,
  [string]$RegressionSet,
  [string[]]$StoryCommits,
  [int]$DeployPipelineId = 1559,
  [int]$ReleaseDefinitionId = 1,
  [string]$RequiredStages = '^Deploy (WebApp|DataAPI|Symphony-API)$',
  [string]$Org = 'https://dev.azure.com/cwtechsolutions',
  [string]$Project = 'Symphony Cloud',
  [string]$RepoPath = '.',
  [string]$DeployBranch = 'refs/heads/develop',
  # Offline check of the decision logic: read the comments from a file ({"comments":[{id,text,modifiedDate}]},
  # the shape ADO returns) instead of the work item. Builds and releases are still read live.
  [string]$CommentsFile,
  [switch]$Json
)

$ErrorActionPreference = 'Stop'
$P = [uri]::EscapeDataString($Project)
$api = "$Org/$P/_apis"
$vsrm = ($Org -replace '^https://dev\.azure\.com/', 'https://vsrm.dev.azure.com/') + "/$P/_apis"

$decision = [ordered]@{
  id = $Id; layer = 'e2e'; verdict = ''; reasons = @(); failing = @(); notPassed = @()
  latestBuild = $null; evidence = $null; storyCommits = @()
  testList = @(); cases = $null; pending = @()
}
function Finish([string]$verdict, [string]$reason) {
  $decision.verdict = $verdict
  if ($reason) { $decision.reasons += $reason }
  if ($Json) { $decision | ConvertTo-Json -Depth 6 }
  else {
    Write-Output "E2E gate for $Id : $verdict"
    foreach ($r in $decision.reasons) { Write-Output "  - $r" }
  }
  $code = @{ 'VERIFIED' = 0; 'FAILED-IN-DEV' = 10; 'BLOCKED-E2E' = 11; 'NEEDS-INFO' = 12 }[$verdict]
  exit $code
}

try {
  $token = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv
  if (-not $token) { throw 'empty token' }
} catch { Write-Error "No Azure DevOps token (az login): $_"; exit 2 }
$H = @{ Authorization = "Bearer $token" }
function Get-Ado([string]$uri) { Invoke-RestMethod -Headers $H -Uri $uri }

Push-Location $RepoPath
try {
  # ---- 1. the story's merged commits
  if ($StoryCommits) {
    $decision.storyCommits = @($StoryCommits)
  } else {
    $wi = Get-Ado "$api/wit/workitems/$Id`?`$expand=relations&api-version=7.1"
    $prLinks = @($wi.relations | Where-Object { $_.url -match '^vstfs:///Git/PullRequestId/' })
    foreach ($l in $prLinks) {
      $parts = [uri]::UnescapeDataString(($l.url -replace '^vstfs:///Git/PullRequestId/', '')).Split('/')
      $pr = Get-Ado "$api/git/repositories/$($parts[1])/pullrequests/$($parts[2])?api-version=7.1"
      # Promotion PRs (develop->qa, qa->uat ...) are linked to stories too; only merges into the
      # branch Dev builds from are this story's deployable code.
      if ($pr.status -eq 'completed' -and $pr.lastMergeCommit -and $pr.targetRefName -eq $DeployBranch) { $decision.storyCommits += $pr.lastMergeCommit.commitId }
    }
  }
  if (-not $decision.storyCommits.Count) { Finish 'NEEDS-INFO' 'No merged PR is linked to the story, so there is no deployed code to prove.' }

  foreach ($c in $decision.storyCommits) {
    git cat-file -e "$c^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { git fetch -q origin 2>$null | Out-Null }
  }

  # ---- 2. the latest deployed Dev build, which must contain the story
  $builds = Get-Ado "$api/build/builds?definitions=$DeployPipelineId&resultFilter=succeeded&statusFilter=completed&branchName=refs/heads/develop&`$top=15&api-version=7.1"
  $latest = $null
  foreach ($b in $builds.value) {
    $rel = Get-Ado "$vsrm/release/releases?definitionId=$ReleaseDefinitionId&artifactVersionId=$($b.id)&`$expand=environments&api-version=7.1"
    $r = @($rel.value) | Sort-Object id -Descending | Select-Object -First 1
    if (-not $r) { continue }
    $stages = @($r.environments | Where-Object { $_.name -match $RequiredStages })
    if ($stages.Count -and -not ($stages | Where-Object { $_.status -ne 'succeeded' })) {
      $latest = [ordered]@{ id = [string]$b.id; number = $b.buildNumber; commit = $b.sourceVersion; release = $r.name }
      break
    }
  }
  if (-not $latest) { Finish 'NEEDS-INFO' "No recent Dev build has a release with every required stage ($RequiredStages) succeeded." }
  $decision.latestBuild = $latest
  foreach ($c in $decision.storyCommits) {
    git merge-base --is-ancestor $c $latest.commit 2>$null
    if ($LASTEXITCODE -ne 0) { Finish 'NEEDS-INFO' "Latest deployed Dev build $($latest.number) does not contain story commit $($c.Substring(0,9)) yet; wait for the deploy." }
  }

  # ---- 3. the evidence comments, and the story's test list
  $comments = if ($CommentsFile) { Get-Content $CommentsFile -Raw | ConvertFrom-Json }
              else { Get-Ado "$api/wit/workItems/$Id/comments?api-version=7.1-preview.4&`$top=200" }
  # Test list (solution or test-list comment): the newest comment that carries one wins.
  $listRx = [regex]('\[batch-remediate \| test \| ' + [regex]::Escape($Id) + ' \| case=([^ |\]]+) \|')
  $listed = @()
  foreach ($c in @($comments.comments | Sort-Object { [datetime]$_.createdDate } -Descending)) {
    $ms = $listRx.Matches([System.Net.WebUtility]::HtmlDecode([string]$c.text))
    if ($ms.Count) { $listed = @($ms | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne '-' }); break }
  }
  $decision.testList = $listed
  $baseRequired = @($listed)
  if ($RegressionSet) {
    $set = Get-Content $RegressionSet -Raw | ConvertFrom-Json
    if ([string]$set.workItem -ne $Id -and -not $StoryCommits) { Write-Warning "Regression set is for $($set.workItem), gate is for $Id." }
    $baseRequired += @($set.scenarios | ForEach-Object { $_.id })
  }
  $decision.pending = @($baseRequired | Sort-Object -Unique)
  $rx = [regex]'\[batch-remediate \| evidence \|[^\]]*\| layer=e2e \|[^\]]*\]'
  $ev = @()
  foreach ($c in $comments.comments) {
    $text = [System.Net.WebUtility]::HtmlDecode([string]$c.text)
    $m = $rx.Match($text)
    if (-not $m.Success) { continue }
    $f = @{}
    foreach ($kv in ($m.Value.Trim('[', ']') -split '\s*\|\s*')) { if ($kv -match '^([a-z]+)=(.*)$') { $f[$Matches[1]] = $Matches[2] } }
    $cases = @{}
    $cm = [regex]::Match($text, '\[batch-remediate \| e2e-cases \| run=' + [regex]::Escape([string]$f['run']) + ' \| ([^\]]*)\]')
    if ($cm.Success -and $cm.Groups[1].Value -ne 'none') {
      foreach ($pair in ($cm.Groups[1].Value -split ';\s*')) { if ($pair -match '^(.+?)=(PASS|FAIL|SKIP|BLOCKED)$') { $cases[$Matches[1]] = $Matches[2] } }
    }
    $why = ''
    $bm = [regex]::Match($text, 'BLOCKED</b> - (.+?)<br>')
    if ($bm.Success) { $why = $bm.Groups[1].Value }
    $ev += [pscustomobject]@{ commentId = $c.id; when = [datetime]$c.modifiedDate; fields = $f; cases = $cases; blockedReason = $why }
  }
  if (-not $ev.Count) { Finish 'NEEDS-INFO' 'E2E layer unverified: no post-deploy evidence comment on the story. Run the post-deploy pipeline for the latest build.' }

  # Match on the build id when the comment has one, else on the build number (older comments).
  $isLatest = {
    param($x)
    $bid = [string]$x.fields['buildid']
    if ($bid -and $bid -ne '-') { $bid -eq $latest.id } else { [string]$x.fields['build'] -eq $latest.number }
  }
  $current = @($ev | Where-Object { & $isLatest $_ } | Sort-Object when -Descending)
  $newest = @($ev | Sort-Object when -Descending)[0]

  # ---- 4. stale
  if (-not $current.Count) {
    $decision.evidence = [ordered]@{ commentId = $newest.commentId; build = $newest.fields['build']; buildid = $newest.fields['buildid']; source = $newest.fields['source']; result = $newest.fields['result'] }
    Finish 'NEEDS-INFO' "Evidence is stale: newest e2e evidence (comment $($newest.commentId)) is for build $($newest.fields['build']), the latest deployed build is $($latest.number). Re-run the post-deploy pipeline for $($latest.number)."
  }
  # Several runs can report on the same build: the pipeline runs everything, and run-pending.js
  # runs only what had no result yet. Merge them oldest to newest; the newest outcome per id wins.
  $merged = [ordered]@{}
  foreach ($x in @($current | Sort-Object when)) {
    foreach ($kv in $x.cases.GetEnumerator()) {
      $merged[$kv.Key] = [pscustomobject]@{ outcome = $kv.Value; source = [string]$x.fields['source']; commentId = $x.commentId; run = $x.fields['run'] }
    }
  }
  $counts = { param($s) $s -eq 'pipeline' -or $s -eq 'watched' }
  $e = $current[0]
  $src = [string]$e.fields['source']
  $decision.evidence = [ordered]@{ commentId = $e.commentId; run = $e.fields['run']; build = $e.fields['build']; buildid = $e.fields['buildid']; pipeline = $e.fields['pipeline']; source = $src; result = $e.fields['result']; req = $e.fields['req']; cases = $e.cases; comments = @($current | ForEach-Object { $_.commentId }) }
  $decision.cases = $merged

  $required = @(@($merged.Keys) + $baseRequired | Sort-Object -Unique)
  # Pending: required ids with no counted pass on this build. A FAIL is not pending - it is a finding,
  # and the same build is never re-run to get a different answer.
  $decision.pending = @($required | Where-Object {
      $o = $merged[$_]
      -not $o -or ($o.outcome -ne 'FAIL' -and -not ($o.outcome -eq 'PASS' -and (& $counts $o.source)))
    })

  # ---- 5. blocked: the newest run on this build ran nothing, and no run on it reported a result
  if ($e.fields['result'] -eq 'BLOCKED' -and -not $merged.Count) {
    $reason = if ($e.blockedReason) { $e.blockedReason } else { 'nothing ran (environment or sign-in)' }
    Finish 'BLOCKED-E2E' "Nothing ran on build $($latest.number): $reason (source=$src). Not a pass."
  }

  # ---- 6. failed: name every failing requirement / scenario, scenarios first
  $fails = @($merged.Keys | Where-Object { $merged[$_].outcome -eq 'FAIL' } | Sort-Object { if ($_ -like 'BR-*') { 0 } else { 1 } }, { $_ })
  $failedNoCases = @($current | Where-Object { $_.fields['result'] -eq 'FAILED' -and -not $_.cases.Count })
  if ($fails.Count -or $failedNoCases.Count) {
    $decision.failing = $fails
    $names = if ($fails.Count) { ($fails | ForEach-Object { $s = $merged[$_].source; if (& $counts $s) { $_ } else { "$_ (source=$s, unverified; a failure still blocks)" } }) -join ', ' } else { '(no case line in the comment)' }
    Finish 'FAILED-IN-DEV' "Failing on build $($latest.number): $names."
  }

  # ---- 7. complete: every required id passed
  $notPassed = @()
  foreach ($r in $required) {
    $o = $merged[$r]
    if (-not $o) { $notPassed += "$r=NOT RUN" } elseif ($o.outcome -ne 'PASS') { $notPassed += "$r=$($o.outcome)" }
  }
  if (-not $required.Count) { $notPassed += 'no requirement or scenario results in the comment' }
  if ($notPassed.Count) {
    $decision.notPassed = $notPassed
    Finish 'NEEDS-INFO' "Not passed on build $($latest.number) (a skip counts as not passed): $($notPassed -join ', ')."
  }

  # ---- 8. verified: every pass comes from a pipeline run that checks out, or a watched run
  $local = @($required | Where-Object { -not (& $counts $merged[$_].source) })
  if ($local.Count) {
    $srcs = @($local | ForEach-Object { $merged[$_].source } | Sort-Object -Unique) -join ', '
    Finish 'NEEDS-INFO' "E2E layer unverified: evidence source=$srcs does not count toward a pass ($($local -join ', ')). Run the post-deploy pipeline, or run-pending.js watched."
  }
  foreach ($pc in @($current | Where-Object { [string]$_.fields['source'] -eq 'pipeline' })) {
    if (-not @($merged.Values | Where-Object { $_.commentId -eq $pc.commentId }).Count) { continue }
    $pid_ = [string]$pc.fields['pipeline']
    if (-not $pid_ -or $pid_ -eq '-') { Finish 'NEEDS-INFO' "E2E layer unverified: comment $($pc.commentId) names no pipeline run." }
    try { $run = Get-Ado "$api/build/builds/$pid_`?api-version=7.1" } catch { Finish 'NEEDS-INFO' "E2E layer unverified: pipeline run $pid_ not found." }
    # The post-deploy pipeline names each run e2e-{Dev build number}-{run id}.
    if ([string]$run.buildNumber -notlike "e2e-$($latest.number)-*") { Finish 'NEEDS-INFO' "E2E layer unverified: pipeline run $pid_ ($($run.buildNumber)) did not test build $($latest.number)." }
    $runs = Get-Ado "$api/test/runs?buildUri=vstfs:///Build/Build/$pid_&includeRunDetails=true&api-version=7.1"
    $tot = 0; $pass = 0
    foreach ($tr in $runs.value) { $tot += [int]$tr.totalTests; $pass += [int]$tr.passedTests }
    $claimed = [string]$pc.fields['req']
    if ("$pass/$tot" -ne $claimed) { Finish 'NEEDS-INFO' "E2E layer unverified: pipeline run $pid_ Tests tab shows $pass/$tot, the comment claims $claimed." }
  }

  $srcAll = @($required | ForEach-Object { $merged[$_].source } | Sort-Object -Unique) -join ', '
  Finish 'VERIFIED' "Every required requirement and scenario passed on the latest deployed build $($latest.number) ($($required.Count) id(s), source=$srcAll). Other layers (post-deploy.md §2) are judged separately."
}
finally { Pop-Location }
