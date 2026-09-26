#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal sealed class DeadCodeRazorUsage(DeadCodeUsageIndex index)
{
    internal void Collect(DeadCodeMarkupDocument source)
    {
        if (source.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) { CollectJavascript(source); return; }
        var name = Path.GetFileNameWithoutExtension(source.Path);
        var owners = index.SourceTypes.Where(type => type.Name == name
            && type.ContainingAssembly.Identity.Equals(source.Compilation.Assembly.Identity)).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().ToArray();
        foreach (var type in owners) BindLocalMembers(source, type, owners.Length == 1);
        foreach (Match tag in Matches(source.Text, @"<([A-Z][\w.]*)\b([^>]*)>")) BindTag(source, tag);
    }

    private void BindTag(DeadCodeMarkupDocument source, Match tag)
    {
        var name = tag.Groups[1].Value;
        var types = index.SourceTypes.Where(type => (type.Name == name || type.ToDisplayString() == name) && IsComponent(type))
            .Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>().ToArray();
        var role = types.Length == 1 ? source.Role : "unknown";
        foreach (var type in types)
        {
            BindComponent(type, role);
            foreach (Match attribute in Matches(tag.Groups[2].Value, @"(?:@bind-)?(\w+)\s*="))
                foreach (var property in type.GetMembers(attribute.Groups[1].Value).OfType<IPropertySymbol>())
                    if (property.GetAttributes().Any(value => value.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Components.ParameterAttribute"
                        && value.AttributeClass.Locations.All(location => !location.IsInSource))) index.Add(property, role);
        }
    }

    private void BindLocalMembers(DeadCodeMarkupDocument source, INamedTypeSymbol type, bool unique)
    {
        if (source.Text.Contains("@page ", StringComparison.Ordinal)) BindComponent(type, source.Role);
        if (RazorGeneratedDeclarations.HasMatchingGeneratedDeclaration(type, source.Path, source.Project)) return;
        foreach (var member in type.GetMembers())
        {
            var pattern = "(?:@(?:on\\w+|bind-\\w+)\\s*=\\s*\"|@)" + Regex.Escape(member.Name) + @"\b";
            if (Regex.IsMatch(source.Text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                index.Add(member, unique ? source.Role : "unknown", type);
        }
    }

    private void BindComponent(INamedTypeSymbol type, string role)
    {
        if (!IsComponent(type)) return;
        index.Add(type, role);
        new DeadCodeIndirectUsage(index).MarkHooks(type, role);
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            if (property.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() ==
                "Microsoft.AspNetCore.Components.InjectAttribute"
                && attribute.AttributeClass.Locations.All(location => !location.IsInSource))) index.Add(property, role);
    }

    private static bool IsComponent(INamedTypeSymbol type)
    {
        for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
            if (parent.ToDisplayString() == "Microsoft.AspNetCore.Components.ComponentBase"
                && parent.Locations.All(location => !location.IsInSource)) return true;
        return false;
    }

    private void CollectJavascript(DeadCodeMarkupDocument source)
    {
        foreach (Match match in Matches(source.Text, "DotNet\\.invokeMethod(?:Async)?\\(\\s*['\"]([^'\"]+)['\"]\\s*,\\s*['\"]([^'\"]+)['\"]"))
        {
            foreach (var type in index.SourceTypes.Where(type => type.ContainingAssembly.Name == match.Groups[1].Value))
                foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(method => MatchesJsIdentifier(method, match.Groups[2].Value)))
                    index.Add(method, source.Role);
        }
    }

    private static bool MatchesJsIdentifier(IMethodSymbol method, string identifier) => method.GetAttributes().Any(attribute =>
        attribute.AttributeClass?.ToDisplayString() == "Microsoft.JSInterop.JSInvokableAttribute"
        && attribute.AttributeClass.Locations.All(location => !location.IsInSource)
        && (attribute.ConstructorArguments.FirstOrDefault().Value as string ?? method.Name) == identifier);

    private static MatchCollection Matches(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
