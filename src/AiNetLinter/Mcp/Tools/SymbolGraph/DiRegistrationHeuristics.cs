#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
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

    internal static async Task<IReadOnlyList<string>> FindRegistrationsAsync(
        Solution solution, INamedTypeSymbol type, CancellationToken ct)
    {
        var typeNames = BuildTypeNameSet(type);
        var hits = new List<string>();
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) != true) continue;
                ct.ThrowIfCancellationRequested();
                if (await ScanDocumentAsync(document, typeNames, outputRoot, hits, ct)) return hits;
            }
        }

        return hits;
    }

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
