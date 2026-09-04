# Audit-Befunde & Konzeptdiskussion: Paginierung, Filter & Agent-UX

> **Datum**: 2026-09-04  
> **Task**: [tasks/assembly-analyse-verbesserungen](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/assembly-analyse-verbesserungen)  
> **Geprüfter Git-Stand**: `ff36df76` (25 Commits über Nacht, 110 Dateien)  
> **Rolle**: Unabhängiger Auditor & Architektur-Sparringspartner  

---

## 1. Übersicht & Kontext

Über Nacht hat ein autonomes Multi-Agenten-System den Task *Assembly-Analyse-Verbesserungen* in drei Epics durchlaufen und 6.551 Zeilen Code hinzugefügt. Das Kernproblem (unbemerkter Fallback auf Dekompilation trotz konfigurierter Git-Quellen, fehlende Mehrmandanten-/Cache-Sicherheit und fehlende Suchfunktionen) wurde grundsätzlich gelöst.

Dieses Dokument bündelt:
1. Die **fundierten Audit-Befunde (Ist-Zustand)** mit konkreten Code-Stellen und Lösungsvorschlägen.
2. Eine **360°-Architekturbewertung und Konzeptausarbeitung** zu den Anmerkungen und Ideen bezüglich **Paginierung, deterministischer Sortierung und Filterung (Kategorie/Regex)** in Agent-Workflows.

---

## 2. Audit-Befunde des aktuellen Codes

### Übersicht der Findings

| Priorität | ID | Bereich / Datei | Kernproblem | Auswirkung im Betrieb |
|:---|:---|:---|:---|:---|
| **P0** | `GIT-PROGRESS-ABORT` | [ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42) | Git-Clone wertet normalen Netzwerk-Progress auf `stderr` als Fehler. | **Jeder echte Remote-Clone bricht ab**, obwohl Exit-Code 0 ist. |
| **P1** | `GIT-LOCALE-DEPENDENCY` | [GiteaGitRepositoryTransport.cs:420-430](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/GiteaGitRepositoryTransport.cs#L420-L430) | Keine `LC_ALL=C` / `LANG=C` Umgebungsvariablen für Git-Child-Prozesse. | Auf nicht-englischen Systemen weichen Git-Strings ab (z. B. `Klone nach...`). |
| **P2** | `JSON-DOM-TRIM-LOOP` | [AssemblyAnalysisResponse.cs:203-207](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L203-L207) | Iterative Byte-Serialisierung in While-Schleife mit nachträglicher Envelope-Rekonstruktion. | Hohe CPU-/Memory-Last; hat 5 Korrekturschleifen im nächtlichen Lauf verursacht. |
| **P2** | `PROD-TEST-BACKDOOR` | [Program.cs:69-74](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs#L69-L74) / [ExternalSourceCacheLeaseProbeCommand.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/ExternalSourceCacheLeaseProbeCommand.cs) | 243 Zeilen Test-Probe direkt im CLI-Haupteinstiegspunkt verdrahtet. | Verletzung von Clean Architecture; Testcode in der Produktiv-Binary. |

---

### Detailanalyse & Vorschläge

#### Befund P0: `GIT-PROGRESS-ABORT` (Kritisch)
* **Fundstelle**: [ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42) und [GiteaGitRepositoryTransport.cs:250-256](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/GiteaGitRepositoryTransport.cs#L250-L256)
* **Problem**: 
  Beim Ausführen von `git clone` schreibt Git Status- und Fortschrittsinformationen (`remote: Enumerating objects...`, `Receiving objects: 100%...`) standardmäßig auf `stderr`. Die Methode `ExternalSourceGitProcessOutputPolicy.IsAllowedStandardErrorLine` erlaubt jedoch strikt *nur* die exakte Zeile `"Cloning into '.ainetlinter-git-clone'..."`. Jede andere Zeile führt dazu, dass `CreateProcessFailure` anschlägt – selbst wenn Git mit Exit-Code 0 erfolgreich beendet wurde.
  Im FastTest fiel dies nicht auf, da dort ein Mock (`RecordingGitExecutor`) verwendet wurde, der genau diese eine Zeile lieferte.
* **[VORSCHLAG]**:
  Entweder wird `git clone` mit dem Parameter `--quiet` aufgerufen (wodurch Git keine Fortschrittsdaten auf `stderr` ausgibt):
  ```csharp
  // Vorschlag 1: In GiteaGitRepositoryTransport.cs
  "clone",
  "--quiet",
  "--single-branch",
  GitNoTagsArgument,
  "--",
  repositoryUrl,
  CloneDirectoryName
  ```
  Oder `ExternalSourceGitProcessOutputPolicy` erkennt typische Git-Fortschrittszeilen tolerant:
  ```csharp
  // Vorschlag 2: In ExternalSourceGitProcessOutputPolicy.cs
  private static bool IsCloneProgressLine(string line) =>
      line.StartsWith("Cloning into", StringComparison.OrdinalIgnoreCase)
      || line.StartsWith("remote:", StringComparison.OrdinalIgnoreCase)
      || line.StartsWith("Receiving objects:", StringComparison.OrdinalIgnoreCase)
      || line.StartsWith("Resolving deltas:", StringComparison.OrdinalIgnoreCase)
      || line.StartsWith("Updating files:", StringComparison.OrdinalIgnoreCase);
  ```

#### Befund P1: `GIT-LOCALE-DEPENDENCY` (Hoch)
* **Fundstelle**: [GiteaGitRepositoryTransport.cs:416-430](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/GiteaGitRepositoryTransport.cs#L416-L430)
* **Problem**:
  In `CreateEnvironment` werden zwar `safe.directory` und Prompt-Sperren gesetzt, aber weder `LC_ALL` noch `LANG` definiert. Wenn das lokale Git deutschsprachig konfiguriert ist, gibt Git lokalisierte Ausgaben wie `"Klone nach..."` aus, was jede englische String-Prüfung bricht.
* **[VORSCHLAG]**:
  In `CreateEnvironment` explizit die C-Locale erzwingen:
  ```csharp
  environment["LC_ALL"] = "C";
  environment["LANG"] = "C";
  ```

#### Befund P2: `JSON-DOM-TRIM-LOOP` (Mittel / Architektur)
* **Fundstelle**: [AssemblyAnalysisResponse.cs:200-212](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L200-L212)
* **Problem**:
  Antworten werden zuerst unbegrenzt gebaut und dann im Nachgang per JSON-DOM-Manipulation kleingeschnitten:
  ```csharp
  while (JsonSerializer.SerializeToUtf8Bytes(node, McpJsonOptions.Default).Length > budget
      && TryTrimNode(node)) { }
  ```
  Das führt bei 50–100 KB JSON-Bäumen zu massiven Allokationen und CPU-Zyklen (mehrfache Komplettserialisierung). Zudem erfordert es eine hochkomplexe Rückrechnung in [AssemblyAnalysisResponseEnvelope.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponseEnvelope.cs) (fast 500 Zeilen Code), die über Nacht zu 5 Korrekturrunden und schließlich zur Verschiebung ins Tech Debt (`accepted-deferred`) führte.
* **[VORSCHLAG]**:
  Langfristige Abkehr von der nachträglichen DOM-Amputation. Budgetierung muss über **Abfrage-Limits (Paging, maxResults, Filter)** direkt an der Erzeugungsstelle (Roslyn-Traversierung / File-Enumeration) greifen.

#### Befund P2: `PROD-TEST-BACKDOOR` (Mittel / Code-Hygiene)
* **Fundstelle**: [Program.cs:69-74](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs#L69-L74) & [ExternalSourceCacheLeaseProbeCommand.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/ExternalSourceCacheLeaseProbeCommand.cs)
* **Problem**:
  In `Program.cs` wurde eine versteckte Weiche vor das reguläre CLI-Parsing gehängt, die ein 14-teiliges Argument-Array für Interprozess-Tests auswertet.
* **[VORSCHLAG]**:
  Diesen Test-Code in das Test-Projekt oder ein eigenes Test-Worker-Binary auslagern, statt die Produktiv-CLI mit Test-Schnittstellen zu belasten.

---

## 3. Ideen & Architektur-Diskussion: Paginierung, Filter & Agent-UX

### Deine Ausgangsthese:
> *„Wir haben an vielen (allen?) Stellen sinnvollerweise Truncatierung, damit wir das Kontextfenster von Agenten nicht sprengen. Einfach abschneiden, ohne dass der Agent eine Chance hat, auf die Informationen 'ganz hinten' zuzugreifen, ist Mist. [...] Ich stelle mir Pagination vor [...] deterministisch sortiert [...] Agent weiß wie viele Pages es gibt (1 von N) [...] jeder Aufruf mit Filter (Kategorie / Text / Regex).“*

### Beurteilung aus Sicht des Kritikers & Auditors

#### 1. Volle Zustimmung: „Abschneiden ohne Fortsetzung ist Mist“
Ein harter Cut (`isTruncated = true`) ohne Möglichkeit zur Fortsetzung führt bei LLMs zu **Halluzinationen oder falschen Negativurteilen**:
- Der Agent sucht eine Klasse `OrderProcessor`.
- Der Server bricht bei 50 Typen ab. `OrderProcessor` lag an Position 52.
- Der Agent folgert: *„Die Klasse existiert nicht im System.“*
- **Fazit**: Jedes gekürzte Ergebnis **MUSS** erreichbar bleiben.

---

#### 2. Offset/Page-Nummerierung (`page: 1 von N`) vs. Cursor (`continuationToken`)

Du schlägst ein klassisches `page`-Modell vor (*„Seite 1 von 2348“*). Das ist für Menschen auf Webseiten Standard. Für **LLM-Agenten** gibt es jedoch entscheidende Vor- und Nachteile:

| Aspekt | Klassische Paginierung (`page=1`, `totalPages=N`) | Opaque Cursor / Continuation-Token (`cursor="abc"`) |
|:---|:---|:---|
| **Verständlichkeit für LLM** | **Sehr hoch**: LLM versteht sofort `page: 2`. | **Hoch**: LLM übergibt einfach `cursor: token`. |
| **Gezieltes Vorblättern** | **Möglich**: LLM könnte theoretisch direkt auf Seite 5 springen. | **Nein**: Nur sequenzielles Weiterblättern möglich. |
| **Performance bei großen Daten** | **Schlecht**: Um `totalPages` zu berechnen, muss das System *alle* 100.000 Dateien/Symbole vorab scannen/filtern/zählen, bevor Seite 1 ausgeliefert werden kann. | **Sehr gut**: Das System scannt nur bis `pageSize + 1` und liefert sofort zurück (Streaming/Lazy). |
| **Stabilität bei Änderungen** | **Fragil**: Wenn Dateien hinzukommen oder gelöscht werden, verschieben sich die Offsets; Elemente erscheinen doppelt oder werden übersprungen. | **Stabil**: Der Token kann einen Snapshot-/Revisionsstand mitführen. |
| **Gefahr für Agent-Loops** | **Hoch**: Sieht ein LLM `Seite 1 von 80`, neigt es oft dazu, in einer Schleife alle 80 Seiten abzurufen, und sprengt erst recht sein Kontextfenster. | **Geringer**: Token signalisiert gezieltes Fortsetzen bei Bedarf. |

* **[VORSCHLAG / SYNTHESE]**:
  Wir sollten für den Agenten ein **hybrides Paging-Modell** anbieten:
  ```json
  {
    "pagination": {
      "page": 1,
      "pageSize": 50,
      "totalItems": 142,
      "totalPages": 3,
      "hasMore": true,
      "nextCursor": "offset:50"
    }
  }
  ```
  - Wo die Gesamtmenge schnell ermittelbar ist (z. B. Roslyn InMemory-Typen oder gecachte Dateilisten), liefern wir `totalItems` und `totalPages`.
  - Wo ein vollständiger Scan teuer wäre (z. B. tiefe Dateisystemsuche über fremde Repos), liefern wir `hasMore: true` und den `nextCursor`, um unnötige Vorab-Zählungen zu vermeiden.

---

#### 3. Deterministische Sortierung (Das Fundament)

Du hast völlig recht: **Paginierung ohne deterministische Sortierung ist nutzlos.**
Wenn ein Tool `Directory.EnumerateFiles()` oder ein `HashSet<ISymbol>` abfragt, ist die Reihenfolge im Dateisystem oder Memory undefiniert. Seite 2 könnte dieselben Elemente wie Seite 1 enthalten.

* **[VORSCHLAG]**:
  Einführung einer strikten Projekt-Konvention für alle listenbasierten MCP-Tools:
  1. **Dateien/Pfade**: Immer `OrderBy(p => p, StringComparer.OrdinalIgnoreCase)`.
  2. **Symbole**: Zuerst nach kanonischem Symbol-Identifier bzw. vollqualifiziertem Typnamen `OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal)`.
  3. **Verstöße/Diagnostics**: Zuerst nach Dateipfad, dann Zeilennummer, dann Regel-ID.

---

#### 4. Filter-First-Strategie: Warum Filtern wichtiger ist als Blättern

Ein Agent sollte **fast nie** durch 10 Seiten blättern müssen. Jeder Tool-Call kostet 2–10 Sekunden Roundtrip-Zeit und mehrere tausend Prompt-Tokens. 

Wenn ein Tool 2.000 Symbole hat, ist die Lösung nicht, dass der Agent 40-mal `page=1, 2, ..., 40` aufruft, sondern dass er die Treffer mit Parametern sofort auf 5–10 Treffer einschränkt.

Deine Idee mit **Kategorie-Filtern und Text-/Regex-Filtern** ist genau der richtige Hebel!

* **[VORSCHLAG: Universelle Filter-Matrix für Listen-Tools]**:
  Jedes listenbasierte MCP-Tool sollte 3 optionale Filter-Dimensionen unterstützen:

  1. **Struktur-/Kategorie-Filter (`kind` / `category`)**:
     - Bei Symbolen: `kind = "Class" | "Interface" | "Method" | "Property"`.
     - Bei Dateien: `extension = ".cs" | ".json"` oder `category = "source" | "test" | "config"`.
     - Bei Violations: `severity = "Error" | "Warning"`, `ruleId = "NoAsyncVoid"`.
  2. **Namens- / Pfadfilter (`pattern` / `namePattern`)**:
     - Standard: Einfacher, robuster Case-Insensitive Substring-Match (z. B. `namePattern: "Repository"`).
  3. **Opt-in Regex (`regexFilter` / `isRegex: true`)**:
     - Für komplexe Muster (z. B. `^I[A-Z].*Service$`).
     - **WICHTIG (Auditor-Warnung zu Regex)**: Jeder Regex, den ein Agent übergibt, **muss zwingend mit einem Match-Timeout versehen sein** (z. B. `TimeSpan.FromMilliseconds(100)` wie in `AssemblySearchTool`), um ReDoS-Angriffe oder Hänger durch fehlerhafte Regex-Muster zu verhindern.

---

#### 5. Das Composite-Dilemma (`get_assembly_context` & `get_feature_context`)

Ein spezielles Problem entsteht bei zusammengesetzten Werkzeugen (Composite Tools):
- `get_assembly_context` liefert gleichzeitig: `types`, `metrics`, `callers`, `impact` und `body`.
- **Hier funktioniert ein einzelner Parameter `page` nicht!** Welche Liste soll Seite 2 sein? Die Callers? Die Typen?

* **[VORSCHLAG: 2-Stufen-Prinzip für Composite-Tools]**:
  1. **Stufe 1 (Composite)** liefert nur eine **Vorschau (Top 5-10) + Zähler + Verweis auf das Spezialtool**:
     ```json
     {
       "callers": {
         "totalCount": 48,
         "preview": [ "... 5 Caller ..." ],
         "isTruncated": true,
         "fetchMoreHint": "Nutze 'get_call_tree' mit symbolIdentifier='...' und page=2 für alle 48 Aufrufer."
       }
     }
     ```
  2. **Stufe 2 (Spezialtool)**: Erst im dedizierten Tool (`get_call_tree`, `find_references`, `get_violations`) greift die vollständige Paginierung (`page`, `pageSize`, `filter`). Das hält das Composite schlank und verhindert überkomplexe Parameter-Objekte.

---

## 4. Konkreter Umsetzungsvorschlag (Harmonisierter Standard)

Um Wildwuchs über die verschiedenen MCP-Tools zu verhindern, sollten wir ein einheitliches C#-Standardmodell für Paginierung und Filterung in `AiNetLinter.Mcp` etablieren:

### 1. Einheitliches Paging-Request-Record
```csharp
// [VORSCHLAG] In AiNetLinter.Mcp.Common
public sealed record PaginationArgs(
    int Page = 1,
    int PageSize = 50,
    string? Cursor = null,
    string? Filter = null,
    bool IsRegex = false,
    string? Category = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 250;

    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
    public int Offset => (NormalizedPage - 1) * NormalizedPageSize;
}
```

### 2. Einheitliches Envelope-Response-Record
```csharp
// [VORSCHLAG] In AiNetLinter.Mcp.Common
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasMore,
    string? NextCursor = null,
    string? FilterApplied = null);
```

### 3. Vorteile für die Agenten-UX
- **Konsistenz**: Jedes Tool (`find_symbol`, `get_file_tree`, `get_violations`, `search_assembly`) spricht exakt dieselbe Sprache (`page`, `pageSize`, `filter`, `category`).
- **Transparenz**: Der Agent sieht in den Metadaten immer:
  `"Zeige Seite 1 von 5 (Einträge 1 bis 50 von 230 gefiltert nach 'Service')"`
- **Verlässlichkeit**: Wenn `totalItems > pageSize`, weiß der Agent exakt, dass noch etwas fehlt, und kann entweder `page: 2` abrufen oder seinen Filter präzisieren.

---

## 5. Empfohlene nächste Schritte

1. **Sofort-Fix (P0 & P1)**: 
   - `GiteaGitRepositoryTransport.cs`: `--quiet` ergänzen und `LC_ALL=C` setzen, damit echte Git-Remote-Clones nicht am `stderr`-Parser scheitern.
2. **Design-Bereinigung (P2)**:
   - Die nächtliche JSON-DOM-Verstümmelungsschleife in `AssemblyAnalysisResponse.cs` durch saubere Paginierungsvorgaben an den Datenquellen schrittweise ablösen.
3. **Paginierungs- & Filter-Rollout**:
   - Den in Abschnitt 4 beschriebenen Standard (`PaginationArgs` / `PagedResult<T>`) als Referenzmuster implementieren und auf die wichtigsten listenbasierten MCP-Tools anwenden.
