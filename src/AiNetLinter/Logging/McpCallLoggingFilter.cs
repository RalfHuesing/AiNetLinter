#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;

namespace AiNetLinter.Logging;

/// <summary>
/// Protokolliert abgeschlossene MCP-Tool-Aufrufe in das bestehende Serilog-Systemlog.
/// Der Filter kennt weder Argumente noch Antwort-Payloads und wird nur im Prozess mit
/// echtem MCP-SDK-Server installiert; der ThinClient selbst registriert keinen Filter.
/// </summary>
internal static class McpCallLoggingFilter
{
    internal const string ExceptionErrorCode = "MCP_TOOL_EXCEPTION";
    internal const string UnknownErrorCode = "MCP_TOOL_ERROR";

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
                var isError = false;
                string? errorCode = null;
                try
                {
                    var result = await next(context, cancellationToken).ConfigureAwait(false);
                    isError = result.IsError == true;
                    errorCode = isError ? ExtractErrorCode(result) : null;
                    return result;
                }
                catch
                {
                    isError = true;
                    errorCode = ExceptionErrorCode;
                    throw;
                }
                finally
                {
                    WriteCompletedCall(
                        context.Params.Name,
                        stopwatch.ElapsedMilliseconds,
                        isError,
                        errorCode,
                        connectionId);
                }
            }));
    }

    internal static string? ExtractErrorCode(CallToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsError != true)
        {
            return null;
        }

        foreach (var content in result.Content)
        {
            if (content is not TextContentBlock { Text: { } text })
            {
                continue;
            }

            const string prefix = "[ERROR]: ";
            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var separator = text.IndexOf(':', prefix.Length);
            if (separator > prefix.Length)
            {
                return text[prefix.Length..separator];
            }
        }

        return UnknownErrorCode;
    }

    private static void WriteCompletedCall(
        string toolName,
        long durationMs,
        bool isError,
        string? errorCode,
        int? connectionId)
    {
        var logger = connectionId is { } id
            ? Log.ForContext("ConnectionId", id)
            : Log.Logger;

        if (isError)
        {
            logger.Information(
                "MCP-Tool-Call abgeschlossen: ToolName={ToolName}, DauerMs={DurationMs}, IsError={IsError}, ErrorCode={ErrorCode}, ConnectionId={ConnectionId}",
                toolName,
                durationMs,
                true,
                errorCode ?? UnknownErrorCode,
                connectionId);
            return;
        }

        logger.Information(
            "MCP-Tool-Call abgeschlossen: ToolName={ToolName}, DauerMs={DurationMs}, IsError={IsError}, ConnectionId={ConnectionId}",
            toolName,
            durationMs,
            false,
            connectionId);
    }
}
