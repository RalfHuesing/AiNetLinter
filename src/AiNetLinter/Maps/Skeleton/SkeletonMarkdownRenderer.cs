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
        sb.AppendLine();
        sb.AppendLine("---");

        var byNamespace = types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var totalTypesWritten = 0;

        for (int i = 0; i < byNamespace.Count; i++)
        {
            var ns = byNamespace[i];
            sb.AppendLine();
            sb.AppendLine($"## {ns.Key}");

            var sortedTypes = ns.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
            for (int j = 0; j < sortedTypes.Count; j++)
            {
                var type = sortedTypes[j];
                AppendType(sb, type);
                totalTypesWritten++;

                if (totalTypesWritten < types.Count)
                {
                    sb.AppendLine("---");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendType(StringBuilder sb, SkeletonTypeInfo type)
    {
        sb.AppendLine();
        var modifierTag = BuildModifierTag(type.Modifiers);
        var basePart = type.BaseTypes != null ? $" {type.BaseTypes}" : "";
        sb.AppendLine($"### {type.Name}{basePart}{modifierTag} — `{type.RelativePath}`");

        if (type.Members.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("```csharp");

            bool hasWrittenAny = false;
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.Field, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.Constructor, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.Property, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.PublicMethod, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.InternalMethod, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.Event, hasWrittenAny);
            hasWrittenAny = AppendMembersOfKind(sb, type.Members, MemberKind.PrivateMethod, hasWrittenAny);

            sb.AppendLine("```");
        }
        sb.AppendLine();
    }

    private static bool AppendMembersOfKind(
        StringBuilder sb,
        IReadOnlyList<SkeletonMemberInfo> members,
        MemberKind kind,
        bool hasWrittenAny)
    {
        var filtered = members.Where(m => m.Kind == kind).ToList();
        if (filtered.Count == 0) return hasWrittenAny;

        if (hasWrittenAny)
        {
            sb.AppendLine();
        }

        foreach (var m in filtered)
        {
            var line = m.MetaComment != null
                ? $"{m.Signature} /* {m.MetaComment} */"
                : m.Signature;
            sb.AppendLine(line);
        }

        return true;
    }

    private static string BuildModifierTag(string modifiers)
    {
        if (modifiers.Contains("sealed"))   return " `sealed`";
        if (modifiers.Contains("abstract")) return " `abstract`";
        if (modifiers.Contains("static"))   return " `static`";
        return "";
    }
}
