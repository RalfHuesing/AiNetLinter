#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

internal static partial class FeatureContextScanner
{
    private static (string FilePath, int StartLine, int EndLine) ExtractLocation(ISymbol symbol, string solutionDir)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference is not null)
        {
            var span = syntaxReference.GetSyntax().GetLocation().GetLineSpan();
            return (PathNormalizer.ToRelative(solutionDir, span.Path), span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
        }
        var location = symbol.Locations.FirstOrDefault(value => value.IsInSource);
        if (location?.SourceTree is null) return ("", 0, 0);
        var lineSpan = location.GetLineSpan();
        return (PathNormalizer.ToRelative(solutionDir, location.SourceTree.FilePath), lineSpan.StartLinePosition.Line + 1, lineSpan.EndLinePosition.Line + 1);
    }

    private static async Task<(List<CallSiteEntry> Visible, IReadOnlyDictionary<CallSiteEntry, McpDocumentScope> Scopes, int ExcludedCount)> FilterCallersAsync(IReadOnlyList<CallSiteEntry> callers, Solution solution, McpScopeInput scopeInput, McpScopeClassifier classifier, CancellationToken ct)
    {
        var visible = new List<CallSiteEntry>(callers.Count);
        var scopes = new Dictionary<CallSiteEntry, McpDocumentScope>();
        var excluded = 0;
        foreach (var caller in callers)
        {
            var document = DiffImpactAnalyzer.FindDocumentByPath(solution, caller.FilePath);
            var scope = document is null ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable) : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(scope, scopeInput)) { excluded++; continue; }
            visible.Add(caller);
            scopes[caller] = scope;
        }
        return (visible, scopes, excluded);
    }

    private static async Task<(List<TestFileCoverageResult> Visible, IReadOnlyDictionary<string, McpDocumentScope> Scopes, int ExcludedCount)> FilterTestFilesAsync(IReadOnlyList<TestFileCoverageResult> testFiles, Solution solution, McpScopeInput scopeInput, McpScopeClassifier classifier, CancellationToken ct)
    {
        var visible = new List<TestFileCoverageResult>(testFiles.Count);
        var scopes = new Dictionary<string, McpDocumentScope>(StringComparer.OrdinalIgnoreCase);
        var excluded = 0;
        foreach (var testFile in testFiles)
        {
            var document = DiffImpactAnalyzer.FindDocumentByPath(solution, testFile.FilePath);
            var scope = document is null ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable) : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(scope, scopeInput)) { excluded++; continue; }
            visible.Add(testFile);
            scopes[testFile.FilePath] = scope;
        }
        return (visible, scopes, excluded);
    }
}
