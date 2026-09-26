#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal sealed class DeadCodeXamlUsage(DeadCodeUsageIndex index)
{
    internal void Collect(DeadCodeMarkupDocument source)
    {
        XElement root;
        try { root = XElement.Parse(source.Text); }
        catch (System.Xml.XmlException) { index.CoverageGaps.Add("xaml_parse"); return; }
        var className = root.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Class")?.Value;
        var codeBehind = className is null ? null : source.Compilation.GetTypeByMetadataName(className);
        if (codeBehind is not null) index.Add(codeBehind, source.Role);
        foreach (var element in root.DescendantsAndSelf()) CollectElement(new(source, element, codeBehind));
    }

    private void CollectElement(ElementContext context)
    {
        var type = ResolveType(context.Element.Name, context.Source.Compilation);
        if (type is not null) index.Add(type, context.Source.Role);
        foreach (var attribute in context.Element.Attributes())
        {
            BindStatic(context, attribute.Value);
            BindPath(context, attribute.Value);
            BindAttached(context, attribute.Name);
            if (context.CodeBehind is not null && type?.GetMembers(attribute.Name.LocalName).Any(member => member is IEventSymbol) == true)
                foreach (var handler in context.CodeBehind.GetMembers(attribute.Value)) index.Add(handler, context.Source.Role);
        }
    }

    private void BindStatic(ElementContext context, string value)
    {
        foreach (Match match in Matches(value, @"\{x:Static\s+(\w+):(\w+)\.(\w+)\}"))
        {
            var ns = context.Element.GetNamespaceOfPrefix(match.Groups[1].Value);
            if (ns is null) continue;
            var type = ResolveType(ns + match.Groups[2].Value, context.Source.Compilation);
            if (type is null) continue;
            foreach (var member in type.GetMembers(match.Groups[3].Value)) index.Add(member, context.Source.Role);
            index.Add(type, context.Source.Role);
        }
    }

    private void BindPath(ElementContext context, string value)
    {
        foreach (Match match in Matches(value, @"\{Binding\s+(?:Path=)?([\w.]+)"))
        {
            var type = FindDataContext(context);
            foreach (var part in match.Groups[1].Value.Split('.'))
            {
                var member = type?.GetMembers(part).OfType<IPropertySymbol>().FirstOrDefault();
                if (member is null) { MarkUnknownPath(context.Source, part); break; }
                index.Add(member, context.Source.Role);
                type = member.Type as INamedTypeSymbol;
            }
        }
    }

    private static INamedTypeSymbol? FindDataContext(ElementContext context)
    {
        foreach (var ancestor in context.Element.AncestorsAndSelf())
        {
            var declaration = ancestor.Elements().FirstOrDefault(element => element.Name.LocalName.EndsWith(".DataContext", StringComparison.Ordinal))?.Elements().FirstOrDefault();
            if (declaration is not null) return ResolveType(declaration.Name, context.Source.Compilation);
        }
        return null;
    }

    private void MarkUnknownPath(DeadCodeMarkupDocument source, string name)
    {
        foreach (var type in index.SourceTypes.Where(type => type.ContainingAssembly.Identity.Equals(source.Compilation.Assembly.Identity)))
            foreach (var member in type.GetMembers(name).OfType<IPropertySymbol>()) index.MarkUnknown(member, "markup_binding_context");
    }

    private void BindAttached(ElementContext context, XName name)
    {
        var parts = name.LocalName.Split('.');
        if (parts.Length != 2) return;
        var type = ResolveType(name.Namespace + parts[0], context.Source.Compilation);
        if (type is null) return;
        var property = type.GetMembers(parts[1] + "Property").OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.Type.ToDisplayString() == "System.Windows.DependencyProperty"
                && field.Type.Locations.All(location => !location.IsInSource));
        var role = property is null ? "unknown" : context.Source.Role;
        foreach (var member in type.GetMembers().Where(member => member.Name == "Get" + parts[1] || member.Name == "Set" + parts[1]))
            index.Add(member, role);
        if (property is not null) index.Add(property, role);
    }

    private static INamedTypeSymbol? ResolveType(XName name, Compilation compilation)
    {
        var ns = name.NamespaceName;
        if (ns.StartsWith("clr-namespace:", StringComparison.Ordinal))
            return compilation.GetTypeByMetadataName(ns[14..].Split(';')[0] + "." + name.LocalName);
        if (ns == "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
            return compilation.GetTypeByMetadataName("System.Windows.Controls." + name.LocalName)
                ?? compilation.GetTypeByMetadataName("System.Windows." + name.LocalName);
        return null;
    }

    private static MatchCollection Matches(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private sealed record ElementContext(DeadCodeMarkupDocument Source, XElement Element, INamedTypeSymbol? CodeBehind);
}
