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

    // ainetlinter-disable MaxConstructorDependencies
    // Diese Records dienen als Behaelter fuer CLI-Argumente und haben keine logischen Abhaengigkeiten.
    internal sealed record ParsedArgs(
        string? ConfigPath,
        string TargetPath,
        string? GraphPath,
        string? PlaybookPath,
        string Format,
        bool Verbose,
        string? CreateBaselinePath,
        string? BaselinePath,
        bool AddDisableAll,
        bool RemoveDisableAll,
        bool DebtReport,
        bool WaveReady,
        bool OnlyChanged,
        string? GitSince,
        bool Fix,
        bool HasImpact,
        string? ImpactRef,
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
            parseResult.GetValue(options.Config),
            parseResult.GetValue(options.Path) ?? "",
            parseResult.GetValue(options.Graph),
            parseResult.GetValue(options.Playbook),
            parseResult.GetValue(options.Format) ?? "text",
            parseResult.GetValue(options.Verbose),
            parseResult.GetValue(options.CreateBaseline),
            parseResult.GetValue(options.Baseline),
            parseResult.GetValue(options.AddDisableAll),
            parseResult.GetValue(options.RemoveDisableAll),
            parseResult.GetValue(options.DebtReport),
            parseResult.GetValue(options.WaveReady),
            parseResult.GetValue(options.OnlyChanged),
            parseResult.GetValue(options.GitSince),
            parseResult.GetValue(options.Fix),
            parseResult.Tokens.Any(t => t.Value == "--impact" || t.Value == "-im"),
            parseResult.GetValue(options.Impact),
            parseResult.GetValue(options.SyncCursorRules),
            parseResult.GetValue(options.Check),
            parseResult.GetValue(options.Footprint));
    }
}
