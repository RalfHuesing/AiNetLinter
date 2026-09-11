#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using AiNetLinter.Mcp.Scope;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Heuristische Suche nach DI-Container-Registrierungen fuer einen gegebenen C#-Typ. Sucht per
/// <c>\b</c>-Word-Boundary-Regex nach <c>AddScoped&lt;...&gt;</c>, <c>AddSingleton&lt;...&gt;</c> und
/// <c>AddTransient&lt;...&gt;</c> in allen .cs-Dateien der Solution. Filtert Treffer auf solche,
/// deren Typ-Parameter-Text den voll-qualifizierten Namen des Ziel-Typs enthaelt, damit
/// generische <c>AddScoped&lt;ILogger&lt;&gt;&gt;</c>-Patterns nicht zu Massen-Treffern fuehren.
/// Convention-basierte Registrierung (z. B. <c>services.AddMvc()</c>) und Factory-basierte
/// Registrierung (z. B. <c>services.AddSingleton&lt;IFoo&gt;(sp =&gt; new Foo())</c>) werden
/// bewusst NICHT erkannt: Convention-/Factory-Patterns sind mit Regex nicht sauber auflösbar.
/// </summary>
internal static class DiRegistrationHeuristics
{
    internal const int MaxRegistrationHits = 20;

    private static readonly Regex AddScopedPattern = new(
        @"\bAddScoped<\s*([\w\.\?\,\s]+?)\s*>",
        RegexOptions.Compiled);
    private static readonly Regex AddSingletonPattern = new(
        @"\bAddSingleton<\s*([\w\.\?\,\s]+?)\s*>",
        RegexOptions.Compiled);
    private static readonly Regex AddTransientPattern = new(
        @"\bAddTransient<\s*([\w\.\?\,\s]+?)\s*>",
        RegexOptions.Compiled);

    internal static Task<IReadOnlyList<string>> FindRegistrationsAsync(
        Solution solution,
        INamedTypeSymbol type,
        CancellationToken ct) =>
        FindRegistrationsAsync(
            new FindRegistrationsRequest(
                solution,
                type,
                McpScopeType.All,
                IncludeGenerated: false,
                new McpScopeClassifier()),
            ct);

    internal static async Task<IReadOnlyList<string>> FindRegistrationsAsync(
        FindRegistrationsRequest request,
        CancellationToken ct)
    {
        var typeNames = BuildTypeNameSet(request.Type);
        var hits = new List<string>();
        var outputRoot = Path.GetDirectoryName(request.Solution.FilePath) ?? "";

        var documents = new List<(Document Document, McpDocumentScope Scope)>();
        foreach (var project in request.Solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) != true) continue;
                ct.ThrowIfCancellationRequested();
                var scope = await request.Classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
                if (!request.Classifier.MatchesScope(scope, request.ScopeType)
                    || (!request.IncludeGenerated && scope.SourceKind == McpSourceKind.Generated)) continue;
                documents.Add((document, scope));
            }
        }

        foreach (var (document, _) in documents
            .OrderBy(item => ProjectRank(item.Scope.ProjectKind))
            .ThenBy(item => item.Scope.SourceKind == McpSourceKind.Editable ? 0 : 1)
            .ThenBy(item => item.Document.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            if (await ScanDocumentAsync(document, typeNames, outputRoot, hits, ct).ConfigureAwait(false)) return hits;
        }

        return hits;
    }

    private static int ProjectRank(McpProjectKind kind) => kind switch
    {
        McpProjectKind.Production => 0,
        McpProjectKind.Tests => 1,
        _ => 2,
    };

    private static async Task<bool> ScanDocumentAsync(
        Document document,
        HashSet<string> typeNames,
        string outputRoot,
        List<string> hits,
        CancellationToken ct)
    {
        var text = await document.GetTextAsync(ct);
        var ctx = ScanContext.Create(new ScanInit(document.FilePath!, outputRoot), text, typeNames, hits);
        return await ScanAllPatternsAsync(ctx);
    }

    private static async Task<bool> ScanAllPatternsAsync(ScanContext ctx)
    {
        ScanWith(ctx, AddScopedPattern, "AddScoped");
        if (ctx.Hits.Count >= MaxRegistrationHits) return true;
        ScanWith(ctx, AddSingletonPattern, "AddSingleton");
        if (ctx.Hits.Count >= MaxRegistrationHits) return true;
        ScanWith(ctx, AddTransientPattern, "AddTransient");
        return ctx.Hits.Count >= MaxRegistrationHits;
    }

    private static HashSet<string> BuildTypeNameSet(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            type.ToDisplayString(),
            type.Name,
        };
        if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            names.Add($"{ns.ToDisplayString()}.{type.Name}");
        }
        return names;
    }

    private static void ScanWith(ScanContext ctx, Regex pattern, string lifestyle)
    {
        foreach (Match match in pattern.Matches(ctx.Content))
        {
            if (ctx.Hits.Count >= MaxRegistrationHits) return;
            var typeArg = match.Groups[1].Value;
            if (!ctx.TypeNames.Any(n => typeArg.Contains(n, StringComparison.Ordinal))) continue;
            ctx.RecordHit(match, typeArg, lifestyle);
        }
    }

    private sealed class ScanContext
    {
        private ScanContext(
            ScanInit init,
            SourceText text,
            HashSet<string> typeNames,
            List<string> hits)
        {
            Init = init;
            Text = text;
            TypeNames = typeNames;
            Hits = hits;
        }

        public string Content => Text.ToString();
        public SourceText Text { get; }
        public ScanInit Init { get; }
        public HashSet<string> TypeNames { get; }
        public List<string> Hits { get; }

        public static ScanContext Create(
            ScanInit init,
            SourceText text,
            HashSet<string> typeNames,
            List<string> hits) =>
            new(init, text, typeNames, hits);

        public void RecordHit(Match match, string typeArg, string lifestyle)
        {
            var line = Text.Lines.GetLinePosition(match.Index).Line + 1;
            var relativePath = PathNormalizer.ToRelative(Init.OutputRoot, Init.FilePath);
            var snippet = match.Value.Trim();
            Hits.Add($"{lifestyle}: {typeArg.Trim()} ({relativePath}:{line}) — {snippet}");
        }
    }

    private sealed record ScanInit(string FilePath, string OutputRoot);
}
