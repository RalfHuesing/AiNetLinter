#nullable enable

namespace AiNetLinter.Cli;

public sealed class LinterArgs
{
    public string? ConfigPath { get; init; }

    public required string TargetPath { get; init; }

    public required bool Verbose { get; init; }

    public string? PlaybookPath { get; init; }

    public string? CreateBaselinePath { get; init; }

    public string? BaselinePath { get; init; }

    public bool OnlyChanged { get; init; }

    public bool AddDisableAll { get; init; }

    public bool RemoveDisableAll { get; init; }

    public bool DebtReport { get; init; }

    public bool WaveReady { get; init; }

    public string? GitSince { get; init; }

    public bool Fix { get; init; }

    public bool HasImpact { get; init; }

    public string? ImpactRef { get; init; }

    public bool SyncAgentRules { get; init; }

    public bool SyncAgentRulesOnly { get; init; }

    public string? AgentRulesPath { get; init; }

    public bool Check { get; init; }

    public bool NoCache { get; init; }

    public int CacheTtlMinutes { get; init; } = 60;

    public string? Footprint { get; init; }

    public string? Docs { get; init; }

    public bool ListRules { get; init; }

    public string? DescribeRule { get; init; }

    public string? SearchRules { get; init; }

    public string? MapType { get; init; }

    public string? EvalType { get; init; }

    public bool ListEvals { get; init; }

    public System.Collections.Generic.IReadOnlyList<string> SpecPaths { get; init; } = [];

    public System.Collections.Generic.IReadOnlyList<string> IncludeProjects { get; init; } = [];

    public System.Collections.Generic.IReadOnlyList<string> ExcludeProjects { get; init; } = [];

    public System.Collections.Generic.IReadOnlyList<string> IncludeNamespaces { get; init; } = [];

    public System.Collections.Generic.IReadOnlyList<string> ExcludeNamespaces { get; init; } = [];

    public bool ExcludeTests { get; init; }

    public bool TestsOnly { get; init; }

    public bool PublicOnly { get; init; }

    public System.Collections.Generic.IReadOnlyList<string>? IgnoreSuppressions { get; init; }

    public bool McpServer { get; init; }

    public System.Collections.Generic.IReadOnlyList<string> GetNormalizedIgnoreSuppressions()
    {
        if (IgnoreSuppressions == null || IgnoreSuppressions.Count == 0) return System.Array.Empty<string>();

        var set = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var item in IgnoreSuppressions)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            var token = item.Trim().ToLowerInvariant();
            if (token == "c#") token = "cs";
            set.Add(token);
        }

        if (set.Contains("all"))
        {
            return new[] { "all" };
        }

        var result = new System.Collections.Generic.List<string>();
        foreach (var lang in new[] { "cs", "razor", "js", "css" })
        {
            if (set.Contains(lang)) result.Add(lang);
        }
        return result;
    }

    public string? Validate()
    {
        if (IsPathMissing())
        {
            return "[ERROR]: --path ist erforderlich (außer bei --docs, --list-rules, --describe-rule, --search-rules, --map, --eval, --list-evals).";
        }

        if (HasConflictingModeOptions())
        {
            return "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.";
        }

        if (OnlyChanged && BaselinePath == null)
        {
            return "[ERROR]: --only-changed erfordert --baseline.";
        }

        return ValidateIgnoreSuppressions();
    }

    private bool IsPathMissing()
    {
        return !HasStandaloneCommand() && string.IsNullOrEmpty(TargetPath);
    }

    private bool HasStandaloneCommand() =>
        Docs != null || ListRules || DescribeRule != null || SearchRules != null || MapType != null || EvalType != null || ListEvals || McpServer;

    private string? ValidateIgnoreSuppressions()
    {
        if (IgnoreSuppressions == null) return null;

        var allowed = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "all", "cs", "c#", "razor", "js", "css" };
        foreach (var lang in IgnoreSuppressions)
        {
            if (string.IsNullOrWhiteSpace(lang) || !allowed.Contains(lang.Trim()))
            {
                return $"[ERROR]: Ungueltige Sprache fuer --ignore-suppressions: '{lang}'. Erlaubte Werte: all, cs, c#, razor, js, css.";
            }
        }
        return null;
    }

    private bool HasConflictingModeOptions()
    {
        int count = 0;
        if (CreateBaselinePath != null) count++;
        if (AddDisableAll) count++;
        if (RemoveDisableAll) count++;
        return count > 1 || (BaselinePath != null && count > 0);
    }
}
