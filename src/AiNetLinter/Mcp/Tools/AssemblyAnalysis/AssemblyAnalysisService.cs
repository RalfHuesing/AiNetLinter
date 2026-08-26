#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisService
{
    internal const int DefaultMaxResults = 100;
    internal const int MaxResults = 1000;

    internal static bool TryValidatePath(string? assemblyPath, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            error = "Pflichtparameter 'assemblyPath' fehlt oder ist leer.";
            return false;
        }

        if (!Path.IsPathFullyQualified(assemblyPath))
        {
            error = $"Der Parameter 'assemblyPath' muss ein absoluter lokaler Pfad sein: '{assemblyPath}'.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(assemblyPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Der Parameter 'assemblyPath' ist kein gültiger lokaler Pfad: '{assemblyPath}' ({ex.Message}).";
            return false;
        }
        if (!string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Der Assembly-Pfad muss auf eine .dll zeigen: '{assemblyPath}'.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            error = $"Die Assembly-Datei wurde nicht gefunden: '{fullPath}'.";
            return false;
        }

        return true;
    }

    internal static int NormalizeMaxResults(int requested) =>
        Math.Clamp(requested, 1, MaxResults);

    internal static async Task<(AssemblyContext? Context, string? Error)> CreateContextAsync(
        string assemblyPath,
        Solution? consumerSolution,
        string? receiverType,
        CancellationToken ct)
    {
        return await AssemblyAnalysisContextFactory.CreateAsync(assemblyPath, consumerSolution, receiverType, ct);
    }

    internal static AssemblyTypeSelection Inspect(
        AssemblyContext context,
        AssemblyInspectionOptions options)
    {
        var types = AssemblyAnalysisSymbolTraversal.GetAllTypes(context.Assembly.GlobalNamespace)
            .Where(type => !options.PublicOnly || IsPublicApi(type))
            .Where(type => MatchesNamespace(type, options.NamespaceFilter))
            .Where(type => Matches(type.ToDisplayString(), options.TypeFilter))
            .OrderBy(type => type.ContainingNamespace.ToDisplayString(), StringComparer.Ordinal)
            .ThenBy(type => type.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

        var limited = types.Take(options.MaxResults).ToList();
        var items = limited
            .Select(type => ToTypeDto(type, options.MemberFilter, options.PublicOnly))
            .ToList();
        var namespaces = types
            .Select(type => type.ContainingNamespace.ToDisplayString())
            .Where(namespaceName => !string.IsNullOrEmpty(namespaceName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(namespaceName => namespaceName, StringComparer.Ordinal)
            .ToList();
        return new AssemblyTypeSelection(items, namespaces, types.Count, limited.Count < types.Count);
    }

    internal static AssemblyExtensionSelection FindExtensions(
        AssemblyContext context,
        AssemblyExtensionSearchOptions options)
    {
        var extensions = AssemblyAnalysisSymbolTraversal.GetAllTypes(context.Assembly.GlobalNamespace)
            .Where(IsPublicApi)
            .SelectMany(type => type.GetMembers().OfType<IMethodSymbol>()
                .Where(method => method.IsExtensionMethod && method.DeclaredAccessibility == Accessibility.Public)
                .Select(method => (Type: type, Method: method)))
            .Where(pair => Matches(pair.Type.ContainingNamespace.ToDisplayString(), options.NamespaceFilter))
            .Where(pair => Matches(pair.Method.Name, options.ExtensionName))
            .OrderBy(pair => pair.Type.ContainingNamespace.ToDisplayString(), StringComparer.Ordinal)
            .ThenBy(pair => pair.Method.ToDisplayString(), StringComparer.Ordinal)
            .ToList();

        var limited = extensions.Take(options.MaxResults).ToList();
        var items = limited.Select(pair => ToExtensionDto(context, pair.Type, pair.Method)).ToList();
        return new AssemblyExtensionSelection(items, extensions.Count, limited.Count < extensions.Count);
    }

    private static AssemblyExtensionDto ToExtensionDto(AssemblyContext context, INamedTypeSymbol declaringType, IMethodSymbol method)
    {
        var receiverType = method.Parameters.Length == 0
            ? "<unbekannt>"
            : method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var applicability = context.Receiver is null ? "not_decidable" : "not_applicable";
        string? reason = context.Receiver is null ? "Kein auflösbarer Consumer-Typ angegeben." : null;

        if (context.Receiver is not null)
        {
            try
            {
                var reduced = method.ReduceExtensionMethod(context.Receiver);
                if (reduced is not null)
                {
                    applicability = "applicable";
                    reason = null;
                }
                else
                {
                    applicability = context.Diagnostics.Count == 0 ? "not_applicable" : "not_decidable";
                    reason = context.Diagnostics.Count == 0
                        ? "Roslyn konnte die Extension für den Consumer-Typ nicht reduzieren."
                        : "Die Extension konnte wegen unvollständiger Consumer-/Abhängigkeitsinformationen nicht entschieden werden.";
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                reason = $"Roslyn konnte die Anwendbarkeit nicht entscheiden: {ex.Message}";
                applicability = "not_decidable";
            }
        }

        return new AssemblyExtensionDto(
            declaringType.ContainingNamespace.ToDisplayString(),
            declaringType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            method.Name,
            MethodSignature(method),
            receiverType,
            GenericParameters(method),
            Constraints(method.TypeParameters),
            applicability,
            reason,
            Attributes(method));
    }

    private static AssemblyTypeDto ToTypeDto(INamedTypeSymbol type, string? memberFilter, bool publicOnly)
    {
        var members = type.GetMembers()
            .Where(member => !member.IsImplicitlyDeclared)
            .Where(member => !IsAccessor(member))
            .Where(member => !publicOnly || IsPublicApi(member))
            .Where(member => Matches(member.Name, memberFilter))
            .Select(ToMemberDto)
            .OrderBy(member => member.Kind, StringComparer.Ordinal)
            .ThenBy(member => member.Signature, StringComparer.Ordinal)
            .ToList();
        return new AssemblyTypeDto(
            type.ContainingNamespace.ToDisplayString(),
            TypeName(type),
            TypeKindName(type),
            type.DeclaredAccessibility.ToString(),
            members,
            Attributes(type));
    }

    private static AssemblyMemberDto ToMemberDto(ISymbol member)
    {
        var method = member as IMethodSymbol;
        return new AssemblyMemberDto(
            MemberKind(member),
            member.Name,
            member.DeclaredAccessibility.ToString(),
            method is null ? member.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) : MethodSignature(method),
            method is null ? Array.Empty<string>() : GenericParameters(method),
            method is null ? Array.Empty<string>() : Constraints(method.TypeParameters),
            Attributes(member));
    }

    private static string MethodSignature(IMethodSymbol method) =>
        $"{method.ReturnType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)} {method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}";

    private static IReadOnlyList<string> GenericParameters(IMethodSymbol method) =>
        method.TypeParameters.Select(parameter => parameter.Name).ToList();

    private static IReadOnlyList<string> Constraints(IEnumerable<ITypeParameterSymbol> parameters) =>
        parameters.Select(parameter =>
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint) constraints.Add("class");
            if (parameter.HasValueTypeConstraint) constraints.Add("struct");
            if (parameter.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
            if (parameter.HasNotNullConstraint) constraints.Add("notnull");
            constraints.AddRange(parameter.ConstraintTypes.Select(type => type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            if (parameter.HasConstructorConstraint) constraints.Add("new()");
            return constraints.Count == 0 ? parameter.Name : $"{parameter.Name}: {string.Join(", ", constraints)}";
        }).ToList();

    private static IReadOnlyList<string> Attributes(ISymbol symbol)
    {
        try
        {
            return symbol.GetAttributes()
                .Where(attribute => attribute.AttributeClass is not null)
                .Select(attribute => attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ["<Attribute konnten nicht aufgelöst werden>"];
        }
    }

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesNamespace(INamedTypeSymbol type, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(type.ContainingNamespace.ToDisplayString(), filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicApi(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public) return false;
        for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (containing.DeclaredAccessibility != Accessibility.Public) return false;
        }

        return true;
    }

    private static string TypeKindName(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Class => "class",
        TypeKind.Interface => "interface",
        TypeKind.Struct => "struct",
        TypeKind.Enum => "enum",
        TypeKind.Delegate => "delegate",
        _ => type.TypeKind.ToString().ToLowerInvariant(),
    };

    private static string TypeName(INamedTypeSymbol type)
    {
        var containingType = type.ContainingType is null ? string.Empty : $"{TypeName(type.ContainingType)}.";
        var typeParameters = type.TypeParameters.Length == 0
            ? string.Empty
            : $"<{string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name))}>";
        return $"{containingType}{type.Name}{typeParameters}";
    }

    private static string MemberKind(ISymbol member) => member switch
    {
        IMethodSymbol => "method",
        IPropertySymbol => "property",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        _ => member.Kind.ToString().ToLowerInvariant(),
    };

    private static bool IsAccessor(ISymbol member) => member is IMethodSymbol
    {
        MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or
            MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise,
    };

}

internal sealed record AssemblyContext(
    IAssemblySymbol Assembly,
    AssemblyIdentityDto? Identity,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<string> Diagnostics,
    Compilation Compilation,
    ITypeSymbol? Receiver,
    string? ConsumerProject);
