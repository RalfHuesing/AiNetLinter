using System.CommandLine;

namespace AiNetLinter.Cli;

/// <summary>
/// Erzeugt einzelne CLI-Optionen für den AiNetLinter-Einstiegspunkt.
/// </summary>
internal static class CliOptionFactory
{
    internal static Option<string?> CreateConfigOption() => new("--config", "-c")
    {
        Description = "Pfad zur JSON-Konfigurationsdatei (rules.json)",
    };

    internal static Option<string> CreatePathOption() => new("--path", "-p")
    {
        Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis",
        Required = true,
    };

    internal static Option<string?> CreateGraphOption() => new("--graph", "-g")
    {
        Description = "Pfad fuer das zu generierende Mermaid-Abhaengigkeitsdiagramm (.md)",
    };

    internal static Option<string> CreateFormatOption() => new("--format", "-f")
    {
        Description = "Ausgabeformat: text (Standard) oder sarif",
        DefaultValueFactory = _ => "text",
    };

    internal static Option<bool> CreateVerboseOption() => new("--verbose", "-v")
    {
        Description = "Detaillierte Protokollausgabe aktivieren",
    };

    internal static Option<string?> CreateBaselineCreateOption() => new("--create-baseline")
    {
        Description = "Erzeugt eine Baseline-JSON mit Datei-Checksummen am angegebenen Pfad",
    };

    internal static Option<string?> CreateBaselineOption() => new("--baseline")
    {
        Description = "Pfad zur Baseline-JSON fuer inkrementelle Migration",
    };

    internal static Option<bool> CreateAddDisableAllOption() => new("--add-disable-all")
    {
        Description = "Audit-Lauf und '// ainetlinter-disable all' nur in Dateien mit Verstoessen einfuegen",
    };

    internal static Option<bool> CreateRemoveDisableAllOption() => new("--remove-disable-all")
    {
        Description = "Entfernt exakte '// ainetlinter-disable all'-Zeilen aus allen .cs-Dateien unter --path",
    };

    internal static Option<bool> CreateDebtReportOption() => new("--debt-report")
    {
        Description = "Tech-Debt-Report (Disable-all nach Ordner, wave-ready Kandidaten); Exit 0",
    };

    internal static Option<bool> CreateWaveReadyOption() => new("--wave-ready")
    {
        Description = "Nur Verstoesse in Dateien ohne '// ainetlinter-disable all'",
    };

    internal static Option<bool> CreateOnlyChangedOption() => new("--only-changed")
    {
        Description = "Nur Verstoesse in geaenderten Dateien (erfordert --baseline)",
    };

    internal static Option<string?> CreateGitSinceOption() => new("--git-since")
    {
        Description = "Nur Verstoesse in per git diff geaenderten .cs-Dateien seit Ref (z. B. HEAD~1)",
    };
}
