# Feature flags — every new story ships behind one

Read in `research` (Phase 0.25, the flag is part of the design), `execute` (Phase 1, it is built),
`tests` (both states are tested) and `results` (its state is checked per environment). Since 3.1
**every story gets a flag**, so any change can be turned off in any environment without a deploy,
and the `execute` PR can merge to Dev for the smoke test while the change stays dark everywhere else.

## 1. The mechanism Symphony already uses

Measured on `develop` (2026-11-09): flags are **App Service application settings**, read as
environment variables, never from ProjectSettings / Key Vault / Table Storage, so they flip per
environment without touching the settings document.

- `HibpPasswordCheckClient.IsHibpPasswordCheckEnabled()` reads `EnableHibpPasswordCheck`
  (`SharedClassLibrary/Services/HibpPasswordCheckClient.cs:~204`), true for `true`, `1`, `on`.
- `DataProcessing.IsAlternateManagerTempMfaBypassEnabled` uses the same convention.
- The Dev, QA, UAT and Prod releases set **no** app settings on deploy, so a setting survives every
  release (checked on the release definitions, 2026-11-09).

**An existing mechanism wins** (`design-gate.md` §1): use this one. Never add Microsoft
FeatureManagement, App Configuration, or a settings-table toggle beside it.

## 2. The shared reader

Each existing flag has its own private helper. The **first story that adds a flag** also adds the
shared reader, once, and every later flag uses it:

```csharp
// symphonyModernization/SharedClassLibrary/FeatureFlags/FeatureFlags.cs
public static class FeatureFlags
{
    // App Service application setting, read as an environment variable. Absent or anything other
    // than true / 1 / on (any case) is OFF: a missing setting fails safe.
    public static bool IsEnabled(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(v) &&
               (v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("on", StringComparison.OrdinalIgnoreCase));
    }
}
```

Check `develop` for it first (`FeatureFlags.IsEnabled`); it may already exist. Do not rewrite the two
older helpers in a story's PR: record a refactor-log entry to move them onto the shared reader.
Unit tests set the variable with `Environment.SetEnvironmentVariable` and clear it in cleanup.

## 3. Naming

`FF_{id}_{PascalSlug}` — e.g. `FF_361204_MailboxStatsColumns`.

- Letters, digits and `_` only: valid as an App Service setting name and an environment variable
  on Windows and Linux.
- The `FF_` prefix lets `assets/Set-FeatureFlag.ps1 -List` show every story flag in an environment.
- One flag per story. A PR series shares the story's flag; a slice that must be separately
  switchable gets `FF_{id}_{Slug}_{Part}`, decided at M3.

## 4. Where the check goes

- **Server: once, at the entry point** — the controller action, the `DataProcessing` method that
  starts the workflow, the endpoint the page calls, or the DI choice of implementation. Never inside
  business logic, data access, validation, or authorization (`standards.md`).
- **Off = exactly the pre-change behavior.** Not a variant, not "mostly the same".
- **Client (jQuery pages):** the browser cannot read server settings. The page asks the server, via
  the call it already makes (`parent.GetFromServer`, which carries `CorrelationID`), or the gated
  endpoint answers "not enabled" and the page shows the old UI. Hiding a button alone is cosmetic;
  the server check is what protects the feature.
- **Which App Services:** the setting must be on **every** App Service that runs the gated code.
  `SharedClassLibrary` runs in more than one (SymphonyWebApplication, DataAPI, Symphony-api,
  EnterpriseMonitoring). Name the services in the research comment, and `Set-FeatureFlag.ps1 -Apps`
  sets them together.

## 5. Lifecycle — who turns it on where

| Environment | State | When, and by whom |
|---|---|---|
| committed code | **off** (setting absent) | always; nothing is committed that turns a flag on |
| Dev | **on** | after the `execute` PR merges and deploys; the developer runs the flip (`execute` prints the command; the skill may run it on Dev after asking) |
| QA | off, then **on** | when `results` reports the story green on Dev and the tester is ready |
| UAT | off, then on | release decision |
| Prod | **off** | release decision; it is also the kill switch: turning it off restores the old behavior without a deploy |

- Settings are **slot settings** (sticky), so a slot swap never carries a Dev or staging value into
  production. The webapp04 staging slot runs as Production: never set a flag on it by accident.
- Changing an app setting restarts that App Service (seconds of interruption). Do it outside test
  runs; on QA tell the tester first.
- The skill itself only ever flips a flag on **Dev**, and only after the developer says yes. QA,
  UAT and Prod are flipped by a person.

## 6. What each mode does with it

- **research** — the flag is part of the design, not a question: M2 shows its name, the App
  Services it lives on, and the entry point(s). The `research` comment carries `FLAG:` with the flip
  command. The only exemption is a change that **cannot** be gated (a schema-only SQL change, a
  build or pipeline file): the developer confirms it at M2 and the reason goes on the ticket.
- **execute** — builds the check and the shared reader if missing; the PR is published (the change
  is dark by default), merges, deploys; the handoff gives the Dev flip and the smoke-test checklist.
- **tests** — every requirement's tests run **flag on**; one test per entry point proves **flag off
  = old behavior** (`DOD-flag-off`). The browser spec turns nothing on itself: it checks the flag is
  on in Dev first and reports `NOT RUN (flag off)` otherwise.
- **results** — reads the flag's value in Dev (and QA, when promotion is near) with
  `Set-FeatureFlag.ps1 -List`. A layer run with the flag off proves nothing. Promotion to QA lists
  the flag and who turns it on there.
- **Removal** — `DOD-flag-removal` (external, owner the developer, after acceptance) and a refactor
  log entry: delete the check, the off path, and the setting in every environment.

## 7. Turning flags on and off — `assets/Set-FeatureFlag.ps1`

```powershell
# what is set where
.\Set-FeatureFlag.ps1 -Environment dev -List
# turn a story's flag on in Dev on the web app and the data API
.\Set-FeatureFlag.ps1 -Environment dev -Name FF_361204_MailboxStatsColumns -On -Apps web,dataapi
# off again (the kill switch)
.\Set-FeatureFlag.ps1 -Environment prod -Name FF_361204_MailboxStatsColumns -Off -Apps web,dataapi
```

It uses your `az login`, sets the value as a slot setting, refuses names without the `FF_` prefix,
asks you to type the environment name before touching UAT or Prod, and prints the value it read
back. App Service names per environment are in `config.symphony.md` (`FLAG_APPS_*`).
