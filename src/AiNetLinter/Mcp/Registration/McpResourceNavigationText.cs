#nullable enable

using System;

namespace AiNetLinter.Mcp.Registration;

using AiNetLinter.Mcp;

/// <summary>Textprojektion des gemeinsamen Navigationskerns fuer MCP-Resources.</summary>
internal static class McpResourceNavigationText
{
    internal static string Format(McpResourceNavigationParameters parameters)
    {
        var lint = parameters.Target.Capabilities.Lint switch
        {
            AnalysisCapabilityStatus.Supported => "supported",
            AnalysisCapabilityStatus.NotConfigured => "not_configured",
            _ => "unsupported",
        };

        return string.Join(
            Environment.NewLine,
            $"- targetPath: `{parameters.Target.CanonicalPath}`",
            $"- origin: `{(parameters.Target.Origin == AnalysisTargetOrigin.Source ? "source" : "decompiled")}`",
            $"- snapshot: `{parameters.Target.Fingerprint}` (kind: `target-file`, fresh: `true`)",
            $"- capabilities: navigation=`supported`, lint=`{lint}`",
            $"- operationStatus: `{parameters.OperationStatus}`",
            $"- completeness: `{parameters.Completeness}`",
            $"- next: `{parameters.NextKind}` — {parameters.NextAction}");
    }
}

internal sealed record McpResourceNavigationParameters(
    AnalysisTarget Target,
    string OperationStatus,
    string Completeness,
    string NextKind,
    string NextAction);
