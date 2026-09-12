# AiNetLinter — Linter-Konfigurationsreferenz

→ [CLI-Referenz & Workflows](cli.md) | [Projektintegration](integration.md) | [Design-Rationale](../rationale.md) | [MCP-Server](../mcp/server.md) | [README](../../README.md)

---
## 1. Der "AI-Mittelweg" für DRY vs. WET

Die klassische Regel **DRY** (Don't Repeat Yourself) führt bei extremem Einsatz zu tiefen, generischen Abstraktionen, die für KIs schwer verständlich sind und den sogenannten "Schmetterlingseffekt" (Änderung an einer Stelle bricht unbemerkt 10 andere Stellen) begünstigen. `AiNetLinter` unterstützt einen pragmatischen Mittelweg:

1.  **Fachliches DRY (Strikt):** Kern-Geschäftslogik und Berechnungen müssen zentral und wiederverwendbar sein (z. B. in Domain-Modellen oder Services). Die KI muss diese Logik nur an einem einzigen Ort ändern.
2.  **Technisches WET (Erlaubt):** Controller, DTOs, Mapper und Queries dürfen redundant bzw. spezifisch pro Use Case (Vertical Slice) aufgebaut sein. Dies minimiert Seiteneffekte und verhindert, dass die KI riesige, geteilte Basisklassen anpassen muss und dabei andere Features beschädigt.

---

## 2. Kernfeatures

- **Roslyn-basierte semantische Analyse:** Evaluierung der gesamten Solution (.sln / .slnx) über einen einzigen Syntax-Walk pro Dokument. Nutzt echte Semantik-Informationen statt textbasierter Heuristiken. MSBuild Design-Time-Properties beschleunigen das Solution-Laden; die Dokument-Analyse läuft parallel bis `Environment.ProcessorCount`.
- **Feingranulares Regelwerk:** Regeln für Klassendesign (Sealed, Value Objects, Vererbungstiefe), Variablen/Typen (kein `dynamic`, keine `out`-Parameter, Nullable Context) und Code-Komplexität (McCabe, SonarSource).
- **PascalCase- & Namensvalidierung:** Typprüfung auf PascalCase-Konventionen sowie Erkennung nicht-semantischer Bezeichner (z. B. `data`, `temp`, `obj`).
- **LSP-Dokumentationstests:** Erzwingt die Verwendung von XML-Docs (`/// <summary>`) auf öffentlichen APIs.
- **Static Test Sentinel:** Statische Test-Präsenzprüfung für komplexe Quellcodeabschnitte anhand von Metadaten-Scans auf referenzierte Testbibliotheken (xunit, nunit etc.).
- **Namespace-Abhängigkeitsprüfung (Vertical Slices):** Verhindert unerlaubte slice-übergreifende Abhängigkeiten, auch bei vollqualifizierten Typnamen.
- **Warnungs-Unterdrückung (Suppression):** Flexibles Deaktivieren von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]`, dateiweit oder komplett per `// ainetlinter-disable all`.
- **Gezielte Bulk-Suppression (`--add-disable-all` / `--remove-disable-all`):** Audit-basiertes Einfügen des Disable-all-Kommentars nur in Dateien mit Verstößen sowie sicheres Entfernen exakter Disable-all-Zeilen.
- **Baseline-Ratchet (Checksum):** Inkrementelle Migration bestehender Codebases — unveränderte Dateien werden per SHA-256 eingefroren, Verstöße nur in geänderten Dateien gemeldet.
- **Projekt-spezifische Regel-Konfiguration (Project Overrides):** Flexibles Überschreiben oder Deaktivieren von Linter-Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) in der Konfiguration.
- **AI-Context-Footprint (Metrik):** Berechnet die Summe aller Codezeilen einer Klasse inklusive aller transitiv referenzierten eigenen Typen, um hohe Kopplung und große Kontext-Footprints für KIs zu vermeiden.
- **Roslyn-basierter CLI Auto-Fixer (`--fix`):** Vollautomatische Behebung trivialer Linter-Verstöße (z. B. fehlendes `sealed`, `readonly` oder `#nullable enable`) über Syntaxbaum-Transformationen.
- **Analyse-Cache (Inkrementelle Optimierung):** Cache zur Vermeidung wiederholter semantischer Analysen für unveränderte C#-Dateien. Reduziert die Ausführungszeit bei inkrementellen Agenten-Runs. Standardmäßig aktiv; deaktivierbar über `--no-cache`.
- **Performance-Profiling & Zeitmessung:** Erfassung der Ausführungszeiten aller Linter-Phasen (Workspace-Laden, Dateianalyse, Post-Checks) und automatische Generierung strukturierter Berichte (`performance.log` & `performance.json`) unter `measurements/` zur Analyse von Performance-Engpässen.
## 3. Konfiguration (`ainetlinter-rules.json`)

Die Konfiguration erfolgt über eine flache JSON-Struktur. Beispiel einer vollständigen Konfiguration:

> **Hinweis:** Dieses Beispiel ist ein bewusst strenges Profil und zeigt keine Code-Defaults. Die tatsächlichen Code-Defaults gelten ohne eigene `ainetlinter-rules.json` und stehen ausschließlich in der Regel-Tabelle unten.

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": false,
    "SealedClassExemptSuffixes": ["Base", "Foundation", "Host"],
    "AllowDynamic": false,
    "AllowOutParameters": false,
    "EnforceValueObjectContracts": true,
    "EnableTestSentinel": true,
    "EnforcePascalCase": true,
    "EnforceXmlDocumentation": true,
    "EnforceSemanticNaming": true,
    "EnforceNullableEnable": true,
    "EnforceNoSilentCatch": true,
    "AllowTryPatternOutParameters": true,
    "AllowCancellationShutdownCatch": true,
    "EnforceMinimalApiAsParameters": false,
    "EnforceResultPatternOverExceptions": true,
    "EnforceExplicitStateImmutability": true,
    "AllowedExceptions": [
      "ArgumentException",
      "ArgumentNullException",
      "ArgumentOutOfRangeException",
      "InvalidOperationException",
      "NotSupportedException",
      "KeyNotFoundException",
      "IndexOutOfRangeException",
      "TimeoutException",
      "ObjectDisposedException",
      "NotImplementedException"
    ],
    "PreventContextDependentOverloads": true,
    "EnforceNamespaceDirectoryMapping": true,
    "DetectAndBanPhantomDependencies": true,
    "BanPublicNestedTypes": true,
    "BanPublicNestedTypesAllowPrivate": true,
    "ImmutabilityExemptSuffixes": [
      "Dto",
      "Entity",
      "Model",
      "Request",
      "Response",
      "Command"
    ]
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxMethodLineCount": 42,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5,
    "MaxInheritanceDepth": 3,
    "InheritanceDepthFrameworkPrefixes": [
      "System.",
      "Microsoft.UI.",
      "System.Windows."
    ],
    "MinCognitiveComplexityForTest": 5,
    "AggregatePartialClassLineCount": false,
    "MaxMethodOverloads": 5,
    "MaxConstructorDependencies": 5,
    "MaxDirectoryDepth": 4,
    "MaxDirectoryChildren": 0,
    "MaxDirectoryChildrenExemptNames": [
      "Migrations",
      "Generated",
      "wwwroot",
      "obj",
      "bin",
      ".git",
      "tasks"
    ],
    "MaxBoolParameterCount": 1,
    "MaxBoolParameterCountAllowPrivate": true,
    "MaxBoolParameterCountExemptMethodPrefixes": ["Try"],
    "MaxPartialClassFiles": 2,
    "MaxPartialClassFilesExemptTypes": [],
    "MaxPublicMembersPerType": 15,
    "MaxPublicMembersPerTypeApplyToTestFiles": false,
    "MaxPublicMembersPerTypeExemptSuffixes": [
      "Extensions",
      "Mapper",
      "Constants",
      "Config",
      "Args",
      "ConfigOverride"
    ],
    "MaxAIContextFootprint": 2500,
    "MaxSwitchArms": 10,
    "MaxSwitchArmsExcludeDispatcher": true,
    "MaxSwitchArmsExemptTypes": [],
    "ExcludeNullCoalescingInitializerComplexity": true,
    "NullCoalescingInitializerMaxNonCoalescingRatio": 0.0
  },
  "TestSentinel": {
    "ClassNamePatterns": [
      "{Name}Tests",
      "{Name}Test",
      "{Name}IntegrationTests",
      "{Name}*Tests"
    ],
    "RecognizeTypeofReference": true,
    "RecognizeCoversComment": true,
    "ExemptClassNameSuffixes": [
      "Extensions",
      "Constants",
      "Converter",
      "Profile",
      "Seed",
      "Migration",
      "Startup",
      "Module"
    ],
    "ExemptWhenInheritsFrom": ["ComponentBase", "IValueConverter", "Profile"],
    "ExemptStaticClasses": true
  },
  "RuleMetadata": {
    "MaxLineCount": { "severity": "error", "intent": "agent-context" },
    "StaticTestSentinel": { "severity": "warning", "intent": "test-coverage" }
  },
  "ForbiddenNamespaceDependencies": [
    {
      "SourceNamespace": "MyFeature.Domain",
      "TargetNamespace": "MyFeature.Infrastructure"
    }
  ]
}
```

### Erklärung der Regeln

| Regel                                            | Bereich | Beschreibung                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| :----------------------------------------------- | :------ | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EnforceSealedClasses`                           | Global  | Zwingt alle konkreten Klassen dazu, als `sealed` deklariert zu werden.                                                                                                                                                                                                                                                                                                                                                                                             |
| `AllowUnsealedPartialClasses`                    | Global  | Erlaubt es, `partial` Klassen unsealed zu lassen (Standard: `false`, nützlich z. B. bei WPF Code-Behind oder Blazor Page-Components).                                                                                                                                                                                                                                                                                                                              |
| `SealedClassExemptSuffixes`                      | Global  | Liste von Klassenname-Suffixen, die von der `EnforceSealedClasses`-Prüfung ausgenommen sind (z. B. `["Base", "Foundation", "Host"]`).                                                                                                                                                                                                                                                                                                                              |
| `AllowDynamic`                                   | Global  | Verbietet das Typschlüsselwort `dynamic` (verhindert statische Analyse-Lücken).                                                                                                                                                                                                                                                                                                                                                                                    |
| `AllowOutParameters`                             | Global  | Verbietet `out`-Parameter zugunsten von C#-Tuples oder Records.                                                                                                                                                                                                                                                                                                                                                                                                    |
| `AllowTryPatternOutParameters`                   | Global  | Erlaubt `out`-Parameter in folgenden idiomatischen Mustern (Standard: `true`): `bool Try*`- und `bool Is*`-Methoden; `string? Try*`-Methoden (Error-String-Muster: `null` = Erfolg, non-null = Fehlermeldung); `void Deconstruct(out ...)`-Methoden (C#-Sprachmuster); lokale Funktionen mit denselben Konventionen; Methoden, die ein Interface-Mitglied implementieren oder eine abstrakte Methode überschreiben (die Signatur ist dann vom Vertrag vorgegeben). |
| `AllowOutParametersInPrivateMethods`             | Global  | Wenn `true`: `out`-Parameter in privaten Methoden werden von `AllowOutParameters` nicht gemeldet. Nützlich in Projekten mit no-DI-Architektur, die private Zerlegungshelfer intern nutzen. Öffentliche und `protected`/`internal` Methoden werden weiterhin geprüft. Standard: `true`.                                                                                                                                                                             |
| `AllowCancellationShutdownCatch`                 | Global  | Erlaubt stummes Abfangen von Cancellation-Exceptions (wie `OperationCanceledException` oder `TaskCanceledException`) bei Host-Shutdown (ohne Pflicht eines `when`-Filters).                                                                                                                                                                                                                                                                                        |
| `AllowedSilentCatchExceptionTypes`               | Global  | Liste von Exception-Typen (einfacher Name, kein Namespace), die lautlos abgefangen werden dürfen — z. B. `["JSDisconnectedException"]` für Blazor-Dispose-Methoden. Analogon zu `AllowCancellationShutdownCatch` für projektspezifische Typen. Standard: `["ObjectDisposedException"]`.                                                                                                                                                                                              |
| `EnforceMinimalApiAsParameters`                  | Global  | Prüft Minimal-API-Endpunkte auf fehlendes `[AsParameters]` bei >4 Parametern (opt-in).                                                                                                                                                                                                                                                                                                                                                                             |
| `EnforceValueObjectContracts`                    | Global  | Zwingt Klassen mit Suffix `ValueObject` dazu, als `record` oder `readonly struct` deklariert zu sein und nur unveränderliche Eigenschaften (ohne `set`) zu haben.                                                                                                                                                                                                                                                                                                  |
| `EnableTestSentinel`                             | Global  | Aktiviert den Test-Präsenzwächter für komplexe Quellcodedateien.                                                                                                                                                                                                                                                                                                                                                                                                   |
| `EnforcePascalCase`                              | Global  | Validiert PascalCase-Schreibweise für Klassen, Structs, Records, Interfaces, Methoden und Properties.                                                                                                                                                                                                                                                                                                                                                              |
| `EnforceAsciiIdentifiers`                        | Global  | Zwingt alle Bezeichner (Klassen, Structs, Records, Interfaces, Enums, Enum-Mitglieder, Methoden, Eigenschaften, Felder, Parameter, lokale Funktionen und Namespaces) dazu, ausschließlich aus ASCII-Zeichen (a-z, A-Z, 0-9, Unterstrich) zu bestehen (Standard: `true`).                                                                                                                                                                                           |
| `EnforceXmlDocumentation`                        | Global  | Erzwingt XML-Dokumentationskommentare an öffentlichen Typ-Deklarationen (Klassen/Interfaces) (Standard: `false`).                                                                                                                                                                                                                                                                                                                                                  |
| `EnforceSemanticNaming`                          | Global  | Markiert generische Parameternamen (z. B. `data`, `temp`, `val`) in oeffentlichen Methoden sowie werkzeug-generierte Dummy-Namen (z. B. `MyRegex`, `NewMethod`, `Class1`) auf allen Deklarationen (auch in Tests) als Fehler. |
| `SemanticNamingExemptMethodNames`                | Global  | Methoden-Namen, für die `EnforceSemanticNaming` nicht geprüft wird. Standard: `["Equals", "CompareTo", "GetHashCode"]` (BCL-Overrides, bei denen Parameternamen wie `obj` konventionell sind). Erweiterbar für projektspezifische Muster.                                                                                                                                                                                                                          |
| `SemanticNamingAllowSubstringOfMethodName`       | Global  | Wenn `true`: Ein Parameter-Name wird nicht gemeldet, wenn er als Teilstring (case-insensitiv) im Methoden-Namen vorkommt. Beispiel: Parameter `item` in Methode `AppendTimelineItemAsync` → nicht flaggen. Standard: `true`.                                                                                                                                                                                                                                       |
| `EnforceNullableEnable`                          | Global  | Stellt sicher, dass `#nullable enable` in jeder Datei deklariert ist oder global über csproj erzwungen wird.                                                                                                                                                                                                                                                                                                                                                       |
| `EnforceNoSilentCatch`                           | Global  | Verbietet stumme `catch`-Blöcke. Ein Catch-Block gilt als stumm (verschluckt), wenn er leer ist und weder `throw`, Methodenaufrufe (Invocations), Rückgabeanweisungen (`return`) noch Zuweisungen (`assignment`) an Felder/Eigenschaften enthält. Variable Namen, die mit `ignored` oder `expected` beginnen (z. B. `catch (Exception ignored)`), oder der Inline-Kommentar `// ainetlinter-disable EnforceNoSilentCatch` deaktivieren die Prüfung.                |
| `BanAsyncVoid`                                   | Global  | Verbietet `async void`-Methoden und lokale Funktionen.                                                                                                                                                                                                                                                                                                                                                                                                             |
| `AsyncVoidAllowEventHandlers`                    | Global  | Ermöglicht die Ausnahme von Event-Handlern mit Signatur `(object sender, EventArgs e)` von der `BanAsyncVoid`-Regel.                                                                                                                                                                                                                                                                                                                                               |
| `BanBlockingTaskAccess`                          | Global  | Verbietet blockierende Task-Zugriffe (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`).                                                                                                                                                                                                                                                                                                                                                                          |
| `BanBlockingTaskAccessAllowInMain`               | Global  | Erlaubt blockierende Task-Zugriffe in statischen `Main` Methoden.                                                                                                                                                                                                                                                                                                                                                                                                  |
| `BanBlockingTaskAccessAllowInTests`              | Global  | Erlaubt blockierende Task-Zugriffe in Test-Projekten.                                                                                                                                                                                                                                                                                                                                                                                                              |
| `EnableDuplicateCodeCheck`                       | Global  | Aktiviert die solution-weite Duplicate-Code-Erkennung (Token-CPD, siehe eigener Abschnitt `DuplicateCode` unten). Meldet `exact`-Cluster als Regelverstoß `DuplicateCode` (ein Fund pro Cluster).                                                                                                                                                                                                                                                               |
| `EnforceResultPatternOverExceptions`             | Global  | Verbietet `throw` für fachlichen Kontrollfluss. Technische Standard-Exceptions (wie `ArgumentNullException`) sind für Fail-Fast erlaubt.                                                                                                                                                                                                                                                                                                                           |
| `ResultPatternAllowThrowInNamespaceSuffixes`     | Global  | Namespace-Suffixe, für die `throw` explizit erlaubt ist (z. B. `["Infrastructure", "Middleware"]`). Segment-basierter Match: `MyApp.Infrastructure` endet mit `.Infrastructure`. Standard: `["Infrastructure", "Endpoints", "Middleware", "Program"]`.                                                                                                                                                                                                                                                                   |
| `ResultPatternAllowCatchRethrow`                 | Global  | Bare `throw;` (Rethrow in einem Catch-Block ohne erneut zu konstruieren) ist immer erlaubt wenn `true`. Standard: `true`.                                                                                                                                                                                                                                                                                                                                          |
| `EnforceExplicitStateImmutability`               | Global  | Zwingt alle Klassen (außer DTOs/Entities) zu Immutabilität (init/get-only Eigenschaften und private readonly Felder).                                                                                                                                                                                                                                                                                                                                              |
| `ImmutabilityExemptBaseTypes`                    | Global  | Liste von Basisklassen oder Schnittstellen, von denen erbende/implementierende Klassen vollständig von der Immutability-Prüfung ausgenommen sind. Standard: `["ComponentBase", "LayoutComponentBase", "ObservableObject", "ObservableRecipient", "BackgroundService", "AuthenticationStateProvider", "INotifyPropertyChanged"]`.                                                                                                                                                                                                                                                                  |
| `ImmutabilityAllowPrivateBackingFields`          | Global  | Erlaubt private mutable Felder mit Unterstrich (`_`) Präfix (z. B. typische WPF MVVM Backing-Felder) (Standard: `true`).                                                                                                                                                                                                                                                                                                                                          |
| `ImmutabilityExemptPatterns`                     | Global  | Wildcard-Muster (`*Suffix`, `Prefix*`, `*Teil*` oder exakter Name) gegen den Klassennamen, die eine Klasse von `EnforceExplicitStateImmutability` ausnehmen. Standard: `[]`.                                                                                                                                                                                                                                                                                       |
| `AllowedEmptyReads`                              | Global  | Schaltet die Regel `AllowedEmptyReads` frei (Standard: `false`). Laut Regel-Metadaten: Leseoperationen ohne unmittelbaren Guard sind bei `true` verboten.                                                                                                                                                                                                                                                                                                          |
| `PreventContextDependentOverloads`               | Global  | Verbietet Methodenüberladungen, die sich nur durch primitive Typen bei gleicher Parameteranzahl unterscheiden.                                                                                                                                                                                                                                                                                                                                                     |
| `EnforceNamespaceDirectoryMapping`               | Global  | Stellt sicher, dass deklarierte Namespaces exakt der physischen Ordnerstruktur entsprechen.                                                                                                                                                                                                                                                                                                                                                                        |
| `DetectAndBanPhantomDependencies`                | Global  | Verbietet die Einbindung nicht auflösbarer Namespaces sowie dynamische Reflection-Lade-APIs. Meldet ein unauflösbares `using` NICHT, wenn das enthaltende Projekt erkennbare Lade-Probleme hat (aktuell: fehlender/veralteter `dotnet restore`, siehe `ProjectRestoreState`) — sonst würde ein fehlender Restore als tausende Einzel-Violations statt als ein einziges `PROJECT_NOT_RESTORED`-Log-Diagnostic erscheinen. Details: `rationale.md` §13.               |
| `BanPublicNestedTypes`                           | Global  | Verbietet `public` und `internal` nested Typen (Klassen, Structs, Records, Enums) innerhalb anderer Typen. Verbessert die Grep-/File-Listing-Navigation für KI-Agenten und verhindert FQN-Halluzinationen (`PaymentStatus` statt `PaymentProcessor.PaymentStatus`). Standard: `true`. Severity: `error`, Intent: `agent-context`.                                                                                                                                  |
| `BanPublicNestedTypesAllowPrivate`               | Global  | Wenn `true` (Standard): `private` nested Typen bleiben erlaubt, da sie kein externes Grep-Target für Agenten darstellen. Auf `false` setzen, um auch private nested Typen zu melden (strikter Greenfield-Modus).                                                                                                                                                                                                                                                   |
| `EnablePerformanceProfiling`                     | Global  | Aktiviert die automatisierte Laufzeit-Messung aller Linter-Phasen und Dateianalysen (Standard: `true`). Erzeugt dauerhaft `measurements/`-Dateien im Projektverzeichnis; über `EnablePerformanceProfiling: false` deaktivierbar.                                                                                                                                                                                                                                              |
| `MaxLineCount`                                   | Metrics | Maximale Zeilenanzahl pro Datei (Standard: 700).                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `MaxMethodParameterCount`                        | Metrics | Maximale Parameteranzahl pro Methode (Standard: 4). `override`-Methoden und explizite/implizite Interface-Implementierungen sind ausgenommen, da ihre Signatur nicht geändert werden kann.                                                                                                                                                                                                                                                                         |
| `MaxMethodParameterCountInTestFiles`             | Metrics | Separater Grenzwert für Testdateien (Standard: 0 = gleicher Grenzwert wie `MaxMethodParameterCount`). Empfehlung: 6–8, da Test-Arrange-Helfer naturgemäß breiter sind.                                                                                                                                                                                                                                                                                             |
| `MethodParameterCountIgnoreTypeNames`            | Metrics | Typ-Namen (einfacher Name, kein Namespace), die beim Zählen der Parameter nicht berücksichtigt werden. Standard: `["CancellationToken"]`.                                                                                                                                                                                                                                                                                      |
| `MethodParameterCountIgnoreTypePrefixes`         | Metrics | Typ-Name-Präfixe, die beim Zählen der Parameter-Anzahl ignoriert werden. Ermöglicht z. B. `["ILogger"]` um `ILogger<T>` auszuschließen. Standard: `[]`.                                                                                                                                                                                                                                                                                                            |
| `MaxMethodParameterCountAllowPrivate`            | Metrics | Wenn `true`: `private` und `protected` Methoden werden vom Parameteranzahl-Check vollständig ausgenommen. Standard: `false`.                                                                                                                                                                                                                                                                                                                                       |
| `MaxMethodParameterCountForNonPublic`            | Metrics | Relaxiertes Limit für `private`/`protected` Methoden (Standard: `6`). `0` = gleicher Grenzwert wie `MaxMethodParameterCount`. Ignoriert wenn `MaxMethodParameterCountAllowPrivate: true`.                                                                                                                                                                                                                                                                          |
| `MaxMethodLineCount`                             | Metrics | Maximale Codezeilenanzahl pro Methode ohne Kommentare/Leerzeilen (Standard: 60).                                                                                                                                                                                                                                                                                                                                                                                   |
| `MaxCyclomaticComplexity`                        | Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode (Standard: 12).                                                                                                                                                                                                                                                                                                                                                                                             |
| `MaxCognitiveComplexity`                         | Metrics | Maximale kognitive Komplexität (SonarSource) pro Methode (Standard: 15).                                                                                                                                                                                                                                                                                                                                                                                            |
| `ExcludeSwitchDispatcherCases`                   | Metrics | Wenn `true` (Standard): Methoden, die als Switch-Dispatcher erkannt werden, sind von `MaxCyclomaticComplexity` und `MaxCognitiveComplexity` ausgenommen. Nicht zu verwechseln mit `MaxSwitchArmsExcludeDispatcher` (betrifft `MaxSwitchArms`).                                                                                                                                                                                                                     |
| `SwitchDispatcherMaxCaseBodyLines`                | Metrics | Maximale Code-Zeilenanzahl pro Case-/If-Zweig, damit dieser Zweig als Dispatcher-Zweig zählt (Standard: 3). Wirkt zusammen mit `ExcludeSwitchDispatcherCases`.                                                                                                                                                                                                                                                                                                      |
| `MaxInheritanceDepth`                            | Metrics | Maximale Tiefe der Vererbungshierarchie (Standard: 2). Framework-Basisklassen (ASP.NET, EF Core, xUnit) können über `InheritanceDepthFrameworkPrefixes` ausgenommen werden.                                                                                                                                                                                                                                                                                        |
| `InheritanceDepthFrameworkPrefixes`              | Metrics | Namespace-Präfixe von Framework-Basistypen, die beim Zählen der Vererbungstiefe ignoriert werden (z. B. `["System.", "System.Windows."]`).                                                                                                                                                                                                                                                                                                                         |
| `MinCognitiveComplexityForTest`                  | Metrics | Schwellenwert der kognitiven Komplexität, ab dem der Test Sentinel eine zugehörige Testklasse einfordert (Standard: 3). Niedrigere Werte erhöhen die Warning-Dichte; empfohlen: 5–7.                                                                                                                                                                                                                                                                               |
| `AggregatePartialClassLineCount`                 | Metrics | Summiert Zeilenanzahl über alle `partial`-Teile eines Typs (opt-in).                                                                                                                                                                                                                                                                                                                                                                                               |
| `MaxMethodOverloads`                             | Metrics | Maximale Anzahl von Methoden-Überladungen pro Name in einer Klasse (Standard: 3). Erlaubt gängige .NET-Patterns (mit/ohne `CancellationToken`, mit/ohne `IProgress` etc.); ab 4+ Überladungen ist ein Parameter-Object die bessere Wahl.                                                                                                                                                                                                                           |
| `MaxConstructorDependencies`                     | Metrics | Maximale Parameter-Anzahl pro Konstruktor / Primärkonstruktor (Standard: 5). Records und Structs, bei denen **alle** Parameter Default-Werte haben, werden automatisch ausgenommen (Options/Config-Objects).                                                                                                                                                                                                                                                       |
| `ConstructorDependencyIgnoreTypePrefixes`        | Metrics | Typ-Name-Präfixe von Framework- oder Cross-Cutting-Abhängigkeiten, die bei `MaxConstructorDependencies` nicht mitgezählt werden (z. B. `["ILogger", "IOptions"]`).                                                                                                                                                                                                                                                                                                 |
| `ConstructorDependencyExemptClassSuffixes`       | Metrics | Klassen-Name-Suffixe, für die `MaxConstructorDependencies` komplett übersprungen wird. Typisch: `["Exception"]` — Exception-Typen haben Payload-Parameter, keine DI-Abhängigkeiten.                                                                                                                                                                                                                                                                                |
| `MaxDirectoryDepth`                              | Metrics | Maximale Ordnertiefe ab csproj-Ebene (Standard: 4).                                                                                                                                                                                                                                                                                                                                                                                                                |
| `MaxDirectoryChildren`                           | Metrics | Maximale Anzahl von Einträgen (Dateien + Unterordner) in einem Verzeichnis (Standard: 0 = deaktiviert). `MaxDirectoryChildrenExemptNames`: Ordnernamen, die ausgenommen werden (Standard enthält u. a. `["Migrations", "Generated", "wwwroot", "obj", "bin", ".git", "tasks"]`).                                                                                                                                                                                                                 |
| `MaxBoolParameterCount`                          | Metrics | Maximale Anzahl von `bool`-Parametern pro Methode oder Konstruktor (Standard: 1). `0` = deaktiviert. `MaxBoolParameterCountAllowPrivate`: Wenn `true`, werden `private`/`protected` Methoden ausgenommen (Standard: `true`). `MaxBoolParameterCountExemptMethodPrefixes`: Methoden-Präfixe, die ausgenommen werden (Standard: `["Try"]`).                                                                                                                               |
| `MaxPartialClassFiles`                           | Metrics | Maximale Anzahl von `partial`-Deklarationsdateien pro Typ (Standard: 2). `0` = deaktiviert. Guidance: Unter-Logik in eigenständige Klassen (z. B. `XyzChecker`) auslagern. `MaxPartialClassFilesExemptTypes`: vollqualifizierte oder einfache Typnamen, die ausgenommen werden (Standard: `[]`).                                                                                                         |
| `MaxPublicMembersPerType`                        | Metrics | Maximale Anzahl öffentlicher Member (Methoden, Properties, Felder, Events) pro Typ (Standard: 15). `0` = deaktiviert. Testdateien werden standardmäßig übersprungen, es sei denn `MaxPublicMembersPerTypeApplyToTestFiles: true` ist gesetzt (Opt-in). `MaxPublicMembersPerTypeExemptSuffixes`: Klassenname-Suffixe, für die die Prüfung übersprungen wird (Standard: `["Extensions", "Mapper", "Constants", "Config", "ConfigOverride", "Args"]`). |
| `MaxAIContextFootprint`                          | Metrics | Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten (Standard: 5000). Oberhalb von ~2.500 Zeilen tritt der „Lost in the Middle"-Effekt bei LLM-Agenten messbar auf. Bei Partial-Klassen wird die Meldung nur einmal pro logischer Klasse ausgegeben (Deduplication), unabhängig von der Anzahl der Partial-Dateien.                                                                                                                               |
| `MaxSwitchArms`                                  | Metrics | Maximale Anzahl Arms in einem Switch-Expression bzw. Labels in einem Switch-Statement pro Methode (Standard: 10). `0` = deaktiviert. Dispatcher-Methoden (reine Routing-Tabellen) können per `MaxSwitchArmsExcludeDispatcher` ausgenommen werden.                                                                                                                                                                                                               |
| `MaxSwitchArmsExcludeDispatcher`                 | Metrics | Wenn `true` (Standard): Methoden die als Switch-Dispatcher klassifiziert werden (alle Cases sind triviale Einzeiler-Aufrufe), werden von `MaxSwitchArms` ausgenommen. Deckt den Hauptanwendungsfall "Routing-Tabelle mit 15+ Arms" ab.                                                                                                                                                                                                                             |
| `MaxSwitchArmsExemptTypes`                       | Metrics | Einfache Typnamen (kein Namespace), deren Methoden von `MaxSwitchArms` komplett ausgenommen werden. Nützlich für State-Machine-Klassen mit vielen legitimen Zuständen (z. B. `["OrderStateMachine"]`). Standard: `[]`.                                                                                                                                                                                                                                             |
| `FootprintIgnoreNamespacePrefixes`               | Metrics | Namespace-Präfixe von Typen, die beim Footprint nicht gezählt werden. Nützlich wenn Drittanbieter-Quellcode direkt in der Solution liegt. Framework-Typen ohne Quellcode (MudBlazor NuGet, `System.*`) werden immer automatisch ausgeschlossen. Standard: `[]`.                                                                                                                                                                                                    |
| `FootprintIgnoreTypeNames`                       | Metrics | Einfache Typ-Namen (kein Namespace), die bei `AIContextFootprint` nicht mitgezählt werden. Ergänzung zu `FootprintIgnoreNamespacePrefixes` für Infrastruktur-Omnipräsenz-Typen die durch den ganzen Dependency-Graphen fließen (z. B. zentrale `SqlExecutor`-Klassen). Nur einfacher Name: z. B. `"SqlExecutor"` nicht `"MyApp.Infra.SqlExecutor"`. Standard: `[]`.                                                                                                |
| `ComplexityNearMissTolerance`                    | Metrics | Toleranzbereich über dem Komplexitätslimit. Verstöße im Bereich `(Limit, Limit + Toleranz]` werden mit dem Hinweis `[near-miss: knapp über Limit]` markiert, zählen aber weiterhin als Verstöße und beeinflussen den Exit-Code. Standard: `1`.                                                                                                                                                                                                       |
| `ExcludeNullCoalescingInitializerComplexity`     | Metrics | Methoden, deren Body ausschließlich ein `return this with { … }` oder `return new T { … }` mit Null-Coalescing-Zuweisungen ist, werden von `MaxCyclomaticComplexity` und `MaxCognitiveComplexity` ausgenommen. Standard: `true` — diese Methoden sind semantisch flach trotz hohem McCabe-Wert.                                                                                                                                                                    |
| `NullCoalescingInitializerMaxNonCoalescingRatio` | Metrics | Maximaler Anteil an nicht-null-coalescing-Ästen, damit eine Methode als NullCoalescingInitializer gilt (0.0–1.0). Standard: `0.0` — alle Branches müssen `??` oder `?:` sein.                                                                                                                                                                                                                                                                                      |
| `MaxLinqChainLength`                             | Metrics | Maximale Anzahl verketteter LINQ-Methoden in einer einzelnen Ausdruckskette (Standard: 0 = deaktiviert). `LinqMethodNames` enthält die erlaubten Methodennamen, um Builder-Ketten von der Prüfung auszuschließen.                                                                                                                                                                                                                                                  |
| `TestSentinel.ClassNamePatterns`                 | Config  | Muster für Testklassen-Namen, z. B. `["{Name}Tests", "{Name}*Tests"]`.                                                                                                                                                                                                                                                                                                                                                                                             |
| `TestSentinel.RecognizeTypeofReference`          | Config  | Erkennt `typeof(MyClass)` in einer Testklasse als Abdeckung. Standard: `true`.                                                                                                                                                                                                                                                                                                                                                                                     |
| `TestSentinel.RecognizeCoversComment`            | Config  | Erkennt `// @covers MyClass`-Kommentare als Abdeckung. Standard: `true`.                                                                                                                                                                                                                                                                                                                                                                                           |
| `TestSentinel.ExemptClassNameSuffixes`           | Config  | Klassen mit diesen Namens-Suffixen werden vom Sentinel ausgenommen. Standard: `["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"]`.                                                                                                                                                                                                                                                                                                                                             |
| `TestSentinel.ExemptWhenInheritsFrom`            | Config  | Klassen die von einem dieser Typen erben oder Interfaces implementieren, werden ausgenommen. Standard: `["ComponentBase", "IValueConverter", "Profile"]`.                                                                                                                                                                                                                                                                                                                        |
| `TestSentinel.ExemptStaticClasses`               | Config  | Statische Klassen werden vom Sentinel ausgenommen wenn `true`. Standard: `true`.                                                                                                                                                                                                                                                                                                                                                                                  |
| `TestSentinel.TestProjectNameSuffixes`           | Config  | Projekt-Name-Suffixe, die ein Projekt als Testprojekt markieren, wenn keine Testrahmenbibliothek in den Metadaten erkannt wird (Fallback). Standard: `["Tests", "Test", "IntegrationTests", "Specs", "Spec"]`. Deckt reine Integration-Test-Projekte ohne direkten xunit-Verweis ab.                                                                                                                                                                               |
| `RuleMetadata`                                   | Config  | Severity (`error`/`warning`) und Intent-Tags pro Regel für LLM-Priorisierung.                                                                                                                                                                                                                                                                                                                                                                                      |

### Projekt-spezifische Regel-Konfiguration (Project Overrides)

In großen Solutions können verschiedene Projekte unterschiedliche Qualitätsanforderungen haben. Über die Sektion `"ProjectOverrides"` in der `ainetlinter-rules.json` können Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) überschrieben werden:

```json
  "ProjectOverrides": {
    "*.Tests": {
      "Global": {
        "EnforceSealedClasses": false
      },
      "Metrics": {
        "MaxMethodLineCount": 100
      }
    }
  }
```

### Pfadbasierte Konfigurations-Overrides (PathOverrides)

`PathOverrides` erlaubt es, Regeln gezielt für bestimmte **Ordner** innerhalb einer Solution zu überschreiben — unabhängig vom Projektnamen. Der Key ist ein Glob-Muster gegen den relativen Dateipfad ab Solution-Root. `PathOverrides` werden **NACH** `ProjectOverrides` angewendet und gewinnen bei Konflikten.

```json
"PathOverrides": {
  "src/MyApp/Handlers/**": {
    "Metrics": {
      "MaxMethodLineCount": 80
    }
  },
  "src/MyApp/Components/**": {
    "Global": {
      "EnforceSealedClasses": false
    }
  },
  "src/MyApp/Migrations/**": {
    "Global": {
      "EnforceNullableEnable": false
    }
  },
  "MyApp/Components/Pages/Test/**": {
    "Metrics": {
      "MaxAIContextFootprint": 12000
    }
  }
}
```

**Glob-Syntax:**

- `**` — matcht beliebig viele Pfadsegmente (inkl. Unterverzeichnisse)
- `*` — matcht ein einzelnes Pfadsegment (kein Slash)
- Pfade werden mit Forward-Slashes verglichen (auch auf Windows)

**Hinweis zu typ-zentrischen Metriken (`MaxAIContextFootprint`, `MaxInheritanceDepth`):** Diese Metriken werden pro logischer Klasse (nicht pro Datei) berechnet und an der repräsentativen Partial-Datei gemeldet. Der PathOverride wird anhand des Dateipfads dieser repräsentativen Datei aufgelöst — das Muster muss also zu dieser Datei passen. Bei Blazor-Komponenten ist die repräsentative Datei in der Regel die `.razor.cs`-Datei im Quellordner.

### BanAsyncVoid

| Schlüssel                     | Typ    | Standard |
| ----------------------------- | ------ | -------- |
| `BanAsyncVoid`                | `bool` | `true`   |
| `AsyncVoidAllowEventHandlers` | `bool` | `true`   |

Verbietet `async void`-Methoden und lokale Funktionen. `async void` schleudert Exceptions direkt in den `SynchronizationContext`, wodurch sie für aufrufende `try/catch`-Blöcke unsichtbar werden und zum App-Absturz oder stillschweigendem Fehlerverfall führen können.

**Ausnahme:** Event-Handler mit der Signatur `(object sender, <Name>EventArgs e)` (bzw. abgeleitete Klassen von `EventArgs`) bleiben erlaubt, wenn `AsyncVoidAllowEventHandlers: true` gesetzt ist.

### BanBlockingTaskAccess

| Schlüssel                           | Typ    | Standard |
| ----------------------------------- | ------ | -------- |
| `BanBlockingTaskAccess`             | `bool` | `true`   |
| `BanBlockingTaskAccessAllowInMain`  | `bool` | `true`   |
| `BanBlockingTaskAccessAllowInTests` | `bool` | `false`  |

Verbietet blockierende Task-Zugriffe (`.Wait()`, `.Result`, `.GetAwaiter().GetResult()`). Diese Muster blockieren ThreadPool-Threads und können in SynchronizationContext-Umgebungen (ASP.NET Classic, WPF) zu Deadlocks führen.

**Ausnahme:** Blockierende Zugriffe in `static void Main` Methoden sind erlaubt wenn `BanBlockingTaskAccessAllowInMain: true` gesetzt ist. In Testdateien sind sie erlaubt wenn `BanBlockingTaskAccessAllowInTests: true` gesetzt ist (standardmäßig falsch, da Tests async sein sollten).

### DuplicateCode

| Schlüssel                          | Typ      | Standard |
| ----------------------------------- | -------- | -------- |
| `EnableDuplicateCodeCheck`          | `bool`   | `true`   |
| `DuplicateCodeMinTokens`            | `int`    | `30`     |
| `DuplicateCodeNgramSize`            | `int`    | `5`      |
| `DuplicateCodeMinSharedNgrams`      | `int`    | `3`      |
| `DuplicateCodeExactThreshold`       | `double` | `0.95`   |
| `DuplicateCodeNearThreshold`        | `double` | `0.80`   |
| `DuplicateCodeFuzzyThreshold`       | `double` | `0.65`   |
| `DuplicateCodeNormalizeIdentifiers` | `bool`   | `false`  |
| `DuplicateCodeMaxResults`           | `int`    | `20`     |

Solution-weite DRY-Erkennung (Token-basiertes Clone-Detection, CCFinder/Jaccard-N-Gram-Ansatz,
Method-Granularität). Für jede Methode/lokale
Funktion mit mindestens `DuplicateCodeMinTokens` Tokens im Body wird der Token-Stream in
überlappende N-Gramme der Größe `DuplicateCodeNgramSize` zerlegt und über einen Inverted Index mit
allen anderen Methoden der Solution verglichen. Methoden-Paare mit mindestens
`DuplicateCodeMinSharedNgrams` gemeinsamen N-Grammen werden per Jaccard-Similarity
(`|A∩B|/|A∪B|`) bewertet; transitiv verbundene Paare (A~B, B~C) werden zu einem Cluster
zusammengefasst statt als isolierte Paare gemeldet.

**Gestaffelte Schwellwerte statt hartem Cut:** `exact` (≥ `DuplicateCodeExactThreshold`, fast
identisch), `near` (≥ `DuplicateCodeNearThreshold`, sehr ähnlich) und `fuzzy` (≥
`DuplicateCodeFuzzyThreshold`, grenzwertig). **Nur `exact`-Cluster erzeugen einen Regelverstoß**
(`DuplicateCode`, Severity `info` — Kandidaten-Befund, kein hartes Anti-Pattern) — `near`/`fuzzy`
wären zu viel Rauschen für automatisches Lint (Live-Dogfood-Befund 2026-08-11: `near`-Cluster
allein erzeugten auf diesem Repo ~23 Einzel-Funde), bleiben aber über das MCP-Tool
`find_duplicates` voll einsehbar. Pro Cluster
wird genau **ein** Regelverstoß gemeldet (repräsentatives Mitglied, analog dem
`MaxPartialClassFiles`-Muster) — `Details` listet trotzdem alle beteiligten Methoden vollständig,
nicht eine Violation pro Mitglied.

**False-Positive-Disziplin:** `bin/`, `obj/`, `.ainetlinter/` und `tests/Fixtures/`-Verzeichnisse
sind fest ausgeschlossen; Methoden mit `[GeneratedCode]`-Attribut (an der Methode oder am
umschließenden Typ) werden übersprungen; `DuplicateCodeMinTokens` filtert triviale Methoden
(leere `Dispose`/`ToString`-Overrides) heraus.

**`DuplicateCodeNormalizeIdentifiers`** (Standard `false`): wenn aktiv, werden Identifier-Tokens
vor dem Vergleich auf `$ID$` und Literal-Tokens auf `$LIT$` normalisiert — schaltet die Erkennung
umbenannter Klone (Type-2, z. B. `CalculateOrderTotal` vs. `CalculateInvoiceTotal`) an. Standard
`false`, weil das sonst semantisch unterschiedliche Methoden mit zufällig ähnlicher Struktur als
Klon markieren würde.

**`DuplicateCodeMaxResults`** begrenzt die als Verstoß gemeldeten Cluster (Top-N nach
Jaccard-Score absteigend) — kein unbegrenzter Dump bei großen Solutions.

**Gezielte Unterdrückung statt globaler Deaktivierung:** Ist eine Ähnlichkeit beabsichtigt (z. B.
strukturell gleiche, aber fachlich unterschiedliche Methoden), unterstützt `DuplicateCode` die
projektübliche dateiweite Suppression-Konvention (`// ainetlinter-disable DuplicateCode`, siehe
Abschnitt „Suppressions" unten) — ein Kommentar in **einer** der am Cluster beteiligten Dateien
reicht, um den gesamten Cluster-Fund zu unterdrücken (der Fund ist eine Aussage über die Beziehung
zwischen den Methoden, keine pro Datei unabhängige).

> Evidenz: Roy & Cordy (2007), Bellon et al. (2007) für die Token-CPD-Methodik; Manning, Raghavan,
> Schütze (2008) für Jaccard/N-Gram/Inverted-Index. Details und Referenz-Tools (CCFinder, PMD CPD,
> jscpd) sind in der wissenschaftlichen Literatur dokumentiert.

**Struktureller Drift-Modus (`find_duplicates mode=structural`):**

| Schlüssel | Typ | Standard |
| --- | --- | --- |
| `StructuralDuplicateExactThreshold` | `double` | `0.90` |
| `StructuralDuplicateNearThreshold` | `double` | `0.80` |
| `StructuralDuplicateFuzzyThreshold` | `double` | `0.70` |

Cosine-Ähnlichkeits-Schwellwerte für `find_duplicates mode=structural`. Bewusst getrennt von den
Jaccard-`DuplicateCode*Threshold`-Werten, damit Kalibrierung der Typ-4-Suche die Clone-Lint-Kalibrierung nicht verschiebt. Diese Schwellwerte erzeugen **keine automatischen Lint-Violations** — `mode=structural` liefert ausschließlich manuell zu prüfende Kandidatencluster.

### MaxLinqChainLength

| Schlüssel            | Typ        | Standard                    |
| -------------------- | ---------- | --------------------------- |
| `MaxLinqChainLength` | `int`      | `0` (deaktiviert)           |
| `LinqMethodNames`    | `string[]` | Standard-LINQ-Methodennamen |

Begrenzt die Anzahl verketteter LINQ-Methoden in einer einzelnen Ausdruckskette. Eine Kette mit mehr Methoden als der Schwellenwert erzeugt eine `warning` (kein `error`).

**Empfohlener Schwellenwert:** 5 (ab 6 Methoden Warnung).

**Konfigurationsbeispiel:**

```json
"Metrics": {
  "MaxLinqChainLength": 5
}
```

**Erweiterung der Whitelist** für projektspezifische LINQ-ähnliche APIs (z. B. EF Core Fluent API):

```json
"Metrics": {
  "LinqMethodNames": ["Where", "Select", "Include", "ThenInclude"]
}
```

> Evidenz: moderat (keine dedizierte Studie zu LINQ-Kettenlänge und LLM-Fehlerrate).
> Deshalb Standard-deaktiviert — bewusstes Opt-in via `ainetlinter-rules.json`.

### AI-Context-Footprint (Metrik)

Der AI-Context-Footprint berechnet die Summe aller Codezeilen der Klasse selbst plus aller transitiv im Quellcode referenzierten eigenen Klassen/Typen. Steigt diese Metrik über den konfigurierten Schwellenwert (`MaxAIContextFootprint`, standardmäßig `5000` Zeilen), wird ein Regelverstoß gemeldet. Dies hilft Entwicklern, hohe Kopplung zu vermeiden und die Token-Belastung für KIs gering zu halten.

### Ausnahmen für EnforceSealedClasses (WPF & Basisklassen)

Die Regel `EnforceSealedClasses` zwingt standardmäßig alle konkreten Klassen dazu, als `sealed` deklariert zu werden. In bestimmten Szenarien (z. B. WPF oder bei dedizierten Basisklassen) führt dies jedoch zu False-Positives:

1. **WPF Partial-Klassen:** Der XAML-Compiler generiert für Code-Behind-Dateien partial Klassen, die standardmäßig nicht `sealed` deklariert sind.
2. **Designte Basisklassen:** Klassen, die als Basisklassen für Vererbung gedacht sind (z. B. `OrderHandlerBase`), sollten nicht versiegelt werden.

Hierfür stehen folgende Konfigurationsoptionen zur Verfügung:

- **`AllowUnsealedPartialClasses`** (Boolean, Default: `false`): Erlaubt es, `partial` Klassen unsealed zu lassen (z. B. `public partial class MainWindow : Window`). Klassen, die explizit `sealed partial` deklariert sind, werden weiterhin korrekt erkannt und führen zu keinem Verstoß.
- **`SealedClassExemptSuffixes`** (Array von Strings, Default: `["Base", "Foundation", "Host"]`): Klassen, deren Name mit einem dieser Suffixe endet, werden von der Prüfung ausgenommen.

#### Empfohlene Konfiguration für WPF- und UI-Projekte:

Da WPF-Templates standardmäßig unsealed partial Klassen generieren, empfiehlt sich ein Projekt-Override in der `ainetlinter-rules.json`:

```json
"ProjectOverrides": {
  "*.Wpf": {
    "Global": {
      "AllowUnsealedPartialClasses": true
    }
  }
}
```

### Framework-Typen bei Vererbungstiefe ausschließen

Die Regel `MaxInheritanceDepth` zählt standardmäßig alle Basisklassen bis zu `System.Object`. Bei UI-Frameworks wie WPF oder Blazor führt dies oft zu False-Positives, da Basisklassen wie `Window` oder `ComponentBase` bereits eine hohe Vererbungstiefe besitzen.

Mit `InheritanceDepthFrameworkPrefixes` können Namespace-Präfixe definiert werden, deren Typen beim Zählen der Vererbungstiefe ignoriert werden. Die Tiefe der eigenen Klassen-Hierarchie wird weiterhin korrekt ermittelt.

Empfohlene Konfiguration für WPF- und Blazor-Projekte:

```json
"Metrics": {
  "MaxInheritanceDepth": 2,
  "InheritanceDepthFrameworkPrefixes": [
    "System.",
    "Microsoft.UI.",
    "System.Windows.",
    "Microsoft.AspNetCore.Components."
  ]
}
```

### Framework-Typen bei Konstruktor-Abhängigkeiten ausschließen

Die Regel `MaxConstructorDependencies` begrenzt standardmäßig die Anzahl der Parameter in Konstruktoren und Primärkonstruktoren (Standard: 5). Cross-Cutting-Concerns wie `ILogger<T>`, `IOptions<T>`, `IHostEnvironment` oder `IConfiguration` zählen hierbei mit, obwohl sie keine fachlichen Abhängigkeiten darstellen.

Mit `ConstructorDependencyIgnoreTypePrefixes` können Typ-Name-Präfixe definiert werden, die beim Zählen der Konstruktor-Abhängigkeiten ignoriert werden. Dies erlaubt es, fachliche Abhängigkeiten sauber von Infrastruktur-Abhängigkeiten zu trennen. Auch die Primärkonstruktor-Syntax (.NET 8+) wird vollständig unterstützt.

#### Automatische Ausnahme: Options/Config-Records und -Structs

`MaxConstructorDependencies` zielt auf **DI-Kopplung** — viele injizierte Services in einer Klasse sind ein Code-Smell (zu viele Verantwortlichkeiten). Records und Structs, bei denen **alle** Primärkonstruktor-Parameter einen Default-Wert haben, fallen nicht in dieses Muster: Sie sind Options/Config-Objects (z. B. CLI-Optionen, Render-Einstellungen), keine Service-Klassen.

Der Linter erkennt dieses Muster automatisch und meldet keine Verletzung:

```csharp
// Kein False-Positive — alle Parameter haben Defaults → Options-Object
public sealed record RunOptions(
    bool Verbose = false,
    bool DryRun = false,
    string? OutputPath = null,
    string? BaselinePath = null,
    string? CacheKey = null,
    string OutputFormat = "text")
{
    public static RunOptions Default { get; } = new();
}
```

Records mit gemischten Parametern (mindestens ein Required-Parameter ohne Default) werden weiterhin geprüft, da Required-Parameter auf echte Abhängigkeiten hinweisen können:

```csharp
// Wird geprüft — ServiceA hat keinen Default-Wert
public sealed record MyHandler(
    ServiceA ServiceA,   // required: kein Default
    ServiceB ServiceB,
    ServiceC ServiceC,
    ServiceD ServiceD,
    ServiceE ServiceE,
    ServiceF ServiceF,
    bool IsEnabled = false);
```

Wer einen Options-Record in Ausnahmefällen trotzdem prüfen möchte, entfernt einfach die Default-Werte oder nutzt die Suppression:

```csharp
// ainetlinter-disable MaxConstructorDependencies
public sealed record SpecialOptions(bool A = false, bool B = false, ...);
```

Empfohlene Konfiguration:

```json
"Metrics": {
  "MaxConstructorDependencies": 5,
  "ConstructorDependencyIgnoreTypePrefixes": [
    "ILogger",
    "IOptions",
    "IOptionsSnapshot",
    "IOptionsMonitor",
    "IHostEnvironment",
    "IWebHostEnvironment",
    "IConfiguration",
    "IServiceProvider",
    "IHttpContextAccessor"
  ]
}
```

### Ausnahmen für EnforceExplicitStateImmutability (WPF & Blazor)

Die Regel `EnforceExplicitStateImmutability` (Code-Default: `false`, also Opt-in — siehe `GlobalConfig.cs`) zwingt, wenn aktiviert, alle Klassen (die keine DTOs oder Entities sind) zur Unveränderlichkeit. Da bei WPF-ViewModels (MVVM) und Blazor-Komponenten mutable Eigenschaften und private Backing-Felder unumgänglich sind, bietet der Linter hierfür dedizierte Ausnahmen:

- **`ImmutabilityExemptBaseTypes`** (Array von Strings, Default: `["ComponentBase", "LayoutComponentBase", "ObservableObject", "ObservableRecipient", "BackgroundService", "AuthenticationStateProvider", "INotifyPropertyChanged"]`): Klassen, die von einer dieser Basisklassen oder Schnittstellen erben (transitiv über die gesamte Hierarchie), werden vollständig von der Immutability-Prüfung ausgenommen.
- **`ImmutabilityAllowPrivateBackingFields`** (Boolean, Default: `true`): Wenn `true`, werden private Felder, die mit einem Unterstrich (`_`) beginnen, nicht als Verstoß gemeldet. Dies erlaubt typische WPF-MVVM Backing-Felder.

#### Empfohlene Konfiguration für WPF (MVVM):

```json
"Global": {
  "EnforceExplicitStateImmutability": true,
  "ImmutabilityExemptBaseTypes": [
    "ObservableObject",
    "ObservableRecipient",
    "INotifyPropertyChanged"
  ],
  "ImmutabilityAllowPrivateBackingFields": true
}
```

#### Empfohlene Konfiguration für Blazor-Projekte:

```json
"Global": {
  "EnforceExplicitStateImmutability": true,
  "ImmutabilityExemptBaseTypes": [
    "ComponentBase",
    "LayoutComponentBase",
    "AuthenticationStateProvider"
  ],
  "ImmutabilityAllowPrivateBackingFields": false
}
```

### Namespace-Verzeichnis-Abgleich (EnforceNamespaceDirectoryMapping)

Die Regel `EnforceNamespaceDirectoryMapping` stellt sicher, dass der Namespace einer Datei ihrer physischen Ordnerstruktur im Dateisystem entspricht. In modernen Feature-Folder-Architekturen (Vertical Slices) weichen Namespaces jedoch oft bewusst ab. Hierfür stehen folgende Anpassungsmöglichkeiten zur Verfügung:

#### Einstellungsoptionen

- **`NamespaceDirectoryMappingMode`** (String, Default: `"suffix-match"`):
  - `"exact"`: Der Namespace muss exakt auf den vollständigen physischen Ordnerpfad ab `.csproj` enden (bisheriges Standardverhalten).
  - `"suffix-match"`: Der Namespace muss auf die letzten N Segmente des Pfades enden. N wird über `NamespaceDirectoryMappingRequiredTrailingSegments` konfiguriert.
  - `"contains-all"`: Alle relevanten Pfad-Segmente müssen im deklarierten Namespace enthalten sein (Reihenfolge egal).
- **`NamespaceDirectoryMappingIgnorePathSegments`** (Array von Strings, Default: `["src", "Source", "Domains", "Handlers"]`): Pfad-Segmente, die beim Abgleich ignoriert werden.
- **`NamespaceDirectoryMappingRequiredTrailingSegments`** (Integer, Default: `2`): Im Modus `"suffix-match"` gibt dies an, wie viele der letzten Ordner-Segmente im Namespace als Suffix übereinstimmen müssen.

#### Beispiele

##### 1. Modus `"exact"`

- **Pfad:** `Features/Admin/Users/`
- **Namespace:** `MyApp.Features.Admin.Users` (Kein Verstoß)
- **Namespace:** `MyApp.Features.Users` (Verstoß, da `Admin` fehlt)

##### 2. Modus `"suffix-match"` (RequiredTrailingSegments: 2, IgnorePathSegments: `["Domains"]`)

- **Pfad:** `Handlers/Domains/Kalender/`
- **Relevante Segmente:** `["Handlers", "Kalender"]` (da `"Domains"` ignoriert wird)
- **Erwarteter Suffix (die letzten 2):** `"Handlers.Kalender"`
- **Namespace:** `MyApp.Handlers.Kalender` (Kein Verstoß)

##### 3. Modus `"contains-all"`

- **Pfad:** `Features/Admin/Users/`
- **Namespace:** `MyApp.Features.Users.Admin` (Kein Verstoß, da `Features`, `Admin` und `Users` alle im Namespace vorkommen)

#### Empfohlene Konfiguration für Feature-Folder-Architektur (Vertical Slice):

```json
"Global": {
  "EnforceNamespaceDirectoryMapping": true,
  "NamespaceDirectoryMappingMode": "suffix-match",
  "NamespaceDirectoryMappingIgnorePathSegments": ["src", "Source", "Domains", "Handlers"],
  "NamespaceDirectoryMappingRequiredTrailingSegments": 2
}
```

> [!NOTE]
> Diese Regel ist standardmäßig aktiviert (`EnforceNamespaceDirectoryMapping: true`, Modus `"suffix-match"`).

### UI-Datei-Trennung (UiSeparation)

Erzwingt das Separation-of-Concerns-Prinzip für Blazor- und WPF-Projekte: Keine Business-Logik oder Styles direkt in Markup-Dateien.

#### Einstellungsoptionen

| Option                                   |     Typ      |                         Default                         | Beschreibung                                                                                                                                                                                                                                                                                                          |
| :--------------------------------------- | :----------: | :-----------------------------------------------------: | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorRequireCodeBehind`                |   Boolean    |                         `true`                          | `.razor`-Dateien mit `@code {}`- oder `@functions {}`-Blöcken müssen eine `.razor.cs`-Begleitdatei haben (Code-Behind-Partial-Class). Reine Template-Dateien ohne Inline-Code lösen keine Verletzung aus.                                                                                                             |
| `BlazorRequireCssIsolation`              |   Boolean    |                         `true`                          | Jede `.razor`-Datei muss eine `.razor.css`-Begleitdatei haben (CSS-Isolation). Verhindert `<style>`-Blöcke inline.                                                                                                                                                                                                    |
| `BlazorCssIsolationOnlyWhenStylesNeeded` |   Boolean    |                         `true`                          | Wenn `true`, wird `BlazorRequireCssIsolation` nur ausgelöst, wenn die `.razor`-Datei native HTML-Elemente (`<div>`, `<span>` etc.) oder explizite `class=`/`style=`-Attribute enthält. Reine Komponenten-Komposition mit PascalCase-Tags (`<MudButton>`) löst keine Verletzung aus. Empfohlen für MudBlazor-Projekte. |
| `WpfRequireMinimalCodeBehind`            |   Boolean    |                         `true`                          | WPF Code-Behind-Klassen (partial classes mit WPF-Basistyp) dürfen nur den Konstruktor mit `InitializeComponent()` enthalten.                                                                                                                                                                                          |
| `WpfCodeBehindBaseTypes`                 | String-Array | `["Window", "UserControl", "Page", "NavigationWindow"]` | Basis-Typnamen, die eine Klasse als WPF Code-Behind identifizieren.                                                                                                                                                                                                                                                   |
| `BlazorExcludeFileNames`                 | String-Array |                  `["_Imports.razor"]`                   | Razor-Dateinamen, die von den Blazor-Checks ausgeschlossen werden.                                                                                                                                                                                                                                                    |
| `WpfExcludeClassNames`                   | String-Array |                          `[]`                           | Klassen-Namen, die vom WPF Code-Behind-Check ausgeschlossen werden.                                                                                                                                                                                                                                                   |

#### Suppression

- **Blazor**: `@* ainetlinter-disable BlazorRequireCodeBehind *@` oder `@* ainetlinter-disable BlazorRequireCssIsolation *@` am Anfang der `.razor`-Datei.
- **WPF**: `// ainetlinter-disable WpfRequireMinimalCodeBehind` in der `.xaml.cs`-Datei (Standard-Suppressions-Syntax).

#### Empfohlene Konfiguration (Vollständige Trennung):

```json
"UiSeparation": {
  "BlazorRequireCodeBehind": true,
  "BlazorRequireCssIsolation": true,
  "WpfRequireMinimalCodeBehind": true,
  "WpfCodeBehindBaseTypes": ["Window", "UserControl", "Page", "NavigationWindow"],
  "BlazorExcludeFileNames": ["_Imports.razor", "App.razor"],
  "WpfExcludeClassNames": []
}
```

#### Empfohlene Konfiguration (Nur Blazor, WPF-Check aus):

```json
"UiSeparation": {
  "BlazorRequireCodeBehind": true,
  "BlazorRequireCssIsolation": false,
  "WpfRequireMinimalCodeBehind": false
}
```

---

### Web-Asset-Linting (Web / CSS, JS, Razor)

Erweitert den Linter um Regeln fuer CSS-, JavaScript- und Razor-Dateien. Web-Dateien werden nicht von Roslyn analysiert, sondern ueber einen parallelen File-System-Walk im PostAnalysis-Schritt geladen und mit dedizierten Analyzern verarbeitet (ExCSS fuer CSS, Esprima fuer JS, Microsoft.AspNetCore.Razor.Language fuer Razor). Opt-in: `Web.IsEnabled = true` schaltet das gesamte Web-Modul ein; ohne Aktivierung wird kein Web-Asset analysiert.

#### Einstellungsoptionen

| Option                            |     Typ      |                            Default                            | Beschreibung                                                                                                                                                                                                 |
| :-------------------------------- | :----------: | :-----------------------------------------------------------: | :----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IsEnabled`                       |   Boolean    |                            `false`                            | Aktiviert das gesamte Web-Modul (CSS, JS, Razor). Master-Switch.                                                                                                                                             |
| `Css.MaxCssLineCount`             |   Integer    |                             `300`                             | Maximale Zeilenanzahl pro CSS-Datei. Verhindert "Lost in the Middle" in grossen monolithischen Stylesheets. `0` = deaktiviert.                                                                               |
| `Css.PreferScopedCss`             |   Boolean    |                            `true`                             | Wenn true, werden globale CSS-Dateien mit vielen Regeln zugunsten von Scoped CSS (`.razor.css`) abgemaahnt (Butterfly-Effekt).                                                                               |
| `Css.PreferScopedCssMinRuleCount` |   Integer    |                              `5`                              | Schwellenwert: ab dieser Anzahl Stil-Regeln in einer globalen CSS-Datei wird `CSS_PreferScopedCss` ausgeloest. CSS-Dateien mit weniger Regeln (Resets, Custom Properties, `@font-face`) sind legitim global. |
| `Css.MaxCssSelectorComplexity`    |   Integer    |                              `3`                              | Maximale Tiefe eines CSS-Selektors (Anzahl Selektor-Segmente, getrennt durch Komma/Whitespace/Combinators). Verhindert ueber-Engineered Selektoren. `0` = deaktiviert.                                       |
| `Css.ExemptPaths`                 | String-Array | `["**/wwwroot/lib/**", "**/node_modules/**", "**/*.min.css"]` | Glob-Muster fuer Pfade, die von der CSS-Analyse ausgeschlossen werden (z. B. Bootstrap, MudBlazor, `*.min.css`).                                                                                             |
| `Js.MaxJsLineCount`               |   Integer    |                             `150`                             | Maximale Zeilenanzahl pro JavaScript-Datei. Verhindert "Lost in the Middle" in grossen monolithischen JS-Interop-Dateien. Komplexe Logik gehoert in C#. `0` = deaktiviert.                                   |
| `Js.EnforceJsModules`             |   Boolean    |                            `true`                             | Wenn true, muessen JS-Dateien als ES6-Modul aufgebaut sein (`export`-Statement vorhanden) und duerfen keine `window.*`-Zuweisungen enthalten.                                                                |
| `Js.ExemptPaths`                  | String-Array | `["**/wwwroot/lib/**", "**/node_modules/**", "**/*.min.js"]`  | Glob-Muster fuer Pfade, die von der JS-Analyse ausgeschlossen werden (z. B. jQuery, Bootstrap-Bundle, `*.min.js`).                                                                                           |
| `Razor.MaxRazorLineCount`         |   Integer    |                             `300`                             | Maximale Zeilenanzahl pro Razor-Datei. Verhindert "Lost in the Middle" bei grossen Komponenten. `0` = deaktiviert.                                                                                           |
| `Razor.MaxRazorCodeBlockLines`    |   Integer    |                             `20`                              | Maximale Zeilenanzahl fuer `@code`-Bloecke in `.razor`-Dateien (Guard-Regel fuer BlazorRequireCodeBehind). `0` = deaktiviert.                                                                                |
| `Razor.MaxMarkupNestingDepth`     |   Integer    |                              `6`                              | Maximale Tiefe von HTML-Verschachtelungen (HTML-Elemente und Blazor-Komponenten zaehlen). Verhindert Tag-Mismatch-Halluzinationen bei KIs. `0` = deaktiviert.                                                 |
| `Razor.BanInlineEventLambdas`     |   Boolean    |                            `true`                             | Wenn true, sind komplexe, mehrzeilige Inline-Event-Lambdas im Markup verboten. Methoden-Referenzen oder triviale Einzeiler sind erlaubt.                                                                     |
| `Razor.MaxControlFlowBlocks`      |   Integer    |                              `8`                              | Maximale Anzahl an `@if`, `@foreach`, `@switch` etc. Bloecken pro Datei (Komplexitaet des konditionalen Renderings). `0` = deaktiviert.                                                                      |
| `Razor.MaxForeachNestingDepth`    |   Integer    |                              `2`                              | Maximale Verschachtelungstiefe von `@foreach`-Schleifen (Verbindung von Markup und Daten-Iteration). `0` = deaktiviert.                                                                                      |
| `Razor.MaxComponentParameterCount`|   Integer    |                              `10`                             | Maximale Anzahl an Parametern bei Komponenten-Aufrufen (HTML-Attribute ausgenommen). Verhindert unuebersichtliche Aufrufe. `0` = deaktiviert.                                                                 |
| `Razor.BanInlineTernaryInAttributes`|  Boolean   |                            `true`                             | Wenn true, sind Ternary-Ausdruecke in HTML-Attributwerten (wie `class="base @(flag ? "active" : "")"`) verboten (Mixed-Context-Fehler).                                                                      |

#### Regeln

| Regel                          | Severity |    Intent     | Beschreibung                                                                                                                                |
| :----------------------------- | :------: | :-----------: | :------------------------------------------------------------------------------------------------------------------------------------------ |
| `CSS_MaxCssLineCount`          |  error   | agent-context | CSS-Datei ueberschreitet das Zeilenlimit. Empfehlung: Datei splitten oder in Scoped CSS ueberfuehren.                                       |
| `CSS_PreferScopedCss`          | warning  | agent-context | Globale CSS-Datei enthaelt mehr Regeln als der Schwellenwert. Empfehlung: Komponenten-Styles in `.razor.css` extrahieren.                   |
| `CSS_MaxCssSelectorComplexity` | warning  | agent-context | CSS-Selektor zu tief verschachtelt. Empfehlung: Wurzel-Selektor verwenden, Spezifitaet reduzieren oder Scoped CSS.                          |
| `CSS_ParseError`               |  error   |    general    | CSS-Datei konnte nicht geparst werden (Syntax-Fehler). Empfehlung: Klammern / Selektor-Syntax korrigieren.                                  |
| `JS_MaxJsLineCount`            |  error   | agent-context | JavaScript-Datei ueberschreitet das Zeilenlimit. Empfehlung: Logik nach C# migrieren oder Datei aufteilen.                                  |
| `JS_EnforceJsModules`          |  error   | agent-context | JavaScript-Datei ist kein ES6-Modul oder nutzt das globale `window`-Objekt. Empfehlung: `export` verwenden, `window`-Zuweisungen vermeiden. |
| `JS_SyntaxError`               |  error   |    general    | JavaScript-Datei konnte nicht geparst werden (Syntax-Fehler). Empfehlung: Klammern / Statements korrigieren.                                |
| `RAZOR_MaxRazorLineCount`         |  error   | agent-context | Razor-Datei ueberschreitet das Zeilenlimit. Empfehlung: Eigenstaendige UI-Bereiche in separate Blazor-Komponenten extrahieren.               |
| `RAZOR_MaxRazorCodeBlockLines`    | warning  | agent-context | `@code`-Block hat zu viele Zeilen. Empfehlung: Verschiebe die C#-Logik in die Code-Behind-Datei (`.razor.cs`).                                |
| `RAZOR_MaxMarkupNestingDepth`     | warning  | agent-context | HTML-Verschachtelungstiefe zu hoch. Empfehlung: Innere Bereiche in eigenstaendige Blazor-Komponenten extrahieren.                           |
| `RAZOR_BanInlineEventLambdas`     | warning  | agent-context | Inline-Event-Lambda in Attribut ist zu komplex. Empfehlung: Logik in eine Methode in der Code-Behind-Datei verschieben.                       |
| `RAZOR_MaxControlFlowBlocks`      | warning  | agent-context | Zu viele Control-Flow-Bloecke. Empfehlung: Teilbereiche in eigenstaendige Komponenten mit klar definierten Eingabe-Parametern auslagern.     |
| `RAZOR_MaxForeachNestingDepth`    | warning  | agent-context | `@foreach`-Verschachtelungstiefe zu hoch. Empfehlung: Innere Schleife in eine Kind-Komponente extrahieren.                                   |
| `RAZOR_MaxComponentParameterCount` | warning  | agent-context | Komponentenaufruf hat zu viele Parameter. Empfehlung: Parameter in ein Parameter-Objekt zusammenfassen oder API reduzieren.                   |
| `RAZOR_BanInlineTernaryInAttributes` | warning | agent-context | Ternary-Ausdruck im Attributwert gefunden. Empfehlung: Wert in einer Property der Code-Behind-Datei vorab berechnen.                         |

#### Suppression

In `.css`-Dateien wird die Standard-CSS-Kommentar-Syntax verwendet, in `.js`-Dateien der klassische JavaScript-Kommentar und in `.razor`-Dateien die Razor-Kommentar-Syntax:

```css
/* ainetlinter-disable CSS_MaxCssLineCount */
/* Begründete Ausnahme für dieses Stylesheet */

/* ainetlinter-disable CSS_MaxCssSelectorComplexity */
.container .sub-container .panel .content .button {
  color: red;
}

/* ainetlinter-disable all */
.foo {
  color: blue;
} /* deaktiviert alle Regeln fuer den Rest der Datei */
```

```javascript
// ainetlinter-disable JS_MaxJsLineCount
export function hugeWrapper() {
  // Begründete Ausnahme für diesen Wrapper
}

// ainetlinter-disable JS_EnforceJsModules
window.myIntegrationFunction = function () {
  console.log("Integration wird migriert");
};

// ainetlinter-disable all
// Deaktiviert alle Regeln fuer den Rest der Datei
```

```razor
@* ainetlinter-disable RAZOR_MaxMarkupNestingDepth *@
<div class="outer">
    <div class="inner-1">
        <div class="inner-2">
            @* Semantisch notwendige Verschachtelung fuer ARIA-Struktur *@
        </div>
    </div>
</div>

@* ainetlinter-disable all *@
@* Deaktiviert alle Regeln fuer den Rest der Datei *@
```

#### Empfohlene Konfiguration (Standardprofil mit Web-Linting):

```json
"Web": {
  "IsEnabled": true,
  "Css": {
    "MaxCssLineCount": 300,
    "PreferScopedCss": true,
    "PreferScopedCssMinRuleCount": 5,
    "MaxCssSelectorComplexity": 3,
    "ExemptPaths": [
      "**/wwwroot/lib/**",
      "**/node_modules/**",
      "**/*.min.css"
    ]
  },
  "Js": {
    "MaxJsLineCount": 150,
    "EnforceJsModules": true,
    "ExemptPaths": [
      "**/wwwroot/lib/**",
      "**/node_modules/**",
      "**/*.min.js"
    ]
  },
  "Razor": {
    "MaxRazorLineCount": 300,
    "MaxRazorCodeBlockLines": 20,
    "MaxMarkupNestingDepth": 6,
    "BanInlineEventLambdas": true,
    "MaxControlFlowBlocks": 8,
    "MaxForeachNestingDepth": 2,
    "MaxComponentParameterCount": 10,
    "BanInlineTernaryInAttributes": true
  }
}
```

#### Abgrenzung zur Blazor-UI-Datei-Trennung

`UiSeparation` prueft die **Struktur** von Blazor-Dateien (hat `.razor` eine `.razor.cs`? Hat es eine `.razor.css`?). `Web.Css` prueft den **Inhalt** der CSS-Dateien (Zeilenanzahl, Selektor-Komplexitaet, Scoped-CSS-Empfehlung). Beide Ebenen sind komplementaer: ein Projekt mit korrekt strukturierten Begleitdateien kann trotzdem zu grosse oder zu komplexe CSS-Inhalte haben.

#### Architektur-Hinweis

Der WebFileCatalog enumeriert Web-Dateien ueber das Dateisystem (Roslyn sieht `.css`/`.js`/`.razor` nicht) und nutzt die bereits geladene `Solution` als Quelle der Projektverzeichnisse — es findet kein zweites MSBuild-Laden statt. Pro Projekt koennen via `ProjectOverrides.*.Web` (mit `WebConfigOverride` / `CssConfigOverride`) abweichende Schwellenwerte gesetzt werden, z. B. um in Testprojekten das Web-Modul abzuschalten oder fuer Blazor-Projekte andere Limits zu setzen.

---

### Datei- und Verzeichnis-Ausschlüsse (FileFilters)

Bei auto-generiertem Code oder temporären Build-Dateien sind viele Linter-Regeln nicht sinnvoll. Über die Sektion `"FileFilters"` in der `ainetlinter-rules.json` können bestimmte Dateien und Verzeichnis-Segmente von der Analyse ausgeschlossen werden.

#### Einstellungsoptionen

- **`ExcludeFilePatterns`** (Array von Strings, Default: `[]`): Glob-Muster, die gegen den Dateinamen (ohne Pfad) geprüft werden (z. B. `["*.designer.cs", "*.g.cs", "AssemblyInfo.cs"]`).
- **`ExcludeDirectoryPatterns`** (Array von Strings, Default: `["obj/", "bin/"]`): Pfad-Segmente. Dateien in Verzeichnissen, die diese Segmente enthalten, werden übersprungen.
- **`SkipGeneratedCodeAttribute`** (Boolean, Default: `false`): Wenn `true`, werden Klassen, Records und Structs, die mit dem `[GeneratedCode]` oder `[GeneratedCodeAttribute]` Attribut deklariert sind, vollständig von der Analyse übersprungen (inkl. ihrer Methoden und Member).

#### Empfohlene Standardkonfiguration:

```json
"FileFilters": {
  "ExcludeFilePatterns": [
    "*.designer.cs",
    "*.g.cs",
    "*.generated.cs",
    "AssemblyInfo.cs",
    "*.AssemblyAttributes.cs"
  ],
  "ExcludeDirectoryPatterns": [
    "obj/",
    "bin/"
  ],
  "SkipGeneratedCodeAttribute": true
}
```

### StaticTestSentinel-Konfiguration

Der `StaticTestSentinel` meldet Klassen als nicht abgedeckt, wenn ihre maximale kognitive Komplexität über `MinCognitiveComplexityForTest` liegt und keine Testabdeckung gefunden wurde. Für Klassen, bei denen Unit-Tests schwierig oder nicht sinnvoll sind, bietet die Sektion `"TestSentinel"` gezielte Exemptions.

#### Testabdeckungs-Erkennung

Der Sentinel erkennt Testabdeckung über drei Wege (alle konfigurierbar):

1. **Testklassen-Name:** Eine Klasse `{Name}Tests` oder `{Name}*Tests` wurde gefunden.
2. **`typeof`-Referenz:** Eine Testklasse enthält `typeof(MyClass)`.
3. **`// @covers`-Kommentar:** Eine Datei enthält `// @covers MyClass`.

#### Testprojekt-Erkennung

Der Sentinel erkennt Testprojekte primär über Metadatenreferenzen (xunit, nunit, testplatform, unittesting). Als Fallback gilt der Projektname: Projekte deren Name mit einem der konfigurierten Suffixe endet, werden als Testprojekte behandelt.

- **`TestProjectNameSuffixes`** (Array von Strings, Default: `["Tests", "Test", "IntegrationTests", "Specs", "Spec"]`): Projekt-Name-Suffixe als Fallback wenn keine Testrahmenbibliothek in den Metadaten erkannt wird. Deckt reine Integration-Test-Projekte ab, die nur über `Microsoft.AspNetCore.Mvc.Testing` referenzieren.

#### Klassen-Exemptions

- **`ExemptClassNameSuffixes`** (Array von Strings, Default: `["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"]`): Klassen deren Name mit einem dieser Suffixe endet, werden vollständig übersprungen.
- **`ExemptWhenInheritsFrom`** (Array von Strings, Default: `["ComponentBase", "IValueConverter", "Profile"]`): Klassen die von einem dieser Typen erben oder Interfaces implementieren, werden übersprungen. Nützlich für Blazor-Komponenten (`ComponentBase`), WPF-Konverter (`IValueConverter`) oder AutoMapper-Profile (`Profile`).
- **`ExemptStaticClasses`** (Boolean, Default: `true`): Statische Klassen (z. B. `public static class StringExtensions`) werden übersprungen.

#### Empfohlene Konfiguration für WPF-Projekte:

```json
"TestSentinel": {
  "ExemptClassNameSuffixes": ["Extensions", "Constants", "Converter"],
  "ExemptWhenInheritsFrom": ["IValueConverter"],
  "ExemptStaticClasses": true
}
```

#### Empfohlene Konfiguration für Blazor-Projekte:

```json
"TestSentinel": {
  "ExemptWhenInheritsFrom": ["ComponentBase", "LayoutComponentBase"],
  "ExemptClassNameSuffixes": ["Extensions", "Constants"],
  "ExemptStaticClasses": true
}
```

### EnforceResultPatternOverExceptions — Namespace-Allow-Liste

Die Regel `EnforceResultPatternOverExceptions` ist standardmäßig **deaktiviert** (`false`). Wenn aktiviert, verbietet sie `throw` für fachlichen Kontrollfluss. Für Infrastruktur- und ASP.NET-Code — wo `throw` das übliche Idiom ist — stehen zwei neue Ausnahme-Mechanismen zur Verfügung:

- **`ResultPatternAllowThrowInNamespaceSuffixes`** (Array von Strings, Default: `["Infrastructure", "Endpoints", "Middleware", "Program"]`): Alle `throw`-Statements in Namespaces, die mit einem dieser Segmente enden, werden ignoriert. Segment-basierter Match: `MyApp.Infrastructure` wird mit Suffix `"Infrastructure"` erkannt.
- **`ResultPatternAllowCatchRethrow`** (Boolean, Default: `true`): Ein bloßes `throw;` ohne Expression (Rethrow in Catch) ist immer erlaubt. Das ist idomatisches C# für Log-and-Rethrow-Muster.

#### Empfohlene Konfiguration (Strict-Profil mit Ausnahmen):

```json
"Global": {
  "EnforceResultPatternOverExceptions": true,
  "ResultPatternAllowThrowInNamespaceSuffixes": [
    "Infrastructure",
    "Endpoints",
    "Middleware",
    "Program"
  ],
  "ResultPatternAllowCatchRethrow": true
}
```

> Fachliche Fehler → `Result<T>`; Infrastruktur/Unerwartetes → `throw` + Log. Die `AllowedExceptions`-Liste (z. B. `ArgumentNullException`) bleibt für typ-basierte Ausnahmen unverändert aktiv.

### Vermeidung von Middle-Man-Klassen (AvoidExcessiveMiddleMen)

Die Regel `AvoidExcessiveMiddleMen` ist standardmäßig **aktiviert** (`true`). Sie analysiert Klassen daraufhin, ob sie überwiegend als reine Weiterleitungsschichten ("Middle Man") agieren. Dies ist ein Design-Constraint für agentische KI-Systeme, da tiefe Weiterleitungsketten den Agenten zwingen, viele Dateien nacheinander zu lesen (Tool-Call-Inflation) und den Kontext mit redundantem Wrapper-Code zu füllen.

#### Einstellungsoptionen

- **`AvoidExcessiveMiddleMen`** (Boolean, Default: `true`): Aktiviert oder deaktiviert den Middle-Man-Check.
- **`MaxMiddleManForwardingRatio`** (Double, Default: `0.60`): Grenzwert für das Verhältnis von Weiterleitungen zur Gesamtanzahl berücksichtigter Methoden und Properties der Klasse. Eine Klasse mit z. B. 10 Methoden, von denen 7 nur Aufrufe weiterleiten (Ratio: 70%), wird abgemahnt. (Bezieht bei `MiddleManIncludePrivateMembers: true` auch private Member ein).
- **`MiddleManMinMemberCount`** (Integer, Default: `5`): Mindestanzahl berücksichtigter Mitglieder (Methoden/Properties) in einer Klasse, ab der die Regel überhaupt greift. Kleine Klassen (z. B. einfache Adapter oder Wrapper mit 2–4 Membern) werden ignoriert, um Fehlalarme zu vermeiden. (Bezieht bei `MiddleManIncludePrivateMembers: true` auch private Member ein).
- **`MiddleManIncludePrivateMembers`** (Boolean, Default: `false`): Wenn `true`, werden auch private Methoden und Properties auf Weiterleitungen analysiert. Explizite Interface-Implementierungen (z. B. `void IDisposable.Dispose()`) bleiben standardmäßig ausgenommen, da sie syntaktisch erzwungen sind.
- **`MiddleManExemptSuffixes`** (Array von Strings, Default: `["Extensions", "Proxy", "Adapter", "Facade"]`): Klassen, deren Name mit einem dieser Suffixe endet, werden vom Check ausgenommen.
- **`MiddleManExemptBaseTypes`** (Array von Strings, Default: `["ComponentBase", "LayoutComponentBase"]`): Klassen, die von einem dieser Typen erben oder implementieren, werden vollständig vom Middle-Man-Check ausgenommen.

#### Erkennungslogik

Eine Methode oder Property wird als **Weiterleitung (Pure Forwarder)** gewertet, wenn:
* Sie als Expression-Body (`=>`) oder als Block mit genau einer return- oder Ausdrücksanweisung deklariert ist.
* Der Rumpf ausschließlich an ein Feld, eine Eigenschaft oder eine statische Methode einer *anderen* Klasse (Collaborator) delegiert.
* Keine Bedingungen (`if`), Schleifen (`foreach`), lokale Variablen oder `try-catch`-Blöcke enthalten sind.
* Aufrufe an lokale Hilfsmethoden der gleichen Klasse oder an geerbte Methoden von Basisklassen zählen *nicht* als Weiterleitung an externe Collaborators.

#### Empfohlene Konfiguration:

```json
"Global": {
  "AvoidExcessiveMiddleMen": true,
  "MaxMiddleManForwardingRatio": 0.60,
  "MiddleManMinMemberCount": 5,
  "MiddleManIncludePrivateMembers": false,
  "MiddleManExemptSuffixes": [
    "Extensions",
    "Proxy",
    "Adapter",
    "Facade"
  ],
  "MiddleManExemptBaseTypes": [
    "ComponentBase",
    "LayoutComponentBase"
  ]
}
```

### Profil-Vorlagen

Für häufige Einsatzszenarien können alle oben genannten Exemptions als vollständige `ainetlinter-rules.json`-Datei zusammengestellt werden.

#### WPF-Profil

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": true,
    "SealedClassExemptSuffixes": ["Base", "ViewModel"],
    "EnforceNoSilentCatch": true,
    "AllowCancellationShutdownCatch": true,
    "EnforceExplicitStateImmutability": true,
    "ImmutabilityExemptBaseTypes": [
      "ObservableObject",
      "ObservableRecipient",
      "INotifyPropertyChanged"
    ],
    "ImmutabilityAllowPrivateBackingFields": true,
    "EnforceResultPatternOverExceptions": false
  },
  "Metrics": {
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": [
      "System.",
      "System.Windows.",
      "Microsoft.UI."
    ],
    "MaxConstructorDependencies": 5,
    "ConstructorDependencyIgnoreTypePrefixes": [
      "ILogger",
      "IOptions",
      "IHostEnvironment"
    ]
  },
  "FileFilters": {
    "ExcludeFilePatterns": ["*.designer.cs", "*.g.cs"],
    "ExcludeDirectoryPatterns": ["obj/", "bin/"],
    "SkipGeneratedCodeAttribute": true
  },
  "TestSentinel": {
    "ExemptClassNameSuffixes": ["Converter", "Extensions", "Constants"],
    "ExemptWhenInheritsFrom": ["IValueConverter"],
    "ExemptStaticClasses": true
  },
  "UiSeparation": {
    "WpfRequireMinimalCodeBehind": true,
    "WpfCodeBehindBaseTypes": [
      "Window",
      "UserControl",
      "Page",
      "NavigationWindow"
    ],
    "BlazorRequireCodeBehind": false,
    "BlazorRequireCssIsolation": false
  }
}
```

#### Blazor-Profil

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": true,
    "EnforceExplicitStateImmutability": true,
    "ImmutabilityExemptBaseTypes": [
      "ComponentBase",
      "LayoutComponentBase",
      "AuthenticationStateProvider",
      "BackgroundService"
    ],
    "ImmutabilityAllowPrivateBackingFields": false,
    "EnforceResultPatternOverExceptions": false
  },
  "Metrics": {
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": [
      "Microsoft.AspNetCore.",
      "Microsoft.Extensions."
    ],
    "ConstructorDependencyIgnoreTypePrefixes": [
      "ILogger",
      "IOptions",
      "IHttpContextAccessor"
    ]
  },
  "FileFilters": {
    "ExcludeFilePatterns": ["*.g.cs", "*.generated.cs"],
    "ExcludeDirectoryPatterns": ["obj/", "bin/"],
    "SkipGeneratedCodeAttribute": true
  },
  "TestSentinel": {
    "ExemptWhenInheritsFrom": ["ComponentBase", "LayoutComponentBase"],
    "ExemptClassNameSuffixes": ["Extensions", "Constants"],
    "ExemptStaticClasses": true
  },
  "UiSeparation": {
    "BlazorRequireCodeBehind": true,
    "BlazorRequireCssIsolation": true,
    "WpfRequireMinimalCodeBehind": false,
    "BlazorExcludeFileNames": ["_Imports.razor", "App.razor", "Routes.razor"]
  }
}
```

## CompoundSuppressions

Kontextabhängige Unterdrückung von Regeln wenn koinzidente Metriken niedrig sind.

```json
"Metrics": {
  "CompoundSuppressions": [
    {
      "TargetRule": "MaxMethodLineCount",
      "WhenAllOf": [
        { "Metric": "CyclomaticComplexity", "AtMost": 3 },
        { "Metric": "CognitiveComplexity",  "AtMost": 5 }
      ],
      "RelaxedLimit": 150,
      "SeverityOverride": "warning",
      "Reason": "Init-Methoden sind semantisch flach."
    }
  ]
}
```

### Felder

| Feld                  | Beschreibung                                                                                                                                                                                                          |
| :-------------------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TargetRule`          | Rule-ID (z.B. `MaxMethodLineCount`)                                                                                                                                                                                   |
| `WhenAllOf[].Metric`  | Metric-Name (siehe unten)                                                                                                                                                                                             |
| `WhenAllOf[].AtMost`  | Bedingung: Metrik ≤ Wert                                                                                                                                                                                              |
| `WhenAllOf[].AtLeast` | Bedingung: Metrik ≥ Wert                                                                                                                                                                                              |
| `RelaxedLimit`        | Relaxiertes Limit wenn aktiv. Fehlt = vollständig supprimieren                                                                                                                                                        |
| `SeverityOverride`    | Optionale Severity-Herabstufung für Violations in Szenario A (Bedingungen erfüllt, RelaxedLimit überschritten). Erlaubte Werte: `"warning"`, `"error"`. Wirkt nur in Kombination mit `RelaxedLimit`. Standard: `null` |
| `Reason`              | Freitext, erscheint in `.mdc` und Violation-Guidance                                                                                                                                                                  |

### Unterstützte Metric-Namen

**Methoden-Ebene:** `CyclomaticComplexity`, `CognitiveComplexity`, `ParameterCount`, `LineCount`  
**Klassen-Ebene:** `ConstructorDependencies`, `PublicMemberCount`

### Klassen-Ebene: Beispiel Interface-Adapter

```json
{
  "TargetRule": "MaxPublicMembersPerType",
  "WhenAllOf": [{ "Metric": "ConstructorDependencies", "AtMost": 2 }],
  "Reason": "Interface-Adapter mit wenigen Deps sind trotz breiter API schwach gekoppelt."
}
```

---

---

## 10. Performance-Profiling & Zeitmessung

Um Performance-Flaschenhälse in großen C#-Solutions gezielt zu analysieren, besitzt `AiNetLinter` ein integriertes Profiling-System.

### Funktionsweise

Wenn das Profiling aktiv ist, misst der Linter automatisch die Ausführungszeit der verschiedenen Verarbeitungsphasen und schreibt detaillierte Reports in den `measurements/`-Ordner direkt neben der ausführbaren Datei:

```
[Ausführungsverzeichnis]/measurements/[ProjektName]/[yyyy-MM-dd]/[ProjektName]-[Zeitstempel]-[UUID]/
  ├── performance.log   <-- Gut lesbarer Textbericht mit Phasenanalyse und den Top-20 langsamsten Dateien
  └── performance.json  <-- Strukturierte JSON-Datei für automatische Auswertungen
```

### Konfiguration

Das Feature ist standardmäßig aktiviert und kann über die Konfigurationsdatei `ainetlinter-rules.json` deaktiviert werden:

```json
"Global": {
  "EnablePerformanceProfiling": false
}
```

---

## 11. Analyse-Cache (Inkrementelle Laufzeitoptimierung)

Um die Latenz im agentischen Entwicklungszyklus ("Agentic Feedback Loop") zu minimieren, besitzt `AiNetLinter` einen inkrementellen Analyse-Cache.

### Funktionsweise

Bei jedem Linter-Durchlauf berechnet `AiNetLinter` für jede C#-Datei einen SHA-256-Hash über deren Inhalt. Ist die Datei seit der letzten Prüfung unverändert, werden ihre gemeldeten Regelverstöße, deklarierten Klassen, `partial`-Teile sowie Testabdeckungssignale direkt aus dem Cache geladen.
Die zeitintensive semantische Roslyn-Analyse (`GetSemanticModelAsync()`) wird für diese Dateien vollständig übersprungen.

### Cache-Ort & Benennung

Der Cache wird im Unterordner `cache/` direkt neben der ausführbaren Datei (`AiNetLinter.exe`) abgelegen. Für jede Solution wird eine separate Cache-Datei angelegt:

```
[Ausführungsverzeichnis]/cache/
  ├── MySolution-a1b2c3d4.json
  └── OtherSolution-f9e7c123.json
```

Der 8-stellige Datei-Hash (`hash8`) basiert auf dem normalisierten absoluten Pfad der Solution-Datei und dem exakten Inhalt der verwendeten Konfigurationsdatei (`ainetlinter-rules.json`).

### Cache-Invalidierung

Die Cache-Validierung erfolgt vollautomatisch:

- **Konfigurationsänderungen:** Eine Anpassung der Linter-Regeln in der `ainetlinter-rules.json` ändert den Datei-Hash im Cache-Dateinamen. Es wird automatisch eine neue Cache-Datei erzeugt.
- **Dateiveränderungen:** Geänderte Dateien besitzen einen neuen Inhalts-Hash und werden automatisch neu analysiert; ihr Cache-Eintrag wird aktualisiert.
- **Tool-Updates:** Bei Schema-Änderungen des Linters wird der Cache über eine interne `SchemaVersion` automatisch vollständig invalidiert.

### TTL-basierte Bereinigung (`--cache-ttl`)

Beim Start jedes Analyse-Runs bereinigt `AiNetLinter` automatisch alle Cache-Dateien im `cache/`-Verzeichnis, deren letzte Schreibzeit (`LastWriteTimeUtc`) älter als der konfigurierte Schwellenwert ist. Die Bereinigung ist global — sie erfasst Leichen aus allen bisherigen Solutions und Rules-Kombinationen.

```powershell
# Standardlauf: Cache-Dateien älter als 60 Minuten werden gelöscht
AiNetLinter.exe --config ainetlinter-rules.json --path .

# Längere Lebensdauer für CI/CD oder manuelle Nutzung
AiNetLinter.exe --config ainetlinter-rules.json --path . --cache-ttl 240

# Kein automatisches Löschen
AiNetLinter.exe --config ainetlinter-rules.json --path . --cache-ttl 0
```

| `--cache-ttl`   | Verhalten                                             |
| :-------------- | :---------------------------------------------------- |
| `60` (Standard) | Cache-Dateien > 60 Min alt werden beim Start gelöscht |
| `0`             | Keine Bereinigung — Cache lebt unbegrenzt             |
| `> 0`           | Bereinigung nach dem angegebenen Minutenwert          |

### Deaktivierung über CLI

Der Cache ist standardmäßig **aktiviert**. Wenn eine vollständige Neu-Analyse aller Dateien erzwungen werden soll:

```powershell
AiNetLinter.exe --path . --config ainetlinter-rules.json --no-cache
```

---

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
