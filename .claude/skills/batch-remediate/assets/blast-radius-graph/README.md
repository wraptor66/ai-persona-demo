# blast-radius-graph

batch-remediate **L1** (call graph) and **L2** (regression-set drift). Needs only the .NET SDK
(8 or later); restores three NuGet packages on first run. Read `references/design-gate.md` §2 for
when the skill runs it.

```
dotnet run --project .claude/skills/batch-remediate/assets/blast-radius-graph -- <command> [options]
```

| Command | Does | Exit |
|---|---|---|
| `graph --id <wi> --base <ref> [--head HEAD]` | changed members from the diff → `blast-radius-<wi>.json` | 0, 2 usage, 4 failure |
| `graph --id <wi> --symbol cs:Type.Member --symbol js:name` | the same, for named members (Phase 0.25, before code exists) | |
| `regression-set --graph <file>` | the M2 proposal → `regression-set-<wi>.json` | 0 |
| `check --graph <file> --set <file>` | new scenario / caller / changed member since M2? | 0 none, **3 drift** |

Options for `graph`: `--root <dir>` (default `symphonyModernization`), `--depth <n>` (code hops,
default 6), `--max-fanin <n>` (hub limit, default 40), `--syntax-only`, `--allow-dirty`,
`--out <file>`.

## What it guarantees

- **Same inputs, same bytes.** Everything is sorted; no timestamps, no absolute paths; `hash` is
  the SHA-256 of the rest. `--head` must be checked out (the workspace reads the working tree),
  and uncommitted source under `--root` is refused unless `--allow-dirty`.
- **C# is semantic** (Roslyn over MSBuildWorkspace, every `.csproj` under the root — not the
  `.sln`, which omits projects such as AILibrary). Overloads and same-named members on other
  types are told apart. A project that will not load (or a legacy non-SDK project) is parsed
  syntax-only, matched by name and argument count, and its edges are `low` — kept, never dropped.
  If no MSBuild can be found at all, the whole run is syntax-only and `inputs.mode` says so.
- **Front end is name-based**: JS calls and handler references, `onclick=` attributes,
  `<script src>` includes, `/api/...` URLs (including `${CONST}/x` and `CONST + '/x'`) matched to
  controller routes, and Symphony's routed `methodName` / `actionname` payloads (via
  `eventmap*.json`). `medium`, or `low` when several files define the same name.
- **Hubs**: a member with more than `--max-fanin` callers is listed under `hubs` with its count
  and not expanded. M2 decides what to do about it.
- Test projects and `tests/` folders are neither changed symbols nor callers.

## Output (abridged)

```json
{
  "schema": "batch-remediate/blast-radius-graph@1",
  "workItem": "386561",
  "inputs": { "root": "...", "base": "<sha>", "head": "<sha>", "depth": 6, "maxFanIn": 40, "mode": "semantic|mixed|syntax" },
  "projects": [ { "path": "...csproj", "loaded": true, "mode": "semantic" } ],
  "changed":  [ { "key": "cs:Ns.Type.Member(string)", "lang": "cs", "change": "modified", "file": "...", "line": 12 } ],
  "scenarios": [ { "id": "BR-386561-ai-dynamicreporting", "page": "...html", "confidence": "medium",
                   "reachedBy": ["cs:..."], "paths": [ { "changed": "cs:...", "confidence": "medium", "path": ["page:...", "...", "cs:..."] } ] } ],
  "internal": [ "changed members that reach no page" ],
  "hubs": [ { "key": "cs:...", "fanIn": 212 } ],
  "nodes": [ ... ], "edges": [ { "from": "caller", "to": "callee", "via": "semantic|syntax|js-name|http-route|methodName-payload|script-include|...", "confidence": "high|medium|low" } ],
  "hash": "<sha256>"
}
```

## Self-check (hardening check 5)

`fixture/` is a tiny app: `Formatter.Format(string)` (the changed member), an overload and a
same-named method on another type (both must stay out), a controller route called from a page.
`fixture/drift/` adds a caller after M2. Steps: `references/post-deploy.md` §8.5.
