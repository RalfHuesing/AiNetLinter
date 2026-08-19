#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Erkennt duplizierte const-Felder (constant_candidates) über mehrere Dokumente.
/// </summary>
internal static class DuplicateConstScanner
{
    internal static async Task DetectDuplicateConstFieldsAsync(
        List<RawMagicValue> sink,
        IReadOnlyList<(Document Document, string FilePath)> matchingDocuments,
        CancellationToken ct)
    {
        var groups = new Dictionary<(string Type, string Value), List<DuplicateConstEntry>>(EqualityComparer<(string, string)>.Default);

        foreach (var (document, filePath) in matchingDocuments)
        {
            ct.ThrowIfCancellationRequested();
            await CollectFromDocumentAsync(document, filePath, groups, ct).ConfigureAwait(false);
        }

        EmitDuplicateConstGroups(sink, groups);
    }

    private static async Task CollectFromDocumentAsync(
        Document document,
        string filePath,
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups,
        CancellationToken ct)
    {
        if (document.SourceCodeKind != SourceCodeKind.Regular) return;

        var tree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
        if (tree is null) return;
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        if (root.ContainsDiagnostics) return;

        CollectDuplicateConstFields(root, filePath, groups);
    }

    private static void EmitDuplicateConstGroups(
        List<RawMagicValue> sink,
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups)
    {
        const int MinDifferentFiles = 2;
        foreach (var (key, entries) in groups)
        {
            if (!HasEnoughDistinctFiles(entries, MinDifferentFiles)) continue;
            var recommendation = BuildDuplicateConstRecommendation(entries);
            foreach (var entry in entries)
            {
                sink.Add(BuildDuplicateConstRawValue(entry, key, recommendation));
            }
        }
    }

    private static bool HasEnoughDistinctFiles(List<DuplicateConstEntry> entries, int minDifferentFiles)
    {
        return entries.Select(e => e.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() >= minDifferentFiles;
    }

    private static string BuildDuplicateConstRecommendation(List<DuplicateConstEntry> entries)
    {
        var fileList = string.Join(", ", entries
            .Select(e => e.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        return $"Hochstufung in eine gemeinsame Konstanten-Klasse (aktuell dupliziert in: {fileList})";
    }

    private static RawMagicValue BuildDuplicateConstRawValue(
        DuplicateConstEntry entry,
        (string Type, string Value) key,
        string recommendation)
    {
        var classification = new MagicValueClassification(
            true,
            MagicValueCategory.ConstantCandidates,
            recommendation,
            $"Dupliziertes const-Feld '{entry.FieldName}' ({key.Type} = {key.Value})");
        var valueType = key.Type.Contains("string", StringComparison.OrdinalIgnoreCase)
            ? MagicValueValueType.String
            : MagicValueValueType.Number;
        return new RawMagicValue(
            entry.FilePath, entry.Line, entry.Column,
            valueType, key.Value, classification);
    }

    private static void CollectDuplicateConstFields(
        SyntaxNode root,
        string filePath,
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups)
    {
        foreach (var fieldDecl in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!IsConstFieldDeclaration(fieldDecl)) continue;
            var typeText = fieldDecl.Declaration.Type.ToString();
            if (string.IsNullOrEmpty(typeText)) continue;

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                TryAddVariableToGroups(variable, fieldDecl, typeText, filePath, groups);
            }
        }
    }

    private static bool IsConstFieldDeclaration(FieldDeclarationSyntax fieldDecl)
    {
        return fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
    }

    private static void TryAddVariableToGroups(
        VariableDeclaratorSyntax variable,
        FieldDeclarationSyntax fieldDecl,
        string typeText,
        string filePath,
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups)
    {
        if (variable.Initializer?.Value is not LiteralExpressionSyntax literal) return;
        var value = literal.Token.ValueText;
        if (string.IsNullOrEmpty(value)) return;

        var entry = CreateDuplicateConstEntry(variable, fieldDecl, literal, filePath);
        if (entry is null) return;
        AddToGroups(groups, (typeText, value), entry);
    }

    private static DuplicateConstEntry? CreateDuplicateConstEntry(
        VariableDeclaratorSyntax variable,
        FieldDeclarationSyntax fieldDecl,
        LiteralExpressionSyntax literal,
        string filePath)
    {
        var containingType = fieldDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Identifier.ValueText
            ?? "(global)";
        var lineSpan = literal.GetLocation().GetLineSpan();
        var line = lineSpan.StartLinePosition.Line + 1;
        var column = lineSpan.StartLinePosition.Character + 1;
        return new DuplicateConstEntry(variable.Identifier.ValueText, filePath, containingType, line, column);
    }

    private static void AddToGroups(
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups,
        (string Type, string Value) key,
        DuplicateConstEntry entry)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<DuplicateConstEntry>();
            groups[key] = list;
        }
        list.Add(entry);
    }

    private sealed record DuplicateConstEntry(
        string FieldName,
        string FilePath,
        string ClassName,
        int Line,
        int Column);
}
