#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Bodies;

internal static class AssemblyDecompiledBodyResolver
{
    internal static AssemblyBodyResolver Create(
        string assemblyPath,
        AssemblyReferenceResolution references,
        AssemblyDecompilationOptions options) =>
        (symbol, maxBodyLines, cancellationToken) => ResolveAsync(
            assemblyPath, references, options, symbol, maxBodyLines, cancellationToken);

    private static async Task<AssemblyBodyResolution> ResolveAsync(
        string assemblyPath,
        AssemblyReferenceResolution references,
        AssemblyDecompilationOptions options,
        ISymbol symbol,
        int maxBodyLines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var unavailable = GetUnavailableBodyResolution(symbol);
        if (unavailable is not null) return unavailable;

        return await DecompileBodyAsync(
            assemblyPath, references, options, symbol, maxBodyLines, cancellationToken)
            .ConfigureAwait(false);
    }

    private static AssemblyBodyResolution? GetUnavailableBodyResolution(ISymbol symbol)
    {
        var declaringType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (declaringType?.TypeKind == TypeKind.Interface)
        {
            return new(null, "unavailable", "decompiledSignatureOnly", "Interfaces haben keine dekompilierbaren Bodies.");
        }

        return AssemblyBodySyntax.HasUnavailableMember(symbol)
            ? new(null, "unavailable", "decompiledSignatureOnly", "Das Symbol ist abstract oder extern und besitzt keinen Body.")
            : null;
    }

    private static async Task<AssemblyBodyResolution> DecompileBodyAsync(
        string assemblyPath,
        AssemblyReferenceResolution references,
        AssemblyDecompilationOptions options,
        ISymbol symbol,
        int maxBodyLines,
        CancellationToken cancellationToken)
    {
        var normalizedLines = Math.Max(1, maxBodyLines);
        if (!AssemblyDecompilationOptions.IsSupportedTimeout(options.EffectiveTimeout))
        {
            return new(
                null,
                "unavailable",
                "decompiledSignatureOnly",
                "Das Decompilation-Timeout liegt außerhalb des von CancellationTokenSource.CancelAfter unterstützten Bereichs.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            deadline.CancelAfter(options.EffectiveTimeout);
            var decompiler = AssemblyDecompilationAdapter.CreateDecompiler(
                assemblyPath,
                references,
                deadline.Token,
                decompileMemberBodies: true);
            var declaringType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            var typeName = new ICSharpCode.Decompiler.TypeSystem.FullTypeName(ToReflectionTypeName(declaringType));
            var source = decompiler.DecompileTypeAsString(typeName);
            source = AssemblyDecompilationSourceText.RemoveCompilerGeneratedNestedTypes(source);
            source = AssemblyDecompilationSourceText.RemoveCompilerGeneratedStateMachineAttributes(source);
            deadline.Token.ThrowIfCancellationRequested();
            var root = CSharpSyntaxTree.ParseText(source).GetRoot(deadline.Token);
            var member = FindMember(root, symbol);
            if (member is null)
            {
                return new(null, "unavailable", "decompiledSignatureOnly", "Für das dekompilierte Symbol wurde kein Member-Body gefunden.");
            }

            var body = LimitLines(member.ToFullString(), normalizedLines);
            return new(
                body,
                "available",
                "decompiledBodyOnDemand",
                body.Contains("truncated", StringComparison.Ordinal) ? "Der Body wurde auf maxBodyLines begrenzt." : null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(null, "unavailable", "decompiledSignatureOnly", "Die Body-Dekomposition wurde wegen Cancellation oder Deadline abgebrochen.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or ICSharpCode.Decompiler.DecompilerException)
        {
            return new(null, "unavailable", "decompiledSignatureOnly", "Body-Dekomposition fehlgeschlagen: " + ex.GetType().Name);
        }
    }

    private static string ToReflectionTypeName(INamedTypeSymbol? type)
    {
        if (type is null) return string.Empty;
        var name = type.MetadataName;
        if (type.ContainingType is not null) return ToReflectionTypeName(type.ContainingType) + "+" + name;
        return type.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + name
            : name;
    }

    private static SyntaxNode? FindMember(SyntaxNode root, ISymbol symbol)
    {
        var type = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .FirstOrDefault(candidate => MatchesContainingType(
                candidate,
                symbol as INamedTypeSymbol ?? symbol.ContainingType));
        if (type is null) return null;

        if (symbol is INamedTypeSymbol) return type;
        if (type is not TypeDeclarationSyntax declaration) return null;

        var member = declaration.Members.FirstOrDefault(candidate => MatchesMember(candidate, symbol));
        if (member is null) return null;
        return symbol is IMethodSymbol { AssociatedSymbol: not null } accessor
            ? FindAccessor(member, accessor)
            : member;
    }

    private static bool MatchesContainingType(BaseTypeDeclarationSyntax candidate, INamedTypeSymbol? type)
    {
        if (type is null) return false;
        var syntaxTypes = candidate.AncestorsAndSelf()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .ToArray();
        var symbolTypes = GetContainingTypes(type);
        return syntaxTypes.Length == symbolTypes.Count
            && syntaxTypes.Zip(symbolTypes).All(pair =>
                string.Equals(pair.First.Identifier.Text, pair.Second.Name, StringComparison.Ordinal)
                && GetTypeParameterCount(pair.First) == pair.Second.TypeParameters.Length);
    }

    private static int GetTypeParameterCount(BaseTypeDeclarationSyntax declaration) =>
        declaration is TypeDeclarationSyntax type
            ? type.TypeParameterList?.Parameters.Count ?? 0
            : 0;

    private static List<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol type)
    {
        var result = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.ContainingType)
        {
            result.Add(current);
        }

        result.Reverse();
        return result;
    }

    private static bool MatchesMember(SyntaxNode member, ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            return method.AssociatedSymbol is not null
                ? MatchesAssociatedMember(member, method)
                : MatchesMethod(member, method);
        }

        if (symbol is IPropertySymbol property) return MatchesProperty(member, property);
        if (symbol is IFieldSymbol field) return member is FieldDeclarationSyntax declaration
            && declaration.Declaration.Variables.Any(variable => variable.Identifier.Text == field.Name);
        if (symbol is IEventSymbol eventSymbol) return MatchesEvent(member, eventSymbol);
        return false;
    }

    private static bool MatchesMethod(SyntaxNode member, IMethodSymbol symbol)
    {
        if (symbol.AssociatedSymbol is not null) return false;

        if (symbol.MethodKind == MethodKind.Constructor)
        {
            return member is ConstructorDeclarationSyntax constructor
                && constructor.Identifier.Text == symbol.ContainingType?.Name
                && MatchesParameters(constructor.ParameterList.Parameters, symbol.Parameters);
        }

        return member is MethodDeclarationSyntax method
            && string.Equals(method.Identifier.Text, symbol.Name, StringComparison.Ordinal)
            && (method.TypeParameterList?.Parameters.Count ?? 0) == symbol.TypeParameters.Length
            && MatchesParameters(method.ParameterList.Parameters, symbol.Parameters);
    }

    private static bool MatchesAssociatedMember(SyntaxNode member, IMethodSymbol symbol) =>
        symbol.AssociatedSymbol switch
        {
            IPropertySymbol property => MatchesProperty(member, property),
            IEventSymbol eventSymbol => MatchesEvent(member, eventSymbol),
            _ => false,
        };

    private static AccessorDeclarationSyntax? FindAccessor(SyntaxNode member, IMethodSymbol symbol)
    {
        var accessors = member switch
        {
            BasePropertyDeclarationSyntax property => property.AccessorList?.Accessors,
            _ => null,
        };
        return accessors?.FirstOrDefault(accessor => MatchesAccessor(accessor, symbol));
    }

    private static bool MatchesAccessor(AccessorDeclarationSyntax accessor, IMethodSymbol symbol)
    {
        var declaringMember = accessor.Parent?.Parent;
        var associatedMemberMatches = symbol.AssociatedSymbol switch
        {
            IPropertySymbol property => declaringMember is BasePropertyDeclarationSyntax declaration
                && MatchesProperty(declaration, property),
            IEventSymbol eventSymbol => declaringMember is EventDeclarationSyntax declaration
                && MatchesEvent(declaration, eventSymbol),
            _ => false,
        };
        return associatedMemberMatches && symbol.MethodKind switch
        {
            MethodKind.PropertyGet => accessor.IsKind(SyntaxKind.GetAccessorDeclaration),
            MethodKind.PropertySet => accessor.IsKind(SyntaxKind.SetAccessorDeclaration),
                MethodKind.EventAdd => accessor.IsKind(SyntaxKind.AddAccessorDeclaration),
                MethodKind.EventRemove => accessor.IsKind(SyntaxKind.RemoveAccessorDeclaration),
                _ => false,
            };
    }

    private static bool MatchesProperty(SyntaxNode member, IPropertySymbol symbol)
    {
        if (symbol.IsIndexer)
        {
            return member is IndexerDeclarationSyntax indexer
                && MatchesParameters(indexer.ParameterList.Parameters, symbol.Parameters);
        }

        return member is PropertyDeclarationSyntax property
            && string.Equals(property.Identifier.Text, symbol.Name, StringComparison.Ordinal);
    }

    private static bool MatchesEvent(SyntaxNode member, IEventSymbol symbol) =>
        member switch
        {
            EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Any(variable => variable.Identifier.Text == symbol.Name),
            EventDeclarationSyntax eventDeclaration => eventDeclaration.Identifier.Text == symbol.Name,
            _ => false,
        };

    private static bool MatchesParameters(
        SeparatedSyntaxList<ParameterSyntax> syntaxParameters,
        ImmutableArray<IParameterSymbol> symbolParameters)
    {
        if (syntaxParameters.Count < symbolParameters.Length) return false;
        for (var index = 0; index < syntaxParameters.Count; index++)
        {
            var syntaxParameter = syntaxParameters[index];
            if (index >= symbolParameters.Length)
            {
                if (syntaxParameter.Default is null) return false;
                continue;
            }

            var symbolParameter = symbolParameters[index];
            if (!string.Equals(
                    GetParameterModifier(syntaxParameter),
                    symbolParameter.RefKind.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                || !MatchesParameterType(syntaxParameter.Type?.ToString(), symbolParameter.Type))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetParameterModifier(ParameterSyntax parameter) =>
        parameter.Modifiers.Any(SyntaxKind.RefKeyword) ? "Ref" :
        parameter.Modifiers.Any(SyntaxKind.OutKeyword) ? "Out" :
        parameter.Modifiers.Any(SyntaxKind.InKeyword) ? "In" :
        "None";

    private static bool MatchesParameterType(string? syntaxType, ITypeSymbol symbolType)
    {
        if (syntaxType is null) return false;
        var normalizedSyntax = NormalizeTypeName(syntaxType);
        if (new[]
        {
            symbolType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            symbolType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            symbolType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        }.Select(NormalizeTypeName).Contains(normalizedSyntax, StringComparer.Ordinal))
        {
            return true;
        }

        if (symbolType.TypeKind != TypeKind.Error) return false;
        var lastSeparator = normalizedSyntax.LastIndexOf('.');
        var simpleSyntaxName = normalizedSyntax[(lastSeparator + 1)..];
        return string.Equals(simpleSyntaxName, symbolType.Name, StringComparison.Ordinal);
    }

    private static string NormalizeTypeName(string value) =>
        value.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("System.Int32", "int", StringComparison.Ordinal)
            .Replace("System.String", "string", StringComparison.Ordinal)
            .Replace("System.Boolean", "bool", StringComparison.Ordinal)
            .Replace("System.Object", "object", StringComparison.Ordinal)
            .Replace("System.Double", "double", StringComparison.Ordinal)
            .Replace("System.Single", "float", StringComparison.Ordinal)
            .Replace("System.Decimal", "decimal", StringComparison.Ordinal)
            .Replace("System.Char", "char", StringComparison.Ordinal)
            .Replace("System.Byte", "byte", StringComparison.Ordinal)
            .Replace("System.Int64", "long", StringComparison.Ordinal)
            .Replace("System.Int16", "short", StringComparison.Ordinal)
            .Replace("System.UInt32", "uint", StringComparison.Ordinal)
            .Replace("System.UInt64", "ulong", StringComparison.Ordinal)
            .Replace("System.UInt16", "ushort", StringComparison.Ordinal)
            .Replace("System.SByte", "sbyte", StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string LimitLines(string text, int maxBodyLines)
    {
        var lines = text.Split('\n');
        return lines.Length <= maxBodyLines
            ? text.TrimEnd()
            : string.Join("\n", lines.Take(maxBodyLines)).TrimEnd()
                + $"\n// ... truncated, total {lines.Length} Zeilen, maxBodyLines erhoehen fuer mehr";
    }
}
