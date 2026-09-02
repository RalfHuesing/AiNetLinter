# MCP-Server Usability & Ergonomie Audit: Neue Befunde (v1.0.161)

**Datum:** 2026-09-02  
**Zielsystem:** AiNetLinter MCP-Server (Version 1.0.161)  
**Status:** Offen für zukünftige Umsetzung  
**Fokus:** Usability-Hürden, Agenten-Sackgassen, Token-Waste und Parameter-Inkonsistenzen

---

## 1. Übersicht & Priorisierungsmatrix

| ID | Kategorie | Schweregrad | Aufwand | Betroffene Tools | Kurzbeschreibung |
|---|---|---|---|---|---|
| `[F-01]` | `[Agenten-Sackgasse / Graph-Bruch]` | **P1** | S-M | `get_symbol_body`, `find_references`, `get_call_tree`, `get_class_structure`, `get_type_hierarchy`, `get_impact` | **Sackgasse bei Assembly-Disambiguierung:** Tool schlägt nackte `M:...`-IDs vor, lehnt diese im Folgeaufruf als `StaleAssemblyId` strikt ab. |
| `[F-02]` | `[API & Parameter]` | **P2** | S | `get_symbol_body`, `find_symbol` | **Inkonsistente Parameter-Kardinalität:** Zwang zu Plural-Arrays (`symbolIdentifiers`, `namePatterns`), während alle anderen Tools skalare Strings (`symbolIdentifier`, `namePattern`) nutzen. |
| `[F-03]` | `[Token-Waste & Signal-to-Noise]` | **P2** | S | `get_hotspots` | **Fehlender `scopeType`-Filter:** Testdateien (500 Zeilen) verdrängen Produktionsdateien in den Top-Kritisch-Hotspots. |
| `[F-04]` | `[API & Parameter]` | **P3** | S | `inspect_assembly`, `find_assembly_extensions` | **Redundante `targetType="assembly"`-Pflicht:** Reine Assembly-Tools schlagen fehl, wenn `targetType` weggelassen wird. |
| `[F-05]` | `[Token-Waste & Payload-Bloat]` | **P3** | S | `pattern_detect` | **Payload-Bloat bei 0 Treffern:** 6 vollständige Sektionen mit Erläuterungstexten und "Keine." fluten den Kontext bei sauberer Codebase. |
| `[F-06]` | `[Ergonomie & Genauigkeit]` | **P3** | S | `get_index_scope` | **Starre Dateityp-Aufschlüsselung:** Listet hardcodiert 5 irrelevante Web/XAML-Null-Einträge, verschweigt aber reale Nicht-C#-Dateien (`.json`, `.md`, etc.). |

---

## 2. Detaillierte Mängelberichte & Umsetzungsvorschläge

---

### `[F-01]` Sackgasse bei Assembly-Disambiguierung (`AmbiguousSymbol` vs. `StaleAssemblyId`)

#### 1. Problembeschreibung & Reproduktion
Wenn ein Methoden- oder Typname in einer dekompilierten Assembly mehrdeutig ist (z. B. Überladungen wie `Dispose()` vs. `Dispose(bool)`), gibt der Server korrekterweise `[ERROR]: AMBIGUOUS_SYMBOL` zurück und listet die Fundstellen auf:

```text
[ERROR]: AMBIGUOUS_SYMBOL: Identifikator 'Sagede.OfficeLine.Rewe.Buchungserfassung.Aufteilungsbuchung.Dispose' ist mehrdeutig — mehrere Symbole gefunden.
  context: 00045-Sagede_OfficeLine_Rewe_Buchungserfassung_Aufteilungsbuchung.cs:90 - Methode: ...Dispose(bool) id: `M:Sagede.OfficeLine.Rewe.Buchungserfassung.Aufteilungsbuchung.Dispose(System.Boolean)`
           00045-Sagede_OfficeLine_Rewe_Buchungserfassung_Aufteilungsbuchung.cs:91 - Methode: ...Dispose() id: `M:Sagede.OfficeLine.Rewe.Buchungserfassung.Aufteilungsbuchung.Dispose`
  hint:    Identifikator praezisieren (voll qualifizierter Name oder Datei:Zeile:Spalte).
```

Folgt der Agent dem `hint` und übergibt die vom Tool vorgeschlagene DocComment-ID `M:Sagede.OfficeLine.Rewe.Buchungserfassung.Aufteilungsbuchung.Dispose` an `get_symbol_body`, `find_references`, `get_call_tree` oder `get_class_structure`, bricht der Folgeaufruf ab:

```text
[ERROR]: INVALID_ARGUMENT: Die Assembly-Symbol-ID 'M:Sagede.OfficeLine.Rewe.Buchungserfassung.Aufteilungsbuchung.Dispose' gehört nicht zur aktuellen Assembly-Generation.
  hint:    Eine aktuelle assembly:<sha256>:<generation>:<symbolId>-ID aus dem Assembly-Ziel verwenden.
```

#### 2. Root-Cause-Analyse im Code
1. **Unvollständige ID-Formatierung im Fehlerkontext:**  
   In [`FindReferencesTool.ResolveByNameAsync`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs#L307) wird `FindSymbolTool.FormatSymbolLocations(s, outputRoot)` ohne Übergabe von `AnalysisSymbolIdentity` aufgerufen. Dadurch fehlt der Präfix `assembly:<sha256>:<generation>:`.
2. **Zu strikte Validierung bei bekannter Session:**  
   In [`SymbolIdentifierResolver.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs#L159-L162) wird bei vorhandener `expectedAssemblyIdentity` geprüft:
   ```csharp
   if (expectedAssemblyIdentity is not null && !isAssemblyId)
   {
       return (null, StaleAssemblyId(stableId));
   }
   ```
   Da `stableId` mit `M:` beginnt und nicht mit `assembly:`, wird `isAssemblyId = false` gesetzt und der Call als veraltet (`StaleAssemblyId`) abgewiesen.

#### 3. Konkreter Umsetzungsvorschlag
- **Komponente:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs` und `FindReferencesTool.cs`
- **Fix:**
  1. In `SymbolIdentifierResolver.TryResolveByStableIdAsync`: Wenn `expectedAssemblyIdentity` gesetzt ist und `stableId` ein gültiges DocumentationCommentId-Präfix (`M:`, `T:`, `P:`, `F:`, `E:`) trägt, aber noch keinen `assembly:`-Präfix hat, die ID automatisch mit `expectedAssemblyIdentity` verknüpfen:
     ```csharp
     if (expectedAssemblyIdentity is not null && !isAssemblyId && HasKnownDocumentationCommentIdPrefix(stableId))
     {
         isAssemblyId = true;
         // Direkt als gültige ID für diese Generation behandeln
     }
     ```
  2. In `AssemblySymbolResolver` / `FindReferencesTool.ResolveByNameAsync`: Beim Formatieren von Kandidaten für `AmbiguousSymbol` die aktuelle Assembly-Identität mitgeben, damit im Fehlerkontext direkt die vollqualifizierten IDs (`assembly:<hash>:<gen>:<docId>`) erscheinen (analog zu `find_symbol` und `get_file_skeleton`).

---

### `[F-02]` Inkonsistente Parameter-Kardinalität (Singular vs. Plural)

#### 1. Problembeschreibung & Reproduktion
- Fast alle Symbol-Tools (`find_references`, `get_call_tree`, `get_type_hierarchy`, `get_class_structure`, `get_impact`, `get_test_context`, `get_feature_context`) verwenden den skalaren Parameter:
  `symbolIdentifier: string`
- [`get_symbol_body`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs) erzwingt hingegen ausschließlich ein Array:
  `symbolIdentifiers: string[]`
  Übergibt ein Agent `symbolIdentifier: "MeinSymbol"`, bricht das Tool mit `INVALID_ARGUMENT: Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer` ab.
- [`find_symbol`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs) erzwingt `namePatterns: string[]` statt auch `namePattern: string` zuzulassen.
- **Positivbeispiel:** [`get_file_skeleton`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SkeletonMap/GetFileSkeletonTool.cs) unterstützt vorbildlich sowohl `filePaths: string[]` als auch `filePath: string`.

#### 2. Root-Cause-Analyse im Code
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`: Schema definiert nur `symbolIdentifiers`.
- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`: Schema definiert nur `namePatterns`.

#### 3. Konkreter Umsetzungsvorschlag
- **Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`
  - `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs`
- **Fix:**
  1. In `GetSymbolBodyTool`: Optionalen Parameter `symbolIdentifier: string` im Schema registrieren und im Handler zusammenführen:
     ```csharp
     var targets = (symbolIdentifiers ?? (string.IsNullOrWhiteSpace(symbolIdentifier) ? [] : [symbolIdentifier]))
         .Where(s => !string.IsNullOrWhiteSpace(s))
         .ToArray();
     ```
  2. In `FindSymbolTool`: Optionalen Parameter `namePattern: string` bzw. `symbol: string` registrieren und auf `namePatterns` mappen.

---

### `[F-03]` Fehlender `scopeType`-Filter bei `get_hotspots` (Test-Pollution)

#### 1. Problembeschreibung & Reproduktion
`get_hotspots` identifiziert Dateien, die sich dem `MaxLineCount`-Limit (500 Zeilen) nähern. Während `find_duplicates` (`[F-10]`) und `search_pattern` (`[F-12]`) erfolgreich um `scopeType: 'production' | 'tests' | 'all'` erweitert wurden, fehlt dieser Filter in `get_hotspots`.
- **Folge:** In AiNetLinter belegen Testdateien (`ViolationMarkdownFormatterTests.cs: 500 Zeilen`, `ExternalSourceConfigurationLoaderTests.cs: 499 Zeilen`, etc.) die vorderen Plätze der kritischen Dateien (>=95%). Ein Agent, der Produktionscode vor Überschreiten des Zeilenlimits refaktorisieren will, muss mit Substring-Filtern (`scopeFilter: "src/AiNetLinter/"`) experimentieren.

#### 2. Root-Cause-Analyse im Code
- `src/AiNetLinter/Mcp/Tools/Hotspots/GetHotspotsTool.cs`: Besitzt nur `scopeFilter`, `minLinePercentage`, `maxResults`.
- `src/AiNetLinter/Mcp/Tools/Hotspots/GetHotspotsScanner.cs`: Filtert Dokumente nicht nach Test-/Produktions-Kategorie.

#### 3. Konkreter Umsetzungsvorschlag
- **Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/Hotspots/GetHotspotsTool.cs`
  - `src/AiNetLinter/Mcp/Tools/Hotspots/GetHotspotsScanner.cs`
- **Fix:**
  Einführung des Parameters `scopeType: 'production' | 'tests' | 'all'` (Default `'production'`). Beim Durchlaufen der Dokumente Test-Projekte (über `ProjectKindFilter` bzw. Naming-Heuristik `.Tests`) gemäß `scopeType` ein- oder ausschließen.

---

### `[F-04]` Redundante `targetType="assembly"`-Pflicht bei reinen Assembly-Tools

#### 1. Problembeschreibung & Reproduktion
`inspect_assembly` und `find_assembly_extensions` sind semantisch reine Assembly-Tools (`targetType='project'` wird ausdrücklich abgewiesen). Dennoch verlangt das MCP-Schema zwingend `targetType="assembly"`. Fehlt der Parameter, schlägt der Aufruf auf Schema-Ebene fehl.

#### 2. Root-Cause-Analyse im Code
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
Beide Tools deklarieren `targetType` in den `required`-Properties des Schemas.

#### 3. Konkreter Umsetzungsvorschlag
- `targetType` in reinen Assembly-Tools als optional markieren mit Default `"assembly"`.
- Falls `targetType` nicht angegeben ist oder `targetPath` auf eine `.dll` / `.exe` zeigt, automatisch `"assembly"` annehmen.

---

### `[F-05]` Payload-Bloat bei `pattern_detect` ohne Treffer

#### 1. Problembeschreibung & Reproduktion
Erzielt kein Pattern Treffer (0 Verstöße im Scope), rendert `pattern_detect` dennoch für alle 6 Patterns je eine H2-Überschrift, die vollständige Beschreibung und "Keine.":
```text
Pattern-Detect: 0 von 6 Patterns mit Treffern, 0 Treffer gesamt in 903 Dateien im Scope
## god-class — Klassen mit zu grossem AI-Context-Footprint, zu vielen Public-Members... (0 Treffer)
Keine.
## async-void — async void Methoden oder Local Functions... (0 Treffer)
Keine.
... (weitere 4 Abschnitte)
```
Dies verschwendet unnötig Token im LLM-Kontextfenster.

#### 2. Root-Cause-Analyse im Code
- `src/AiNetLinter/Mcp/Tools/PatternMatching/PatternDetectTool.cs`
Der Formatter iteriert ausnahmslos über alle abgefragten Patterns und schreibt stets den vollständigen Beschreibungsblock.

#### 3. Konkreter Umsetzungsvorschlag
- Wenn `totalMatches == 0`, eine kompakte Bestätigung wie bei `get_violations` ausgeben:
  ```text
  Pattern-Detect: 0 von 6 Patterns mit Treffern in 903 Dateien im Scope.
  Keine Auffälligkeiten gefunden.
  ```
  Die ausführlichen Erläuterungen nur anzeigen, wenn tatsächlich Treffer vorliegen oder gezielt ein einzelnes Pattern abgefragt wurde.

---

### `[F-06]` Starre Dateityp-Aufschlüsselung in `get_index_scope`

#### 1. Problembeschreibung & Reproduktion
`get_index_scope` gibt hardcodiert immer dieselben 6 Extensionen aus:
```text
.cs: 903 Dateien (voll vom Symbolgraph abgedeckt)
.css: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.html: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.js: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.razor: 0 Dateien (nicht vom Symbolgraph abgedeckt)
.xaml: 0 Dateien (nicht vom Symbolgraph abgedeckt)
```
In Nicht-Web- und Backend-Repositories sind 5 davon immer 0. Vorhandene Nicht-C#-Dateien (`.json: 6`, `.md: 18`, `.props: 4`, `.ps1: 2`, `.slnx: 1`) werden hingegen verschwiegen.

#### 2. Root-Cause-Analyse im Code
- `src/AiNetLinter/Mcp/Tools/IndexScope/GetIndexScopeTool.cs`
Der Scanner filtert fest auf die Liste `[".css", ".html", ".js", ".razor", ".xaml"]`.

#### 3. Konkreter Umsetzungsvorschlag
- Dynamische Aggregation: Statt fixer Null-Einträge die tatsächlich im Projektverzeichnis vorkommenden Dateiendungen zählen und auflisten (z. B. `.json`, `.md`, `.props`, `.yaml`).
- Null-Einträge für nicht vorhandene Extensionen unterdrücken.
