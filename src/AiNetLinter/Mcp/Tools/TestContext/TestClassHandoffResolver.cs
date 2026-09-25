#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.SymbolGraph.CallGraph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Loest die durch statische Test-Evidenz sichtbaren Testklassen in direkte Handoffs auf.
/// </summary>
internal static class TestClassHandoffResolver
{
    internal static async Task<IReadOnlyList<TestClassHandoff>> ResolveAsync(
        TestFileCoverageResult file,
        Solution solution,
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, file.FilePath);
        if (document is null) return [];

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (root is null || semanticModel is null) return [];

        var names = file.TestClassNames is { Count: > 0 }
            ? file.TestClassNames
            : [file.TestClassName];
        var requestedNames = new HashSet<string>(names, StringComparer.Ordinal);
        return root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(declaration => requestedNames.Contains(declaration.Identifier.Text))
            .Select(declaration => semanticModel.GetDeclaredSymbol(declaration, ct))
            .OfType<INamedTypeSymbol>()
            .Select(symbol => new TestClassHandoff(
                symbol.Name,
                CallGraphTraversal.GetStableSymbolId(symbol, assemblyIdentity, solution)))
            .OrderBy(testClass => testClass.Name, StringComparer.Ordinal)
            .ToList();
    }
}
