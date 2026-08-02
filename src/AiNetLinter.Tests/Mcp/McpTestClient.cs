#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Tests.Mcp;

public sealed class McpTestClient : IAsyncDisposable
{
    private readonly McpClient _client;

    private McpTestClient(McpClient client)
    {
        _client = client;
    }

    public static async Task<McpTestClient> ConnectAsync(
        string targetDirectory,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default,
        McpTestClientRetryOptions? retryOptions = null)
    {
        retryOptions ??= new McpTestClientRetryOptions();
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryOptions.MaxRetries)
        {
            try
            {
                var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException($"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}");
                }

                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "ainetlinter-mcp-test-client",
                    Command = exePath,
                    Arguments = ["--mcp-server", "--path", targetDirectory],
                });

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
                return new McpTestClient(client);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                attempt++;
                if (attempt > retryOptions.MaxRetries) break;

                var delayMs = retryOptions.BaseDelayMs * Math.Pow(retryOptions.BackoffFactor, attempt - 1);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"MCP-Client-Connect scheiterte nach {retryOptions.MaxRetries + 1} Versuchen gegen '{targetDirectory}'.",
            lastException);
    }

    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        return await _client.CallToolAsync(toolName, arguments, cancellationToken: cts.Token);
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        return await _client.ListToolsAsync(cancellationToken: cts.Token);
    }

    public async Task<string> CallToolGetTextAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await CallToolAsync(toolName, arguments, timeoutSeconds, cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"MCP Tool '{toolName}' lieferte einen Fehler-Result zurück.");
        }

        if (result.Content == null || result.Content.Count == 0)
        {
            return string.Empty;
        }

        if (result.Content[0] is TextContentBlock textBlock)
        {
            return textBlock.Text ?? string.Empty;
        }

        return string.Empty;
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}
