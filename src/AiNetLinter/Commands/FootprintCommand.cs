#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Metrics;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Führt die AI-Context-Footprint-Analyse für eine einzelne Klasse aus.
/// </summary>
internal static class FootprintCommand
{
    /// <summary>
    /// Sucht die Klasse in der Solution und gibt die Footprint-Details aus.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;
        try
        {
            using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath, ct);
            var targetSymbol = await FindTypeSymbolAsync(catalog.Solution, args.Footprint!);
            if (targetSymbol == null)
            {
                c.WriteError($"[ERROR]: Klasse '{args.Footprint}' wurde in der Solution nicht gefunden.");
                return 1;
            }
            var (totalLines, topDeps) = AIContextFootprintCalculator.CalculateDetailed(targetSymbol);
            c.WriteLine($"AI-Context-Footprint fuer Klasse '{targetSymbol.ToDisplayString()}':");
            c.WriteLine($"Gesamt transitive Zeilen: {totalLines}");
            c.WriteLine("Top-Abhängigkeiten:");
            foreach (var dep in topDeps)
                c.WriteLine($"  + {dep.Name} ({dep.Lines} Zeilen)");
            return 0;
        }
        catch (Exception ex)
        {
            c.WriteError($"[ERROR]: Fehler bei der Footprint-Analyse: {ex.Message}");
            return 2;
        }
    }

    private static async Task<INamedTypeSymbol?> FindTypeSymbolAsync(Solution solution, string typeName)
    {
        foreach (var project in solution.Projects)
        {
            var symbol = await FindInProjectAsync(project, typeName);
            if (symbol != null) return symbol;
        }
        return null;
    }

    private static async Task<INamedTypeSymbol?> FindInProjectAsync(Project project, string typeName)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return null;
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var symbol = await FindInSyntaxTreeAsync(compilation, syntaxTree, typeName);
            if (symbol != null) return symbol;
        }
        return null;
    }

    private static async Task<INamedTypeSymbol?> FindInSyntaxTreeAsync(
        Compilation compilation, SyntaxTree syntaxTree, string typeName)
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync();
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if (symbol != null && (
                string.Equals(symbol.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbol.ToDisplayString(), typeName, StringComparison.OrdinalIgnoreCase)))
                return symbol;
        }
        return null;
    }
}
