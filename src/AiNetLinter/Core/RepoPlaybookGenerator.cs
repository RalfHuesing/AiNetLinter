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
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using AiNetLinter.Models;

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

    private sealed record PlaybookDocInfo(
        string FilePath,
        string ProjectName,
        bool HasDisableAll,
        int LineCount,
        List<string> Namespaces
    );

    private sealed record PlaybookDocScanResult(
        int ResultMethods,
        int Throws,
        bool HasDisableAll,
        int LineCount,
        List<string> Namespaces
    );

    private sealed record PlaybookStats(
        int TotalResultMethods,
        int TotalThrows,
        Dictionary<string, int> SuppressionCounts,
        List<PlaybookDocInfo> DocInfos,
        List<RuleViolation> Violations
    );

    /// <summary>
    /// Generiert das Playbook und schreibt es in die angegebene Datei.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn-Solution.</param>
    /// <param name="outputPath">Der Pfad zur Ausgabedatei (.md).</param>
    /// <param name="verbose">Aktiviert detailliertes Protokoll-Logging.</param>
    /// <param name="config">Die globale Linter-Konfiguration.</param>
    /// <param name="configPath">Der Pfad zur Konfigurationsdatei.</param>
    /// <param name="precomputedViolations">Optional bereits berechnete Linter-Regelverstöße zur Vermeidung doppelter Analyse.</param>
    /// <returns>Ein Task-Objekt für asynchrone Ausführung.</returns>
    public static async Task GenerateAsync(
        Solution solution,
        string outputPath,
        bool verbose,
        LinterConfig? config = null,
        string configPath = "rules.json",
        IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
    {
        var content = await BuildContentAsync(solution, verbose, config, configPath, precomputedViolations);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(outputPath) && await File.ReadAllTextAsync(outputPath, Encoding.UTF8) == content)
        {
            if (verbose)
            {
                Console.WriteLine($"[INFO]: Repo-Playbook ist bereits aktuell (kein Schreibzugriff): {outputPath}");
            }
            return;
        }

        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Repo-Playbook erfolgreich generiert unter: {outputPath}");
        }
    }

    /// <summary>
    /// Generiert den Playbook-Inhalt als String (ohne Datei zu schreiben).
    /// Für den --check-Modus und Tests.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn-Solution.</param>
    /// <param name="verbose">Aktiviert detailliertes Protokoll-Logging.</param>
    /// <param name="config">Die globale Linter-Konfiguration.</param>
    /// <param name="configPath">Der Pfad zur Konfigurationsdatei.</param>
    /// <param name="precomputedViolations">Optional bereits berechnete Linter-Regelverstöße.</param>
    /// <returns>Der generierte Markdown-Inhalt.</returns>
    public static async Task<string> BuildContentAsync(
        Solution solution,
        bool verbose,
        LinterConfig? config = null,
        string configPath = "rules.json",
        IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
    {
        var stats = await ScanSolutionAsync(solution, config, configPath, precomputedViolations);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        var version = typeof(RepoPlaybookGenerator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        return BuildContent(stats, solutionDir, config, configPath, version);
    }

    private static async Task<PlaybookStats> ScanSolutionAsync(
        Solution solution,
        LinterConfig? config,
        string configPath,
        IReadOnlyCollection<RuleViolation>? precomputedViolations = null)
    {
        int totalResultMethods = 0;
        int totalThrows = 0;
        var suppressionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var docInfos = new List<PlaybookDocInfo>();

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                var docScan = await ScanDocumentAsync(document, suppressionCounts, config);
                totalResultMethods += docScan.ResultMethods;
                totalThrows += docScan.Throws;
                
                docInfos.Add(new PlaybookDocInfo(
                    document.FilePath ?? string.Empty,
                    project.Name,
                    docScan.HasDisableAll,
                    docScan.LineCount,
                    docScan.Namespaces
                ));
            }
        }

        List<RuleViolation> violations = new();
        if (config != null)
        {
            if (precomputedViolations != null)
            {
                violations.AddRange(precomputedViolations);
            }
            else
            {
                string? rulesJsonContent = null;
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                {
                    rulesJsonContent = File.ReadAllText(configPath, Encoding.UTF8);
                }
                var engine = new LinterEngine(config, rulesJsonContent);
                var results = await engine.RunAsync(solution);
                violations.AddRange(results);
            }
        }

        return new PlaybookStats(totalResultMethods, totalThrows, suppressionCounts, docInfos, violations);
    }

    private static async Task<PlaybookDocScanResult> ScanDocumentAsync(Document document, Dictionary<string, int> suppressionCounts, LinterConfig? config)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (semanticModel == null || syntaxRoot == null)
        {
            return new PlaybookDocScanResult(0, 0, false, 0, []);
        }

        var effectiveConfig = config != null ? ProjectConfigResolver.ResolveForDocument(document, config) : null;
        var allowedExceptions = effectiveConfig?.Global?.AllowedExceptions;

        var walker = new PlaybookSyntaxWalker(semanticModel, allowedExceptions);
        walker.Visit(syntaxRoot);

        var text = (await document.GetTextAsync()).ToString();
        bool hasDisableAll = text.Contains("ainetlinter-disable all");

        CollectSuppressionsFromTrivia(syntaxRoot, suppressionCounts);

        var namespaces = syntaxRoot.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString())
            .Distinct()
            .ToList();

        int lineCount = syntaxRoot.GetText().Lines.Count;

        return new PlaybookDocScanResult(walker.ResultPatternCount, walker.ThrowCount, hasDisableAll, lineCount, namespaces);
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

        suppressionCounts[rule] = suppressionCounts.TryGetValue(rule, out var currentCount) ? currentCount + 1 : 1;
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

    private static string BuildContent(PlaybookStats stats, string solutionDir, LinterConfig? config, string configPath, string version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("description: Repo-Statistik, bei Architektur-Fragen lesen");
        sb.AppendLine("globs: ");
        sb.AppendLine("alwaysApply: false");
        sb.AppendLine("---");
        sb.AppendLine("# AI Repository Playbook (Auto-Generated)");
        sb.AppendLine($"Auto-generiert durch AiNetLinter {version} aus `{configPath}`.");
        sb.AppendLine("Dieses Dokument wurde automatisiert durch den **AiNetLinter** erzeugt.");
        sb.AppendLine("Es dient als Orientierungshilfe fuer KI-Assistenten (wie Cursor), um sich an die Codierungsrichtlinien, Architekturmuster und Ausnahmen dieser Codebase anzupassen.");
        sb.AppendLine();
        sb.AppendLine("## 1. Genutzte Architekturmuster");
        sb.AppendLine($"- **Result-Pattern-Nutzung:** {stats.TotalResultMethods} Methoden liefern `Result` oder `Result<T>` zurueck.");
        sb.AppendLine($"- **Kontrollfluss-Exceptions:** {stats.TotalThrows} `throw`-Anweisungen wurden im Code-Rumpf gefunden.");
        sb.AppendLine();
        sb.AppendLine("## 2. Abweichungen / Unterdrueckte Linter-Regeln");

        AppendSuppressionList(sb, stats.SuppressionCounts);
        sb.AppendLine();

        if (config != null)
        {
            sb.AppendLine("## 3. Migrations-Status");
            sb.AppendLine();

            int totalFiles = stats.DocInfos.Count;
            int waveReadyFiles = stats.DocInfos.Count(d => !d.HasDisableAll);
            double waveReadyPercentage = totalFiles > 0 ? (double)waveReadyFiles / totalFiles * 100 : 0;

            sb.AppendLine($"- **Wave-ready Dateien:** {waveReadyFiles} / {totalFiles} ({waveReadyPercentage:F0} %)");

            var filesWithDisableAll = stats.DocInfos.Where(d => d.HasDisableAll).Select(d => d.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var waveReadyViolations = stats.Violations.Where(v => !filesWithDisableAll.Contains(v.FilePath)).ToList();

            sb.AppendLine($"- **Verstösse nur wave-ready (default rules):** {waveReadyViolations.Count}");
            sb.AppendLine($"- **Top-Ordner wave-ready-Verstöße:**");

            var folderGroups = waveReadyViolations
                .Select(v =>
                {
                    if (string.IsNullOrEmpty(v.FilePath) || string.IsNullOrEmpty(solutionDir))
                    {
                        return "Root";
                    }
                    var rel = PathNormalizer.ToRelative(solutionDir, v.FilePath);
                    var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
                    return string.IsNullOrEmpty(dir) ? "Root" : dir;
                })
                .GroupBy(d => d)
                .Select(g => new { Folder = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(3)
                .ToList();

            if (folderGroups.Count == 0)
            {
                sb.AppendLine("  - Keine offenen Verstöße in wave-ready Dateien.");
            }
            else
            {
                foreach (var folder in folderGroups)
                {
                    sb.AppendLine($"  - `{folder.Folder}/`: {folder.Count}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("## 4. Architektur-Slices (nach Ordner)");
            sb.AppendLine();

            var sliceGroups = stats.DocInfos
                .Select(d => new { Doc = d, Slice = GetFolderSlice(d.FilePath, solutionDir) })
                .GroupBy(x => x.Slice)
                .Select(g =>
                {
                    var docs = g.Select(x => x.Doc).ToList();
                    var sortedLocs = docs.Select(d => d.LineCount).OrderBy(x => x).ToList();
                    int medianLoc = sortedLocs.Count > 0 ? sortedLocs[sortedLocs.Count / 2] : 0;
                    int disableAllCount = docs.Count(d => d.HasDisableAll);
                    return new { Slice = g.Key, FileCount = docs.Count, MedianLoc = medianLoc, DisableAllCount = disableAllCount };
                })
                .OrderByDescending(x => x.FileCount)
                .Take(5)
                .ToList();

            foreach (var slice in sliceGroups)
            {
                var disableAllStr = slice.DisableAllCount > 0 ? $", {slice.DisableAllCount}× disable-all" : "";
                sb.AppendLine($"- **{slice.Slice}/**: {slice.FileCount} files, median Footprint {slice.MedianLoc} LOC{disableAllStr}");
            }
            sb.AppendLine();

            sb.AppendLine("## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)");
            sb.AppendLine();
            sb.AppendLine("| Intent | Offene Verstöße (wave-ready) | Regeln |");
            sb.AppendLine("| :--- | ---: | :--- |");

            var intentGroups = waveReadyViolations
                .GroupBy(v => RuleMetadataRegistry.Resolve(v.RuleName ?? "", config).Intent)
                .Select(g => new
                {
                    Intent = g.Key,
                    Count = g.Count(),
                    Rules = string.Join(", ", g.Select(v => v.RuleName).Distinct())
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (intentGroups.Count == 0)
            {
                sb.AppendLine("| - | 0 | Keine offenen Verstöße |");
            }
            else
            {
                foreach (var group in intentGroups)
                {
                    sb.AppendLine($"| {group.Intent} | {group.Count} | {group.Rules} |");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetFolderSlice(string filePath, string solutionDir)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(solutionDir))
        {
            return "Root";
        }
        var rel = PathNormalizer.ToRelative(solutionDir, filePath).Replace('\\', '/');
        var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return string.Join("/", parts.Take(2));
        return parts.Length == 1 ? parts[0] : "Root";
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

    private sealed class PlaybookSyntaxWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly IReadOnlyCollection<string>? _allowedExceptions;

        public int ResultPatternCount { get; private set; }
        public int ThrowCount { get; private set; }

        public PlaybookSyntaxWalker(SemanticModel semanticModel, IReadOnlyCollection<string>? allowedExceptions) : base(SyntaxWalkerDepth.Node)
        {
            _semanticModel = semanticModel;
            _allowedExceptions = allowedExceptions;
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

        private bool IsProjectInternal(ITypeSymbol typeSymbol)
        {
            return SymbolEqualityComparer.Default.Equals(
                typeSymbol.ContainingAssembly,
                _semanticModel.Compilation.Assembly);
        }

        public override void VisitThrowStatement(ThrowStatementSyntax node)
        {
            if (!IsAllowedException(node.Expression))
            {
                ThrowCount++;
            }
            base.VisitThrowStatement(node);
        }

        public override void VisitThrowExpression(ThrowExpressionSyntax node)
        {
            if (!IsAllowedException(node.Expression))
            {
                ThrowCount++;
            }
            base.VisitThrowExpression(node);
        }

        private bool IsAllowedException(ExpressionSyntax? expression)
        {
            if (expression is not ObjectCreationExpressionSyntax creation) return false;
            if (_allowedExceptions == null) return false;

            var typeSymbol = _semanticModel.GetTypeInfo(creation).Type;
            if (typeSymbol == null) return false;

            return _allowedExceptions.Contains(typeSymbol.Name);
        }

        private bool IsOrContainsResult(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Name == "Result")
            {
                return true;
            }

            if (IsProjectInternal(typeSymbol) && typeSymbol.Name.EndsWith("Result", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (typeSymbol is INamedTypeSymbol namedType)
            {
                return IsGenericResultWrapper(namedType);
            }

            return false;
        }

        private bool IsGenericResultWrapper(INamedTypeSymbol namedType)
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
