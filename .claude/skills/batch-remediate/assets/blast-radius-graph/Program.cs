// blast-radius-graph - batch-remediate L1 (call graph) and L2 (regression-set drift).
//
//   graph           changed symbols -> callers -> pages -> scenarios, as blast-radius-{id}.json
//   regression-set  the M2 proposal generated from a graph: regression-set-{id}.json
//   check           a fresh graph against the accepted regression set; exit 3 when it has drifted
//
// Run from anywhere inside the repo (the repo root is found with git):
//   dotnet run --project .claude/skills/batch-remediate/assets/blast-radius-graph -- graph --id 386561 --base 9fb18e750^1 --head 9fb18e750
//   dotnet run --project ... -- graph --id 386561 --symbol cs:FilterTreeEvaluator.Evaluate --symbol js:applyFilters
//   dotnet run --project ... -- regression-set --graph blast-radius-386561.json
//   dotnet run --project ... -- check --graph blast-radius-386561.json --set regression-set-386561.json
//
// Exit codes: 0 ok · 2 usage or input error · 3 drift (M2 reopens) · 4 analysis failed.
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using BlastRadiusGraph;

var argv = args.ToList();
string? Opt(string name) { var i = argv.IndexOf(name); return i >= 0 && i + 1 < argv.Count ? argv[i + 1] : null; }
List<string> Opts(string name) => argv.Select((a, i) => (a, i)).Where(x => x.a == name && x.i + 1 < argv.Count).Select(x => argv[x.i + 1]).ToList();
bool Flag(string name) => argv.Contains(name);

var cmd = argv.FirstOrDefault() ?? "";
try
{
    switch (cmd)
    {
        case "graph": return await GraphCommand();
        case "regression-set": return RegressionSetCommand();
        case "check": return CheckCommand();
        default:
            Console.Error.WriteLine("usage: blast-radius-graph graph|regression-set|check [options]  (see Program.cs header or README.md)");
            return 2;
    }
}
catch (UsageException ex) { Console.Error.WriteLine(ex.Message); return 2; }
catch (Exception ex) { Console.Error.WriteLine($"analysis failed: {ex}"); return 4; }

// ------------------------------------------------------------------------------------------ graph

async Task<int> GraphCommand()
{
    var id = Opt("--id") ?? throw new UsageException("--id <work item> is required");
    var repo = Git.TopLevel(Directory.GetCurrentDirectory());
    var root = (Opt("--root") ?? "symphonyModernization").Replace('\\', '/').Trim('/');
    var depth = int.Parse(Opt("--depth") ?? "6");
    var maxFanIn = int.Parse(Opt("--max-fanin") ?? "40");
    var symbols = Opts("--symbol");
    var syntaxOnly = Flag("--syntax-only");
    var git = new Git(repo);
    if (!Directory.Exists(Path.Combine(repo, root))) throw new UsageException($"--root {root} not found under {repo}");

    var headSha = git.Sha(Opt("--head") ?? "HEAD");
    var baseRef = Opt("--base");
    if (baseRef == null && symbols.Count == 0) throw new UsageException("give --base <ref> (changed symbols from the diff) or one or more --symbol");
    var baseSha = baseRef == null ? "" : git.Sha(baseRef);

    // The workspace reads the working tree, so the working tree must be the head being analyzed.
    if (git.Sha("HEAD") != headSha) throw new UsageException($"--head {headSha[..9]} is not checked out here (HEAD is {git.Sha("HEAD")[..9]}). Run from a worktree at that commit.");
    // Build output (bin/, obj/ - the workspace's own design-time build writes obj/) is not source.
    var dirty = git.Run("status", "--porcelain", "--", root).Split('\n').Select(l => l.TrimEnd('\r'))
        .Where(l => l.Length > 3 && !Regex.IsMatch(l[3..].Replace('\\', '/'), @"(^|/)(bin|obj)(/|$)")).ToList();
    if (!Flag("--allow-dirty") && dirty.Count > 0)
        throw new UsageException($"{root} has uncommitted changes ({dirty[0][3..]}{(dirty.Count > 1 ? $" and {dirty.Count - 1} more" : "")}); the graph would not be reproducible. Commit, stash, or pass --allow-dirty.");

    if (!syntaxOnly)
    {
        try { MSBuildLocator.RegisterDefaults(); }
        catch (InvalidOperationException ex)
        {
            // No SDK the runtime can load: keep going syntax-only, every C# edge marked low.
            Console.Error.WriteLine($"No usable MSBuild ({ex.Message.Split('\n')[0]}); falling back to syntax-only.");
            syntaxOnly = true;
        }
    }
    Console.Error.WriteLine($"Loading C# projects under {root} ({(syntaxOnly ? "syntax only" : "semantic")}) ...");
    var cs = await CSharpIndex.LoadAsync(repo, root, syntaxOnly);
    Console.Error.WriteLine($"  {cs.Projects.Count(p => p.Loaded)} of {cs.Projects.Count} projects semantic; {cs.SyntaxOnly.Count} files syntax-only");
    var js = new JsIndex(repo, root, Opt("--web-root") ?? "wwwroot");
    var eventMap = LoadEventMap(repo, root);

    var g = new GraphDoc { WorkItem = id };
    var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
    void AddNode(Node n) => nodes.TryAdd(n.Key, n);

    // ---- changed symbols
    var changed = new List<ChangedSymbol>();
    if (baseSha.Length > 0)
    {
        foreach (var fc in git.Diff(baseSha, headSha, root))
        {
            var newRel = fc.NewPath; var oldRel = fc.OldPath;
            var any = newRel ?? oldRel!;
            if (CSharpIndex.IsTestPath(any) || JsIndex.Excluded(any)) continue;
            if (any.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var headNames = new HashSet<string>();
                if (newRel != null)
                {
                    var text = File.ReadAllText(Path.Combine(repo, newRel));
                    foreach (var (k, kind, line) in await cs.MembersTouchingAsync(newRel, text, fc.NewLines))
                    {
                        changed.Add(new(k, "cs", oldRel == null ? "added" : "modified", newRel, line));
                        AddNode(new Node(k, kind, newRel, line, cs.RouteOf(k)));
                    }
                    var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text);
                    headNames = CSharpIndex.Members(tree.GetRoot()).Select(m => CSharpIndex.MemberName(CSharpIndex.SyntaxKey(m))).ToHashSet();
                }
                if (oldRel != null && git.Show(baseSha, oldRel) is { } oldText)
                {
                    var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(oldText);
                    foreach (var m in CSharpIndex.Members(tree.GetRoot()))
                    {
                        var (s, e) = CSharpIndex.Lines(m);
                        var k = CSharpIndex.SyntaxKey(m);
                        if (!fc.OldLines.Any(l => l >= s && l <= e) || headNames.Contains(CSharpIndex.MemberName(k))) continue;
                        changed.Add(new(k, "cs", "removed", oldRel, s));
                        AddNode(new Node(k, "cs-method", oldRel, s));
                    }
                }
            }
            else if (Regex.IsMatch(any, @"\.(js|html?|cshtml)$", RegexOptions.IgnoreCase))
            {
                var headFns = newRel != null && js.Functions.TryGetValue(newRel, out var hf) ? hf : new List<JsFunction>();
                if (newRel != null)
                {
                    bool fileLevel = false;
                    foreach (var line in fc.NewLines.OrderBy(l => l))
                    {
                        var f = js.Enclosing(newRel, line);
                        if (f == null) { fileLevel = true; continue; }
                        var k = JsKey(f);
                        if (changed.Any(c => c.Key == k)) continue;
                        changed.Add(new(k, "js", oldRel == null ? "added" : "modified", newRel, f.Start));
                        AddNode(new Node(k, "js-function", newRel, f.Start));
                    }
                    if (fileLevel)
                    {
                        var k = FileKey(newRel);
                        changed.Add(new(k, "js", oldRel == null ? "added" : "modified", newRel, 0));
                        AddNode(new Node(k, JsIndex.IsPage(newRel) ? "page" : "script", newRel, 0));
                    }
                }
                if (oldRel != null && git.Show(baseSha, oldRel) is { } oldText)
                    foreach (var f in JsIndex.FunctionsOf(oldRel, oldText))
                    {
                        if (!fc.OldLines.Any(l => l >= f.Start && l <= f.End)) continue;
                        if (headFns.Any(h => h.Name == f.Name) || js.DefinitionCount(f.Name) > 0) continue; // moved or still defined
                        var k = JsKey(f);
                        changed.Add(new(k, "js", "removed", oldRel, f.Start));
                        AddNode(new Node(k, "js-function", oldRel, f.Start));
                    }
            }
        }
    }
    foreach (var s in symbols)
    {
        if (s.StartsWith("js:"))
            foreach (var f in js.Functions.Values.SelectMany(x => x).Where(f => f.Name == s[3..]))
            {
                changed.Add(new(JsKey(f), "js", "named", f.File, f.Start));
                AddNode(new Node(JsKey(f), "js-function", f.File, f.Start));
            }
        else if (s.StartsWith("cs:"))
            foreach (var (k, kind, file, line) in await cs.FindDeclaredAsync(s[3..]))
            {
                changed.Add(new(k, "cs", "named", file, line));
                AddNode(new Node(k, kind, file, line, cs.RouteOf(k)));
            }
        else throw new UsageException($"--symbol {s}: prefix with cs: (Type.Member) or js: (functionName)");
    }
    changed = changed.GroupBy(c => c.Key).Select(gr => gr.First()).ToList();
    if (changed.Count == 0) Console.Error.WriteLine("No changed shared symbols found.");

    // ---- callers, cached per node
    var cache = new Dictionary<string, List<(Node Caller, Edge Edge)>>(StringComparer.Ordinal);
    async Task<List<(Node Caller, Edge Edge)>> CallersOf(Node n)
    {
        if (cache.TryGetValue(n.Key, out var hit)) return hit;
        var list = new List<(Node Caller, Edge Edge)>();
        switch (n.Kind)
        {
            case "cs-method" or "cs-ctor" or "cs-property" or "cs-action":
                list.AddRange(await cs.CallersAsync(n.Key));
                if (cs.IsPublicMethod(n.Key))
                    foreach (var (file, line, via) in js.PayloadSites(CSharpIndex.MemberName(n.Key), eventMap))
                    {
                        var (caller, _) = CallerAt(js, file, line);
                        list.Add((caller, new Edge(caller.Key, n.Key, file, line, via, "medium")));
                    }
                if (n.Route != null) list.AddRange(RouteCallers(n, js));
                break;
            case "js-function":
                var name = n.Key[(n.Key.IndexOf('#') + 1)..];
                foreach (var site in js.CallSites(name))
                {
                    // Only a call site that can see this definition (browser scoping) calls it.
                    var targets = js.Targets(name, site);
                    if (!targets.Any(t => t.File == n.File)) continue;
                    var conf = targets.Count == 1 ? "medium" : "low";
                    var (caller, _) = CallerAt(js, site.File, site.Line);
                    if (caller.Key == n.Key) continue;
                    list.Add((caller, new Edge(caller.Key, n.Key, site.File, site.Line, "js-name", conf)));
                }
                // A function in a page belongs to that page; one in a shared script reaches the pages
                // that load it even when no static call was found (dynamic wiring) - low confidence.
                var container = new Node(FileKey(n.File), JsIndex.IsPage(n.File) ? "page" : "script", n.File, 0);
                list.Add((container, new Edge(container.Key, n.Key, n.File, n.Line, JsIndex.IsPage(n.File) ? "defined-in-page" : "defined-in-script", JsIndex.IsPage(n.File) ? "high" : "low")));
                break;
            case "script":
                if (js.PagesIncluding.TryGetValue(Path.GetFileName(n.File), out var pages))
                    foreach (var p in pages.OrderBy(p => p, StringComparer.Ordinal))
                        list.Add((new Node(FileKey(p), "page", p, 0), new Edge(FileKey(p), n.Key, p, 0, "script-include", "medium")));
                break;
        }
        cache[n.Key] = list;
        return list;
    }

    // ---- walk up from each changed symbol; depth counts code hops only
    var edges = new HashSet<Edge>();
    var scenarios = new Dictionary<string, (string Page, List<ScenarioPath> Paths)>(StringComparer.Ordinal);
    var internalOnly = new List<string>();
    var hubs = new Dictionary<string, int>(StringComparer.Ordinal);
    int visited = 0, ci = 0;
    foreach (var c in changed.OrderBy(c => c.Key, StringComparer.Ordinal))
    {
        Console.Error.WriteLine($"  [{++ci}/{changed.Count}] {c.Key}");
        var start = nodes[c.Key];
        var parent = new Dictionary<string, (string Child, Edge Edge)>(StringComparer.Ordinal);
        var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [start.Key] = 0 };
        var q = new Queue<Node>(); q.Enqueue(start);
        var reachedPages = new List<string>();
        if (start.Kind == "page") reachedPages.Add(start.Key);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (dist[cur.Key] >= depth) continue;
            if (++visited % 200 == 0) Console.Error.WriteLine($"      {visited} nodes walked, {nodes.Count} known, {hubs.Count} hub(s)");
            var callers = await CallersOf(cur);
            // A hub (more callers than --max-fanin) is recorded and not expanded: walking through it
            // would put most of the application in the blast radius and tell M2 nothing. M2 sees it
            // listed with its caller count; design-gate.md §3 rule 3 already steers such changes to C.
            var fanIn = callers.Select(x => x.Caller.Key).Distinct().Count();
            if (fanIn > maxFanIn && cur.Kind is not ("script" or "page"))
            {
                hubs[cur.Key] = fanIn;
                continue;
            }
            foreach (var (caller, edge) in callers.OrderBy(x => x.Edge.From, StringComparer.Ordinal).ThenBy(x => x.Edge.File, StringComparer.Ordinal).ThenBy(x => x.Edge.Line))
            {
                edges.Add(edge);
                AddNode(caller);
                if (dist.ContainsKey(caller.Key)) continue;
                var hop = edge.Via is "script-include" or "defined-in-page" or "defined-in-script" ? 0 : 1;
                dist[caller.Key] = dist[cur.Key] + hop;
                parent[caller.Key] = (cur.Key, edge);
                if (caller.Kind == "page") reachedPages.Add(caller.Key);
                q.Enqueue(caller);
            }
        }
        if (reachedPages.Count == 0) { internalOnly.Add(c.Key); continue; }
        foreach (var pk in reachedPages)
        {
            var path = new List<string> { pk };
            var conf = "high";
            var k = pk;
            while (parent.TryGetValue(k, out var p)) { conf = Canonical.Min(conf, p.Edge.Confidence); path.Add(p.Child); k = p.Child; }
            var page = nodes[pk].File;
            var sid = ScenarioId(id, page);
            if (!scenarios.TryGetValue(sid, out var sc)) scenarios[sid] = sc = (page, new List<ScenarioPath>());
            sc.Paths.Add(new ScenarioPath(c.Key, conf, path));
        }
    }

    g.Inputs = new Inputs(root, baseSha, headSha, depth, maxFanIn, syntaxOnly ? "syntax" : cs.Projects.All(p => p.Loaded) ? "semantic" : "mixed",
        symbols.OrderBy(s => s, StringComparer.Ordinal).ToList());
    g.Projects = cs.Projects;
    g.Changed = changed;
    g.Internal = internalOnly;
    g.Hubs = hubs.Select(kv => new Hub(kv.Key, kv.Value)).ToList();
    g.Nodes = nodes.Values.ToList();
    g.Edges = edges.ToList();
    g.Scenarios = scenarios.Select(kv => new Scenario(kv.Key, kv.Value.Page,
            kv.Value.Paths.Select(p => p.Confidence).Aggregate("low", Canonical.Max),
            kv.Value.Paths.Select(p => p.Changed).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
            kv.Value.Paths.OrderBy(p => p.Changed, StringComparer.Ordinal).ToList()))
        .ToList();

    var json = Canonical.Finish(g);
    var outPath = Opt("--out") ?? $"blast-radius-{id}.json";
    File.WriteAllText(outPath, json);
    Console.Error.WriteLine($"{changed.Count} changed symbol(s), {g.Scenarios.Count} scenario(s), {g.Internal.Count} internal-only, {g.Edges.Count} edges -> {outPath}");
    Console.Error.WriteLine($"hash {g.Hash}");
    foreach (var s in g.Scenarios) Console.Error.WriteLine($"  {s.Id,-60} {s.Confidence,-6} {s.Page}");
    return 0;
}


static string JsKey(JsFunction f) => $"js:{f.File}#{f.Name}";
static string FileKey(string rel) => (JsIndex.IsPage(rel) ? "page:" : "script:") + rel;

static (Node Node, int Line) CallerAt(JsIndex js, string file, int line)
{
    var f = js.Enclosing(file, line);
    return f != null
        ? (new Node(JsKey(f), "js-function", file, f.Start), line)
        : (new Node(FileKey(file), JsIndex.IsPage(file) ? "page" : "script", file, 0), line);
}

/// <summary>Front-end code whose /api/ URL matches the action's route template.</summary>
static IEnumerable<(Node, Edge)> RouteCallers(Node n, JsIndex js)
{
    var rx = new Regex("^" + Regex.Replace(Regex.Escape(n.Route!.ToLowerInvariant()), @"\\\{[^}]*}", "[^/]+") + "$");
    foreach (var (file, line, path) in js.ApiUrls)
    {
        if (!rx.IsMatch(path.ToLowerInvariant())) continue;
        var (caller, _) = CallerAt(js, file, line);
        yield return (caller, new Edge(caller.Key, n.Key, file, line, "http-route", "medium"));
    }
}

static Dictionary<string, string> LoadEventMap(string repo, string root)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var f in Directory.EnumerateFiles(Path.Combine(repo, root), "eventmap*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
    {
        if (f.Replace('\\', '/').Contains("/bin/") || f.Replace('\\', '/').Contains("/obj/")) continue;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : new List<JsonElement> { doc.RootElement };
            foreach (var it in items)
                if (it.TryGetProperty("definitions", out var defs) && defs.ValueKind == JsonValueKind.Object)
                    foreach (var d in defs.EnumerateObject())
                        if (d.Value.ValueKind == JsonValueKind.Object && d.Value.TryGetProperty("methodName", out var mn) && mn.ValueKind == JsonValueKind.Object
                            && mn.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String)
                            map.TryAdd(d.Name, tag.GetString()!);
        }
        catch (JsonException) { /* not an event map */ }
    }
    return map;
}

/// <summary>BR-{id}-{page}: derived from the page path only, so it never shifts when other scenarios
/// appear or disappear.</summary>
static string ScenarioId(string id, string page)
{
    var p = page.Replace('\\', '/');
    var w = p.IndexOf("wwwroot/", StringComparison.OrdinalIgnoreCase);
    if (w >= 0) p = p[(w + "wwwroot/".Length)..];
    foreach (var prefix in new[] { "pages/componentpages/", "pages/" })
        if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { p = p[prefix.Length..]; break; }
    p = Regex.Replace(Path.ChangeExtension(p, null)!.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
    return $"BR-{id}-{p}";
}

// ------------------------------------------------------------------------------- regression-set

int RegressionSetCommand()
{
    var graphPath = Opt("--graph") ?? throw new UsageException("--graph <blast-radius-{id}.json> is required");
    var g = Canonical.Deserialize<GraphDoc>(File.ReadAllText(graphPath));
    var changedKeys = g.Changed.Select(c => c.Key).ToHashSet();
    var set = new RegressionSet
    {
        WorkItem = g.WorkItem, GraphHash = g.Hash, Base = g.Inputs.Base, Head = g.Inputs.Head,
        Changed = changedKeys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
        Scenarios = g.Scenarios.Select(s => new ScenarioRef(s.Id, s.Page, s.Confidence)).OrderBy(s => s.Id, StringComparer.Ordinal).ToList(),
        Callers = g.Nodes.Where(n => !changedKeys.Contains(n.Key)).Select(n => n.Key).OrderBy(k => k, StringComparer.Ordinal).ToList()
    };
    var outPath = Opt("--out") ?? $"regression-set-{g.WorkItem}.json";
    File.WriteAllText(outPath, Canonical.Serialize(set));
    Console.Error.WriteLine($"{set.Scenarios.Count} scenario(s), {set.Callers.Count} caller node(s) -> {outPath}");
    Console.Error.WriteLine("Move a scenario to \"excluded\" with a reason only by decision at M2; never delete it silently.");
    return 0;
}

// ----------------------------------------------------------------------------------------- check

int CheckCommand()
{
    var g = Canonical.Deserialize<GraphDoc>(File.ReadAllText(Opt("--graph") ?? throw new UsageException("--graph is required")));
    var set = Canonical.Deserialize<RegressionSet>(File.ReadAllText(Opt("--set") ?? throw new UsageException("--set is required")));
    if (g.WorkItem != set.WorkItem) throw new UsageException($"graph is for {g.WorkItem}, set is for {set.WorkItem}");

    var agreed = set.Scenarios.Select(s => s.Id).Concat(set.Excluded.Select(e => e.Id)).ToHashSet();
    var changedKeys = g.Changed.Select(c => c.Key).ToHashSet();
    var newScenarios = g.Scenarios.Where(s => !agreed.Contains(s.Id)).ToList();
    var knownCallers = set.Callers.ToHashSet();
    var newCallers = g.Nodes.Where(n => !changedKeys.Contains(n.Key) && !knownCallers.Contains(n.Key)).ToList();
    var newChanged = changedKeys.Except(set.Changed).OrderBy(k => k, StringComparer.Ordinal).ToList();
    var gone = set.Scenarios.Where(s => g.Scenarios.All(x => x.Id != s.Id)).ToList();

    Console.WriteLine($"Regression-set check for {g.WorkItem}: graph {g.Hash[..12]} vs agreed {set.GraphHash[..Math.Min(12, set.GraphHash.Length)]}");
    foreach (var s in newScenarios) Console.WriteLine($"  NEW SCENARIO   {s.Id}  ({s.Page}, {s.Confidence}) reached by {string.Join(", ", s.ReachedBy)}");
    foreach (var n in newCallers.OrderBy(n => n.Key, StringComparer.Ordinal)) Console.WriteLine($"  NEW CALLER     {n.Key}  ({n.File}:{n.Line})");
    foreach (var k in newChanged) Console.WriteLine($"  NEW CHANGE     {k}");
    foreach (var s in gone) Console.WriteLine($"  no longer reached (informational): {s.Id}");

    if (newScenarios.Count + newCallers.Count + newChanged.Count == 0)
    {
        Console.WriteLine("No drift: every caller and scenario is inside the agreed regression set.");
        return 0;
    }
    Console.WriteLine("DRIFT: the blast radius grew after M2 was agreed. M2 reopens (design-gate.md, section 2.4).");
    return 3;
}

sealed class UsageException(string message) : Exception(message);
