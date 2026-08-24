# AiNetLinter - Projekt-Roadmap

Diese Roadmap dokumentiert den aktuellen Entwicklungsstand des `AiNetLinter`-Projekts und teilt die Features in logische Epics und Kapitel auf. Sie dient als Arbeitsgrundlage für die schrittweise Implementierung.

---

## Epic 1: Bootstrapping & Infrastruktur

- [x] Initialisierung der Projektstruktur mit `.slnx` (Solution) und `.csproj`
- [x] Einrichtung der globalen AI-Richtlinien (`.agents/rules/AiNetLinterRichtlinien.mdc`)
- [x] Definition der Konfigurationsstruktur (`Config.cs`)
- [x] **Automatischer rules.json-Sync:** Beim Laden via `--config` werden fehlende Optionen mit Standardwerten ergänzt und veraltete Optionen entfernt; Nutzer-Werte bleiben erhalten (`ConfigSyncer`)
- [x] Definition der Fehlermodelle (`RuleViolation.cs`)
- [x] Implementierung des CLI-Einstiegspunkts (`Program.cs`) mit Argument-Parsing
- [x] Setup des xUnit v3 Testprojekts (`AiNetLinter.Tests`) und Integration in die Solution

---

## Epic 2: Core Roslyn Rules Implementation

- [x] **Regel: EnforceSealedClasses** – Zwingt konkrete Klassen zu `sealed`
- [x] **Regel: AllowDynamic** – Verbietet `dynamic` Typisierung
- [x] **Regel: AllowOutParameters** – Verbietet `out`-Parameter
- [x] **Regel: MaxLineCount** – Validiert maximale Zeilenanzahl pro Datei
- [x] **Regel: MaxMethodParameterCount** – Validiert Parameterlimit pro Methode
- [x] **Regel: MaxMethodLineCount** – Validiert maximale Codezeilenanzahl pro Methode (ohne Kommentare/Leerzeilen, Standard: 42)
- [x] **Regel: MaxCyclomaticComplexity** – McCabe-Komplexität über Roslyn analysieren
- [x] **Regel: MaxCognitiveComplexity** – Kognitive Komplexität nach SonarSource-Standard analysieren

---

## Epic 3: Project & Solution Parsing

- [x] Parse moderne `.slnx`-Dateien (XML-basiert), um enthaltene Projekte zu extrahieren
- [x] Parse klassische `.sln`-Dateien, falls vorhanden
- [x] Parse `.csproj`-Dateien, um alle kompilierten `.cs`-Quelldateien zu identifizieren
- [x] Ignorieren von generierten oder transienten Code-Dateien (z. B. `obj/`, `bin/`, `.vs/`)

---

## Epic 4: CLI Interface & AI-Actionable Output

- [x] Ausgangs-Exit-Codes definieren (0 = Erfolg, 1 = Regelbrüche, >1 = Fatale Fehler)
- [x] Strukturierte, maschinenlesbare AI-Fehlermeldungen auf `stdout` ausgeben
- [x] Unterstützung für Verbose-Logging (`--verbose` oder `-v`)
- [x] Datum + Zeit im Header von jeglichem Text-Output zur Nachverfolgbarkeit

---

## Epic 5: Self-Testing CLI Integration (Dogfooding)

- [x] Erstellung einer zentralen `rules.json` für den Eigenlauf des Tools
- [x] Implementierung von Integrationstests, die den kompilierten Linter (`AiNetLinter.dll` / `.exe`) auf die eigene Codebase loslassen
- [x] Automatisches Einbinden des Linters in den `dotnet test` Build-Prozess (Integrationstest führt CLI auf gesamtem src/ Ordner aus)

---

## Epic 6: Future Capabilities (Roadmap)

- [x] **Namespace-Kopplung (Vertical Slices):** Verbot von unerlaubten slice-übergreifenden Abhängigkeiten (mittels ForbiddenNamespaceDependencies)
- [x] **Maschinenlesbare Verträge (Contracts):** Unterstützung strukturierter Typ-Verträge (durch Prüfung von \*ValueObject Suffix)
- [x] **Traceability-Graphen (Entfernt):** Analyse von Seiteneffekten bei Code-Änderungen (Generierung von Mermaid-Projekt-Abhängigkeitsgraphen)
- [x] **Static Test Sentinel:** Statische Test-Präsenzprüfung für hochrelevante Codeabschnitte
- [x] **Granularer Bypass-Modus für Suppressions (`--ignore-suppressions`):** umgesetzt, siehe Epic 12.

---

## Epic 7: Tokenizer- & Semantik-Optimierung (BPE & LSP)

- [x] **PascalCase-Validierung:** Statische Typprüfung, dass alle Klassen, Structs, Records, Interfaces, Methoden und Properties strikt in PascalCase geschrieben sind (optimiert die Token-Zerlegung von Byte-Pair-Encoding Tokenizern).
- [x] **XML-Doc-Obligatorium für Public APIs:** Zwingende Präsenz von `/// <summary>` Dokumentationen an allen öffentlichen Klassen und Methoden (damit AI-Agenten die Absicht über Language Server Protocol / LSP direkt im Kontext verstehen).
- [x] **Erkennung generischer Bezeichner:** Erkennung und Flagging von nicht-semantischen Parameternamen (z. B. `data`, `temp`, `obj`, `val`) in öffentlichen Methodenschnittstellen.

---

## Epic 8: Agent-Resilienz & Fehleranalyse (Compiler-Leitplanken)

- [x] **Nullable-Präsenzprüfung:** Überprüfung, ob `#nullable enable` in jeder Datei deklariert ist oder global erzwungen wird, um LLM-bedingte NullReferenceExceptions zu minimieren.
- [x] **Vermeidung stummer Catch-Blöcke (Silent Swallowing):** Warnung bei leeren `catch`-Blöcken oder bei Blocks, die Exceptions ohne Logging/Rethrow verschlucken (dies bricht die Fehlerkorrektur des agentischen Loops).
- [x] **Limitierung der Vererbungstiefe (MaxInheritanceDepth):** Begrenzung der Vererbungshierarchie (z. B. max. Tiefe von 2), um "Context Dispersion" zu verhindern (LLMs müssen nicht über mehrere Quelldateien hinweg vererbte Member rekonstruieren).

---

## Epic 9: Architektur-Bereinigung & Fehlerbehebung (Critical Architecture Updates)

- [x] **ClassMap Namespace-Awareness:** Erweitere die Klassen- und Vererbungserkennung so, dass Klassen anhand ihres vollqualifizierten Namens (Namespace + Klassenname) eindeutig identifiziert werden. Löst den Absturz-Bug (`Duplicate Key Exception` im `ToDictionary`) bei gleichnamigen Klassen in unterschiedlichen Namespaces auf.
- [x] **Konfigurierbarer Sentinel-Schwellenwert:** Mache den Kognitiven Komplexitäts-Schwellenwert (bisher hartcodiert auf `3`) in der `MetricsConfig` (z. B. `MinCognitiveComplexityForTest`) konfigurierbar, statt ihn fest im Code zu verankern.
- [x] **Rekursive globale Nullable-Erkennung:** Erweitere die Erkennung globaler Nullable-Einstellungen so, dass sie rekursiv nach oben in `Directory.Build.props` und `.csproj` Dateien sucht und nicht beim ersten Fund einer leeren csproj die Suche abbricht.
- [x] **Laufzeit-Fehlerbehandlung für Dateizugriffe:** Reiche IO-Exceptions beim Lesen von Quellcodedateien als fatalen CLI-Fehler nach oben (Exit-Code `2` / stderr) anstatt sie als Regelverstöße im Ergebnisbericht unterzubringen.

### Architektur-Pflege (Code-Audit 2026-06)

- [x] **DRY-Fix:** `LoadRulesJsonContent` in `ConfigLoader` zentralisiert (Plan 01)
- [x] **Namespace-Konsistenz:** `DisableAllDetector` nach `AiNetLinter.Suppression` verschoben (Plan 02)
- [x] **Namespace-Konsistenz:** `UiFileSeparationChecker` nach `AiNetLinter.Core.Checkers` (Plan 03)
- [x] **Core-Entschlackung:** `AiNetLinter.Generators`-Namespace extrahiert (Plan 04)
- [x] **ViolationDescription-Record:** `ReportViolation`-Overloads mit `ViolationDescription` vereinfacht (Plan R04)

---

## Epic 10: Erweiterte Analyse & CI/CD-Integration (Extensions & Best Practices)

- [x] **Syntaktische Typ-Analyse für verbotene Namespace-Kopplungen:** Durchsuche den Quellcode nach der Verwendung von vollqualifizierten Typnamen (in `QualifiedNameSyntax` und `MemberAccessExpressionSyntax`), die gegen die konfigurierten Namespace-Kopplungen verstoßen (auch wenn kein `using`-Statement verwendet wird).
- [x] **Test Sentinel mit Inhalts-Validierung:** Testklassen werden nur gezählt, wenn sie Testmethoden mit `[Fact]`/`[Theory]`/`[Test]`/`[TestMethod]` enthalten.

---

## Epic 11: Roslyn Workspace & Semantische Analyse (Roslyn Workspace Refactoring)

- [x] **Integration von MSBuildWorkspace & MSBuildLocator:** Binde die benötigten NuGet-Pakete ein und initialisiere den MSBuildWorkspace zur vollständigen Evaluierung der Solution-Struktur (.sln / .slnx).
- [x] **Umstellung auf Solution-weites Laden:** Ersetze das textbasierte Parsen einzelner Dateien durch das Laden der Solution in den Speicher und das Abfragen der `Compilation` und des `SemanticModel` pro Dokument.
- [x] **Semantische Vererbungstiefen-Prüfung:** Nutze `INamedTypeSymbol.BaseType` des semantischen Modells, um die exakte Vererbungshierarchie über Projektgrenzen hinweg ohne textbasierte Heuristiken zu ermitteln.
- [x] **Semantische Nullable-Prüfung:** Nutze `compilation.Options.NullableContextOptions`, um die Nullability-Einstellungen direkt vom Compiler auszuwerten (inkl. Directory.Build.props und konditionaler Flags).
- [x] **Semantische Namespace-Kopplungs-Prüfung:** Analysiere Symbol-Referenzen über `SemanticModel.GetSymbolInfo`, um unerlaubte Namespace-Abhängigkeiten zuverlässig auf Typ- und Member-Ebene zu erkennen.
- [x] **Bereinigung von veraltetem Code:** Entferne obsolete textbasierte Heuristiken (manuelles Csproj-Parsing, manuelle Dateisuchen und String-basierte Namespace-Suchen).

---

## Epic 12: Audit Remediation & CLI Robustness

- [x] **Semantische Testerkennung:** Nutze `SemanticModel.GetSymbolInfo(attr).Symbol` in `LinterAnalyzer.cs`, um echten Namespace/Typ von Test-Attributen (`Xunit`, `NUnit`, `Microsoft.VisualStudio.TestTools.UnitTesting`) zu prüfen statt unzuverlässiger Textsuche.
- [x] **Consolidated Syntax Walk (Performance):** Führe `ClassCollector` und `LinterAnalyzer` zusammen, um Klasseninfos direkt beim ersten Syntax-Walk zu erheben und redundantes Syntax-Walking zu verhindern. Lösche die obsolete Klasse `ClassCollector.cs`.
- [x] **System.CommandLine Integration:** Ersetze das manuelle CLI-Argument-Parsing durch die offizielle `System.CommandLine`-Bibliothek zur Parameter- und Flag-Validierung.
- [x] **Semantische dynamic-Erkennung:** Überprüfe `dynamic` über das `SemanticModel` (`TypeKind.Dynamic`), um Fehlermeldungen bei lokalen Variablen namens `dynamic` zu vermeiden.
- [x] **Unterstützung für ainetlinter-disable:** Erlaube das Unterdrücken von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]` oder dateiweit.
- [x] **Dateiweites Disable-all (`// ainetlinter-disable all`):** Deaktiviert alle Regeln für eine gesamte Quelldatei.
- [x] **CLI Bulk-Suppression (`--add-disable-all`):** Fügt den Disable-all-Kommentar nur in Dateien mit Audit-Verstößen ein.
- [x] **CLI Bulk-Entfernung (`--remove-disable-all`):** Entfernt exakte `// ainetlinter-disable all`-Zeilen per Regex aus allen `.cs`-Dateien unter `--path`.
- [x] **CLI Granularer Suppression-Bypass (`--ignore-suppressions`):** Dynamisches Umgehen von Code-Unterdrückungen (`disable all` und inline `disable [Rule]`) beim Linter-Lauf mit granularen Sprachfiltern (`all`, `cs`/`c#`, `razor`, `js`, `css`).
- [x] **Projektbasierte Test-Dateierkennung:** Bestimme Testprojekte dynamisch durch Analyse ihrer referenzierten Test-Assemblies (`xunit`, `nunit` etc.) im MSBuild-Projekt, um fragile Dateipfad-Heuristiken abzulösen.
- [x] **LLM-optimierte CLI-Textausgabe:** Kompakte, token-effiziente Standardausgabe mit relativem Pfad (Basis `--path`), sortierten Einzeilern, LLM-Anweisungsheader und relativem SARIF-URI statt absoluter `file://`-Pfade.
- [x] **Parallele Dokument-Analyse & MSBuild Design-Time-Properties:** `MSBuildWorkspace` mit `DesignTimeBuild`/`SkipCompilerExecution` für schnelleres Laden; parallele Roslyn-Analyse aller `.cs`-Dokumente mit thread-sicheren Sammlungen (`ConcurrentBag`/`ConcurrentDictionary`).
- [x] **CLI-Summary (by file / by rule):** Parsebare Summary-Segmente oben in der Textausgabe für schnelles LLM-Triage-Parsing — Fehleranzahl pro Datei und pro Regel, gefolgt von der unveränderten Detail-Liste unter `## Violations`.

---

## GitHub Release

- [x] **Release-Infrastruktur & ZIP-Archive reparieren:**
  - **Ziel:** Nur noch 3 Plattform-ZIP-Ablagen (Windows, Linux, macOS) im Release bereitstellen. Keine losen Binärdateien oder `rules.json` daneben.
  - **Status:** Abgeschlossen. Der Release-Prozess über GitHub Actions erzeugt 3 Plattform-ZIP-Archive inkl. BuildHost-DLLs.

---

## Epic 13: Scope-Verwirrung & Immutability (Scope- & Zustands-Leitplanken)

_Hinweis: Alle Regeln müssen über die `rules.json` konfigurierbar sein (Aktivierung und Schwellenwerte)._

- [x] **Variable Shadowing (Verdeckung) verbieten:**
  - Statische Prüfung (über `SemanticModel` / `SyntaxTree`), ob lokale Variablen oder Parameter Felder/Eigenschaften der Klasse oder Parameter äußerer Methoden verdecken (`Shadowing`).
  - Fehlermeldung bei Verstößen, da Shadowing die Variablenverfolgung bei LLMs stört.
  - Konfigurierbar unter `GlobalConfig` (z. B. `EnforceNoVariableShadowing`).
- [x] **MaxMethodOverloads limitieren:**
  - Methode overload count analysieren. Warnung, wenn eine Klasse mehr als `MaxMethodOverloads` (Standard: 2) gleichnamige Methoden deklariert.
  - LLMs scheitern oft bei der Zuordnung feiner Typunterschiede bei übermäßigem Overloading.
  - Konfigurierbar unter `MetricsConfig` (z. B. `MaxMethodOverloads`).
- [x] **Verbot von Parameter-Reassignment (Readonly Parameter):**
  - Analysiere, ob Parameter innerhalb von Methodenkörpern überschrieben werden (z. B. `amount = amount * 2`).
  - Parameter müssen implizit als `readonly` behandelt werden, da Reassignment den linearen Tokenizer-Fluss stört.
  - Konfigurierbar unter `GlobalConfig` (z. B. `EnforceReadonlyParameters`).
- [x] **Immutability-Check für Klassenfelder:**
  - Warnung, wenn `private` Felder nicht als `readonly` deklariert sind, obwohl sie nur im Konstruktor zugewiesen werden. Minimiert veränderlichen Zustand für sicherere KI-Edits.
  - Konfigurierbar unter `GlobalConfig` (z. B. `EnforceReadonlyFields`).

---

## Epic 14: Topologische Kopplung & Magic Values (Kopplung & Semantik)

_Hinweis: Alle Regeln müssen über die `rules.json` konfigurierbar sein._

- [x] **Efferent Coupling limitieren (Constructor Dependencies):**
  - Überprüfe die Anzahl der Konstruktor-Parameter (injected Dependencies). Warnung bei Überschreitung von `MaxConstructorDependencies` (Standard: 5).
  - Zu viele Abhängigkeiten verletzen das Single Responsibility Principle und vergrößern das RAG-Kontextfenster.
  - Konfigurierbar unter `MetricsConfig` (z. B. `MaxConstructorDependencies`).
- [ ] ~~**Vermeidung von Magic Values (Numbers & Strings):**~~ **Entfernt am 2026-06-19** (Commit `764281a`, Begründung laut Commit-Message: *"Regel greift kein konkretes LLM-Failure-Pattern"*). `MagicValuesChecker`, `MagicValuesConfig`, `MagicValuesConfigOverride` sowie alle Konfigurationsfelder (`EnforceNoMagicValues`) wurden vollständig entfernt, inkl. Tests, Docs und `rules.json`-Einträgen. Ursprünglich geplant: literale Werte (`status == 4`, `role == "Admin"`) direkt in Methodenkörpern finden, mit Ausnahmen für `0`/`1`/`-1`/leere Strings, und stattdessen Konstanten/`static readonly`/`enum`s erzwingen. Ein gezielteres On-Demand-Audit-Tool (MCP-Tool statt Build-Regel, mit fachlicher Klassifizierung und Security-Fokus) ist im aktuellen `find_magic_values`-Tool umgesetzt.

---

## Epic 16: Baseline Ratchet (Inkrementelle Migration)

- [x] **Checksum-basierte Baseline:** `--create-baseline` erzeugt JSON mit SHA-256-Checksummen aller analysierbaren `.cs`- sowie Web-Dateien (CSS, JS, Razor)
- [x] **Baseline-Filter im Audit:** `--baseline` unterdrückt Verstöße in unveränderten Dateien (Checksum-Vergleich)
- [x] **Automatisches Baseline-Update:** Bei erkannter Checksum-Abweichung wird die gesamte Baseline-Datei neu geschrieben (weicher Ratchet)
- [x] **SourceFileCatalog:** Gemeinsame Solution-Enumeration für Linter und Baseline ohne Git-Abhängigkeit

---

## Epic 17: Agent-Workflow Features (SAN-Refactoring)

- [x] **Try*/Is*-Ausnahme für out-Parameter:** `AllowTryPatternOutParameters` erlaubt `out` in `bool Try*`- und `bool Is*`-Methoden (idiomatisches C#)
- [x] **Error-String-Try\*-Muster für out-Parameter:** `string? TryXxx(out T)` (null = Erfolg, non-null = Fehlermeldung) wird von `AllowTryPatternOutParameters` als gleichwertiges Try\*-Muster erkannt und nicht als Violation gemeldet
- [x] **Guidance im Text-Output:** Detail-Zeilen mit `→ {Guidance}` für LLM-Refactor-Hints
- [x] **Smarter Static Test Sentinel:** Flexible Klassenname-Patterns, `typeof`/`nameof`-Referenzen und `// @covers`-Kommentare
- [x] **OCE-Catch-Allowlist:** `AllowCancellationShutdownCatch` für Host-Shutdown mit `OperationCanceledException` + Filter
- [x] **Erweiterbare Silent-Catch-Allowlist:** `AllowedSilentCatchExceptionTypes` für projektspezifische Exception-Typen (z. B. Blazor `JSDisconnectedException`)
- [x] **MaxMethodParameterCount Override-Exemption:** `override`- und Interface-Implementierungen ausgenommen (Signatur nicht änderbar)
- [x] **MaxMethodParameterCount Accessibility-Differenzierung:** `MaxMethodParameterCountAllowPrivate` (vollständige Ausnahme) und `MaxMethodParameterCountForNonPublic` (relaxiertes Limit) für `private`/`protected` Methoden
- [x] **Tech-Debt-Report (`--debt-report`):** Parsebarer Report nach Ordnern und wave-ready Kandidaten
- [x] **Wellen-Scope-Filter:** `--wave-ready`, `--only-changed` (mit `--baseline`), `--git-since`
- [x] **Regel-Metadaten (Severity + Intent):** `RuleMetadata` in rules.json, Intent-Spalte in Summary, SARIF level
- [x] **Minimal-API-[AsParameters]-Check:** Opt-in via `EnforceMinimalApiAsParameters`
- [x] **Partial-Class-Aggregation:** `AggregatePartialClassLineCount` summiert Zeilen über partial-Teile
- [x] **Erweiterte kognitive Guidance:** Konkrete Extract-Method-Hints bei starker Komplexitätsüberschreitung

---

## Epic 15: Kontrollfluss-Brüche (Control Flow Resilience)

_Hinweis: Konfigurierbar über die `rules.json`._

- [x] **Exceptions for Control Flow verbieten:**
  - Warnung bei der Verwendung von `throw` in Methoden, die keine Konstruktoren oder explizite Validierungs-Guards (z. B. Methoden mit Suffix `Guard` oder `Validate`) sind.
  - Erzwinge das Result-Pattern (`Result<T>`) für fachliche Fehlerzustände, da KI-Agenten Kontrollflussbrüche durch Exceptions schwer statisch verfolgen können.
  - Konfigurierbar unter `GlobalConfig` (z. B. `EnforceResultPatternOverExceptions`).

---

## Epic 18: Performance-Optimierungen (Parallelisierung & Caching)

- [x] **Parallele Kompilierung laden:** Parallele Ausführung von `GetCompilationAsync()` über alle Projekte der Solution.
- [x] **Short-Circuiting für Namespace-Checks:** Vermeidung von teuren Roslyn Semantik-Lookups für Identifiers, falls keine Namespace-Kopplungsregeln definiert sind.
- [x] **In-Memory Suppression-Prüfung:** Verwendung der bereits geladenen Roslyn Document Source-Texte im Speicher für die Suppression-Prüfung statt redundanter synchroner Disk-Lesezugriffe.
- [x] **Performance-Profiling & Zeitmessung:** Integriertes Profiling-System zur Erfassung der Ausführungszeiten von Linter-Phasen und Generierung von performance.log & performance.json unter `measurements/` zur Analyse von Flaschenhälsen.
- [x] **TTL-basierte Cache-Bereinigung (`--cache-ttl`):** Globale Bereinigung veralteter Cache-Dateien beim Programmstart anhand von `LastWriteTimeUtc`. Standard: 60 Minuten. Verhindert das stille Akkumulieren von Leichen aus alten Solutions- oder Rules-Kombinationen. `0` = unbegrenzt.

---

## Epic 19: AI-Developer Experience (AI-DX) & Tooling

- [x] **AI-Context-Footprint (Metrik):** Berechnung der transitiven Quellcodezeilen aller Klassenabhängigkeiten. Traversiert die Symbolabhängigkeiten über das semantische Modell und summiert die Zeilenlängen der Quelldateien.
- [x] **Automatisch generiertes Repo-Playbook:** Generierung einer Übersicht über aktive Suppression-Regeln und genutzte Entwurfsmuster in `.agents/rules/playbook.md`. Wertet Suppression-Häufigkeiten und genutzte Syntaxpatterns (z. B. Vorhandensein des Result-Patterns) global aus.
- [x] **Roslyn-basierter CLI Auto-Fixer (`--fix`):** Automatische Behebung einfacher Verstöße (z. B. Hinzufügen von `sealed`, `readonly`, oder XML-Skeletten) direkt über die CLI, via `CodeFixProvider`/`Workspace.TryApplyChanges`.
- [x] **Semantische Diff-Impact-Analyse:** Analyse geänderter Methoden-Signaturen im Git Diff und Auflistung aller betroffenen Call-Sites in anderen Projekten, via `GitChangedFilesResolver` und `SymbolFinder.FindReferencesAsync`.
- [x] **Dynamischer, LLM-orientierter Codegraph (Entfernt):** Generierte einen Software-Abhängigkeitsgraphen im Mermaid-Format aus Typdeklarationen, Basisklassen, Interface-Implementierungen und Feld-/Konstruktor-Abhängigkeiten.
- [x] **Projekt-spezifische Regel-Konfiguration (Project Overrides):** Unterstützung von projekt- oder namensraumspezifischen Regel-Überschreibungen in der `rules.json` (z. B. Deaktivieren von `EnforceSealedClasses` für Testprojekte).
- [x] **`find_magic_values` MCP-Tool (On-Demand-Magic-Value-Audit):** 20. MCP-Tool — Roslyn-basierter On-Demand-Audit über alle `.cs`-Dokumente der Solution, klassifiziert Literale (URLs, Pfade, Connection-Strings, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) mit Ziel-Empfehlungen (`appsettings.json`, `Constants.cs`, `StatusCodes.StatusXXX...`). Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter, `ignoreNumbers`-Erweiterung. Erweiterte Heuristiken (`enum_candidates`/`nameof_candidates`/`localization_candidates`/`security_candidates`, duplizierte `private const`-Erkennung, Suppression via `SyntaxTrivia`, `changedOnly`) sind in einer Folgeversion geplant. Stand 2026-08-14.

---

## Epic 20: AI-Readability & Agentic Resilience Upgrades

- [x] **Regel: EnforceExplicitStateImmutability** – Zwingt Klassen (außer DTOs/Entities) zur Unveränderlichkeit (init Properties, readonly private fields).
- [x] **Fehlerbehandlung: Refine Exception Control Flow** – Erlaubt das Werfen von fatalen/technischen Standard-Exceptions für Fail-Fast-Muster.
- [x] **Token-Hygiene: Refine XML Documentation** – Reduziert XML-Dokumentationspflichten auf Typ-Deklarationen zur Token-Einsparung.
- [x] **Regel: PreventContextDependentOverloads** – Limitierung auf max. 3 Methodenüberladungen und Verbot primitiver Überladungskonflikte.
- [x] **Regel: EnforceNamespaceDirectoryMapping** – Erzwingt exakte Namespace-Ordner-Konformität und begrenzt die Ordnertiefe (MaxDirectoryDepth).
- [x] **Regel: DetectAndBanPhantomDependencies** – Verhindert nicht-referenzierte using-Imports und dynamische Reflection-Lade-APIs.

---

## Epic 21: Consumer Integration & DX Refinements

- [x] **Konfigurations-Erweiterungen:** Support für `ImmutabilityExemptPatterns` (Wildcards) und `AllowedEmptyReads` in `Config`.
- [x] **Immutability Heuristiken:** Roslyn-basierte Erkennung von `IConfiguration`/`IOptions` Bindings und `[JsonSerializable]`.
- [x] **Truncation & Test-Ausnahme:** Guidance-Update mit C#-Beispiel, Berücksichtigung von `AllowedEmptyReads` und Ausnehmen von Test-Fakes (Fake, Mock, Test).
- [x] **Namespace-Abhängigkeiten mit Wildcards:** Glob-Matching für verbotene Namespace-Kopplungen.
- [x] **Auto-Fixer sealed nested classes:** Erweiterung des automatischen Sealing auf private verschachtelte Klassen.
- [x] **Agent-Rules Generator Overhaul:** Vollständiges Rendern aller globalen Schalter, Metadaten-Tags und tabellarische Darstellung der `ProjectOverrides`.
- [x] **Schnellerer Rules-Sync & CLI `--check`:** Direkter Konfigurations-Check und Drift-Erkennung ohne Laden der Solution (Exit 1 bei Abweichung).
- [x] **Detaillierter Footprint & Debug-CLI:** `--footprint <Klassenname>` mit Auswertung der Top-3 Abhängigkeiten zur RAG-Optimierung.
- [x] **Repo-Playbook-Generator Erweiterung:** Frontmatter (`alwaysApply: false`), Migrations-Status, Architektur-Slices und LLM-Prioritäten nach Intent.

---

## Epic 22: UI-Datei-Trennung (Blazor & WPF)

- [x] **Regel: BlazorRequireCodeBehind** – Jede `.razor`-Datei muss eine `.razor.cs`-Begleitdatei (Code-Behind-Partial-Class) haben. Dateisystem-basierter Check (Roslyn sieht `.razor` nicht).
- [x] **Regel: BlazorRequireCssIsolation** – Jede `.razor`-Datei muss eine `.razor.css`-Begleitdatei (CSS-Isolation) haben.
- [x] **Regel: WpfRequireMinimalCodeBehind** – WPF Code-Behind-Klassen (`partial class : Window/UserControl/...`) dürfen nur den Konstruktor mit `InitializeComponent()` enthalten (Roslyn-Check).
- [x] **Suppression:** Razor-Kommentar-Syntax `@* ainetlinter-disable RuleName *@`, Klassen-Ausschlusslisten per Config.
- [x] **Konfigurations-Sektion `UiSeparation`** mit WPF/Blazor-getrennten Optionen, Ausschluss-Listen und Projekt-Override-Support.

---

## Epic 23: Strukturmetriken & API-Surface-Kontrolle

- [x] **Regel: MaxBoolParameterCount** – Maximale Anzahl von `bool`-Parametern pro Methode/Konstruktor (Standard: 1). Bool-Parameter sind an der Call-Site opak (`DoWork(true, false)`); ab Überschreitung: Parameter-Object-Pattern. `MaxBoolParameterCountAllowPrivate` (Standard: `true`) und `MaxBoolParameterCountExemptMethodPrefixes` für projektspezifische Ausnahmen.
- [x] **Regel: MaxDirectoryChildren** – Maximale Anzahl von Einträgen in einem Verzeichnis (Standard: 0 = deaktiviert). Verhindert Flat-Folder-Antipattern (zu viele Dateien auf einer Ebene); Empfehlung: 20–30 für Mittelklasse-Projekte. `MaxDirectoryChildrenExemptNames` für bekannte Ausnahmen wie `Migrations`, `Generated`.
- [x] **Regel: MaxPartialClassFiles** – Maximale Anzahl von `partial`-Deklarationsdateien pro Typ (Standard: 2). Mehr als 2 `partial`-Dateien signalisieren eine zu breite Klasse; Guidance: Unter-Logik in eigenständige `XyzChecker`/`XyzValidator`-Klassen auslagern. `MaxPartialClassFilesExemptTypes` für unvermeidliche Ausnahmen.
- [x] **Regel: MaxPublicMembersPerType** – Maximale Anzahl öffentlicher Member pro Typ (Standard: 15). Begrenzt die API-Surface und reduziert den KI-Kontextaufwand beim Verstehen eines Typs. `MaxPublicMembersPerTypeExemptSuffixes` für Typen mit strukturell großer API-Surface (z. B. `Extensions`, `Mapper`).
- [x] **Regel: MaxSwitchArms** – Maximale Anzahl Arms in einem Switch-Expression bzw. Labels in einem Switch-Statement pro Methode (Standard: 10). Schützt Agenten vor überlangen Entscheidungsstrukturen.

---

## Epic 24: Agent-Readability — Strukturelle Top-Level-Pflicht

_Hinweis: Konfigurierbar über die `rules.json`._

- [x] **Regel: BanPublicNestedTypes** – Verbietet `public` und `internal` nested Typen (Klassen, Structs, Records, Enums) innerhalb anderer Typen. Private nested Typen bleiben standardmäßig erlaubt (Implementierungsdetail). Verbessert die Grep-/File-Listing-Navigation für KI-Agenten und verhindert FQN-Halluzinationen (`PaymentStatus` statt `PaymentProcessor.PaymentStatus`). Konfigurierbar unter `Global.BanPublicNestedTypes` (Default `true`) und `Global.BanPublicNestedTypesAllowPrivate` (Default `true`). Severity: `error`, Intent: `agent-context`.

---

## Epic 25: Compound Suppressions (Kontextabhängige Metrik-Gewichtung)

- [x] **Datenmodell:** Records `MetricCondition` und `CompoundSuppression` in `Config.cs`
- [x] **`MetricsConfig.CompoundSuppressions`:** Property mit 1 aktivem Default für `MaxMethodLineCount`
- [x] **`CompoundSuppressionEvaluator`:** Isolierter Helper mit `Evaluate/FindConfigured/IsActive`
- [x] **Phase 1 — Methoden-Ebene:** `MaxMethodLineCount` und `MaxMethodParameterCount` unterstützen Compound-Suppression mit 3-Szenarien-Guidance
- [x] **Phase 2 — Klassen-Ebene:** `MaxPublicMembersPerType` und `MaxConstructorDependencies` unterstützen Compound-Suppression
- [x] **AgentRules-Generator:** Abschnitt `Compound Suppressions` in `.mdc`-Output (inkl. Severity-Spalte)
- [x] **Docs:** `rationale.md` Abschnitt 11/12, vollständiger `configuration.md`-Abschnitt
- [x] **Tests:** Unit (Evaluator), Integration (Szenarien A–L), Guidance-Text, Config-Sync
- [x] **`SeverityOverride`:** `CompoundSuppression.SeverityOverride` und `RuleViolation.EffectiveSeverity` — Violations in Szenario A (RelaxedLimit überschritten, Bedingungen erfüllt) können auf `"warning"` herabgestuft werden; `HasErrorSeverity` berücksichtigt `EffectiveSeverity`; Formatter zeigt `[warn]`-Tag
- [x] **NullCoalescingInitializer-Classifier:** Optionale Komplexitätsausnahme (`MaxCyclomaticComplexity` / `MaxCognitiveComplexity`) für triviale Initialisierungs- und Merge-Methoden (Null-Coalescing-Initializer).

---

## Epic 26: Async/Await-Sicherheit

- [x] **Regel: BanAsyncVoid** — Verbietet `async void` Methoden und lokale Funktionen (außer Event-Handler).
- [x] **Regel: BanBlockingTaskAccess** — Verbietet `.Wait()`, `.Result` und `.GetAwaiter().GetResult()` auf Tasks.

---

## Epic 27: Feature-Audit 2026-06 — Default-Kalibrierung

Ergebnisse des empirischen Feature-Audits (46 Features bewertet, Cluster A–H, Papers 2018–2026). Die Kalibrierung wurde in der projekteigenen `rules.json` (Dogfooding-Config dieses Repos) umgesetzt. Die eingebauten Code-Defaults in `MetricsConfig.cs`/`GlobalConfig.cs` (das, was ein Nutzer ohne eigene `rules.json` erhält) tragen weiterhin die alten Werte — dieser Teil der Kalibrierung steht noch aus.

- [x] **M01 — MaxLineCount: 500 statt Code-Default 700** — praktische Kalibrierung auf einen gängigen Mittelwert, motiviert durch das allgemeine „Lost in the Middle"-Phänomen (Liu et al. 2023, siehe `rationale.md` Regel 1); kein direkt aus einer Studie abgeleiteter Wert. *(Korrektur 2026-08-13: der zuvor hier genannte Beleg "Ardito et al. 2020" war falsch zugeordnet — dieses Paper ist ein Survey über Maintainability-Metriken/-Tools ohne LOC-Schwellenwert-Aussage und erschien 2020, drei Jahre vor Liu et al. 2023, kann das "Lost in the Middle"-Konzept also chronologisch nicht stützen.)*
- [x] **M06 — MaxInheritanceDepth: 3 statt Code-Default 2** — Wert 2 erzeugt False Positives für ASP.NET-Controller, EF-Entities, xUnit-Testklassen ohne korrekte `InheritanceDepthFrameworkPrefixes`.
- [x] **M07 — MaxMethodOverloads: 5 statt Code-Default 3** — Standard-.NET-Async-Patterns (mit/ohne `CancellationToken`, mit/ohne `IProgress`) erzeugen regulär 3–5 Overloads.
- [x] **M14 — MaxAIContextFootprint: 2500 statt Code-Default 5000** — Empirisch belegter Aufmerksamkeitsabfall bei LLMs ab ~2.000–3.000 transitiven Zeilen (Liu et al. 2023, „Lost in the Middle").
- [x] **M16 — MinCognitiveComplexityForTest: 5 statt Code-Default 3** — Wert 3 erzeugt Warnungs-Flut für triviale Methoden; 5 trifft tatsächlich risikorelevante Komplexität.
- [x] **F09 — EnablePerformanceProfiling: false statt Code-Default true** — Profiling ist eine Entwickler-Debug-Funktion; dauerhaft aktiv erzeugt es `measurements/`-Artefakte im Projektverzeichnis.
- [x] Guidance-Updates — Fehlermeldungen für `BanAsyncVoid`, `BanBlockingTaskAccess`, `MaxInheritanceDepth`, `AIContextFootprint`, `MaxMethodLineCount` und `MaxMethodOverloads` mit Audit-Erkenntnissen ergänzt.

---

## Epic 28: LINQ-Komplexitäts-Kontrolle

- [x] **Regel: MaxLinqChainLength** — Begrenzt die Anzahl verketteter LINQ-Methoden pro Ausdruckskette (Standard: 0 = deaktiviert).

---

## Epic 29: Web-Asset-Linting (CSS / JS / Razor)

Erweitert den Linter um AI-spezifische Regeln fuer Web-Assets (Phase 1: CSS umgesetzt; Phase 2: JS und Phase 3: Razor folgen spaeter). Implementiert die Forschungsdokumente [Research/Extend-Web-Features/00_Overview.md](../Research/Extend-Web-Features/00_Overview.md), [01_CSS_Linting.md](../Research/Extend-Web-Features/01_CSS_Linting.md), [02_JS_Linting.md](../Research/Extend-Web-Features/02_JS_Linting.md) und [03_Razor_Linting.md](../Research/Extend-Web-Features/03_Razor_Linting.md).

### Phase 1 — CSS (umgesetzt)

- [x] **NuGet-Abhaengigkeit:** ExCSS 4.1.4 (MIT-Lizenz) als reines CSS-Parsing-Backend fuer die Selektor-Analyse.
- [x] **Konfigurations-Sektion `Web` / `Web.Css`:** Neue Sektion in `rules.json` (master switch `Web.IsEnabled`, plus `MaxCssLineCount`, `PreferScopedCss`, `PreferScopedCssMinRuleCount`, `MaxCssSelectorComplexity`, `ExemptPaths`).
- [x] **`WebFileCatalog`:** Enumeriert `.css`/`.razor.css`-Dateien aus den Projektverzeichnissen der Solution (parallel zur Roslyn-Solution, kein zweites MSBuild-Laden). Filtert `obj/`, `bin/`, `node_modules/` und CSS-spezifische `ExemptPaths` heraus.
- [x] **`CssAnalyzer`:** AST-Walk ueber ExCSS-Stylesheet. Prueft Zeilenlimit, Selektor-Komplexitaet (Anzahl Segmente, getrennt durch Komma/Whitespace/Combinators) und Scoped-CSS-Empfehlung fuer globale Dateien. Erzeugt `CSS_ParseError` bei Syntax-Fehlern.
- [x] **`WebFileSeparationChecker`:** Post-Analysis-Check (parallel zu `UiFileSeparationChecker`), der die CSS-Regeln ausfuehrt und Per-File Suppression (`/* ainetlinter-disable RuleId */`, `/* ainetlinter-disable all */`) anwendet.
- [x] **Regel-IDs:** `CSS_MaxCssLineCount`, `CSS_PreferScopedCss`, `CSS_MaxCssSelectorComplexity`, `CSS_ParseError` in `LinterRuleIds` und `RuleRegistry.Web.cs` registriert (Severity: error / warning, Intent: agent-context / general).
- [x] **Project-Overrides:** `WebConfigOverride` und `CssConfigOverride` mit `Apply`-Logik; `ProjectConfigResolver.MergeConfig` reicht den Web-Override-Tree durch.
- [x] **Suppression:** Eigener `WebSuppressionDetector` (dateiweit via `ainetlinter-disable all` und regel-spezifisch via `ainetlinter-disable RuleId`).
- [x] **Test-Suite:** `CssAnalyzerTests.cs` (15 Tests, Szenarien A-H aus dem Research-Dokument plus Edge-Cases) und `WebSuppressionDetectorTests.cs` (6 Tests). 21 / 21 gruen.
- [x] **Dogfooding:** AiNetLinter laeuft mit aktivierter Web-Sektion sauber auf der eigenen Codebase durch (keine CSS-Violations im Self-Audit, ExCSS-Integration verifiziert).
- [x] **Dokumentation:** Konfigurationsreferenz in `Docs/configuration.md` um Web-Sektion erweitert; dieser Epic-Eintrag in `ROADMAP.md`.
- [x] **Bugfix CSS_MaxCssSelectorComplexity-Zeilennummer** (Research/04_CSS_SelectorLineNumbers.md): `CheckSelectorComplexity` meldete immer `LineNumber = 1` statt der tatsaechlichen ExCSS-Quellzeile. Fix: Direktes Erstellen der `RuleViolation` mit `rule.StylesheetText?.Range.Start.Line` statt Umweg ueber den gemeinsamen Helper. Nebeneffekt: `CreateViolation` hat wieder ≤4 Parameter (MaxMethodParameterCount-Konformitaet). Testabdeckung: `Analyze_ReportsCorrectLineNumber_ForSelectorComplexityViolation`.

### Phase 2 — JavaScript (umgesetzt)

- [x] **NuGet-Abhaengigkeit:** Esprima 3.0.6 (BSD-3-Clause-Lizenz) als standardkonformer ECMAScript-Parser.
- [x] **Konfigurations-Sektion `Web.Js`:** `MaxJsLineCount` (Standard 150), `EnforceJsModules` (Standard `true`), `ExemptPaths` in `rules.json` synchronisiert.
- [x] **`JsAnalyzer`:** `JavaScriptParser.ParseModule()` zuerst, Fallback auf `ParseScript()`. Eine Datei gilt nur dann als ES6-Modul, wenn `ParseModule` gelingt UND der Body mindestens eine `Import`-/`Export`-Deklaration enthaelt (Esprima 3.x parst Skript-Code sonst ebenfalls als Modul). Prueft `JS_MaxJsLineCount`, `JS_EnforceJsModules` (fehlende `export`-Statements UND `window.xyz = ...`-Zuweisungen in Modulen) und `JS_SyntaxError`.
- [x] **Regel-IDs:** `JS_MaxJsLineCount`, `JS_EnforceJsModules`, `JS_SyntaxError` in `LinterRuleIds` und `RuleRegistry.Web.cs` registriert (Severity: error / error / error, Intent: agent-context / agent-context / general).
- [x] **Project-Overrides:** `JsConfigOverride` mit `MaxJsLineCount`, `EnforceJsModules`, `ExemptPaths`; `ProjectConfigResolver.MergeConfig` reicht den `Js`-Override-Tree durch.
- [x] **WebFileCatalog:** Neuer Input-Record `WebFileDiscoveryRequest` buendelt `FileFilters`, `CssExemptPaths` und `JsExemptPaths` (Reduzierung der Parameter-Anzahl auf <=4 fuer `Collect()`).
- [x] **WebFileSeparationChecker:** Splittet CSS- und JS-Analyse in eigene Helper-Methoden (`AnalyzeCssEntries` / `AnalyzeJsEntries`); gemeinsame Per-File-Verarbeitung in `AnalyzeSingleFile` (Cognitive Complexity von 22 auf ~6 reduziert).
- [x] **Test-Suite:** `JsAnalyzerTests.cs` mit 20 Tests (Szenarien A-H aus dem Research-Dokument plus zusaetzliche Edge-Cases wie `globalThis`-Zuweisung, `this`-Zuweisung, `window.alert()`-Aufruf, mehrere Window-Pollutions, leerer Content, Zeilennummer bei Syntax-Fehlern). Alle Tests gruen.
- [x] **Dogfooding:** AiNetLinter laeuft mit aktivierter JS-Sektion sauber auf der eigenen Codebase durch (Integration-Tests bestanden, Esprima 3.0.6 API verifiziert, eigene Code-Regeln eingehalten: `EnforceNoSilentCatch`, `MaxMethodParameterCount`, `MaxCognitiveComplexity`).
- [x] **Dokumentation:** Konfigurationsreferenz in `Docs/configuration.md` um JS-Sektion erweitert; dieser Epic-Eintrag in `ROADMAP.md`.

### Phase 3 — Razor (umgesetzt)

- [x] **NuGet-Abhaengigkeit:** Keine (gestrichen — textbasierter Ansatz gewaehlt. Da die Regeln auf einfachem Pattern-Counting wie Block-Anzahl, Verschachtelung und Attributen basieren, ist kein voller AST-Parser noetig. Vermeidet NuGet-Versionierungs- und BuildHost-Komplexitaeten).
- [x] **Konfigurations-Sektion `Web.Razor`:** `MaxRazorLineCount`, `MaxRazorCodeBlockLines`, `BanInlineEventLambdas`, `MaxMarkupNestingDepth`, `MaxControlFlowBlocks`, `MaxForeachNestingDepth`, `MaxComponentParameterCount`, `BanInlineTernaryInAttributes` in `rules.json` integriert und per `WebConfig` unterstuetzt.
- [x] **`RazorAnalyzer`:** Textbasierter Analyzer, der Razor-Markup effizient auf Dateigroesse, HTML-Verschachtelungstiefe, Event-Lambdas, Control-Flow-Komplexitaet (Schleifen und Verzweigungen) sowie Inline-Ternaries in Attributen scannt.
- [x] **Regel-IDs:** Die acht Regeln (`RAZOR_MaxRazorLineCount`, `RAZOR_MaxRazorCodeBlockLines`, `RAZOR_MaxMarkupNestingDepth`, `RAZOR_BanInlineEventLambdas`, `RAZOR_MaxControlFlowBlocks`, `RAZOR_MaxForeachNestingDepth`, `RAZOR_MaxComponentParameterCount`, `RAZOR_BanInlineTernaryInAttributes`) sind in `LinterRuleIds` deklariert und in der Rule-Registry registriert.
- [x] **Project-Overrides:** Volle Unterstuetzung fuer Project-Overrides (z. B. Deaktivierung der Razor-Regeln fuer Testprojekte via `ProjectOverrides`).
- [x] **Test-Suite:** 33 Unit-Tests in `RazorAnalyzerTests.cs` und `RazorAnalyzerTests.Extended.cs`. Coverage-Bericht (sofern verfügbar) verweist auf den geprüften Anteil.
- [x] **Dogfooding:** CLI-Integrationstests auf der eigenen Codebase mit aktivierter Razor-Sektion — Exit-Code 0, keine neuen Verstöße in der Vergleichsbasis.
- [x] **Dokumentation:** Vollstaendige Dokumentation der Regeln, Konfigurationen und Suppressions in `Docs/configuration.md` und `README.md`.

*Hinweis zum Go/No-Go-Kriterium:* Das Risiko bezueglich Zeilennummern-Uebersetzung entfaellt beim textbasierten Parser, da dieser direkt auf den Original-Dateizeilen arbeitet und Zeilennummern praezise bestimmt.

---

## Epic 30: Codebase-Landkarten (`--map`) — ENTFERNT

> **Entfernt am 2026-08-11** (ersatzlos, siehe M8 in `tasks/features/05-roadmap.md`). Die CLI-Exposition (`--map vocabulary`, `--map structure`, `--map hotspots`, `--map skeleton`) sowie `VocabularyMapBuilder` und `StructureMapBuilder` wurden entfernt, da die MCP-Tools (`get_hotspots`, `get_file_skeleton` u. a.) den gleichen Nutzen strukturierter bieten. `HotspotMapBuilder` und `SkeletonMapBuilder` bestehen intern weiter, da die MCP-Tools sie referenzieren.
>
> Ursprünglich ergänzte dieses Epic vier Discovery-Befehle für strukturierte Markdown-Ausgaben (Vokabular-Gruppierung nach Typ-Suffix, Verzeichnisbaum mit Dateigrößen, Hotspot-Fokusansicht nahe am `MaxLineCount`-Limit, semantisches Code-Skelett für LLM-Audits).

---

## Epic 31: Eval-Audit-Prompt-Feature (`--eval`) — ENTFERNT

> **Entfernt am 2026-08-11** (ersatzlos, siehe M8 in `tasks/features/05-roadmap.md`). `--eval`, `--list-evals`, `--spec` sowie der komplette `Evals`-Namespace (`EvalDefinition`, `EvalRegistry`, `SpecLoader`, `EvalAssembler`, `EvalCommand`, `ListEvalsCommand`) und die Templates unter `Docs/Evals/` wurden entfernt.
>
> Ursprünglich assemblierte dieses Feature vollständige LLM-Audit-Prompts aus eingebetteten Templates, Spezifikations-Quellen und frisch generierter Codebase-Evidenz (naming-drift, architecture-intent) inkl. XML-Spec-Isolation, Task-First-Ordering und Token-Budget-Warnung.

---

## Epic 32: Globales Projekt- & Namespace-Filtering

Einführung von globalen Filter-Parametern zur Eingrenzung des Analyse-Scopes bei großen Software-Systemen.

- [x] **CLI-Optionen:** Integration der Parameter `--project`, `--exclude-project`, `--namespace`, `--exclude-namespace`, `--exclude-tests`, `--tests-only` und `--public-only` in die CLI-Infrastruktur (`LinterArgs`, `CliOptionFactory`, `CliOptions`, `CliCommandBuilder`, `Program`).
- [x] **Projekt- und Testfilterung:** Dynamische Filterung der Projekte in `SourceFileCatalog` und `LinterEngine` bei der Document-Sammlung zur Optimierung von Performance und CI-Zeiten.
- [x] **Namespace-Filterung:** Dynamischer Ausschluss von C#-Typdeklarationen in Walks & Checks (`LinterAnalyzer`, `SkeletonSyntaxWalker`), um Kontext-Überlastung bei LLMs zu verhindern.
- [x] **Sichtbarkeits-Filterung:** Filterung nicht-öffentlicher Member bei der Skeleton-Map-Generierung (`--public-only`).
- [x] **Dokumentation:** Aktualisierung der CLI-Tabellen und Hinzufügen von Scope-Filtering Handbüchern in `agent-api.md`, `configuration.md`, `README.md` und `ROADMAP.md`.
## Epic 33: Bedingte Baseline-Dokumentation in Agent-Rules (`--sync-agent-rules`)

Erweitert die generierten `.agents/rules/AiNetLinter.mdc`-Dateien um eine projekt-agnostische Erklärung der Baseline-Mechanik (`--create-baseline`), wenn im Zielprojekt eine Baseline verwendet wird.

- [x] **Dynamische Erkennung (`DetectBaselineUsage`):** Automatische Prüfung in `AgentRulesGenerator`, ob im Workspace eine Baseline-Datei (z. B. `baseline.json`) existiert oder `--baseline` per CLI übergeben wurde.
- [x] **Bedingter Abschnitt (`## Baseline-Mechanik`):** Generierung der Erklärungen zu Zweck, `--create-baseline`-Aktualisierung, Verbot von manuellem Editieren und Projekt-Integration nur bei aktiver Baseline-Nutzung.
- [x] **Test-Suite:** Unit-Tests in `SyncAgentRulesCommandTests.cs` zur Validierung der Erkennung und der bedingten Abschnitte.
- [x] **Dokumentation:** Aktualisierung von `configuration.md`, `agent-api.md`, `README.md` und `ROADMAP.md`.

---

## MCP-Codegraph-Server (EPIC-01..08)

Seit 2026-08 schrittweise aufgebauter stdio-basierter MCP-Server, der die Roslyn-basierte Solution-Analyse als granular abfragbare Tools für AI-Coding-Agenten bereitstellt (historischer Stand nach EPIC-08: 13 Tools). Diese EPICs sind **separat** von den oben gelisteten Epics 1-33 zu lesen — sie beziehen sich auf den MCP-Server-Modus (`ainetlinter --mcp-server`), nicht auf den CLI-Batch-Modus. EPIC-01 bis EPIC-07 wurden mit dem damaligen Stand von 9 Tools umgesetzt; EPIC-08 erweiterte den Symbolgraphen um `get_symbol_body` sowie `depth`/DI-Hinweis-Erweiterungen, EPIC-09 um das System-Log-Call-Logging. Vollständige, aktuelle Tool-Referenz: [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).

### EPIC-A — Projektregistry und transportneutrales Multi-Solution-Routing (umgesetzt am 2026-08-24)

- [x] Eine `ainetlinter.project.json` mit den Pflichtfeldern `solution` und
  `rules` bindet beide Pfade relativ zur Definitionsdatei; MCP-Registrierungen
  verwenden nur `command` und `--mcp-server`.
- [x] Die MCP-Tools und die Overview-Resource adressieren Projekte über einen
  absoluten `projectRoot` und nutzen die projektbezogene Registry mit Lease-,
  Load- und Eviction-Verträgen.
- [x] Die Overview ist als Resource-Template
  `ainetlinter://overview{?projectRoot}` registriert. Die C#-Live-Teststrecke
  prüft Discovery sowie den Read der URL-kodierten Repository-URI und bestätigt
  einen `text/markdown`-Snapshot mit Root-, Solution- und Regelstatus.
- [x] Der Abschlussnachweis einschließlich Audit-Triage, read-only Prüfung der
  Repo-/Hermes-Registrierungen und Entscheidungsregister steht in
  `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-008/step-result.md`.

### EPIC-B — Geteilter Daemon mit ThinClient (umgesetzt am 2026-08-24)

- [x] Interner `--daemon-start`-Pfad mit Named-Pipe-Akzeptanz, Pipe-Level-
  Handshake, geteilter Projektregistry und einer MCP-SDK-Session je Verbindung;
  `--mcp-server` verbindet sich über den ThinClient zuerst und startet den Host
  bei fehlendem Endpunkt detached.
- [x] Idle-Exit mit injizierbarer Zeitquelle; aktive Verbindungen, Loads und
  Warmups verhindern den Exit. Standardwert: 10 Minuten.
- [x] Debounced MRU-State unter `%LOCALAPPDATA%` mit tolerantem Laden und
  maximal zwei parallelen Warmups; opaker Stdio-Pump, begrenzter Readiness-/
  Replay-Retry, ThinClient-Parent-Reaper und `AINETLINTER_NO_DAEMON=1` sind
  aktiv. Health weist Daemon-Modus, connectionId, PID, Uptime, Keys und Version
  aus; der Daemon selbst bleibt parent-ungebunden.

### Abgeschlossen

- [x] **EPIC-01 — CLI-Flag:** `--mcp-server` als neuer Server-Start, stdio-Transport, JSON-RPC-Handshake.
- [x] **EPIC-02 — Resident-Server:** Solution wird einmal via `MSBuildWorkspace` geladen und über die Prozesslaufzeit resident gehalten; Staleness-Invalidierung per Datei-`mtime` + SHA-256-Hash mit inkrementellem `WithDocumentText` (kein Komplett-Reload).
- [x] **EPIC-03 — 5/9 Symbolgraph-Tools:** `find_symbol`, `find_references`, `get_impact`, `get_type_hierarchy`, `get_file_skeleton`.
- [x] **EPIC-04 — 4/4 Struktur-/Qualitäts-Tools:** `get_index_scope`, `get_hotspots`, `get_violations`, `search_pattern` (alle reviewt, approved).
- [x] **EPIC-05 — Scope-Kommunikation + Miss-Hint:** `McpServerOptionsFactory.ServerInstructions` sendet den C#-only-Scope zentral beim `initialize`-Handshake; `find_symbol` liefert bei 0 C#-Treffern eine trunkierte Datei-Liste der Nicht-C#-Treffer als Fallback-Hinweis auf `search_pattern`.
- [x] **EPIC-06 — Fehlerbehandlung:** 8 der damals 9 Tools prependieren einen aggregierten Compile-Fehler-Warnhinweis; `get_file_skeleton` nutzt einen datei-spezifischen Warnhinweis; nicht-ladbare Solution führt zu Server-Start mit `[WARN]` und Tool-Calls liefern `SOLUTION_NOT_LOADED` statt Crash; Defensiv-Wrapper fangen unerwartete Roslyn-Exceptions ab.
- [x] **EPIC-07 — Test-Infrastruktur:** 9 neue Test-Klassen + Erweiterung der `McpLiveRepositoryTests`/`McpTestClient`-Harness + neue Fixtures (`CompileErrorMiniFixture`, `McpLiveRepositoryFixture`, u. a.); Volllauf 1161/1161 grün.
- [x] **EPIC-08 — Doku & Symbolgraph-Erweiterungen:** Sektion „MCP-Server-Modus" in `agent-api.md`, „MCP-Server registrieren" in `integration.md` inkl. Tool-vs-`rg`-Empfehlung, README-Hinweis; verifiziert durch `McpDocumentationSmokeTests`. Zusätzlich: `get_symbol_body` mit stabilen Symbol-IDs, `depth`-Parameter für `find_references`/`get_impact`, DI-Registrierungs-Hinweis in `get_type_hierarchy` (siehe „Nächste Phase" unten für Details).
- [x] **EPIC-10 — `get_call_tree` (echter Baum, ASCII/Mermaid): umgesetzt** — fuenftes Symbolgraph-Tool (`SymbolGraphToolRegistrations`), Caller-Tree-Traversierung ueber `CallGraphTraversal.BuildTreeAsync` (eigene Grenzwerte: depth hard cap 5, Knoten hard cap 250, `topN`-Fan-Out-Kappung pro Ebene), Ausgabe als ASCII-Baum (`MetricsTreeRenderer`/`MetricsTreeNode` aus `metrics_tree` wiederverwendet statt eines dritten ASCII-Renderers) oder Mermaid-`flowchart TD` (neuer `CallTreeMermaidRenderer`). Revidiert die in `02-ainetlinter-mcp-current.md` dokumentierte fruehere Konzept-Entscheidung ("bewusst kein `get_call_tree`").
- [x] **EPIC-10-Erweiterung — `direction` fuer `get_call_tree`:** `incoming` bleibt der Default; `outgoing` traversiert InvocationExpressions, ObjectCreation und MemberAccess per SemanticModel transitiv, `both` liefert beide Richtungen abwechselnd innerhalb des Fan-Outs. ASCII/Mermaid, `topN` und der 250-Knoten-Hardcap gelten fuer alle Richtungen; ungueltige Werte liefern recoverable `INVALID_ARGUMENT`.
- [x] **EPIC-11 — MCP-Server-Lebenszyklus:** Parent-Prozess-Watchdog mit automatischer PID-Ermittlung (Windows `NtQueryInformationProcess`, Linux `/proc`, macOS `getppid()`), optionaler CLI-Option `--parent-pid <pid>`, CancellationToken-Verknüpfung und Exit-Code `0` bei Parent-Exit. Fast-Tests für Erkennung/Watchdog sowie ein E2E-Test für die Prozessbeendigung sichern das Verhalten ab.

### Nächste Phase — P0/P1-Rest-Erweiterungen (Konzept Z. 207-324)

Aus dem Konzept übernommene Erweiterungen, die nach EPIC-08 angegangen werden. Jede hat eigenes Risiko und bekommt eine eigene Planungs-Einheit:

- **Trunkierung + `maxResults` für alle Listen-Tools** — `find_symbol`, `find_references`, `get_impact`, `search_pattern` mit `maxResults`-Parameter (Default 50) und einheitlicher Meta-Zeile. Status: **bereits umgesetzt** in 002/004/005; bleibt hier als Referenz.
- **Regel-ID in `get_violations`-Ausgabe** — jeder Verstoß trägt seine Regel-ID, kein `agent_hint`-Feld nötig. Status: **bereits umgesetzt** in 001; bleibt hier als Referenz.
- **Kaltstart entkoppeln** — stdio-Transport zuerst aufsetzen, Solution-Load als Hintergrund-Task; `McpCodeGraphServer` bekommt dritten Zustand „lädt noch", Tools antworten in dieser Zeit mit einer strukturierten „Solution wird noch geladen"-Antwort. Status: **umgesetzt in EPIC-05** (B.4).
- **Neu angelegte/gelöschte `.cs`-Dateien sichtbar machen** — zusätzlicher Verzeichnis-Sweep, der Dokumente ohne Datei entfernt und neue Dateien über die Roslyn-Solution-API einhängt (Projekt-Zuordnung über längsten gemeinsamen Pfad-Präfix). Bekannte Einschränkung: `<Compile Remove=...>`-Ausschlüsse werden nicht erkannt. Status: **umgesetzt in EPIC-05** (B.2).
- **Staleness-Sweep über Verzeichnis-`mtime` kurzschließen** — Verzeichnis-`mtime` cachen, unveränderte Verzeichnisse komplett überspringen; deckt zusammen mit dem vorigen Punkt den Datei-Sweep ab. Status: **umgesetzt in EPIC-05** (B.5).
- **`rules.json`-Auto-Discovery** — ohne `--config` neben der aufgelösten Solution-Datei nach `rules.json` suchen; wird keine gefunden, `[WARN]` auf stderr **und** Vermerk in `get_violations`-Antwort. Status: **umgesetzt in EPIC-04** (B.1).
- **stdout strukturell als reiner Protokollkanal** — eigene `ILintConsole`-Implementierung für den MCP-Modus, die auch `WriteLine` nach stderr leitet. Status: **umgesetzt in EPIC-06** (B.6) — `McpLintConsole` mit `Instance`-Singleton, Aktivierung in `Program.cs:43`, E2E-Regressions-Test in `McpServerCommandJsonRpcFramingTests` (Integration, spawned `AiNetLinter.exe` und verifiziert jede stdout-Zeile als gültigen JSON-RPC-Frame).
- **Generierte Last-Fixture** — synthetische Solution definierter Größe (z. B. 500/5.000 Dateien) als Skalierungsnachweis; Messlauf für Kaltstart-Zeit und Tool-Call-Dauer. Status: **umgesetzt in EPIC-05** (B.3).
- **Tool-vs-`rg`-Empfehlung in `Docs/integration.md`** — reine Doku, kein Code. Status: **umgesetzt in 008** (siehe `integration.md#mcp-server-registrieren`).
- **Discovery-Kontextbudget und Protokollpfade** — globale `ServerInstructions` auf 724 UTF-8-Bytes gekürzt; Legacy-`initialize` sowie MCP-2026-07-28-`server/discover` und jeweils `tools/list` per Raw-Wire gegen die registrierte Toolcollection geprüft. Status: **umgesetzt am 2026-08-20** (Aufgabe `tasks/mcp-agenten-effizienz/02`).
- **Transitive Symbolgraph-Ausgaben** — `find_references` und der Symbol-Branch von `get_impact` liefern für jede erlaubte Tiefe dieselbe strukturierte `callSites`/`completeness`-Antwort; Trunkierung nach `maxResults`, besuchten Knoten und Depth-Clamp wird getrennt ausgewiesen. Status: **umgesetzt am 2026-08-21** (Aufgabe `tasks/mcp-agenten-effizienz/03`).
- **Opt-in C#-Roslyn-Enrichment für `search_pattern`** — `enrichCSharp=false` bleibt der kompatible Default; sichtbare Treffer können bei expliziter Aktivierung als Deklaration, Symbolreferenz, Kommentar, String, Code oder unbekannt eingeordnet werden. Stabile `symbolId`-Werte sowie `ambiguous`-/`unavailable`-Zustände bleiben auf den residenten Snapshot und eindeutig zuordenbare Dokumente begrenzt. Status: **umgesetzt am 2026-08-21** (Aufgabe `tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget`).
- **`get_symbol_body` + stabile Symbol-IDs in `get_file_skeleton` (E.1)** — neues Tool liefert den Source-Body eines C#-Symbols per stabiler Roslyn-`DocumentationCommentId` (überlebt Refactorings solange FQN stabil); `get_file_skeleton`-Output wird um stabile `id:`-Felder pro `SkeletonTypeInfo`/`SkeletonMemberInfo` erweitert. Status: **umgesetzt in EPIC-08** (step-012) — neue 4. Registrar-Klasse `SymbolBodyToolRegistrations` (eigene Klasse, weil `SymbolGraphToolRegistrations` bereits am 2850-PathOverride hängt), `GetSymbolBodyTool` + `SymbolIdentifierResolver` mit `TryResolveByStableIdAsync`.
- **`depth`-Parameter an `find_references`/`get_impact` mit aggregierter Ausgabe ab `depth > 1` (E.2)** — Symbol-Branch nutzt `CallGraphTraversal`-Helper mit `MaxRecursionNodes`-Begrenzung; `depth` default 1, hard cap 3; `depth > 1` kollabiert transitive Treffer in einer Topologie-Übersicht. Git-Branch ignoriert `depth` (Doku-konform). Status: **umgesetzt in EPIC-08** (step-012).
- **DI-Registrierungs-Hinweis als Zusatzzeile in `get_type_hierarchy` (E.3)** — `DiRegistrationHeuristics` mit `\b`-Word-Boundary-Regex auf `AddScoped`/`AddSingleton`/`AddTransient` + Heuristik-Filter auf `type.ToDisplayString()`. 4. Sektion im Tool-Output mit explizitem Header „DI-Registrierungen (heuristisch, Convention-/Factory-basiertes Scanning nicht abgedeckt)". Test-Mini-Solution `DiRegistrationMini/` als realistisches DI-Setup. Status: **umgesetzt in EPIC-08** (step-012).
- **Tech-Debt-Abschluss (Muss-Haben D, EPIC-07)** — TD-001 (ungenutzte transitive Paket-Referenz) + TD-002 (Subprozess-E2E-Fixture-Pool) + TD-006 (`SafeEnumerateFiles`/`IsGeneratedPath`-DRY-Konsolidierung in `FileSystemExclusionHelpers`) + TD-008 (Refactoring-Historie „ehemalige 6-Parameter-Signatur" in `GetViolationsScanner.cs`) geschlossen; TD-004 (Footprint-Druck auf Tool-Registrierungs-Sammelklassen) bewusst zurückgestellt mit Begründung: gemeinsame Basis-Klasse würde das „dünner Dispatch + Scanner/Formatter-Datei"-Pattern aus EPIC-03 verwässern, Footprint-Druck ist über PathOverride-Mechanik + `ILinterEngineConfig`-Entlastung aus step-008 beherrschbar. Status: **umgesetzt in EPIC-08** (step-012).

Details und Reihenfolge der geplanten Punkte folgen in den jeweiligen Epic-Abschnitten weiter oben. Jede Erweiterung wird einzeln geplant, eigene Einheit, eigener Review — keine „Alles-oder-nichts"-Bündelung.

---

## MCP-Tool `metrics_tree`

Eigener, mittlerweile abgeschlossener Task (separat von den oben gelisteten MCP-Codegraph-Server-EPICs, eigene EPIC-Zählung; Task-Ordner nach Abschluss entfernt). Neues MCP-Tool `metrics_tree` liefert einen ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten und sortierten Top-N-Kindern je Ebene — Ebene-für-Ebene-Exploration einer Solution statt Komplett-Dump.

- [x] **EPIC-01 — Datei-Walk-Modi:** `code_size` (LoC + Bytes, absteigend sortiert) und `comment_density` (Kommentar-Ratio, aufsteigend sortiert), reiner `SolutionFileWalker`-basierter Datei-Walk ohne Roslyn-Overhead; modus-agnostischer ASCII-Tree-Renderer.
- [x] **EPIC-02 — Roslyn-Modi:** `violation_density` (Lint-Verstöße pro Datei via `LinterEngine`, Total/Fehler/Warnungen) und `complexity` (Ø/max. zyklomatische und max. kognitive Komplexität pro Datei über `ComplexityCalculator`, nur `MethodDeclarationSyntax`); Tool-Registrierung nach `AnalysisToolRegistrations` verschoben (gleicher Grund wie bei `get_violations`: `LinterEngine`-Pull-in). Vollständige Tool-Referenz: [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).

---

## MCP-Tool `pattern_detect`

Umgesetzt als S2.2 aus `tasks/features/05-roadmap.md`. Neues MCP-Tool `pattern_detect` gruppiert die von der bereits residenten `LinterEngine` erzeugten Lint-Verstöße nach Pattern-Kategorie (god-class, async-void, long-method, public-without-doc, empty-catch, feature-envy) statt der flachen Datei-für-Datei-Liste von `get_violations` — Solution-weite Audit-Sicht in Sekunden.

- [x] **6 von 10 in der Roadmap genannten Patterns umgesetzt** (`god-class`, `async-void`, `long-method`, `public-without-doc`, `empty-catch`, `feature-envy`) — reine Aggregation bereits existierender, produktiver Linter-Regeln (`AIContextFootprint`/`MaxPublicMembersPerType`/`MaxLineCount`, `BanAsyncVoid`, `MaxMethodLineCount`/`MaxCyclomaticComplexity`/`MaxCognitiveComplexity`, `EnforceXmlDocumentation`, `EnforceNoSilentCatch`, `AvoidExcessiveMiddleMen`), kein neuer Detection-Code. Die anderen 4 (`deep-nesting`, `disposable-not-disposed`, `static-state`, `magic-numbers`) sind bewusst zurückgestellt — keine existierende Erkennung, würden neue Roslyn-Syntax-Walker mit eigenem False-Positive-Risiko erfordern (analog zum `method_count`-Präzedenzfall bei `metrics_tree`). Vollständige Tool-Referenz: [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).

---

## MCP-Tool `dependency_graph`

Umgesetzt als M2 aus `tasks/features/05-roadmap.md`, priorisiert nach der Dogfooding-Session 2026-08-10/11 als groesste verbleibende Navigationsluecke ("welche Dateien/Typen haengen an Datei/Modul X"). Neues MCP-Tool `dependency_graph` (17. Tool) beantwortet das direkt statt mehrerer `find_symbol`/`find_references`-Umwege.

- [x] **Datei-/Typ-Ebene ueber echte `SemanticModel`-Typreferenzen** (nicht nur `using`-Direktiven), gefiltert auf in der Solution deklarierte Typen (BCL-/NuGet-Rauschen ausgeschlossen). Knoten sind Dateien, Kanten Datei-zu-Datei annotiert mit den ueberquerenden Typnamen; `typeIdentifier` scoped enger als `filePath` (nur die Deklaration des einen Typs statt der ganzen Datei).
- [x] **`incoming`/`outgoing`/`both`** ueber `direction`-Parameter, `depth` (Default 1, hard cap 3) traversiert transitiv auf Datei-Ebene, zyklensicher per Visited-Set (schliessende Kante bleibt sichtbar statt verworfen zu werden), hart begrenzt auf 150 besuchte Dateien.
- [x] **`maxResults`** (Default 50) von Anfang an, mit Trunkierungs-Meta-Zeile und korrekt unterdruecktem Sufficiency-Hinweis bei Trunkierung (durch `maxResults` oder den Traversierungs-Hard-Cap) — genau die Bug-Klasse aus der Dogfooding-Session (`get_violations`/`get_hotspots`/`get_type_hierarchy`/`get_call_tree`), die hier von Anfang an vermieden wurde.
- [x] **`StructuredContent` als Objekt**, nie ein nacktes Array — bleibt (anders als `find_references`/`get_impact`) auch bei `depth > 1` gefuellt, weil die BFS ihre Kanten durchgehend als strukturierte Records haelt statt wie bei der transitiven Caller-Traversierung nur Strings zu akkumulieren.
- [x] **Projekt-Ebene als guenstige Zusatz-Sicht** (`Project.ProjectReferences` des Zielprojekts, kein NuGet-Aufruf) — kein vollstaendiger Projektgraph, wie im Scope vorgesehen.
- [x] **NuGet-Vulnerability-Scanning bewusst nicht umgesetzt** (widerspricht dem Cloud-Abhaengigkeits-Anti-Ziel, siehe `tasks/features/05-roadmap.md` §0) — deckt sich mit der urspruenglichen Scope-Entscheidung.
- [x] Registrierung als sechstes Symbolgraph-Tool in `SymbolGraphToolRegistrations.cs` (nicht als eigene Registrations-Datei) — reuse von `FindReferencesTool.ResolveSymbolAsync` und demselben Visited-Set-Traversierungsmuster wie `CallGraphTraversal`.
- [x] 25 neue Unit-Tests (Scanner + Tool, inkl. Zyklen-Fall, Typ-Scope-Praezision, Trunkierung, Depth-Clamping) + 1 Live-Repo-Test. Vollstaendige Tool-Referenz inkl. Structured-Output-Beispiel: [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).

---

## MCP-Tool `find_duplicates` / Linter-Regel `DuplicateCode` (Drift-Audit — DRY-Erkennung)

Umgesetzt als M9 aus `tasks/features/05-roadmap.md`, priorisiert direkt nach M8. Token-basiertes
Code-Clone-Detection (CCFinder/Jaccard-N-Gram-Ansatz, Method-Granularitaet, siehe
`tasks/features/07-drift-audit-ideen.md` §A) — der urspruengliche Nutzer-Anlass war die
`JsonSerializerOptions`-Duplikation aus der Dogfooding-Session 2026-08-10/11, die vor S1.3 in
4 MCP-Tools separat instanziiert war.

- [x] **`DuplicateDetectionEngine`** (`Core/DuplicateDetection/`): Token-Extraktion, N-Gram-
  Shingling, Inverted Index, Jaccard-Similarity, transitive Cluster-Bildung (Union-Find),
  gestaffelte Schwellwerte `exact`/`near`/`fuzzy` (0.95/0.80/0.65) statt hartem Cut. Geteilt
  zwischen Linter-Checker und MCP-Tool (eine Engine, zwei Konsumenten).
- [x] **MCP-Tool `find_duplicates`** (18. Tool, `Mcp/Tools/DuplicateDetection/`): `mode="clone"`
  (Default) liefert Cluster gestaffelt nach Aehnlichkeit; `mode="refactoring-drift"` (Idee C,
  "absence-of-calls"-Heuristik, Murphy-Hill 2005) findet Methoden, die einen per `helperSymbol`
  benannten Helper strukturell nachbauen statt ihn aufzurufen — als Kandidaten gelistet, fliesst
  nicht in Lint/`safeguard` ein (On-Demand-only); `mode="structural"` (Typ-4/Intended Duplication)
  erkennt semantisch aehnliche Hilfsmethoden anhand deterministischer Roslyn-Strukturprofile und
  Cosine-Similarity — ebenfalls On-Demand-only, Kandidatencluster mit Strukturprofil-Kurzfassung,
  eigene Cosine-Schwellwerte (`StructuralDuplicate*Threshold`, Standard 0.90/0.80/0.70).
- [x] **Linter-Checker `DuplicateCodeChecker`**: solution-weite Nachpruefung (via
  `PostAnalysisChecks`, nicht Datei-Node-Walker wie die meisten anderen Checker). Meldet nur
  `exact`-Cluster (Severity `info`), ein Regelverstoss pro Cluster (repraesentatives Mitglied,
  `Details` listet alle Mitglieder) — `near`/`fuzzy` bleiben ueber das Tool/den Skill einsehbar,
  waeren aber zu viel Rauschen fuer automatisches Lint (Live-Dogfood-Befund: `near` allein erzeugte
  ~23 Einzel-Funde auf diesem Repo). Respektiert die dateiweite `// ainetlinter-disable
  DuplicateCode`-Suppression-Konvention ueber alle Cluster-Mitglieder.
- [x] **Self-Audit-Skill** `.agents/skills/drift-audit/SKILL.md` (Idee F, projekteigen, nicht Teil
  des generischen `Agent-Scaffolding`-Pakets) — Vier-Schritte-Playbook (+ struktureller Scan-Schritt
  fuer Typ-4-Kandidaten), Cadence pro Epic verpflichtend / pro Step optional (Hinweis in `AGENTS.md`).
- [x] `rules.json`-Config (`Global.DuplicateCode*`, 9 Keys + `StructuralDuplicate*Threshold`, 3 Keys)
  + `RuleRegistry`-Eintrag (`--list-rules`/`--describe-rule`/`--search-rules`).
- [x] 75+ neue Unit-/Integrationstests (Engine, Checker, Tool, Refactoring-Drift, Suppression,
  Structural-Detector, Structural-Tool) + Live-Repo-Tests. Vollstaendige Tool-Referenz:
  [Docs/agent-api.md#mcp-server-modus](agent-api.md#mcp-server-modus).
- [x] Naming-Drift (Idee E), AST-CPD (Idee B) und Pattern-Cluster-Detection (Idee D) bewusst nicht
  umgesetzt — siehe `tasks/features/07-drift-audit-ideen.md`.

---

## Restore-Erkennung (`ProjectRestoreState`) — Phantom-Dependency-Folgefehler bei fehlendem `dotnet restore`

Bug-Report aus einer Dogfooding-Session gegen ein fremdes, per `ainetlinter`-MCP-Server gelintetes
Projekt: `MSBuildWorkspace` fuehrt (anders als `dotnet build`) keinen impliziten NuGet-Restore aus —
ein nicht restoretes Zielprojekt liess `DetectAndBanPhantomDependencies` tausende Einzel-Violations
pro unaufloesbarem `using` melden und den `safeguard`-Score auf 0,00/10 einbrechen, obwohl `dotnet
build` fuer dasselbe Projekt mit Exit-Code 0 abschloss. Architektur-Entscheidung (Erkennen statt Auto-Restore)
und Begruendung: `rationale.md` §13.

- [x] **`ProjectRestoreState`** (`Baseline/`): dateisystembasierte Erkennung (`obj/project.assets.json`
  fehlt oder ist aelter als die `.csproj`), kein Netzwerk-/Prozess-Seiteneffekt.
- [x] **`LinterEngine.ReportRestoreDiagnostics`**: meldet nicht restorete Projekte EINMAL pro Projekt
  (`PROJECT_NOT_RESTORED`) statt tausender Einzel-Violations — greift fuer alle `RunAsync`-Ueberladungen
  (Pfad, `SourceFileCatalog`, nackte `Solution`), erreicht damit auch die MCP-Tools (`get_violations`,
  `safeguard`, `pattern_detect`, `metrics_tree`), die `LinterEngine.RunAsync(Solution, …)` ohne
  Catalog aufrufen.
- [x] **`CheckerContext.ProjectHasLoadDiagnostics`** (ueber neues `DocumentLoadState`-Parameter-Object,
  haelt den Konstruktor unter dem Bool-Parameter-/Dependency-Limit): pro Dokument granular, gespeist
  aus `ProjectRestoreState.ComputeProjectsNeedingRestore`. `PhantomDependencyChecker.CheckPhantomNamespace`
  unterdrueckt Funde nur fuer Dokumente eines betroffenen Projekts — ein sauber geladenes Projekt B
  in derselben Solution wie ein nicht restoretes Projekt A wird weiterhin normal gelintet.
- [x] Neue Unit-/Integrationstests: `ProjectRestoreStateTests`, `LinterEngineProjectRestoreTests`
  (End-to-End ueber `LinterEngine.RunAsync(Solution, …)`), erweiterte `PhantomDependencyCheckerTests`
  (Suppression nur bei Lade-Problem, echte isolierte Phantome weiterhin gemeldet).

---

## MCP-Tool-Robustheit: fehlender/falsch benannter Pflichtparameter crasht nicht mehr

Bug-Report aus einer anderen Session: `get_type_hierarchy` und weitere MCP-Tools stuerzten intern
ab ("An error occurred invoking...") statt eines sauberen `[ERROR]`-Ergebnisses, wenn ein Aufrufer
den falschen Parameter-Namen uebergab (z. B. `symbolIdentifier` statt des von `get_type_hierarchy`
erwarteten `typeIdentifier`). Ursache: die Identifier-/Pattern-Parameter waren in den
`McpServerTool.Create`-Registrierungen (`Mcp/*ToolRegistrations.cs`) als Pflicht-Parameter ohne
Default deklariert — die `ModelContextProtocol.Server`-SDK-Argument-Bindung scheiterte damit vor
Erreichen des Tool-Codes, bevor die eigene `INVALID_ARGUMENT`-Behandlung greifen konnte.

- [x] Betroffene Parameter auf optional (`string? x = null`) umgestellt: `find_symbol.namePattern`,
  `find_references`/`get_call_tree.symbolIdentifier`, `get_type_hierarchy.typeIdentifier`,
  `get_symbol_body.identifier`, `get_file_skeleton.filePath`, `search_pattern.pattern`,
  `metrics_tree.mode`/`root`. `find_duplicates` (inkl. `helperSymbol`) war bereits vollstaendig
  optional deklariert — kein Aenderungsbedarf, per Test abgesichert.
  Die bewusst unterschiedlichen Parameter-Namen je Tool (dokumentieren das jeweils erwartete
  Format) bleiben unveraendert.
- [x] Jede betroffene Tool-Execute-Methode prueft den Parameter jetzt explizit auf `null`/leer und
  liefert `McpToolResults.Recoverable(INVALID_ARGUMENT, ...)` mit einem Hint, der den korrekten
  Parameternamen und das erwartete Format nennt — wiederverwendeter, bereits vorhandener
  `INVALID_ARGUMENT`-Code statt eines neuen.
- [x] Neue E2E-Tests auf echter SDK-Bindungsebene (`McpTestClient` ueber `StdioClientTransport`,
  nicht nur Unit-Tests auf `ExecuteAsync`) in `McpServerAllToolsE2ETests`: fehlender Parameter je
  betroffenem Tool sowie eine direkte Reproduktion des gemeldeten Bugs (`get_type_hierarchy` mit
  `symbolIdentifier` statt `typeIdentifier`).
- [x] Doku aktualisiert: `Docs/agent-api.md` (neuer Abschnitt zum Verhalten bei fehlendem/falsch
  benanntem Pflichtparameter), `Mcp/IsErrorPolicy.md` (Policy-Zeile + Audit-Tabelle ergaenzt).

---

## Symbolgraph-Positionsauflösung: `Datei:Zeile`-Fallback ohne Spalte

Bug-Report aus einer anderen Session (gegen ein fremdes Projekt getestet): laut Tool-Beschreibung
soll ein Identifikator im Format `Datei:Zeile` funktionieren, schlug in der Praxis aber immer mit
`SYMBOL_NOT_FOUND` fehl — nur `Datei:Zeile:Spalte` (mit expliziter Spalte) funktionierte.
Root Cause: `SymbolIdentifierResolver.TryParsePosition` verlangte strikt mindestens drei durch `:`
getrennte Segmente; bei nur zwei Segmenten interpretierte `FindReferencesTool.ResolveByNameAsync`
den kompletten String faelschlich als qualifizierten Namen. `ResolveSymbolAsync` ist der
gemeinsame Einstiegspunkt fuer `find_references`, `get_impact`, `get_type_hierarchy` und
`get_symbol_body` — der Fix wirkt transitiv fuer alle vier Tools.

- [x] **`SymbolIdentifierResolver.TryParseLineOnlyPosition`**: parst wie `TryParsePosition` von
  hinten (letztes Segment = Zeile, Rest inkl. enthaltener `:` wieder zum Pfad zusammengesetzt) —
  deckt sowohl relative Pfade (zwei Segmente) als auch absolute Windows-Laufwerksbuchstaben-Pfade
  (drei Segmente durch den Doppelpunkt nach dem Laufwerksbuchstaben, z. B. `C:\Datei.cs:91`) ab.
  Eine anfangs erwogene Beschraenkung auf exakt zwei Segmente haette Laufwerksbuchstaben-Pfade
  grundsaetzlich ausgeschlossen — auf einem reinen Windows-Projekt der Normalfall, kein Sonderfall.
- [x] **`SymbolIdentifierResolver.ResolveSymbolsOnLine`**: sammelt alle eindeutigen, quelltext-
  eigenen Symbole einer Zeile (Tokens iterieren, `ResolveSymbolAtToken` je Token, Dedup per
  `SymbolEqualityComparer`, gefiltert auf `Location.IsInSource` — Metadata-/BCL-Symbole wie das
  `string`-Schluesselwort eines Rueckgabetyps waeren sonst reines Rauschen).
- [x] **`FindReferencesTool.ResolveByLineAsync`**: neuer Aufloesungspfad neben
  `ResolveByPositionAsync`, teilt sich mit diesem die Dokument-/Zeilen-Validierung
  (`ResolveDocumentForLineAsync`, keine Duplikation). Genau ein Symbol auf der Zeile → Treffer;
  mehrere → `AMBIGUOUS_SYMBOL` mit Kandidatenliste (analog zur Namensaufloesung); keins →
  `SYMBOL_NOT_FOUND`.
- [x] Neue Unit-Tests: reine Parsing-Tests fuer `TryParsePosition`/`TryParseLineOnlyPosition`
  (inkl. Windows-Laufwerksbuchstaben-Regression), `find_references`-Integrationstests
  (eindeutige Zeile = identisches Ergebnis zu Datei:Zeile:Spalte, mehrdeutige Zeile,
  Zeile ohne Symbole) sowie je ein Test, der den transitiven Effekt fuer `get_type_hierarchy`
  und `get_symbol_body` belegt.
- [x] Doku aktualisiert: Tool-Beschreibungen in `SymbolGraphToolRegistrations.cs` /
  `SymbolBodyToolRegistrations.cs`, `Docs/agent-api.md` (Tool-Tabelle + `get_symbol_body`-Detail-
  Abschnitt).

---

## DRY-Konsolidierung (Drift-Audit via `find_duplicates`)

`find_duplicates(scopeDir="src", minTokens=20)` fand 188 Cluster (13 `exact`, 24 `near`, 151
`fuzzy`) ueber die gesamte Solution. Nach Konsolidierung: 165 Cluster (0 `exact`, 16 `near`,
149 `fuzzy`) — die verbleibenden `near`-Cluster sind geprueft und als fachlich legitime,
strukturell nur zufaellig aehnliche Testmethoden (parametrisierte Szenario-Varianten) eingestuft.

- [x] Produktionscode: `SyncAgentRulesCommand`/`AgentRulesGenerator.ResolveBaseDirectory`,
  `BoolParameterChecker.CheckMethod`/`CheckConstructor`, `DiffImpactAnalyzer`/
  `GitChangedFilesResolver.FindGitRoot` (neu: `GitRepositoryLocator`), `DiffImpactAnalyzer`/
  `LinterAutoFixer.FindDocumentByPath`, `HotspotMapBuilder`/`GetHotspotsScanner.AppendSection`
  (neu: `Output.HotspotSectionFormatter`), `GetViolationsScanner`/`MetricsTreeRoslynScanner`/
  `SafeguardScanner.ResolveSeverity` (neu: `RuleRegistry.ResolveSeverity`), `CssAnalyzer`/
  `JsAnalyzer`/`RazorAnalyzer.CountLines` (neu: `Web.WebTextMetrics`) konsolidiert.
- [x] `near`-Cluster mit produktivem Befund konsolidiert: `ImmutabilityChecker`/
  `MiddleManChecker.HasExemptBaseType` (neu: `Core.Checkers.ExemptBaseTypeResolver`),
  `UiFileSeparationChecker`/`CssAnalyzer`/`JsAnalyzer.CreateViolation` (neu:
  `Models.RuleViolationFactory`), `SourceFileCatalog`/`LinterEngine.CollectValidDocuments`,
  `StateChecker.CheckConstructorDependencies`/`CheckPrimaryConstructorDependencies` (gemeinsamer
  `ReportIfExceedsDependencyLimit`-Kern).
- [x] Refactoring-Drift gefunden und gefixt: `SearchPatternScanner.IsGeneratedPath` und
  `SuppressionFileResolver.IsGeneratedPath` bauten den bereits zentralisierten
  `Baseline.FileSystemExclusionHelpers`/`SourceFileCatalog.IsGeneratedPath`-Filter partiell nach
  und liessen dabei den Worktree-Ausschluss aus — beide auf die zentrale Implementierung
  umgestellt (behebt einen latenten Bug: vervielfachte Treffer bei `search_pattern`/
  Suppression-Sync innerhalb von `.claude/worktrees/`).
- [x] Test-Helper: `TestHelper` um `DeleteFileIfExists`, `DeleteDirectoryIfExists`,
  `TryDeleteLogFileAndDirectory`, `FindSlnxFile`, `BuildCalibratedMethod`/
  `CalibratedBaseStatements`, `CreateSemanticModel`, `CreateFaultySolution` ergaenzt (jeweils
  mehrfach dupliziert in Testklassen); `ThrowingTextLoader` (4x als private Nested-Klasse
  dupliziert) in eine eigene Top-Level-Datei extrahiert; `McpMiniFixtureBase<TWorkspace>` fuer
  `BaselineMcpFixture`/`SymbolGraphMcpFixture` (analog zum bestehenden `FixtureWorkspaceBase`-
  Muster fuer die zugehoerigen Workspaces).

---

## Testsuite-Restrukturierung & Speedup (FastTests, IntegrationTests, TestKit)

Vollständige Neustrukturierung und Beschleunigung der Testsuite (.NET 10 / xUnit v3) zur Trennung von schnellen In-Memory-Tests und prozess-/dateibasierten Integrationstests:

- [x] **Drei spezialisierte Testprojekte:**
  - `src/AiNetLinter.FastTests`: Reine In-Memory-Tests (Unit & Component, Roslyn Adhoc-Workspaces, Ausführung < 10s).
  - `src/AiNetLinter.IntegrationTests`: Datei-I/O-, CLI-, Dogfood-, Performance- und Stress-Tests.
  - `src/AiNetLinter.TestKit`: Wiederverwendbare Test-Infrastruktur, Fixtures (`RoslynTestSolutionFactory`, `IsolatedFixtureLease`, `RecordingLintConsole`).
- [x] **Vollständige Migration aller 183 Testklassen / 1259+ Tests:**
  - 100% der Klassen methodengenau und verhaltensgetreu migriert.
  - Altes Legacy-Projekt `src/AiNetLinter.Tests` vollständig aus Solution und Dateisystem entfernt.
- [x] **Kategorisierung & Selektive Testläufe:**
  - Schnelle Entwicklungsschleife über `Category=Unit` oder `Category=Component`.
  - Normales CI/Verifikations-Gate über `Category!=Stress`.
  - Gezielte Last- und Stresstests über `Category=Stress`.

---

## Feedback-Runde 1: MCP-UX & Regel-Präzision

Erweiterungen und Verfeinerungen basierend auf praktischem Agent-Feedback:

- [x] **FB-02: `AvoidExcessiveMiddleMen` für Testdateien überspringen:**
  - Test-Fixtures, Mocks und Helper leiten Methodenaufrufe häufig 1:1 weiter; diese werden nun standardmäßig von der Prüfung ausgenommen.
- [x] **FB-03: `MaxPublicMembersPerType` für Testdateien mit Opt-in:**
  - Testklassen mit vielen `[Fact]`-Methoden werden standardmäßig nicht mehr als Verstoß gewertet.
  - Neue Konfigurationsoption `MaxPublicMembersPerTypeApplyToTestFiles` (Standard: `false`) ermöglicht explizites Opt-in.
- [x] **FB-04: `find_duplicates` UX-Verbesserungen:**
  - Neuer Parameter `scopeType` (`"all"` [Default], `"production"`, `"tests"`) zur gezielten Einschränkung der Duplicate-Detection.
  - Top-Cluster-Übersichts-Header bei mehr als 20 Treffern für schnellen Überblick.
- [x] **Teil B: Code-Snippets in `get_violations`:**
  - Neue Parameter `includeSnippet` (Default `false`) und `contextLines` (0-5, Default `2`).
  - Quellcode-Ausschnitte werden direkt in Text-Report und `structuredContent` eingebettet (spart separate `get_symbol_body`-Calls).
- [x] **Teil A: Neues MCP-Tool `get_class_structure`:**
  - Tabellarische Member- und Zeilen-Übersicht eines Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl, Signatur).
  - Parameter `sortBy` (`"lines"` [Default], `"kind"`, `"name"`), `maxMembers` (Default 50, Cap 200) mit Truncation-Meta-Zeile und `Truncated`-Flag im StructuredContent (`TotalMemberCount`/`ShownMemberCount`).
  - Bei `record`-Typen werden die Parameter des Primary Constructors als eigene Zeilen (`Kind: "PrimaryCtor-Param"`) vor den restlichen Membern ausgegeben.
  - Unterstützt Partial-Classes-Kombination und `ClassStructurePayload` im StructuredContent.
- [x] **FB-01: Heuristik für declaration-only types im `AIContextFootprint`:**
  - Reine Datenträger-Typen (DTOs, Models, Options, Enums, Records ohne Methoden) werden im transitiven Footprint auf max. 10 Deklarationszeilen gedeckelt (`MaxDeclarationLines` in `AIContextFootprintCalculator`).

---

## Hierarchische Code-Exploration: `get_namespace_tree` (P1 - Progressive Disclosure)

Strukturierter semantischer Drilldown entlang der logischen C#-Hierarchie (Projekte ➔ Namespaces ➔ Typen):

- [x] **3 Zoom-Stufen:**
  - Stufe 1: Solution-Überblick über alle Projekte (`Typ: Lib/Exe/Test`, Namespace- und Typ-Anzahl).
  - Stufe 2: Projekt-/Namespace-Drilldown (`depth`-Traversierung 1-3, `includeTypes: false/true`, Einrückungsebenen).
  - Stufe 3: Typen-Auflistung im Namespace (`Name`, `Kind`, `FilePath:Line`, `Visibility: public/internal/private`).
- [x] **Filterung & Absicherung:**
  - Filter nach `kind` (`class`, `interface`, `record`, `struct`, `enum`, `all`).
  - Ausschluss von generierten Symbolen (`<CompilerGeneratedAttribute>`, `<Clone>$`, `EqualityContract`).
  - Quellcode-Fokus (`IsInSource` / `DeclaringSyntaxReferences`).
  - Robuste Truncation (`maxResults`, Cap 200) mit Truncation-Meta-Zeile und StructuredContent-Payload (`NamespaceTreePayload`).
- [x] **DRY-Refactoring & Konsolidierung:**
  - `SymbolKindClassifier` und `SymbolVisibilityResolver` für modulübergreifende Einheitlichkeit bei Typ-, Kind- und Visibility-Deskriptoren.

---

## One-Shot Metriken & Context-Footprint: `metrics_lookup` (Feature 02)

Punktgenaue Symbol-Analyse (Methoden, Konstruktoren, Properties, Typen) in einem einzigen MCP-Aufruf:

- [x] **Symbol-Auflösung & Metriken:**
  - Unterstützt DocCommentId (`M:...`), `Datei.cs:Zeile:Spalte`, `Datei.cs:Zeile`, qualifizierte und unqualifizierte Namen via `FindReferencesTool.ResolveSymbolAsync`.
  - Liefert Netto-Codezeilen (`MethodLineCounter`), zyklomatische & kognitive Komplexität (`ComplexityCalculator`), Parameteranzahl (brutto/effektiv mit Ignored-Types-Filterung) und `AIContextFootprint` (`AIContextFootprintCalculator.CalculateDetailed`).
  - Schwellwert-Abgleich gegen aktive `rules.json` (`[OK]`, `[WARN]`, `[VIOLATION]`).
- [x] **Generalisierung von `ComplexityCalculator`:**
  - `SyntaxNode`-Überladungen für Konstruktoren, Property-Accessoren und Lambdas.
- [x] **Structured Output & Tests:**
  - `MetricsLookupResultDto` in `structuredContent`.
  - Vollständige FastTests- & IntegrationTests-Abdeckung.

---

## Composite One-Shot-Exploration: `get_feature_context` (Feature 01)

Bündelt 5 Dimensionen (Deklaration, Metriken & Budget, direkte Aufrufer, statische Test-Zuordnung und Linter-Violations) in einem einzigen residenten Aufruf vor Feature-Edits/Refactorings:

- [x] **Symbol-Auflösung & Composite Facade:**
  - Unterstützt Name, `Datei.cs:Zeile`, `Datei.cs:Zeile:Spalte` und `DocCommentId` über `FindReferencesTool.ResolveSymbolAsync`.
  - Aggregiert Deklarationsdaten, `MetricsLookupScanner`, `DiffImpactAnalyzer.FindCallSiteEntriesAsync`, residenten `TestCoverageScanner` und `LinterEngine`-Violations.
- [x] **Steuerungs-Flags & Structured Content:**
  - `includeCallers`, `includeTests`, `includeMetrics`, `includeViolations` (jeweils Default `true`).
  - `maxCallers` und `maxTests` (Default 10, Cap 50) mit Truncation-Hinweisen.
  - Vollständiges, typisiertes `FeatureContextPayload` in `structuredContent`.
- [x] **Tests & Integration:**
  - FastTests in `AiNetLinter.FastTests/Mcp/Tools/FeatureContext/` und `AiNetLinter.FastTests/Core/TestCoverageScannerTests.cs`.
  - Registrierung in `AnalysisToolRegistrations.cs` und `ServerInstructions.cs`.

---

## Statische Test-Zuordnung: `get_test_context` (Feature 02)

Ermittelt zielgerichtet zugeordnete Test-Dateien, Test-Klassen, Test-Methoden, Test-Kategorien (Unit/Integration) und direkt ausführbare `dotnet test` Filterbefehle für ein C#-Symbol:

- [x] **Fokussiertes MCP-Tool `get_test_context`:**
  - Parameter `symbol` und `symbolIdentifier` (Alias) mit flexibler Symbolauflösung via `FindReferencesTool.ResolveSymbolAsync`.
  - `maxResults` (Default 30, Cap 100) mit Truncation-Hinweisen.
- [x] **Wiederverwendeter residenter Test-Scanner & Tech-Debt-Bereinigung:**
  - Nutzt `TestCoverageScanner.FindTestsForSymbolAsync` ohne Duplikation.
  - Auslagerung von Match-Reason- und Category-Literalen in typsichere `TestCoverageMatchReasons` und `TestCategories` Konstanten.
- [x] **Diagnose & Structured Output:**
  - Klare Diagnosehinweise und empfohlene Testpfade, wenn keine Tests statisch zugeordnet sind.
  - Vollständiges typisiertes `TestContextPayload` in `structuredContent`.
  - Direkte `dotnet test --filter ...` Befehlsempfehlungen im Markdown-Report.
- [x] **Tests & Dokumentation:**
  - Vollständige FastTests in `AiNetLinter.FastTests/Mcp/Tools/TestContext/GetTestContextToolTests.cs`.
  - Integrationstest-Absicherung in `McpHandshakeToolRegistrationTests.cs`.
  - Synchronisation von `agent-api.md`, `integration.md`, `ROADMAP.md` und `README.md`.

---

## Strukturierter Diff-Kontext: `get_impact` mit `detailLevel=change-context`

Der Git-Diff-Zweig von `get_impact` liefert auf `detailLevel=change-context` den vollen Änderungs-Kontext einer Codeänderung als strukturierte Antwort:

- [x] **Vertrag & Parameter:**
  - `detailLevel` (`"callers"` Default, `"change-context"`; nur im Git-Diff-Modus, nie zusammen mit `symbolIdentifier` — sonst recoverable `INVALID_ARGUMENT` mit Hinweis auf `get_feature_context`), case-insensitive.
  - `maxChangedSymbols` (Default 20, Cap 100) und `maxTestsPerSymbol` (Default 10, Cap 50) mit Clamp (`< 1` → Default, `> Cap` → Cap).
  - Strukturiertes Payload in `structuredContent`: geänderte Dateien mit Hunk-Ranges, geänderte Symbole, Call-Sites, statisch zugeordnete Tests (`testAssociations`), diffbezogene Violations ohne Snippets, empfohlene `dotnet test`-Befehle und Completeness-Metadaten; kompakte Textform mit Sufficiency-Hinweis bei vollständigem Ergebnis, sonst Trunkierungs-Meta-Zeile; „kein Repo / leerer Diff" liefert ein leeres, vertragsgültiges Objekt.
- [x] **Deterministische Symbol-Kappung vor Folgeanalysen:**
  - Kappung im Analyzer-Kern nach Symbolermittlung und VOR Call-Site-/Test-/Violations-Analysen, Sortierung Projekt → Datei → Startzeile → Symbol-ID; weggekappte Symbole erscheinen nirgends in der Antwort, die Gesamtzahl wird gespiegelt.
  - Der `callers`-Pfad bleibt ohne wirksamen Cap verhaltensidentisch (Snapshot-Tests unangetastet grün).
- [x] **Solutionweite Violations-Stufe („Linter genau einmal"):**
  - Genau ein Lint-Lauf pro Aufruf über den gemeinsamen Helper, diffbezogene Filterung auf Hunks und Spannen gezeigter Symbole, zentrale Pfadnormalisierung der drei Pfadsemantiken.
- [x] **Gebatchte Test-Zuordnung:**
  - Ein Solution-Durchlauf für alle geänderten Symbole (Evidenzarten und Prioritäten des statischen Testscanners unverändert), Testmethoden je Symbol gekappt; `recommendedTestCommands` dedupliziert — genau ein Befehl je betroffenem Testprojekt.
- [x] **depth>1-Verhaltenskorrektur:**
  - Die Traversierung enqueued das einschließende Aufrufer-Member statt der referenzierten Definition — `depth > 1` liefert echte mehrstufige Aufruferketten für `find_references` und den `get_impact`-Symbol-Branch (Verhaltenskorrektur mit geänderten Bestandsausgaben, nicht nur additive Erweiterung); lokale Funktionen tragen eindeutige `#lf:`-IDs.
- [x] **Dokumentation & Grenzen:**
  - change-context-Vertrag (Feldnamen, Defaults/Caps, Fehlerfälle) samt sechs dokumentierten Grenzen in `Docs/agent-api.md`; `README.md`-Toolzeile synchronisiert.

---

## System-Logging: Prozess-Lifecycle in `logs/` (`appsettings.json`)

Prozessinternes System-Logging (Serilog-Datei-Sink), das Prozess- und Verbindungs-
Lifecycle aller Prozessrollen in einer gemeinsamen Tagesdatei sichtbar macht:

- [x] **Konfiguration über `appsettings.json` neben der EXE** (im Release-Archiv enthalten):
  `Logging:MinimumLevel` (Default `Debug`), `Logging:Directory` (Default `logs`, relativ zur EXE),
  `Logging:RetainedFileCount` (Default `14`). Fehlende Datei = Built-in-Defaults; defekte oder
  ungültige Datei = harter Abbruch mit `[CONFIG]`-Meldung in derselben System-Logdatei;
  unbekannte Schlüssel werden abgelehnt.
- [x] **Täglich rollende Logdateien `logs/ainetlinter-<yyyy-MM-dd>.log`** mit Prozessrolle
  (`cli`/`thin-client`/`daemon`) im jeder Zeile; Thin-Client und Daemon teilen einen Sink,
  sodass eine Session prozessübergreifend lesbar ist. Keine stdout/stderr-Belastung —
  der MCP-Wire-Verkehr bleibt unangetastet.
- [x] **Lifecycle-Instrumentierung:** Prozessstart (PID, Rolle, Version, Argumente),
  Daemon-Connect-or-Start inklusive detached Spawn, Handshake-Ergebnisse mit
  Client-PID/Versionen/Konfigurationsabweichungen, Ablehnungen (Versionskonflikt,
  Protokollversion), MCP-Session-Enden und -Ausnahmen, Parent-Watchdog-Abbrüche,
  Pipe-Pump-Ende mit Ursache und Replay-Fenster, Idle-Exit sowie Exit-Codes beider Prozesse.
- [x] **MCP-Tool-Call-Logging:** `Logging:McpCallLogging` ist standardmäßig aktiv (fehlender
  Schlüssel = `true`) und schreibt genau ein Event je abgeschlossenem Tool-Call mit
  `ToolName`, `DurationMs`, `IsError` sowie bei Fehlern `ErrorCode`; Daemon-Events tragen
  `ConnectionId`. Argumente, Response-Payloads und ThinClient-Duplikate werden nicht geloggt.
- [x] **Konsolen-Spiegelung:** Alle `[INFO]`-/`[WARN]`-/`[ERROR]`-/`[FATAL ERROR]`-Diagnose-
  zeilen von `LinterConsole`/`McpLintConsole` werden severity-klassifiziert ins Log gespiegelt
  ([FATAL ERROR]→Fatal, [ERROR]→Error, [WARN]→Warning, [INFO]→Information).
- [x] **Tests & Dokumentation:** 17 Unit-Tests (Loader-Validierung inkl. Defekt-Fälle,
  Defaults, Bereichsprüfungen, Severity-Klassifizierung) in `AiNetLinter.FastTests/Logging/`;
  Abschnitt „System-Logging" in `Docs/configuration.md`.

## MCP-Batch-Toolfamilie: Reine Array-Parameter & Einheits-DTOs (Task 13)

Vollständige Umstellung der vier MCP-Batch-Tools auf reine Array-Parameter und Beseitigung des Singular/Array-Dualismus:

- [x] **Reine Array-Parameter:** `find_symbol` (`namePatterns`), `get_file_skeleton` (`filePaths`), `get_symbol_body` (`symbolIdentifiers`), `metrics_lookup` (`symbolIdentifiers`). Alle Singular-Parameter (`namePattern`, `filePath`, `symbolIdentifier`) vollständig entfernt.
- [x] **Einheitliche Helper-Logik:** `McpBatchArguments.Normalize` ersetzt `Collect` (kein Single/Multiple-Merge-Code mehr, string-trimming, Leer-/Whitespace-Filterung, Ordinal- bzw. OrdinalIgnoreCase-Deduplizierung).
- [x] **Batch-DTO-Vereinheitlichung:**
  - `find_symbol` liefert immer `FindSymbolBatchDto` (`results: [{ namePattern, matches: [...] }]`), auch bei Länge 1.
  - `metrics_lookup` liefert immer `MetricsLookupBatchDto` (`results`, `requestedCount`), auch bei genau einem Symbol.
- [x] **Cap & Validierung:** `find_symbol` hat hartes Limit `MaxPatternsPerCall = 10` (bei > 10 sofort `INVALID_ARGUMENT`); alle 4 Tools liefern einheitliches `INVALID_ARGUMENT` bei leerem/fehlendem Parameter.
- [x] **Tests & Integration:** Sämtliche Unit-, Komponenten- und Integrationstests aktualisiert; neue Tests für Normalisierung, Batch-Ausführung, Caps und Multi-Pattern-Miss-Hints.

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
