#nullable enable

using System.Linq;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

internal static partial class RuleRegistry
{
    private static RuleMetadata[] BuildArchitectureRules() =>
    [
        new(
            RuleId: "ForbiddenNamespaceDependency",
            DisplayName: "Namespace Abhaengigkeiten",
            GetShortDescription: c => "Unerlaubte Namespace-Abhaengigkeit gemaess Architektur-Regeln.",
            Warum: "Architektur-Slices sollen entkoppelt sein. Direkte Abhängigkeiten zwischen verbotenen Namespaces erzeugen Zyklen, die Agenten nicht erkennen und weiterverstärken.",
            Alternativen:
            [
                "**Interface/Abstraktion**: Die Abhängigkeit hinter einem Interface im erlaubten Namespace verstecken.",
                "**Events/Messages**: Kommunikation über Events statt direkten Aufruf (Inversion of Control).",
                "**Shared-Kernel**: Gemeinsam genutzte Typen in einen neutral erlaubten Namespace verschieben."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Unerlaubte Namespace-Abhaengigkeit gemaess Architektur-Regeln.",
            HasAutoFix: false,
            IsEnabled: c => c.ForbiddenNamespaceDependencies.Any(),
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "EnforceNamespaceDirectoryMapping",
            DisplayName: "Namespace Pfadmapping",
            GetShortDescription: c => "Namespace entspricht nicht dem Verzeichnis-Pfad.",
            Warum: "Wenn Namespace und Dateipfad nicht übereinstimmen, können Agenten Dateien nicht über den Namespace lokalisieren und erzeugen fehlerhafte `using`-Direktiven.",
            Alternativen:
            [
                "**Namespace anpassen**: Namespace an den Verzeichnispfad angleichen.",
                "**Datei verschieben**: Datei in das zum Namespace passende Verzeichnis verschieben."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Namespace muss Verzeichnispfad entsprechen (Modus: `rules.json`).",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceNamespaceDirectoryMapping,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "DetectAndBanPhantomDependencies",
            DisplayName: "Keine Phantom Dependencies",
            GetShortDescription: c => "Phantom-Abhaengigkeiten (unaufloesbare Namespaces oder Reflection-Laden) verboten.",
            Warum: "Nicht-auflösbare Namespaces und Reflection-Lade-APIs sind die häufigste Halluzinations-Quelle in KI-generiertem Code — der Compiler sieht sie nicht, das Programm scheitert erst zur Laufzeit.",
            Alternativen:
            [
                "**Korrekte `using`-Direktiven**: Nur explizit referenzierte NuGet-Pakete und Projekt-Namespaces verwenden.",
                "**NuGet-Referenzen prüfen**: Ob das benötigte Paket in der `.csproj` steht.",
                "**Reflection-Load ersetzen**: `Assembly.LoadFrom` / `Activator.CreateInstance` durch statische Registrierung."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Keine unauflösbaren `using`; kein `Type.GetType`/`Activator.CreateInstance` für App-Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.DetectAndBanPhantomDependencies,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildTestCoverageRules() =>
    [
        new(
            RuleId: "StaticTestSentinel",
            DisplayName: "Testabdeckung Sentinel",
            GetShortDescription: c => "Fehlende Testabdeckung (Unit-Test) fuer komplexe Klasse.",
            Warum: "Komplexe Typen ohne Testabdeckung sind für Agenten eine Black Box — sie können keine Regression bei Änderungen erkennen.",
            Alternativen:
            [
                "**Testklasse anlegen**: `{Name}Tests.cs` im entsprechenden Test-Projekt.",
                "**`typeof(T)`-Referenz**: `typeof(FooClass)` in einer Testklasse — `EnableTestSentinel` erkennt das als Sentinel.",
                "**`// @covers T`-Kommentar**: In einer bestehenden Testklasse ergänzen.",
                "**Blazor Code-Behind (False-Positive)**: Deklariere die Klasse im `.razor.cs`-File explizit mit `: ComponentBase` (damit die statische Analyse sie als ausgenommen erkennt) oder füge Namen/Suffix zu `rules.json → TestSentinel.ExemptClassNameSuffixes` hinzu."
            ],
            SicherheitsHinweis: null,
            Intent: "test-coverage",
            Severity: "warning",
            CursorHint: "Für komplexe Typen: Testklasse, `typeof(T)` oder `// @covers T`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnableTestSentinel,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];
}
