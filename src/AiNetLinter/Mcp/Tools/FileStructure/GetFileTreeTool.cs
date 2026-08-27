#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Read-only MCP-Tool fuer eine physische, projektgebundene Dateilandkarte. Der Tool-Dispatch
/// validiert nur den Vertrag; Walk, Filter und Rendering liegen in den dafuer getrennten Klassen.
/// </summary>
internal static class GetFileTreeTool
{
    internal const int DefaultMaxResults = 200;
    internal const int MaxResultsCap = 2_000;
    internal const int MaxDepthCap = 32;

    internal static Task<CallToolResult> ExecuteAsync(
        string projectRoot,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var validation = GetFileTreeInputValidator.Validate(projectRoot, input);
        if (validation is not null) return Task.FromResult(validation);

        try
        {
            var scan = GetFileTreeScanner.Scan(projectRoot, input, cancellationToken);
            var text = GetFileTreeRenderer.Render(scan);
            return Task.FromResult(McpToolResults.Text(text, new { fileTree = scan.Payload }));
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
                context: projectRoot,
                hint: "Root, Berechtigungen und Ausschlussmuster pruefen."));
        }
    }
}
