#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Read-only MCP-Tool fuer eine physische, projektgebundene Dateilandkarte. Der Tool-Dispatch
/// validiert nur den Vertrag; Walk, Filter und Rendering liegen in den dafuer getrennten Klassen.
/// </summary>
internal static class GetFileTreeTool
{
    internal const int DefaultMaxResults = 20;
    internal const int DefaultMaxResponseBytes = 8 * 1024;
    internal const int MaxResultsCap = 2_000;
    internal const int MaxResponseBytesCap = 64 * 1024;
    internal const int MaxDepthCap = 32;

    internal static Task<CallToolResult> ExecuteAsync(
        string targetPath,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var resolution = AnalysisTargetResolver.ResolveTargetPathOnly(new AnalysisTargetRequest(targetPath));
        if (resolution.Error is not null) return Task.FromResult(resolution.Error);
        if (resolution.Target!.Origin != AnalysisTargetOrigin.Source)
        {
            return Task.FromResult(AssemblyAnalysisDispatcher.UnsupportedAssemblyTarget(resolution.Target.CanonicalPath));
        }

        return ExecutePhysicalAsync(resolution.Target.AnalysisRoot, input, cancellationToken);
    }

    internal static Task<CallToolResult> ExecutePhysicalAsync(
        string analysisRoot,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var validation = GetFileTreeInputValidator.Validate(analysisRoot, input);
        if (validation is not null) return Task.FromResult(validation);

        try
        {
            var scan = GetFileTreeScanner.Scan(analysisRoot, input, cancellationToken);
            var text = GetFileTreeRenderer.Render(scan);
            if (input.MaxResponseBytes > 0 && Encoding.UTF8.GetByteCount(text) > input.MaxResponseBytes)
            {
                return Task.FromResult(ResponseBudgetTooSmall(analysisRoot, input, cancellationToken));
            }
            return Task.FromResult(McpToolResults.Text(text));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(McpToolResults.Recoverable(
                LinterErrorCodes.ResourceNotFound,
                $"Dateisystem konnte nicht vollstaendig gelesen werden: {ex.Message}",
                context: analysisRoot,
                hint: "Root, Berechtigungen und Ausschlussmuster pruefen."));
        }
    }

    private static CallToolResult ResponseBudgetTooSmall(
        string analysisRoot,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var minimumInput = input with { MaxResponseBytes = MaxResponseBytesCap };
        var minimumText = GetFileTreeRenderer.Render(
            GetFileTreeScanner.Scan(analysisRoot, minimumInput, cancellationToken));
        var minimumResponseBytes = Encoding.UTF8.GetByteCount(minimumText);
        return McpToolResults.Error(
            LinterErrorCodes.ResponseBudgetTooSmall,
            $"maxResponseBytes={input.MaxResponseBytes} ist zu klein für die vollständige Dateilandkarte im angeforderten Scope.",
            new McpErrorParameters(
                Hint: $"maxResponseBytes auf mindestens {minimumResponseBytes} setzen; der Retry liefert die vollständige wertvolle Dateilandkarte für diesen Scope.",
                FieldPath: "$.maxResponseBytes",
                RequestedBytes: input.MaxResponseBytes,
                MinimumResponseBytes: minimumResponseBytes));
    }
}
