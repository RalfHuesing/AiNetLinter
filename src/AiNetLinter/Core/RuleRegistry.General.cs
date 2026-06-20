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
                "**`// @covers T`-Kommentar**: In einer bestehenden Testklasse ergänzen."
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

    // Delegiert an Unter-Gruppen — zu viele Regeln für eine einzelne Methode.
    private static RuleMetadata[] BuildGeneralRules() =>
    [
        ..BuildGeneralCoreRules(),
        ..BuildGeneralAllowRules(),
        ..BuildGeneralAdvancedRules(),
        ..BuildUiSeparationRules(),
    ];

    private static RuleMetadata[] BuildGeneralCoreRules() =>
    [
        new(
            RuleId: "EnforceSealedClasses",
            DisplayName: "Sealed Classes Pflicht",
            GetShortDescription: c => "Konkrete Klassen muessen 'sealed' sein (oder 'sealed partial').",
            Warum: "Nicht-`sealed` Klassen signalisieren irrtümlich, dass Vererbung gewollt ist. Agenten leiten dann von Klassen ab, die nie dafür gedacht waren — erzeugt fragile Hierarchien.",
            Alternativen:
            [
                "**`sealed` ergänzen**: `public sealed class Foo` — auto-fix via `--fix` verfügbar.",
                "**Bei partial-Klassen**: `sealed partial class Foo` (alle Partial-Dateien erhalten `sealed`).",
                "**Wenn Vererbung gewollt**: Suffix prüfen (`Base`, `Foundation`, `Host` sind exempt) oder Klasse explizit `abstract` make."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`sealed` für konkrete Klassen; Ausnahmen: Suffixe in `rules.json → SealedClassExemptSuffixes`.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforceSealedClasses,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "BanPublicNestedTypes",
            DisplayName: "Keine Nested Typen",
            GetShortDescription: c => "Verbot oeffentlicher nested Typen.",
            Warum: "Nested Typen (auch `internal`) erscheinen nicht in Dateilisten und Grep-Ergebnissen auf Namespace-Ebene. Agenten lokalisieren sie über File-Lookup nicht, halluzinieren FQNs (`Outer.Inner` statt `Inner`) und duplizieren sie unbemerkt.",
            Alternativen:
            [
                "**Top-Level-Typ extrahieren** (bevorzugt): Typ in eigene `.cs`-Datei im selben Ordner verschieben. Bei Namenskonflikt Hostnamen als Prefix: `DataTableColumnDefinition` statt `ColumnDefinition`.",
                "**Privat machen**: Wenn der Typ ausschließlich klassenintern genutzt wird — auf `private nested` reduzieren (nur wenn `BanPublicNestedTypesAllowPrivate: true`).",
                "**In Host-Datei als Top-Level verschieben**: Als Top-Level-Typ direkt über oder unter der Host-Klasse in derselben Datei — nur für sehr kleine Hilfstypen sinnvoll."
            ],
            SicherheitsHinweis: "Bei > 5 betroffenen Typen: Nutzer fragen. Externe Referenzen auf `HostClass.NestedType` sind Breaking Changes — Scope prüfen.",
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Verbot oeffentlicher nested Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.BanPublicNestedTypes,
            IsMetric: false,
            IncludeInCursorRules: false,
            ConfigKeyHint: "rules.json → Global.NestedTypeExemptSuffixes"
        ),
        new(
            RuleId: "EnforcePascalCase",
            DisplayName: "PascalCase Bezeichner",
            GetShortDescription: c => "PascalCase fuer oeffentliche Bezeichner erforderlich.",
            Warum: "Agenten orientieren sich an Namenskonventionen, um Typen und Methoden zu finden. Inkonsistente Schreibweise führt zu 'Type not found'-Fehlern und Halluzinationen.",
            Alternativen:
            [
                "**Umbenennen**: `public string myField` → `public string MyField` — auto-fix via `--fix` verfügbar."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Öffentliche Typen/Methoden/Properties: PascalCase.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforcePascalCase,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceSemanticNaming",
            DisplayName: "Semantische Namensgebung",
            GetShortDescription: c => "Generische Namen (data, temp, obj) sind in oeffentlichen Signaturen verboten.",
            Warum: "Generische Namen (`data`, `temp`, `obj`) in öffentlichen Signaturen geben keine Information über den Zweck — Agenten wählen falsche Variablen.",
            Alternativen:
            [
                "**Sprechende Namen**: `data` → `userRecord`, `temp` → `formattedLabel`, `obj` → `siteConfiguration`.",
                "**Typ als Namenspräfix**: Wenn kein fachlicher Name greifbar — zumindest den Typ kodieren (`configEntry` statt `obj`)."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Keine `data`/`temp`/`obj` in öffentlichen Signaturen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceSemanticNaming,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceNullableEnable",
            DisplayName: "Nullable Enable Pflicht",
            GetShortDescription: c => "#nullable enable fehlt am Dateianfang.",
            Warum: "Ohne `#nullable enable` kann der Agent nicht zwischen null-sicheren und unsicheren Pfaden unterscheiden — erzeugt potenzielle NullReferenceExceptions.",
            Alternativen:
            [
                "**Dateikopf ergänzen**: `#nullable enable` als erste Zeile der `.cs`-Datei — auto-fix via `--fix` verfügbar."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`#nullable enable` am Dateianfang jeder `.cs`-Datei.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforceNullableEnable,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildGeneralAllowRules() =>
    [
        new(
            RuleId: "AllowDynamic",
            DisplayName: "Verbot Dynamic Typen",
            GetShortDescription: c => "'dynamic' ist verboten. Nutze statische Typen.",
            Warum: "`dynamic` deaktiviert statische Typanalyse. Agenten können keine korrekten Typ-Inferenzen machen und erzeugen Code, der erst zur Laufzeit scheitert.",
            Alternativen:
            [
                "**Interface oder abstrakte Klasse**: Gemeinsame Schnittstelle statt `dynamic`.",
                "**Generics**: Typparameter `T` statt `object`/`dynamic` für typsichere Flexibilität.",
                "**`Dictionary<string, object>`**: Für Schlüssel-Wert-Szenarien mit expliziten Casts."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`dynamic` ist verboten.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowDynamic,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowOutParameters",
            DisplayName: "Verbot Out Parameter",
            GetShortDescription: c => "'out'-Parameter sind verboten. Benutze Tuples oder Records.",
            Warum: "`out`-Parameter erzeugen Seiteneffekte, die nicht aus der Methodensignatur erkennbar sind — Agenten übersehen Out-Parameter oder setzen sie falsch.",
            Alternativen:
            [
                "**Tuple-Rückgabe**: `(bool Success, int Value) TryGet(string key)`.",
                "**Record**: `TryGetResult TryGet(string key)` mit `record TryGetResult(bool Success, int Value)`.",
                "**Try-Pattern** (erlaubt in `Try*`-Methoden): `bool TryGet(out int value)` — `AllowTryPatternOutParameters` greift."
            ],
            SicherheitsHinweis: null,
            Intent: "csharp-idiom",
            Severity: "warning",
            CursorHint: "`out` Parameter verboten; Ausnahme: `Try*`-Methoden.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowOutParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowUnsealedPartialClasses",
            DisplayName: "Allow Unsealed Partial Classes",
            GetShortDescription: c => "Unversiegelte partial Klassen erlaubt (z. B. Blazor-Komponenten).",
            Warum: "Blazor-Komponenten und generierte Code-Klassen benoetigen unversiegelte partial Klassen. Diese Option deaktiviert die EnforceSealedClasses-Pruefung fuer solche Typen.",
            Alternativen: ["**Standard**: `AllowUnsealedPartialClasses: false` — alle partial Klassen muessen sealed sein."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Unversiegelte `partial` Klassen erlaubt (z. B. Blazor-Komponenten).",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowUnsealedPartialClasses,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowTryPatternOutParameters",
            DisplayName: "Allow Try Pattern Out Parameters",
            GetShortDescription: c => "out-Parameter in Try*-Methoden erlaubt.",
            Warum: "Das Try*-Pattern (TryParse, TryGet) benoetigt out-Parameter. Diese Option deaktiviert die Pruefung auf out-Parameter fuer Methoden mit 'Try'-Praefix.",
            Alternativen: ["**Ohne out**: Gib Tuple<bool, T> oder Result<T> zurueck statt out-Parameter."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`out` in `Try*`-Methoden erlaubt.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowTryPatternOutParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowCancellationShutdownCatch",
            DisplayName: "Allow Cancellation Shutdown Catch",
            GetShortDescription: c => "OperationCanceledException beim Shutdown abfangen erlaubt.",
            Warum: "Graceful-Shutdown-Logik muss OperationCanceledException abfangen koennen. Diese Option erlaubt den Catch in Shutdown-Methoden, die sonst durch die Pruefung blockiert waeren.",
            Alternativen: ["**Ohne Catch**: CancellationToken weiterpropagieren und kein Catch auf oberster Ebene."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`OperationCanceledException` beim Shutdown abfangen erlaubt.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowCancellationShutdownCatch,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowedEmptyReads",
            DisplayName: "Allowed Empty Reads",
            GetShortDescription: c => "Leseoperationen ohne unmittelbaren Guard verboten.",
            Warum: "Leseoperationen ohne unmittelbaren Null-Guard sind eine haeufige Fehlerquelle. Diese Option erlaubt spezifische Ausnahmen, z. B. fuer Properties mit garantiertem Wert nach Initialisierung.",
            Alternativen: ["**Mit Guard**: Null-Check direkt nach der Leseoperation."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Leseoperationen immer mit unmittelbarem Guard versehen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowedEmptyReads,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildGeneralAdvancedRules() =>
    [
        new(
            RuleId: "EnforceXmlDocumentation",
            DisplayName: "XML API Dokumentation",
            GetShortDescription: c => "Fehlende XML-Dokumentation fuer oeffentliche Schnittstellen.",
            Warum: "Fehlende XML-Dokumentation für öffentliche APIs zwingt Agenten, den Zweck aus dem Code zu inferieren — erhöht Fehlerrate bei komplexen Parametern.",
            Alternativen:
            [
                "**Summary ergänzen**: `/// <summary>Beschreibung des Zwecks.</summary>` vor dem public Member.",
                "**Parameter dokumentieren**: `/// <param name=\"x\">Bedeutung von x.</param>` für nicht-selbsterklärende Parameter."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "warning",
            CursorHint: "XML-Dokumentation für öffentliche APIs.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceXmlDocumentation,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceExplicitStateImmutability",
            DisplayName: "Immutability Pflicht",
            GetShortDescription: c => "Felder und Properties muessen readonly oder init-only sein.",
            Warum: "Veränderlicher Zustand ist für Agenten schwer zu verfolgen — sie übersehen, wo Zustand geändert wird, und erzeugen Race Conditions.",
            Alternativen:
            [
                "**`readonly` Felder**: Initialisierung im Konstruktor, keine spätere Mutation.",
                "**`init`-only Properties**: `public string Name { get; init; }` — nur bei Objekterstellung setzbar.",
                "**`record`-Typ**: Strukturell unveränderlich — Mutation via `with`-Ausdruck (Copy-and-Modify)."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-resilience",
            Severity: "error",
            CursorHint: "Felder und Properties `readonly`/`init`-only.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceExplicitStateImmutability,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceMinimalApiAsParameters",
            DisplayName: "Minimal API Bindung",
            GetShortDescription: c => "Minimal-API: Parameter muessen per [AsParameters] gebunden werden.",
            Warum: "Minimal-API-Endpunkte ohne Parameter-Binding-Records verleiten Agenten zu inkonsistentem Parameterhandling.",
            Alternativen:
            [
                "**Parameter-Record**: Eingabeparameter in einen `record` zusammenfassen — `record CreateFooRequest(string Name, int Count)`."
            ],
            SicherheitsHinweis: null,
            Intent: "aspnet-binding",
            Severity: "error",
            CursorHint: "Minimal-API: >4 Parameter → `[AsParameters]` + `record`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceMinimalApiAsParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceResultPatternOverExceptions",
            DisplayName: "Result Pattern Pflicht",
            GetShortDescription: c => "Fachlicher Kontrollfluss muss Result-Pattern statt Exceptions (throw) nutzen.",
            Warum: "Exceptions für Business-Logik sind für Agenten schwer zu verfolgen — sie übersehen `throw`-Pfade und schreiben keine Tests für Fehlerfälle.",
            Alternativen:
            [
                "**Result-Typ**: Rückgabetyp `Result<T, Error>` oder `OneOf<Success, Error>` (Bibliothek je nach Projekt wählen).",
                "**Discriminated Union**: Erfolg/Fehler explizit als Typ modellieren — keine Exception für erwartete Fehlerfälle."
            ],
            SicherheitsHinweis: null,
            Intent: "control-flow",
            Severity: "error",
            CursorHint: "`Result<T>` für Domänenfehler; `throw` nur für Infrastruktur-Fehler.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceResultPatternOverExceptions,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceValueObjectContracts",
            DisplayName: "ValueObject Immutability",
            GetShortDescription: c => "ValueObject-Klassen muessen record oder readonly struct sein.",
            Warum: "Klassen mit `*ValueObject`-Suffix sollten strukturell unveränderlich sein. Agenten fügen sonst mutierbare Properties hinzu und brechen das Invariant.",
            Alternativen:
            [
                "**`record`**: `public sealed record PriceValueObject(decimal Amount, string Currency)` — primäres Konstrukt für Value Objects.",
                "**`readonly struct`**: Für kleine, häufig kopierte Value Objects ohne Vererbungsbedarf."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Klassen mit `*ValueObject`-Suffix: `record` oder `readonly struct`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceValueObjectContracts,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "PreventContextDependentOverloads",
            DisplayName: "Keine primitives-only Ueberladungen",
            GetShortDescription: c => "Keine Überladungen mit identischer Parameteranzahl für primitive Typen.",
            Warum: "Überladungen mit identischer Parameteranzahl, die sich nur durch primitive Typen unterscheiden, sind für Agenten nicht disambiguierbar — falscher Aufruf bleibt kompilierbar.",
            Alternativen:
            [
                "**Explizite Methodennamen**: `ParseFromString(string)` + `ParseFromInt(int)` statt `Parse(string)` + `Parse(int)`.",
                "**Named-Constructor-Pattern**: Statische Factory-Methoden mit klaren Namen statt Überladungen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Keine Überladungen mit identischer Parameteranzahl für primitive Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.PreventContextDependentOverloads,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildUiSeparationRules() =>
    [
        new(
            RuleId: "BlazorRequireCodeBehind",
            DisplayName: "Blazor Code Behind",
            GetShortDescription: c => "Blazor-Komponenten muessen Code-Behind nutzen.",
            Warum: "Logik in `@code { }` Blöcken ist für Agenten ohne Razor-Unterstützung unsichtbar — sie modifizieren nur die `.razor.cs`-Datei und übersehen den `@code`-Block.",
            Alternativen:
            [
                "**Code-Behind-Datei anlegen**: `@code { ... }` → separate `.razor.cs` partial class verschieben.",
                "**Suppression** (in `.razor`-Dateien): `@* ainetlinter-disable BlazorRequireCodeBehind *@`"
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Blazor-Komponenten muessen Code-Behind nutzen.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.BlazorRequireCodeBehind,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "BlazorRequireCssIsolation",
            DisplayName: "Blazor CSS Isolation",
            GetShortDescription: c => "Blazor-Komponenten muessen CSS-Isolation nutzen.",
            Warum: "Inline `<style>`-Tags in `.razor`-Dateien werden vom Agenten oft übersehen und nicht in CSS-Isolation-Dateien migriert — erzeugt Style-Konflikte.",
            Alternativen:
            [
                "**CSS-Isolation-Datei anlegen**: `<style>` → `.razor.css` Datei im gleichen Ordner.",
                "**Suppression** (wenn keine Styles nötig): `@* ainetlinter-disable BlazorRequireCssIsolation *@`"
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "warning",
            CursorHint: "Blazor-Komponenten muessen CSS-Isolation nutzen.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.BlazorRequireCssIsolation,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "WpfRequireMinimalCodeBehind",
            DisplayName: "WPF Minimal Code Behind",
            GetShortDescription: c => "WPF-Code-Behind darf keine Business-Logik enthalten.",
            Warum: "Umfangreiches Code-Behind in WPF verletzt MVVM. Agenten fügen Logik ins Code-Behind ein, wenn sie den ViewModel nicht finden — kumuliert technische Schulden.",
            Alternativen:
            [
                "**ViewModel**: Logik, Commands und Properties in den zugehörigen ViewModel verschieben.",
                "**EventToCommand-Binding**: Event-Handler durch Command-Bindings ersetzen (MVVM-Infrastruktur des Projekts nutzen)."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "WPF-Code-Behind darf keine Business-Logik enthalten.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.WpfRequireMinimalCodeBehind,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
    ];
}
