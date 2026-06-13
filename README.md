# AiNetLinter - AI-Optimierte .NET-Code-Validierung & Linter

`AiNetLinter` ist ein hochperformantes .NET 10 CLI-Linter-Tool, das speziell dafür entwickelt wurde, C#-Codebases für die Bearbeitung durch autonome AI-Agenten (wie Cursor, Claude Code, GitHub Copilot) zu optimieren und gleichzeitig die kognitive Last für menschliche Entwickler zu minimieren. 

Indem es Code-Metriken und Strukturvorgaben über Roslyn-Syntaxanalysen prüft, stellt das Tool sicher, dass der Code für Sprachmodelle (LLMs) maximal verständlich bleibt und Fehler im agentischen Entwicklungszyklus ("Agentic Loop") automatisiert korrigiert werden können.

---

## 1. Vision & Leitbild: Der "AI-Readability-Index"

Wenn KI-Agenten Code nicht mehr nur vervollständigen, sondern ihn autonom editieren, refaktorieren und erweitern, verschiebt sich das wichtigste Qualitätsmerkmal von Software: **Der Code muss so designt sein, dass eine KI ihn fehlerfrei erfassen und manipulieren kann.**

`AiNetLinter` setzt genau hier an und erzwingt einen modernen, AI-optimierten C#-Programmierstil, der auf wissenschaftlichen Erkenntnissen der LLM-Forschung und der Praxis agentischer Tools basiert.

### Die wissenschaftlichen Grundlagen der AI-Readability

#### 1. Begrenzung der Dateigröße (Max. 500 Zeilen)
*   **Wissenschaftlicher Hintergrund:** Die Forschung zum Phänomen **"Lost in the Middle"** (Liu et al.) zeigt, dass LLMs Informationen am Anfang und am Ende ihres Kontextfensters hervorragend verarbeiten, in der Mitte jedoch signifikant an Aufmerksamkeit verlieren.
*   **Konsequenz für den Code:** Lange Dateien erhöhen das Risiko, dass ein AI-Agent beim Editieren Codebereiche überspringt, bestehende Logik "vergisst" oder fehlerhafte Zeilen generiert. `AiNetLinter` deckelt die Dateigröße standardmäßig auf 500 Zeilen.

#### 2. Radikale Reduzierung der Komplexität
*   **Wissenschaftlicher Hintergrund:** LLMs generieren Code linear (Token für Token). Verschachtelte `if-else`-Kaskaden, tiefe Schleifen und komplexe Verzweigungen zwingen die Attention Heads des Modells, komplexe Zustandsmatrizen im Arbeitsspeicher mitzuführen. Dies führt nachweislich zu Logikfehlern und Halluzinationen.
*   **Konsequenz für den Code:** `AiNetLinter` prüft sowohl die klassische **zyklomatische Komplexität** als auch die **kognitive Komplexität** (SonarSource-Standard). Flacher Code mit Early Returns ist für KIs um Welten einfacher fehlerfrei zu erweitern.
*   **Methodenlänge (Max. 42 Codezeilen):** Lange, aber flache Methoden ohne Verzweigungen entgehen der kognitiven Komplexitätsprüfung, belasten aber trotzdem das Kontextfenster und erschweren präzise Diffs. Daher begrenzt `MaxMethodLineCount` die reine Codezeilenanzahl pro Methode (ohne Kommentare und Leerzeilen).

#### 3. Der "Human-Naturalness Channel" & Semantische Namen
*   **Wissenschaftlicher Hintergrund:** Studien zur KI-Code-Kognition (z. B. *"When Names Disappear: Revealing What LLMs Actually Understand About Code"*, 2025) belegen, dass LLMs Code über zwei Kanäle verstehen: den *strukturellen Kanal* (Syntax, Typen) und den *linguistischen Kanal* (Bezeichnernamen, Kommentare). Fehlen sprechende Namen (z. B. durch Obfuskation oder generische Bezeichner wie `Check1()`), bricht die KI-Performance um bis zu 30 % ein.
*   **Das Risiko von "Over-Reliance":** LLMs neigen dazu, Namen übermäßig zu vertrauen. Weicht der Methodenname von der tatsächlichen Logik ab (z. B. durch nachträgliche Anforderungsänderungen), interpretieren LLMs den Code fast immer falsch.
*   **Konsequenz für den Code:**
    *   Fokus auf die Absicht (Intent) statt auf flüchtige Bedingungen: `ValidateGermanPremiumInvoice` statt `ValidateInvoiceWhenCustomerIsPremiumAndCountryIsGermany`.
    *   Verwendung des C#-Standards (PascalCase), da Tokenizer (Byte-Pair-Encoding) diese perfekt in semantische Token wie `Validate`, `Premium`, `Invoice` zerlegen können.
    *   Auslagerung von Detailbeschreibungen in XML-Doc-Comments (`///`), welche von AI-Agenten über LSP (Language Server Protocol) gelesen werden, statt den Methodennamen unleserlich lang zu machen.

#### 4. Lokalität vor Schichtenarchitektur (Vertical Slices)
*   Der größte Feind eines AI-Agenten ist das "Herumspringen" im Repository (hohe Streuung). Wenn für ein Feature Anpassungen in Projekten für DTOs, Business-Logik, EF-Mapping und Controller getätigt werden müssen, schießt die Fehlerquote hoch. 
*   **Konsequenz:** Die Architektur sollte auf **Vertical Slices** setzen. Ein Feature gehört zusammenhängend in einen Ordner oder im besten Fall in eine Datei, damit das Problem vollständig in ein einziges Prompt-Kontextfenster passt.

#### 5. Compiler-gestützte Leitplanken (.NET 10 Features)
*   Agenten arbeiten iterativ: Code schreiben $\rightarrow$ Compiler ausführen $\rightarrow$ Fehler korrigieren.
*   `AiNetLinter` setzt darauf, dass der Compiler selbst zur Leitplanke wird:
    *   `#nullable enable` ist Pflicht (erzwingt Null-Checks).
    *   `required` Properties in Records (verhindert unvollständiges Instanziieren).
    *   Exhaustive Pattern Matching (Compiler wirft Fehler, wenn z. B. ein neues Enum-Mitglied im `switch` vergessen wurde).

---

## 2. Der "AI-Mittelweg" für DRY vs. WET

Die klassische Regel **DRY** (Don't Repeat Yourself) führt bei extremem Einsatz zu tiefen, generischen Abstraktionen, die für KIs schwer verständlich sind und den gefürchteten "Schmetterlingseffekt" (Änderung an einer Stelle bricht unbemerkt 10 andere Stellen) begünstigen. `AiNetLinter` unterstützt einen pragmatischen Mittelweg:

1.  **Fachliches DRY (Strikt):** Kern-Geschäftslogik und Berechnungen müssen zentral und wiederverwendbar sein (z. B. in Domain-Modellen oder Services). Die KI muss diese Logik nur an einem einzigen Ort ändern.
2.  **Technisches WET (Erlaubt):** Controller, DTOs, Mapper und Queries dürfen redundant bzw. spezifisch pro Use Case (Vertical Slice) aufgebaut sein. Dies minimiert Seiteneffekte und verhindert, dass die KI riesige, geteilte Basisklassen anpassen muss und dabei andere Features beschädigt.

---

## 3. Kernfeatures von AiNetLinter

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

---

## 4. Konfiguration (`rules.json`)

Die Konfiguration erfolgt über eine flache, leicht verständliche JSON-Struktur. Beispiel einer vollständigen Konfiguration:

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": false,
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
    "EnforceMinimalApiAsParameters": false
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxMethodLineCount": 42,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5,
    "MaxInheritanceDepth": 2,
    "MinCognitiveComplexityForTest": 3,
    "AggregatePartialClassLineCount": false
  },
  "TestSentinel": {
    "ClassNamePatterns": ["{Name}Tests", "{Name}Test", "{Name}IntegrationTests", "{Name}*Tests"],
    "RecognizeTypeofReference": true,
    "RecognizeCoversComment": true
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
| `AllowUnsealedPartialClasses` | Global | Erlaubt es, `partial` Klassen unsealed zu lassen (Standard: `false`, nützlich z. B. bei Blazor Page-Components). |
| `AllowDynamic` | Global | Verbietet das Typschlüsselwort `dynamic` (verhindert statische Analyse-Lücken). |
| `AllowOutParameters` | Global | Verbietet `out`-Parameter zugunsten von C#-Tuples oder Records. |
| `AllowTryPatternOutParameters` | Global | Erlaubt `out` in `bool Try*`-Methoden (Standard: `true`, idiomatisches C#). |
| `AllowCancellationShutdownCatch` | Global | Erlaubt leere `catch (OperationCanceledException) when (...)` bei Host-Shutdown. |
| `EnforceMinimalApiAsParameters` | Global | Prüft Minimal-API-Endpunkte auf fehlendes `[AsParameters]` bei >4 Parametern (opt-in). |
| `EnforceValueObjectContracts` | Global | Zwingt Klassen mit Suffix `ValueObject` dazu, als `record` oder `readonly struct` deklariert zu sein und nur unveränderliche Eigenschaften (ohne `set`) zu haben. |
| `EnableTestSentinel` | Global | Aktiviert den Test-Präsenzwächter für komplexe Quellcodedateien. |
| `EnforcePascalCase` | Global | Validiert PascalCase-Schreibweise für Klassen, Structs, Records, Interfaces, Methoden und Properties. |
| `EnforceXmlDocumentation` | Global | Erzwingt XML-Dokumentationskommentare an öffentlichen Schnittstellen für LSP-Integrationen. |
| `EnforceSemanticNaming` | Global | Markiert generische Parameternamen (z. B. `data`, `temp`, `val`) in öffentlichen Methoden als Fehler. |
| `EnforceNullableEnable` | Global | Stellt sicher, dass `#nullable enable` in jeder Datei deklariert ist oder global über csproj erzwungen wird. |
| `EnforceNoSilentCatch` | Global | Verbietet leere `catch`-Blöcke oder solche, die Fehler verschlucken ohne re-throw oder Logging. Variable Namen, die mit `ignored` oder `expected` beginnen (z. B. `catch (Exception ignored)`), werden ignoriert. |
| `MaxLineCount` | Metrics | Maximale Zeilenanzahl pro Datei (Standard: 500), um "Lost in the Middle"-Effekte zu verhindern. |
| `MaxMethodParameterCount`| Metrics | Maximale Parameteranzahl pro Methode (Standard: 4). |
| `MaxMethodLineCount` | Metrics | Maximale Codezeilenanzahl pro Methode ohne Kommentare/Leerzeilen (Standard: 42). |
| `MaxCyclomaticComplexity`| Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode (Standard: 5). |
| `MaxCognitiveComplexity` | Metrics | Maximale kognitive Komplexität (SonarSource) pro Methode (Standard: 5). |
| `MaxInheritanceDepth` | Metrics | Maximale Tiefe der Vererbungshierarchie (Standard: 2). |
| `MinCognitiveComplexityForTest` | Metrics | Schwellenwert der kognitiven Komplexität, ab dem der Test Sentinel eine zugehörige Testklasse einfordert. |
| `AggregatePartialClassLineCount` | Metrics | Summiert Zeilenanzahl über alle `partial`-Teile eines Typs (opt-in). |
| `TestSentinel` | Config | Flexible Testabdeckung: Klassenname-Patterns, `typeof`-Referenz, `// @covers`-Kommentar. |
| `RuleMetadata` | Config | Severity (`error`/`warning`) und Intent-Tags pro Regel für LLM-Priorisierung. |

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
*   `-f`, `--format` (Format): Ausgabeformat: `text` (Standard) oder `sarif` (Optional).
*   `-v`, `--verbose` (Flag): Aktiviert detaillierte Protokollausgaben (Optional).
*   `--debt-report` (Flag): Tech-Debt-Report (Disable-all nach Ordner, wave-ready Kandidaten); Exit 0 (Optional).
*   `--wave-ready` (Flag): Nur Verstöße in Dateien ohne `// ainetlinter-disable all` (Optional).
*   `--only-changed` (Flag): Nur geänderte Dateien — erfordert `--baseline` (Optional).
*   `--git-since` (Ref): Nur Verstöße in per `git diff` geänderten `.cs`-Dateien seit Ref, z. B. `HEAD~1` (Optional).

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

**Use-Case:** Bestehende („alte“) Projekte mit hunderten oder tausenden Verstößen schrittweise auf AiNetLinter-Stand bringen — ohne Big-Bang-Refactoring und ohne Git-Integration.

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
src/AiNetLinter/Core/LinterAnalyzer.cs:77 EnforceSealedClasses | Klasse 'Foo' nicht sealed → Füge den 'sealed' Modifikator hinzu.
src/AiNetLinter/Models/RuleViolation.cs:6 MaxLineCount | Datei hat 520 Zeilen (max 500) → Teile die Datei in kleinere Klassen auf.
```

**Summary-Formate:**
- Datei: `{anzahl} {relativerPfad}` — absteigend nach Anzahl
- Regel: Markdown-Tabelle `| Rule | Count | Intent |` — absteigend nach Anzahl

**Detail-Zeilenformat:** `{relativerPfad}:{zeile} {RegelName} | {Details} → {Guidance}` (Guidance nur wenn vorhanden)

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

Um sicherzustellen, dass AI-Agenten die Regeln während der Arbeit einhalten, kann `AiNetLinter` über ein Test-Projekt integriert werden.

```csharp
using Xunit;
using System.Diagnostics;
using System.IO;

public sealed class ArchitectureTests
{
    [Fact]
    public void Enforce_AiNetLinter_Rules_On_Solution()
    {
        var solutionPath = Path.GetFullPath("../../../../AiNetLinter.slnx");
        var configPath = Path.GetFullPath("../../../../rules.json");
        var baselinePath = Path.GetFullPath("../../../../ainetlinter-baseline.json");
        var linterCliPath = Path.GetFullPath("../../../../src/AiNetLinter/bin/Debug/net10.0/AiNetLinter.exe");

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

        Assert.True(process.ExitCode == 0, $"AiNetLinter hat Verstoesse gefunden:\n{output}");
    }
}
```

---

## 8. Zukunfts-Roadmap (Ausblick)

*   **Scope-Verwirrungs-Linter (Context Ambiguity):** Erkennung und Verbot von Variable Shadowing (lokale Variablen verbergen Klassenfelder) und übermäßigem Method Overloading (max. 2), da LLMs hierdurch oft falschen Kontext interpretieren.
*   **Deterministische Zustandsprüfung:** Verbot von Parameter-Reassignment (Parameter in Methoden überschreiben) und Erzwingen von `readonly` Feldern auf Klassenebene.
*   **Topologische Kopplungsprüfung:** Begrenzung von Fan-Out / Fan-In auf Konstruktor-Ebene (z. B. `MaxConstructorDependencies = 5`), um übermäßigen Kontextaufwand für KIs zu verringern.
*   **Vermeidung von Magic Values:** Warnungen bei unbenannten Literalen (Magic Numbers / Magic Strings) in Methoden zur Erhöhung des semantischen Namensraums.
*   **Result-Pattern über Exception Control Flow:** Flaggen von fachlichen `throw` Ausdrücken außerhalb von Konstruktoren zur Forcierung des Result-Patterns.
*   **Optimiertes Speicher-Management:** Sequentielles Laden und Entladen von Projekten im `MSBuildWorkspace` für sehr große Monolithen.