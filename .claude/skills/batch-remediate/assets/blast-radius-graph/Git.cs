// git plumbing: resolve refs, read files at a commit, and the changed line ranges between two commits.
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BlastRadiusGraph;

public sealed class FileChange
{
    public string? OldPath;
    public string? NewPath;
    public HashSet<int> OldLines = new();
    public HashSet<int> NewLines = new();
}

public sealed class Git
{
    readonly string _repo;
    public Git(string repo) => _repo = repo;

    public string Run(params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = _repo, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.quotepath=off");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', args)}: {stderr.Result.Trim()}");
        return stdout.Result;
    }

    public static string TopLevel(string dir)
    {
        var g = new Git(dir);
        return Path.GetFullPath(g.Run("rev-parse", "--show-toplevel").Trim());
    }

    public string Sha(string rev) => Run("rev-parse", "--verify", rev + "^{commit}").Trim();

    public string? Show(string sha, string repoRelPath)
    {
        try { return Run("show", $"{sha}:{repoRelPath}"); } catch { return null; }
    }

    static readonly Regex Hunk = new(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.Compiled);

    /// <summary>Changed lines per file under <paramref name="pathspec"/>. A pure deletion marks the
    /// lines either side of it on the new side, so the member it was cut from counts as changed.</summary>
    public List<FileChange> Diff(string baseSha, string headSha, string pathspec)
    {
        var text = Run("diff", "-U0", "--no-renames", "--no-color", baseSha, headSha, "--", pathspec);
        var list = new List<FileChange>();
        FileChange? cur = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("diff --git ")) { cur = new FileChange(); list.Add(cur); continue; }
            if (cur == null) continue;
            if (line.StartsWith("--- ")) { cur.OldPath = line == "--- /dev/null" ? null : line[6..]; continue; }
            if (line.StartsWith("+++ ")) { cur.NewPath = line == "+++ /dev/null" ? null : line[6..]; continue; }
            var m = Hunk.Match(line);
            if (!m.Success) continue;
            int a = int.Parse(m.Groups[1].Value), b = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
            int c = int.Parse(m.Groups[3].Value), d = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1;
            for (int i = 0; i < b; i++) cur.OldLines.Add(a + i);
            for (int i = 0; i < d; i++) cur.NewLines.Add(c + i);
            if (d == 0) { cur.NewLines.Add(c); cur.NewLines.Add(c + 1); }
            if (b == 0) { cur.OldLines.Add(a); cur.OldLines.Add(a + 1); }
        }
        return list;
    }
}
