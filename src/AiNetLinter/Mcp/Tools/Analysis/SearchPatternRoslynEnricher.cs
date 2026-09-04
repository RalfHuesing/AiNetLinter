#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Serilog;

namespace AiNetLinter.Mcp.Tools.Analysis;

internal static class SearchPatternRoslynEnricher
{
    internal static async Task<IReadOnlyList<SearchPatternMatch>> EnrichAsync(
        Solution solution,
        IReadOnlyList<SearchPatternMatch> matches,
        CancellationToken ct)
    {
        var documents = BuildDocumentIndex(solution);
        var snapshots = new Dictionary<string, Task<SearchPatternRoslynSnapshot>>(
            StringComparer.OrdinalIgnoreCase);
        var enriched = new List<SearchPatternMatch>(matches.Count);

        foreach (var match in matches)
        {
            ct.ThrowIfCancellationRequested();
            var semantic = await EnrichMatchAsync(
                solution,
                documents,
                snapshots,
                match,
                ct).ConfigureAwait(false);
            enriched.Add(match with { Semantic = semantic });
        }

        return enriched;
    }

    private static async Task<SearchPatternSemantic> EnrichMatchAsync(
        Solution solution,
        IReadOnlyDictionary<string, IReadOnlyList<SearchPatternRoslynDocument>> documents,
        IDictionary<string, Task<SearchPatternRoslynSnapshot>> snapshots,
        SearchPatternMatch match,
        CancellationToken ct)
    {
        if (!IsCSharpPath(match.FilePath)) return NotApplicable();
        var absolutePath = GetAbsolutePath(solution, match.FilePath);
        if (absolutePath is null) return Unavailable();
        var canonicalPath = CanonicalPath(absolutePath);
        if (!documents.TryGetValue(canonicalPath, out var candidates)) return Unavailable();

        var selected = SelectDocument(candidates, match.ProjectName);
        if (selected is null) return candidates.Count > 1 ? Ambiguous() : Unavailable();

        if (!snapshots.TryGetValue(canonicalPath, out var snapshotTask))
        {
            snapshotTask = LoadSnapshotAsync(selected.Document, ct);
            snapshots.Add(canonicalPath, snapshotTask);
        }

        var snapshot = await snapshotTask.ConfigureAwait(false);
        return AnalyzeSnapshot(snapshot, match, ct);
    }

    private static Dictionary<string, IReadOnlyList<SearchPatternRoslynDocument>> BuildDocumentIndex(
        Solution solution)
    {
        var index = new Dictionary<string, List<SearchPatternRoslynDocument>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.FilePath)) continue;
                var path = CanonicalPath(document.FilePath!);
                if (!index.TryGetValue(path, out var entries))
                {
                    entries = [];
                    index.Add(path, entries);
                }

                entries.Add(new SearchPatternRoslynDocument(project.Name, document));
            }
        }

        return index.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<SearchPatternRoslynDocument>)entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static SearchPatternRoslynDocument? SelectDocument(
        IReadOnlyList<SearchPatternRoslynDocument> candidates,
        string? projectName)
    {
        var selected = string.IsNullOrEmpty(projectName)
            ? candidates
            : candidates.Where(candidate => string.Equals(
                candidate.ProjectName,
                projectName,
                StringComparison.Ordinal)).ToArray();
        return selected.Count == 1 ? selected[0] : null;
    }

    private static async Task<SearchPatternRoslynSnapshot> LoadSnapshotAsync(
        Document document,
        CancellationToken ct)
    {
        try
        {
            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
            if (root is null || semanticModel is null) return SearchPatternRoslynSnapshot.Unavailable;
            return new(root, semanticModel, root.SyntaxTree.GetText(ct));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning(exception, "Roslyn-Snapshot fuer search_pattern nicht verfuegbar (Datei={FilePath})", document.FilePath);
            return SearchPatternRoslynSnapshot.Unavailable;
        }
    }

    private static SearchPatternSemantic AnalyzeSnapshot(
        SearchPatternRoslynSnapshot snapshot,
        SearchPatternMatch match,
        CancellationToken ct)
    {
        if (snapshot.Root is null || snapshot.SemanticModel is null || snapshot.Text is null)
        {
            return Unavailable();
        }

        if (match.Line < 1 || match.Line > snapshot.Text.Lines.Count) return Unavailable();
        var line = snapshot.Text.Lines[match.Line - 1];
        if (!string.Equals(line.ToString(), match.LineText, StringComparison.Ordinal)) return Unavailable();

        var semantics = new List<SearchPatternSemantic>(match.MatchRanges.Count);
        foreach (var range in match.MatchRanges)
        {
            if (!TryGetPosition(line, range, snapshot.Text.Length, out var position)) return Unavailable();
            semantics.Add(AnalyzeRange(snapshot.Root, snapshot.SemanticModel, position, ct));
        }

        return Merge(semantics);
    }

    private static bool TryGetPosition(
        TextLine line,
        SearchPatternMatchRange range,
        int textLength,
        out int position)
    {
        position = 0;
        if (range.Column < 1 || range.Length < 0) return false;
        var start = line.Start + range.Column - 1;
        if (start > line.End || start > textLength) return false;
        if (range.Length > line.End - start) return false;
        position = Math.Min(start, Math.Max(line.Start, line.End - 1));
        return textLength > 0;
    }

    private static SearchPatternSemantic AnalyzeRange(
        SyntaxNode root,
        SemanticModel semanticModel,
        int position,
        CancellationToken ct)
    {
        var trivia = root.FindTrivia(position, findInsideTrivia: true);
        if (trivia.IsCommentOrDocTrivia()) return new("comment", "not_applicable", null);

        var token = root.FindToken(position, findInsideTrivia: true);
        if (IsString(token)) return new("string", "not_applicable", null);

        var declaration = FindDeclarationNode(token);
        if (declaration is not null)
        {
            var symbol = semanticModel.GetDeclaredSymbol(declaration, ct);
            return symbol is null ? Unknown() : Resolved("declaration", symbol);
        }

        var reference = FindReferenceNode(token);
        if (reference is null) return new("code", "not_applicable", null);
        return AnalyzeReference(semanticModel, reference, ct);
    }

    private static SearchPatternSemantic AnalyzeReference(
        SemanticModel semanticModel,
        SimpleNameSyntax reference,
        CancellationToken ct)
    {
        var info = semanticModel.GetSymbolInfo(reference, ct);
        if (info.Symbol is not null && info.CandidateSymbols.Length == 0)
        {
            return Resolved("symbol_reference", info.Symbol);
        }

        if (info.Symbol is null && info.CandidateSymbols.Length == 1)
        {
            return Resolved("symbol_reference", info.CandidateSymbols[0]);
        }

        if (info.Symbol is null && info.CandidateSymbols.Length == 0
            && semanticModel.GetTypeInfo(reference, ct).Type is { } type)
        {
            return Resolved("symbol_reference", type);
        }

        return info.CandidateSymbols.Length > 0
            ? Ambiguous()
            : Unknown();
    }

    private static SearchPatternSemantic Merge(IReadOnlyList<SearchPatternSemantic> semantics)
    {
        var distinct = semantics.Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : Ambiguous();
    }

    private static SyntaxNode? FindDeclarationNode(SyntaxToken token)
    {
        if (token.Parent is null) return null;
        return token.Parent.AncestorsAndSelf()
            .FirstOrDefault(node => HasDeclarationIdentifier(node, token));
    }

    private static bool HasDeclarationIdentifier(SyntaxNode node, SyntaxToken token) =>
        node switch
        {
            BaseTypeDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            MethodDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            ConstructorDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            DestructorDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            PropertyDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            EventDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            VariableDeclaratorSyntax declaration => SameSpan(declaration.Identifier, token),
            EnumMemberDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            ParameterSyntax declaration => SameSpan(declaration.Identifier, token),
            LocalFunctionStatementSyntax declaration => SameSpan(declaration.Identifier, token),
            DelegateDeclarationSyntax declaration => SameSpan(declaration.Identifier, token),
            _ => false,
        };

    private static SimpleNameSyntax? FindReferenceNode(SyntaxToken token) =>
        token.Parent?.AncestorsAndSelf().OfType<SimpleNameSyntax>().FirstOrDefault();

    private static bool IsString(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StringLiteralToken)
        || token.IsKind(SyntaxKind.InterpolatedStringTextToken);

    private static bool SameSpan(SyntaxToken left, SyntaxToken right) =>
        left.SpanStart == right.SpanStart && left.Span.Length == right.Span.Length;

    private static string? GetAbsolutePath(Solution solution, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(solution.FilePath)) return null;
        var root = Path.GetDirectoryName(Path.GetFullPath(solution.FilePath));
        return string.IsNullOrEmpty(root)
            ? null
            : Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CanonicalPath(string path) => Path.GetFullPath(path);

    private static bool IsCSharpPath(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static SearchPatternSemantic Resolved(string kind, ISymbol symbol) =>
        new(kind, "resolved", symbol.TryGetDocCommentId());

    private static SearchPatternSemantic NotApplicable() =>
        new("unknown", "not_applicable", null);

    private static SearchPatternSemantic Unknown() =>
        new("unknown", "unknown", null);

    private static SearchPatternSemantic Ambiguous() =>
        new("unknown", "ambiguous", null);

    private static SearchPatternSemantic Unavailable() =>
        new("unknown", "unavailable", null);

    private sealed record SearchPatternRoslynDocument(string ProjectName, Document Document);

    private sealed record SearchPatternRoslynSnapshot(
        SyntaxNode? Root,
        SemanticModel? SemanticModel,
        SourceText? Text)
    {
        internal static SearchPatternRoslynSnapshot Unavailable { get; } = new(null, null, null);
    }
}
