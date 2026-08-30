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
    Canceled,
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
/// Interner Kontext fuer das Logging eines ausgefuehrten Tool-Calls.
/// </summary>
internal sealed record CallLogContext(
    string ToolName,
    string ArgumentsJson,
    long DurationMs,
    McpCallDetails Details,
    int? ConnectionId);

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
                    var logContext = new CallLogContext(
                        context.Params.Name,
                        argumentsJson,
                        stopwatch.ElapsedMilliseconds,
                        details,
                        connectionId);
                    WriteCompletedCall(logContext);
                    return result;
                }
                catch (Exception ex)
                {
                    details = ClassifyException(ex, cancellationToken.IsCancellationRequested);
                    var logContext = new CallLogContext(
                        context.Params.Name,
                        argumentsJson,
                        stopwatch.ElapsedMilliseconds,
                        details,
                        connectionId);
                    WriteCompletedCall(logContext, details.Status == McpCallStatus.Exception ? ex : null);
                    throw;
                }
            }));
    }

    internal static McpCallDetails ClassifyException(Exception exception, bool requestCancellationRequested)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is OperationCanceledException && requestCancellationRequested
            ? new McpCallDetails(McpCallStatus.Canceled)
            : new McpCallDetails(McpCallStatus.Exception, ExceptionErrorCode, exception.Message);
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
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        const string prefix = "[ERROR]:";
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.StartsWith("[INFO]:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return null;
            }

            var afterPrefix = trimmed.Substring(prefix.Length).Trim();
            var colonIndex = afterPrefix.IndexOf(':');
            if (colonIndex > 0)
            {
                var code = afterPrefix.Substring(0, colonIndex).Trim();
                var message = afterPrefix.Substring(colonIndex + 1).Trim();
                return (code, message);
            }

            return (UnknownErrorCode, afterPrefix);
        }

        return null;
    }

    private static bool IsEmptyResult(string? firstText)
    {
        if (string.IsNullOrWhiteSpace(firstText))
        {
            return true;
        }

        var lines = firstText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("[INFO]:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ZeroHitPrefixes.Any(p => trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (trimmed.Contains("0 Treffer fuer das angegebene Pattern", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            break;
        }

        return false;
    }

    private static void WriteCompletedCall(CallLogContext context, Exception? exception = null) =>
        WriteCompletedCall(Log.Logger, context, exception);

    internal static void WriteCompletedCall(ILogger logger, CallLogContext context, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var contextualLogger = context.ConnectionId is { } id
            ? logger.ForContext(nameof(CallLogContext.ConnectionId), id)
            : logger;

        switch (context.Details.Status)
        {
            case McpCallStatus.RecoverableError:
                LogRecoverableError(contextualLogger, context);
                break;

            case McpCallStatus.ProtocolError:
            case McpCallStatus.Exception:
                LogProtocolError(contextualLogger, context, exception);
                break;

            case McpCallStatus.Empty:
                LogStatus(contextualLogger, "[EMPTY]", context);
                break;

            case McpCallStatus.Loading:
                LogStatus(contextualLogger, "[LOADING]", context);
                break;

            case McpCallStatus.Canceled:
                LogStatus(contextualLogger, "[CANCELED]", context);
                break;

            case McpCallStatus.Success:
            default:
                LogStatus(contextualLogger, "[SUCCESS]", context);
                break;
        }
    }

    private static void LogRecoverableError(ILogger logger, CallLogContext context)
    {
        logger.Warning(
            "MCP-Tool-Call [RECOVERABLE_ERROR]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
            context.ToolName,
            context.Details.Status.ToString(),
            context.DurationMs,
            context.Details.ErrorCode ?? UnknownErrorCode,
            context.Details.ErrorMessage ?? string.Empty,
            context.ArgumentsJson,
            context.ConnectionId);
    }

    private static void LogProtocolError(ILogger logger, CallLogContext context, Exception? exception)
    {
        var tag = context.Details.Status == McpCallStatus.Exception ? "EXCEPTION" : "PROTOCOL_ERROR";
        var errorCode = context.Details.ErrorCode ?? (context.Details.Status == McpCallStatus.Exception ? ExceptionErrorCode : UnknownErrorCode);
        var errorMessage = context.Details.ErrorMessage ?? exception?.Message ?? string.Empty;

        if (exception is not null)
        {
            logger.Error(
                exception,
                "MCP-Tool-Call [{Tag}]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
                tag,
                context.ToolName,
                context.Details.Status.ToString(),
                context.DurationMs,
                errorCode,
                errorMessage,
                context.ArgumentsJson,
                context.ConnectionId);
        }
        else
        {
            logger.Error(
                "MCP-Tool-Call [{Tag}]: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}, Arguments={Arguments}, ConnectionId={ConnectionId}",
                tag,
                context.ToolName,
                context.Details.Status.ToString(),
                context.DurationMs,
                errorCode,
                errorMessage,
                context.ArgumentsJson,
                context.ConnectionId);
        }
    }

    private static void LogStatus(ILogger logger, string tag, CallLogContext context)
    {
        logger.Information(
            "MCP-Tool-Call {Tag}: ToolName={ToolName}, Status={Status}, DauerMs={DurationMs}, Arguments={Arguments}, ConnectionId={ConnectionId}",
            tag,
            context.ToolName,
            context.Details.Status.ToString(),
            context.DurationMs,
            context.ArgumentsJson,
            context.ConnectionId);
    }
}
