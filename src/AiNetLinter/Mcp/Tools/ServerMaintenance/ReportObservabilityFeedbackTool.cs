#nullable enable

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using RalfHuesing.Mcp.Observability;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// MCP-Tool <c>report_observability_feedback</c>: Ermoeglicht LLM-Agenten, strukturierte Fehlerberichte,
/// Falsch-Positive oder Feature-Requests zu melden, um AiNetLinter kontinuierlich zu verbessern.
/// Delegiert an die Feedback-Infrastruktur von <see cref="RalfHuesing.Mcp.Observability"/>.
/// </summary>
internal static class ReportObservabilityFeedbackTool
{
    private static readonly MethodInfo? ReportFeedbackMethod = typeof(McpObservabilityOptions).Assembly
        .GetType("RalfHuesing.Mcp.Observability.Internal.FeedbackTools")?
        .GetMethod("ReportFeedback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    internal static Task<CallToolResult> ExecuteAsync(
        IServiceProvider? services,
        string feedbackType,
        string title,
        string description,
        string? relatedTool = null,
        string severity = "medium",
        string? expectedBehavior = null,
        string? actualBehavior = null,
        string? additionalContext = null,
        CancellationToken ct = default)
    {
        if (ReportFeedbackMethod is not null)
        {
            try
            {
                var msg = ReportFeedbackMethod.Invoke(null, [
                    services!,
                    feedbackType,
                    title,
                    description,
                    relatedTool ?? string.Empty,
                    severity,
                    expectedBehavior ?? string.Empty,
                    actualBehavior ?? string.Empty,
                    additionalContext ?? string.Empty,
                ]) as string;

                return Task.FromResult(McpToolResults.Text(msg ?? $"Feedback '{title}' ({feedbackType}) wurde empfangen."));
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                return Task.FromResult(McpToolResults.Error(LinterErrorCodes.AnalysisFailed, $"Fehler beim Speichern des Feedbacks: {ex.InnerException.Message}"));
            }
        }

        return Task.FromResult(McpToolResults.Text($"Feedback '{title}' ({feedbackType}) wurde empfangen."));
    }
}
