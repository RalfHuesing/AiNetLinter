#nullable enable

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;

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

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var (root, options) = CliCommandBuilder.Build();

        root.SetAction(async parseResult =>
        {
            try
            {
                var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(parseResult, options));
                if (linterArgs.Docs == null)
                {
                    Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
                return await ExecuteLinterAsync(linterArgs, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("[INFO]: Abgebrochen.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 2;
            }
        });

        return await root.Parse(args).InvokeAsync();
    }

    private static LinterArgs ToLinterArgs(CliParsedArgs parsed)
    {
        return new LinterArgs
        {
            ConfigPath = parsed.ConfigPath,
            TargetPath = parsed.TargetPath,
            Verbose = parsed.Output.Verbose,
            GraphPath = parsed.Output.GraphPath,
            PlaybookPath = parsed.Output.PlaybookPath,
            CreateBaselinePath = parsed.Baseline.CreateBaselinePath,
            BaselinePath = parsed.Baseline.BaselinePath,
            OnlyChanged = parsed.Baseline.OnlyChanged,
            AddDisableAll = parsed.Maintenance.AddDisableAll,
            RemoveDisableAll = parsed.Maintenance.RemoveDisableAll,
            WaveReady = parsed.Scope.WaveReady,
            GitSince = parsed.Scope.GitSince,
            DebtReport = parsed.DebtReport,
            Fix = parsed.Fix,
            HasImpact = parsed.Impact.HasImpact,
            ImpactRef = parsed.Impact.ImpactRef,
            SyncCursorRules = parsed.SyncCursorRules,
            Check = parsed.Check,
            NoCache = parsed.NoCache,
            CacheTtlMinutes = parsed.CacheTtlMinutes,
            Footprint = parsed.Footprint,
            Docs = parsed.Docs,
            ListRules = parsed.ListRules,
            DescribeRule = parsed.DescribeRule,
            SearchRules = parsed.SearchRules,
        };
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args, CancellationToken ct)
    {
        var standaloneResult = TryRunStandaloneCommand(args);
        if (standaloneResult.HasValue) return standaloneResult.Value;

        var validationError = ValidateArgs(args);
        if (validationError.HasValue) return validationError.Value;

        if (args.Check && args.PlaybookPath != null) return await PlaybookCheckCommand.RunAsync(args, ct);

        // Schneller Pfad: --sync-cursor-rules ohne --playbook.
        // Wenn --playbook ebenfalls gesetzt ist, fällt der Aufruf durch zu AuditCommand,
        // das beide Ausgaben via GenerateOptionalOutputsAsync erzeugt.
        if (args.SyncCursorRules && args.PlaybookPath == null) return SyncCursorRulesCommand.Run(args);

        if (args.Footprint != null) return await FootprintCommand.RunAsync(args, ct);

        var maintenanceResult = await MaintenanceCommand.TryRunAsync(args, ct);
        if (maintenanceResult.HasValue) return maintenanceResult.Value;

        if (args.DebtReport) return await DebtReportCommand.RunAsync(args, ct);

        if (args.HasImpact) return await ImpactCommand.RunAsync(args, ct);

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
