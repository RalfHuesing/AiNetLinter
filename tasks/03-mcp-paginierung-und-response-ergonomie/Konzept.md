---
status: draft
task: 03-mcp-paginierung-und-response-ergonomie
priority: 3
---

# Konzept: MCP-Paginierung, Wire-Budget & Response-Ergonomie

## 1. Ziel & Nutzen

Dieses Konzept löst das fundamentale Usability-Problem für KI-Agenten: **Die Erblindung durch harte Truncation und leere Textantworten.**

**Kernnutzen:**
- **Beseitigung des P0-Fehlers `MCP-TEXT-BLACK-HOLE`**: Der Server ersetzt den Textinhalt niemals mehr durch einen nutzlosen Truncation-Einzeiler. Der Agent sieht immer die wichtigsten Treffer im Textfeld (`content[0].text`), da LLMs primär hierauf zugreifen.
- **Echtes Paging an der Datenquelle statt nachträglicher Trimm-Schleife**: Ersetzen der CPU- und Memory-intensiven While-Schleife in [AssemblyAnalysisResponse.cs:203](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L203) durch saubere Paginierung (`page`, `pageSize`, `cursor`).
- **Verlässliche Navigation für Agenten**: Ein einheitlicher Standard (`PaginationArgs`, `PagedResult<T>`) ermöglicht es Agenten, gezielt vorzublättern („Seite 2 von 5“) oder nach Kategorien zu filtern, ohne Daten am Ende abzuschneiden.
- **Token-Ersparnis**: Relative Pfade in `find_symbol` statt riesiger interner Cache-Pfade sparen hunderte unnötige Prompt-Tokens pro Call.

---

## 2. Betroffene Projektbereiche & Ist-Zustand

### 2.1 Problembereiche im Code
1. **[AssemblyAnalysisResponse.cs:203-207](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L203-L207)**:
   - Iteriert in einer While-Schleife, serialisiert JSON immer wieder neu und wirft bei Budget-Überschreitung den Text komplett weg:
     ```text
     [ASSEMBLY] StructuredContent ist die kanonische Nutzlast; die Textdarstellung wurde wegen des gemeinsamen Wire-Budgets gekürzt.
     ```
   - Folge: Für LLM-Clients wie Cursor, Antigravity und Claude Desktop sieht das Ergebnis aus wie **0 Treffer**.
2. **Fehlende Paginierungs-Parameter**:
   - `search_assembly`, `get_assembly_context`, `get_call_tree` kennen kein `page` oder `pageSize`. Sie schneiden hart ab (`isTruncated = true`), ohne dass der Agent jemals an die hinteren Ergebnisse herankommt.
3. **[AssemblyAnalysisContextFactory.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs)**:
   - `get_assembly_context` rendert im Textmodus lediglich `Abschnitt: metrics` oder `Abschnitt: types`, liefert aber keinen einzigen Typnamen oder Metrikwert im Text mit.
4. **[FindSymbolTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CodeGraph/FindSymbolTool.cs)**:
   - Gibt im Assembly-Modus den vollen internen Cache-Pfad (`C:\Daten\Tools\AiNetLinter-win-x64\cache\asm.antigravity\...`) für jedes einzelne gefundene Symbol aus.

---

## 3. Muss-Kriterien & Akzeptanzkriterien

### 3.1 Muss-Kriterien (Funktional)
1. **Paging-First an der Datenquelle**:
   - Einführung von `PaginationArgs`:
     ```csharp
     public sealed record PaginationArgs(
         int Page = 1,
         int PageSize = 50,
         string? Cursor = null,
         string? Filter = null,
         bool IsRegex = false,
         string? Category = null);
     ```
   - Alle listenbasierten Assembly-Tools (`search_assembly`, `find_symbol`, `get_call_tree`, `get_violations`) akzeptieren diese Parameter.
   - Paginierung greift **bevor** serialisiert wird (z. B. `LINQ: .Skip(offset).Take(pageSize)`).
2. **Deterministische Sortierung**:
   - Ergebnisse werden vor der Paginierung ausnahmslos stabil sortiert:
     - Pfade: `StringComparer.OrdinalIgnoreCase`.
     - Symbole: Vollqualifizierter Typ-/Membername (`StringComparer.Ordinal`).
     - Verstöße: Datei, Zeile, Regel-ID.
3. **Paging-Metadaten im Response-Envelope (`PagedResult<T>`)**:
   - Liefert stets: `Page`, `PageSize`, `TotalItems`, `TotalPages`, `HasMore`, `NextCursor`.
4. **Niemals leerer Text bei vorhandenen Treffern**:
   - Textdarstellung liefert immer mindestens die ersten Treffer der angeforderten Seite.
   - Wenn das Wire-Budget knapp wird, wird der Text am Zeilenende sauber abgeschnitten und mit folgendem Hinweis versehen:
     `"Treffer 1-10 von 140 angezeigt. Nutze page=2 oder verfeinere mit filter='...'."`
   - Der Satz `"[ASSEMBLY] StructuredContent ist die kanonische Nutzlast..."` wird ersatzlos gestrichen.
5. **Kompakte Textdarstellung für Composite-Tools (`get_assembly_context`)**:
   - Statt leerer Überschriften liefert das Tool eine aussagekräftige Vorschau (z. B. Top 5 Klassen, Anzahl Methoden, Schlüsselmetriken) und verweist für Vollständigkeit auf die Einzeltools.
6. **Relative Pfade in `find_symbol`**:
   - Pfade innerhalb des Assembly-Caches werden relativ zum Assembly-/Quellcode-Root formatiert (z. B. `src/Core/OrderService.cs:45` statt `C:\Daten\Tools\AiNetLinter-win-x64\cache\asm...`).

### 3.2 Akzeptanzkriterien (Verifikation)
- [ ] Ein Test beweist: Ein Aufruf von `search_assembly` mit vielen Treffern liefert im Textfeld verständliche Trefferzeilen und **keine** `StructuredContent ist die kanonische Nutzlast`-Meldung.
- [ ] Ein Test beweist: Aufruf mit `page=1, pageSize=5` und anschließend `page=2, pageSize=5` liefert disjunkte, lückenlose Ergebnisse in deterministischer Reihenfolge.
- [ ] Die nächtliche While-Schleifen-DOM-Kürzung in `AssemblyAnalysisResponse.cs` ist entfernt und durch Budget-Checks ersetzt.

---

## 4. Non-Goals (Scope-Grenzen)

- **Keine Änderungen an der Git-Download-Logik**: Dies ist in Task `01` abgeschlossen.
- **Keine Änderungen an der Quellcode-Zuordnung**: Dies ist in Task `02` abgeschlossen.
- **Keine Überarbeitung der SQL-/LINQ-Heuristiken**: Dies ist Gegenstand von Task `04`.

---

## 5. Geplante Verifikation

1. **Automatisierte Tests**:
   - `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
   - Spezifische Wire-Budget- und Pagination-Tests in `AssemblySearchToolTests` und `GetAssemblyContextToolTests`.
2. **Build-Prüfung**:
   - `dotnet build` (warnungs- und fehlerfrei).

---

## 6. Arbeitsgedächtnis (nur Draft)

### Kontextanker & Evidenz
- Aus `tasks/assembly-analyse-verbesserungen/audit-findings-und-ideen.md`:
  - P0-Befund `MCP-TEXT-BLACK-HOLE`: Textdarstellung wird bei Budget-Überschreitung gelöscht.
  - P1-Befund `COMPOSITE-TEXT-EMPTY`: `get_assembly_context` Textausgabe hat keinen fachlichen Inhalt.
  - P2-Befund `JSON-DOM-TRIM-LOOP`: While-Schleife in `AssemblyAnalysisResponse.cs:203-207`.
  - P2-Befund `FIND-SYMBOL-ABSOLUTE-PATH`: Monster-Cache-Pfade in `find_symbol`.
- Aus dem Chat mit Ralf:
  - Paginierung muss deterministisch sein.
  - LLM muss wissen: `1 von N Seiten`.
  - Filter-First: Textfilter und Kategorien müssen vorhanden sein, damit Agenten nicht 20 Seiten durchblättern müssen.
