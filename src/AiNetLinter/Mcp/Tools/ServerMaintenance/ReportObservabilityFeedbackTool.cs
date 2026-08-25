#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Logging;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Serilog;

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

/// <summary>
/// DTO fuer die strukturierte Antwort des MCP-Tools <c>report_observability_feedback</c>.
/// </summary>
internal sealed record FeedbackResultPayload(
    bool Received,
    string Title,
    string FeedbackType,
    string Severity,
    DateTime TimestampUtc);

/// <summary>
/// Parameter-Objekt fuer den Aufruf von <see cref="ReportObservabilityFeedbackTool.ExecuteAsync"/>.
/// </summary>
internal sealed record ReportObservabilityFeedbackParameters(
    string? FeedbackType,
    string? Title,
    string? Description,
    string? RelatedTool = null,
    string? Severity = "medium",
    string? ExpectedBehavior = null,
    string? ActualBehavior = null,
    string? AdditionalContext = null,
    string? ProjectRoot = null);

/// <summary>
/// MCP-Tool <c>report_observability_feedback</c>: Ermoeglicht KI-Agenten, Probleme,
/// Fehlverhalten, verwirrende Ausgaben, False Positives oder Feature-Wuensche direkt
/// an den Linter-Server zu melden.
/// Schreibt den Report strukturiert und unbeschraenkt ins System-Log (Serilog).
/// </summary>
internal static class ReportObservabilityFeedbackTool
{
    internal static Task<CallToolResult> ExecuteAsync(ReportObservabilityFeedbackParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (ValidateParameters(parameters) is { } validationError)
        {
            return Task.FromResult(validationError);
        }

        var normalizedType = parameters.FeedbackType!.Trim();
        var normalizedTitle = parameters.Title!.Trim();
        var normalizedDesc = parameters.Description!.Trim();
        var normalizedSeverity = string.IsNullOrWhiteSpace(parameters.Severity)
            ? "medium"
            : parameters.Severity.Trim().ToLowerInvariant();

        LogFeedback(parameters, normalizedType, normalizedTitle, normalizedDesc, normalizedSeverity);

        var responseText = $"[INFO]: Feedback '{normalizedTitle}' ({normalizedType}) erfolgreich protokolliert. " +
                            "Vielen Dank! Bitte mit dem besten verfügbaren Workaround fortfahren.";

        var payload = new FeedbackResultPayload(
            Received: true,
            Title: normalizedTitle,
            FeedbackType: normalizedType,
            Severity: normalizedSeverity,
            TimestampUtc: DateTime.UtcNow);

        return Task.FromResult(McpToolResults.Text(responseText, payload));
    }

    private static CallToolResult? ValidateParameters(ReportObservabilityFeedbackParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.FeedbackType))
        {
            return McpToolResults.InvalidArgument(
                "Pflichtparameter 'feedbackType' fehlt oder ist leer.",
                "Gueltige Werte: 'issue', 'feature_request', 'confusing_output', 'false_positive'.");
        }

        if (string.IsNullOrWhiteSpace(parameters.Title))
        {
            return McpToolResults.InvalidArgument(
                "Pflichtparameter 'title' fehlt oder ist leer.",
                "Kurzen, praegnanten Titel angeben (max. 120 Zeichen).");
        }

        if (string.IsNullOrWhiteSpace(parameters.Description))
        {
            return McpToolResults.InvalidArgument(
                "Pflichtparameter 'description' fehlt oder ist leer.",
                "Detaillierte Fehler- oder Wunschbeschreibung angeben.");
        }

        return null;
    }

    private static void LogFeedback(
        ReportObservabilityFeedbackParameters parameters,
        string normalizedType,
        string normalizedTitle,
        string normalizedDesc,
        string normalizedSeverity)
    {
        const string template =
            "==================== AGENT FEEDBACK EMPFANGEN ====================\n" +
            "Typ:          {FeedbackType}\n" +
            "Schweregrad:  {Severity}\n" +
            "Titel:        {Title}\n" +
            "Tool:         {RelatedTool}\n" +
            "Projekt:      {ProjectRoot}\n" +
            "Beschreibung: {Description}\n" +
            "Erwartet:     {ExpectedBehavior}\n" +
            "Tatsaechlich: {ActualBehavior}\n" +
            "Kontext:      {AdditionalContext}\n" +
            "==================================================================";

        Action<string, object?[]> logAction = normalizedSeverity switch
        {
            "critical" or "high" or "error" => Log.Error,
            "warn" or "warning" => Log.Warning,
            _ => Log.Information
        };

        logAction(
            template,
            [
                normalizedType,
                normalizedSeverity,
                normalizedTitle,
                parameters.RelatedTool ?? "n/a",
                parameters.ProjectRoot ?? "n/a",
                normalizedDesc,
                parameters.ExpectedBehavior ?? "n/a",
                parameters.ActualBehavior ?? "n/a",
                parameters.AdditionalContext ?? "n/a"
            ]);
    }
}
