#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Core;

namespace AiNetLinter.Generators;

/// <summary>
/// Analysiert eine Solution und generiert einen kompakten Abhängigkeitsgraphen (nur Produktionscode).
/// </summary>
public sealed class CodegraphGenerator
{
    /// <summary>
    /// Generiert den Abhängigkeitsgraphen und schreibt ihn in die angegebene Datei.
    /// </summary>
    public static async Task GenerateAsync(Solution solution, string outputPath)
    {
        var typesByNamespace = await CollectProductionTypesAsync(solution);
        var version = typeof(CodegraphGenerator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var content = BuildContent(typesByNamespace, version);
        await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);
    }

    private static async Task<SortedDictionary<string, List<TypeInfo>>> CollectProductionTypesAsync(Solution solution)
    {
        var allTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            if (TestProjectDetector.IsTestProject(project)) continue;

            foreach (var document in project.Documents)
            {
                if (!IsValidDocument(document)) continue;
                await ExtractTypesAsync(document, allTypes);
            }
        }

        return GroupByNamespace(allTypes);
    }

    private static SortedDictionary<string, List<TypeInfo>> GroupByNamespace(HashSet<INamedTypeSymbol> allTypes)
    {
        var result = new SortedDictionary<string, List<TypeInfo>>(StringComparer.Ordinal);

        foreach (var type in allTypes)
        {
            var ns = ResolveNamespaceKey(type);
            if (!result.TryGetValue(ns, out var list))
            {
                list = [];
                result[ns] = list;
            }
            list.Add(BuildTypeInfo(type, allTypes));
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return result;
    }

    private static string ResolveNamespaceKey(INamedTypeSymbol type)
    {
        var ns = type.ContainingNamespace;
        if (ns == null || ns.IsGlobalNamespace) return "(global)";
        return ns.ToDisplayString();
    }

    private static bool IsValidDocument(Document document)
    {
        var path = document.FilePath ?? document.Name;
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        return !IsGeneratedPath(path);
    }

    private static bool IsGeneratedPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);

    private static async Task ExtractTypesAsync(Document document, HashSet<INamedTypeSymbol> allTypes)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return;

        var root = await document.GetSyntaxRootAsync();
        if (root == null) return;

        foreach (var node in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(node) is INamedTypeSymbol symbol)
                allTypes.Add(symbol);
        }
    }

    private static TypeInfo BuildTypeInfo(INamedTypeSymbol type, HashSet<INamedTypeSymbol> allTypes)
    {
        var modifiers = BuildModifiers(ResolveKind(type), type.DeclaringSyntaxReferences.Length > 1);

        var baseType = type.BaseType != null && allTypes.Contains(type.BaseType)
            ? type.BaseType.Name : null;

        var interfaces = type.Interfaces
            .Where(allTypes.Contains)
            .Select(i => i.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var deps = CollectDependencies(type, allTypes);

        return new TypeInfo(type.Name, modifiers, baseType, interfaces, deps);
    }

    private static string BuildModifiers(string kind, bool isPartial)
    {
        if (!string.IsNullOrEmpty(kind) && isPartial) return $"{kind}, partial";
        if (isPartial) return "partial";
        return kind;
    }

    private static string ResolveKind(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface) return "interface";
        if (type.TypeKind == TypeKind.Struct) return "struct";
        if (type.TypeKind == TypeKind.Enum) return "enum";
        if (type.IsRecord) return "record";
        if (type.IsAbstract && !type.IsStatic) return "abstract";
        return "";
    }

    private static IReadOnlyList<string> CollectDependencies(INamedTypeSymbol type, HashSet<INamedTypeSymbol> allTypes)
    {
        var deps = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field && field.Type is INamedTypeSymbol ft && allTypes.Contains(ft))
            {
                deps.Add(ft.Name);
            }
            else if (member is IPropertySymbol prop && prop.Type is INamedTypeSymbol pt && allTypes.Contains(pt))
            {
                deps.Add(pt.Name);
            }
            else if (member is IMethodSymbol method && IsRelevantMethod(method))
            {
                CollectMethodSignatureDeps(method, allTypes, deps);
            }
        }

        return deps.OrderBy(d => d, StringComparer.Ordinal).ToArray();
    }

    private static bool IsRelevantMethod(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.Constructor;

    private static void CollectMethodSignatureDeps(IMethodSymbol method, HashSet<INamedTypeSymbol> allTypes, HashSet<string> deps)
    {
        foreach (var param in method.Parameters)
        {
            if (param.Type is INamedTypeSymbol pt && allTypes.Contains(pt))
                deps.Add(pt.Name);
        }

        if (method.ReturnType is INamedTypeSymbol rt && allTypes.Contains(rt))
            deps.Add(rt.Name);
    }

    private static string BuildContent(SortedDictionary<string, List<TypeInfo>> typesByNamespace, string version)
    {
        var totalTypes = typesByNamespace.Values.Sum(v => v.Count);
        var sb = new StringBuilder();

        sb.AppendLine($"# Codegraph (Auto-generiert durch AiNetLinter {version})");
        sb.AppendLine($"Produktionscode · {totalTypes} Typen · {typesByNamespace.Count} Namespaces");

        foreach (var (ns, types) in typesByNamespace)
        {
            sb.AppendLine();
            sb.AppendLine($"## {ns} ({types.Count})");

            foreach (var t in types)
                sb.AppendLine(BuildTypeLine(t));
        }

        return sb.ToString();
    }

    private static string BuildTypeLine(TypeInfo t)
    {
        var sb = new StringBuilder();
        sb.Append($"- {t.Name}");

        if (!string.IsNullOrEmpty(t.Modifiers)) sb.Append($" [{t.Modifiers}]");
        if (t.BaseType != null) sb.Append($" : {t.BaseType}");
        if (t.Interfaces.Count > 0) sb.Append($" impl {string.Join(", ", t.Interfaces)}");
        if (t.Dependencies.Count > 0) sb.Append($" → {string.Join(", ", t.Dependencies)}");

        return sb.ToString();
    }
}
