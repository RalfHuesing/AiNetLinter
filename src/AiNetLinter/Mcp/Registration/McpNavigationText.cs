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
            $"- contractVersion: `{navigation.ContractVersion}`",
            $"- targetPath: `{navigation.Target.TargetPath}`",
            $"- origin: `{navigation.Target.Origin}`",
            $"- snapshot: `{navigation.Snapshot.Fingerprint}` (kind: `{navigation.Snapshot.Kind}`, fresh: `{navigation.Snapshot.Fresh.ToString().ToLowerInvariant()}`)",
            $"- status: operation=`{navigation.Status.Operation}`, completeness=`{navigation.Status.Completeness}`{(navigation.Status.Code is null ? string.Empty : $", code=`{navigation.Status.Code}`")}",
            $"- next: {(navigation.Next is null ? "none" : $"`{navigation.Next.Kind}` — {navigation.Next.Action}")}");
}
