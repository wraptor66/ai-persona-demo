// C# side of the graph. Semantic (Roslyn SemanticModel over MSBuildWorkspace) wherever a project loads,
// so overloads and same-named members resolve to the right declaration. Files in a project that will
// not load are parsed syntax-only and their edges are marked low confidence - kept, never dropped.
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace BlastRadiusGraph;

public sealed class CSharpIndex
{
    readonly string _repo;
    public Solution? Solution;
    public readonly List<ProjectLoad> Projects = new();
    /// <summary>Every .cs file that is not in a loaded project (rel path -> syntax tree).</summary>
    public readonly Dictionary<string, SyntaxTree> SyntaxOnly = new(StringComparer.Ordinal);
    readonly Dictionary<string, Document> _docs = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, ISymbol> Symbols = new(StringComparer.Ordinal);

    static readonly SymbolDisplayFormat KeyFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    CSharpIndex(string repo) => _repo = repo;

    public static bool IsTestPath(string rel) =>
        Regex.IsMatch(rel, @"(^|/)[^/]*(\.Tests|Test|Tests|Harness|Simulation)[^/]*/", RegexOptions.IgnoreCase)
        || rel.Contains("/tests/", StringComparison.OrdinalIgnoreCase);

    string Rel(string abs) => Path.GetRelativePath(_repo, abs).Replace('\\', '/');

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<CSharpIndex> LoadAsync(string repo, string root, bool syntaxOnly)
    {
        var ix = new CSharpIndex(repo);
        var abs = Path.Combine(repo, root);
        bool Skip(string p) { var s = p.Replace('\\', '/').ToLowerInvariant(); return s.Contains("/bin/") || s.Contains("/obj/") || s.Contains("/node_modules/") || s.Contains("/.vs/"); }
        var csprojs = Directory.EnumerateFiles(abs, "*.csproj", SearchOption.AllDirectories).Where(p => !Skip(p))
            .OrderBy(p => p, StringComparer.Ordinal).ToList();

        if (!syntaxOnly)
        {
            var failures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ws = MSBuildWorkspace.Create(new Dictionary<string, string> { ["Configuration"] = "Debug" });
            ws.RegisterWorkspaceFailedHandler(e =>
            {
                if (e.Diagnostic.Kind != WorkspaceDiagnosticKind.Failure) return;
                var m = Regex.Match(e.Diagnostic.Message, @"[A-Za-z]:\\[^']+?\.csproj");
                var key = m.Success ? Path.GetFullPath(m.Value) : "";
                if (key.Length > 0 && !failures.ContainsKey(key)) failures[key] = OneLine(e.Diagnostic.Message);
            });
            foreach (var p in csprojs)
            {
                if (IsLegacyProject(p)) { ix.Projects.Add(new(ix.Rel(p), false, "syntax", "legacy (non-SDK) project")); continue; }
                try
                {
                    if (ws.CurrentSolution.Projects.Any(x => string.Equals(x.FilePath, p, StringComparison.OrdinalIgnoreCase))) continue;
                    await ws.OpenProjectAsync(p);
                }
                catch (Exception ex) { failures[Path.GetFullPath(p)] = OneLine(ex.Message); }
            }
            ix.Solution = ws.CurrentSolution;
            var loaded = ix.Solution.Projects.Where(x => x.FilePath != null && x.Documents.Any())
                .Select(x => Path.GetFullPath(x.FilePath!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var p in csprojs)
            {
                if (ix.Projects.Any(x => x.Path == ix.Rel(p))) continue;
                var ok = loaded.Contains(Path.GetFullPath(p));
                // A load warning (e.g. an unresolved package) still yields a usable semantic model for
                // source symbols; only a project that produced no documents falls back to syntax.
                ix.Projects.Add(new(ix.Rel(p), ok, ok ? "semantic" : "syntax", ok ? null : failures.GetValueOrDefault(Path.GetFullPath(p)) ?? "no documents loaded"));
            }
            foreach (var d in ix.Solution.Projects.SelectMany(x => x.Documents))
                if (d.FilePath != null) ix._docs.TryAdd(ix.Rel(d.FilePath), d);
        }
        else ix.Projects.AddRange(csprojs.Select(p => new ProjectLoad(ix.Rel(p), false, "syntax", "--syntax-only")));

        foreach (var f in Directory.EnumerateFiles(abs, "*.cs", SearchOption.AllDirectories).Where(f => !Skip(f)).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = ix.Rel(f);
            if (ix._docs.ContainsKey(rel) || IsTestPath(rel)) continue;
            ix.SyntaxOnly[rel] = CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: rel);
        }
        return ix;
    }

    static bool IsLegacyProject(string csproj)
    {
        using var r = new StreamReader(csproj);
        for (int i = 0; i < 5 && r.ReadLine() is { } l; i++) if (l.Contains("Sdk=\"")) return false;
        return true;
    }

    static string OneLine(string s) { s = Regex.Replace(s, @"\s+", " ").Trim(); return s.Length > 200 ? s[..200] : s; }

    public bool IsSemantic(string rel) => _docs.ContainsKey(rel);

    // ------------------------------------------------------------ declarations

    public static IEnumerable<MemberDeclarationSyntax> Members(SyntaxNode root) =>
        root.DescendantNodes().OfType<MemberDeclarationSyntax>()
            .Where(m => m is BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax);

    public static (int Start, int End) Lines(SyntaxNode n)
    {
        var s = n.GetLocation().GetLineSpan();
        return (s.StartLinePosition.Line + 1, s.EndLinePosition.Line + 1);
    }

    /// <summary>Members whose span (including attributes) overlaps the given lines.</summary>
    public async Task<List<(string Key, string Kind, int Line)>> MembersTouchingAsync(string rel, string text, HashSet<int> lines)
    {
        var result = new List<(string, string, int)>();
        if (_docs.TryGetValue(rel, out var doc))
        {
            var model = await doc.GetSemanticModelAsync();
            var root = await doc.GetSyntaxRootAsync();
            foreach (var m in Members(root!))
            {
                var (s, e) = Lines(m);
                if (!lines.Any(l => l >= s && l <= e)) continue;
                var sym = model!.GetDeclaredSymbol(m);
                if (sym == null) continue;
                result.Add((Register(sym), KindOf(sym), s));
            }
            return result;
        }
        var tree = CSharpSyntaxTree.ParseText(text, path: rel);
        foreach (var m in Members(tree.GetRoot()))
        {
            var (s, e) = Lines(m);
            if (lines.Any(l => l >= s && l <= e)) result.Add((SyntaxKey(m), KindOfSyntax(m), s));
        }
        return result;
    }

    public string Register(ISymbol sym)
    {
        var key = "cs:" + sym.ToDisplayString(KeyFormat);
        Symbols.TryAdd(key, sym);
        return key;
    }

    public static string KindOf(ISymbol s) => s switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor } => "cs-ctor",
        IMethodSymbol m when IsAction(m) => "cs-action",
        IMethodSymbol => "cs-method",
        _ => "cs-property"
    };

    static bool IsAction(IMethodSymbol m) =>
        m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic && m.MethodKind == MethodKind.Ordinary
        && m.ContainingType.Name.EndsWith("Controller") && m.ContainingType.Name.Length > "Controller".Length;

    static string KindOfSyntax(MemberDeclarationSyntax m) => m switch
    {
        ConstructorDeclarationSyntax => "cs-ctor",
        MethodDeclarationSyntax md when md.Modifiers.Any(x => x.Text == "public") && TypeName(md).EndsWith("Controller") => "cs-action",
        BaseMethodDeclarationSyntax => "cs-method",
        _ => "cs-property"
    };

    static string TypeName(SyntaxNode n) => n.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "";

    /// <summary>Key for a member found without a semantic model: namespace.Type.Name(param types as written).</summary>
    public static string SyntaxKey(MemberDeclarationSyntax m)
    {
        var types = m.Ancestors().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.Text);
        var ns = m.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString());
        var owner = string.Join(".", ns.Concat(types));
        var (name, ps) = m switch
        {
            MethodDeclarationSyntax md => (md.Identifier.Text, md.ParameterList),
            ConstructorDeclarationSyntax cd => (cd.Identifier.Text, cd.ParameterList),
            PropertyDeclarationSyntax pd => (pd.Identifier.Text, (ParameterListSyntax?)null),
            IndexerDeclarationSyntax => ("this[]", null),
            _ => ("?", null)
        };
        var sig = ps == null ? "" : "(" + string.Join(", ", ps.Parameters.Select(p => p.Type?.ToString() ?? "")) + ")";
        return $"cs:{owner}.{name}{sig}";
    }

    static string NameOfKey(string key)
    {
        var k = key[3..];
        var paren = k.IndexOf('(');
        var head = paren >= 0 ? k[..paren] : k;
        var name = head[(head.LastIndexOf('.') + 1)..];
        return name;
    }

    static int ArityOfKey(string key)
    {
        var p = key.IndexOf('(');
        if (p < 0) return -1;
        var inner = key[(p + 1)..key.LastIndexOf(')')];
        return inner.Trim().Length == 0 ? 0 : inner.Split(',').Length;
    }

    public (string File, int Line)? Location(string key)
    {
        if (!Symbols.TryGetValue(key, out var s)) return null;
        var loc = s.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc == null) return null;
        return (Rel(loc.SourceTree!.FilePath), loc.GetLineSpan().StartLinePosition.Line + 1);
    }

    /// <summary>Route templates for a controller action: [Route] on the class, [Http*]/[Route] on the method.</summary>
    public string? RouteOf(string key)
    {
        if (!Symbols.TryGetValue(key, out var s) || s is not IMethodSymbol m || !IsAction(m)) return null;
        string ctrl = m.ContainingType.Name[..^"Controller".Length];
        string? Arg(IEnumerable<AttributeData> attrs, params string[] names) => attrs
            .Where(a => a.AttributeClass != null && names.Contains(a.AttributeClass.Name))
            .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string ?? "")
            .FirstOrDefault();
        var baseRoute = (Arg(m.ContainingType.GetAttributes(), "RouteAttribute", "Route") ?? "").Replace("[controller]", ctrl, StringComparison.OrdinalIgnoreCase);
        var at = Arg(m.GetAttributes(), "HttpGetAttribute", "HttpPostAttribute", "HttpPutAttribute", "HttpDeleteAttribute", "HttpPatchAttribute", "RouteAttribute");
        var r = string.IsNullOrEmpty(at) ? (baseRoute.Length > 0 ? baseRoute : $"{ctrl}/{m.Name}")
              : at.StartsWith("/") || at.StartsWith("~") ? at.TrimStart('~', '/')
              : baseRoute.Length > 0 ? baseRoute + "/" + at : at;
        return r.Replace("[action]", m.Name, StringComparison.OrdinalIgnoreCase).Trim('/');
    }

    // ------------------------------------------------------------ callers

    /// <summary>Callers of a C# member: semantic references (high), plus a syntax scan of files outside
    /// any loaded project (low). A member only known by syntax is matched by name and arity (low).</summary>
    public async Task<List<(Node Caller, Edge Edge)>> CallersAsync(string key)
    {
        var found = new List<(Node, Edge)>();
        if (Symbols.TryGetValue(key, out var sym) && Solution != null)
        {
            var refs = await SymbolFinder.FindReferencesAsync(sym, Solution);
            foreach (var r in refs)
                foreach (var loc in r.Locations)
                {
                    var rel = Rel(loc.Document.FilePath ?? "");
                    if (IsTestPath(rel)) continue;
                    var root = await loc.Document.GetSyntaxRootAsync();
                    var model = await loc.Document.GetSemanticModelAsync();
                    var node = root!.FindNode(loc.Location.SourceSpan);
                    var line = loc.Location.GetLineSpan().StartLinePosition.Line + 1;
                    var caller = EnclosingSymbol(node, model!);
                    if (caller == null) continue;
                    var ck = Register(caller);
                    if (ck == key) continue;
                    var (cf, cl) = Location(ck) ?? (rel, line);
                    found.Add((new Node(ck, KindOf(caller), cf, cl, RouteOf(ck)), new Edge(ck, key, rel, line, "semantic", "high")));
                }
        }
        found.AddRange(SyntaxCallers(key));
        return found;
    }

    ISymbol? EnclosingSymbol(SyntaxNode node, SemanticModel model)
    {
        foreach (var a in node.AncestorsAndSelf())
        {
            switch (a)
            {
                case LocalFunctionStatementSyntax: continue;
                case BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax:
                    return model.GetDeclaredSymbol(a);
                case AccessorDeclarationSyntax acc when acc.Parent?.Parent is BasePropertyDeclarationSyntax bp:
                    return model.GetDeclaredSymbol(bp);
                case VariableDeclaratorSyntax v when v.Parent?.Parent is FieldDeclarationSyntax:
                    return model.GetDeclaredSymbol(v);
            }
        }
        return null;
    }

    IEnumerable<(Node, Edge)> SyntaxCallers(string key)
    {
        var name = NameOfKey(key);
        var arity = ArityOfKey(key);
        bool semanticTarget = Symbols.ContainsKey(key);
        // A semantic target only needs the files the workspace could not see; a syntax-only target
        // has no semantic references at all, so every file is scanned.
        IEnumerable<(string Rel, SyntaxTree Tree)> trees = SyntaxOnly.Select(kv => (kv.Key, kv.Value));
        if (!semanticTarget)
            trees = trees.Concat(_docs.Where(d => !IsTestPath(d.Key)).Select(d => (d.Key, d.Value.GetSyntaxTreeAsync().Result!)));
        foreach (var (rel, tree) in trees.OrderBy(t => t.Item1, StringComparer.Ordinal))
            foreach (var inv in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var called = inv.Expression switch
                {
                    IdentifierNameSyntax i => i.Identifier.Text,
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                    GenericNameSyntax g => g.Identifier.Text,
                    _ => null
                };
                if (called != name) continue;
                if (arity >= 0 && inv.ArgumentList.Arguments.Count != arity) continue;
                var encl = inv.Ancestors().OfType<MemberDeclarationSyntax>()
                    .FirstOrDefault(m => m is BaseMethodDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax);
                if (encl == null) continue;
                var ck = SyntaxKey(encl);
                if (ck == key) continue;
                var (s, _) = Lines(encl);
                var line = Lines(inv).Start;
                yield return (new Node(ck, KindOfSyntax(encl), rel, s, SyntaxRoute(encl)), new Edge(ck, key, rel, line, "syntax", "low"));
            }
    }

    static string? SyntaxRoute(MemberDeclarationSyntax m)
    {
        if (m is not MethodDeclarationSyntax md || !TypeName(md).EndsWith("Controller")) return null;
        var type = md.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        string ctrl = TypeName(md)[..^"Controller".Length];
        string? Arg(SyntaxList<AttributeListSyntax> al, params string[] names) => al.SelectMany(a => a.Attributes)
            .Where(a => names.Contains(a.Name.ToString().Split('.').Last().Replace("Attribute", "")))
            .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax l ? l.Token.ValueText : "")
            .FirstOrDefault();
        var baseRoute = (type == null ? "" : Arg(type.AttributeLists, "Route") ?? "").Replace("[controller]", ctrl, StringComparison.OrdinalIgnoreCase);
        var at = Arg(md.AttributeLists, "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "HttpPatch", "Route");
        var r = string.IsNullOrEmpty(at) ? (baseRoute.Length > 0 ? baseRoute : $"{ctrl}/{md.Identifier.Text}")
              : at.StartsWith("/") || at.StartsWith("~") ? at.TrimStart('~', '/') : baseRoute.Length > 0 ? baseRoute + "/" + at : at;
        return r.Replace("[action]", md.Identifier.Text, StringComparison.OrdinalIgnoreCase).Trim('/');
    }

    /// <summary>Resolve a --symbol: "cs:Type.Member" matches every overload on every type of that name;
    /// a full key with parameters ("cs:Ns.Type.Member(string)", as written in the graph) matches exactly one.</summary>
    public async Task<List<(string Key, string Kind, string File, int Line)>> FindDeclaredAsync(string typeDotMember)
    {
        var exact = typeDotMember.Contains('(') ? "cs:" + typeDotMember : null;
        var head = exact == null ? typeDotMember : typeDotMember[..typeDotMember.IndexOf('(')];
        var parts = head.Split('.');
        string member = parts[^1], type = parts.Length > 1 ? parts[^2] : "";
        var list = new List<(string, string, string, int)>();
        foreach (var (rel, doc) in _docs.OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            if (IsTestPath(rel)) continue;
            var root = await doc.GetSyntaxRootAsync();
            var model = await doc.GetSemanticModelAsync();
            foreach (var m in Members(root!))
            {
                var sym = model!.GetDeclaredSymbol(m);
                if (sym == null || sym.Name != member && !(member == type && sym is IMethodSymbol { MethodKind: MethodKind.Constructor })) continue;
                if (type.Length > 0 && sym.ContainingType?.Name != type) continue;
                var key = "cs:" + sym.ToDisplayString(KeyFormat);
                if (exact != null && key != exact) continue;
                list.Add((Register(sym), KindOf(sym), rel, Lines(m).Start));
            }
        }
        foreach (var (rel, tree) in SyntaxOnly)
            foreach (var m in Members(tree.GetRoot()))
            {
                var k = SyntaxKey(m);
                if (exact != null ? k == exact : NameOfKey(k) == member && (type.Length == 0 || TypeName(m) == type))
                    list.Add((k, KindOfSyntax(m), rel, Lines(m).Start));
            }
        return list;
    }

    public bool IsPublicMethod(string key) =>
        Symbols.TryGetValue(key, out var s) ? s is IMethodSymbol { DeclaredAccessibility: Accessibility.Public, MethodKind: MethodKind.Ordinary }
                                            : key.Contains('(');

    public static string MemberName(string key) => NameOfKey(key);
}
