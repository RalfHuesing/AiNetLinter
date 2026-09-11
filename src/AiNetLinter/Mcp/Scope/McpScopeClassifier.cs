#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Core.Checkers;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Scope;

/// <summary>
/// Gemeinsame MCP-Quelle für Projekt-, Test- und Generated-Klassifikation.
/// Der Cache gehört zur Classifier-Instanz und identifiziert Einträge über den
/// Roslyn-Snapshot und die DocumentId, nie nur über einen Dateipfad.
/// </summary>
internal sealed class McpScopeClassifier
{
    private readonly ConcurrentDictionary<CacheKey, McpDocumentScope> _cache = new();

    internal int CachedEntryCount => _cache.Count;

    internal async Task<McpDocumentScope> ClassifyAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        // The document text version is the effective Roslyn snapshot for this
        // classification. Solution.Version is not guaranteed to advance for
        // detached AdhocWorkspace snapshots after WithDocumentText.
        var key = new CacheKey(
            document.Project.Solution.Version,
            await document.GetTextVersionAsync(cancellationToken).ConfigureAwait(false),
            document.Id);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var sourceKind = await ClassifySourceKindAsync(document, cancellationToken).ConfigureAwait(false);
        var result = new McpDocumentScope(ClassifyProjectKind(document), sourceKind);
        _cache[key] = result;
        return result;
    }

    internal bool MatchesScope(McpDocumentScope scope, McpScopeType requestedScope) =>
        requestedScope switch
        {
            McpScopeType.All => true,
            McpScopeType.Production => scope.ProjectKind == McpProjectKind.Production,
            McpScopeType.Tests => scope.ProjectKind == McpProjectKind.Tests,
            _ => false,
        };

    internal async Task<McpSymbolScope> ClassifySymbolAsync(
        ISymbol symbol,
        Solution solution,
        McpScopeType requestedScope,
        bool includeGenerated,
        CancellationToken cancellationToken)
    {
        var locations = symbol.Locations.Where(location => location.IsInSource).ToList();
        if (locations.Count == 0)
        {
            return new(
                McpProjectKind.Unknown,
                McpSourceKind.Editable,
                HasSourceLocation: false,
                IsVisible: requestedScope == McpScopeType.All);
        }

        var classified = new List<McpDocumentScope>(locations.Count);
        foreach (var location in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = solution.GetDocument(location.SourceTree!);
            if (document is null) continue;
            classified.Add(await ClassifyAsync(document, cancellationToken).ConfigureAwait(false));
        }

        if (classified.Count == 0)
        {
            return new(McpProjectKind.Unknown, McpSourceKind.Editable, true, requestedScope == McpScopeType.All);
        }

        var preferred = classified
            .OrderBy(scope => ProjectRank(scope.ProjectKind))
            .ThenBy(scope => SourceRank(scope.SourceKind))
            .First();
        var visible = classified.Any(scope =>
            MatchesScope(scope, requestedScope)
            && (includeGenerated || scope.SourceKind != McpSourceKind.Generated));
        return new(preferred.ProjectKind, preferred.SourceKind, true, visible);
    }

    private static int ProjectRank(McpProjectKind kind) => kind switch
    {
        McpProjectKind.Production => 0,
        McpProjectKind.Tests => 1,
        _ => 2,
    };

    private static int SourceRank(McpSourceKind kind) => kind == McpSourceKind.Editable ? 0 : 1;

    private static McpProjectKind ClassifyProjectKind(Document document)
    {
        var project = document.Project;
        if (TestDetector.IsDecompiledAssemblyProject(project)) return McpProjectKind.Unknown;

        if (TestDetector.IsTestProject(project)
            || (document.FilePath is { } path && TestDetector.IsTestFile(path)))
        {
            return McpProjectKind.Tests;
        }

        // A Roslyn project with no test evidence is a production document. This
        // preserves ordinary AdhocWorkspace documents, which commonly have no
        // physical project path. Unknown is reserved for decompiled/unsupported
        // project identities where production semantics are not established.
        return project.Language.Equals(LanguageNames.CSharp, StringComparison.Ordinal)
            ? McpProjectKind.Production
            : McpProjectKind.Unknown;
    }

    private static async Task<McpSourceKind> ClassifySourceKindAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        if (IsGeneratedPath(document.FilePath ?? document.Name)) return McpSourceKind.Generated;

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (ContainsAutoGeneratedHeader(text.ToString())) return McpSourceKind.Generated;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not null && semanticModel is not null && GeneratedCodeDetector.IsGenerated(root, semanticModel))
        {
            return McpSourceKind.Generated;
        }

        return McpSourceKind.Editable;
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
            || HasSuffix(normalized, ".g.cs")
            || HasSuffix(normalized, ".g.i.cs")
            || HasSuffix(normalized, ".generated.cs")
            || HasSuffix(normalized, ".designer.cs");
    }

    private static bool HasSuffix(string path, string suffix) =>
        path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAutoGeneratedHeader(string text)
    {
        using var reader = new StringReader(text);
        for (var lineNumber = 0; lineNumber < 5; lineNumber++)
        {
            var line = reader.ReadLine();
            if (line is null) return false;
            if (line.Contains("<auto-generated", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private readonly record struct CacheKey(
        VersionStamp SolutionSnapshot,
        VersionStamp DocumentSnapshot,
        DocumentId DocumentId);
}
