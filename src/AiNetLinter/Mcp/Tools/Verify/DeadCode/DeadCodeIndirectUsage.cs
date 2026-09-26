#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>Ordnet vorhandene Laufzeitkanäle konkreten Typen und Membern zu.</summary>
internal sealed class DeadCodeIndirectUsage(DeadCodeUsageIndex index)
{
    internal void Collect(CancellationToken ct)
    {
        foreach (var source in index.Documents)
        {
            foreach (var binary in source.Root.DescendantNodes().OfType<BinaryExpressionSyntax>())
                if (source.Model.GetSymbolInfo(binary, ct).Symbol is IMethodSymbol { ContainingType.IsRecord: true } operation)
                    MarkRecord(operation.ContainingType, source.Role);
            foreach (var call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                ct.ThrowIfCancellationRequested();
                if (source.Model.GetSymbolInfo(call, ct).Symbol is not IMethodSymbol method) continue;
                var context = new DeadCodeBindingCall(source, call, method);
                CollectRecordUse(context);
                new DeadCodeFrameworkUsage(index).Collect(context);
                CollectMetadataDispatch(context);
                new DeadCodeReflectionUsage(index).Collect(context);
            }
        }
    }

    private void CollectRecordUse(DeadCodeBindingCall context)
    {
        var method = context.Method;
        var type = method.ContainingType;
        if (type.IsRecord && method.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct")
            MarkRecord(type, context.Source.Role, method.Name == "ToString");
        if (type.ContainingNamespace.ToDisplayString() is "System.Collections.Generic" or "System.Collections.Concurrent"
            && type.Name is "Dictionary" or "ConcurrentDictionary" or "HashSet"
            && type.TypeArguments.FirstOrDefault() is INamedTypeSymbol { IsRecord: true } key)
            MarkRecord(key, context.Source.Role);
    }

    private void MarkRecord(INamedTypeSymbol type, string role, bool printing = false)
    {
        var storedProperties = type.GetMembers().OfType<IFieldSymbol>().Select(field => field.AssociatedSymbol)
            .OfType<IPropertySymbol>().ToHashSet(SymbolEqualityComparer.Default);
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic))
            if (printing || storedProperties.Contains(property)) index.Add(property, role);
    }

    private void CollectMetadataDispatch(DeadCodeBindingCall context)
    {
        if (context.Method.Locations.Any(location => location.IsInSource)) return;
        foreach (var argument in context.Call.ArgumentList.Arguments)
        {
            var type = ResolveExpressionType(argument.Expression, context.Source.Model);
            if (type is not null) MarkHooks(type, context.Source.Role);
        }
        if (context.Call.Expression is MemberAccessExpressionSyntax access)
        {
            var type = ResolveExpressionType(access.Expression, context.Source.Model);
            if (type is not null) MarkHooks(type, context.Source.Role);
        }
    }

    internal void MarkHooks(INamedTypeSymbol type, string role)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol { OverriddenMethod: { } parent }
                && parent.Locations.All(location => !location.IsInSource)) index.Add(member, role);
            foreach (var contract in type.AllInterfaces.SelectMany(contract => contract.GetMembers()))
            {
                if (contract.Locations.All(location => !location.IsInSource)
                    && SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(contract), member))
                    index.Add(member, role);
            }
        }
    }

    internal static INamedTypeSymbol? ResolveExpressionType(ExpressionSyntax expression, SemanticModel model)
    {
        if (model.GetSymbolInfo(expression).Symbol is ILocalSymbol local)
        {
            var declaration = local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;
            if (declaration?.Initializer?.Value is { } value)
                return model.GetTypeInfo(value).Type as INamedTypeSymbol;
        }
        return model.GetTypeInfo(expression).Type as INamedTypeSymbol;
    }

}

internal sealed record DeadCodeBindingCall(DeadCodeUsageDocument Source, InvocationExpressionSyntax Call, IMethodSymbol Method);
