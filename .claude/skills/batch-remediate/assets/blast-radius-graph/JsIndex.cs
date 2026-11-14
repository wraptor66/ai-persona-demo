// Front-end index: JS function ranges, name-based call sites, <script src> includes, and /api/ URLs.
// JS has no types, so every edge found here is name-based, resolved the way the browser scopes it:
// a function inline in a page is visible only in that page (Symphony pages run in their own
// iframe); a function in a .js file is visible in the pages that load it and in scripts loaded by
// the same page. Medium when exactly one visible definition matches, low when several do.
using System.Text;
using System.Text.RegularExpressions;

namespace BlastRadiusGraph;

/// <summary>IsMember: an object/class method (called as obj.name(...)), not a global function.</summary>
public sealed record JsFunction(string Name, string File, int Start, int End, bool IsMember = false);

public readonly record struct CallSite(string File, int Line, bool Member);

public sealed class JsIndex
{
    public readonly Dictionary<string, string> Raw = new(StringComparer.Ordinal);      // rel -> text
    public readonly Dictionary<string, string[]> Code = new(StringComparer.Ordinal);   // rel -> code-only lines
    public readonly Dictionary<string, List<JsFunction>> Functions = new(StringComparer.Ordinal);
    public readonly Dictionary<string, List<string>> PagesIncluding = new(StringComparer.OrdinalIgnoreCase); // script file name -> pages
    public readonly List<(string File, int Line, string Path)> ApiUrls = new();

    static readonly string[] Ext = { ".js", ".html", ".htm", ".cshtml" };
    static readonly Regex[] Decl =
    {
        new(@"\bfunction\s*\*?\s+([A-Za-z_$][\w$]*)\s*\(", RegexOptions.Compiled),
        new(@"(?<![\w$.])([A-Za-z_$][\w$]*)\s*[:=]\s*(?:async\s+)?function\b", RegexOptions.Compiled),
        new(@"(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z_$][\w$]*)\s*=>", RegexOptions.Compiled),
        // method shorthand: name(a, b = 1) { - a plain parameter list only, so a call that takes a
        // callback (foo(function () {, setTimeout(() => {) is never mistaken for a definition
        new(@"^\s*(?:async\s+|static\s+|get\s+|set\s+)*([A-Za-z_$][\w$]*)\s*\(\s*(?:[A-Za-z_$][\w$]*(?:\s*=\s*[^,(){}]+)?\s*,?\s*)*\)\s*\{", RegexOptions.Compiled),
    };
    /// <summary>Which Decl patterns declare a member: `name: function` (pattern 1 with a colon) and shorthand (3).</summary>
    static bool IsMemberDecl(int pattern, Match m, string line) =>
        pattern == 3 || pattern == 1 && line[(m.Groups[1].Index + m.Groups[1].Length)..].TrimStart().StartsWith(':');
    static readonly HashSet<string> Keywords = new() { "if", "for", "while", "switch", "catch", "function", "return", "with", "else", "do", "try", "constructor" };
    static readonly Regex ScriptSrc = new(@"<script[^>]+src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ScriptBlock = new(@"<script\b[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex ApiConst = new(@"(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*=\s*(['""`])(/?api/[^'""`]*)\2", RegexOptions.Compiled);
    static readonly Regex StringLit = new(@"(['""`])((?:(?!\1).)*)\1", RegexOptions.Compiled);

    public static bool IsPage(string rel) => !rel.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

    public static bool Excluded(string rel)
    {
        var s = "/" + rel.ToLowerInvariant();
        return s.Contains("/lib/") || s.Contains("/node_modules/") || s.Contains("/tests/") || s.Contains("/bin/") || s.Contains("/obj/")
            || s.EndsWith(".min.js") || s.Contains("playwright-report") || s.Contains("/test-results/") || s.Contains("/.vs/");
    }

    /// <summary>Only files under a web root are the application's front end (generated docs such
    /// as VSdoc/ are not).</summary>
    public static bool UnderWebRoot(string rel, string webRoot) =>
        ("/" + rel.ToLowerInvariant()).Contains("/" + webRoot.ToLowerInvariant().Trim('/') + "/");

    public JsIndex(string repo, string root, string webRoot = "wwwroot")
    {
        var abs = Path.Combine(repo, root);
        var files = Directory.EnumerateFiles(abs, "*.*", SearchOption.AllDirectories)
            .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => Path.GetRelativePath(repo, f).Replace('\\', '/'))
            .Where(r => !Excluded(r) && UnderWebRoot(r, webRoot))
            .OrderBy(r => r, StringComparer.Ordinal);
        foreach (var rel in files)
        {
            var text = File.ReadAllText(Path.Combine(repo, rel));
            Raw[rel] = text;
            var code = CodeOnly(text, IsPage(rel));
            Code[rel] = code.Split('\n');
            Functions[rel] = FindFunctions(rel, Code[rel]);
            if (IsPage(rel))
                foreach (Match m in ScriptSrc.Matches(text))
                {
                    var fn = Path.GetFileName(m.Groups[1].Value.Split('?')[0]);
                    if (!PagesIncluding.TryGetValue(fn, out var l)) PagesIncluding[fn] = l = new();
                    if (!l.Contains(rel)) l.Add(rel);
                }
            CollectApiUrls(rel, text);
        }
    }

    /// <summary>Parse a file as it was at another commit (for removed functions).</summary>
    public static List<JsFunction> FunctionsOf(string rel, string text) => FindFunctions(rel, CodeOnly(text, IsPage(rel)).Split('\n'));

    /// <summary>Blanks strings and comments (keeping newlines) so braces and calls are code only.
    /// In a page, only &lt;script&gt; bodies are code.</summary>
    static string CodeOnly(string text, bool page)
    {
        if (page)
        {
            var sb = new StringBuilder(Regex.Replace(text, @"[^\n]", " "));
            foreach (Match m in ScriptBlock.Matches(text))
            {
                var g = m.Groups[1];
                var code = CodeOnly(g.Value, false);
                for (int i = 0; i < code.Length; i++) sb[g.Index + i] = code[i];
            }
            return sb.ToString();
        }
        var o = new StringBuilder(text.Length);
        int n = text.Length;
        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            char nx = i + 1 < n ? text[i + 1] : '\0';
            if (c == '/' && nx == '/') { while (i < n && text[i] != '\n') { o.Append(' '); i++; } if (i < n) o.Append('\n'); continue; }
            if (c == '/' && nx == '*')
            {
                o.Append("  "); i += 2;
                while (i < n && !(text[i] == '*' && i + 1 < n && text[i + 1] == '/')) { o.Append(text[i] == '\n' ? '\n' : ' '); i++; }
                if (i < n) { o.Append("  "); i++; }
                continue;
            }
            if (c is '\'' or '"' or '`')
            {
                o.Append(' '); i++;
                while (i < n && text[i] != c)
                {
                    if (text[i] == '\\' && i + 1 < n) { o.Append(' '); i++; o.Append(text[i] == '\n' ? '\n' : ' '); i++; continue; }
                    if (c != '`' && text[i] == '\n') break; // unterminated: give up on this string
                    o.Append(text[i] == '\n' ? '\n' : ' '); i++;
                }
                if (i < n) o.Append(text[i] == '\n' ? '\n' : ' ');
                continue;
            }
            o.Append(c);
        }
        return o.ToString();
    }

    static List<JsFunction> FindFunctions(string rel, string[] lines)
    {
        var list = new List<JsFunction>();
        for (int i = 0; i < lines.Length; i++)
        {
            for (int p = 0; p < Decl.Length; p++)
            {
                var m = Decl[p].Match(lines[i]);
                if (!m.Success || Keywords.Contains(m.Groups[1].Value)) continue;
                list.Add(new JsFunction(m.Groups[1].Value, rel, i + 1, EndOf(lines, i, m.Index), IsMemberDecl(p, m, lines[i])));
                break;
            }
        }
        return list;
    }

    /// <summary>The line holding the brace that closes the body opened on or after (line, col).</summary>
    static int EndOf(string[] lines, int line, int col)
    {
        int depth = 0; bool opened = false;
        for (int i = line; i < lines.Length; i++)
        {
            var s = lines[i];
            for (int j = i == line ? col : 0; j < s.Length; j++)
            {
                if (s[j] == '{') { depth++; opened = true; }
                else if (s[j] == '}') { depth--; if (opened && depth == 0) return i + 1; }
            }
            if (!opened && s.TrimEnd().EndsWith(';')) return i + 1; // arrow function without a body
            if (!opened && i > line + 2) return line + 1;
        }
        return lines.Length;
    }

    public JsFunction? Enclosing(string rel, int line) =>
        Functions.TryGetValue(rel, out var fs)
            ? fs.Where(f => f.Start <= line && f.End >= line).OrderBy(f => f.End - f.Start).ThenBy(f => f.Start).FirstOrDefault()
            : null;

    public int DefinitionCount(string name)
    {
        if (_calls == null) BuildCallIndex();
        return _defCount.GetValueOrDefault(name);
    }

    static readonly Regex CallAny = new(@"(?<![\w$])([A-Za-z_$][\w$]*)\s*\(", RegexOptions.Compiled);
    static readonly Regex HandlerAny = new(@"(?:addEventListener\s*\([^,]+,\s*|[(,=]\s*)([A-Za-z_$][\w$]*)\s*[,);]", RegexOptions.Compiled);
    static readonly Regex AttrAny = new(@"\bon\w+\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    Dictionary<string, List<CallSite>>? _calls;
    readonly Dictionary<string, int> _defCount = new(StringComparer.Ordinal);

    /// <summary>Call sites and handler references of <paramref name="name"/>, excluding its declarations.
    /// Member = the line only calls it as obj.name(...). Every file is tokenized once.</summary>
    public IReadOnlyList<CallSite> CallSites(string name)
    {
        if (_calls == null) BuildCallIndex();
        return _calls!.TryGetValue(name, out var l) ? l : Array.Empty<CallSite>();
    }

    void BuildCallIndex()
    {
        _calls = new(StringComparer.Ordinal);
        foreach (var f in Functions.Values.SelectMany(x => x))
        {
            _defCount[f.Name] = _defCount.GetValueOrDefault(f.Name) + 1;
            if (!_byName.TryGetValue(f.Name, out var l)) _byName[f.Name] = l = new();
            l.Add(f);
        }
        var seen = new Dictionary<(string Name, string File, int Line), bool>(); // -> member-only so far
        void Add(string name, string rel, int line, bool member)
        {
            var k = (name, rel, line);
            seen[k] = seen.TryGetValue(k, out var m) ? m && member : member;
        }
        static bool AfterDot(string l, int index)
        {
            for (int j = index - 1; j >= 0; j--) { if (char.IsWhiteSpace(l[j])) continue; return l[j] == '.'; }
            return false;
        }
        foreach (var (rel, lines) in Code)
        {
            var declAt = Functions[rel].GroupBy(f => f.Start).ToDictionary(g => g.Key, g => g.Select(f => f.Name).ToHashSet());
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (l.IndexOf('(') < 0) continue;
                var occ = new Dictionary<string, (int Direct, int Member)>(StringComparer.Ordinal);
                foreach (Match m in CallAny.Matches(l))
                {
                    var o = occ.GetValueOrDefault(m.Groups[1].Value);
                    occ[m.Groups[1].Value] = AfterDot(l, m.Index) ? (o.Direct, o.Member + 1) : (o.Direct + 1, o.Member);
                }
                foreach (Match m in HandlerAny.Matches(l))
                {
                    var o = occ.GetValueOrDefault(m.Groups[1].Value);
                    occ[m.Groups[1].Value] = (o.Direct + 1, o.Member);
                }
                declAt.TryGetValue(i + 1, out var decl);
                foreach (var (name, (direct, member)) in occ)
                {
                    var d = direct - (decl != null && decl.Contains(name) ? 1 : 0);
                    if (d > 0) Add(name, rel, i + 1, false);
                    else if (member > 0) Add(name, rel, i + 1, true);
                }
            }
            if (IsPage(rel))
            {
                var raw = Raw[rel].Split('\n');
                for (int i = 0; i < raw.Length; i++)
                    foreach (Match a in AttrAny.Matches(raw[i]))
                        foreach (Match m in CallAny.Matches(a.Groups[1].Value)) Add(m.Groups[1].Value, rel, i + 1, AfterDot(a.Groups[1].Value, m.Index));
            }
        }
        foreach (var g in seen.GroupBy(kv => kv.Key.Name))
            _calls[g.Key] = g.Select(kv => new CallSite(kv.Key.File, kv.Key.Line, kv.Value))
                .OrderBy(c => c.File, StringComparer.Ordinal).ThenBy(c => c.Line).ToList();
    }

    // ---- scope: which definitions a call site can actually reach

    Dictionary<string, HashSet<string>>? _scriptsOfPage;

    /// <summary>True when code in <paramref name="callFile"/> can see a function defined in <paramref name="defFile"/>.</summary>
    public bool Visible(string defFile, string callFile)
    {
        if (defFile == callFile) return true;
        if (IsPage(defFile)) return false; // inline page code is private to its page (own iframe)
        var defName = Path.GetFileName(defFile);
        if (!PagesIncluding.TryGetValue(defName, out var pages)) return !IsPage(callFile); // loaded some other way: scripts only
        if (IsPage(callFile)) return pages.Contains(callFile);
        if (_scriptsOfPage == null)
        {
            _scriptsOfPage = new(StringComparer.Ordinal);
            foreach (var (script, ps) in PagesIncluding)
                foreach (var p in ps)
                {
                    if (!_scriptsOfPage.TryGetValue(p, out var s)) _scriptsOfPage[p] = s = new(StringComparer.OrdinalIgnoreCase);
                    s.Add(script);
                }
        }
        var callName = Path.GetFileName(callFile);
        return pages.Any(p => _scriptsOfPage.TryGetValue(p, out var s) && s.Contains(callName));
    }

    /// <summary>Definitions of <paramref name="name"/> a call site can reach, member calls matching member definitions only.</summary>
    public List<JsFunction> Targets(string name, CallSite site)
    {
        if (_calls == null) BuildCallIndex();
        return _byName.TryGetValue(name, out var defs)
            ? defs.Where(f => (!site.Member || f.IsMember) && Visible(f.File, site.File)).ToList()
            : new List<JsFunction>();
    }
    readonly Dictionary<string, List<JsFunction>> _byName = new(StringComparer.Ordinal);

    // ---- routed payloads: methodName "X" / actionname "Y", indexed once
    static readonly Regex MethodNameAny = new(@"(?:[""']?methodname[""']?\s*[:=]\s*|\[\s*[""']methodname[""']\s*\]\s*=\s*)[""'`](\w+)[""'`]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ActionNameAny = new(@"(?:[""']?actionname[""']?\s*[:=]\s*|\[\s*[""']actionname[""']\s*\]\s*=\s*)[""'`](\w+)[""'`]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    Dictionary<string, List<(string File, int Line, string Via)>>? _payloads;

    /// <summary>Front-end lines naming the method in a routed payload, directly or via an event-map action.</summary>
    public IEnumerable<(string File, int Line, string Via)> PayloadSites(string method, Dictionary<string, string> eventMap)
    {
        if (_payloads == null)
        {
            _payloads = new(StringComparer.Ordinal);
            foreach (var (rel, text) in Raw)
            {
                if (text.IndexOf("methodname", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("actionname", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in MethodNameAny.Matches(lines[i])) AddP(m.Groups[1].Value, rel, i + 1, "methodName-payload");
                    foreach (Match m in ActionNameAny.Matches(lines[i]))
                        if (eventMap.TryGetValue(m.Groups[1].Value, out var target)) AddP(target, rel, i + 1, "actionname-eventmap");
                }
            }
        }
        return _payloads.TryGetValue(method, out var l) ? l : Enumerable.Empty<(string, int, string)>();

        void AddP(string name, string rel, int line, string via)
        {
            if (!_payloads!.TryGetValue(name, out var list)) _payloads[name] = list = new();
            if (!list.Contains((rel, line, via))) list.Add((rel, line, via));
        }
    }

    void CollectApiUrls(string rel, string text)
    {
        var consts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in ApiConst.Matches(text)) consts.TryAdd(m.Groups[1].Value, m.Groups[3].Value.TrimStart('/'));
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.Length > 4000) continue;
            foreach (Match s in StringLit.Matches(l))
            {
                var v = s.Groups[2].Value;
                v = Regex.Replace(v, @"\$\{\s*([A-Za-z_$][\w$]*)\s*\}", m => consts.TryGetValue(m.Groups[1].Value, out var c) ? c : "{x}");
                v = Regex.Replace(v, @"\$\{[^}]*\}", "{x}");
                // CONST + '/segment'
                var before = l[..s.Index];
                var concat = Regex.Match(before, @"([A-Za-z_$][\w$]*)\s*\+\s*$");
                if (concat.Success && consts.TryGetValue(concat.Groups[1].Value, out var baseUrl)) v = baseUrl + v;
                var api = Regex.Match(v, @"(?:^|/)(api/[A-Za-z0-9_\-/{}.]+)");
                if (api.Success) ApiUrls.Add((rel, i + 1, api.Groups[1].Value.TrimEnd('/')));
            }
        }
    }
}
