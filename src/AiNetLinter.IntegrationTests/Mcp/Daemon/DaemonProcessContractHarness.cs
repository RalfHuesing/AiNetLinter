#nullable enable

using System.Diagnostics;
using System.Globalization;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.IntegrationTests.Mcp.Platform;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

internal sealed record DaemonProcessSpec(
    string WorkingDirectory,
    string LocalAppData,
    decimal IdleExitMinutes,
    int MaxProjects = 1)
{
    internal EffectiveDaemonConfiguration Configuration => new(
        MaxProjects,
        IdleExitMinutes,
        DaemonProtocol.DefaultLogTarget);
}

internal static class DaemonProcessContractHarness
{
    private static readonly SemaphoreSlim EndpointGate = new(1, 1);
    private static readonly McpConnectRetryOptions ReadinessRetry = new()
    {
        MaxRetries = 20,
        BaseDelayMs = 50,
        BackoffFactor = 1.2,
    };

    internal static async Task<IDisposable> AcquireEndpointAsync(CancellationToken cancellationToken)
    {
        await EndpointGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new EndpointLease(EndpointGate);
    }

    internal static async Task<DaemonProcessHandle> StartAsync(
        DaemonProcessSpec spec,
        CancellationToken cancellationToken)
    {
        var lifetime = await SubprocessLifetimeBudget.Shared
            .AcquireAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var process = new Process { StartInfo = CreateStartInfo(spec) };
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Der Daemon-Prozess konnte nicht gestartet werden.");
            }

            return new DaemonProcessHandle(process, lifetime);
        }
        catch
        {
            lifetime.Dispose();
            throw;
        }
    }

    internal static Task<McpProcessRunResult> RunToExitAsync(
        DaemonProcessSpec spec,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        RunToExitAsyncCore(spec, timeout, cancellationToken);

    internal static Task<DaemonPipeConnection> ConnectWhenReadyAsync(
        CancellationToken cancellationToken) =>
        McpProcessHost.ConnectWithRetryAsync(
            token => ConnectAsync(token),
            ReadinessRetry,
            cancellationToken: cancellationToken);

    internal static async Task<DaemonWelcome> PerformHandshakeAsync(
        DaemonPipeConnection connection,
        DaemonProcessSpec spec,
        CancellationToken cancellationToken)
    {
        var executableVersion = typeof(DaemonHost).Assembly.GetName().Version?.ToString() ?? "unknown";
        await connection.WriteJsonFrameAsync(
            new DaemonHello(executableVersion, Environment.ProcessId, spec.Configuration),
            cancellationToken).ConfigureAwait(false);
        var welcome = await connection
            .ReadJsonFrameAsync<DaemonWelcome>(cancellationToken)
            .ConfigureAwait(false);
        return welcome ?? throw new InvalidOperationException("Der Daemon lieferte kein Welcome-Frame.");
    }

    private static async Task<McpProcessRunResult> RunToExitAsyncCore(
        DaemonProcessSpec spec,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await McpProcessRunner
            .RunAsync(CreateStartInfo(spec), timeout)
            .ConfigureAwait(false);
    }

    private static async Task<DaemonPipeConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var transport = new DaemonPipeTransport();
        return await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(DaemonProcessSpec spec)
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"AiNetLinter.exe nicht gefunden: {executablePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = spec.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--daemon-start");
        startInfo.ArgumentList.Add("--mcp-daemon-idle-exit-minutes");
        startInfo.ArgumentList.Add(spec.IdleExitMinutes.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--mcp-max-projects");
        startInfo.ArgumentList.Add(spec.MaxProjects.ToString(CultureInfo.InvariantCulture));
        startInfo.Environment["LOCALAPPDATA"] = spec.LocalAppData;
        return startInfo;
    }
}

internal sealed class EndpointLease(SemaphoreSlim gate) : IDisposable
{
    private int disposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) gate.Release();
    }
}

internal sealed class DaemonProcessHandle : IAsyncDisposable
{
    private readonly Process process;
    private readonly IDisposable lifetime;
    private readonly Task<string> output;
    private readonly Task<string> error;
    private int disposed;

    internal DaemonProcessHandle(Process process, IDisposable lifetime)
    {
        this.process = process;
        this.lifetime = lifetime;
        output = process.StandardOutput.ReadToEndAsync();
        error = process.StandardError.ReadToEndAsync();
    }

    internal async Task<McpProcessRunResult> WaitForExitAsync(TimeSpan timeout)
    {
        var exit = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exit, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != exit)
        {
            Terminate();
            await exit.ConfigureAwait(false);
            return await CreateResultAsync(timedOut: true).ConfigureAwait(false);
        }

        return await CreateResultAsync(timedOut: false).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        try
        {
            if (!process.HasExited)
            {
                Terminate();
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
        }
        finally
        {
            process.Dispose();
            lifetime.Dispose();
        }
    }

    private async Task<McpProcessRunResult> CreateResultAsync(bool timedOut)
    {
        await Task.WhenAll(output, error).ConfigureAwait(false);
        var outputText = await output.ConfigureAwait(false);
        var errorText = await error.ConfigureAwait(false);
        return new McpProcessRunResult(process.ExitCode, outputText, errorText, timedOut);
    }

    private void Terminate()
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
    }
}
