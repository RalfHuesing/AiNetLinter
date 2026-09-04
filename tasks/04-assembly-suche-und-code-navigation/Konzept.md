---
status: draft
task: 04-assembly-suche-und-code-navigation
priority: 4
---

# Konzept: Assembly-Suche, AST-Filter & Code-Navigation

## 1. Ziel & Nutzen

Dieses Konzept verfeinert die inhaltliche semantische Analyse und Suche in externen Assemblys für den praktischen Einsatz durch KI-Agenten.

**Kernnutzen:**
- **Präzise Datenzugriffs-Erkennung**: Behebung von `DATA-ACCESS-LINQ-POLLUTION`. Die Suche nach Datenbankoperationen (`data_access`) liefert echte SQL- und DB-Aufrufe statt 80 % gewöhnlicher C#-LINQ-Operationen (`.Select(...)`).
- **Fehlertolerante Dateifilter**: Unterstützung intuitiver Glob-Muster (`*.cs`, `!*Resources*`) in `search_assembly`, statt Agenten zu komplexen Regex-Konstrukten zu zwingen.
- **Fensterbasiertes Lesen langer Methoden**: `get_symbol_body` erhält Paging-Unterstützung (`startLine`, `lineCount`), damit Agenten auch 500-Zeilen-Methoden blockweise und ohne Datenverlust erfassen können.
- **Robuster Call-Tree**: Transitive Aufrufketten (`get_call_tree`) lassen sich tiefenspezifisch abfragen, ohne das Wire-Budget zu sprengen.

---

## 2. Betroffene Projektbereiche & Ist-Zustand

### 2.1 Problembereiche im Code
1. **[AssemblySearchTool.cs:37](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs#L37)**:
   - Der Filter für `data_access` enthält `\bSELECT\b`. Weil Regex case-insensitive ausgeführt wird, matcht jede LINQ-Listenabfrage wie `items.Select(x => x.Id)` oder Query-Syntax `select x`.
   - Folge im Live-Test: Von 20 Treffern waren 16 bloße LINQ-Statements im RAM und keine Datenbankinteraktionen.
2. **Dateifilter in `search_assembly` (`fileFilter`)**:
   - Akzeptiert aktuell ausschließlich vollständige Regex-Ausdrücke. Agenten scheitern oft an Escaping (`\\.cs$`) oder negativen Lookaheads.
3. **[GetSymbolBodyTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CodeGraph/GetSymbolBodyTool.cs)**:
   - Schneidet bei Erreichen von `maxBodyLines` (Default 80) einfach ab. Möchte der Agent die Zeilen 80–160 lesen, gibt es keinen Parameter `startLine` oder `offset`. Er muss die Grenze künstlich auf `maxBodyLines: 200` hochdrehen und liest die ersten 80 Zeilen redundant noch einmal.

---

## 3. Muss-Kriterien & Akzeptanzkriterien

### 3.1 Muss-Kriterien (Funktional)
1. **Intelligente Bereinigung des `data_access`-Filters**:
   - SQL-Keywords wie `SELECT`, `UPDATE`, `INSERT`, `DELETE` werden im C#-Codekontext nur noch gematcht, wenn:
     - sie sich innerhalb eines String-Literals befinden (z. B. `@"SELECT * FROM KHKArtikel"`), ODER
     - typische SQL-Strukturen vorliegen (z. B. `\bSELECT\s+.*?\s+FROM\b`), ODER
     - bekannte DB-Klassen/Interfaces aufgerufen werden (z. B. `DbCommand`, `ExecuteReader`, `SqlQuery`, `FromSqlInterpolated`, `Dapper`).
   - Reines LINQ (`.Select(`, `.Where(`, `select ... in`) wird für die Kategorie `data_access` **explizit ignoriert**.
2. **Glob- und Regex-Support für `fileFilter`**:
   - Wenn `fileFilter` Glob-Sonderzeichen (`*`, `?`, `!`) enthält, wird automatisch ein Regex via Glob-to-Regex-Übersetzer gebildet (z. B. `*.cs` -> `.*\.cs$`, `!*Designer*` -> Negation).
   - Ist es kein Glob, wird es wie gewohnt als Regex interpretiert.
3. **Paging / Offset in `get_symbol_body`**:
   - Neue optionale Parameter:
     - `startLine`: 1-basierte Startzeile innerhalb des Methodenkörpers (Default: 1).
     - `maxBodyLines`: Anzahl der ab Startzeile auszugebenden Zeilen (Default: 80).
   - Liefert Metadaten: `totalBodyLines`, `displayedStartLine`, `displayedEndLine`, `hasMoreLines`.
4. **Erweiterter `get_call_tree`**:
   - Unterstützt Paginierung der Knoten je Ebene (`maxChildrenPerNode`) und filtert Framework-/System-Aufrufe standardmäßig heraus, sofern nicht explizit `includeSystem: true` gesetzt ist.

### 3.2 Akzeptanzkriterien (Verifikation)
- [ ] Ein Test beweist: Ein C#-Codeabschnitt mit `items.Select(x => x.Name)` wird bei `searchKind: "data_access"` **nicht** gematcht, während `context.Database.SqlQuery("SELECT Name FROM...")` sicher gefunden wird.
- [ ] Ein Test beweist: `fileFilter: "*.cs"` filtert alle Nicht-C#-Dateien sauber heraus.
- [ ] Ein Test beweist: `get_symbol_body` mit `startLine: 81, maxBodyLines: 50` liefert exakt die zweite Hälfte einer 130-Zeilen-Methode inklusive korrekter Zeilennummern.

---

## 4. Non-Goals (Scope-Grenzen)

- **Keine Full-Blown SQL-Parser-Engine**: Es genügt eine robuste semantische Heuristik (String-Literale + DB-APIs + SQL-Begleitwörter), um LINQ von SQL zu trennen.
- **Keine Änderung an Git- oder Paginierungs-Infrastruktur**: Diese Komponenten werden als fertig und stabil aus Task `01`, `02` und `03` vorausgesetzt.

---

## 5. Geplante Verifikation

1. **Automatisierte Tests**:
   - Fast-Tests für SQL-/LINQ-Differenzierung in `AssemblySearchToolTests.cs`.
   - Fast-Tests für `GetSymbolBodyToolTests.cs` mit Paging-/Offset-Szenarien.
   - Fast-Tests für Glob-to-Regex-Übersetzung.
2. **Build-Prüfung**:
   - `dotnet build` (warnungs- und fehlerfrei).

---

## 6. Arbeitsgedächtnis (nur Draft)

### Kontextanker & Evidenz
- Aus `tasks/assembly-analyse-verbesserungen/audit-findings-und-ideen.md`:
  - P1-Befund `DATA-ACCESS-LINQ-POLLUTION`: [AssemblySearchTool.cs:37](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs#L37).
  - P2-Befund `SEARCH-FILEFILTER-ERGONOMICS`: Nur Regex in `search_assembly`.
  - P3-Befund `BODY-TRUNCATION-NO-OFFSET`: Kein Paging in `get_symbol_body`.
- Relevante Dateien:
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs`
  - `src/AiNetLinter/Mcp/Tools/CodeGraph/GetSymbolBodyTool.cs`
  - `src/AiNetLinter/Mcp/Tools/CodeGraph/GetCallTreeTool.cs`
