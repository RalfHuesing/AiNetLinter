using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Erzeugt die System.CommandLine-Definition für die AiNetLinter-CLI.
/// </summary>
internal static class CliCommandBuilder
{
    // ainetlinter-disable MaxConstructorDependencies
    // Diese Records dienen als Behaelter fuer CLI-Optionen und haben keine logischen Abhaengigkeiten.
    internal sealed record Options(
        Option<string?> Config,
        Option<string> Path,
        Option<string?> Graph,
        Option<string?> Playbook,
        Option<string> Format,
        Option<bool> Verbose,
        Option<string?> CreateBaseline,
        Option<string?> Baseline,
        Option<bool> AddDisableAll,
        Option<bool> RemoveDisableAll,
        Option<bool> DebtReport,
        Option<bool> WaveReady,
        Option<bool> OnlyChanged,
        Option<string?> GitSince,
        Option<bool> Fix,
        Option<string?> Impact,
        Option<bool> SyncCursorRules,
        Option<bool> Check,
        Option<string?> Footprint);

    internal sealed record OutputOptions(
        string? GraphPath,
        string? PlaybookPath,
        string Format,
        bool Verbose);

    internal sealed record BaselineOptions(
        string? CreateBaselinePath,
        string? BaselinePath,
        bool OnlyChanged);

    internal sealed record MaintenanceOptions(
        bool AddDisableAll,
        bool RemoveDisableAll);

    internal sealed record ScopeOptions(
        bool WaveReady,
        string? GitSince);

    internal sealed record ImpactOptions(
        bool HasImpact,
        string? ImpactRef);

    // ainetlinter-disable MaxConstructorDependencies
    // Dieser Record ist ein CLI-Parsing-DTO und hat keine logischen Abhaengigkeiten.
    internal sealed record ParsedArgs(
        string? ConfigPath,
        string TargetPath,
        OutputOptions Output,
        BaselineOptions Baseline,
        MaintenanceOptions Maintenance,
        ScopeOptions Scope,
        bool DebtReport,
        bool Fix,
        ImpactOptions Impact,
        bool SyncCursorRules,
        bool Check,
        string? Footprint);

    internal static (RootCommand Root, Options Options) Build()
    {
        var options = CreateOptions();
        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            options.Config, options.Path, options.Graph, options.Playbook, options.Format, options.Verbose,
            options.CreateBaseline, options.Baseline, options.AddDisableAll, options.RemoveDisableAll,
            options.DebtReport, options.WaveReady, options.OnlyChanged, options.GitSince,
            options.Fix, options.Impact, options.SyncCursorRules, options.Check, options.Footprint,
        };

        return (root, options);
    }

    private static Options CreateOptions()
    {
        return new Options(
            CliOptionFactory.CreateConfigOption(),
            CliOptionFactory.CreatePathOption(),
            CliOptionFactory.CreateGraphOption(),
            CliOptionFactory.CreatePlaybookOption(),
            CliOptionFactory.CreateFormatOption(),
            CliOptionFactory.CreateVerboseOption(),
            CliOptionFactory.CreateBaselineCreateOption(),
            CliOptionFactory.CreateBaselineOption(),
            CliOptionFactory.CreateAddDisableAllOption(),
            CliOptionFactory.CreateRemoveDisableAllOption(),
            CliOptionFactory.CreateDebtReportOption(),
            CliOptionFactory.CreateWaveReadyOption(),
            CliOptionFactory.CreateOnlyChangedOption(),
            CliOptionFactory.CreateGitSinceOption(),
            CliOptionFactory.CreateFixOption(),
            CliOptionFactory.CreateImpactOption(),
            CliOptionFactory.CreateSyncCursorRulesOption(),
            CliOptionFactory.CreateCheckOption(),
            CliOptionFactory.CreateFootprintOption());
    }

    internal static ParsedArgs Parse(ParseResult parseResult, Options options)
    {
        return new ParsedArgs(
            ConfigPath: parseResult.GetValue(options.Config),
            TargetPath: parseResult.GetValue(options.Path) ?? "",
            Output: new OutputOptions(
                GraphPath: parseResult.GetValue(options.Graph),
                PlaybookPath: parseResult.GetValue(options.Playbook),
                Format: parseResult.GetValue(options.Format) ?? "text",
                Verbose: parseResult.GetValue(options.Verbose)),
            Baseline: new BaselineOptions(
                CreateBaselinePath: parseResult.GetValue(options.CreateBaseline),
                BaselinePath: parseResult.GetValue(options.Baseline),
                OnlyChanged: parseResult.GetValue(options.OnlyChanged)),
            Maintenance: new MaintenanceOptions(
                AddDisableAll: parseResult.GetValue(options.AddDisableAll),
                RemoveDisableAll: parseResult.GetValue(options.RemoveDisableAll)),
            Scope: new ScopeOptions(
                WaveReady: parseResult.GetValue(options.WaveReady),
                GitSince: parseResult.GetValue(options.GitSince)),
            DebtReport: parseResult.GetValue(options.DebtReport),
            Fix: parseResult.GetValue(options.Fix),
            Impact: new ImpactOptions(
                HasImpact: parseResult.GetResult(options.Impact) is not null,
                ImpactRef: parseResult.GetValue(options.Impact)),
            SyncCursorRules: parseResult.GetValue(options.SyncCursorRules),
            Check: parseResult.GetValue(options.Check),
            Footprint: parseResult.GetValue(options.Footprint));
    }
}
