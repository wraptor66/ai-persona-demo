<#
.SYNOPSIS
  Lists, turns on, or turns off batch-remediate story flags (App Service settings named FF_*).

.DESCRIPTION
  Flags are App Service application settings read as environment variables
  (references/feature-flags.md). This script sets them as slot settings, so a slot swap never
  carries them, and reads the value back. It uses your Azure CLI sign-in (az login).

  Changing an app setting restarts that App Service for a few seconds.
  UAT and Prod ask you to type the environment name before changing anything.

.EXAMPLE
  .\Set-FeatureFlag.ps1 -Environment dev -List
.EXAMPLE
  .\Set-FeatureFlag.ps1 -Environment dev -Name FF_361204_MailboxStatsColumns -On -Apps web,dataapi
.EXAMPLE
  .\Set-FeatureFlag.ps1 -Environment prod -Name FF_361204_MailboxStatsColumns -Off -Apps web,dataapi
#>
[CmdletBinding(DefaultParameterSetName = 'List')]
param(
  [Parameter(Mandatory)][ValidateSet('dev', 'qa', 'uat', 'prod')][string]$Environment,
  [Parameter(ParameterSetName = 'List')][switch]$List,
  [Parameter(ParameterSetName = 'Set', Mandatory)][string]$Name,
  [Parameter(ParameterSetName = 'Set')][switch]$On,
  [Parameter(ParameterSetName = 'Set')][switch]$Off,
  [string[]]$Apps = @('web')
)
$ErrorActionPreference = 'Stop'
function Stop-With($problem, $fix) {
  Write-Host "`n  [STOP] $problem" -ForegroundColor Red
  Write-Host "         Fix: $fix`n" -ForegroundColor Yellow
  exit 1
}

# ---------------------------------------------------------------------------------------------
# App Service names per environment and role. Dev is known; fill the others once
# (or list them: az webapp list --query "[].{name:name, rg:resourceGroup}" -o table).
# ---------------------------------------------------------------------------------------------
$AppMap = @{
  dev  = @{ web = 'symphonywebdev'; dataapi = ''; api = ''; monitoring = '' }
  qa   = @{ web = '';               dataapi = ''; api = ''; monitoring = '' }
  uat  = @{ web = '';               dataapi = ''; api = ''; monitoring = '' }
  prod = @{ web = '';               dataapi = ''; api = ''; monitoring = '' }
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) { Stop-With "az is not on PATH." "Install the Azure CLI, open a new window, az login." }
$Apps = @($Apps | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLower() } | Where-Object { $_ })
if ($PSCmdlet.ParameterSetName -eq 'Set') {
  if ($Name -notmatch '^FF_[A-Za-z0-9_]+$') { Stop-With "'$Name' is not a story flag name." "Use FF_{id}_{PascalSlug}, letters, digits and _ only." }
  if ($On -eq $Off) { Stop-With "Say -On or -Off (one of them)." "Add -On or -Off." }
}

function Resolve-App([string]$role) {
  $n = $AppMap[$Environment][$role]
  if (-not $n) { Stop-With "No App Service name for '$role' in $Environment." "Fill `$AppMap at the top of this script (or pass the role your story needs)." }
  $ErrorActionPreference = 'Continue'
  $rg = az webapp list --query "[?name=='$n'].resourceGroup | [0]" -o tsv 2>$null
  if ($LASTEXITCODE -ne 0 -or -not $rg) { Stop-With "App Service '$n' not found with your sign-in." "az login (and az account set --subscription <the one with $n>), then re-run." }
  return @{ name = $n; rg = $rg }
}

if ($PSCmdlet.ParameterSetName -eq 'List') {
  $roles = @($AppMap[$Environment].Keys | Where-Object { $AppMap[$Environment][$_] })
  foreach ($role in $roles) {
    $a = Resolve-App $role
    $ErrorActionPreference = 'Continue'
    $s = az webapp config appsettings list -n $a.name -g $a.rg -o json 2>$null | ConvertFrom-Json
    $flags = @($s | Where-Object { $_.name -like 'FF_*' -or $_.name -like 'Enable*' })
    Write-Host "`n  $Environment / $role ($($a.name))"
    if ($flags.Count -eq 0) { Write-Host "    (no flags set - every story flag is OFF here)" }
    foreach ($f in $flags | Sort-Object name) { Write-Host ("    {0,-48} {1,-6} {2}" -f $f.name, $f.value, $(if ($f.slotSetting) { 'slot' } else { 'NOT slot-sticky' })) }
  }
  Write-Host ""
  exit 0
}

if ($Environment -in @('uat', 'prod')) {
  $typed = Read-Host "  You are changing $Name in $($Environment.ToUpper()). Type the environment name to continue"
  if ($typed -ne $Environment) { Stop-With "Not confirmed." "Re-run and type '$Environment' when asked." }
}

$value = if ($On) { 'true' } else { 'false' }
foreach ($role in $Apps) {
  $a = Resolve-App $role
  $ErrorActionPreference = 'Continue'
  az webapp config appsettings set -n $a.name -g $a.rg --slot-settings "$Name=$value" -o none 2>$null
  if ($LASTEXITCODE -ne 0) { Stop-With "Could not set $Name on $($a.name)." "Check you have Contributor (or Website Contributor) on $($a.rg)." }
  $read = az webapp config appsettings list -n $a.name -g $a.rg --query "[?name=='$Name'] | [0]" -o json 2>$null | ConvertFrom-Json
  $ok = ($read -and $read.value -eq $value)
  $color = if ($ok) { 'Green' } else { 'Red' }
  Write-Host ("  {0} {1} = {2} on {3} ({4}){5}" -f $(if ($ok) { '[done]' } else { '[FAIL]' }), $Name, $read.value, $a.name, $Environment, $(if ($read.slotSetting) { ', slot setting' } else { '' })) -ForegroundColor $color
  if (-not $ok) { exit 1 }
}
Write-Host "  The App Service restarts to pick it up (a few seconds)."
