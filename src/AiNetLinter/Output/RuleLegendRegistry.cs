#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Output;

public sealed record RuleLegendEntry(string Warum, string[] Alternativen, string? SicherheitsHinweis = null);

/// <summary>
/// Enthält Warum-Beschreibungen und Fix-Alternativen für alle bekannten Linter-Regeln.
/// Neue Regeln hier ergänzen — der Test AllRegisteredRulesHaveExplicitLegendEntry schlägt sonst an.
/// </summary>
public static class RuleLegendRegistry
{
    public static bool HasEntry(string ruleName) => Entries.ContainsKey(ruleName);

    public static IReadOnlyCollection<string> KnownRuleNames => Entries.Keys.ToList().AsReadOnly();

    public static RuleLegendEntry? TryGet(string ruleName) =>
        Entries.TryGetValue(ruleName, out var e) ? e : null;

    internal static string Render(string ruleName, int count, string intent)
    {
        var sb = new StringBuilder();
        var plural = count == 1 ? "Verstoß" : "Verstöße";
        sb.Append($"\n### {ruleName} — {count} {plural} [{intent}]\n");

        if (!Entries.TryGetValue(ruleName, out var entry))
        {
            sb.Append("Keine spezifische Anleitung hinterlegt — behebe gemäß Projektrichtlinien.\n");
            return sb.ToString();
        }

        sb.Append($"**Warum:** {entry.Warum}\n\n");
        sb.Append("**Fix-Alternativen:**\n");
        foreach (var alt in entry.Alternativen)
            sb.Append($"- {alt}\n");

        if (entry.SicherheitsHinweis != null)
            sb.Append($"\n> ⚠ {entry.SicherheitsHinweis}\n");

        return sb.ToString();
    }

    private static readonly Dictionary<string, RuleLegendEntry> Entries =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["BanPublicNestedTypes"] = new(
                Warum: "Nested Typen (auch `internal`) erscheinen nicht in Dateilisten und Grep-Ergebnissen auf Namespace-Ebene. Agenten lokalisieren sie über File-Lookup nicht, halluzinieren FQNs (`Outer.Inner` statt `Inner`) und duplizieren sie unbemerkt.",
                Alternativen: [
                    "**Top-Level-Typ extrahieren** (bevorzugt): Typ in eigene `.cs`-Datei im selben Ordner verschieben. Bei Namenskonflikt Hostnamen als Prefix: `DataTableColumnDefinition` statt `ColumnDefinition`.",
                    "**Privat machen**: Wenn der Typ ausschließlich klassenintern genutzt wird — auf `private nested` reduzieren (nur wenn `BanPublicNestedTypesAllowPrivate: true`).",
                    "**In Host-Datei als Top-Level verschieben**: Als Top-Level-Typ direkt über oder unter der Host-Klasse in derselben Datei — nur für sehr kleine Hilfstypen sinnvoll.",
                ],
                SicherheitsHinweis: "Bei > 5 betroffenen Typen: Nutzer fragen. Externe Referenzen auf `HostClass.NestedType` sind Breaking Changes — Scope prüfen."
            ),
            ["MaxPublicMembersPerType"] = new(
                Warum: "Breite API-Fläche erhöht die Wahrscheinlichkeit, dass Agenten existierende Methoden übersehen und duplizieren. Der Agent wählt aus dem sichtbaren Ausschnitt, nicht der vollständigen Klasse.",
                Alternativen: [
                    "**Klasse nach Verantwortlichkeit aufteilen**: z. B. Command/Query, Read/Write, Domain/Infrastructure als separate Klassen.",
                    "**Facade-Prinzip**: Hilfsmethoden auf `private` oder `internal` reduzieren — nur die echte öffentliche API exponieren.",
                    "**Extension-Methoden auslagern**: Optional-/Hilfsmethoden als `*Extensions`-Klasse im selben Namespace (Suffix `Extensions` ist per Default exempt).",
                    "**State-Objekt**: Zusammengehörige Properties in ein dediziertes `record`-Zustandsobjekt auslagern.",
                ],
                SicherheitsHinweis: "Oft ein SRP-Signal. Vor größerem Refactoring Nutzer fragen und Architektur-Constraints (`.cursor/rules`, `CLAUDE.md`) lesen."
            ),
            ["MaxBoolParameterCount"] = new(
                Warum: "`DoWork(true, false)` trägt an der Call-Site keine semantische Information — der Agent ordnet Flags falsch zu und macht Aufruffehler.",
                Alternativen: [
                    "**Parameter-Object** (bevorzugt): `record WorkOptions(bool WithLogging, bool ForceRefresh)` — die Call-Site wird selbsterklärend.",
                    "**Enum**: Bei zwei oder mehr Flags, die verschiedene Modi darstellen, ein Enum statt bool-Kombination.",
                    "**Named Arguments** (kurzfristig): `DoWork(withLogging: true, forceRefresh: false)` — kein Strukturumbau nötig, rein syntaktische Verbesserung.",
                    "**Separierte Methoden**: Wenn die Pfade fachlich distinct sind — `ProcessSingle()` / `ProcessBatch()` statt `Process(bool isBatch)`.",
                ]
            ),
            ["MaxPartialClassFiles"] = new(
                Warum: "Agenten sehen nur die aktuell geöffnete Datei. Invarianten, Felder und Methoden aus anderen Partial-Dateien derselben Klasse sind unsichtbar — der Agent erkennt Konflikte nicht und dupliziert Logik.",
                Alternativen: [
                    "**Eigenständige Klassen extrahieren** (bevorzugt): Logik aus Partials in dedizierte, fachlich benannte Klassen auslagern — z. B. `FooCommandHandler`, `FooQueryHandler`, `FooValidator`.",
                    "**Facade-Klasse**: Wenn Partials verschiedene Subsysteme bedienen, eine schlanke Fassadenklasse pro Subsystem einführen.",
                    "**Namespace-basierte Trennung**: Verwandte Methoden in eigenständige Klassen im selben Namespace verschieben statt Partials.",
                    "**Interface** (nur wenn Projektregeln es erlauben): Wenn Partials verschiedene Rollen abbilden — Interfaces extrahieren und Implementierungen trennen.",
                ],
                SicherheitsHinweis: "Partials aufzulösen ist ein tiefgreifender Architektureingriff. **Nutzer ZWINGEND fragen bevor du beginnst** — die gewählte Alternativarchitektur muss dem Projektstil entsprechen."
            ),
            ["AIContextFootprint"] = new(
                Warum: "Ein zu großer transitiver Code-Footprint bedeutet: der Agent braucht das volle Kontextbudget für eine einzige Klasse. Er sieht nie den vollständigen Kontext und übersieht Invarianten.",
                Alternativen: [
                    "**Schlankes Interface einführen**: Die größten Abhängigkeiten (s. Details) hinter einem minimalen Interface verstecken — reduziert den transitiven Footprint direkt.",
                    "**Klasse aufteilen**: Klasse nach Verantwortlichkeiten teilen und die Teile separat halten — jeder Teil hat kleineren Footprint.",
                    "**Abhängigkeit kapseln**: Statt direkter Abhängigkeit eine Facade oder ein Data-Transfer-Objekt übergeben.",
                ],
                SicherheitsHinweis: "Interfaces einführen kann Architekturentscheidungen ändern. Nutzer fragen ob Interfaces im Projekt erlaubt sind."
            ),
            ["EnforceSealedClasses"] = new(
                Warum: "Nicht-`sealed` Klassen signalisieren irrtümlich, dass Vererbung gewollt ist. Agenten leiten dann von Klassen ab, die nie dafür gedacht waren — erzeugt fragile Hierarchien.",
                Alternativen: [
                    "**`sealed` ergänzen**: `public sealed class Foo` — auto-fix via `--fix` verfügbar.",
                    "**Bei partial-Klassen**: `sealed partial class Foo` (alle Partial-Dateien erhalten `sealed`).",
                    "**Wenn Vererbung gewollt**: Suffix prüfen (`Base`, `Foundation`, `Host` sind exempt) oder Klasse explizit `abstract` machen.",
                ]
            ),
            ["EnforceNoSilentCatch"] = new(
                Warum: "Leere catch-Blöcke verbergen Fehler. Agenten können nicht erkennen ob ein Fehler normal oder kritisch ist — führt zu Silent Data Loss.",
                Alternativen: [
                    "**Logging + Rethrow**: `catch (Exception ex) { _logger.LogError(ex, \"...\"); throw; }`",
                    "**Gezieltes Abfangen**: Nur die erwartete Exception-Type abfangen und spezifisch behandeln oder in ein Ergebnisobjekt umwandeln.",
                    "**Exception-Variable `ignored`**: `catch (SomeException ignored)` — der Linter erkennt den Variablennamen als explizit gewolltes Ignorieren.",
                    "**Suppression** (letztes Mittel, nur nach Freigabe): `// ainetlinter-disable EnforceNoSilentCatch` an der catch-Zeile.",
                ]
            ),
            ["MaxLineCount"] = new(
                Warum: "Lange Dateien übersteigen das lesbare Kontextfenster. Der Agent sieht nur Ausschnitte und übersieht Invarianten am Datei-Anfang oder -Ende.",
                Alternativen: [
                    "**Vertical Slices**: Datei nach Verantwortlichkeit aufteilen (Command/Query, Domain/Infrastructure).",
                    "**Helper-Klassen auslagern**: Hilfsmethoden und innere Logik in dedizierte Klassen extrahieren.",
                    "**Partial** (letztes Mittel): Nur wenn Datei bereits partial ist — `MaxPartialClassFiles`-Grenze beachten.",
                ]
            ),
            ["MaxMethodLineCount"] = new(
                Warum: "Lange Methoden übersteigen den analysierbaren Ausschnitt — Seiteneffekte am Methodenende werden vom Agenten übersehen.",
                Alternativen: [
                    "**Hilfsmethoden extrahieren**: Abschnitte mit klarem Zweck in private Methoden auslagern (Named-Block-Muster).",
                    "**Early Return**: Validierung an den Anfang, Hauptlogik flach halten.",
                    "**Command/Query aufteilen**: Wenn die Methode sowohl schreibt als auch liest — zwei separate Methoden.",
                ]
            ),
            ["MaxMethodParameterCount"] = new(
                Warum: "Viele Parameter erhöhen die Wahrscheinlichkeit, dass Agenten Argumente in falscher Reihenfolge übergeben oder Pflichtparameter übersehen.",
                Alternativen: [
                    "**Parameter-Object** (bevorzugt): `record FooRequest(int X, string Y, bool Z)` — C# Primary Constructor, kompakt und selbstdokumentierend.",
                    "**Builder-Pattern**: Für optionale Parameter mit vielen Kombinationen.",
                    "**Methode aufteilen**: Wenn Parameter verschiedene Anwendungsfälle kodieren — separierte Methoden mit eindeutigen Namen.",
                ]
            ),
            ["MaxCyclomaticComplexity"] = new(
                Warum: "Hohe zyklomatische Komplexität (McCabe) bedeutet viele mögliche Ausführungspfade. Agenten analysieren typischerweise den Happy-Path und übersehen Randfälle.",
                Alternativen: [
                    "**Methode aufteilen**: Jeden größeren Zweig in eine benannte Hilfsmethode auslagern.",
                    "**Dictionary-Dispatch**: `switch`-Kaskaden über Typen/Werte → `Dictionary<Key, Action>` oder Strategy-Pattern.",
                    "**Guard Clauses**: Frühe Rückgaben für Fehlerfälle reduzieren Verschachtelung ohne Logikänderung.",
                ]
            ),
            ["MaxCognitiveComplexity"] = new(
                Warum: "Kognitive Komplexität (SonarSource) misst die mentale Last beim Lesen — tief verschachtelter Code wird vom Agenten falsch interpretiert.",
                Alternativen: [
                    "**Early Return**: Validierung zuerst, Hauptpfad danach flach.",
                    "**Bedingungen benennen**: Komplexe boolean-Ausdrücke in benannte Methoden/Properties extrahieren (`bool IsEligible => ...`).",
                    "**Schleifenrumpf auslagern**: Schleifeninhalt in Hilfsmethode — die Schleife wird zur Iteration, die Methode zur Verarbeitungslogik.",
                ]
            ),
            ["ForbiddenNamespaceDependency"] = new(
                Warum: "Architektur-Slices sollen entkoppelt sein. Direkte Abhängigkeiten zwischen verbotenen Namespaces erzeugen Zyklen, die Agenten nicht erkennen und weiterverstärken.",
                Alternativen: [
                    "**Interface/Abstraktion**: Die Abhängigkeit hinter einem Interface im erlaubten Namespace verstecken.",
                    "**Events/Messages**: Kommunikation über Events statt direkten Aufruf (Inversion of Control).",
                    "**Shared-Kernel**: Gemeinsam genutzte Typen in einen neutral erlaubten Namespace verschieben.",
                ]
            ),
            ["EnforcePascalCase"] = new(
                Warum: "Agenten orientieren sich an Namenskonventionen, um Typen und Methoden zu finden. Inkonsistente Schreibweise führt zu 'Type not found'-Fehlern und Halluzinationen.",
                Alternativen: [
                    "**Umbenennen**: `public string myField` → `public string MyField` — auto-fix via `--fix` verfügbar.",
                ]
            ),
            ["EnforceXmlDocumentation"] = new(
                Warum: "Fehlende XML-Dokumentation für öffentliche APIs zwingt Agenten, den Zweck aus dem Code zu inferieren — erhöht Fehlerrate bei komplexen Parametern.",
                Alternativen: [
                    "**Summary ergänzen**: `/// <summary>Beschreibung des Zwecks.</summary>` vor dem public Member.",
                    "**Parameter dokumentieren**: `/// <param name=\"x\">Bedeutung von x.</param>` für nicht-selbsterklärende Parameter.",
                ]
            ),
            ["EnforceSemanticNaming"] = new(
                Warum: "Generische Namen (`data`, `temp`, `obj`) in öffentlichen Signaturen geben keine Information über den Zweck — Agenten wählen falsche Variablen.",
                Alternativen: [
                    "**Sprechende Namen**: `data` → `userRecord`, `temp` → `formattedLabel`, `obj` → `siteConfiguration`.",
                    "**Typ als Namenspräfix**: Wenn kein fachlicher Name greifbar — zumindest den Typ kodieren (`configEntry` statt `obj`).",
                ]
            ),
            ["EnforceNullableEnable"] = new(
                Warum: "Ohne `#nullable enable` kann der Agent nicht zwischen null-sicheren und unsicheren Pfaden unterscheiden — erzeugt potenzielle NullReferenceExceptions.",
                Alternativen: [
                    "**Dateikopf ergänzen**: `#nullable enable` als erste Zeile der `.cs`-Datei — auto-fix via `--fix` verfügbar.",
                ]
            ),
            ["AllowDynamic"] = new(
                Warum: "`dynamic` deaktiviert statische Typanalyse. Agenten können keine korrekten Typ-Inferenzen machen und erzeugen Code, der erst zur Laufzeit scheitert.",
                Alternativen: [
                    "**Interface oder abstrakte Klasse**: Gemeinsame Schnittstelle statt `dynamic`.",
                    "**Generics**: Typparameter `T` statt `object`/`dynamic` für typsichere Flexibilität.",
                    "**`Dictionary<string, object>`**: Für Schlüssel-Wert-Szenarien mit expliziten Casts.",
                ]
            ),
            ["AllowOutParameters"] = new(
                Warum: "`out`-Parameter erzeugen Seiteneffekte, die nicht aus der Methodensignatur erkennbar sind — Agenten übersehen Out-Parameter oder setzen sie falsch.",
                Alternativen: [
                    "**Tuple-Rückgabe**: `(bool Success, int Value) TryGet(string key)`.",
                    "**Record**: `TryGetResult TryGet(string key)` mit `record TryGetResult(bool Success, int Value)`.",
                    "**Try-Pattern** (erlaubt in `Try*`-Methoden): `bool TryGet(out int value)` — `AllowTryPatternOutParameters` greift.",
                ]
            ),
            ["StaticTestSentinel"] = new(
                Warum: "Komplexe Typen ohne Testabdeckung sind für Agenten eine Black Box — sie können keine Regression bei Änderungen erkennen.",
                Alternativen: [
                    "**Testklasse anlegen**: `{Name}Tests.cs` im entsprechenden Test-Projekt.",
                    "**`typeof(T)`-Referenz**: `typeof(FooClass)` in einer Testklasse — `EnableTestSentinel` erkennt das als Sentinel.",
                    "**`// @covers T`-Kommentar**: In einer bestehenden Testklasse ergänzen.",
                ]
            ),
            ["EnforceExplicitStateImmutability"] = new(
                Warum: "Veränderlicher Zustand ist für Agenten schwer zu verfolgen — sie übersehen, wo Zustand geändert wird, und erzeugen Race Conditions.",
                Alternativen: [
                    "**`readonly` Felder**: Initialisierung im Konstruktor, keine spätere Mutation.",
                    "**`init`-only Properties**: `public string Name { get; init; }` — nur bei Objekterstellung setzbar.",
                    "**`record`-Typ**: Strukturell unveränderlich — Mutation via `with`-Ausdruck (Copy-and-Modify).",
                ]
            ),
            ["PreventContextDependentOverloads"] = new(
                Warum: "Überladungen mit identischer Parameteranzahl, die sich nur durch primitive Typen unterscheiden, sind für Agenten nicht disambiguierbar — falscher Aufruf bleibt kompilierbar.",
                Alternativen: [
                    "**Explizite Methodennamen**: `ParseFromString(string)` + `ParseFromInt(int)` statt `Parse(string)` + `Parse(int)`.",
                    "**Named-Constructor-Pattern**: Statische Factory-Methoden mit klaren Namen statt Überladungen.",
                ]
            ),
            ["EnforceNamespaceDirectoryMapping"] = new(
                Warum: "Wenn Namespace und Dateipfad nicht übereinstimmen, können Agenten Dateien nicht über den Namespace lokalisieren und erzeugen fehlerhafte `using`-Direktiven.",
                Alternativen: [
                    "**Namespace anpassen**: Namespace an den Verzeichnispfad angleichen.",
                    "**Datei verschieben**: Datei in das zum Namespace passende Verzeichnis verschieben.",
                ]
            ),
            ["DetectAndBanPhantomDependencies"] = new(
                Warum: "Nicht-auflösbare Namespaces und Reflection-Lade-APIs sind die häufigste Halluzinations-Quelle in KI-generiertem Code — der Compiler sieht sie nicht, das Programm scheitert erst zur Laufzeit.",
                Alternativen: [
                    "**Korrekte `using`-Direktiven**: Nur explizit referenzierte NuGet-Pakete und Projekt-Namespaces verwenden.",
                    "**NuGet-Referenzen prüfen**: Ob das benötigte Paket in der `.csproj` steht.",
                    "**Reflection-Load ersetzen**: `Assembly.LoadFrom` / `Activator.CreateInstance` durch statische Registrierung.",
                ]
            ),
            ["MaxDirectoryDepth"] = new(
                Warum: "Tief verschachtelte Verzeichnisse sind für Agenten schwer zu navigieren — File-Listings überschreiten das Kontextfenster.",
                Alternativen: [
                    "**Flache Struktur bevorzugen**: Features/Domains auf weniger Verzeichnisebenen zusammenfassen.",
                    "**Namespace-Segmente zusammenfassen**: Verzeichnisse, die nur einen Unterordner enthalten, mit dem Elternverzeichnis zusammenführen.",
                ]
            ),
            ["MaxDirectoryChildren"] = new(
                Warum: "Zu viele Dateien in einem Verzeichnis übersteigen die Darstellbarkeit in einem File-Listing — Agenten wählen aus einem unvollständigen Satz und übersehen Dateien.",
                Alternativen: [
                    "**Unterverzeichnis anlegen**: Verwandte Dateien in einen Unterordner mit sprechendem Namen gruppieren.",
                ]
            ),
            ["MaxMethodOverloads"] = new(
                Warum: "Viele Überladungen mit ähnlicher Semantik erschweren dem Agenten die Auswahl der richtigen Signatur — er wählt die falsche und verursacht subtile Fehler.",
                Alternativen: [
                    "**Optionale Parameter**: `Foo(string x, bool flag = false)` statt zwei Überladungen.",
                    "**Explizite Namen**: `FooWithLogging(...)` statt `Foo(...)` + `Foo(..., ILogger)` — semantisch klar, keine Überladungsauflösung nötig.",
                ]
            ),
            ["MaxConstructorDependencies"] = new(
                Warum: "Konstruktoren mit vielen Abhängigkeiten signalisieren zu viele Verantwortlichkeiten — Agenten übergeben falsche Abhängigkeiten oder erzeugen inkorrekte Objekte.",
                Alternativen: [
                    "**Klasse aufteilen**: Verantwortlichkeiten separieren — jede Teil-Klasse hat weniger Abhängigkeiten.",
                    "**Parameter-Aggregation**: Zusammenhängende Abhängigkeiten in ein Konfigurations-Record bündeln.",
                ]
            ),
            ["BlazorRequireCodeBehind"] = new(
                Warum: "Logik in `@code { }` Blöcken ist für Agenten ohne Razor-Unterstützung unsichtbar — sie modifizieren nur die `.razor.cs`-Datei und übersehen den `@code`-Block.",
                Alternativen: [
                    "**Code-Behind-Datei anlegen**: `@code { ... }` → separate `.razor.cs` partial class verschieben.",
                    "**Suppression** (in `.razor`-Dateien): `@* ainetlinter-disable BlazorRequireCodeBehind *@`",
                ]
            ),
            ["BlazorRequireCssIsolation"] = new(
                Warum: "Inline `<style>`-Tags in `.razor`-Dateien werden vom Agenten oft übersehen und nicht in CSS-Isolation-Dateien migriert — erzeugt Style-Konflikte.",
                Alternativen: [
                    "**CSS-Isolation-Datei anlegen**: `<style>` → `.razor.css` Datei im gleichen Ordner.",
                    "**Suppression** (wenn keine Styles nötig): `@* ainetlinter-disable BlazorRequireCssIsolation *@`",
                ]
            ),
            ["WpfRequireMinimalCodeBehind"] = new(
                Warum: "Umfangreiches Code-Behind in WPF verletzt MVVM. Agenten fügen Logik ins Code-Behind ein, wenn sie den ViewModel nicht finden — kumuliert technische Schulden.",
                Alternativen: [
                    "**ViewModel**: Logik, Commands und Properties in den zugehörigen ViewModel verschieben.",
                    "**EventToCommand-Binding**: Event-Handler durch Command-Bindings ersetzen (MVVM-Infrastruktur des Projekts nutzen).",
                ]
            ),
            ["EnforceMinimalApiAsParameters"] = new(
                Warum: "Minimal-API-Endpunkte ohne Parameter-Binding-Records verleiten Agenten zu inkonsistentem Parameterhandling.",
                Alternativen: [
                    "**Parameter-Record**: Eingabeparameter in einen `record` zusammenfassen — `record CreateFooRequest(string Name, int Count)`.",
                ]
            ),
            ["EnforceResultPatternOverExceptions"] = new(
                Warum: "Exceptions für Business-Logik sind für Agenten schwer zu verfolgen — sie übersehen `throw`-Pfade und schreiben keine Tests für Fehlerfälle.",
                Alternativen: [
                    "**Result-Typ**: Rückgabetyp `Result<T, Error>` oder `OneOf<Success, Error>` (Bibliothek je nach Projekt wählen).",
                    "**Discriminated Union**: Erfolg/Fehler explizit als Typ modellieren — keine Exception für erwartete Fehlerfälle.",
                ]
            ),
            ["EnforceValueObjectContracts"] = new(
                Warum: "Klassen mit `*ValueObject`-Suffix sollten strukturell unveränderlich sein. Agenten fügen sonst mutierbare Properties hinzu und brechen das Invariant.",
                Alternativen: [
                    "**`record`**: `public sealed record PriceValueObject(decimal Amount, string Currency)` — primäres Konstrukt für Value Objects.",
                    "**`readonly struct`**: Für kleine, häufig kopierte Value Objects ohne Vererbungsbedarf.",
                ]
            ),
            ["MaxInheritanceDepth"] = new(
                Warum: "Tiefe Vererbungshierarchien sind für Agenten schwer zu durchdringen — sie sehen nicht alle Basisklassen-Methoden und übersehen Overrides.",
                Alternativen: [
                    "**Komposition statt Vererbung**: Funktionalität per Aggregation einbinden statt abzuleiten.",
                    "**Interface statt Basisklasse**: Vertrag definieren, nicht Implementierung teilen — reduziert Hierarchietiefe.",
                ]
            ),
        };
}
