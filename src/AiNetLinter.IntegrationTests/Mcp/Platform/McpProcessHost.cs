#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal sealed record McpProcessTarget(string RootPath, IDisposable? Owner = null);

internal sealed class McpProcessHost : IAsyncDisposable
{
    private const string LoadingMessagePrefix = "[INFO]: Server laedt die Solution noch.";
    private const int LoadingRetryCount = 30;
    private static readonly McpConnectRetryOptions DefaultConnectRetryOptions = new();
    private readonly McpClient client;
    private readonly McpProcessTarget target;
    private readonly IDisposable lease;

    private McpProcessHost(McpClient client, McpProcessTarget target, IDisposable lease)
    {
        this.client = client;
        this.target = target;
        this.lease = lease;
    }

    public static Task<McpProcessHost> StartAsync(
        FixtureWorkspace workspace,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        McpFixtureProjectDefinition.Ensure(workspace.RootPath);
        return StartAsync(new McpProcessTarget(workspace.RootPath, workspace), timeout, cancellationToken);
    }

    public static async Task<McpProcessHost> StartAsync(
        McpProcessTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
            if (!File.Exists(exePath)) throw new FileNotFoundException($"AiNetLinter.exe nicht gefunden: {exePath}");

            var client = await ConnectWithRetryAsync(
                async token =>
                {
                    var transport = new StdioClientTransport(new StdioClientTransportOptions
                    {
                        Name = "ainetlinter-integration-mcp-host",
                        Command = exePath,
                        Arguments = ["--mcp-server"],
                        WorkingDirectory = target.RootPath,
                    });
                    using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutSource.CancelAfter(timeout);
                    return await McpClient.CreateAsync(transport, cancellationToken: timeoutSource.Token).ConfigureAwait(false);
                },
                DefaultConnectRetryOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new McpProcessHost(client, target, lease);
        }
        catch
        {
            target.Owner?.Dispose();
            lease.Dispose();
            throw;
        }
    }

    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveArguments = arguments is null
            ? new Dictionary<string, object?> { ["projectRoot"] = target.RootPath }
            : arguments.ContainsKey("projectRoot")
                ? arguments
                : new Dictionary<string, object?>(arguments)
                {
                    ["projectRoot"] = target.RootPath,
                };

        for (var attempt = 0; attempt < LoadingRetryCount; attempt++)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
            var result = await client.CallToolAsync(toolName, effectiveArguments, cancellationToken: timeoutSource.Token).ConfigureAwait(false);
            if (!IsLoadingResponse(result)) return result;
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        using var finalTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        finalTimeout.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
        return await client.CallToolAsync(toolName, effectiveArguments, cancellationToken: finalTimeout.Token).ConfigureAwait(false);
    }

    public async Task<string> CallToolGetTextAsync(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        var result = await CallToolAsync(toolName, arguments).ConfigureAwait(false);
        if (result.IsError == true) throw new InvalidOperationException($"MCP-Tool '{toolName}' lieferte einen Fehler.");
        return result.Content is { Count: > 0 } && result.Content[0] is TextContentBlock text ? text.Text ?? string.Empty : string.Empty;
    }

    public ValueTask<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        client.ListToolsAsync(cancellationToken: cancellationToken);

    public ValueTask<IList<McpClientResource>> ListResourcesAsync(CancellationToken cancellationToken = default) =>
        client.ListResourcesAsync(cancellationToken: cancellationToken);

    public ValueTask<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        client.ListResourceTemplatesAsync(cancellationToken: cancellationToken);

    public ValueTask<ReadResourceResult> ReadResourceAsync(
        string uri,
        CancellationToken cancellationToken = default) =>
        client.ReadResourceAsync(uri, cancellationToken: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try { await client.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            target.Owner?.Dispose();
            lease.Dispose();
        }
    }

    private static bool IsLoadingResponse(CallToolResult result) =>
        result.IsError != true && result.Content is { Count: > 0 } &&
        result.Content[0] is TextContentBlock text &&
        text.Text?.StartsWith(LoadingMessagePrefix, StringComparison.Ordinal) == true;

    internal static async Task<T> ConnectWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> connectAttempt,
        McpConnectRetryOptions retryOptions,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectAttempt);
        ArgumentNullException.ThrowIfNull(retryOptions);
        retryOptions.Validate();

        var attempts = 0;
        Exception? lastException = null;
        var delay = delayAsync ?? ((duration, token) => Task.Delay(duration, token));

        while (attempts <= retryOptions.MaxRetries)
        {
            try
            {
                return await connectAttempt(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
                attempts++;
                if (attempts > retryOptions.MaxRetries) break;

                var delayMilliseconds = retryOptions.BaseDelayMs * Math.Pow(retryOptions.BackoffFactor, attempts - 1);
                await delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"MCP-Client-Connect scheiterte nach {retryOptions.MaxRetries + 1} Versuchen.",
            lastException);
    }
}

internal static class McpFixtureProjectDefinition
{
    internal static void Ensure(string rootPath)
    {
        var definitionPath = Path.Combine(rootPath, "ainetlinter.project.json");
        if (File.Exists(definitionPath)) return;

        var solutionPath = Directory.EnumerateFiles(rootPath, "*.slnx", SearchOption.TopDirectoryOnly).Single();
        var rulesPath = Path.Combine(rootPath, "rules.json");
        if (!File.Exists(rulesPath))
        {
            var repositoryRulesPath = Path.Combine(SolutionRootLocator.Find(), "rules.json");
            File.Copy(repositoryRulesPath, rulesPath);
        }

        var definition = new
        {
            solution = Path.GetRelativePath(rootPath, solutionPath),
            rules = "rules.json",
        };
        File.WriteAllText(definitionPath, JsonSerializer.Serialize(definition));
    }
}

internal sealed record McpConnectRetryOptions(int MaxRetries = 3, int BaseDelayMs = 500, double BackoffFactor = 2.0)
{
    public void Validate()
    {
        if (MaxRetries < 0) throw new ArgumentOutOfRangeException(nameof(MaxRetries));
        if (BaseDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(BaseDelayMs));
        if (BackoffFactor <= 0) throw new ArgumentOutOfRangeException(nameof(BackoffFactor));
    }
}
