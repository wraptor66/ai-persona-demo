// Graph model and canonical JSON. Everything written is sorted and free of timestamps and absolute
// paths, so the same inputs produce byte-identical output and the same hash.
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlastRadiusGraph;

/// <summary>cs-method | cs-ctor | cs-property | cs-action | js-function | script | page</summary>
public sealed record Node(string Key, string Kind, string File, int Line, string? Route = null);

/// <summary>From = the caller, To = the callee. Via says how the edge was found.</summary>
public sealed record Edge(string From, string To, string File, int Line, string Via, string Confidence);

/// <summary>Change = modified | added | removed.</summary>
public sealed record ChangedSymbol(string Key, string Lang, string Change, string File, int Line);

public sealed record ProjectLoad(string Path, bool Loaded, string Mode, string? Reason);

public sealed record ScenarioPath(string Changed, string Confidence, List<string> Path);

public sealed record Scenario(string Id, string Page, string Confidence, List<string> ReachedBy, List<ScenarioPath> Paths);

public sealed record Inputs(string Root, string Base, string Head, int Depth, int MaxFanIn, string Mode, List<string> Symbols);

/// <summary>A member with more callers than --max-fanin: recorded, not expanded.</summary>
public sealed record Hub(string Key, int FanIn);

public sealed class GraphDoc
{
    public string Schema { get; set; } = "batch-remediate/blast-radius-graph@1";
    public string WorkItem { get; set; } = "";
    public Inputs Inputs { get; set; } = new("", "", "", 0, 0, "", new());
    public List<ProjectLoad> Projects { get; set; } = new();
    public List<ChangedSymbol> Changed { get; set; } = new();
    public List<Scenario> Scenarios { get; set; } = new();
    /// <summary>Changed symbols that reach no page: internal, proven by unit tests only.</summary>
    public List<string> Internal { get; set; } = new();
    public List<Hub> Hubs { get; set; } = new();
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public string Hash { get; set; } = "";
}

public sealed record ScenarioRef(string Id, string Page, string Confidence);
public sealed record Exclusion(string Id, string Reason);

/// <summary>The regression set agreed at M2 (L2). Generated from the graph, then edited by people only
/// to move scenarios into Excluded with a reason.</summary>
public sealed class RegressionSet
{
    public string Schema { get; set; } = "batch-remediate/regression-set@1";
    public string WorkItem { get; set; } = "";
    public string GraphHash { get; set; } = "";
    public string Base { get; set; } = "";
    public string Head { get; set; } = "";
    public List<string> Changed { get; set; } = new();
    public List<ScenarioRef> Scenarios { get; set; } = new();
    public List<Exclusion> Excluded { get; set; } = new();
    public List<string> Callers { get; set; } = new();
}

public static class Canonical
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n") + "\n";

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("empty JSON");

    /// <summary>Sorts every collection and stamps the hash of the content (hash field blank while hashing).</summary>
    public static string Finish(GraphDoc g)
    {
        g.Projects = g.Projects.OrderBy(p => p.Path, StringComparer.Ordinal).ToList();
        g.Changed = g.Changed.OrderBy(c => c.Key, StringComparer.Ordinal).ThenBy(c => c.File, StringComparer.Ordinal).ToList();
        g.Internal = g.Internal.Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        g.Hubs = g.Hubs.OrderBy(h => h.Key, StringComparer.Ordinal).ToList();
        g.Nodes = g.Nodes.OrderBy(n => n.Key, StringComparer.Ordinal).ToList();
        g.Edges = g.Edges.Distinct()
            .OrderBy(e => e.From, StringComparer.Ordinal).ThenBy(e => e.To, StringComparer.Ordinal)
            .ThenBy(e => e.File, StringComparer.Ordinal).ThenBy(e => e.Line).ThenBy(e => e.Via, StringComparer.Ordinal).ToList();
        g.Scenarios = g.Scenarios.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        g.Hash = "";
        g.Hash = Sha256(Serialize(g));
        return Serialize(g);
    }

    public static string Sha256(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    static readonly string[] Rank = { "low", "medium", "high" };
    public static string Min(string a, string b) => Array.IndexOf(Rank, a) <= Array.IndexOf(Rank, b) ? a : b;
    public static string Max(string a, string b) => Array.IndexOf(Rank, a) >= Array.IndexOf(Rank, b) ? a : b;
}
