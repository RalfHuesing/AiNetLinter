using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Erzeugt die System.CommandLine-Definition für die AiNetLinter-CLI.
/// </summary>
internal static class CliCommandBuilder
{
    internal sealed record Options(
        Option<string?> Config,
        Option<string> Path,
        Option<string?> Graph,
        Option<string> Format,
        Option<bool> Verbose,
        Option<string?> CreateBaseline,
        Option<string?> Baseline,
        Option<bool> AddDisableAll,
        Option<bool> RemoveDisableAll);

    internal sealed record ParsedArgs(
        string? ConfigPath,
        string TargetPath,
        string? GraphPath,
        string Format,
        bool Verbose,
        string? CreateBaselinePath,
        string? BaselinePath,
        bool AddDisableAll,
        bool RemoveDisableAll);

    internal static (RootCommand Root, Options Options) Build()
    {
        var options = CreateOptions();
        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            options.Config, options.Path, options.Graph, options.Format, options.Verbose,
            options.CreateBaseline, options.Baseline, options.AddDisableAll, options.RemoveDisableAll,
        };

        return (root, options);
    }

    private static Options CreateOptions()
    {
        return new Options(
            CliOptionFactory.CreateConfigOption(),
            CliOptionFactory.CreatePathOption(),
            CliOptionFactory.CreateGraphOption(),
            CliOptionFactory.CreateFormatOption(),
            CliOptionFactory.CreateVerboseOption(),
            CliOptionFactory.CreateBaselineCreateOption(),
            CliOptionFactory.CreateBaselineOption(),
            CliOptionFactory.CreateAddDisableAllOption(),
            CliOptionFactory.CreateRemoveDisableAllOption());
    }

    internal static ParsedArgs Parse(ParseResult parseResult, Options options)
    {
        return new ParsedArgs(
            parseResult.GetValue(options.Config),
            parseResult.GetValue(options.Path) ?? "",
            parseResult.GetValue(options.Graph),
            parseResult.GetValue(options.Format) ?? "text",
            parseResult.GetValue(options.Verbose),
            parseResult.GetValue(options.CreateBaseline),
            parseResult.GetValue(options.Baseline),
            parseResult.GetValue(options.AddDisableAll),
            parseResult.GetValue(options.RemoveDisableAll));
    }
}
