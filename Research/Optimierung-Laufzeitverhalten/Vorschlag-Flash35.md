Deine Idee, die Validierung über eine Checksummen-basierte Cache-Strategie zu beschleunigen, macht **absolut Sinn** und ist der **Hebel Nummer 1**, um die Laufzeit in großen Projekten von Minuten auf Sekunden zu senken.

### Das aktuelle Problem im Code

Wenn man den Code in [Program.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs#L357-L361) analysiert, sieht man, dass die `--baseline`-Funktionalität aktuell so implementiert ist:

```csharp
var comparison = BaselineComparer.Compare(storedBaseline, currentChecksums);

var engine = new LinterEngine(config);
var violations = await engine.RunAsync(catalog); // <--- HIER: Analysiert die GANZE Solution
var filtered = BaselineViolationFilter.Filter(violations, comparison.ChangedFiles, outputRoot); // Filtert danach
```

Das bedeutet: **Der Linter führt aktuell trotz Baseline-Vergleich die komplette semantische Analyse auf 100% aller Dateien aus** und filtert die Ergebnisse erst ganz am Ende. Bei großen Projekten führt Roslyn hierbei für jede einzelne Datei Syntax-Parsing, Symbol-Binding und semantische Analysen durch. Das ist extrem speicher- und CPU-intensiv.

---

### Bringt das wirklich was? (Performance-Impact)
**Ja, massiv.** 
* **Ohne Cache:** Jedes Mal müssen Hunderte oder Tausende von Syntaxbäumen und semantische Modelle neu geladen und analysiert werden.
* **Mit Cache (nur geänderte Dateien):** Es werden nur noch die geänderten 1–5 Dateien analysiert. Der Zeitaufwand für die reine Analyse schrumpft von Minuten auf **unter 100 Millisekunden**. 
* Der einzige verbleibende Flaschenhals ist dann das einmalige Laden der Solution via MSBuild (dazu unten mehr).

---

### Verlieren wir dadurch Features? (Dateiübergreifende Prüfungen)
Ja, wenn wir einfach nur geänderte Dateien analysieren und den Rest verwerfen, brechen folgende Features oder erzeugen False-Positives:

1. **`StaticTestSentinel` (Testabdeckungs-Prüfung):**
   * **Das Problem:** Ein Sentinel prüft, ob für die Quellklasse `MyService` (in einer geänderten Datei) eine Testklasse existiert. Die Testklasse `MyServiceTests` liegt jedoch in einer unveränderten Testdatei. Wenn wir die unveränderte Testdatei nicht analysieren, erfährt die Engine nichts von der Existenz von `MyServiceTests`. `MyService` wird fälschlicherweise als "nicht abgedeckt" gemeldet (**False-Positive**).
   * **Die Lösung:** Wir müssen die extrahierten Testsignale (Testklassennamen, `typeof`-Referenzen) aller unveränderten Dateien in einem Cache vorhalten und beim Post-Analysis-Schritt mit den neuen Signalen zusammenführen.

2. **Partial Classes (`AggregatePartialClassLineCount`):**
   * **Das Problem:** Wenn eine Klasse über mehrere Dateien aufgeteilt ist und wir nur den geänderten Teil analysieren, fehlt uns die Zeilenanzahl der unveränderten Teile. Wir können das Limit (`MaxLineCount` für den aggregierten Typ) nicht korrekt berechnen.
   * **Die Lösung:** Die Zeilenanzahl der unveränderten Partial-Teile muss im Cache gemerkt werden.

3. **`AIContextFootprint` (Transitive Kopplungs-Zeilenanzahl):**
   * **Das Problem:** Wenn sich die Größe von Klasse `B` ändert, ändert sich der transitive Footprint von Klasse `A` (die von `B` abhängt). Wenn `A` unverändert ist, würde ihr Footprint nicht neu berechnet werden.
   * **Die Lösung:** Das ist in der Praxis vernachlässigbar. Der Entwickler interessiert sich primär für den Footprint der Klasse, an der er gerade arbeitet. Alternativ kann man den Footprint-Graph basierend auf den gecachten Klassenbeziehungen im Speicher neu bewerten, ohne die Dateien neu zu parsen.

---

### Konzept für eine schnelle, feature-erhaltende Lösung: Der Linter-Cache

Statt nur Dateipfade zu merken, führen wir eine persistente Cache-Datei ein (z. B. `.ainetlinter-cache.json`).

#### 1. Struktur des Cache-Eintrags pro Datei:
Für jede Datei speichern wir:
```json
{
  "FilePath": "src/Services/MyService.cs",
  "Checksum": "sha256-hash...",
  "Violations": [ /* Alle in dieser Datei gefundenen Verstöße */ ],
  "DeclaredClasses": [
    {
      "Name": "MyService",
      "IsStatic": false,
      "BaseTypeNames": ["IService"],
      "MaxCognitiveComplexity": 4
    }
  ],
  "PartialParts": [ /* Name und Zeilenanzahl für partials */ ],
  "TestSignals": {
    "IsTestFile": false,
    "TestClassNames": [],
    "ReferencedTypes": [],
    "CoversComments": []
  }
}
```

#### 2. Der Ablauf des Linter-Runs:
1. **Laden:** Cache einlesen (falls vorhanden).
2. **Abgleich:** Alle Dateipfade der Solution ermitteln und Checksummen berechnen.
3. **Aufteilung:**
   * **Gecacht (98%):** Checksumme stimmt überein $\rightarrow$ Wir übernehmen `Violations`, `DeclaredClasses`, `PartialParts` und `TestSignals` direkt aus dem Cache in den `AnalysisState`.
   * **Zu Analysieren (2%):** Datei ist neu oder Checksumme weicht ab $\rightarrow$ Wir rufen das Roslyn `SemanticModel` ab, führen den `LinterAnalyzer` aus, sammeln die neuen Daten und aktualisieren den Cache-Eintrag.
4. **Post-Analysis-Checks:** Die globalen Checks (Sentinel, Partials, Inheritance) laufen wie gewohnt auf den aggregierten (gecachten + neu analysierten) Klassen und Signalen.
5. **Ausgabe & Speichern:** Ergebnisse ausgeben und den aktualisierten Cache zurück auf die Festplatte schreiben.

---

### Weitere Ideen, um das Prüfen noch schneller zu machen

Selbst mit Cache gibt es bei sehr großen Projekten ein Nadelöhr: **Das Laden der Solution über MSBuildWorkspace.** Das Auswerten der `.csproj` und NuGet-Referenzen kann beim Starten der CLI 10–30 Sekunden dauern.

Hier sind ergänzende Ansätze:

#### Idee A: Ein Watch-Mode (Hintergrund-Daemon)
* **Konzept:** Der Linter läuft als persistenter Prozess im Hintergrund (z. B. über ein Terminal-Kommando `ainetlinter --watch`).
* **Vorteil:** Die Solution wird **nur ein einziges Mal** geladen. Der Daemon überwacht Dateiänderungen über einen `FileSystemWatcher`. Sobald eine Datei gespeichert wird, updatet Roslyn das Dokument im bestehenden In-Memory-Workspace inkrementell. 
* **Latenz:** Der Linter spuckt das Ergebnis in **unter 50 Millisekunden** aus. Dies eignet sich perfekt für direkte Editor-Feedbacks.

#### Idee B: Projekt-basiertes Auditing (`--project`)
* **Konzept:** Wenn man in einer großen Microservice- oder Monolith-Struktur arbeitet, möchte man nicht die gesamte Solution prüfen. Mit einer CLI-Option wie `--project MyService.csproj` wird nur das spezifische Projekt geladen.
* **Vorteil:** Reduziert die Ladezeit der Solution und die Anzahl der zu prüfenden Dateien drastisch, ohne dass man einen Cache benötigt.

#### Idee C: Lightweight Roslyn-Eigenschaften aktivieren
Beim Workspace-Laden nutzen wir bereits Design-Time-Builds in [LinterEngine.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterEngine.cs#L57-L64):
```csharp
    public static Dictionary<string, string> CreateWorkspaceProperties() => new()
    {
        ["DesignTimeBuild"] = "true",
        ["SkipCompilerExecution"] = "true",
        // ...
    };
```
Man könnte prüfen, ob durch das Deaktivieren von XML-Dokumentations-Parsing im Workspace (`workspace.LoadMetadataForReferencedProjects = false`) das Laden der Metadaten noch weiter beschleunigt werden kann.

### Zusammenfassung
Dein Ansatz mit dem Cache ist **vollkommen richtig**. Er löst das Performanceproblem an der Wurzel (der Vermeidung unnötiger Roslyn-Analysen). Um keine Features zu verlieren, müssen wir lediglich die Metadaten der Analysen (Verstöße, deklarierte Klassen und Test-Signale) im Cache vorhalten, anstatt nur die Dateipfade.