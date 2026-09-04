#nullable enable

using System;
using System.Text;
using AiNetLinter.Mcp.Assemblies;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisOriginText
{
    internal static void Append(StringBuilder builder, AssemblyOrigin origin)
    {
        if (origin.IsDecompiled)
        {
            builder.AppendLine("Quelle: Dekompilat");
            return;
        }

        builder.AppendLine($"Quelle: `{origin.OriginKind}`");
    }
}
