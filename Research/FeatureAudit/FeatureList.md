# AiNetLinter Feature Audit — Feature-Liste

Vollständige Eingabeliste für den Research-Agenten.  
**Nicht bearbeiten** — dieser Agent liest diese Datei; geschrieben wird ausschließlich in `temp\` und `Result\`.

**Gesamt:** 46 Features (17 Metriken, 20 Boolean-Regeln, 9 System-Features)

---

## Gruppe M: Numerische Metriken

### M01 — MaxLineCount
- **Aktueller Wert:** 700
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Zeilenanzahl pro `.cs`-Datei
- **Relevante Paper-Cluster:** B, C
- **Result-Datei:** `Result\metrics\M01-MaxLineCount.md`

### M02 — MaxMethodLineCount
- **Aktueller Wert:** 60 (relaxed: 150 via CompoundSuppression wenn CC≤3 und CogC≤5)
- **Severity:** error (warning bei CompoundSuppression)
- **Status:** Aktiv
- **Beschreibung:** Maximale Zeilenanzahl pro Methode
- **Besonderheit:** Komplexeste Wechselwirkung im Tool — CompoundSuppression erlaubt bis 150 Zeilen bei geringer Komplexität (Palomba et al. 2018 wird als Begründung zitiert)
- **Relevante Paper-Cluster:** A, B, C
- **Result-Datei:** `Result\metrics\M02-MaxMethodLineCount.md`

### M03 — MaxMethodParameterCount
- **Aktueller Wert:** 4 (in Tests: 6; `CancellationToken` wird nicht gezählt)
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl Parameter pro Methode. Bei Überschreitung: Parameter-Record empfohlen.
- **Besonderheit:** `MethodParameterCountIgnoreTypeNames` erlaubt Whitelist bestimmter Typen
- **Relevante Paper-Cluster:** A, D, E
- **Result-Datei:** `Result\metrics\M03-MaxMethodParameterCount.md`

### M04 — MaxCyclomaticComplexity
- **Aktueller Wert:** 12
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** McCabe Cyclomatic Complexity pro Methode (Anzahl linear unabhängiger Pfade)
- **Besonderheit:** `ComplexityNearMissTolerance: 1` gibt einen Puffer; Switch-Dispatcher-Cases können ausgenommen werden
- **Relevante Paper-Cluster:** A, C, F
- **Result-Datei:** `Result\metrics\M04-MaxCyclomaticComplexity.md`

### M05 — MaxCognitiveComplexity
- **Aktueller Wert:** 15
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** SonarSource Cognitive Complexity pro Methode — misst Verschachtelungstiefe stärker als McCabe
- **Besonderheit:** Beeinflusst CompoundSuppression (M17) und TestSentinel (M16)
- **Relevante Paper-Cluster:** A, C
- **Result-Datei:** `Result\metrics\M05-MaxCognitiveComplexity.md`

### M06 — MaxInheritanceDepth
- **Aktueller Wert:** 2
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Vererbungstiefe (DIT — Depth of Inheritance Tree), Framework-Basisklassen ausnehmbar
- **Relevante Paper-Cluster:** E, C
- **Result-Datei:** `Result\metrics\M06-MaxInheritanceDepth.md`

### M07 — MaxMethodOverloads
- **Aktueller Wert:** 3
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl gleichnamiger Methoden (Overloads) pro Typ
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\metrics\M07-MaxMethodOverloads.md`

### M08 — MaxConstructorDependencies
- **Aktueller Wert:** 5
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl Constructor-Parameter (Abhängigkeiten). Infrastruktur-Typen (ILogger, IOptions, IConfiguration usw.) werden ignoriert.
- **Relevante Paper-Cluster:** E, C
- **Result-Datei:** `Result\metrics\M08-MaxConstructorDependencies.md`

### M09 — MaxDirectoryDepth
- **Aktueller Wert:** 4
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Verschachtelungstiefe des Projektverzeichnisses
- **Relevante Paper-Cluster:** B, C, E
- **Result-Datei:** `Result\metrics\M09-MaxDirectoryDepth.md`

### M10 — MaxDirectoryChildren
- **Aktueller Wert:** 0 (= deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert
- **Beschreibung:** Maximale Anzahl Dateien und Unterordner pro Verzeichnis. Ausnahmen: `Migrations`, `Generated`, `wwwroot`, `obj`, `bin`, `.git`
- **Relevante Paper-Cluster:** B, C
- **Result-Datei:** `Result\metrics\M10-MaxDirectoryChildren.md`

### M11 — MaxBoolParameterCount
- **Aktueller Wert:** 1
- **Severity:** error
- **Status:** Aktiv (private Methoden ausgenommen; `Try*`-Methoden ausgenommen)
- **Beschreibung:** Maximale Anzahl `bool`-Parameter pro öffentlicher Methode
- **Relevante Paper-Cluster:** D, F
- **Result-Datei:** `Result\metrics\M11-MaxBoolParameterCount.md`

### M12 — MaxPartialClassFiles
- **Aktueller Wert:** 2
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl Dateien in denen eine `partial class` definiert werden darf (0 = deaktiviert)
- **Besonderheit:** `AggregatePartialClassLineCount: false` — Zeilenzählung erfolgt pro Datei, nicht aggregiert
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\metrics\M12-MaxPartialClassFiles.md`

### M13 — MaxPublicMembersPerType
- **Aktueller Wert:** 15
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl öffentlicher Member pro Typ. Ausnahmen: `Extensions`, `Mapper`, `Constants`, `Config`, `Args`.
- **Relevante Paper-Cluster:** E, C
- **Result-Datei:** `Result\metrics\M13-MaxPublicMembersPerType.md`

### M14 — MaxAIContextFootprint
- **Aktueller Wert:** 5000 (transitive Zeilen)
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Misst die transitiven Zeilenanzahl eigener Typen, die ein LLM laden müsste um diese Klasse vollständig zu verstehen (Kopplung × LOC). Einzigartiges Feature — nicht in anderen Lintern vorhanden.
- **Relevante Paper-Cluster:** C, E
- **Result-Datei:** `Result\metrics\M14-MaxAIContextFootprint.md`

### M15 — MaxSwitchArms
- **Aktueller Wert:** 10
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Maximale Anzahl Arms (Cases) in Switch-Expressions und Switch-Statements. Dispatcher-Pattern kann ausgenommen werden.
- **Relevante Paper-Cluster:** A, F
- **Result-Datei:** `Result\metrics\M15-MaxSwitchArms.md`

### M16 — MinCognitiveComplexityForTest
- **Aktueller Wert:** 3
- **Severity:** warning (via TestSentinel)
- **Status:** Aktiv
- **Beschreibung:** Mindest-Cognitive-Complexity einer Klasse/Methode, ab der ein Test (oder `// @covers T`) erzwungen wird
- **Besonderheit:** Teil des TestSentinel-Systems (R08)
- **Relevante Paper-Cluster:** A, G
- **Result-Datei:** `Result\metrics\M16-MinCognitiveComplexityForTest.md`

### M17 — CompoundSuppressions (Mechanismus)
- **Aktueller Wert:** 1 Suppression aktiv: MaxMethodLineCount → 150 wenn CC≤3 ∧ CogC≤5
- **Severity:** konfigurierbar per `SeverityOverride`
- **Status:** Aktiv
- **Beschreibung:** Meta-Feature: Erlaubt kontextabhängige Regelunterdrückung — Regel X gilt mit anderem Grenzwert wenn mehrere Bedingungen gleichzeitig erfüllt sind
- **Besonderheit:** Erhöht Regelkomplexität signifikant; einzigartiges Feature im Tool
- **Relevante Paper-Cluster:** A, B, C
- **Result-Datei:** `Result\metrics\M17-CompoundSuppressions.md`

---

## Gruppe R: Boolean-Regeln

### R01 — EnforceSealedClasses
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv (in `*.Tests`-Projekten deaktiviert)
- **Beschreibung:** Jede konkrete Klasse muss `sealed` sein. Ausnahmen per Suffix: `Base`, `Foundation`, `Host`.
- **Relevante Paper-Cluster:** C, D
- **Result-Datei:** `Result\bool-rules\R01-EnforceSealedClasses.md`

### R02 — AllowDynamic (Verbot)
- **Aktueller Wert:** false (= `dynamic` ist verboten)
- **Severity:** error
- **Status:** Aktiv, kein Opt-out
- **Beschreibung:** Das `dynamic`-Schlüsselwort ist in C# komplett verboten
- **Relevante Paper-Cluster:** C, D
- **Result-Datei:** `Result\bool-rules\R02-AllowDynamic.md`

### R03 — AllowOutParameters (Verbot)
- **Aktueller Wert:** false (= `out`-Parameter sind verboten)
- **Severity:** warning
- **Status:** Aktiv (mit Ausnahmen via R04 und R06)
- **Beschreibung:** `out`-Parameter sind grundsätzlich verboten. Ausnahmen: Try-Pattern (R04) und private Methoden (R06).
- **Relevante Paper-Cluster:** D
- **Result-Datei:** `Result\bool-rules\R03-AllowOutParameters.md`

### R04 — AllowTryPatternOutParameters
- **Aktueller Wert:** true
- **Status:** Aktiv (Ausnahme zu R03)
- **Beschreibung:** `out`-Parameter in Methoden mit `Try`-Präfix sind erlaubt (TryParse-Pattern)
- **Relevante Paper-Cluster:** D
- **Result-Datei:** `Result\bool-rules\R04-AllowTryPatternOutParameters.md`

### R05 — AllowCancellationShutdownCatch
- **Aktueller Wert:** true
- **Status:** Aktiv (Ausnahme zu R13)
- **Beschreibung:** `OperationCanceledException` beim Shutdown und `ObjectDisposedException` dürfen still abgefangen werden
- **Relevante Paper-Cluster:** D, F
- **Result-Datei:** `Result\bool-rules\R05-AllowCancellationShutdownCatch.md`

### R06 — AllowOutParametersInPrivateMethods
- **Aktueller Wert:** true
- **Status:** Aktiv (Ausnahme zu R03)
- **Beschreibung:** `out`-Parameter in privaten Methoden sind erlaubt
- **Relevante Paper-Cluster:** D
- **Result-Datei:** `Result\bool-rules\R06-AllowOutParametersInPrivateMethods.md`

### R07 — EnforceValueObjectContracts
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Klassen mit `*ValueObject`-Suffix müssen `record` oder `readonly struct` sein
- **Relevante Paper-Cluster:** D, E
- **Result-Datei:** `Result\bool-rules\R07-EnforceValueObjectContracts.md`

### R08 — EnableTestSentinel
- **Aktueller Wert:** true
- **Severity:** warning
- **Status:** Aktiv
- **Beschreibung:** Für komplexe Typen (CogC > M16) wird eine Testklasse, `typeof(T)`-Referenz oder `// @covers T` erzwungen
- **Besonderheit:** Exemptions für Extensions, Constants, static classes usw.
- **Relevante Paper-Cluster:** G
- **Result-Datei:** `Result\bool-rules\R08-EnableTestSentinel.md`

### R09 — EnforcePascalCase
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Öffentliche Typen, Methoden und Properties müssen PascalCase verwenden
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\bool-rules\R09-EnforcePascalCase.md`

### R10 — EnforceXmlDocumentation
- **Aktueller Wert:** false (deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert — in `.mdc` explizit als "nicht erzwingen" gelistet
- **Beschreibung:** Würde XML-Kommentare (`///`) auf allen öffentlichen Membern erzwingen
- **Relevante Paper-Cluster:** C, D
- **Result-Datei:** `Result\bool-rules\R10-EnforceXmlDocumentation.md`

### R11 — EnforceSemanticNaming
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Verbietet bedeutungslose Namen (`data`, `temp`, `obj`, `result`, `info`, `value`) in öffentlichen Signaturen. Substring-Matching erlaubt.
- **Besonderheit:** Exempt: `Equals`, `CompareTo`, `GetHashCode`
- **Relevante Paper-Cluster:** D, C, F
- **Result-Datei:** `Result\bool-rules\R11-EnforceSemanticNaming.md`

### R12 — EnforceNullableEnable
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Jede `.cs`-Datei muss `#nullable enable` am Anfang haben
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\bool-rules\R12-EnforceNullableEnable.md`

### R13 — EnforceNoSilentCatch
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Leere `catch`-Blöcke sind verboten. Pflicht: Log + sichtbarer Fehler oder `throw;`. Ausnahmen via `AllowedSilentCatchExceptionTypes` und R05.
- **Relevante Paper-Cluster:** D, F, C
- **Result-Datei:** `Result\bool-rules\R13-EnforceNoSilentCatch.md`

### R14 — EnforceMinimalApiAsParameters
- **Aktueller Wert:** false (deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert
- **Beschreibung:** ASP.NET Minimal API: Parameter aus HttpContext müssen als explizite Methodenparameter deklariert werden
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\bool-rules\R14-EnforceMinimalApiAsParameters.md`

### R15 — EnforceResultPatternOverExceptions
- **Aktueller Wert:** false (deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert — explizit in `.mdc` als "nicht erzwingen" gelistet
- **Beschreibung:** Erzwingt `Result<T>`-Pattern anstelle von Exceptions für Kontrollfluss
- **Besonderheit:** Ausnahmen für Infrastructure, Endpoints, Middleware, Program; CatchRethrow erlaubt
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\bool-rules\R15-EnforceResultPatternOverExceptions.md`

### R16 — EnforceExplicitStateImmutability
- **Aktueller Wert:** false (deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert
- **Beschreibung:** Klassen mit State müssen explizit als immutable deklariert werden (`readonly`, `record`, `init`-only Properties)
- **Relevante Paper-Cluster:** D, E, C
- **Result-Datei:** `Result\bool-rules\R16-EnforceExplicitStateImmutability.md`

### R17 — PreventContextDependentOverloads
- **Aktueller Wert:** false (deaktiviert)
- **Severity:** error (wenn aktiviert)
- **Status:** Deaktiviert
- **Beschreibung:** Verbietet Overloads die sich nur durch Kontext-Typen (HttpContext, DbContext usw.) unterscheiden
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\bool-rules\R17-PreventContextDependentOverloads.md`

### R18 — EnforceNamespaceDirectoryMapping
- **Aktueller Wert:** true (Modus: `suffix-match`, mind. 2 trailing Segmente)
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Namespace muss dem Verzeichnispfad entsprechen. Ignorierte Segmente: `src`, `Source`, `Domains`, `Handlers`.
- **Relevante Paper-Cluster:** E, C
- **Result-Datei:** `Result\bool-rules\R18-EnforceNamespaceDirectoryMapping.md`

### R19 — DetectAndBanPhantomDependencies
- **Aktueller Wert:** true
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Verbietet nicht auflösbare `using`-Direktiven und `Type.GetType`/`Activator.CreateInstance` für App-Typen (häufigste Halluzinations-Quelle in LLM-generiertem Code)
- **Relevante Paper-Cluster:** C, D
- **Result-Datei:** `Result\bool-rules\R19-DetectAndBanPhantomDependencies.md`

### R20 — BanPublicNestedTypes
- **Aktueller Wert:** true (private Nested Types erlaubt)
- **Severity:** error
- **Status:** Aktiv
- **Beschreibung:** Öffentliche verschachtelte Typen (Klassen, Records, Enums innerhalb anderer Typen) sind verboten
- **Relevante Paper-Cluster:** D, C, E
- **Result-Datei:** `Result\bool-rules\R20-BanPublicNestedTypes.md`

---

## Gruppe F: System- und CLI-Features

### F01 — Baseline / Ratchet-Mechanismus
- **CLI-Flag:** `--baseline <pfad>`
- **Status:** Vorhanden
- **Beschreibung:** Friert bestehende Verstöße per SHA-256 ein. Nur geänderte Dateien werden gegen neue Regeln geprüft. Erlaubt sofortiges Onboarding in Legacy-Projekten.
- **Relevante Paper-Cluster:** C (Vergleich mit anderen Linter-Tools)
- **Result-Datei:** `Result\features\F01-Baseline-Ratchet.md`

### F02 — Auto-Fix
- **CLI-Flags:** `--fix`, `--dry-run`
- **Status:** Vorhanden
- **Beschreibung:** Automatische Korrektur trivialer Verstöße: `sealed` hinzufügen, `#nullable enable` einfügen, PascalCase korrigieren
- **Relevante Paper-Cluster:** C, D
- **Result-Datei:** `Result\features\F02-AutoFix.md`

### F03 — Discovery-Commands
- **CLI-Flags:** `--list-rules`, `--describe-rule <name>`, `--docs <name>`
- **Status:** Vorhanden
- **Beschreibung:** Erlaubt einem LLM-Agenten das Tool explorativ zu verstehen und eigenständig in ein Projekt zu integrieren — ohne Vorab-Konfiguration durch den Entwickler
- **Relevante Paper-Cluster:** C
- **Result-Datei:** `Result\features\F03-Discovery.md`

### F04 — ProjectOverrides
- **Konfiguration:** `rules.json → ProjectOverrides`
- **Status:** Vorhanden (aktives Beispiel: `*.Tests` mit lockeren Limits)
- **Beschreibung:** Projektscharfe Regelabweichungen per Glob-Pattern
- **Relevante Paper-Cluster:** D, E
- **Result-Datei:** `Result\features\F04-ProjectOverrides.md`

### F05 — PathOverrides
- **Konfiguration:** `rules.json → PathOverrides`
- **Status:** Vorhanden, aktuell leer
- **Beschreibung:** Pfadbezogene Regelabweichungen, granularer als ProjectOverrides
- **Relevante Paper-Cluster:** D
- **Result-Datei:** `Result\features\F05-PathOverrides.md`

### F06 — UiSeparation (Blazor / WPF)
- **Konfiguration:** `rules.json → UiSeparation`
- **Status:** Vorhanden
- **Beschreibung:** Erzwingt Code-Behind-Trennung für Blazor (`.razor.cs`) und minimalen Code-Behind für WPF; optional CSS-Isolation
- **Relevante Paper-Cluster:** D, C
- **Result-Datei:** `Result\features\F06-UiSeparation.md`

### F07 — FileFilters
- **Konfiguration:** `rules.json → FileFilters`
- **Status:** Vorhanden
- **Beschreibung:** Ausschluss generierter Dateien (`*.g.cs`, `*.generated.cs`, `AssemblyInfo.cs`) und Verzeichnisse (`obj/`, `bin/`) sowie Klassen mit `[GeneratedCode]`-Attribut
- **Relevante Paper-Cluster:** D
- **Result-Datei:** `Result\features\F07-FileFilters.md`

### F08 — ForbiddenNamespaceDependencies
- **Konfiguration:** `rules.json → ForbiddenNamespaceDependencies`
- **Status:** Vorhanden, aktuell leer (keine Verbote konfiguriert)
- **Beschreibung:** Erlaubt das Verbieten von Namespace-Abhängigkeiten (z.B. Infrastructure darf nicht Domain direkt importieren — Onion/Clean-Architecture-Erzwingung)
- **Relevante Paper-Cluster:** E, C
- **Result-Datei:** `Result\features\F08-ForbiddenNamespaceDependencies.md`

### F09 — EnablePerformanceProfiling
- **Konfiguration:** `rules.json → Global.EnablePerformanceProfiling: true`
- **Status:** Aktiv
- **Beschreibung:** Schreibt Performance-Messungen pro Lint-Lauf in `measurements/` — hilft Bottlenecks im Linter selbst zu identifizieren
- **Relevante Paper-Cluster:** (kein Paper-Cluster — rein internes Tool-Feature)
- **Result-Datei:** `Result\features\F09-EnablePerformanceProfiling.md`
