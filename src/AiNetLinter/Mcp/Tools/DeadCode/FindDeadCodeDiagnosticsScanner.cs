#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Extrahiert Compiler- und Analyzer-Diagnosen (CS0169, CS0414, IDE0051, IDE0052) fuer ungenutzte private Member und Felder.
/// </summary>
internal static class FindDeadCodeDiagnosticsScanner
{
    private static readonly HashSet<string> LocalDiagnosticIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "CS0169",
        "CS0414",
        "IDE0051",
        "IDE0052"
    };

    /// <summary>
    /// Durchsucht die Compilation nach Diagnosen fuer ungenutzten Code.
    /// </summary>
    internal static async Task ScanProjectDiagnosticsAsync(
        IEnumerable<Document> documents,
        Compilation compilation,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        var documentMap = documents
            .Where(d => d.FilePath != null)
            .ToDictionary(d => d.FilePath!, StringComparer.OrdinalIgnoreCase);

        var diagnostics = compilation.GetDiagnostics(ct);
        foreach (var diag in diagnostics)
        {
            if (ct.IsCancellationRequested) break;
            if (!LocalDiagnosticIds.Contains(diag.Id)) continue;
            if (diag.Location.SourceTree?.FilePath is not { } filePath) continue;
            if (!documentMap.TryGetValue(filePath, out var document)) continue;

            await ProcessDiagnosticAsync(diag, document, context, ct);
        }
    }

    private static async Task ProcessDiagnosticAsync(
        Diagnostic diag,
        Document document,
        DeadCodeScanContext context,
        CancellationToken ct)
    {
        var span = diag.Location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var column = span.StartLinePosition.Character + 1;
        var relativePath = PathNormalizer.ToRelative(context.SolutionDir, span.Path);

        if (context.DeadSymbols.Any(s => s.File.Equals(relativePath, StringComparison.OrdinalIgnoreCase) && s.Line == line))
        {
            return;
        }

        var (symbolName, containerType, kind, id) = await ResolveDiagnosticSymbolAsync(diag, document, ct);

        var entry = new DeadCodeEntry(
            Id: id,
            Kind: kind,
            ContainerType: containerType,
            SymbolName: symbolName,
            File: relativePath,
            Line: line,
            Column: column,
            Accessibility: "private",
            Confidence: "high",
            Reason: $"Compiler-Diagnose {diag.Id}: {diag.GetMessage()}",
            LimitsApplies: []);

        context.DeadSymbols.Add(entry);
        context.ScannedCount++;

        if (context.ByKind.TryGetValue(kind, out var count))
            context.ByKind[kind] = count + 1;
        else
            context.ByKind[kind] = 1;
    }

    private static async Task<(string SymbolName, string ContainerType, string Kind, string Id)> ResolveDiagnosticSymbolAsync(
        Diagnostic diag,
        Document document,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        var node = root?.FindNode(diag.Location.SourceSpan);
        var semanticModel = await document.GetSemanticModelAsync(ct);

        ISymbol? symbol = null;
        if (node != null && semanticModel != null)
        {
            symbol = semanticModel.GetDeclaredSymbol(node, ct) ?? semanticModel.GetSymbolInfo(node, ct).Symbol;
        }

        var symbolName = symbol?.Name ?? (node?.ToString() ?? diag.Id);
        var containerType = symbol?.ContainingType?.ToDisplayString() ?? symbol?.ContainingNamespace?.ToDisplayString() ?? "";
        var kind = symbol != null ? FindDeadCodeScanner.GetSymbolKindString(symbol) : "field";
        var id = symbol?.ToDisplayString() ?? $"{containerType}.{symbolName}";

        return (symbolName, containerType, kind, id);
    }
}
