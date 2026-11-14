<#
.SYNOPSIS
  blast-radius.ps1 - consumer-impact check for a changed shared symbol (PowerShell port of
  blast-radius.sh, extended to C#).

.DESCRIPTION
  Answers: "I changed the timing / ordering / failure contract of SYMBOL. Who calls it, and does
  any caller invoke it inside a loop?"

  A caller inside an iteration depends on per-call cost. Making the call cheaper (sync -> async,
  blocking -> fire-and-forget) removes pacing the loop never declared. Cause and effect land in
  different files, so diff review cannot see it.

  Loop detection is a brace-depth heuristic, not a parser. Read every flagged site; drop false
  positives by reading the code, never by pattern.

.EXAMPLE
  pwsh assets/blast-radius.ps1 -Symbol sendUILog -Root symphonyModernization/SymphonyWebApplication/wwwroot
.EXAMPLE
  pwsh assets/blast-radius.ps1 -Symbol GetMailboxStats -Root . -Json > blast.json

.OUTPUTS
  Exit 1 if any call site is loop-nested (gate fails), 0 otherwise, 2 on bad usage.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)][string]$Symbol,
  [Parameter(Position = 1)][string]$Root = '.',
  [string[]]$Include = @('*.cs', '*.cshtml', '*.js', '*.ts', '*.html'),
  [switch]$Json
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Root)) { Write-Error "Root not found: $Root"; exit 2 }

$loopHeader = [regex]'(\bfor\s*\(|\bforeach\s*\(|\bwhile\s*\(|\bdo\s*\{|\.forEach\s*\(|\$\.each\s*\(|\.ForEach\s*\(|Parallel\.ForEach(Async)?\s*\(|\.map\s*\()'
$symbolPattern = [regex]::new('\b' + [regex]::Escape($Symbol) + '\b')

$skipDirs = @('\bin\', '\obj\', '\node_modules\', '\.git\', '/bin/', '/obj/', '/node_modules/', '/.git/')
$files = Get-ChildItem -LiteralPath $Root -Recurse -File -Include $Include -ErrorAction SilentlyContinue |
  Where-Object { $p = $_.FullName; -not ($skipDirs | Where-Object { $p.Contains($_) }) }

$sites = New-Object System.Collections.Generic.List[object]
$fileCount = 0

foreach ($f in $files) {
  $lines = [System.IO.File]::ReadAllLines($f.FullName)
  $hit = $false
  $inLoop = $false; $depth = 0; $loopStart = 0; $kind = ''; $opened = $false

  for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]

    if (-not $inLoop) {
      $m = $loopHeader.Match($line)
      if ($m.Success) {
        $inLoop = $true; $depth = 0; $loopStart = $i + 1; $opened = $false
        $kind = if ($m.Value -match '\.forEach|\.ForEach|\$\.each|Parallel\.ForEach|\.map') { 'callback' } else { 'loop' }
      }
    }

    $isDefinition = $line -match ('(function\s+|\b(public|private|protected|internal|static|async)\b[^=;(]*\s)' + [regex]::Escape($Symbol) + '\s*\(')
    if ($symbolPattern.IsMatch($line) -and -not $isDefinition) {
      $hit = $true
      $sites.Add([pscustomobject]@{
        File       = (Resolve-Path -LiteralPath $f.FullName -Relative)
        Line       = $i + 1
        LoopNested = $inLoop
        LoopKind   = if ($inLoop) { $kind } else { '' }
        LoopLine   = if ($inLoop) { $loopStart } else { 0 }
        Text       = $line.Trim().Substring(0, [Math]::Min(110, $line.Trim().Length))
      })
    }

    if ($inLoop) {
      $open = ([regex]::Matches($line, '\{')).Count
      $close = ([regex]::Matches($line, '\}')).Count
      if ($open -gt 0) { $opened = $true }
      $depth += $open - $close
      # A loop with no brace on its header line (C# single-statement body) ends after one statement.
      if (($opened -and $depth -le 0 -and ($i + 1) -gt $loopStart) -or
          (-not $opened -and ($i + 1) -gt $loopStart -and $line.TrimEnd().EndsWith(';'))) {
        $inLoop = $false
      }
    }
  }
  if ($hit) { $fileCount++ }
}

$flagged = @($sites | Where-Object { $_.LoopNested })

if ($Json) {
  [pscustomobject]@{
    symbol     = $Symbol
    root       = $Root
    callSites  = $sites.Count
    files      = $fileCount
    loopNested = $flagged.Count
    sites      = $sites
  } | ConvertTo-Json -Depth 4
} else {
  "Blast radius: $Symbol"
  "Root:         $Root"
  ''
  foreach ($s in $flagged) {
    "  {0}:{1}  (inside {2} opened at line {3})" -f $s.File, $s.Line, $s.LoopKind, $s.LoopLine
    "    $($s.Text)"
  }
  ''
  "call sites: $($sites.Count) across $fileCount file(s)"
  if ($flagged.Count -gt 0) {
    "loop-nested: $($flagged.Count) site(s)  -- REVIEW REQUIRED"
    ''
    'For each site above, answer in the REVIEW file (section 4.5):'
    "  1. Does the loop have explicit pacing that does not depend on ${Symbol}?"
    '  2. How many concurrent operations does the loop issue at realistic input size?'
    '  3. Does anything else in the loop (progress UI, completion, unblock, ordering) assume per-iteration cost?'
    'Any site that cannot answer all three gets a test or a recorded risk.'
  } else {
    'loop-nested: none'
  }
}

if ($flagged.Count -gt 0) { exit 1 } else { exit 0 }
