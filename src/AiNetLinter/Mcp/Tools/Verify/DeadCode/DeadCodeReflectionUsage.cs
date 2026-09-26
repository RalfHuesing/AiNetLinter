#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal sealed class DeadCodeReflectionUsage(DeadCodeUsageIndex index)
{
    internal void Collect(DeadCodeBindingCall context)
    {
        var owner = context.Method.ContainingType.ToDisplayString();
        if (owner == "System.Reflection.Assembly" && context.Method.Name == "GetTypes")
            CollectAssembly(context);
        if (owner != "System.Type" || context.Call.Expression is not MemberAccessExpressionSyntax access) return;
        if (context.Method.Name is not ("GetField" or "GetFields" or "GetProperty" or "GetProperties" or "GetMethod" or "GetMethods")) return;
        CollectMembers(context, access.Expression);
    }

    private void CollectMembers(DeadCodeBindingCall context, ExpressionSyntax receiver)
    {
        var types = ResolveTypes(receiver, context).ToArray();
        var nameArgument = context.Call.ArgumentList.Arguments.FirstOrDefault(argument =>
            context.Source.Model.GetTypeInfo(argument.Expression).Type?.SpecialType == SpecialType.System_String);
        var name = nameArgument is null ? null : context.Source.Model.GetConstantValue(nameArgument.Expression).Value as string;
        var flagsArgument = context.Call.ArgumentList.Arguments.FirstOrDefault(argument =>
            context.Source.Model.GetTypeInfo(argument.Expression).Type?.ToDisplayString() == "System.Reflection.BindingFlags");
        var flags = flagsArgument is null ? 28 : context.Source.Model.GetConstantValue(flagsArgument.Expression).Value as int?;
        var role = (nameArgument is not null && name is null) || flags is null ? "unknown" : context.Source.Role;
        var selection = new DeadCodeReflectionSelection(context);
        foreach (var type in types)
        {
            foreach (var member in type.Type.GetMembers().Where(member => Matches(member, context.Method.Name, name, flags)))
            {
                var selected = selection.Includes(member);
                if (selected == false) continue;
                RecordSelection(member, selected, role == "unknown" ? role : type.Role);
            }
        }
    }

    private void RecordSelection(ISymbol member, bool? selected, string role)
    {
        if (role == "unknown" || selected is null) index.MarkUnknown(member);
        else index.Add(member, role);
    }

    private IEnumerable<DeadCodeGenericBinding> ResolveTypes(ExpressionSyntax receiver, DeadCodeBindingCall context)
    {
        if (receiver is not TypeOfExpressionSyntax typeOf) yield break;
        var symbol = context.Source.Model.GetTypeInfo(typeOf.Type).Type;
        if (symbol is INamedTypeSymbol type) yield return new(type, context.Source.Role);
        if (symbol is ITypeParameterSymbol parameter && parameter.ContainingSymbol is IMethodSymbol method
            && index.TypeArguments.TryGetValue(DeadCodeUsageIndex.Key(method) + ":" + parameter.Name, out var types))
            foreach (var argument in types) yield return argument;
    }

    private static bool Matches(ISymbol member, string operation, string? name, int? flags)
    {
        if (name is not null && member.Name != name) return false;
        var matchingKind = operation switch
        {
            "GetField" or "GetFields" => member is IFieldSymbol,
            "GetProperty" or "GetProperties" => member is IPropertySymbol,
            _ => member is IMethodSymbol { MethodKind: MethodKind.Ordinary }
        };
        if (!matchingKind) return false;
        if (flags is null) return true;
        return (flags.Value & (member.IsStatic ? 8 : 4)) != 0
            && (flags.Value & (member.DeclaredAccessibility == Accessibility.Public ? 16 : 32)) != 0;
    }

    private void CollectAssembly(DeadCodeBindingCall context)
    {
        if (context.Call.Expression is not MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: TypeOfExpressionSyntax anchor } }) return;
        if (context.Source.Model.GetTypeInfo(anchor.Type).Type is not INamedTypeSymbol anchorType) return;
        var region = context.Call.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (region is null) return;
        var calls = region.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var activation = calls.Any(call => context.Source.Model.GetSymbolInfo(call).Symbol is IMethodSymbol method
            && method.ContainingType.ToDisplayString() == "System.Activator" && method.Name == "CreateInstance");
        var filter = calls.Select(call => call.Expression).OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(access => access.Name.Identifier.ValueText == "IsAssignableFrom" && access.Expression is TypeOfExpressionSyntax);
        var contract = filter?.Expression is TypeOfExpressionSyntax contractSyntax
            ? context.Source.Model.GetTypeInfo(contractSyntax.Type).Type as INamedTypeSymbol : null;
        var knownFilter = region.DescendantNodes().OfType<IfStatementSyntax>().All(condition => IsKnownTypeCondition(condition.Condition, context.Source));
        MarkAssemblyTypes(anchorType, contract, activation && knownFilter ? context.Source.Role : "unknown");
    }

    private static bool IsKnownTypeCondition(ExpressionSyntax expression, DeadCodeUsageDocument source) => expression switch
    {
        BinaryExpressionSyntax binary when binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression) =>
            IsKnownTypeCondition(binary.Left, source) && IsKnownTypeCondition(binary.Right, source),
        MemberAccessExpressionSyntax access => source.Model.GetSymbolInfo(access).Symbol is IPropertySymbol property
            && property.ContainingType.ToDisplayString() == "System.Type" && property.Name == "IsClass",
        InvocationExpressionSyntax call => source.Model.GetSymbolInfo(call).Symbol is IMethodSymbol method
            && method.ContainingType.ToDisplayString() == "System.Type" && method.Name == "IsAssignableFrom",
        _ => false
    };

    private void MarkAssemblyTypes(INamedTypeSymbol anchorType, INamedTypeSymbol? contract, string role)
    {
        foreach (var source in index.Documents.Where(source => source.Model.Compilation.Assembly.Identity.Equals(anchorType.ContainingAssembly.Identity)))
        {
            foreach (var declaration in source.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (source.Model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } type) continue;
                if (contract is not null && !type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)
                    && !Inherits(type, contract)) continue;
                if (contract is null || role == "unknown") index.MarkUnknown(type);
                else index.Add(type, role);
            }
        }
    }

    private static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol parent)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, parent)) return true;
        return false;
    }
}
