#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.TypeResolution;

internal static class ResolveTypeOriginTool
{
    internal static async Task<CallToolResult> ExecuteProjectAsync(
        ISolutionStateProvider server,
        string typeName,
        CancellationToken ct)
    {
        if (server.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = server.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return McpToolResults.InvalidArgument(
                "typeName darf nicht leer sein.",
                hint: "Vollqualifizierten oder einfachen Typnamen angeben, z. B. 'IDataProvider' oder 'Vendor.Data.BaseCommand'.");
        }

        var context = CreateContext(typeName, solution.FilePath ?? ".", null, ct);
        var searchedAssemblies = new List<string>();

        foreach (var project in solution.Projects.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
            if (compilation is null) continue;

            var result = SearchInCompilation(compilation, context, searchedAssemblies);
            if (result is not null) return result;
        }

        return NotFoundResult(context.TypeName, searchedAssemblies);
    }

    internal static Task<CallToolResult> ExecuteAssemblyAsync(
        AssemblyAnalysisLease lease,
        string typeName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return Task.FromResult(McpToolResults.InvalidArgument(
                "typeName darf nicht leer sein.",
                hint: "Vollqualifizierten oder einfachen Typnamen angeben, z. B. 'IDataProvider' oder 'Vendor.Data.BaseCommand'."));
        }

        var context = CreateContext(typeName, lease.CanonicalPath, lease.Context.References, ct);
        var compilation = lease.Context.Compilation;
        var searchedAssemblies = new List<string>();
        var result = SearchInCompilation(compilation, context, searchedAssemblies);
        if (result is not null) return Task.FromResult(result);

        return Task.FromResult(NotFoundResult(context.TypeName, searchedAssemblies));
    }

    internal static CallToolResult ExecuteCompilation(
        Compilation compilation,
        string typeName,
        string fallbackPath,
        IReadOnlyList<AssemblyReferenceDto>? assemblyReferences,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return McpToolResults.InvalidArgument(
                "typeName darf nicht leer sein.",
                hint: "Vollqualifizierten oder einfachen Typnamen angeben, z. B. 'IDataProvider' oder 'Vendor.Data.BaseCommand'.");
        }

        var context = CreateContext(typeName, fallbackPath, assemblyReferences, ct);
        var searchedAssemblies = new List<string>();
        var result = SearchInCompilation(compilation, context, searchedAssemblies);
        return result ?? NotFoundResult(context.TypeName, searchedAssemblies);
    }

    private static ResolveContext CreateContext(
        string typeName,
        string fallbackPath,
        IReadOnlyList<AssemblyReferenceDto>? assemblyReferences,
        CancellationToken ct)
    {
        var trimmed = typeName.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        var isQualified = trimmed.Contains('.');
        return new ResolveContext(typeName, trimmed, isQualified, fallbackPath, assemblyReferences, ct);
    }

    private static CallToolResult? SearchInCompilation(
        Compilation compilation,
        ResolveContext context,
        List<string> searchedAssemblies)
    {
        var direct = SearchDirectType(compilation, context);
        if (direct is not null)
        {
            return BuildDirectResult(direct, compilation, context);
        }

        return SearchReferencedAssemblies(compilation, context, searchedAssemblies);
    }

    private static INamedTypeSymbol? SearchDirectType(Compilation compilation, ResolveContext context)
    {
        if (context.IsQualified)
        {
            return compilation.GetTypeByMetadataName(context.NormalizedName)
                ?? FindWithArity(compilation, context.NormalizedName);
        }

        return FindTypeInNamespace(compilation.Assembly.GlobalNamespace, context.NormalizedName, context.CancellationToken);
    }

    private static CallToolResult? SearchReferencedAssemblies(
        Compilation compilation,
        ResolveContext context,
        List<string> searchedAssemblies)
    {
        foreach (var metadataRef in compilation.References)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (compilation.GetAssemblyOrModuleSymbol(metadataRef) is not IAssemblySymbol asm) continue;
            searchedAssemblies.Add(asm.Name);

            var refType = context.IsQualified
                ? (asm.GetTypeByMetadataName(context.NormalizedName) ?? FindWithArity(asm, context.NormalizedName))
                : FindTypeInNamespace(asm.GlobalNamespace, context.NormalizedName, context.CancellationToken);

            if (refType is not null)
            {
                var dllPath = (metadataRef as PortableExecutableReference)?.FilePath
                    ?? ResolvePathFromReferences(asm.Name, context.AssemblyReferences)
                    ?? asm.Name + ".dll";
                return BuildSuccess(refType, asm.Name, dllPath, isSource: false, searchedAssemblies);
            }
        }

        return null;
    }

    private static CallToolResult BuildDirectResult(
        INamedTypeSymbol type,
        Compilation compilation,
        ResolveContext context)
    {
        var isSource = SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
        var asmName = type.ContainingAssembly.Name;
        string path;

        if (isSource)
        {
            path = context.FallbackPath;
        }
        else
        {
            var metaRef = compilation.GetMetadataReference(type.ContainingAssembly);
            path = (metaRef as PortableExecutableReference)?.FilePath
                ?? ResolvePathFromReferences(asmName, context.AssemblyReferences)
                ?? asmName + ".dll";
        }

        return BuildSuccess(type, asmName, path, isSource, [asmName]);
    }

    private static INamedTypeSymbol? FindWithArity(Compilation compilation, string name)
    {
        var bracketIndex = name.IndexOf('<');
        if (bracketIndex < 0) return null;
        var arity = name.Count(c => c == ',') + 1;
        var baseName = name[..bracketIndex].Trim();
        return compilation.GetTypeByMetadataName($"{baseName}`{arity}");
    }

    private static INamedTypeSymbol? FindWithArity(IAssemblySymbol asm, string name)
    {
        var bracketIndex = name.IndexOf('<');
        if (bracketIndex < 0) return null;
        var arity = name.Count(c => c == ',') + 1;
        var baseName = name[..bracketIndex].Trim();
        return asm.GetTypeByMetadataName($"{baseName}`{arity}");
    }

    private static INamedTypeSymbol? FindTypeInNamespace(
        INamespaceSymbol ns,
        string typeName,
        CancellationToken ct)
    {
        foreach (var member in ns.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (IsTypeMatch(member, typeName)) return member;
            var nested = FindNestedType(member, typeName, ct);
            if (nested is not null) return nested;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            ct.ThrowIfCancellationRequested();
            var match = FindTypeInNamespace(child, typeName, ct);
            if (match is not null) return match;
        }

        return null;
    }

    private static INamedTypeSymbol? FindNestedType(
        INamedTypeSymbol parent,
        string typeName,
        CancellationToken ct)
    {
        foreach (var member in parent.GetTypeMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (IsTypeMatch(member, typeName)) return member;
            var nested = FindNestedType(member, typeName, ct);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static bool IsTypeMatch(INamedTypeSymbol type, string typeName) =>
        string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(type.MetadataName, typeName, StringComparison.OrdinalIgnoreCase);

    private static string? ResolvePathFromReferences(
        string assemblyName,
        IReadOnlyList<AssemblyReferenceDto>? references)
    {
        if (references is null) return null;
        return references
            .FirstOrDefault(r => string.Equals(r.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            ?.ResolvedPath;
    }

    private static CallToolResult BuildSuccess(
        INamedTypeSymbol type,
        string assemblyName,
        string assemblyPath,
        bool isSource,
        IReadOnlyList<string> searched)
    {
        var origin = new TypeOriginInfoDto(
            assemblyName,
            assemblyPath,
            type.ToDisplayString(),
            FormatTypeKind(type.TypeKind),
            isSource,
            type.ContainingNamespace?.ToDisplayString() ?? "");

        return SuccessResult(new ResolveTypeOriginResultDto(type.Name, true, origin, searched));
    }

    private static string FormatTypeKind(TypeKind kind) =>
        kind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => kind.ToString().ToLowerInvariant()
        };

    private static CallToolResult SuccessResult(ResolveTypeOriginResultDto result)
    {
        var text = RenderMarkdown(result);
        return McpToolResults.Text(text, new { resolveTypeOrigin = result });
    }

    private static CallToolResult NotFoundResult(string typeName, IReadOnlyList<string> searched)
    {
        var payload = new ResolveTypeOriginResultDto(typeName, false, null, searched);
        var distinctSearched = searched.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var hint = distinctSearched.Count > 0
            ? $"Durchsuchte Referenzen ({distinctSearched.Count}): {string.Join(", ", distinctSearched.Take(10))}"
            : "Keine referenzierten Assemblies in der Compilation gefunden.";

        return McpToolResults.Recoverable(
            LinterErrorCodes.SymbolNotFound,
            $"Typ '{typeName}' wurde in keiner der referenzierten Assemblies gefunden.",
            context: typeName,
            hint: hint);
    }

    private static string RenderMarkdown(ResolveTypeOriginResultDto result)
    {
        if (!result.Found || result.Origin is null)
        {
            return $"# Typ-Herkunft: `{result.TypeName}`\n\nNicht gefunden in {result.SearchedAssemblies.Count} durchsuchten Referenzen.";
        }

        var origin = result.Origin;
        var sourceLabel = origin.IsSource ? "Projekt-Quellcode" : "Referenzierte Assembly";
        return $"""
            # Typ-Herkunft: `{result.TypeName}`
            - **Vollqualifizierter Name**: `{origin.FullName}`
            - **Symbol-Art**: `{origin.Kind}`
            - **Assembly**: `{origin.AssemblyName}`
            - **Dateipfad**: `{origin.AssemblyPath}`
            - **Herkunft**: {sourceLabel}
            """;
    }

    private sealed record ResolveContext(
        string TypeName,
        string NormalizedName,
        bool IsQualified,
        string FallbackPath,
        IReadOnlyList<AssemblyReferenceDto>? AssemblyReferences,
        CancellationToken CancellationToken);
}
