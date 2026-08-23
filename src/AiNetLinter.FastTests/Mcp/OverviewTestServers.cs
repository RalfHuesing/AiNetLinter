#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Output;

namespace AiNetLinter.FastTests.Mcp;

internal static class OverviewTestServers
{
    internal static McpCodeGraphServer PendingLoadServer() =>
        new(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            UsedDefaultConfig = false,
            LoadFunc = token =>
            {
                var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => pending.TrySetCanceled(token));
                return pending.Task;
            },
        });

    internal static McpCodeGraphServer FaultingLoadServer(ILintConsole console) =>
        new(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = console,
            Config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            UsedDefaultConfig = false,
            LoadFunc = _ => Task.FromException<SourceFileCatalog?>(new InvalidOperationException("Simulierter Kalt-Load-Fehler")),
        });
}
