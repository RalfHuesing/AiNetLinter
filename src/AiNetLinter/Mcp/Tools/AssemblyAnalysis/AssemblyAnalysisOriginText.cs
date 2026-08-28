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
            builder.AppendLine($"Herkunft: `{origin.OriginKind}` — `{origin.GeneratedDocumentPath}`");
            builder.AppendLine("Hinweis: Der angeforderte Code wurde dekompiliert und kann von der Originalquelle abweichen.");
            return;
        }

        builder.AppendLine($"Herkunft: `{origin.OriginKind}`");
        if (!string.IsNullOrWhiteSpace(origin.SourceProjectPath))
        {
            builder.AppendLine($"Source-Projekt: `{origin.SourceProjectPath}`");
        }

        if (origin.SourceSnapshotIdentity is { } identity)
        {
            builder.AppendLine($"Source-Snapshot: `{identity.RepositoryUrl}` @ `{identity.LoadedRevision}` — Solution `{identity.SolutionPath}`");
        }
    }
}
