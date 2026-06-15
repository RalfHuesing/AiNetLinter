# AiNetLinter — Konfigurationsreferenz & Dokumentation

→ [README](../README.md) | [Design-Rationale](rationale.md)

---

## 1. Der "AI-Mittelweg" für DRY vs. WET

Die klassische Regel **DRY** (Don't Repeat Yourself) führt bei extremem Einsatz zu tiefen, generischen Abstraktionen, die für KIs schwer verständlich sind und den gefürchteten "Schmetterlingseffekt" (Änderung an einer Stelle bricht unbemerkt 10 andere Stellen) begünstigen. `AiNetLinter` unterstützt einen pragmatischen Mittelweg:

1.  **Fachliches DRY (Strikt):** Kern-Geschäftslogik und Berechnungen müssen zentral und wiederverwendbar sein (z. B. in Domain-Modellen oder Services). Die KI muss diese Logik nur an einem einzigen Ort ändern.
2.  **Technisches WET (Erlaubt):** Controller, DTOs, Mapper und Queries dürfen redundant bzw. spezifisch pro Use Case (Vertical Slice) aufgebaut sein. Dies minimiert Seiteneffekte und verhindert, dass die KI riesige, geteilte Basisklassen anpassen muss und dabei andere Features beschädigt.

---

## 2. Kernfeatures

*   **Roslyn-basierte semantische Analyse:** Evaluierung der gesamten Solution (.sln / .slnx) über einen einzigen Syntax-Walk pro Dokument. Nutzt echte Semantik-Informationen statt textbasierter Heuristiken. MSBuild Design-Time-Properties beschleunigen das Solution-Laden; die Dokument-Analyse läuft parallel bis `Environment.ProcessorCount`.
*   **Feingranulares Regelwerk:** Umfassende Regeln für Klassendesign (Sealed, Value Objects, Vererbungstiefe), Variablen/Typen (kein `dynamic`, keine `out`-Parameter, Nullable Context) und Code-Komplexität (McCabe, SonarSource).
*   **PascalCase- & Namensvalidierung:** Typprüfung auf PascalCase-Konventionen sowie Erkennung nicht-semantischer Bezeichner (z. B. `data`, `temp`, `obj`).
*   **LSP-Dokumentationstests:** Erzwingt die Verwendung von XML-Docs (`/// <summary>`) auf öffentlichen APIs.
*   **Static Test Sentinel:** Statische Test-Präsenzprüfung für komplexe Quellcodeabschnitte anhand von Metadaten-Scans auf referenzierte Testbibliotheken (xunit, nunit etc.).
*   **Namespace-Abhängigkeitsprüfung (Vertical Slices):** Verhindert unerlaubte slice-übergreifende Abhängigkeiten, auch bei vollqualifizierten Typnamen.
*   **Warnungs-Unterdrückung (Suppression):** Flexibles Deaktivieren von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]`, dateiweit oder komplett per `// ainetlinter-disable all`.
*   **Gezielte Bulk-Suppression (`--add-disable-all` / `--remove-disable-all`):** Audit-basiertes Einfügen des Disable-all-Kommentars nur in Dateien mit Verstößen sowie sicheres Entfernen exakter Disable-all-Zeilen.
*   **SARIF- & Dependency-Graph-Export:** Generierung strukturierter SARIF-Fehlerberichte für CI/CD sowie automatisches Zeichnen von Mermaid-Abhängigkeitsdiagrammen.
*   **Baseline-Ratchet (Checksum):** Inkrementelle Migration bestehender Codebases — unveränderte Dateien werden per SHA-256 eingefroren, Verstöße nur in geänderten Dateien gemeldet.
*   **Projekt-spezifische Regel-Konfiguration (Project Overrides):** Flexibles Überschreiben oder Deaktivieren von Linter-Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) in der Konfiguration.
*   **AI-Context-Footprint (Metrik):** Berechnet die Summe aller Codezeilen einer Klasse inklusive aller transitiv referenzierten eigenen Typen, um hohe Kopplung und große Kontext-Footprints für KIs zu vermeiden.
*   **Automatisch generiertes Repo-Playbook:** Analysiert die Codebase und generiert eine Übersicht über genutzte Muster und Unterdrückungsstatistiken zur automatischen Kontext-Adaption für KI-Agenten.
*   **Roslyn-basierter CLI Auto-Fixer (`--fix`):** Vollautomatische Behebung trivialer Linter-Verstöße (z. B. fehlendes `sealed`, `readonly` oder `#nullable enable`) über Syntaxbaum-Transformationen.
*   **Semantische Diff-Impact-Analyse (`--impact`):** Git-gestützte Auswirkungsanalyse, die bei Signaturänderungen alle betroffenen Aufrufstellen (Call-Sites) in der gesamten Solution ermittelt.
*   **Analyse-Cache (Inkrementelle Optimierung):** Cache zur Vermeidung wiederholter semantischer Analysen für unveränderte C#-Dateien. Reduziert die Ausführungszeit bei inkrementellen Agenten-Runs drastisch. Standardmäßig aktiv; deaktivierbar über `--no-cache`.
*   **Performance-Profiling & Zeitmessung:** Erfassung der Ausführungszeiten aller Linter-Phasen (Workspace-Laden, Dateianalyse, Post-Checks) und automatische Generierung strukturierter Berichte (`performance.log` & `performance.json`) unter `measurements/` zur Analyse von Performance-Engpässen.

---

## 3. Konfiguration (`rules.json`)

Die Konfiguration erfolgt über eine flache, leicht verständliche JSON-Struktur. Beispiel einer vollständigen Konfiguration:

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
    "EnforceNoVariableShadowing": true,
    "EnforceReadonlyParameters": true,
    "EnforceReadonlyFields": true,
    "EnforceNoMagicValues": true,
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
    "EnforceStrictBoundaryForBusinessLogic": true,
    "PreventContextDependentOverloads": true,
    "RequireExplicitTruncationHandling": true,
    "EnforceNamespaceDirectoryMapping": true,
    "DetectAndBanPhantomDependencies": true,
    "ImmutabilityExemptSuffixes": ["Dto", "Entity", "Model", "Request", "Response", "Command"]
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxMethodLineCount": 42,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5,
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": [
      "System.",
      "Microsoft.UI.",
      "System.Windows."
    ],
    "MinCognitiveComplexityForTest": 3,
    "AggregatePartialClassLineCount": false,
    "MaxMethodOverloads": 3,
    "MaxConstructorDependencies": 5,
    "MaxDirectoryDepth": 4,
    "MaxAIContextFootprint": 5000
  },
  "TestSentinel": {
    "ClassNamePatterns": ["{Name}Tests", "{Name}Test", "{Name}IntegrationTests", "{Name}*Tests"],
    "RecognizeTypeofReference": true,
    "RecognizeCoversComment": true,
    "ExemptClassNameSuffixes": ["Extensions", "Constants", "Converter", "Profile"],
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

| Regel | Bereich | Beschreibung |
| :--- | :--- | :--- |
| `EnforceSealedClasses` | Global | Zwingt alle konkreten Klassen dazu, als `sealed` deklariert zu werden. |
| `AllowUnsealedPartialClasses` | Global | Erlaubt es, `partial` Klassen unsealed zu lassen (Standard: `false`, nützlich z. B. bei WPF Code-Behind oder Blazor Page-Components). |
| `SealedClassExemptSuffixes` | Global | Liste von Klassenname-Suffixen, die von der `EnforceSealedClasses`-Prüfung ausgenommen sind (z. B. `["Base", "Foundation", "Host"]`). |
| `AllowDynamic` | Global | Verbietet das Typschlüsselwort `dynamic` (verhindert statische Analyse-Lücken). |
| `AllowOutParameters` | Global | Verbietet `out`-Parameter zugunsten von C#-Tuples oder Records. |
| `AllowTryPatternOutParameters` | Global | Erlaubt `out` in `bool Try*`-Methoden (Standard: `true`, idiomatisches C#). |
| `AllowCancellationShutdownCatch` | Global | Erlaubt stummes Abfangen von Cancellation-Exceptions (wie `OperationCanceledException` oder `TaskCanceledException`) bei Host-Shutdown (ohne Pflicht eines `when`-Filters). |
| `EnforceMinimalApiAsParameters` | Global | Prüft Minimal-API-Endpunkte auf fehlendes `[AsParameters]` bei >4 Parametern (opt-in). |
| `EnforceValueObjectContracts` | Global | Zwingt Klassen mit Suffix `ValueObject` dazu, als `record` oder `readonly struct` deklariert zu sein und nur unveränderliche Eigenschaften (ohne `set`) zu haben. |
| `EnableTestSentinel` | Global | Aktiviert den Test-Präsenzwächter für komplexe Quellcodedateien. |
| `EnforcePascalCase` | Global | Validiert PascalCase-Schreibweise für Klassen, Structs, Records, Interfaces, Methoden und Properties. |
| `EnforceXmlDocumentation` | Global | Erzwingt XML-Dokumentationskommentare an öffentlichen Typ-Deklarationen (Klassen/Interfaces) (Standard: `false`). |
| `EnforceSemanticNaming` | Global | Markiert generische Parameternamen (z. B. `data`, `temp`, `val`) in öffentlichen Methoden als Fehler. |
| `EnforceNullableEnable` | Global | Stellt sicher, dass `#nullable enable` in jeder Datei deklariert ist oder global über csproj erzwungen wird. |
| `EnforceNoSilentCatch` | Global | Verbietet stumme `catch`-Blöcke. Ein Catch-Block gilt als stumm (verschluckt), wenn er leer ist und weder `throw`, Methodenaufrufe (Invocations), Rückgabeanweisungen (`return`) noch Zuweisungen (`assignment`) an Felder/Eigenschaften enthält. Variable Namen, die mit `ignored` oder `expected` beginnen (z. B. `catch (Exception ignored)`), oder der Inline-Kommentar `// ainetlinter-disable EnforceNoSilentCatch` deaktivieren die Prüfung. |
| `EnforceResultPatternOverExceptions` | Global | Verbietet `throw` für fachlichen Kontrollfluss. Technische Standard-Exceptions (wie `ArgumentNullException`) sind für Fail-Fast erlaubt. |
| `ResultPatternAllowThrowInNamespaceSuffixes` | Global | Namespace-Suffixe, für die `throw` explizit erlaubt ist (z. B. `["Infrastructure", "Middleware"]`). Segment-basierter Match: `MyApp.Infrastructure` endet mit `.Infrastructure`. Standard: `[]`. |
| `ResultPatternAllowCatchRethrow` | Global | Bare `throw;` (Rethrow in einem Catch-Block ohne erneut zu konstruieren) ist immer erlaubt wenn `true`. Standard: `true`. |
| `EnforceNoVariableShadowing` | Global | Verbietet das Verdecken von Feldern, Eigenschaften und äußeren Parametern durch lokale Variablen und Parameter. |
| `EnforceReadonlyParameters` | Global | Verbietet das Überschreiben von Methodenschnittstellen-Parametern (Verbot von Parameter-Reassignment). |
| `EnforceReadonlyFields` | Global | Prüft, ob private Felder, die nur im Konstruktor/Initialisierer zugewiesen werden, als `readonly` deklariert sind. |
| `EnforceNoMagicValues` | Global | Verbietet Magic Numbers und Magic Strings direkt in Methodenkörpern außerhalb von Konstanten-Deklarationen (Ausnahmen: `0`, `1`, `""`). |
| `EnforceExplicitStateImmutability` | Global | Zwingt alle Klassen (außer DTOs/Entities) zu Immutabilität (init/get-only Eigenschaften und private readonly Felder). |
| `ImmutabilityExemptBaseTypes` | Global | Liste von Basisklassen oder Schnittstellen, von denen erbende/implementierende Klassen vollständig von der Immutability-Prüfung ausgenommen sind (z. B. `["ComponentBase", "ObservableObject"]`). |
| `ImmutabilityAllowPrivateBackingFields` | Global | Erlaubt private mutable Felder mit Unterstrich (`_`) Präfix (z. B. typische WPF MVVM Backing-Felder) (Standard: `false`). |
| `EnforceStrictBoundaryForBusinessLogic` | Global | Zwingt reine Rechen- und Logikfunktionen in zustandslose `static` Methoden ohne I/O-Aufrufe. |
| `PreventContextDependentOverloads` | Global | Verbietet Methodenüberladungen, die sich nur durch primitive Typen bei gleicher Parameteranzahl unterscheiden. |
| `RequireExplicitTruncationHandling` | Global | Erzwingt unmittelbare Validierung (Länge/EOF-Check) nach I/O- und Stream-Leseoperationen. |
| `EnforceNamespaceDirectoryMapping` | Global | Stellt sicher, dass deklarierte Namespaces exakt der physischen Ordnerstruktur entsprechen. |
| `DetectAndBanPhantomDependencies` | Global | Verbietet die Einbindung nicht auflösbarer Namespaces sowie dynamische Reflection-Lade-APIs. |
| `EnablePerformanceProfiling` | Global | Aktiviert die automatisierte Laufzeit-Messung aller Linter-Phasen und Dateianalysen (Standard: `true`). |
| `MaxLineCount` | Metrics | Maximale Zeilenanzahl pro Datei (Standard: 500). |
| `MaxMethodParameterCount`| Metrics | Maximale Parameteranzahl pro Methode (Standard: 4). |
| `MaxMethodLineCount` | Metrics | Maximale Codezeilenanzahl pro Methode ohne Kommentare/Leerzeilen (Standard: 42). |
| `MaxCyclomaticComplexity`| Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode (Standard: 5). |
| `MaxCognitiveComplexity` | Metrics | Maximale kognitive Komplexität (SonarSource) pro Methode (Standard: 5). |
| `MaxInheritanceDepth` | Metrics | Maximale Tiefe der Vererbungshierarchie (Standard: 2). |
| `InheritanceDepthFrameworkPrefixes` | Metrics | Namespace-Präfixe von Framework-Basistypen, die beim Zählen der Vererbungstiefe ignoriert werden (z. B. `["System.", "System.Windows."]`). |
| `MinCognitiveComplexityForTest` | Metrics | Schwellenwert der kognitiven Komplexität, ab dem der Test Sentinel eine zugehörige Testklasse einfordert. |
| `AggregatePartialClassLineCount` | Metrics | Summiert Zeilenanzahl über alle `partial`-Teile eines Typs (opt-in). |
| `MaxMethodOverloads` | Metrics | Maximale Anzahl von Methoden-Überladungen pro Name in einer Klasse (Standard: 3). |
| `MaxConstructorDependencies` | Metrics | Maximale Parameter-Anzahl pro Konstruktor / Primärkonstruktor (Standard: 5). Records und Structs, bei denen **alle** Parameter Default-Werte haben, werden automatisch ausgenommen (Options/Config-Objects). |
| `ConstructorDependencyIgnoreTypePrefixes` | Metrics | Typ-Name-Präfixe von Framework- oder Cross-Cutting-Abhängigkeiten, die bei `MaxConstructorDependencies` nicht mitgezählt werden (z. B. `["ILogger", "IOptions"]`). |
| `MaxDirectoryDepth` | Metrics | Maximale Ordnertiefe ab csproj-Ebene (Standard: 4). |
| `MaxAIContextFootprint` | Metrics | Die maximale Anzahl transitiver Codezeilen von Klassenabhängigkeiten (Standard: 5000). |
| `TestSentinel.ClassNamePatterns` | Config | Muster für Testklassen-Namen, z. B. `["{Name}Tests", "{Name}*Tests"]`. |
| `TestSentinel.RecognizeTypeofReference` | Config | Erkennt `typeof(MyClass)` in einer Testklasse als Abdeckung. Standard: `true`. |
| `TestSentinel.RecognizeCoversComment` | Config | Erkennt `// @covers MyClass`-Kommentare als Abdeckung. Standard: `true`. |
| `TestSentinel.ExemptClassNameSuffixes` | Config | Klassen mit diesen Namens-Suffixen werden vom Sentinel ausgenommen (z. B. `["Extensions", "Constants", "Converter"]`). |
| `TestSentinel.ExemptWhenInheritsFrom` | Config | Klassen die von einem dieser Typen erben oder Interfaces implementieren, werden ausgenommen (z. B. `["ComponentBase", "IValueConverter"]`). |
| `TestSentinel.ExemptStaticClasses` | Config | Statische Klassen werden vom Sentinel ausgenommen wenn `true`. Standard: `false`. |
| `RuleMetadata` | Config | Severity (`error`/`warning`) und Intent-Tags pro Regel für LLM-Priorisierung. |

### Projekt-spezifische Regel-Konfiguration (Project Overrides)

In großen Solutions können verschiedene Projekte unterschiedliche Qualitätsanforderungen haben. In Testprojekten sind beispielsweise literale Werte (Magic Values) in Assertions erwünscht. Über die Sektion `"ProjectOverrides"` in der `rules.json` können Regeln gezielt für bestimmte Projekte (z. B. über Wildcards wie `*.Tests`) überschrieben werden:

```json
  "ProjectOverrides": {
    "*.Tests": {
      "Global": {
        "EnforceNoMagicValues": false,
        "EnforceSealedClasses": false
      },
      "Metrics": {
        "MaxMethodLineCount": 100
      }
    }
  }
```

### MagicValues-Konfiguration

Der Bool-Schalter `EnforceNoMagicValues` in der `Global`-Sektion ist weiterhin der Haupt-Switch, um die Magic-Value-Erkennung zu aktivieren oder zu deaktivieren. Wenn diese Regel aktiv ist, kann über die Sektion `"MagicValues"` das Erkennungsverhalten detailliert konfiguriert werden.

#### Einstellungsoptionen

- **`Mode`** (String, Default: `"all"`):
  - `"all"`: Alle String- und numerischen Literale im Rumpf von Methoden werden als Magic Values gewertet (bisheriges Verhalten).
  - `"numeric-only"`: Nur numerische Literale (außer `0`, `1`, `-1` und in `IgnoreNumericValues` konfigurierte Werte) werden gemeldet. Strings werden komplett ignoriert.
  - `"numeric-and-short-string"`: Numerische Literale sowie String-Literale mit einer Länge kleiner als `MinStringLength` werden gemeldet.
- **`MinStringLength`** (Integer, Default: `0`): Mindestlänge für einen String, um als magic gewertet zu werden (nur aktiv im Modus `"numeric-and-short-string"`).
- **`IgnoreStringPatterns`** (Array von Strings, Default: `[]`): Regex-Muster für String-Literale, die ignoriert werden sollen (z. B. Routen-Muster like `^/[\w/{}\-]*$`).
- **`IgnoreNumericValues`** (Array von Numbers, Default: `[]`): Zusätzliche numerische Werte, die ignoriert werden (z. B. Timeout- oder Batch-Größen wie `404` oder `1000`).
- **`IgnoreInvocationPrefixes`** (Array von Strings, Default: `[]`): String-Literale, die direkt als Argumente an Methoden übergeben werden, deren Name mit einem dieser Präfixe beginnt (z. B. `"Log"`, `"MapGet"`), werden ignoriert.
- **`IgnoreCollectionInitializers`** (Boolean, Default: `false`): Wenn `true`, werden Literale innerhalb von Collection-, Array- oder Dictionary-Initialisierern ignoriert.

#### Vorgefertigte Konfigurations-Profile

##### 1. Default-Profil (Bisheriges Standardverhalten)
```json
"Global": {
  "EnforceNoMagicValues": true
},
"MagicValues": {
  "Mode": "all",
  "MinStringLength": 0,
  "IgnoreStringPatterns": [],
  "IgnoreNumericValues": [],
  "IgnoreInvocationPrefixes": [],
  "IgnoreCollectionInitializers": false
}
```

##### 2. Pragmatic-Profil (Sinnvolle Standardregelung mit Fokus auf Zahlen)
```json
"Global": {
  "EnforceNoMagicValues": true
},
"MagicValues": {
  "Mode": "numeric-only"
}
```

##### 3. Metadata-Aware-Profil (Für moderne APIs und Metadaten-lastige Apps)
```json
"Global": {
  "EnforceNoMagicValues": true
},
"MagicValues": {
  "Mode": "numeric-only",
  "IgnoreStringPatterns": [
    "^/[\\w/{}\\-]*$",
    "^[a-z][a-zA-Z0-9_]*$"
  ],
  "IgnoreInvocationPrefixes": [
    "Log", "MapGet", "MapPost", "MapPut", "MapDelete", "MapGroup",
    "GetSection", "GetValue", "GetRequiredSection",
    "TypedResults.Problem", "Results.Problem"
  ],
  "IgnoreCollectionInitializers": true
}
```

### AI-Context-Footprint (Metrik)

Der AI-Context-Footprint berechnet die Summe aller Codezeilen der Klasse selbst plus aller transitiv im Quellcode referenzierten eigenen Klassen/Typen. Steigt diese Metrik über den konfigurierten Schwellenwert (`MaxAIContextFootprint`, standardmäßig `5000` Zeilen), wird ein Regelverstoß gemeldet. Dies hilft Entwicklern, hohe Kopplung zu vermeiden und die Token-Belastung für KIs gering zu halten.

### Ausnahmen für EnforceSealedClasses (WPF & Basisklassen)

Die Regel `EnforceSealedClasses` zwingt standardmäßig alle konkreten Klassen dazu, als `sealed` deklariert zu werden. In bestimmten Szenarien (z. B. WPF oder bei dedizierten Basisklassen) führt dies jedoch zu False-Positives:

1. **WPF Partial-Klassen:** Der XAML-Compiler generiert für Code-Behind-Dateien partial Klassen, die standardmäßig nicht `sealed` deklariert sind. 
2. **Designte Basisklassen:** Klassen, die als Basisklassen für Vererbung gedacht sind (z. B. `OrderHandlerBase`), sollten nicht versiegelt werden.

Hierfür stehen folgende Konfigurationsoptionen zur Verfügung:

- **`AllowUnsealedPartialClasses`** (Boolean, Default: `false`): Erlaubt es, `partial` Klassen unsealed zu lassen (z. B. `public partial class MainWindow : Window`). Klassen, die explizit `sealed partial` deklariert sind, werden weiterhin korrekt erkannt und führen zu keinem Verstoß.
- **`SealedClassExemptSuffixes`** (Array von Strings, Default: `[]`): Klassen, deren Name mit einem dieser Suffixe endet (z. B. `"Base"`, `"Foundation"`, `"Host"`), werden von der Prüfung ausgenommen.

#### Empfohlene Konfiguration für WPF- und UI-Projekte:

Da WPF-Templates standardmäßig unsealed partial Klassen generieren, empfiehlt sich ein Projekt-Override in der `rules.json`:

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
    string? PlaybookPath = null,
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

Die Regel `EnforceExplicitStateImmutability` zwingt standardmäßig alle Klassen (die keine DTOs oder Entities sind) zur Unveränderlichkeit. Da bei WPF-ViewModels (MVVM) und Blazor-Komponenten mutable Eigenschaften und private Backing-Felder unumgänglich sind, bietet der Linter hierfür dedizierte Ausnahmen:

- **`ImmutabilityExemptBaseTypes`** (Array von Strings, Default: `[]`): Klassen, die von einer dieser Basisklassen oder Schnittstellen erben (transitiv über die gesamte Hierarchie), werden vollständig von der Immutability-Prüfung ausgenommen (z. B. `["ComponentBase", "ObservableObject", "INotifyPropertyChanged"]`).
- **`ImmutabilityAllowPrivateBackingFields`** (Boolean, Default: `false`): Wenn `true`, werden private Felder, die mit einem Unterstrich (`_`) beginnen, nicht als Verstoß gemeldet. Dies erlaubt typische WPF-MVVM Backing-Felder.

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

- **`NamespaceDirectoryMappingMode`** (String, Default: `"exact"`):
  - `"exact"`: Der Namespace muss exakt auf den vollständigen physischen Ordnerpfad ab `.csproj` enden (bisheriges Standardverhalten).
  - `"suffix-match"`: Der Namespace muss auf die letzten N Segmente des Pfades enden. N wird über `NamespaceDirectoryMappingRequiredTrailingSegments` konfiguriert.
  - `"contains-all"`: Alle relevanten Pfad-Segmente müssen im deklarierten Namespace enthalten sein (Reihenfolge egal).
- **`NamespaceDirectoryMappingIgnorePathSegments`** (Array von Strings, Default: `[]`): Pfad-Segmente, die beim Abgleich ignoriert werden (z. B. `["src", "Source", "Domains"]`).
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
> Diese Regel ist standardmäßig deaktiviert und sollte nur in strikten Profilen oder bei klar definierten Projektarchitekturen aktiviert werden.

### Datei- und Verzeichnis-Ausschlüsse (FileFilters)

Bei auto-generiertem Code oder temporären Build-Dateien sind viele Linter-Regeln nicht sinnvoll. Über die Sektion `"FileFilters"` in der `rules.json` können bestimmte Dateien und Verzeichnis-Segmente von der Analyse ausgeschlossen werden.

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

#### Klassen-Exemptions

- **`ExemptClassNameSuffixes`** (Array von Strings, Default: `[]`): Klassen deren Name mit einem dieser Suffixe endet, werden vollständig übersprungen. Empfehlung: `["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"]`.
- **`ExemptWhenInheritsFrom`** (Array von Strings, Default: `[]`): Klassen die von einem dieser Typen erben oder Interfaces implementieren, werden übersprungen. Nützlich für Blazor-Komponenten (`ComponentBase`), WPF-Konverter (`IValueConverter`) oder AutoMapper-Profile (`Profile`).
- **`ExemptStaticClasses`** (Boolean, Default: `false`): Statische Klassen (z. B. `public static class StringExtensions`) werden übersprungen.

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

- **`ResultPatternAllowThrowInNamespaceSuffixes`** (Array von Strings, Default: `[]`): Alle `throw`-Statements in Namespaces, die mit einem dieser Segmente enden, werden ignoriert. Segment-basierter Match: `MyApp.Infrastructure` wird mit Suffix `"Infrastructure"` erkannt. Empfehlung: `["Infrastructure", "Endpoints", "Middleware", "Program"]`.
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

### Profil-Vorlagen

Für häufige Einsatzszenarien können alle oben genannten Exemptions als vollständige `rules.json`-Datei zusammengestellt werden.

#### WPF-Profil (`wpf.rules.json`)

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": true,
    "SealedClassExemptSuffixes": ["Base", "ViewModel"],
    "EnforceNoSilentCatch": true,
    "AllowCancellationShutdownCatch": true,
    "EnforceExplicitStateImmutability": true,
    "ImmutabilityExemptBaseTypes": ["ObservableObject", "ObservableRecipient", "INotifyPropertyChanged"],
    "ImmutabilityAllowPrivateBackingFields": true,
    "EnforceResultPatternOverExceptions": false
  },
  "Metrics": {
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": ["System.", "System.Windows.", "Microsoft.UI."],
    "MaxConstructorDependencies": 5,
    "ConstructorDependencyIgnoreTypePrefixes": ["ILogger", "IOptions", "IHostEnvironment"]
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
  }
}
```

#### Blazor-Profil (`blazor.rules.json`)

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
    "InheritanceDepthFrameworkPrefixes": ["Microsoft.AspNetCore.", "Microsoft.Extensions."],
    "ConstructorDependencyIgnoreTypePrefixes": ["ILogger", "IOptions", "IHttpContextAccessor"]
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
  }
}
```

---

## 4. Kompilieren & Bereitstellen (Build & Deployment)

Da `AiNetLinter` auf Roslyn-Compiler-Diensten und `MSBuildWorkspace` aufbaut, muss das Tool für die Verwendung in anderen Repositories speziell kompiliert und verpackt werden.

### Lokalen Build erzeugen
Um das Tool als eigenständiges, plattformspezifisches CLI-Tool für Windows zu kompilieren:
```bash
dotnet publish src/AiNetLinter/AiNetLinter.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

### WICHTIG: MSBuild-Abhängigkeiten (BuildHost-Ordner)
`MSBuildWorkspace` benötigt externe Host-Prozesse zum Parsen von Visual Studio Projektdateien. Nach dem Build müssen zwingend folgende Unterordner im selben Verzeichnis wie die `AiNetLinter.exe` liegen:
*   `BuildHost-netcore/`
*   `BuildHost-net472/`

Diese Ordner werden standardmäßig beim `dotnet publish` automatisch erzeugt. **Wenn Sie das Tool in ein anderes Repository kopieren (z. B. in einen `tools/`-Ordner), müssen diese beiden Unterordner mitsamt ihren DLLs zwingend mitkopiert werden.** Andernfalls bricht das Tool bei der Analyse einer Solution mit einem fatalen MSBuildWorkspace-Ladefehler ab.

---

## 5. CLI-Schnittstelle

`AiNetLinter` wird als Windows .NET 10 Core CLI-Tool ausgeführt.

### Aufruf-Syntax

```bash
ainetlinter --config <Pfad-zur-rules.json> --path <Pfad-zur-slnx-oder-Verzeichnis> [Optionen]
```

### Parameter

*   `-c`, `--config` (Pfad): Der Pfad zur `rules.json` (Erforderlich für Audit-Läufe; nicht nötig mit `--create-baseline`).
*   `-p`, `--path` (Pfad): Der Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (Erforderlich).
*   `--create-baseline` (Pfad): Erzeugt eine Baseline-JSON mit SHA-256-Checksummen aller `.cs`-Dateien (Optional).
*   `--baseline` (Pfad): Pfad zur Baseline-JSON für inkrementelle Migration — unterdrückt Verstöße in unveränderten Dateien (Optional).
*   `--add-disable-all` (Flag): Führt einen Audit-Lauf aus und fügt `// ainetlinter-disable all` nur in Dateien mit Verstößen ein; erfordert `--config` (Optional).
*   `--remove-disable-all` (Flag): Entfernt exakte `// ainetlinter-disable all`-Zeilen aus allen `.cs`-Dateien unter `--path`; erfordert keine `--config` (Optional).
*   `-g`, `--graph` (Pfad): Pfad für das zu generierende Mermaid-Abhängigkeitsdiagramm `.md` (Optional).
*   `-pb`, `--playbook` (Pfad): Pfad für das zu generierende AI Repository Playbook `.md` oder `.mdc` (Optional). Cursor-Frontmatter wird immer eingebettet — bei Ablage unter `.cursor/rules/` empfiehlt sich `.mdc` als Dateiendung.
*   `-f`, `--format` (Format): Ausgabeformat: `text` (Standard) oder `sarif` (Optional).
*   `--verbose` (Flag): Aktiviert detaillierte Protokollausgaben (Optional).
*   `--debt-report` (Flag): Tech-Debt-Report (Disable-all nach Ordner, wave-ready Kandidaten); Exit 0 (Optional).
*   `--wave-ready` (Flag): Nur Verstöße in Dateien ohne `// ainetlinter-disable all` (Optional).
*   `--only-changed` (Flag): Nur geänderte Dateien — erfordert `--baseline` (Optional).
*   `--git-since` (Ref): Nur Verstöße in per `git diff` geänderten `.cs`-Dateien seit Ref, z. B. `HEAD~1` (Optional).
*   `--fix` (Flag): Automatische Behebung einfacher Verstöße (z. B. `sealed`, `readonly`, `#nullable enable`) direkt über die CLI (Optional).
*   `-im`, `--impact` (Ref): Semantische Diff-Impact-Analyse ab Git-Referenz (z. B. `HEAD~1` oder leer für uncommitted). Listet alle betroffenen Aufrufstellen (Call-Sites) in der Solution auf (Optional).
*   `-scr`, `--sync-cursor-rules` (Flag): Synchronisiert die `rules.json` Konfiguration als `.cursor/rules/AiNetLinter.mdc` Regeldatei (Optional).
*   `--check` (Flag): Drift-Check ohne Datei-Schreiben (Optional). Kombiniert mit `--sync-cursor-rules`: Prüft `.cursor/rules/AiNetLinter.mdc`. Kombiniert mit `--playbook`: Prüft ob das Playbook aktuell ist. Exit 1 bei Abweichungen, Exit 0 bei Übereinstimmung.
*   `--footprint` (Klassenname): Startet eine Ad-hoc-Analyse der transitiven Zeilen für den angegebenen Klassennamen (inklusive Top-3-Abhängigkeiten) und beendet den Prozess mit Exit 0 (Optional).
*   `--readme` (Flag): Gibt die eingebettete Dokumentation direkt auf stdout aus — ohne `--path`, ohne Dateisystem-Zugriff. Für LLM-Agenten, die Projektkontext abrufen wollen. Exit 0 (Optional).
*   `--no-cache` (Flag): Erzwingt eine vollständige Neu-Analyse aller Dateien (deaktiviert den Analyse-Cache) (Optional).
*   `--cache-ttl` (Minuten): Cache-Lebensdauer in Minuten. Alle Cache-Dateien, die älter als dieser Wert sind, werden beim Programmstart automatisch gelöscht. Standard: `60`. `0` = unbegrenzt (keine Bereinigung). Die Bereinigung läuft unabhängig von `--no-cache` (Optional).

### Wellen-Workflow (Agent-Migration)

Für schrittweise Freischaltung von Legacy-Code (z. B. 5 Dateien pro Welle):

```bash
# Tech-Debt-Übersicht (kein Audit, Exit 0)
ainetlinter --path ./MeinProjekt.slnx --debt-report

# Nur bereits freigeschaltete Dateien mit Verstößen
ainetlinter --config rules.json --path ./MeinProjekt.slnx --wave-ready

# Diese Woche angefasste, freigeschaltete Dateien
ainetlinter --config rules.json --path ./MeinProjekt.slnx --wave-ready --git-since HEAD~7
```

### Inkrementelle Migration (Baseline / Ratchet)

**Use-Case:** Bestehende („alte") Projekte mit hunderten oder tausenden Verstößen schrittweise auf AiNetLinter-Stand bringen — ohne Big-Bang-Refactoring und ohne Git-Integration.

**Workflow:**

1. **Einmalig einfrieren** — alle aktuellen Dateien per Checksumme in der Baseline speichern:
   ```bash
   ainetlinter --path ./MeinProjekt.slnx --create-baseline ainetlinter-baseline.json
   ```
2. **Baseline ins Repository committen** — die Datei `ainetlinter-baseline.json` versionieren.
3. **Regulärer Lauf / CI** — nur Verstöße in geänderten Dateien melden:
   ```bash
   ainetlinter --config rules.json --path ./MeinProjekt.slnx --baseline ainetlinter-baseline.json
   ```
4. **Datei bearbeiten** — Verstöße nur in dieser Datei werden ausgegeben; die Baseline wird automatisch mit den aktuellen Checksummen aktualisiert (weicher Ratchet).

**Semantik:**

| Zustand | Verhalten |
| :--- | :--- |
| Checksumme identisch mit Baseline | Datei unverändert → Verstöße werden **nicht** gemeldet |
| Checksumme abweichend oder Datei neu | Datei wurde angefasst → Verstöße werden **gemeldet** |
| Irgendeine Abweichung erkannt | Gesamte Baseline-Datei wird neu geschrieben |

**Weicher Ratchet:** Nach einem Lauf mit geänderten Dateien werden die neuen Checksummen eingefroren — auch wenn noch Verstöße bestehen. Um weitere Verbesserungen zu erzwingen, die Datei erneut bearbeiten.

**Baseline-Format** (relative Pfade mit Forward-Slashes, Basis: `--path`):

```json
{
  "version": 1,
  "files": {
    "src/MyApp/Program.cs": "a1b2c3d4e5f6..."
  }
}
```

### Roslyn-basierter CLI Auto-Fixer (`--fix`)

Die Option `--fix` behebt einfache Verstöße (wie das Fehlen von `sealed` bei konkreten Klassen, `readonly` bei privaten Feldern oder das Fehlen von `#nullable enable` am Dateianfang) vollautomatisiert über Roslyn-Syntaxbaum-Transformationen direkt beim Audit-Lauf.

### Semantische Diff-Impact-Analyse (`--impact` / `-im`)

Bei Änderungen öffentlicher, interner oder geschützter Methodensignaturen hilft die Impact-Analyse, alle davon betroffenen Aufrufstellen (Call-Sites) in der gesamten Solution zu ermitteln. Sie analysiert dazu das Git-Diff (`git diff -U0`), ordnet geänderte Zeilen den deklarierten Methoden zu und sucht deren Referenzen.

Aufrufbeispiel:
```bash
ainetlinter --path ./MeinProjekt.slnx --impact HEAD~1
```

### Automatisch generiertes Repo-Playbook (`--playbook` / `-pb`)

Das Repo-Playbook scannt die bestehende Codebase und fasst Erkenntnisse wie genutzte Architekturmuster (Result-Pattern vs. throw) und Unterdrückungsstatistiken (deaktivierte Linter-Regeln) zusammen. KI-Agenten können dieses Dokument beim Start laden, um sich an die Gewohnheiten des Repositories anzupassen.

Das Playbook wird über das CLI-Argument `--playbook <Pfad>` oder `-pb <Pfad>` generiert, standardmäßig unter `.cursor/rules/playbook.md`:
```bash
ainetlinter --config rules.json --path ./MeinProjekt.slnx --playbook .cursor/rules/playbook.md
```

### Exit-Codes

*   `0`: Erfolg (Keine Regelverstöße gefunden).
*   `1`: Regelbrüche wurden identifiziert und ausgegeben.
*   `2`: Fataler Fehler (z. B. IO-Exception, MSBuildWorkspace-Ladefehler).

### Ausgabeformate

Alle Dateipfade in der Ausgabe sind **relativ zum `--path`-Argument** (Verzeichnis bzw. übergeordnetes Verzeichnis bei `.sln`/`.slnx`), mit Forward-Slashes.

#### Text (Standard, LLM-optimiert)

Token-effiziente Ausgabe für AI-Agenten. Jeder Text-Lauf gibt zuerst einen `# Run: [Datum und Uhrzeit]` Header aus. Bei Erfolg folgt `OK`. Bei Verstößen: kompakter Header mit Handlungsanweisung, parsebare Summary-Segmente (nach Datei und Regel) und sortierte Detail-Einzeiler.

```
# Run: 2026-06-13 09:06:13
# AiNetLinter · 2 violations
Behebe nur die gelisteten Verstöße. Minimaler Diff — kein Refactoring ausserhalb betroffener Stellen/Zeilen.

## Summary · by file
1 src/AiNetLinter/Core/LinterAnalyzer.cs
1 src/AiNetLinter/Models/RuleViolation.cs

## Summary · by rule
| Rule | Count | Intent |
|------|------:|--------|
| EnforceSealedClasses | 1 | general |
| MaxLineCount | 1 | agent-context |

## Violations
src/AiNetLinter/Core/LinterAnalyzer.cs:77 EnforceSealedClasses | Klasse 'Foo' nicht sealed -> Füge den 'sealed' Modifikator hinzu.
src/AiNetLinter/Models/RuleViolation.cs:6 MaxLineCount | Datei hat 520 Zeilen (max 500) -> Teile die Datei in kleinere Klassen auf.
```

**Summary-Formate:**
- Datei: `{anzahl} {relativerPfad}` — absteigend nach Anzahl
- Regel: Markdown-Tabelle `| Rule | Count | Intent |` — absteigend nach Anzahl

**Detail-Zeilenformat:** `{relativerPfad}:{zeile} {RegelName} | {Details} -> {Guidance}` (Guidance nur wenn vorhanden)

#### SARIF (`--format sarif`)

Strukturiertes JSON für CI/CD-Integration. `artifactLocation.uri` enthält relative Pfade (Basis: `--path`).

---

## 6. Lokale Warnungs-Unterdrückung (Suppression)

Sollte es notwendig sein, bestimmte Regeln für eine Datei oder Zeile zu deaktivieren, kann dies über C#-Kommentare gelöst werden:

```csharp
// ainetlinter-disable all
// Deaktiviert alle AiNetLinter-Regeln für die gesamte Datei.

// ainetlinter-disable MaxLineCount
// Deaktiviert nur die MaxLineCount-Prüfung dateiweit.

public void LegacyMethod(int a, int b, int c, int d, int e) // ainetlinter-disable MaxMethodParameterCount
{
    // Deaktiviert den Parameter-Count-Linter exklusiv für diese Zeile
}

try
{
    int.Parse("not-a-number");
}
catch (Exception) // ainetlinter-disable EnforceNoSilentCatch
{
    // Deaktiviert den Silent-Catch-Linter exklusiv für diese catch-Zeile
}
```

### Gezielter Bulk-Ausschluss (nur betroffene Dateien)

Für Legacy-Codebases, in denen vorerst nur Dateien mit aktuellen Verstößen ausgeschlossen werden sollen:

```bash
ainetlinter --config rules.json --path ./MeinProjekt.slnx --add-disable-all
```

**Ablauf:**
1. Vollständiger Audit-Lauf mit der angegebenen `rules.json`
2. Ermittlung aller Dateien mit mindestens einem Verstoß
3. Einfügen von `// ainetlinter-disable all` am Dateianfang — nur in diesen Dateien
4. Bereits markierte Dateien werden übersprungen

Saubere Dateien bleiben unverändert und werden weiterhin geprüft.

### Bulk-Entfernung des Disable-all-Kommentars

Zum Rückbau nach Refactoring oder wenn der Ausschluss nicht mehr nötig ist:

```bash
ainetlinter --path ./MeinProjekt.slnx --remove-disable-all
```

Es werden ausschließlich Zeilen entfernt, die **exakt** `// ainetlinter-disable all` entsprechen (Zeilenanfang bis Zeilenende, `\r\n` und `\n` werden berücksichtigt). Abweichende Varianten wie eingerückte oder erweiterte Kommentare bleiben unangetastet.

---

## 7. Integration in Unit Tests

Um sicherzustellen, dass AI-Agenten (wie Cursor oder Claude Code) die Linter-Regeln im laufenden Entwicklungsbetrieb eines Repositories nicht verletzen, empfiehlt sich die Integration als Unit-Test.

Hier ist ein C#-Integrationsbeispiel für ein beliebiges anderes Projekt:

```csharp
using Xunit;
using System.Diagnostics;
using System.IO;

public sealed class ArchitectureTests
{
    [Fact]
    public void Enforce_AiNetLinter_Rules_On_Solution()
    {
        // Pfade relativ zu diesem Testprojekt auflösen
        var solutionPath = Path.GetFullPath("../../../MyProject.slnx");
        var configPath = Path.GetFullPath("../../../rules.json");
        var baselinePath = Path.GetFullPath("../../../ainetlinter-baseline.json");
        
        // Pfad zur bereitgestellten AiNetLinter.exe (samt den BuildHost-Ordnern im selben Pfad)
        var linterCliPath = Path.GetFullPath("../../../tools/ainetlinter/AiNetLinter.exe");

        var processInfo = new ProcessStartInfo
        {
            FileName = linterCliPath,
            Arguments = $"--config \"{configPath}\" --path \"{solutionPath}\" --baseline \"{baselinePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);
        
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // Wenn der Linter Verstöße findet, liefert er Exit-Code 1 und der Test schlägt fehl
        Assert.True(process.ExitCode == 0, $"AiNetLinter hat Verstoesse gefunden:\n{output}");
    }
}
```

> [!IMPORTANT]
> **MSBuild-Abhängigkeiten beachten:**
> Für diesen Test müssen im Verzeichnis `tools/ainetlinter/` neben der `AiNetLinter.exe` auch unbedingt die beiden Unterordner `BuildHost-netcore/` und `BuildHost-net472/` liegen, die beim Build/Publish des Tools erzeugt werden. Andernfalls schlägt die Analyse fehl.

---

## 8. Integration durch LLM/Agent

Dieser Abschnitt beschreibt, wie ein autonomer AI-Agent `AiNetLinter` selbständig in seinen Arbeits-Loop integrieren kann.

### Workflow für Agenten

1. **Vor einer Änderung:** Kontext aus generierten Artefakten laden
   ```
   Docs/codegraph.md          — Abhängigkeitsgraph (auto-generiert)
   Docs/playbook.md           — Architektur-Status, Top-Verstöße
   .cursor/rules/AiNetLinter.mdc  — Aktive Regeln und Limits
   ```

2. **Nach einer Änderung:** Linter ausführen
   ```powershell
   AiNetLinter.exe --path . --config rules.json
   ```

3. **Verstöße interpretieren** (anhand `RuleMetadata.intent`):
   - `intent: agent-context` — Komplexitäts-/Größenverstoß → direkt beheben
   - `intent: agent-resilience` — `EnforceNoSilentCatch` → Priorität hoch
   - `intent: test-coverage` — `StaticTestSentinel` → Test hinzufügen oder Exemption prüfen
   - `intent: architecture` — Namespace-/Vererbungsverstoß → nur mit Rücksprache beheben

4. **Suppression bei unvermeidbaren Verstößen:**
   ```csharp
   // ainetlinter-disable EnforceNoSilentCatch
   catch (Exception) { }
   
   catch (Exception ignored) { }  // Alternative: Variable "ignored" benennen
   ```

### Zwei-Stufen-Modell

| Profil | Zweck | Wann aktivieren |
|--------|-------|-----------------|
| `platform-default` | Produktiv — Agenten beheben Verstöße direkt | Regulärer Entwicklungsbetrieb |
| `platform-ai-strict` | Zielrichtung — zeigt was sein sollte | Code-Reviews, Architektur-Audits |

### Cursor-Regeln synchronisieren

Nach jeder `rules.json`-Änderung muss `.cursor/rules/AiNetLinter.mdc` neu generiert werden:
```powershell
AiNetLinter.exe --path . --config rules.json --sync-cursor-rules
```

Drift prüfen (Exit 1 bei Abweichungen, nützlich für CI):
```powershell
AiNetLinter.exe --path . --config rules.json --sync-cursor-rules --check
```

---

## 9. Zukunfts-Roadmap (Ausblick)

*   **Erweiterte semantische Datenflussanalyse:** Statische Überprüfung komplexerer Datenflussketten, um veränderliche Zustandsänderungen über Klassengrenzen hinweg für KIs zu markieren.
*   **Weitere automatische CLI Code-Fixes:** Ausbau des Auto-Fixers zur Behebung komplexerer Strukturverletzungen (z. B. automatisches Auslagern übergroßer Methoden).

---

## 10. Consumer-Setup & Pragmatic Defaults

### Consumer-Setup-Checkliste

Für die produktive Integration von `AiNetLinter` in ein bestehendes Projekt empfiehlt sich folgendes Vorgehen:

1. **Explizite Konfiguration:** Erstelle eine `rules.json` mit **allen** verfügbaren Konfigurations-Keys explizit eingetragen. Dies zwingt Entwickler zur bewussten Aktivierung/Deaktivierung neuer Regeln bei Updates.
2. **Projekt-Overrides für Tests:** Definiere unter `ProjectOverrides` (z. B. für `*.Tests`) pragmatischere Schwellenwerte. So dürfen im Testcode Literale (Magic Values) verwendet werden und das Sealing konkreter Klassen kann deaktiviert werden.
3. **Synchronisation der MDC-Dateien:** Nutze `--sync-cursor-rules` im Pre-Commit- oder CI-Schritt, um die `.cursor/rules/AiNetLinter.mdc` automatisch aktuell zu halten. Workflow-Richtlinien und organisatorische Regeln sollten getrennt in einer separaten, manuell gepflegten Datei wie `.cursor/rules/CodeQualitaet.mdc` verwaltet werden.
4. **Integrationstests statt Blockade:** Binde die Linter-Prüfung in die Unit-Test-Suite ein (siehe Sektion 7). Es empfiehlt sich in der Migrationsphase, den Test bei Verstößen nicht zwingend fehlschlagen zu lassen (Exit 0/1 als Information), sondern den Report als Orientierung für Entwickler zu nutzen.
5. **MSBuild BuildHost-Verzeichnis:** Stelle sicher, dass bei der Distribution des Linters im CI-Build/Publish-Prozess die Verzeichnisse `BuildHost-netcore/` und `BuildHost-net472/` stets direkt neben der ausführbaren `AiNetLinter.exe` liegen.

### Pragmatic Agent Defaults

Bei größeren Migrations-Szenarien sollten viele Regeln schrittweise eingeführt werden. Hier ist die empfohlene Konfigurationsebene ("Pragmatic Agent Defaults"):

| Regel | Pragmatic | Strict | Begründung / Kontext |
| :--- | :--- | :--- | :--- |
| `DetectAndBanPhantomDependencies` | **on** | **on** | Verhindert, dass KIs nicht-existente Typen/Namespaces oder dynamische Reflektion erzeugen. |
| `RequireExplicitTruncationHandling` | **on** | **on** | Schützt vor Endlosschleifen beim I/O-Lesen. |
| `MaxAIContextFootprint` | **5000** | **4000** | Schont das RAG-Kontextbudget der LLM-Modelle. |
| `AllowUnsealedPartialClasses` | **on** | **on** | Erforderlich für UI-Frameworks wie Blazor (Komponenten-Klassen). |
| `EnforceExplicitStateImmutability` | **off** | **on** | Sollte bei Legacy-Projekten zunächst deaktiviert bleiben und erst bei refaktorierter Immutability aktiviert werden. |
| `EnforceNamespaceDirectoryMapping` | **off** | **on** | Bei Feature-Foldern oder älteren Namespace-Strukturen deaktivieren. |
| `EnforceResultPatternOverExceptions` | **off** | **on** | Deaktivieren, falls im Altsystem noch weitreichend Exceptions geworfen werden (z. B. zur Validierung). |
| `MaxCyclomaticComplexity` | **8** | **5** | Ein pragmatischerer Wert (8) verhindert übermäßiges Aufsplittern bei komplexen Altrechner-Methoden. |

---

## 11. Performance-Profiling & Zeitmessung

Um Performance-Flaschenhälse in großen C#-Solutions gezielt zu analysieren, besitzt `AiNetLinter` ein integriertes Profiling-System.

### Funktionsweise

Wenn das Profiling aktiv ist, misst der Linter automatisch die Ausführungszeit der verschiedenen Verarbeitungsphasen und schreibt detaillierte Reports in den `measurements/`-Ordner direkt neben der ausführbaren Datei:

```
[Ausführungsverzeichnis]/measurements/[ProjektName]/[yyyy-MM-dd]/[ProjektName]-[Zeitstempel]-[UUID]/
  ├── performance.log   <-- Gut lesbarer Textbericht mit Phasenanalyse und den Top-20 langsamsten Dateien
  └── performance.json  <-- Strukturierte JSON-Datei für automatische Auswertungen
```

### Konfiguration

Das Feature ist standardmäßig aktiviert und kann über die Konfigurationsdatei `rules.json` deaktiviert werden:

```json
"Global": {
  "EnablePerformanceProfiling": false
}
```

---

## 12. Analyse-Cache (Inkrementelle Laufzeitoptimierung)

Um die Latenz im agentischen Entwicklungszyklus ("Agentic Feedback Loop") zu minimieren, besitzt `AiNetLinter` einen intelligenten, inkrementellen Analyse-Cache.

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

Der 8-stellige Datei-Hash (`hash8`) basiert auf dem normalisierten absoluten Pfad der Solution-Datei und dem exakten Inhalt der verwendeten Konfigurationsdatei (`rules.json`). 

### Cache-Invalidierung

Die Cache-Validierung erfolgt vollautomatisch:
- **Konfigurationsänderungen:** Eine Anpassung der Linter-Regeln in der `rules.json` ändert den Datei-Hash im Cache-Dateinamen. Es wird automatisch eine neue Cache-Datei erzeugt.
- **Dateiveränderungen:** Geänderte Dateien besitzen einen neuen Inhalts-Hash und werden automatisch neu analysiert; ihr Cache-Eintrag wird aktualisiert.
- **Tool-Updates:** Bei Schema-Änderungen des Linters wird der Cache über eine interne `SchemaVersion` automatisch vollständig invalidiert.

### TTL-basierte Bereinigung (`--cache-ttl`)

Beim Start jedes Analyse-Runs bereinigt `AiNetLinter` automatisch alle Cache-Dateien im `cache/`-Verzeichnis, deren letzte Schreibzeit (`LastWriteTimeUtc`) älter als der konfigurierte Schwellenwert ist. Die Bereinigung ist global — sie erfasst Leichen aus allen bisherigen Solutions und Rules-Kombinationen.

```powershell
# Standardlauf: Cache-Dateien älter als 60 Minuten werden gelöscht
AiNetLinter.exe --config rules.json --path .

# Längere Lebensdauer für CI/CD oder manuelle Nutzung
AiNetLinter.exe --config rules.json --path . --cache-ttl 240

# Kein automatisches Löschen
AiNetLinter.exe --config rules.json --path . --cache-ttl 0
```

| `--cache-ttl` | Verhalten |
| :--- | :--- |
| `60` (Standard) | Cache-Dateien > 60 Min alt werden beim Start gelöscht |
| `0` | Keine Bereinigung — Cache lebt unbegrenzt |
| `> 0` | Bereinigung nach dem angegebenen Minutenwert |

**Warum `LastWriteTimeUtc` statt Filename-Timestamp?** Der Filename-Timestamp kodiert *wann der Linter gebaut wurde*. `SaveIfDirty()` setzt `LastWriteTimeUtc` auf "jetzt" — das ist die korrekte Uhr für "wie frisch sind die Analyseergebnisse".

### Deaktivierung über CLI

Der Cache ist standardmäßig **aktiviert**. Wenn eine vollständige Neu-Analyse aller Dateien erzwungen werden soll:

```powershell
AiNetLinter.exe --path . --config rules.json --no-cache
```

### Kombinierter Lauf (Single Analysis)

Um den Ressourcenverbrauch bei paralleler Generierung optionaler Ausgaben zu minimieren, verschmilzt `AiNetLinter` die Ausführung von:
- **Lint-Lauf** (`--config rules.json --path ...`)
- **Playbook-Generierung** (`--playbook ...`)
- **Graph-Generierung** (`--graph ...`)

Wenn diese Optionen kombiniert werden, wird die semantische Roslyn-Analyse aller Dokumente **genau einmal** ausgeführt. Die berechneten Regelverstöße werden direkt an den Playbook-Generator weitergegeben, anstatt eine zweite vollständige Analyse anzustoßen. Dies führt bei kombinierten Aufrufen zu einer Halbierung der Gesamtlaufzeit.
