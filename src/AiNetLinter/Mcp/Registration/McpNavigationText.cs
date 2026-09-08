#nullable enable

using System;

namespace AiNetLinter.Mcp.Registration;

using AiNetLinter.Mcp;

/// <summary>Textprojektion des gemeinsamen Navigationsaggregats fuer Toolantworten.</summary>
internal static class McpNavigationText
{
    internal static string Format(McpNavigationPayload navigation) =>
        string.Join(
            Environment.NewLine,
            "## Navigation",
            $"- targetPath: `{navigation.Target.TargetPath}`",
            $"- origin: `{navigation.Origin}`",
            $"- snapshot: `{navigation.Snapshot.Fingerprint}` (kind: `{navigation.Snapshot.Kind}`, fresh: `{navigation.Snapshot.Fresh.ToString().ToLowerInvariant()}`)",
            $"- operationStatus: `{navigation.OperationStatus}`",
            $"- result: available=`{navigation.Result.Available.ToString().ToLowerInvariant()}`{(navigation.Result.Code is null ? string.Empty : $", code=`{navigation.Result.Code}`")}",
            $"- completeness: `{navigation.Completeness}`",
            $"- next: `{navigation.Next.Kind}` — {navigation.Next.Action}");
}
