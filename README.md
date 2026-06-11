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

*   **Roslyn-basierte Blitz-Analyse:** Das Tool nutzt bewusst **nicht** den schweren `MSBuildWorkspace` (der ein vollständiges Kompilieren erfordert), sondern parst C#-Dateien direkt über `CSharpSyntaxTree.ParseText` und analysiert sie mit einem optimierten `CSharpSyntaxWalker`. Das erlaubt Analysen von hunderten Quelldateien in wenigen Millisekunden.
*   **JSON-Konfiguration:** Einfache, declarative Steuerung aller Regeln über eine zentrale `rules.json`.
*   **Actionable AI-Feedback auf stdout:** Fehlermeldungen sind so formuliert, dass sie einem AI-Agenten im Terminal eine präzise, direkt ausführbare Arbeitsanweisung geben.
*   **Unit-Test-Integration:** Der Linter kann direkt als Assert-Schritt in klassischen xUnit/NUnit-Tests ausgeführt werden, um die Einhaltung der Regeln im lokalen Entwicklungs-Loop zu erzwingen.

---

## 4. Konfiguration (`rules.json`)

Die Konfiguration erfolgt über eine flache, leicht verständliche JSON-Struktur:

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowDynamic": false,
    "AllowOutParameters": false
  },
  "Metrics": {
    "MaxLineCount": 500,
    "MaxMethodParameterCount": 4,
    "MaxCyclomaticComplexity": 5,
    "MaxCognitiveComplexity": 5
  }
}
```

### Erklärung der Regeln

| Regel | Bereich | Beschreibung |
| :--- | :--- | :--- |
| `EnforceSealedClasses` | Global | Zwingt alle konkreten Klassen (die nicht `abstract` oder `static` sind) dazu, als `sealed` deklariert zu werden. Reduziert Vererbungsketten und optimiert die .NET-Runtime (Devirtualisierung). |
| `AllowDynamic` | Global | Verbietet das Schlüsselwort `dynamic`. Löscht Typsicherheit und verhindert, dass LLMs die verfügbaren Member statisch analysieren können. |
| `AllowOutParameters` | Global | Verbietet `out`-Parameter. Erzwingt stattdessen C#-Tuples oder dedizierte Records für mehrere Rückgabewerte, was für KIs intuitiver lesbar ist. |
| `MaxLineCount` | Metrics | Maximale Zeilenanzahl pro `.cs`-Datei (Standard: 500), um "Lost in the Middle"-Effekte zu verhindern. |
| `MaxMethodParameterCount`| Metrics | Maximale Anzahl an Parametern pro Methode (Standard: 4). Erzwingt bei Überschreitung das Kapseln in Parameter-Objects (`record`). |
| `MaxCyclomaticComplexity`| Metrics | Maximale zyklomatische Komplexität (McCabe) pro Methode. |
| `MaxCognitiveComplexity` | Metrics | Maximale kognitive Komplexität (SonarSource-Standard) pro Methode, um verschachtelte Kontrollstrukturen zu unterbinden. |

---

## 5. CLI-Schnittstelle

`AiNetLinter` wird als Windows .NET 10 Core CLI-Tool ausgeführt.

### Aufruf-Syntax

```bash
ainetlinter --config <Pfad-zur-Config-JSON> --path <Pfad-zur-slnx-oder-Verzeichnis>
```

### Parameter

*   `-c`, `--config` (Pfad): Der Pfad zur `rules.json`.
*   `-p`, `--path` (Pfad): Der Pfad zur modernen `.slnx`-Projektmappendatei, einer `.csproj` oder direkt zu einem Quellcode-Verzeichnis.
*   `-v`, `--verbose` (Flag): Aktiviert detaillierte Logging-Ausgaben für Debugging-Zwecke.

### CLI-Ausgabe (Beispiel für AI-Agenten)

Tritt ein Regelverstoß auf, gibt das Tool strukturierte Fehlermeldungen auf `stdout` aus und beendet sich mit einem Exit-Code ungleich 0 (`1`).

```plaintext
[ARCH-ERROR]: Die Methode 'CalculateDiscount' in 'C:\Entwicklung\Project\Services\InvoiceService.cs' auf Zeile 45 bricht die AI-Readability-Regeln.
- Erwartete Kognitive Komplexität: max. 5
- Tatsächliche Kognitive Komplexität: 8
Bitte refaktoriere die Methode, indem du verschachtelte Logik in kleine, pure Hilfsmethoden oder Pattern Matchings auslagerst.

[ARCH-ERROR]: Die Klasse 'CustomerRepository' in 'C:\Entwicklung\Project\Repositories\CustomerRepository.cs' auf Zeile 12 ist nicht als 'sealed' deklariert.
Bitte füge den Modifikator 'sealed' zur Klassendeklaration hinzu, um die Vererbungshierarchie flach zu halten.
```

---

## 6. Integration in Unit Tests

Um sicherzustellen, dass AI-Agenten (und menschliche Entwickler) die Regeln während der Arbeit einhalten, kann `AiNetLinter` über ein Test-Projekt integriert werden. Dadurch wird ein Regelbruch sofort im lokalen Test-Runner (z. B. via `dotnet test`) gemeldet.

### Beispiel für einen xUnit-Test in C#

```csharp
using Xunit;
using System.Diagnostics;
using System.IO;

public sealed class ArchitectureTests
{
    [Fact]
    public void Enforce_AiNetLinter_Rules_On_Solution()
    {
        // Pfad zur Solution und Config ermitteln
        var solutionPath = Path.GetFullPath("../../../../MyProject.slnx");
        var configPath = Path.GetFullPath("../../../../ainetlinter-rules.json");
        var linterCliPath = Path.GetFullPath("../../../../tools/ainetlinter.exe");

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

        // Wenn der Linter Fehler findet (ExitCode != 0), schlägt der Test fehl
        // und gibt die genauen Handlungsanweisungen für den AI-Agenten aus.
        Assert.True(process.ExitCode == 0, $"AiNetLinter hat Verstöße gefunden:\n{output}");
    }
}
```

---

## 7. Zukunfts-Roadmap (Ausblick)

In zukünftigen Versionen soll `AiNetLinter` um folgende Konzepte erweitert werden:
*   **Architekturschnitte validieren (Namespace-Kopplung):** Einbindung von Regeln ähnlich wie ArchUnitNET, um unzulässige Abhängigkeiten zwischen Vertical Slices (z. B. `MyApp.Features.Invoicing` darf nicht direkt von `MyApp.Features.Customer` abhängen) direkt auf Syntax-Ebene zu verbieten.
*   **Maschinenlesbare Verträge (Contracts):** Unterstützung strukturierter Typ-Verträge (z. B. automatische Validierung von feingranularen Value Objects wie `PositiveInteger`), um manuelle Werteprüfungen überflüssig zu machen.
*   **Traceability-Graphen:** Automatische Analyse von Abhängigkeiten, um AI-Agenten vorab mitzuteilen, welche Teile des Systems von einer geplanten Änderung betroffen sein werden.
*   **Statische Test-Präsenzprüfung & Relevanz-Analyse (Static Test Sentinel):** Statische Ermittlung der Kritikalität von Klassen und Methoden auf Basis von Komplexität und Kopplung (wie viele andere Klassen hängen von dieser ab?). Der Linter prüft rein statisch (z. B. über Namenskonventionen wie `[ClassName]Tests.cs` oder Referenz-Suchen im Testprojekt), ob für diesen relevanten Code Test-Deklarationen existieren, und schlägt bei ungetesteten, aber hochrelevanten Codebereichen Alarm. Die inhaltliche Sinnhaftigkeit der Tests kann zwar statisch nicht verifiziert werden, aber das Tool erzwingt das Vorhandensein von Testgerüsten – was KI-Agenten effektiv dazu zwingt, für neuen oder komplexen Code sofort begleitende Unit Tests zu schreiben.