#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AiNetLinter.IntegrationTests.Mcp;

internal sealed record McpProcessRunResult(int ExitCode, string Output, string Error, bool TimedOut);

internal static class McpProcessRunner
{
    public static async Task<McpProcessRunResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exit = process.WaitForExitAsync();
        if (await Task.WhenAny(exit, Task.Delay(timeout)).ConfigureAwait(false) != exit)
        {
            process.Kill(entireProcessTree: true);
            await exit.ConfigureAwait(false);
            return new McpProcessRunResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false), true);
        }
        return new McpProcessRunResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false), false);
    }
}
