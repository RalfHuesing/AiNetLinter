#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Generiert ein Repository-Playbook (.md) mit Suppression-Statistiken und Architekturmustern.
/// </summary>
public sealed class RepoPlaybookGenerator
{
    private const string DisableMarker = "ainetlinter-disable";
    private const string AllKeyword = "all";
    private const string MultiLineCommentEnd = "*/";

    private static readonly IReadOnlyDictionary<string, string> RuleDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EnforceSealedClasses"] = "Konkrete Klassen muessen 'sealed' sein (oder 'sealed partial').",
            ["EnforceNoSilentCatch"] = "Exceptions duerfen nicht stumm abgefangen werden (Ausnahme: Variable heisst 'ignored' oder 'expected').",
            ["MaxLineCount"] = "Dateizeilenlimit (max. 500 Zeilen) ueberschritten.",
            ["MaxMethodParameterCount"] = "Methode hat zu viele Parameter (max. 4). Kapselung in einen 'record' erforderlich.",
            ["MaxMethodLineCount"] = "Methode hat zu viele Codezeilen (max. 42 Zeilen).",
            ["MaxCyclomaticComplexity"] = "Zu hohe zyklomatische Komplexitaet (max. 5).",
            ["MaxCognitiveComplexity"] = "Zu hohe kognitive Komplexitaet (max. 5).",
            ["ForbiddenNamespaceDependency"] = "Unerlaubte Namespace-Abhaengigkeit gemaess Architektur-Regeln.",
            ["EnforcePascalCase"] = "PascalCase fuer oeffentliche Bezeichner erforderlich.",
            ["EnforceXmlDocumentation"] = "Fehlende XML-Dokumentation fuer oeffentliche Schnittstellen.",
            ["EnforceSemanticNaming"] = "Generische Namen (data, temp, obj) sind in oeffentlichen Signaturen verboten.",
            ["EnforceNullableEnable"] = "#nullable enable fehlt am Dateianfang.",
            ["AllowDynamic"] = "'dynamic' ist verboten. Nutze statische Typen.",
            ["AllowOutParameters"] = "'out'-Parameter sind verboten. Benutze Tuples oder Records.",
            ["StaticTestSentinel"] = "Fehlende Testabdeckung (Unit-Test) fuer komplexe Klasse.",
            ["EnforceResultPatternOverExceptions"] = "Fachlicher Kontrollfluss muss Result-Pattern statt Exceptions (throw) nutzen.",
            ["EnforceNoVariableShadowing"] = "Verdecken von aeusseren Feldern/Properties durch lokale Variablen verboten.",
            ["EnforceReadonlyParameters"] = "Zuweisung an Methodenschnittstellen-Parameter verboten.",
            ["EnforceReadonlyFields"] = "Private Felder, die nur im Konstruktor zugewiesen werden, muessen readonly sein.",
            ["EnforceNoMagicValues"] = "Magic Numbers/Strings verboten (Ausnahmen: 0, 1, \"\").",
            ["MaxMethodOverloads"] = "Zu viele Methodenueberladungen (max. 10).",
            ["MaxConstructorDependencies"] = "Zu viele Konstruktorabhaengigkeiten (max. 20).",
            ["AIContextFootprint"] = "AI-Context-Footprint (transitive Codezeilen aller Abhaengigkeiten) ueberschreitet Limit (max. 5000).",
            ["all"] = "Alle Linter-Regeln deaktiviert fuer diese Datei oder diesen Bereich."
        };


    /// <summary>
    /// Generiert das Playbook und schreibt es in die angegebene Datei.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn-Solution.</param>
    /// <param name="outputPath">Der Pfad zur Ausgabedatei (.md).</param>
    /// <param name="verbose">Aktiviert detailliertes Protokoll-Logging.</param>
    /// <returns>Ein Task-Objekt für asynchrone Ausführung.</returns>
    public static async Task GenerateAsync(Solution solution, string outputPath, bool verbose)
    {
        var stats = await ScanSolutionAsync(solution);
        await WritePlaybookFileAsync(outputPath, stats, verbose);
    }

    private static async Task<PlaybookStats> ScanSolutionAsync(Solution solution)
    {
        int totalResultMethods = 0;
        int totalThrows = 0;
        var suppressionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            var projectStats = await ScanProjectAsync(project, suppressionCounts);
            totalResultMethods += projectStats.ResultMethods;
            totalThrows += projectStats.Throws;
        }

        return new PlaybookStats(totalResultMethods, totalThrows, suppressionCounts);
    }

    private static async Task<(int ResultMethods, int Throws)> ScanProjectAsync(Project project, Dictionary<string, int> suppressionCounts)
    {
        int totalResultMethods = 0;
        int totalThrows = 0;

        foreach (var document in project.Documents)
        {
            var docStats = await ScanDocumentAsync(document, suppressionCounts);
            totalResultMethods += docStats.ResultMethods;
            totalThrows += docStats.Throws;
        }

        return (totalResultMethods, totalThrows);
    }

    private static async Task<(int ResultMethods, int Throws)> ScanDocumentAsync(Document document, Dictionary<string, int> suppressionCounts)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (semanticModel == null || syntaxRoot == null)
        {
            return (0, 0);
        }

        var walker = new PlaybookSyntaxWalker(semanticModel);
        walker.Visit(syntaxRoot);

        CollectSuppressionsFromTrivia(syntaxRoot, suppressionCounts);

        return (walker.ResultPatternCount, walker.ThrowCount);
    }

    private static void CollectSuppressionsFromTrivia(SyntaxNode root, Dictionary<string, int> suppressionCounts)
    {
        var triviaList = root.DescendantTrivia();
        foreach (var trivia in triviaList)
        {
            var kind = trivia.Kind();
            if (kind == SyntaxKind.SingleLineCommentTrivia || kind == SyntaxKind.MultiLineCommentTrivia)
            {
                ProcessComment(trivia.ToString(), suppressionCounts);
            }
        }
    }

    private static void ProcessComment(string commentText, Dictionary<string, int> suppressionCounts)
    {
        var idx = commentText.IndexOf(DisableMarker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return;
        }

        var suffix = commentText.Substring(idx + DisableMarker.Length).Trim();
        var rule = GetRuleNameFromSuffix(suffix);

        lock (suppressionCounts)
        {
            suppressionCounts[rule] = suppressionCounts.TryGetValue(rule, out var currentCount) ? currentCount + 1 : 1;
        }
    }

    private static string GetRuleNameFromSuffix(string suffix)
    {
        if (string.IsNullOrEmpty(suffix) || suffix.StartsWith(AllKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return AllKeyword;
        }

        var parts = suffix.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return AllKeyword;
        }

        var rule = parts[0].Trim();
        if (rule.EndsWith(MultiLineCommentEnd, StringComparison.Ordinal))
        {
            rule = rule.Substring(0, rule.Length - MultiLineCommentEnd.Length).Trim();
        }

        return rule.TrimEnd(',', ';', '.', '\'', '"');
    }

    private static async Task WritePlaybookFileAsync(string outputPath, PlaybookStats stats, bool verbose)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!-- Auto-generated by AiNetLinter. Do not edit manually. -->");
        sb.AppendLine("# AI Repository Playbook (Auto-Generated)");
        sb.AppendLine("Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.");
        sb.AppendLine("Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.");
        sb.AppendLine();
        sb.AppendLine("## 1. Genutzte Architekturmuster");
        sb.AppendLine($"- **Result-Pattern-Nutzung:** {stats.TotalResultMethods} Methoden liefern `Result` oder `Result<T>` zurueck.");
        sb.AppendLine($"- **Kontrollfluss-Exceptions:** {stats.TotalThrows} `throw`-Anweisungen wurden im Code-Rumpf gefunden.");
        sb.AppendLine();
        sb.AppendLine("## 2. Abweichungen / Unterdrueckte Linter-Regeln");

        AppendSuppressionList(sb, stats.SuppressionCounts);

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);

        if (verbose)
        {
            Console.WriteLine($"[INFO]: Repo-Playbook erfolgreich generiert unter: {outputPath}");
        }
    }

    private static void AppendSuppressionList(StringBuilder sb, Dictionary<string, int> suppressionCounts)
    {
        if (suppressionCounts.Count == 0)
        {
            sb.AppendLine("In dieser Codebase sind aktuell keine Linter-Regeln unterdrueckt.");
            return;
        }

        sb.AppendLine("Folgende Regeln werden in diesem Projekt bewusst unterdrueckt:");
        sb.AppendLine();
        foreach (var item in suppressionCounts.OrderByDescending(x => x.Value))
        {
            var rule = item.Key;
            var count = item.Value;
            var description = RuleDescriptions.TryGetValue(rule, out var desc) ? desc : $"Regel '{rule}'.";
            sb.AppendLine($"- **{rule}:** {count} mal deaktiviert.");
            sb.AppendLine($"  *Bedeutung:* {description}");
        }
    }

    private sealed record PlaybookStats(
        int TotalResultMethods,
        int TotalThrows,
        Dictionary<string, int> SuppressionCounts
    );

    private sealed class PlaybookSyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;

        public int ResultPatternCount { get; private set; }
        public int ThrowCount { get; private set; }

        public PlaybookSyntaxWalker(SemanticModel semanticModel) : base(SyntaxWalkerDepth.Node)
        {
            _semanticModel = semanticModel;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(node);
            if (symbol != null && IsOrContainsResult(symbol.ReturnType))
            {
                ResultPatternCount++;
            }
            base.VisitMethodDeclaration(node);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            ThrowCount++;
            base.VisitThrowStatement(node);
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            ThrowCount++;
            base.VisitThrowExpression(node);
        }

        private static bool IsOrContainsResult(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Name == "Result")
            {
                return true;
            }

            if (typeSymbol is INamedTypeSymbol namedType)
            {
                return IsGenericResultWrapper(namedType);
            }

            return false;
        }

        private static bool IsGenericResultWrapper(INamedTypeSymbol namedType)
        {
            if (!namedType.IsGenericType)
            {
                return false;
            }

            if (namedType.Name != "Task" && namedType.Name != "ValueTask")
            {
                return false;
            }

            var innerType = namedType.TypeArguments.FirstOrDefault();
            return innerType != null && IsOrContainsResult(innerType);
        }
    }
}
