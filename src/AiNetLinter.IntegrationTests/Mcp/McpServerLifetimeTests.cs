#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

[Trait("Category", "Integration")]
public sealed class McpServerLifetimeTests
{
    [Fact]
    public async Task ExplicitParentExit_StopsMcpServerWithinFiveSeconds()
    {
        using var lease = await SubprocessLifetimeBudget.Shared.AcquireAsync(CancellationToken.None);
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        McpFixtureProjectDefinition.Ensure(fixture.RootPath);
        using var parentProcess = StartLongRunningParentProcess();
        var serverProcess = StartMcpServer(fixture.RootPath, parentProcess.Id);
        var stderrTask = serverProcess.StandardError.ReadToEndAsync();
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            var startedAt = DateTime.UtcNow;
            parentProcess.Kill(entireProcessTree: true);

            var exitTask = serverProcess.WaitForExitAsync();
            var completedTask = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(exitTask, completedTask);
            Assert.True(DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(5));
            Assert.Equal(0, serverProcess.ExitCode);
            await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            if (!serverProcess.HasExited)
            {
                serverProcess.Kill(entireProcessTree: true);
                await serverProcess.WaitForExitAsync();
            }

            serverProcess.Dispose();
        }
    }

    private static Process StartLongRunningParentProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("ping 127.0.0.1 -n 61 > nul");
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Test-Parentprozess konnte nicht gestartet werden.");
    }

    private static Process StartMcpServer(string solutionPath, int parentProcessId)
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"AiNetLinter.exe nicht gefunden: {executablePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = solutionPath,
        };
        startInfo.ArgumentList.Add("--mcp-server");
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(parentProcessId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--mcp-log");
        startInfo.ArgumentList.Add("off");

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("MCP-Serverprozess konnte nicht gestartet werden.");
    }
}
