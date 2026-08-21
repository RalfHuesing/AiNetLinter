#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Observability;

internal static class McpLogReportFormatter
{
    private const string TextFormat = "text";
    private const string JsonFormat = "json";

    internal static string Format(McpLogAnalysisReport report, string? format)
    {
        return format?.Trim().ToLowerInvariant() switch
        {
            null or "" or TextFormat => FormatText(report),
            JsonFormat => JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }),
            _ => throw new ArgumentException("Format muss 'text' oder 'json' sein.", nameof(format)),
        };
    }

    private static string FormatText(McpLogAnalysisReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# MCP-Call-Log-Auswertung");
        builder.AppendLine();
        builder.AppendLine($"- Eingabe: {report.InputPath}");
        builder.AppendLine($"- Log-Dateien: {report.LogFiles.Count}");
        builder.AppendLine($"- Tool-Calls: {report.ToolCallCount}");
        builder.AppendLine($"- Ungueltige JSONL-Zeilen: {report.InvalidLineCount}");
        foreach (var detail in report.InvalidLineDetails)
        {
            builder.AppendLine($"  - {detail}");
        }
        builder.AppendLine($"- Ignorierte Nicht-Tool-Records: {report.IgnoredRecordCount}");
        builder.AppendLine();
        AppendToolCounts(builder, report);
        AppendErrorCounts(builder, report);
        AppendLoadingCounts(builder, report);
        AppendCompletenessCounts(builder, report);
        AppendSessions(builder, report);
        return builder.ToString().TrimEnd();
    }

    private static void AppendToolCounts(StringBuilder builder, McpLogAnalysisReport report)
    {
        builder.AppendLine("## Calls pro Tool");
        foreach (var item in report.CallsPerTool)
        {
            builder.AppendLine($"- {item.Key}: {item.Value}");
        }

        if (report.CallsPerTool.Count == 0)
        {
            builder.AppendLine("- keine");
        }

        builder.AppendLine();
    }

    private static void AppendErrorCounts(StringBuilder builder, McpLogAnalysisReport report)
    {
        builder.AppendLine("## Fehler");
        builder.AppendLine($"- isError-Ergebnisse: {report.ErrorResultCount}/{report.ToolCallCount} ({FormatPercent(report.IsErrorRate)})");
        foreach (var item in report.ErrorCodes)
        {
            builder.AppendLine($"- {item.Key}: {item.Value}");
        }

        if (report.ErrorCodes.Count == 0)
        {
            builder.AppendLine("- Fehlercodes: keine");
        }

        builder.AppendLine();
    }

    private static void AppendLoadingCounts(StringBuilder builder, McpLogAnalysisReport report)
    {
        builder.AppendLine("## Loading-Retry-Bursts");
        builder.AppendLine($"- Bursts: {report.LoadingRetryBurstCount}");
        builder.AppendLine($"- Calls in Bursts: {report.LoadingRetryCallCount}");
        builder.AppendLine();
    }

    private static void AppendCompletenessCounts(StringBuilder builder, McpLogAnalysisReport report)
    {
        builder.AppendLine("## Truncation/Completeness");
        builder.AppendLine($"- Erkennung: {report.CompletenessDetection}");
        builder.AppendLine($"- Response-Trunkierungen: {report.ResponseTruncatedCount}");
        builder.AppendLine($"- Vollstaendig-Hinweise: {report.CompletenessCompleteCount}");
        builder.AppendLine($"- Trunkierte/gekappte Antworten: {report.CompletenessTruncatedCount}");
        builder.AppendLine($"- Unbekannter Completeness-Status: {report.CompletenessUnknownCount}");
        builder.AppendLine();
    }

    private static void AppendSessions(StringBuilder builder, McpLogAnalysisReport report)
    {
        builder.AppendLine("## Sessions");
        if (report.Sessions.Count == 0)
        {
            builder.AppendLine("- keine");
            return;
        }

        foreach (var session in report.Sessions)
        {
            var sequence = string.Join(" -> ", session.ToolSequence);
            builder.AppendLine($"- {session.LogFile} | PID {session.ProcessId} | Instance {session.InstanceId} | " +
                $"Calls {session.ToolCallCount} | Dauer {session.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s | " +
                $"Sequenz: {sequence}");
        }
    }

    private static string FormatPercent(double value) =>
        value.ToString("P2", CultureInfo.InvariantCulture);
}
