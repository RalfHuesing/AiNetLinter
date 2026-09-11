#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static partial class GetClassStructureTool
{
    private static async Task<(List<string> Files, List<ClassStructureLocation> Locations, int TotalLines)> CollectDeclarationFilesAsync(
        INamedTypeSymbol namedType, Solution solution, string solutionDir, McpScopeClassifier classifier, McpScopeInput scopeInput, CancellationToken ct)
    {
        var files = new List<string>();
        var locations = new List<ClassStructureLocation>();
        var totalLines = 0;
        foreach (var syntaxReference in namedType.DeclaringSyntaxReferences)
        {
            var tree = syntaxReference.SyntaxTree;
            var document = solution.GetDocument(tree);
            var scope = document is null
                ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable)
                : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (classifier.IsVisible(scope, scopeInput))
            {
                var path = PathNormalizer.ToRelative(solutionDir, tree.FilePath);
                files.Add(path);
                locations.Add(new ClassStructureLocation(path, McpScopeValues.ToWireValue(scope.ProjectKind), McpScopeValues.ToWireValue(scope.SourceKind), true));
                var span = (await syntaxReference.GetSyntaxAsync(ct).ConfigureAwait(false)).GetLocation().GetLineSpan();
                totalLines += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
            }
        }
        return (files.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), locations, totalLines);
    }

    private static async Task<(List<ClassStructureMemberEntry> VisibleMembers, int ExcludedCount)> FilterMembersByScopeAsync(
        IReadOnlyList<ClassStructureMemberEntry> members, Solution solution, string solutionDir, McpScopeClassifier classifier, McpScopeInput scopeInput, CancellationToken ct)
    {
        var visible = new List<ClassStructureMemberEntry>(members.Count);
        var excluded = 0;
        foreach (var member in members)
        {
            var document = solution.Projects.SelectMany(project => project.Documents).FirstOrDefault(candidate => candidate.FilePath is not null && PathNormalizer.ToRelative(solutionDir, candidate.FilePath).Equals(member.FilePath, StringComparison.OrdinalIgnoreCase));
            var scope = document is null ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable) : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(scope, scopeInput)) { excluded++; continue; }
            visible.Add(member with { ScopeType = McpScopeValues.ToWireValue(scope.ProjectKind), SourceKind = McpScopeValues.ToWireValue(scope.SourceKind) });
        }
        return (visible, excluded);
    }
}
