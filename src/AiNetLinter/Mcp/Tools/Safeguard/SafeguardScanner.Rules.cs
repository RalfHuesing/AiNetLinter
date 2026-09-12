#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Safeguard;

internal static partial class SafeguardScanner
{
    /// <summary>Lookup-Tabelle pro bekannter Regel-ID; vermeidet <c>MaxSwitchArms</c>-Verstoss
    /// und ermoeglicht das Hinzufuegen weiterer Regeln ohne Steuerungslogik-Aenderung.
    /// Unbekannte RuleNames erhalten einen generischen Default-Hinweis.</summary>
    private static readonly IReadOnlyDictionary<string, string> RuleHints =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LinterRuleIds.MaxLineCount] =
                "Datei aufteilen — Klassen/Methoden extrahieren, Partial-Klassen pruefen.",
            [LinterRuleIds.MaxMethodLineCount] =
                "Methode aufteilen — Hilfsmethoden extrahieren, Verantwortlichkeit aufspalten.",
            [LinterRuleIds.MaxMethodParameterCount] =
                "Parameter-Record einfuehren — verwandte Argumente in einem Werteobjekt buendeln.",
            [LinterRuleIds.MaxCyclomaticComplexity] =
                "Komplexitaet reduzieren — fruehe Returns, kleinere Methoden, Polymorphie statt Switch.",
            [LinterRuleIds.MaxCognitiveComplexity] =
                "Komplexitaet reduzieren — fruehe Returns, kleinere Methoden, Polymorphie statt Switch.",
            [LinterRuleIds.AIContextFootprint] =
                "Footprint reduzieren — Abhaengigkeiten aufsloesen, kleine Typen favorisieren.",
            [LinterRuleIds.EnforceSealedClasses] =
                "Klasse versiegeln (`sealed`) — Vererbungsabsicht klaeren oder Blatt-Klasse markieren.",
            [LinterRuleIds.MaxConstructorDependencies] =
                "DI-Law-of-Demeter pruefen — Aggregate-Fassade einfuehren, Konstruktor-Injektion reduzieren.",
            [LinterRuleIds.BanAsyncVoid] =
                "Async void durch `async Task` ersetzen — Ausnahmen werden sonst verschluckt.",
            [LinterRuleIds.BanBlockingTaskAccess] =
                "Blocking-Calls (.Wait/.Result/.GetAwaiter().GetResult()) durch `await` ersetzen.",
            [LinterRuleIds.EnforceNoSilentCatch] =
                "Catch-Block sichtbar machen — Log schreiben oder Exception re-throwen.",
        };

    private static string ResolveHintForRule(string ruleName, Config config)
        => RuleHints.TryGetValue(ruleName, out var hint)
            ? hint
            : $"Regel-Verstoss '{ruleName}' pruefen — Details in Docs/linter/configuration.md.";

    private static async Task<IReadOnlyList<ScannedClass>> EnumerateConcreteClassesAsync(
        Solution solution, string? scopeFilter, Config config, string solutionDir, CancellationToken ct)
    {
        var collected = new List<ScannedClass>();
        foreach (var project in solution.Projects)
        {
            var compilation = await TryGetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                if (!SourceFileCatalog.IsValidDocument(document, solutionDir)
                    || !ViolationScopeFilter.MatchesScope(
                        document.FilePath ?? document.Name, project.Name, solutionDir, scopeFilter)
                    || FileFilterEvaluator.IsExcluded(document.FilePath ?? document.Name, config.FileFilters))
                {
                    continue;
                }

                var effectiveConfig = ProjectConfigResolver.ResolveForDocument(document, config, solutionDir);
                collected.AddRange(
                    await CollectClassDeclarationsAsync(document, compilation, effectiveConfig, ct));
            }
        }
        return collected;
    }

    /// <summary>
    /// Liefert die Compilation oder null, wenn das Projekt grundsaetzlich nicht kompilierbar ist
    /// (<c>SupportsCompilation == false</c> — legitimer, erwartbarer Fall, z. B. echtes
    /// Nicht-C#-Projekt). Fuer kompilierbare Projekte wird <see cref="GetCompilationWithRetryAsync"/>
    /// aufgerufen, die transiente Fehlschlaege per Retry abfaengt und einen dauerhaften Fehlschlag
    /// als <see cref="SafeguardCompilationException"/> wirft (von <see cref="ComputeScoreAsync"/>
    /// als Malfunction behandelt).
    /// </summary>
    private static Task<Compilation?> TryGetCompilationAsync(Project project, CancellationToken ct)
    {
        if (!project.SupportsCompilation) return Task.FromResult<Compilation?>(null);
        return GetCompilationWithRetryAsync(project.GetCompilationAsync, project.Name, ct);
    }

    /// <summary>
    /// Retried eine Compilation-Beschaffungsfunktion bis zu <see cref="CompilationRetryAttempts"/> mal
    /// (linearer Backoff via <see cref="CompilationRetryBaseDelayMs"/>), um transiente Fehlschlaege
    /// (z. B. MSBuild-/Ressourcen-Kontention unter paralleler Last) von echten, dauerhaften
    /// Compile-Problemen zu unterscheiden. <paramref name="getCompilation"/> statt direkt
    /// <c>Project.GetCompilationAsync</c>, damit die Retry-/Backoff-Logik isoliert von einer echten
    /// Roslyn-<c>Project</c>-Instanz testbar ist (Pattern konsistent mit <see cref="BuildScoreResult"/>).
    /// Wirft nach dem letzten erfolglosen Versuch eine <see cref="SafeguardCompilationException"/>
    /// statt still <c>null</c> zurueckzugeben — ein kompilierbares Projekt, das dauerhaft nicht
    /// kompiliert, darf nicht lautlos aus der Klassen-Aggregation fallen (siehe Determinismus-Hinweis
    /// an <see cref="TryGetCompilationAsync"/>).
    /// </summary>
    internal static async Task<Compilation?> GetCompilationWithRetryAsync(
        Func<CancellationToken, Task<Compilation?>> getCompilation, string projectName, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= CompilationRetryAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var compilation = await getCompilation(ct);
                if (compilation is not null) return compilation;
                lastError = null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt < CompilationRetryAttempts)
            {
                await Task.Delay(CompilationRetryBaseDelayMs * attempt, ct);
            }
        }

        throw new SafeguardCompilationException(
            $"Compilation fuer Projekt '{projectName}' schlug nach {CompilationRetryAttempts} " +
            "Versuchen fehl (SupportsCompilation=true, aber GetCompilationAsync lieferte wiederholt " +
            "keine Compilation).",
            lastError);
    }

    private static async Task<IReadOnlyList<ScannedClass>> CollectClassDeclarationsAsync(
        Document document, Compilation compilation, Config config, CancellationToken ct)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree is null) return Array.Empty<ScannedClass>();

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync(ct);
        var result = new List<ScannedClass>();
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (TryBuildScannedClass(classDecl, semanticModel, config) is { } scanned) result.Add(scanned);
        }
        return result;
    }

    private static ScannedClass? TryBuildScannedClass(
        ClassDeclarationSyntax classDecl, SemanticModel semanticModel, Config config)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (symbol is null || symbol.TypeKind != TypeKind.Class || symbol.IsAbstract) return null;
        return BuildScannedClass(symbol, classDecl, config);
    }

    private static ScannedClass BuildScannedClass(
        INamedTypeSymbol symbol, ClassDeclarationSyntax classDecl, Config config)
    {
        var maxCc = 0;
        foreach (var method in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            maxCc = Math.Max(maxCc, ComplexityCalculator.GetCognitiveComplexity(method));
        }

        var footprint = AIContextFootprintCalculator.Calculate(
            symbol,
            config.Metrics.FootprintIgnoreNamespacePrefixes,
            config.Metrics.FootprintIgnoreTypeNames);

        return new ScannedClass(
            Name: symbol.Name,
            MaxCognitiveComplexity: maxCc,
            AIContextFootprint: footprint,
            IsSealed: symbol.IsSealed);
    }
}
