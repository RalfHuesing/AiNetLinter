#nullable enable

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

    internal static Option<bool> CreateSyncAgentRulesOption() => new("--sync-agent-rules", "-sar")
    {
        Description = "Synchronisiert die rules.json Konfiguration als .agents/rules/AiNetLinter.mdc Datei",
    };

    internal static Option<bool> CreateSyncAgentRulesOnlyOption() => new("--sync-agent-rules-only", "-saro")
    {
        Description = "Synchronisiert die rules.json Konfiguration als .agents/rules/AiNetLinter.mdc Datei und beendet das Programm (schneller Pfad ohne Lint-Lauf)",
    };


    internal static Option<string?> CreateAgentRulesPathOption() => new("--agent-rules-path", "-arp")
    {
        Description = "Benutzerdefinierter Pfad (Verzeichnis oder .mdc-Datei) fuer die Synchronisation der Agent-Regeln (Optional)",
    };

    internal static Option<bool> CreateCheckOption() => new("--check")
    {
        Description = "Prueft auf Drift (z. B. bei --sync-agent-rules) ohne Dateien zu schreiben",
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

    internal static Option<string[]> CreateIncludeProjectOption()
    {
        var opt = new Option<string[]>("--project")
        {
            Description = "Filtert die Analyse auf bestimmte Projektnamen (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Core,*.Domain').",
            AllowMultipleArgumentsPerToken = true,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }

    internal static Option<string[]> CreateExcludeProjectOption()
    {
        var opt = new Option<string[]>("--exclude-project")
        {
            Description = "Schließt bestimmte Projekte von der Analyse aus (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Tests').",
            AllowMultipleArgumentsPerToken = true,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }

    internal static Option<string[]> CreateIncludeNamespaceOption()
    {
        var opt = new Option<string[]>("--namespace")
        {
            Description = "Filtert die Analyse auf bestimmte C#-Namespaces (kommagetrennt, Glob-Muster erlaubt, z. B. 'San.Auth*').",
            AllowMultipleArgumentsPerToken = true,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }

    internal static Option<string[]> CreateExcludeNamespaceOption()
    {
        var opt = new Option<string[]>("--exclude-namespace")
        {
            Description = "Schließt bestimmte Namespaces aus (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Internal').",
            AllowMultipleArgumentsPerToken = true,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }

    internal static Option<bool> CreateExcludeTestsOption() => new("--exclude-tests")
    {
        Description = "Shortcut, um alle automatisch erkannten Testprojekte auszublenden.",
    };

    internal static Option<bool> CreateTestsOnlyOption() => new("--tests-only")
    {
        Description = "Shortcut, um ausschließlich Testprojekte zu analysieren.",
    };

    internal static Option<bool> CreatePublicOnlyOption() => new("--public-only")
    {
        Description = "Blendet private und protected Member in Maps (wie skeleton) aus, um Token zu sparen.",
    };

    internal static Option<bool> CreateMcpServerOption() => new("--mcp-server")
    {
        Description = "Startet einen stdio-basierten MCP-Server ohne eigenen Projektbezug: Jeder Tool-Aufruf adressiert per projectRoot einen Projekt-Key aus der Definitionsdatei ainetlinter.project.json im Projektroot.",
    };

    internal static Option<string?> CreateMcpLogOption() => new("--mcp-log", "-mcp-log")
    {
        Description = "Optionaler Pfad fuer das MCP-Call-Log (JSONL-Format, ein Eintrag pro Zeile). Default: aktiv unter %LOCALAPPDATA%\\RalfHuesing\\McpObservability\\ainetlinter\\<yyyy-MM-dd>\\. Ohne Wert (ZeroOrOne) wird dieser Standardpfad verwendet; explizite Pfade werden absolut wie angegeben oder relativ zum Solution-Verzeichnis aufgeloest. Jeder Prozess schreibt eine eigene Datei mit PID und InstanceId. Beispiel: --mcp-log ./.mcp-log/",
        Arity = ArgumentArity.ZeroOrOne,
    };

    internal static Option<int?> CreateParentPidOption() => new("--parent-pid")
    {
        Description = "Optionale PID des Elternprozesses fuer den MCP-Lebenszyklus-Watchdog. Ohne diese Option wird die Parent-PID automatisch ermittelt.",
    };

    internal static Option<decimal?> CreateMcpProjectTtlOption() => new("--mcp-project-ttl-minutes")
    {
        Description = "Optionale Idle-TTL der Projekt-Registry in Minuten (Dezimalwerte, InvariantCulture, z. B. 0.05 fuer ca. 3 Sekunden). Ohne Flag gilt der Default von 45 Minuten.",
    };

    internal static Option<int?> CreateMcpMaxProjectsOption() => new("--mcp-max-projects")
    {
        Description = "Optionale maximale Anzahl residenter Projekt-Keys in der Projekt-Registry (LRU-Rahmen). Ohne Flag gilt der Default von 4.",
    };

    internal static Option<string?> CreateAnalyzeMcpLogOption() => new("--analyze-mcp-log")
    {
        Description = "Offline-Auswertung eines MCP-Call-Logs: JSONL-Datei, Log-Verzeichnis oder Glob.",
        Arity = ArgumentArity.ExactlyOne,
    };

    internal static Option<string?> CreateFormatOption() => new("--format")
    {
        Description = "Ausgabeformat fuer --analyze-mcp-log: text (Standard) oder json.",
        DefaultValueFactory = _ => "text",
    };

    internal static Option<string[]> CreateIgnoreSuppressionsOption()
    {
        var opt = new Option<string[]>("--ignore-suppressions")
        {
            Description = "Ignoriert Suppressions (dateiweit & inline) fuer bestimmte Sprachen (kommagetrennt oder mehrfach angebbar: all, cs/c#, razor, js, css). Standard ohne Wert: all.",
            AllowMultipleArgumentsPerToken = true,
        };
        opt.Arity = ArgumentArity.ZeroOrMore;
        return opt;
    }
}
