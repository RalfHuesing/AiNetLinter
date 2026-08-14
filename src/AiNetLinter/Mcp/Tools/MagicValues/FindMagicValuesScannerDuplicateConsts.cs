#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Partial-Class-Erweiterung von <see cref="FindMagicValuesScanner"/>: enthaelt die
/// <c>constant_candidates</c>-Heuristik fuer duplizierte <c>private const</c>-Felder.
/// Aus der Hauptdatei in eine eigene Datei extrahiert, damit
/// <see cref="FindMagicValuesScanner"/> unter dem <c>MaxLineCount: 500</c>-Limit bleibt
/// (siehe <c>AiNetLinter.mdc</c>).
/// </summary>
internal static partial class FindMagicValuesScanner
{
    /// <summary>Loest die <c>constant_candidates</c>-Heuristik fuer duplizierte
    /// <c>private const</c>-Felder auf: iteriert alle <c>Project</c>s/Document/s,
    /// sammelt <c>FieldDeclarationSyntax</c> mit <c>const</c>-Modifier, gruppiert
    /// nach (Type, Value) und meldet jede Gruppe mit ≥ 2 Vorkommen in ≥ 2 verschiedenen
    /// Dateien. Hinweis: der <see cref="MagicValuesClassifier.Classify"/>-Pfad wird
    /// bewusst NICHT durchlaufen, weil <c>const</c>-Definitionen semantisch keine
    /// "Literale" sind — die Suppression-Pruefung auf einem <c>LiteralExpressionSyntax</c>
    /// wuerde hier nicht greifen, was methodisch sauber ist (Definition != Anwendung).</summary>
    private static async Task DetectDuplicateConstFieldsAsync(
        List<RawMagicValue> sink, Solution solution, CancellationToken ct)
    {
        var groups = new Dictionary<(string Type, string Value), List<DuplicateConstEntry>>(EqualityComparer<(string, string)>.Default);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                ct.ThrowIfCancellationRequested();
                await CollectFromDocumentAsync(document, solutionDir, groups, ct).ConfigureAwait(false);
            }
        }

        EmitDuplicateConstGroups(sink, groups);
    }

    /// <summary>Laedt die Syntax-Tree fuer ein einzelnes Document, filtert per
    /// <see cref="IsProcessableDocument"/>, und sammelt Const-Feld-Duplikate. Aus
    /// <see cref="DetectDuplicateConstFieldsAsync"/> extrahiert, um dessen kognitive
    /// Komplexitaet unter dem 15-Limit zu halten.</summary>
    private static async Task CollectFromDocumentAsync(
        Document document,
        string solutionDir,
        Dictionary<(string Type, string Value), List<DuplicateConstEntry>> groups,
        CancellationToken ct)
    {
        if (!IsProcessableDocument(document)) return;

        var tree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
        if (tree is null) return;
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        if (root.ContainsDiagnostics) return;

        var filePath = solutionDir.Length == 0
            ? document.FilePath!
            : Path.GetRelativePath(solutionDir, document.FilePath!).Replace('\\', '/');

        CollectDuplicateConstFields(root, filePath, groups);
    }

    /// <summary>Prueft, ob ein Document fuer die Const-Duplikat-Sammlung in Frage kommt
    /// (regulaerer Source-Code, .cs-Endung).</summary>
    private static bool IsProcessableDocument(Document document)
    {
        if (document.SourceCodeKind != SourceCodeKind.Regular) return false;
        if (document.FilePath is null) return false;
        if (!document.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Schreibt pro Const-Duplikat-Gruppe mit ≥ 2 Vorkommen in ≥ 2 verschiedenen
    /// Dateien je einen <see cref="RawMagicValue"/> in den Sink. Aus
    /// <see cref="DetectDuplicateConstFieldsAsync"/> extrahiert, um dessen kognitive
    /// Komplexitaet unter dem 15-Limit zu halten.</summary>
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
        return new RawMagicValue(
            entry.FilePath, entry.Line, entry.Column,
            MagicValueValueType.Number, key.Value, classification);
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
