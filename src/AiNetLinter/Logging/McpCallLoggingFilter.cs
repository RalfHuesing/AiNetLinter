#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace AiNetLinter.Logging;

/// <summary>
/// Status-Klassifizierung eines MCP-Tool-Aufrufs fuer differenzierte Logs.
/// </summary>
internal enum McpCallStatus
{
    Success,
    Empty,
    RecoverableError,
    ProtocolError,
    Loading,
    Exception,
}

/// <summary>
/// Analyseergebnis eines MCP-Tool-Aufrufs.
/// </summary>
internal sealed record McpCallDetails(
    McpCallStatus Status,
    string? ErrorCode = null,
    string? ErrorMessage = null);

/// <summary>
/// Protokolliert ausgefuehrte MCP-Tool-Aufrufe mit Argumenten, Laufzeit und Status-Klassifizierung
/// in das Serilog-Systemlog.
/// Analysiert Ergebnisse differenziert nach Success, 0-Treffern (Empty), Recoverable-Fehlern
/// (z. B. INVALID_ARGUMENT, SYMBOL_NOT_FOUND) und echten Protokoll-/Laufzeitfehlern.
/// </summary>
internal static class McpCallLoggingFilter
{
    internal const string ExceptionErrorCode = "MCP_TOOL_EXCEPTION";
    internal const string UnknownErrorCode = "MCP_TOOL_ERROR";

    private static readonly string[] ZeroHitPrefixes =
    [
        "0 Treffer",
        "Keine ",
        "0 Verstöße",
        "0 Verstoesse",
        "0 Referenzen",
        "0 Duplikate",
        "0 Funde",
        "0 Betroffene",
        "0 betroffene",
        "0 Kanten",
        "0 Magic Values",
        "0 Symbole",
        "0 Dateien",
    ];

    internal static void Configure(IMcpServerBuilder builder, int? connectionId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!SystemLog.Config.McpCallLogging)
        {
            return;
        }

        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next =>
            async (context, cancellationToken) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var argumentsJson = FormatArguments(context.Params.Arguments);
                McpCallDetails details;

                try
                {
                    var result = await next(context, cancellationToken).ConfigureAwait(false);
                    details = AnalyzeResult(result);
                    WriteCompletedCall(
                        context.Params.Name,
                        argumentsJson,
                        stopwatch.ElapsedMilliseconds,
                        details,
                        connectionId);
                    return result;
                }
                catch (Exception ex)
                {
                    details = new McpCallDetails(McpCallStatus.Exception, ExceptionErrorCode, ex.Message);
                    WriteCompletedCall(
                        context.Params.Name,
                        argumentsJson,
                        stopwatch.ElapsedMilliseconds,
                        details,
                        connectionId,
                        ex);
                    throw;
                }
            }));
    }

    /// <summary>
    /// Formatiert uebergebene Argumente als kompaktes JSON fuer das Log.
    /// </summary>
    internal static string FormatArguments(object? arguments)
    {
        if (arguments is null)
        {
            return "{}";
        }

        if (arguments is IReadOnlyDictionary<string, object?> dict && dict.Count == 0)
        {
            return "{}";
        }

        try
        {
            return JsonSerializer.Serialize(arguments, McpJsonOptions.Default);
        }
        catch
        {
            return "<unserializable>";
        }
    }

    /// <summary>
    /// Analysiert das <see cref="CallToolResult"/> und klassifiziert den Status sowie Fehlerdetails.
    /// </summary>
    internal static McpCallDetails AnalyzeResult(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var firstText = GetFirstText(result);

        if (result.IsError == true)
        {
            if (firstText is not null && ParseStructuredError(firstText) is { } errorInfo)
            {
                return new McpCallDetails(McpCallStatus.ProtocolError, errorInfo.Code, errorInfo.Message);
            }

            return new McpCallDetails(McpCallStatus.ProtocolError, UnknownErrorCode, firstText);
        }

        if (firstText is not null)
        {
            if (ParseStructuredError(firstText) is { } recoverableInfo)
            {
                return new McpCallDetails(McpCallStatus.RecoverableError, recoverableInfo.Code, recoverableInfo.Message);
            }

            if (firstText.StartsWith("[INFO]:", StringComparison.Ordinal) &&
                firstText.Contains("Server laedt die Solution", StringComparison.OrdinalIgnoreCase))
            {
                return new McpCallDetails(McpCallStatus.Loading);
            }
        }

        if (IsEmptyResult(firstText))
        {
            return new McpCallDetails(McpCallStatus.Empty);
        }

        return new McpCallDetails(McpCallStatus.Success);
    }

    /// <summary>
    /// Rueckwaertskompatible Hilfsmethode fuer bestehende Tests und Aufrufer.
    /// </summary>
    internal static string? ExtractErrorCode(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var details = AnalyzeResult(result);
        return details.ErrorCode;
    }

    private static string? GetFirstText(CallToolResult result)
    {
        if (result.Content is null) return null;

        foreach (var block in result.Content)
        {
            if (block is TextContentBlock { Text: { } text })
            {
                return text;
            }
        }

        return null;
    }

    private static (string Code, string Message)? ParseStructuredError(string text)
    {
        const string prefix = "[ERROR]: ";
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var afterPrefix = text.Substring(prefix.Length);
        var firstLine = afterPrefix;
        var newlineIndex = afterPrefix.IndexOfAny(['\r', '\n']);
        if (newlineIndex >= 0)
        {
            firstLine = afterPrefix.Substring(0, newlineIndex);
        }

        var colonIndex = firstLine.IndexOf(':');
        if (colonIndex > 0)
        {
            var code = firstLine.Substring(0, colonIndex).Trim();
            var message = firstLine.Substring(colonIndex + 1).Trim();
            return (code, message);
        }

        return (UnknownErrorCode, firstLine.Trim());
    }

    private static bool IsEmptyResult(string? firstText)
    {
        if (string.IsNullOrWhiteSpace(firstText))
        {
            return true;
        }

        var trimmed = firstText.TrimStart();
        if (ZeroHitPrefixes.Any(p => trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return trimmed.Contains("0 Treffer fuer das angegebene Pattern", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteCompletedCall(
        string toolName,
        string argumentsJson,
        long durationMs,
        McpCallDetails details,
        int? connectionId,
        Exception? exception = null)
    {
        var logger = connectionId is { } id
            ? Log.ForContext("ConnectionId", id)
            : Log.Logger;

        switch (details.Status)
        {
            case McpCallStatus.RecoverableError:
                LogRecoverableError(logger, toolName, durationMs, details, argumentsJson, connectionId);
                break;

            case McpCallStatus.ProtocolError:
            case McpCallStatus.Exception:
                LogProtocolError(logger, toolName, durationMs, details, argumentsJson, connectionId, exception);
                break;

            case McpCallStatus.Empty:
                LogStatus(logger, "[EMPTY]", toolName, durationMs, details, argumentsJson, connectionId);
                break;

            case McpCallStatus.Loading:
                LogStatus(logger, "[LOADING]", toolName, durationMs, details, argumentsJson, connectionId);
                break;

            case McpCallStatus.Success:
            default:
                LogStatus(logger, "[SUCCESS]", toolName, durationMs, details, argumentsJson, connectionId);
                break;
        }
    }

    private static void LogRecoverableError(
        ILogger logger,
        string toolName,
        long durationMs,
        McpCallDetails details,
        string argumentsJson,
        int? connectionId)
    {
        logger.Warning(
            "MCP-Tool-Call [RECOVERABLE_ERROR]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
            toolName,
            details.Status.ToString(),
            durationMs,
            details.ErrorCode ?? UnknownErrorCode,
            details.ErrorMessage ?? string.Empty,
            argumentsJson,
            connectionId);
    }

    private static void LogProtocolError(
        ILogger logger,
        string toolName,
        long durationMs,
        McpCallDetails details,
        string argumentsJson,
        int? connectionId,
        Exception? exception)
    {
        var tag = details.Status == McpCallStatus.Exception ? "EXCEPTION" : "PROTOCOL_ERROR";
        var errorCode = details.ErrorCode ?? (details.Status == McpCallStatus.Exception ? ExceptionErrorCode : UnknownErrorCode);
        var errorMessage = details.ErrorMessage ?? exception?.Message ?? string.Empty;

        if (exception is not null)
        {
            logger.Error(
                exception,
                "MCP-Tool-Call [{Tag}]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
                tag,
                toolName,
                details.Status.ToString(),
                durationMs,
                errorCode,
                errorMessage,
                argumentsJson,
                connectionId);
        }
        else
        {
            logger.Error(
                "MCP-Tool-Call [{Tag}]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
                tag,
                toolName,
                details.Status.ToString(),
                durationMs,
                errorCode,
                errorMessage,
                argumentsJson,
                connectionId);
        }
    }

    private static void LogStatus(
        ILogger logger,
        string tag,
        string toolName,
        long durationMs,
        McpCallDetails details,
        string argumentsJson,
        int? connectionId)
    {
        logger.Information(
            "MCP-Tool-Call {Tag}: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, Arguments={Arguments}, ConnectionId={ConnectionId}",
            tag,
            toolName,
            details.Status.ToString(),
            durationMs,
            argumentsJson,
            connectionId);
    }
}
