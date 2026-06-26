#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Rendert eine Liste von <see cref="SkeletonTypeInfo"/>-Objekten als Markdown.
/// </summary>
internal static class SkeletonMarkdownRenderer
{
    internal static string Render(
        IReadOnlyList<SkeletonTypeInfo> types,
        string solutionPath,
        DateTimeOffset generatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Skeleton Map");
        sb.AppendLine();
        sb.AppendLine($"> Erzeugt: {generatedAt:yyyy-MM-dd HH:mm}"
            + $" | Typen: {types.Count}"
            + $" | Member: {types.Sum(t => t.Members.Count)}"
            + $" | Pfad: {solutionPath.Replace('\\', '/')}");

        var byNamespace = types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var ns in byNamespace)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"## {ns.Key}");

            foreach (var type in ns.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                AppendType(sb, type);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendType(StringBuilder sb, SkeletonTypeInfo type)
    {
        sb.AppendLine();
        var modifierTag = BuildModifierTag(type.Modifiers);
        var basePart = type.BaseTypes != null ? $" {type.BaseTypes}" : "";
        sb.AppendLine($"### {type.Name}{basePart}{modifierTag}");
        sb.AppendLine($"`{type.RelativePath}`");
        sb.AppendLine();
        sb.AppendLine("```csharp");

        AppendMembersOfKind(sb, type.Members, MemberKind.Field);
        AppendMembersOfKind(sb, type.Members, MemberKind.Constructor, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.Property, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.PublicMethod, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.InternalMethod, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.Event, addBlankBefore: true);
        AppendMembersOfKind(sb, type.Members, MemberKind.PrivateMethod, addBlankBefore: true);

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("---");
    }

    private static void AppendMembersOfKind(
        StringBuilder sb,
        IReadOnlyList<SkeletonMemberInfo> members,
        MemberKind kind,
        bool addBlankBefore = false)
    {
        var filtered = members.Where(m => m.Kind == kind).ToList();
        if (filtered.Count == 0) return;

        if (addBlankBefore && sb.Length > 0 && sb[^1] != '\n')
            sb.AppendLine();
        else if (addBlankBefore)
            sb.AppendLine();

        foreach (var m in filtered)
        {
            var line = m.MetaComment != null
                ? $"{m.Signature} // {m.MetaComment}"
                : m.Signature;
            sb.AppendLine(line);
        }
    }

    private static string BuildModifierTag(string modifiers)
    {
        if (modifiers.Contains("sealed"))   return " `sealed`";
        if (modifiers.Contains("abstract")) return " `abstract`";
        if (modifiers.Contains("static"))   return " `static`";
        return "";
    }
}
