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
        Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (nicht erforderlich bei --docs)",
    };

    internal static Option<string?> CreatePlaybookOption() => new("--playbook", "-pb")
    {
        Description = "Pfad fuer das zu generierende AI Repository-Playbook (.md)",
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

    internal static Option<string?> CreateDocsOption() => new("--docs", "-d")
    {
        Description = "Gibt eine integrierte Dokumentationsdatei aus (Optionen: integration, readme, agent-api, configuration, rationale, roadmap, rules-json; case-insensitive). 'integration' erklaert die vollstaendige Projekt-Integration Schritt fuer Schritt.",
    };

    internal static Option<bool> CreateListRulesOption() => new("--list-rules")
    {
        Description = "Alle bekannten Regeln als Tabelle ausgeben",
    };

    internal static Option<string?> CreateDescribeRuleOption() => new("--describe-rule")
    {
        Description = "Vollstaendige Beschreibung einer Regel ausgeben (z. B. --describe-rule EnforceNullableEnable)",
    };

    internal static Option<string?> CreateSearchRulesOption() => new("--search-rules")
    {
        Description = "Regeln nach Stichwort durchsuchen (RuleId, Bezeichnung, Warum, Intent)",
    };

    internal static Option<bool> CreateNoCacheOption() => new("--no-cache")
    {
        Description = "Cache deaktivieren — erzwingt vollständige Neu-Analyse aller Dateien.",
    };

    internal static Option<int> CreateCacheTtlOption() => new("--cache-ttl")
    {
        Description = "Cache-Lebensdauer in Minuten (0 = unbegrenzt). Standard: 60.",
        DefaultValueFactory = _ => 60,
    };

    internal static Option<string?> CreateMapOption() => new("--map")
    {
        Description = "Codebase-Landkarte generieren. Erfordert --path. Typen: vocabulary | structure | hotspots",
    };

    internal static Option<string?> CreateEvalOption() => new("--eval")
    {
        Description = "Assemblierten Eval-Audit-Prompt ausgeben. Erfordert --path. Namen: naming-drift | architecture-intent",
    };

    internal static Option<bool> CreateListEvalsOption() => new("--list-evals")
    {
        Description = "Alle verfügbaren Eval-Typen als Tabelle ausgeben",
    };

    internal static Option<string[]> CreateSpecOption()
    {
        var opt = new Option<string[]>("--spec")
        {
            Description = "Spezifikations-Quelle für --eval: Datei oder Verzeichnis (erste Ebene, nur .md). Mehrfach angebbar.",
            AllowMultipleArgumentsPerToken = false,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }
}
