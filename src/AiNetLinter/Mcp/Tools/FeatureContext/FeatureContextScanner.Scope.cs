#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FeatureContext;

internal static partial class FeatureContextScanner
{
    private static (string FilePath, int StartLine, int EndLine) ExtractLocation(ISymbol symbol, string solutionDir)
    {
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxReference is not null)
        {
            var span = syntaxReference.GetSyntax().GetLocation().GetLineSpan();
            return (PathNormalizer.ToRelative(solutionDir, span.Path), span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
        }
        var location = symbol.Locations.FirstOrDefault(value => value.IsInSource);
        if (location?.SourceTree is null) return ("", 0, 0);
        var lineSpan = location.GetLineSpan();
        return (PathNormalizer.ToRelative(solutionDir, location.SourceTree.FilePath), lineSpan.StartLinePosition.Line + 1, lineSpan.EndLinePosition.Line + 1);
    }

    private static async Task<(List<CallSiteEntry> Visible, IReadOnlyDictionary<CallSiteEntry, McpDocumentScope> Scopes, int ExcludedCount)> FilterCallersAsync(IReadOnlyList<CallSiteEntry> callers, Solution solution, McpScopeInput scopeInput, McpScopeClassifier classifier, CancellationToken ct)
    {
        var visible = new List<CallSiteEntry>(callers.Count);
        var scopes = new Dictionary<CallSiteEntry, McpDocumentScope>();
        var excluded = 0;
        foreach (var caller in callers)
        {
            var document = DiffImpactAnalyzer.FindDocumentByPath(solution, caller.FilePath);
            var scope = document is null ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable) : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(scope, scopeInput)) { excluded++; continue; }
            visible.Add(caller);
            scopes[caller] = scope;
        }
        return (visible, scopes, excluded);
    }

    private static async Task<(List<TestFileCoverageResult> Visible, IReadOnlyDictionary<string, McpDocumentScope> Scopes, int ExcludedCount)> FilterTestFilesAsync(IReadOnlyList<TestFileCoverageResult> testFiles, Solution solution, McpScopeInput scopeInput, McpScopeClassifier classifier, CancellationToken ct)
    {
        var visible = new List<TestFileCoverageResult>(testFiles.Count);
        var scopes = new Dictionary<string, McpDocumentScope>(StringComparer.OrdinalIgnoreCase);
        var excluded = 0;
        foreach (var testFile in testFiles)
        {
            var document = DiffImpactAnalyzer.FindDocumentByPath(solution, testFile.FilePath);
            var scope = document is null ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable) : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(scope, scopeInput)) { excluded++; continue; }
            visible.Add(testFile);
            scopes[testFile.FilePath] = scope;
        }
        return (visible, scopes, excluded);
    }

    private static async Task<(string? ReturnType, IReadOnlyList<string> Parameters, IReadOnlyList<string>? BaseTypes, IReadOnlyList<string>? Members)> ExtractNamedTypeDetailsAsync(
        INamedTypeSymbol namedType,
        Solution solution,
        McpScopeClassifier classifier,
        McpScopeInput scopeInput,
        CancellationToken ct)
    {
        var baseTypes = ExtractBaseTypes(namedType);
        var members = await ExtractNamedTypeMembersAsync(namedType, solution, classifier, scopeInput, ct).ConfigureAwait(false);
        return (null, [], baseTypes, members);
    }

    private static IReadOnlyList<string>? ExtractBaseTypes(INamedTypeSymbol namedType)
    {
        var baseTypes = new List<string>();
        if (namedType.BaseType != null && namedType.BaseType.SpecialType != SpecialType.System_Object)
        {
            baseTypes.Add(namedType.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }
        foreach (var iface in namedType.Interfaces)
        {
            baseTypes.Add(iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        return baseTypes.Count > 0 ? baseTypes : null;
    }

    private static async Task<IReadOnlyList<string>?> ExtractNamedTypeMembersAsync(
        INamedTypeSymbol namedType,
        Solution solution,
        McpScopeClassifier classifier,
        McpScopeInput scopeInput,
        CancellationToken ct)
    {
        var members = new List<string>();
        foreach (var member in namedType.GetMembers()
            .Where(m => !m.IsImplicitlyDeclared && m.CanBeReferencedByName)
            .Where(m => m is IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.Constructor }
                     or IPropertySymbol
                     or IEventSymbol))
        {
            var location = member.Locations.FirstOrDefault(value => value.IsInSource);
            var document = location?.SourceTree is null ? null : solution.GetDocument(location.SourceTree);
            var memberScope = document is null
                ? new McpDocumentScope(McpProjectKind.Unknown, McpSourceKind.Editable)
                : await classifier.ClassifyAsync(document, ct).ConfigureAwait(false);
            if (!classifier.IsVisible(memberScope, scopeInput)) continue;
            members.Add(FormatMemberSignature(member));
            if (members.Count == 25) break;
        }

        return members.Count > 0 ? members : null;
    }

    private static string FormatMemberSignature(ISymbol member)
    {
        if (member is IMethodSymbol method)
        {
            var ret = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var pars = string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
            return $"{method.Name}({pars}) : {ret}";
        }

        if (member is IPropertySymbol prop)
        {
            var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return $"{prop.Name} : {propType}";
        }

        if (member is IEventSymbol evt)
        {
            var evtType = evt.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return $"event {evt.Name} : {evtType}";
        }

        return member.Name;
    }
}
