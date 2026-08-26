#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Logging;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Output;

namespace AiNetLinter;

/// <summary>
/// Der CLI-Einstiegspunkt für den Linter.
/// </summary>
public static class Program
{
    /// <summary>
    /// Der Einstiegspunkt für die Ausführung der Linter-CLI.
    /// </summary>
    /// <param name="args">Die Befehlszeilenargumente.</param>
    /// <returns>Der Exit-Code des Programms (0 = Erfolg, 1 = Linter-Verstoesse, 2 = Fataler Fehler).</returns>
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var processRole = DetermineProcessRole(args);
        var exitCode = 2;
        try
        {
            exitCode = await RunMainAsync(args, processRole).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Serilog.Log.Error("Prozess abgebrochen (OperationCanceled), ExitCode=2");
            Console.Error.WriteLine("[INFO]: Abgebrochen.");
            exitCode = 2;
        }
        catch (Exception ex)
        {
            Serilog.Log.Fatal(ex, "Prozess mit unerwartetem Fehler beendet, ExitCode=2");
            Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex}");
            exitCode = 2;
        }
        finally
        {
            if (SystemLog.IsInitialized)
            {
                Serilog.Log.Information("Prozess beendet: ExitCode={ExitCode}, Rolle={ProcessRole}", exitCode, processRole);
                SystemLog.CloseAndFlush();
            }
        }

        return exitCode;
    }

    private static async Task<int> RunMainAsync(string[] args, string processRole)
    {
        SystemLog.Initialize(processRole);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var (root, options) = CliCommandBuilder.Build();
        root.SetAction(parseResult => RunParsedCommandAsync(parseResult, options, cancellation.Token));
        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count != 0)
        {
            Serilog.Log.Error("CLI-Parsefehler: {ErrorCount} Fehler erkannt", parseResult.Errors.Count);
        }

        return await parseResult.InvokeAsync().ConfigureAwait(false);
    }

    private static async Task<int> RunParsedCommandAsync(
        System.CommandLine.ParseResult parseResult,
        CliOptions options,
        CancellationToken cancellationToken)
    {
        var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(parseResult, options));
        if (linterArgs.McpServer)
        {
            return await ThinClientProxy.RunAsync(linterArgs, cancellationToken, McpLintConsole.Instance).ConfigureAwait(false);
        }

        if (linterArgs.DaemonStart)
        {
            return await DaemonHostCommand.RunAsync(linterArgs, cancellationToken, McpLintConsole.Instance).ConfigureAwait(false);
        }

        if (linterArgs.Docs == null)
        {
            Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        return await ExecuteLinterAsync(linterArgs, cancellationToken).ConfigureAwait(false);
    }

    private static string DetermineProcessRole(string[] args) =>
        args.Any(argument => string.Equals(argument, CliOptionFactory.McpServer, StringComparison.OrdinalIgnoreCase))
            ? ProcessRoles.ThinClient
            : args.Any(argument => string.Equals(argument, CliOptionFactory.DaemonStart, StringComparison.OrdinalIgnoreCase))
                ? ProcessRoles.Daemon
                : ProcessRoles.Cli;

    private static LinterArgs ToLinterArgs(CliParsedArgs parsed)
    {
        return new LinterArgs
        {
            ConfigPath = parsed.ConfigPath,
            TargetPath = parsed.TargetPath,
            Verbose = parsed.Output.Verbose,
            CreateBaselinePath = parsed.Baseline.CreateBaselinePath,
            BaselinePath = parsed.Baseline.BaselinePath,
            OnlyChanged = parsed.Baseline.OnlyChanged,
            AddDisableAll = parsed.Maintenance.AddDisableAll,
            RemoveDisableAll = parsed.Maintenance.RemoveDisableAll,
            WaveReady = parsed.Scope.WaveReady,
            Fix = parsed.Fix,
            SyncAgentRules = parsed.SyncAgentRules,
            SyncAgentRulesOnly = parsed.SyncAgentRulesOnly,
            AgentRulesPath = parsed.AgentRulesPath,
            NoCache = parsed.NoCache,
            CacheTtlMinutes = parsed.CacheTtlMinutes,
            Docs = parsed.Docs,
            ListRules = parsed.ListRules,
            DescribeRule = parsed.DescribeRule,
            SearchRules = parsed.SearchRules,
            McpServer = parsed.McpServer,
            DaemonStart = parsed.DaemonStart,
            ParentPid = parsed.ParentPid,
            McpProjectTtlMinutes = parsed.McpProjectTtlMinutes,
            McpMaxProjects = parsed.McpMaxProjects,
            McpDaemonIdleExitMinutes = parsed.McpDaemonIdleExitMinutes,
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args, CancellationToken ct)
    {
        var standaloneResult = TryRunStandaloneCommand(args);
        if (standaloneResult.HasValue) return standaloneResult.Value;

        var validationError = ValidateArgs(args);
        if (validationError.HasValue) return validationError.Value;

        // Schneller Pfad: --sync-agent-rules-only.
        if (args.SyncAgentRulesOnly) return SyncAgentRulesCommand.Run(args);

        var maintenanceResult = await MaintenanceCommand.TryRunAsync(args, ct);
        if (maintenanceResult.HasValue) return maintenanceResult.Value;

        return await AuditCommand.RunAsync(args, ct);
    }

    private static int? TryRunStandaloneCommand(LinterArgs args)
    {
        if (args.Docs != null) return DocsCommand.Run(args.Docs);
        if (args.ListRules) return ListRulesCommand.ListAll();
        if (args.DescribeRule != null) return ListRulesCommand.DescribeOne(args.DescribeRule);
        if (args.SearchRules != null) return ListRulesCommand.Search(args.SearchRules);
        return null;
    }

    private static int? ValidateArgs(LinterArgs args)
    {
        var error = args.Validate();
        if (error != null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        return null;
    }
}
