#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Analysiert eine Solution und generiert ein Mermaid-Klassendiagramm der internen Strukturen.
/// </summary>
public sealed class CodegraphGenerator
{
    /// <summary>
    /// Generiert den Abhängigkeitsgraphen und schreibt ihn in die angegebene Datei.
    /// </summary>
    /// <param name="solution">Die zu analysierende Roslyn Solution.</param>
    /// <param name="outputPath">Der Pfad zur Ausgabedatei (.md).</param>
    public static async Task GenerateAsync(Solution solution, string outputPath)
    {
        var allTypes = await CollectDeclaredTypesAsync(solution);
        
        using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await writer.WriteLineAsync("# Codebase-Architektur & Abhaengigkeitsgraph (Auto-Generated)");
        await writer.WriteLineAsync("Dieses Dokument visualisiert die interne Klassenstruktur und deren Beziehungen.");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("```mermaid");
        await writer.WriteLineAsync("classDiagram");

        WriteClasses(writer, allTypes);
        WriteRelationships(writer, allTypes);

        await writer.WriteLineAsync("```");
    }

    private static async Task<HashSet<INamedTypeSymbol>> CollectDeclaredTypesAsync(Solution solution)
    {
        var allTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                await ExtractTypesFromDocumentAsync(document, allTypes);
            }
        }

        return allTypes;
    }

    private static async Task ExtractTypesFromDocumentAsync(Document document, HashSet<INamedTypeSymbol> allTypes)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            return;
        }

        var root = await document.GetSyntaxRootAsync();
        if (root == null)
        {
            return;
        }

        var classNodes = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var classNode in classNodes)
        {
            var symbol = semanticModel.GetDeclaredSymbol(classNode);
            if (symbol is INamedTypeSymbol namedType)
            {
                allTypes.Add(namedType);
            }
        }
    }

    private static void WriteClasses(StreamWriter writer, HashSet<INamedTypeSymbol> types)
    {
        foreach (var type in types)
        {
            var typeName = type.Name;
            writer.WriteLine($"    class {typeName} {{");

            var publicMethods = type.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary);

            foreach (var method in publicMethods)
            {
                writer.WriteLine($"        +{method.Name}()");
            }

            writer.WriteLine("    }");
        }
    }

    private static void WriteRelationships(StreamWriter writer, HashSet<INamedTypeSymbol> allTypes)
    {
        foreach (var type in allTypes)
        {
            var typeName = type.Name;

            WriteInheritanceAndInterfaces(writer, type, typeName, allTypes);
            WriteUsages(writer, type, typeName, allTypes);
        }
    }

    private static void WriteInheritanceAndInterfaces(StreamWriter writer, INamedTypeSymbol type, string typeName, HashSet<INamedTypeSymbol> allTypes)
    {
        if (type.BaseType != null && allTypes.Contains(type.BaseType))
        {
            writer.WriteLine($"    {type.BaseType.Name} <|-- {typeName} : erbt");
        }

        foreach (var iface in type.Interfaces)
        {
            if (allTypes.Contains(iface))
            {
                writer.WriteLine($"    {iface.Name} <|.. {typeName} : implementiert");
            }
        }
    }

    private static void WriteUsages(StreamWriter writer, INamedTypeSymbol type, string typeName, HashSet<INamedTypeSymbol> allTypes)
    {
        var referencedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        CollectFieldDependencies(type, allTypes, referencedTypes);
        CollectPropertyDependencies(type, allTypes, referencedTypes);

        foreach (var dep in referencedTypes)
        {
            writer.WriteLine($"    {typeName} --> {dep.Name} : nutzt");
        }
    }

    private static void CollectFieldDependencies(INamedTypeSymbol type, HashSet<INamedTypeSymbol> allTypes, HashSet<INamedTypeSymbol> referencedTypes)
    {
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.Type is INamedTypeSymbol fieldType && allTypes.Contains(fieldType))
            {
                referencedTypes.Add(fieldType);
            }
        }
    }

    private static void CollectPropertyDependencies(INamedTypeSymbol type, HashSet<INamedTypeSymbol> allTypes, HashSet<INamedTypeSymbol> referencedTypes)
    {
        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.Type is INamedTypeSymbol propType && allTypes.Contains(propType))
            {
                referencedTypes.Add(propType);
            }
        }
    }
}
