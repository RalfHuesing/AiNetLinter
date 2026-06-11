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
        Option<bool> AddDisableAll);

    internal sealed record ParsedArgs(
        string? ConfigPath,
        string TargetPath,
        string? GraphPath,
        string Format,
        bool Verbose,
        string? CreateBaselinePath,
        string? BaselinePath,
        bool AddDisableAll);

    internal static (RootCommand Root, Options Options) Build()
    {
        var options = CreateOptions();
        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            options.Config, options.Path, options.Graph, options.Format, options.Verbose,
            options.CreateBaseline, options.Baseline, options.AddDisableAll,
        };

        return (root, options);
    }

    private static Options CreateOptions()
    {
        var configOpt = new Option<string?>("--config", "-c")
        {
            Description = "Pfad zur JSON-Konfigurationsdatei (rules.json)",
        };
        var pathOpt = new Option<string>("--path", "-p")
        {
            Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis",
            Required = true,
        };
        var graphOpt = new Option<string?>("--graph", "-g")
        {
            Description = "Pfad für das zu generierende Mermaid-Abhängigkeitsdiagramm (.md)",
        };
        var formatOpt = new Option<string>("--format", "-f")
        {
            Description = "Ausgabeformat: text (Standard) oder sarif",
            DefaultValueFactory = _ => "text",
        };
        var verboseOpt = new Option<bool>("--verbose", "-v")
        {
            Description = "Detaillierte Protokollausgabe aktivieren",
        };
        var createBaselineOpt = new Option<string?>("--create-baseline")
        {
            Description = "Erzeugt eine Baseline-JSON mit Datei-Checksummen am angegebenen Pfad",
        };
        var baselineOpt = new Option<string?>("--baseline")
        {
            Description = "Pfad zur Baseline-JSON für inkrementelle Migration",
        };
        var addDisableAllOpt = new Option<bool>("--add-disable-all")
        {
            Description = "Fügt '// ainetlinter-disable all' oben in alle .cs-Dateien unter --path ein",
        };

        return new Options(
            configOpt, pathOpt, graphOpt, formatOpt, verboseOpt, createBaselineOpt, baselineOpt, addDisableAllOpt);
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
            parseResult.GetValue(options.AddDisableAll));
    }
}
