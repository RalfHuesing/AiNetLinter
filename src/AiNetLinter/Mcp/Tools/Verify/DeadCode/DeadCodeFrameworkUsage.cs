#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>Bindet Member nur bei semantisch belegten Frameworkaufrufen.</summary>
internal sealed class DeadCodeFrameworkUsage(DeadCodeUsageIndex index)
{
    internal void Collect(DeadCodeBindingCall context)
    {
        if (context.Method.Locations.Any(location => location.IsInSource)) return;
        var api = context.Method.ContainingType.ToDisplayString();
        if (api == "System.Text.Json.JsonSerializer") CollectJson(context);
        if (api == "Microsoft.Extensions.Configuration.ConfigurationBinder") CollectConfiguration(context);
        if (api == "Dapper.SqlMapper") MarkUncertainDataContract(context);
        if (IsRegistration(context.Method))
            foreach (var type in context.Method.TypeArguments.OfType<INamedTypeSymbol>())
                new DeadCodeIndirectUsage(index).MarkHooks(type, context.Source.Role);
        if (api == "Microsoft.AspNetCore.Builder.UseMiddlewareExtensions" && context.Method.Name == "UseMiddleware")
            foreach (var type in context.Method.TypeArguments.OfType<INamedTypeSymbol>()) BindMiddleware(type, context.Source.Role);
    }

    private static bool IsRegistration(IMethodSymbol method) => method.ContainingType.ToDisplayString() switch
    {
        "Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions" => method.Name == "ConfigureOptions",
        "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions" => method.Name is "AddSingleton" or "AddScoped" or "AddTransient",
        "Microsoft.Extensions.DependencyInjection.ServiceCollectionHostedServiceExtensions" => method.Name == "AddHostedService",
        _ => false
    };

    private void CollectJson(DeadCodeBindingCall context)
    {
        var types = context.Method.TypeArguments.OfType<INamedTypeSymbol>().ToArray();
        foreach (var type in types)
        {
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public) continue;
                if (IsAlwaysIgnored(property)) continue;
                index.Add(property, context.Source.Role);
            }
        }
    }

    private static bool IsAlwaysIgnored(IPropertySymbol property) => property.GetAttributes().Any(attribute =>
        attribute.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonIgnoreAttribute"
        && attribute.AttributeClass.Locations.All(location => !location.IsInSource)
        && (!attribute.NamedArguments.Any(pair => pair.Key == "Condition")
            || attribute.NamedArguments.Any(pair => pair.Key == "Condition" && pair.Value.Value is 1)));

    private void CollectConfiguration(DeadCodeBindingCall context)
    {
        var types = context.Method.TypeArguments.OfType<INamedTypeSymbol>().ToList();
        if (context.Method.Name == "Bind")
            types.AddRange(context.Call.ArgumentList.Arguments.Select(argument =>
                DeadCodeIndirectUsage.ResolveExpressionType(argument.Expression, context.Source.Model)).OfType<INamedTypeSymbol>());
        var customOptions = context.Method.Parameters.Any(parameter => parameter.Type.Name == "Action");
        foreach (var type in types)
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic && !property.IsIndexer))
            {
                if (customOptions) index.MarkUnknown(property);
                else if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public) index.Add(property, context.Source.Role);
            }
    }

    private void MarkUncertainDataContract(DeadCodeBindingCall context)
    {
        foreach (var type in context.Method.TypeArguments.OfType<INamedTypeSymbol>())
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic))
                index.MarkUnknown(property);
    }

    private void BindMiddleware(INamedTypeSymbol type, string role)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.Name is not ("Invoke" or "InvokeAsync") || method.IsStatic) continue;
            if (method.DeclaredAccessibility != Accessibility.Public) continue;
            if (method.Parameters.FirstOrDefault()?.Type.ToDisplayString() != "Microsoft.AspNetCore.Http.HttpContext") continue;
            if (method.ReturnType.ToDisplayString() != "System.Threading.Tasks.Task") continue;
            index.Add(method, role);
        }
    }
}
