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

    internal static Option<string?> CreatePathOption() => new Option<string?>("--path", "-p")
    {
        Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (nicht erforderlich bei --readme)",
    };

    internal static Option<string?> CreateGraphOption() => new("--graph", "-g")
    {
        Description = "Pfad fuer das zu generierende Mermaid-Abhaengigkeitsdiagramm (.md)",
    };

    internal static Option<string?> CreatePlaybookOption() => new("--playbook", "-pb")
    {
        Description = "Pfad fuer das zu generierende AI Repository-Playbook (.md)",
    };

    internal static Option<string> CreateFormatOption()
    {
        var option = new Option<string>("--format", "-f")
        {
            Description = "Ausgabeformat: text (Standard) oder sarif",
            DefaultValueFactory = _ => "text",
        };
        option.AcceptOnlyFromAmong("text", "sarif");
        return option;
    }

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

    internal static Option<bool> CreateFixOption() => new("--fix")
    {
        Description = "Automatische Behebung einfacher Verstoesse (z. B. sealed, readonly, #nullable enable) direkt ueber die CLI",
    };

    internal static Option<string?> CreateImpactOption() => new("--impact", "-im")
    {
        Description = "Semantische Diff-Impact-Analyse seit Git-Ref (z. B. HEAD~1 oder leer fuer uncommitted)",
        Arity = ArgumentArity.ZeroOrOne
    };

    internal static Option<bool> CreateSyncCursorRulesOption() => new("--sync-cursor-rules", "-scr")
    {
        Description = "Synchronisiert die rules.json Konfiguration als .cursor/rules/AiNetLinter.mdc Datei",
    };

    internal static Option<bool> CreateCheckOption() => new("--check")
    {
        Description = "Prueft auf Drift (z. B. bei --sync-cursor-rules) ohne Dateien zu schreiben",
    };

    internal static Option<string?> CreateFootprintOption() => new("--footprint")
    {
        Description = "Zeigt den detaillierten AI-Context-Footprint fuer eine Klasse an",
    };

    internal static Option<bool> CreateReadmeOption()
    {
        long byteCount = 0;
        try
        {
            using var stream = typeof(CliOptionFactory).Assembly.GetManifestResourceStream("README.md");
            if (stream != null) byteCount = stream.Length;
        }
        catch (Exception ignored) { _ = ignored; }

        return new Option<bool>("--readme")
        {
            Description = $"Gibt die integrierte README.md fuer KI-Agenten aus (Kontext-Footprint: ca. {byteCount} Bytes / ~{byteCount / 4} Tokens).",
        };
    }

    internal static Option<bool> CreateNoCacheOption() => new("--no-cache")
    {
        Description = "Cache deaktivieren — erzwingt vollständige Neu-Analyse aller Dateien.",
    };

    internal static Option<int> CreateCacheTtlOption() => new("--cache-ttl")
    {
        Description = "Cache-Lebensdauer in Minuten (0 = unbegrenzt). Standard: 60.",
        DefaultValueFactory = _ => 60,
    };
}
