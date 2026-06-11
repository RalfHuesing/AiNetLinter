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

*   **Roslyn-basierte semantische Analyse:** Evaluierung der gesamten Solution (.sln / .slnx) über einen einzigen Syntax-Walk pro Dokument. Nutzt echte Semantik-Informationen statt textbasierter Heuristiken.
*   **Feingranulares Regelwerk:** Umfassende Regeln für Klassendesign (Sealed, Value Objects, Vererbungstiefe), Variablen/Typen (kein `dynamic`, keine `out`-Parameter, Nullable Context) und Code-Komplexität (McCabe, SonarSource).
*   **PascalCase- & Namensvalidierung:** Typprüfung auf PascalCase-Konventionen sowie Erkennung nicht-semantischer Bezeichner (z. B. `data`, `temp`, `obj`).
*   **LSP-Dokumentationstests:** Erzwingt die Verwendung von XML-Docs (`/// <summary>`) auf öffentlichen APIs.
*   **Static Test Sentinel:** Statische Test-Präsenzprüfung für komplexe Quellcodeabschnitte anhand von Metadaten-Scans auf referenzierte Testbibliotheken (xunit, nunit etc.).
*   **Namespace-Abhängigkeitsprüfung (Vertical Slices):** Verhindert unerlaubte slice-übergreifende Abhängigkeiten, auch bei vollqualifizierten Typnamen.
*   **Warnungs-Unterdrückung (Suppression):** Flexibles Deaktivieren von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]` oder dateiweit.
*   **SARIF- & Dependency-Graph-Export:** Generierung strukturierter SARIF-Fehlerberichte für CI/CD sowie automatisches Zeichnen von Mermaid-Abhängigkeitsdiagrammen.

---

## 4. Konfiguration (`rules.json`)

Die Konfiguration erfolgt über eine flache, leicht verständliche JSON-Struktur. Beispiel einer vollständigen Konfiguration:

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowDynamic": false,
    "AllowOutParameters": false,
    "EnforceValueObjectContracts": true,
    "EnableTestSentinel": true,
    "EnforcePascalCase": true,
    "EnforceXmlDocumentation": true,
    "EnforceSemanticNaming": true,
    "EnforceNullableEnable": true,
    "EnforceNoSilentCatch": true
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5,
    "MaxInheritanceDepth": 2,
    "MinCognitiveComplexityForTest": 3
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
| `AllowDynamic` | Global | Verbietet das Typschlüsselwort `dynamic` (verhindert statische Analyse-Lücken). |
| `AllowOutParameters` | Global | Verbietet `out`-Parameter zugunsten von C#-Tuples oder Records. |
| `EnforceValueObjectContracts` | Global | Zwingt Klassen mit Suffix `ValueObject` dazu, als `record` oder `readonly struct` deklariert zu sein und nur unveränderliche Eigenschaften (ohne `set`) zu haben. |
| `EnableTestSentinel` | Global | Aktiviert den Test-Präsenzwächter für komplexe Quellcodedateien. |
| `EnforcePascalCase` | Global | Validiert PascalCase-Schreibweise für Klassen, Structs, Records, Interfaces, Methoden und Properties. |
| `EnforceXmlDocumentation` | Global | Erzwingt XML-Dokumentationskommentare an öffentlichen Schnittstellen für LSP-Integrationen. |
| `EnforceSemanticNaming` | Global | Markiert generische Parameternamen (z. B. `data`, `temp`, `val`) in öffentlichen Methoden als Fehler. |
| `EnforceNullableEnable` | Global | Stellt sicher, dass `#nullable enable` in jeder Datei deklariert ist oder global über csproj erzwungen wird. |
| `EnforceNoSilentCatch` | Global | Verbietet leere `catch`-Blöcke oder solche, die Fehler verschlucken ohne re-throw oder Logging. |
| `MaxLineCount` | Metrics | Maximale Zeilenanzahl pro Datei (Standard: 500), um "Lost in the Middle"-Effekte zu verhindern. |
| `MaxMethodParameterCount`| Metrics | Maximale Parameteranzahl pro Methode (Standard: 4). |
| `MaxCyclomaticComplexity`| Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode (Standard: 5). |
| `MaxCognitiveComplexity` | Metrics | Maximale kognitive Komplexität (SonarSource) pro Methode (Standard: 5). |
| `MaxInheritanceDepth` | Metrics | Maximale Tiefe der Vererbungshierarchie (Standard: 2). |
| `MinCognitiveComplexityForTest` | Metrics | Schwellenwert der kognitiven Komplexität, ab dem der Test Sentinel eine zugehörige Testklasse einfordert. |

---

## 5. CLI-Schnittstelle

`AiNetLinter` wird als Windows .NET 10 Core CLI-Tool ausgeführt.

### Aufruf-Syntax

```bash
ainetlinter --config <Pfad-zur-rules.json> --path <Pfad-zur-slnx-oder-Verzeichnis> [Optionen]
```

### Parameter

*   `-c`, `--config` (Pfad): Der Pfad zur `rules.json` (Erforderlich).
*   `-p`, `--path` (Pfad): Der Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (Erforderlich).
*   `-g`, `--graph` (Pfad): Pfad für das zu generierende Mermaid-Abhängigkeitsdiagramm `.md` (Optional).
*   `-f`, `--format` (Format): Ausgabeformat: `text` (Standard) oder `sarif` (Optional).
*   `-v`, `--verbose` (Flag): Aktiviert detaillierte Protokollausgaben (Optional).

### Exit-Codes

*   `0`: Erfolg (Keine Regelverstöße gefunden).
*   `1`: Regelbrüche wurden identifiziert und ausgegeben.
*   `2`: Fataler Fehler (z. B. IO-Exception, MSBuildWorkspace-Ladefehler).

### Ausgabeformate

Alle Dateipfade in der Ausgabe sind **relativ zum `--path`-Argument** (Verzeichnis bzw. übergeordnetes Verzeichnis bei `.sln`/`.slnx`), mit Forward-Slashes.

#### Text (Standard, LLM-optimiert)

Token-effiziente Ausgabe für AI-Agenten. Bei Erfolg: `OK`. Bei Verstößen: kompakter Header mit Handlungsanweisung, gefolgt von sortierten Einzeilern.

```
# AiNetLinter · 2 violations
Behebe nur die gelisteten Verstöße. Minimaler Diff — kein Refactoring ausserhalb betroffener Stellen/Zeilen.

src/AiNetLinter/Core/LinterAnalyzer.cs:77 EnforceSealedClasses | Klasse 'Foo' nicht sealed
src/AiNetLinter/Models/RuleViolation.cs:6 MaxLineCount | Datei hat 520 Zeilen (max 500)
```

Zeilenformat: `{relativerPfad}:{zeile} {RegelName} | {Details}`

#### SARIF (`--format sarif`)

Strukturiertes JSON für CI/CD-Integration. `artifactLocation.uri` enthält relative Pfade (Basis: `--path`).

---

## 6. Lokale Warnungs-Unterdrückung (Suppression)

Sollte es notwendig sein, bestimmte Regeln für eine Datei oder Zeile zu deaktivieren, kann dies über C#-Kommentare gelöst werden:

```csharp
// ainetlinter-disable MaxLineCount
// Dieser Kommentar oben in der Datei deaktiviert die MaxLineCount Prüfung für die gesamte Datei.

public void LegacyMethod(int a, int b, int c, int d, int e) // ainetlinter-disable MaxMethodParameterCount
{
    // Deaktiviert den Parameter-Count-Linter exklusiv für diese Zeile
}
```

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
        var linterCliPath = Path.GetFullPath("../../../../src/AiNetLinter/bin/Debug/net10.0/AiNetLinter.exe");

        var processInfo = new ProcessStartInfo
        {
            FileName = linterCliPath,
            Arguments = $"--config \"{configPath}\" --path \"{solutionPath}\"",
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