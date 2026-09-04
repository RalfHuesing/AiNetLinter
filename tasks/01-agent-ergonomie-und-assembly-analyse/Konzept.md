---
status: implemented
task: 01-agent-ergonomie-und-assembly-analyse
priority: 1
---

# Konzept: Agent-Ergonomie, Assembly-Analyse & Präzise Code-Navigation

## 1. Ziel und Nutzen

Dieses Konzept bündelt die essenziellen, praxiserprobten Verbesserungen für KI-Agenten, die den AiNetLinter MCP-Server während der Softwareentwicklung und Code-Analyse einsetzen.

Im praktischen Einsatz von LLM-Agenten (Antigravity, Cursor, Claude) haben sich fünf konkrete Schwachstellen gezeigt, die Tokens verbrennen, Suchergebnisse mit Rauschen fluten oder den Agenten durch leere Textantworten erblinden lassen. Dieses Konzept behebt diese Schwachstellen gezielt, schlank und ohne spekulatives Over-Engineering:

1. **Präzise Datenzugriffssuche (`data_access`)**: Beseitigt die Kontamination durch gewöhnliche C#-LINQ-Statements (`.Select(...)`). Agenten finden echte SQL- und Datenbankzugriffe statt 80 % In-Memory-Listenoperationen.
2. **Fehlertolerante Glob-Filter (`fileFilter`)**: Ermöglicht einfache Glob-Muster (`*.cs`, `!*Designer*`) in der Assembly-Suche, anstatt Agenten zu komplexem Regex-Escaping zu zwingen.
3. **Windowing für lange Methoden (`get_symbol_body`)**: Ermöglicht das gezielte Lesen von Methodenabschnitten via `startLine` und `maxBodyLines`, ohne bei großen Methoden hunderte Zeilen redundant übertragen zu müssen.
4. **Kompakte relative Pfade in Assembly-Symbolen**: Ersetzt ~180 Zeichen lange absolute Cache-Pfade in `find_symbol` durch saubere, relative Pfade ab Assembly-Wurzel (`MyNamespace/Class.cs`), was pro Response hunderte Tokens spart.
5. **Verlässliche Textdarstellung & Short-Circuiting**: Verhindert das Erblinden des Agenten (`MCP-TEXT-BLACK-HOLE`), behält `maxResults` als steuerbaren Parameter bei und bricht bei Trefferlimits sofort frühzeitig ab (Short-Circuit), statt die Solution unnötig bis `int.MaxValue` zu durchsuchen.

---

## 2. Betroffene Projektbereiche & Ausgangslage

### 2.1 Betroffene Komponenten
- **`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs`**:
  Enthält aktuell die Regex-Definitionen für `data_access` und die Auswertung von `fileFilter`.
- **`src/AiNetLinter/Mcp/Tools/CodeGraph/GetSymbolBodyTool.cs`**:
  Liest Methodenrümpfe und schneidet bisher nach `maxBodyLines` ohne Offset-Unterstützung ab.
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs` & Formatter**:
  Gibt bei dekompilierten Assemblys aktuell den vollen absoluten temporären Cache-Dateipfad aus.
- **`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`**:
  Verantwortlich für die Text- und JSON-Projektion bei Assembly-Antworten; neigt bei Budget-Überschreitung zum vollständigen Verwerfen des Textinhalts.
- **`src/AiNetLinter.FastTests/` & `src/AiNetLinter.IntegrationTests/`**:
  Gezielte Unit- und Regressionstests für alle 5 Bereiche.

---

## 3. Muss-Kriterien & Fachliche Anforderungen

### 3.1 Bereinigung der `data_access`-Suche (LINQ-Pollution)
- **Problem**: Die Suche nach `searchKind: "data_access"` nutzt aktuell `\bSELECT\b` (case-insensitive). In C# matcht das gewöhnliche LINQ-Aufrufe wie `items.Select(x => x.Id)` oder Query-Syntax `select x`. Im Live-Test waren 16 von 20 Treffern reines LINQ im Arbeitsspeicher.
- **Muss-Kriterium**:
  - Gewöhnliche C#-LINQ-Methoden (`.Select(`, `.Where(`, `.OrderBy(`, `.GroupBy(`) und die C#-Query-Keywords (`select\s+[a-zA-Z0-9_]+\s+in`) werden für `data_access` **explizit ignoriert**.
  - SQL-Keywords (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `EXEC`) matchen nur, wenn:
    - sie sich innerhalb eines C#-String-Literals befinden (z. B. `@"SELECT * FROM ..."` oder `$"UPDATE {table}..."`), ODER
    - typische SQL-Strukturmuster vorliegen (z. B. `\bSELECT\s+.*?\s+FROM\b`), ODER
    - bekannte Datenbank-Klassen, Interfaces oder Methoden aufgerufen werden (`DbCommand`, `ExecuteReader`, `ExecuteNonQuery`, `SqlQuery`, `FromSqlInterpolated`, `Dapper`, `DataContext`, `SaveChanges`).

### 3.2 Glob-Unterstützung für `fileFilter`
- **Problem**: `search_assembly` verlangt aktuell für `fileFilter` zwingend reguläre Ausdrücke. Agenten scheitern häufig an Regex-Besonderheiten (z. B. `(?<!Designer)\.cs$`).
- **Muss-Kriterium**:
  - `fileFilter` erkennt automatisch, ob ein Glob-Muster vorliegt (Präsenz von `*`, `?`, `!` oder Pfadseparatoren).
  - Glob-Muster werden intern deterministisch in einen Regex übersetzt:
    - `*.cs` matcht alle C#-Dateien.
    - `*Service*.cs` matcht alle C#-Dateien mit "Service" im Namen.
    - `!*Designer*` schließt Designer-Dateien aus.
  - Enthält der String keine Glob-Syntax, bleibt er abwärtskompatibel als regulärer Ausdruck wirksam.

### 3.3 Windowing in `get_symbol_body`
- **Problem**: Bei langen Methoden (> 80 Zeilen) bricht `get_symbol_body` ab. Will der Agent Zeilen 81–160 lesen, muss er `maxBodyLines: 160` setzen und liest Zeilen 1–80 redundant ein zweites Mal.
- **Muss-Kriterium**:
  - Neuer optionaler Parameter `startLine` (1-basierte Zeilennummer relativ zum Methodenbeginn, Default: 1).
  - Bestehender Parameter `maxBodyLines` bestimmt die maximale Zeilenanzahl ab `startLine` (Default: 80, Min: 1, Max: 500).
  - Das Ergebnis enthält Metadaten zur Navigation:
    - `displayedStartLine`: Erste angezeigte Zeilennummer.
    - `displayedEndLine`: Letzte angezeigte Zeilennummer.
    - `totalBodyLines`: Gesamtzeilenzahl der Methode.
    - `hasMoreLines`: Boolean, ob nach `displayedEndLine` noch Zeilen folgen.
  - Textdarstellung zeigt den Zeilenbereich klar an (z. B. `Methodenrumpf MyMethod (Zeilen 81-135 von 135)`).

### 3.4 Relative Pfade in dekompilierten Symbol-Ergebnissen
- **Problem**: `find_symbol` gibt im Assembly-Modus vor jedem Treffer den vollen, ~180 Zeichen langen Cache-Pfad aus (`C:\Daten\Tools\AiNetLinter-win-x64\cache\asm-decompile-7a8b9c\Services\OrderService.cs`).
- **Muss-Kriterium**:
  - Der Pfad wird relativ zum virtuellen Wurzelverzeichnis der dekodierten Assembly ausgegeben (z. B. `Services/OrderService.cs`).
  - Dies spart bei 30 Treffern über 1.000 Tokens pro MCP-Response.

### 3.5 Verlässliche Textdarstellung & Short-Circuiting (`MCP-TEXT-BLACK-HOLE`)
- **Problem**:
  1. Bei großen Payloads hat der Server früher das gesamte Textfeld durch eine pauschale Meldung ersetzt (*„StructuredContent ist die kanonische Nutzlast...“*), wodurch LLMs erblindeten.
  2. Bisherige Paging-Versuche haben die Solution bis `int.MaxValue` durchsucht und die Ergebnisse danach per `Skip/Take` zerschnitten.
- **Muss-Kriterium**:
  - **Kein Text-Black-Hole**: Das primäre Textfeld `content[0].text` wird **niemals** vollständig durch einen Einzeiler ersetzt. Wenn gekürzt werden muss, werden die ersten Treffer im Text gerendert und eine kurze Hinweiszeile angehängt.
  - **Short-Circuiting im Scanner**: Wenn `maxResults = 20` angefordert ist, bricht der Roslyn-/Syntax-Scanner nach Erreichen von `maxResults + 1` Treffern sofort ab. Er durchsucht **nicht** die gesamte Solution bis `int.MaxValue`.
  - **Pragmatischer Offset-Support**: Für flache Listen wird ein optionaler Parameter `offset` (int, Default 0, Min 0) unterstützt. Liefert ein Scan `maxResults + 1` Treffer, weiß der Server sofort, dass weitere Treffer existieren (`hasMore = true`), schneidet den letzten Puffer-Treffer ab und gibt den nächsten Offset an:
    ```text
    Zeige 20 Treffer (weitere Treffer vorhanden).
    Tipp: Präzisiere 'namePattern' oder nutze 'offset: 20' für die nächsten Treffer.
    ```
  - **`maxResults` bleibt erhalten**: Alle Tools behalten ihren steuerbaren Parameter `maxResults` (Default 20 bzw. 50, Min 1, Max 100).

---

## 4. Explizite Non-Goals (Scope-Grenzen)

- **Kein Remote-Git-Download / Git-Engine**: AiNetLinter klont keine externen Repositories im Hintergrund. Externe Assemblys werden wie bisher schnell und lokal über die ILSpy-/Roslyn-Dekompilation analysiert.
- **Kein kryptografisches Snapshot-Hashing**: Es werden keine SHA-256-Fingerprints über die gesamte Solution oder das Dateisystem berechnet. Der MCP-Server läuft lokal in der IDE des Entwicklers; best-effort Navigation reicht vollkommen aus.
- **Kein universelles `PaginationArgs` / `PagedResult<T>`**: Fachliche Filter (`namePattern`, `kind`, `severity`, `ruleId`) bleiben strikt tool-spezifisch. Es wird keine künstliche Vererbungshierarchie über alle Tools gestülpt.
- **Keine Full-Blown SQL-Parser-Engine**: Für die Trennung von LINQ und SQL genügt eine robuste lexikalisch-semantische Heuristik (String-Literale, DB-APIs, SQL-Begleitwörter).

---

## 5. Betriebs- und Fehlermodell

- **Fehlerfreie Degration**: Schlägt ein Glob-Muster in `fileFilter` fehl, fällt das System transparent auf die Behandlung als regulärer Ausdruck zurück; führt auch dies zu einem Regex-Syntaxfehler, wird eine verständliche `INVALID_ARGUMENT`-Fehlermeldung mit Hilfestellung geliefert.
- **Out-of-Bounds-Toleranz in `get_symbol_body`**: Wird `startLine` größer als `totalBodyLines` übergeben, stürzt das Tool nicht ab, sondern liefert eine leere Zeilenausgabe mit `hasMoreLines = false` und dem Hinweis, dass die Zeilennummer außerhalb der Methode liegt.
- **Regex-Sicherheit**: Alle Regex-Evaluationen (sowohl übersetzte Globs als auch Anwender-Filter) werden mit einem strikten Timeout (100 ms) ausgeführt, um ReDoS-Blockaden des Servers zu verhindern.

---

## 6. Geplante Verifikation

### 6.1 Automatisierte Tests
1. **FastTests (Unit & Component)**:
   - `AssemblySearchToolTests`: Verifikation, dass `.Select(...)` ignoriert wird, während `SELECT ... FROM` und `context.Database.SqlQuery(...)` zuverlässig als `data_access` erkannt werden.
   - `AssemblySearchGlobTests`: Verifikation der Glob-to-Regex-Übersetzung (`*.cs`, `!*Resources*`, `*Service*`).
   - `GetSymbolBodyToolTests`: Tests für `startLine: 1`, `startLine: 50`, `maxBodyLines`, Randfälle (Zeilennummer zu groß, 1-Zeilen-Methoden).
   - `AssemblyFindSymbolTests`: Nachweis relativer Pfade anstelle absoluter Cache-Pfade.
   - `ShortCircuitingTests`: Nachweis, dass Scanner bei Erreichen von `maxResults + 1` die Suche sofort beenden.
2. **IntegrationTests**:
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (vollständiger Durchlauf).
3. **Build-Prüfung**:
   - `dotnet build` (warnungs- und fehlerfrei mit `TreatWarningsAsErrors = true`).

### 6.2 Qualitäts- und Linter-Checks
- Nach der Umsetzung: Gezielte MCP-Prüfung via `get_violations` auf den geänderten Dateien.
- Keine Einführung neuer Linter-Warnungen oder unzulässiger Parametergrenzen.

---

## 7. Aufteilung in handhabbare Arbeitspakete (Roadmap-Vorschau)

1. **Paket 1: Semantische Filter & Dateimuster (`data_access` LINQ-Bereinigung & Glob-Filter)**
   - `AssemblySearchTool.cs` bereinigen (SQL vs. LINQ).
   - Glob-to-Regex-Helper integrieren und mit Tests absichern.
2. **Paket 2: Methoden-Windowing & Pfad-Ergonomie**
   - `GetSymbolBodyTool.cs` um `startLine` erweitern.
   - Relative Pfade in `AssemblyFindSymbolTool.cs` durchsetzen.
3. **Paket 3: Response-Projektion & Short-Circuiting**
   - Sicherstellen, dass Text-Responses niemals durch `StructuredContent`-Hinweise ausgelöscht werden.
   - Frühzeitiges Abbrechen in Scannern nach `maxResults + 1`.
   - Schlanker `offset`-Support mit Hinweisen in der Textausgabe.
4. **Paket 4: Gesamtabschluss & Dokumentation**
   - Aktualisierung von `Docs/agent-api.md`.
   - Vollständiger grüner Testlauf über beide Testprojekte.
