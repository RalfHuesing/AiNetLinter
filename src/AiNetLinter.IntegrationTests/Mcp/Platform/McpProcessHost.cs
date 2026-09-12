#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Platform;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.IntegrationTests.Mcp.Platform;

internal sealed record McpProcessTarget(
    string TargetPath,
    IDisposable? Owner = null)
{
    internal string WorkingDirectory => Path.GetDirectoryName(Path.GetFullPath(TargetPath))!;
}

internal sealed class McpProcessHost : IAsyncDisposable
{
    private const string LoadingMessagePrefix = "[INFO]: Server laedt die Solution noch.";
    // Große Dogfood-Workspaces können nach dem MCP-Handshake noch deutlich
    // länger als 15 Sekunden laden; der Loading-Vertrag bleibt dabei gültig.
    private const int LoadingRetryCount = 120;
    // Assembly-Ziele materialisieren WholeProjectDecompiler-Inhalte synchron; der Client muss
    // dem konfigurierten Assembly-Decompilation-Timeout von 180 Sekunden entsprechen.
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(180);
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
        McpFixtureTargetSetup.Ensure(workspace.SolutionPath);
        return StartAsync(new McpProcessTarget(workspace.SolutionPath, workspace), timeout, cancellationToken);
    }

    public static async Task<McpProcessHost> StartAsync(
        McpProcessTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        McpFixtureTargetSetup.Ensure(target.TargetPath);

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
                        WorkingDirectory = target.WorkingDirectory,
                        EnvironmentVariables = new Dictionary<string, string?>
                        {
                            ["AINETLINTER_NO_DAEMON"] = "1",
                        },
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
        var effectiveArguments = BuildEffectiveArguments(toolName, arguments);

        for (var attempt = 0; attempt < LoadingRetryCount; attempt++)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout ?? DefaultCallTimeout);
            var result = await client.CallToolAsync(toolName, effectiveArguments, cancellationToken: timeoutSource.Token).ConfigureAwait(false);
            if (!IsLoadingResponse(result)) return result;
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        using var finalTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        finalTimeout.CancelAfter(timeout ?? DefaultCallTimeout);
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

    internal string TargetPath => target.TargetPath;

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

    private Dictionary<string, object?> BuildEffectiveArguments(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        if (toolName is "get_server_health")
        {
            return arguments is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(arguments);
        }

        var effective = arguments is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(arguments);
        effective.TryAdd("targetPath", TargetPath);
        return effective;
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

internal static class McpFixtureTargetSetup
{
    internal static void Ensure(string targetPath)
    {
        if (!Path.IsPathFullyQualified(targetPath))
        {
            throw new ArgumentException("MCP-Fixture-Ziele muessen absolute Pfade sein.", nameof(targetPath));
        }

        var solutionPath = Path.GetFullPath(targetPath);
        var extension = Path.GetExtension(solutionPath);
        if (!File.Exists(solutionPath) ||
            (extension is not ".slnx" and not ".sln"))
        {
            throw new FileNotFoundException($"MCP-Fixture-Solution nicht gefunden: {solutionPath}", solutionPath);
        }

        var adjacentRulesPath = Path.Combine(Path.GetDirectoryName(solutionPath)!, "ainetlinter-rules.json");
        if (!File.Exists(adjacentRulesPath))
        {
            var sourceRulesPath = Path.Combine(SolutionRootLocator.Find(), "ainetlinter-rules.json");
            File.Copy(sourceRulesPath, adjacentRulesPath);
        }

        RestoreProjectsIfNeeded(solutionPath);
    }

    private static void RestoreProjectsIfNeeded(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        var projectsNeedRestore = Directory.EnumerateFiles(
                solutionDirectory,
                "*.csproj",
                SearchOption.AllDirectories)
            .Where(projectPath => !projectPath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Any(projectPath =>
            {
                var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json");
                return !File.Exists(assetsPath) || File.GetLastWriteTimeUtc(projectPath) > File.GetLastWriteTimeUtc(assetsPath);
            });

        if (!projectsNeedRestore) return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = solutionDirectory,
            Arguments = $"restore \"{solutionPath}\" --nologo --ignore-failed-sources",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("dotnet restore konnte für die isolierte MCP-Fixture nicht gestartet werden.");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"dotnet restore der isolierten MCP-Fixture fehlgeschlagen (ExitCode {process.ExitCode}): {error}");
        }
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
