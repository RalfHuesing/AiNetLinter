# AiNetLinter MCP-Server — Vollständiger Exploration- & Test-Report

**Datum:** 2026-08-14  
**Testobjekt:** AiNetLinter Stdio-MCP-Server (`AiNetLinter.slnx`, .NET 10)  
**Testumgebung:** Windows x64, Roslyn MSBuildWorkspace resident, 483 `.cs`-Dateien  
**Status:** Alle 18 MCP-Tools + Resource `ainetlinter://overview` erfolgreich live und explorativ getestet.

---

## 1. Executive Summary

Der resident geladene MCP-Server `ainetlinter` funktioniert in der Praxis **extrem performant, stabil und präzise**:
- **Zero-Crash-Garantie eingehalten:** Kein einziger ungültiger oder fehlerhafter Aufruf führte zum Server-Absturz. Alle Fehlerfälle (`INVALID_ARGUMENT`, `RESOURCE_NOT_FOUND`, `SYMBOL_NOT_FOUND`, `CONFIG_NOT_FOUND`) folgen der `IsErrorPolicy.md` (`isError = false` mit strukturiertem Fehlerblock und Handlungsanleitung).
- **Hohe Ausführungsgeschwindigkeit:** Fast alle Abfragen (`get_symbol_body`, `get_file_skeleton`, `get_type_hierarchy`, `dependency_graph`, `find_symbol`, `safeguard`, `metrics_tree`) antworten im Millisekundenbereich, da der Roslyn-Workspace im Speicher resident gehalten wird.
- **Sufficiency-Doctrine:** Vollständige Ergebnisse liefern verlässliche `[HINWEIS]`-Marker, trunkiertes Feedback liefert saubere Meta-Zeilen (`[N Treffer gesamt, M gezeigt ...]`).

Im Zuge der intensiven explorativen Tests und Edge-Case-Analysen wurden jedoch **4 konkrete Mängel/Bugs** identifiziert, die vor der Implementierung weiterer Features (wie `find_magic_values`) behoben werden sollten.

---

## 2. Detaillierte Tool-Testergebnisse (Alle 18 Tools + 1 Resource)

| Nr. | Tool | Getestete Szenarien & Edge Cases | Ergebnis | Bewertung |
|:---|:---|:---|:---|:---:|
| 1 | `get_server_health` | Standardaufruf, State-Snapshot, Uptime-Messung, Refreshes | Liefert sauberen Status (`LoadState: Loaded`, 483 Dateien, Uptime, Config-Pfad). StructuredContent vorhanden. | 🟢 Perfekt |
| 2 | `get_index_scope` | Standardaufruf, Dateityp-Aufschlüsselung | Erkennt 483 `.cs`-Dateien, 0 Web-/XAML-Dateien. StructuredContent vorhanden. | 🟢 Perfekt |
| 3 | `get_hotspots` | Solution-weit, `scopeFilter`, Zeilenlimits | Listet Dateien nahe am `MaxLineCount` (500) tabellarisch mit Prozentwerten. StructuredContent vorhanden. **Pfad-Separator-Bug bei `scopeFilter` mit `/` entdeckt (siehe Befund 1).** | 🟡 Bug bei `/` |
| 4 | `reload_config` | Ohne Parameter (Reload Default), mit `rules.json`, mit nicht-existentem Pfad, mit ungültiger Datei | Reloadet Config im Speicher; ungültiger Pfad liefert sauberes `CONFIG_NOT_FOUND` mit Beibehaltung der alten Config. | 🟢 Perfekt |
| 5 | `metrics_tree` | Alle 4 Modi (`code_size`, `comment_density`, `violation_density`, `complexity`), `root` mit `/`, `fileFilter` Regex, ungültige Regex, ungültiger Mode, fehlender Mode | Funktioniert exzellent. Fehlerhafte Regex und ungültiger/fehlender Mode liefern saubere `INVALID_ARGUMENT`-Meldungen. | 🟢 Perfekt |
| 6 | `find_symbol` | Substring, `kind`-Filter (`Class`, `Method`, etc.), `maxResults`-Trunkierung, Miss-Hint in Nicht-C#-Dateien, 0 Treffer, ungültiger `kind`, fehlendes `namePattern` | Sehr treffsicher. Miss-Hint verweist bei Textfunden vorbildlich auf `search_pattern`. Trunkiert mit standardisierter Meta-Zeile. | 🟢 Perfekt |
| 7 | `get_file_skeleton` | Valide `.cs`-Datei, `id:`-Kommentar-Marker, nicht-existente Datei, Nicht-C#-Datei (`rules.json`), fehlender Parameter | Erzeugt saubere Signatur-Karten mit stabilen `id:`-DocComment-IDs. `RESOURCE_NOT_FOUND` bei fehlenden/Nicht-C#-Dateien. | 🟢 Perfekt |
| 8 | `get_symbol_body` | Stabile `id:` (DocCommentId), `Datei:Zeile:Spalte`, `Datei:Zeile` (Line-only), `maxBodyLines`-Kappung, unbekanntes Symbol, Nicht-C#-Datei, fehlender Parameter | Extrem schneller Body-Abruf. `maxBodyLines` setzt sauberen Ellipsen-Indikator. `SYMBOL_NOT_FOUND` bei unbekannten IDs. | 🟢 Perfekt |
| 9 | `find_references` | `depth=1` (direkt), `depth=2`/`depth=3` (transitiv), `maxResults`-Trunkierung, unbekanntes Symbol, fehlender Parameter | Findet Aufrufstellen exakt. Transitive Suche aggregiert sauber und deklariert `depth` in der Meta-Zeile. | 🟢 Perfekt |
| 10 | `get_call_tree` | `format: "ascii"`, `format: "mermaid"`, `depth`, `topN`-Kappung, unbekanntes Symbol, fehlender Parameter | Caller-Baum wird strukturiert gerendert; Mermaid liefert valides `flowchart TD` Diagramm. | 🟢 Perfekt |
| 11 | `get_type_hierarchy` | Klasse, Interface (`ILintConsole`), unbekannter Typ, fehlender Parameter, falscher Parametername | Zeigt Basisklassen, Interfaces und alle implementierenden Klassen im Repo. | 🟢 Perfekt |
| 12 | `dependency_graph` | `filePath`, `typeIdentifier`, `direction` (`incoming`, `outgoing`, `both`), `depth=1..2`, gegenseitiger Ausschluss beider Parameter | Semantische Abhängigkeitsanalyse funktioniert präzise. **Copy-Paste-Fehler im `hint:`-Text entdeckt (siehe Befund 2).** | 🟡 Hint-Bug |
| 13 | `get_violations` | Solution-weit, `maxResults`-Trunkierung, `scopeFilter` | 0 Violations (Clean Codebase). **Pfad-Separator-Bug bei `scopeFilter` mit `/` (siehe Befund 1).** | 🟡 Bug bei `/` |
| 14 | `safeguard` | `minScore: 0.0` (PASS), `minScore: 10.0` (PASS/FAIL), `maxViolations`, StructuredContent Schema | Score 10.00/10 PASS. JSON-Schema enthält alle erwarteten Felder (`passed`, `score`, `remediation`, etc.). **Pfad-Separator-Bug bei `scopeFilter` mit `/` (siehe Befund 1).** | 🟡 Bug bei `/` |
| 15 | `pattern_detect` | Alle 6 Patterns, Pattern-Subset (`["god-class", "empty-catch"]`), unbekannte Pattern-ID | Gruppiert Audit-Meldungen nach den 6 definierten Patterns. Unbekannte IDs liefern `INVALID_ARGUMENT`. **Pfad-Separator-Bug bei `scopeFilter` mit `/` (siehe Befund 1).** | 🟡 Bug bei `/` |
| 16 | `search_pattern` | Substring-Suche, Regex-Suche (`WarnThreshold\s*=\s*[0-9.]+`), ungültige Regex, fehlender Parameter | Durchsucht alle Dateien blitzschnell. Ungültige Regex liefert `INVALID_ARGUMENT`. | 🟢 Perfekt |
| 17 | `get_impact` | Default (uncommitted Git-Diff), `gitRef: "HEAD"`, `symbolIdentifier`, beide Parameter (Exklusivität), unbekanntes Symbol | Git-Diff- und Symbol-Zweige funktionieren wie spezifiziert. Falsche Parameterkombination liefert sauberes `INVALID_ARGUMENT`. | 🟢 Perfekt |
| 18 | `find_duplicates` | `mode: clone` (fuzzy/near/exact), `mode: refactoring-drift` mit `helperSymbol`, fehlendes `helperSymbol`, ungültiger `mode` | Scannt 2370 Methoden in < 1s. Findet Clone-Cluster und prüft Refactoring-Drift. **Copy-Paste-Fehler im `hint:`-Text entdeckt (siehe Befunde 3 & 4).** | 🟡 Hint-Bugs |
| 19 | Resource `ainetlinter://overview` | Read via `resources/read` | Liefert vollständige Übersicht aller 18 Tools inkl. Solution-/Config-Status. | 🟢 Perfekt |

---

## 3. Identifizierte Probleme & Mängel (Befunde)

### Befund 1 (Kritisch für Windows/Agent-Loops): Fehlende Pfad-Normalisierung bei `scopeFilter`
- **Betroffene Tools:** `get_hotspots`, `get_violations`, `safeguard`, `pattern_detect`
- **Symptom:** Wenn ein Agent einen Pfad mit Forward-Slashes übergibt (z. B. `scopeFilter: "src/AiNetLinter/Mcp"`), meldet das Tool:
  `Keine Dateien im Scope (Filter: 'src/AiNetLinter/Mcp') — Filter pruefen.` bzw. `0 Klassen analysiert`.
  Wird derselbe Pfad mit Windows-Backslashes übergeben (`scopeFilter: "src\AiNetLinter\Mcp"`), werden sofort korrekt 71 Dateien/Klassen gefunden.
- **Ursache:** In `ViolationScopeFilter.cs` / `GetHotspotsScanner.cs` wird ein String-Contains-Check auf den Dateipfad durchgeführt, ohne vorher `/` und `\` zu vereinheitlichen (`Path.Normalize` oder `.Replace('/', Path.DirectorySeparatorChar)`).
- **Empfehlung:** Vor dem String-Vergleich `scopeFilter.Replace('/', '\\')` bzw. Normalisierung auf `Path.DirectorySeparatorChar` durchführen.

---

### Befund 2 (UX / Hint-Fehler): Copy-Paste-Fehler im Hint-Text von `dependency_graph`
- **Betroffenes Tool:** `dependency_graph`
- **Symptom:** Wenn sowohl `filePath` als auch `typeIdentifier` (oder keins von beiden) übergeben wird, lautet die Fehlermeldung:
  ```text
  [ERROR]: INVALID_ARGUMENT: filePath und typeIdentifier sind gegenseitig exklusiv — genau einen angeben, nie beide oder keins.
    hint:    Entweder gitRef ODER symbolIdentifier angeben, nie beide.
  ```
- **Ursache:** Der Hint-Text wurde 1:1 aus `GetImpactTool.cs` kopiert und nicht an `dependency_graph` angepasst.
- **Empfehlung:** In `DependencyGraphTool.cs` den Hint auf `Entweder filePath ODER typeIdentifier angeben, nie beide.` korrigieren.

---

### Befund 3 (UX / Hint-Fehler): Copy-Paste-Fehler im Hint-Text bei fehlendem `helperSymbol` in `find_duplicates`
- **Betroffenes Tool:** `find_duplicates` (`mode: "refactoring-drift"`)
- **Symptom:** Wenn `mode: "refactoring-drift"` ohne `helperSymbol` aufgerufen wird, lautet der Hint:
  ```text
  [ERROR]: INVALID_ARGUMENT: helperSymbol ist bei mode='refactoring-drift' Pflicht ...
    hint:    Entweder gitRef ODER symbolIdentifier angeben, nie beide.
  ```
- **Ursache:** Auch hier wurde der Standard-Hint aus `GetImpactTool.cs` versehentlich übernommen.
- **Empfehlung:** Hint anpassen auf: `helperSymbol als C#-Symbol-Identifikator angeben (z. B. "Klasse.Methode" oder "M:Namespace.Klasse.Methode").`

---

### Befund 4 (UX / Hint-Fehler): Copy-Paste-Fehler im Hint-Text bei ungültigem `mode` in `find_duplicates`
- **Betroffenes Tool:** `find_duplicates`
- **Symptom:** Bei Übergabe eines ungültigen Modus (z. B. `mode: "unsupported"`):
  ```text
  [ERROR]: INVALID_ARGUMENT: Ungueltiger mode-Wert 'unsupported' — gueltig sind 'clone', 'refactoring-drift'.
    hint:    Entweder gitRef ODER symbolIdentifier angeben, nie beide.
  ```
- **Ursache:** Identischer Copy-Paste-Fehler.
- **Empfehlung:** Hint anpassen auf: `mode='clone' oder mode='refactoring-drift' angeben.`

---

## 4. Szenario-Analyse: Was wäre für `find_magic_values` zu tun? (Dogfooding-Nachweis)

Unter Nutzung **ausschließlich der MCP-Tools** wurde analysiert, wie `find_magic_values` (aus `tasks/magic-values-in-mcp/konzept.md`) in die bestehende Architektur integriert werden müsste:

1. **Tool-Registrierung (`find_symbol` & `get_file_skeleton`):**
   - Registrierung in `AnalysisToolRegistrations.cs` (analog zu `get_violations`, `safeguard`, `pattern_detect`).
   - `AnalysisToolRegistrations.Register` nimmt `McpServerPrimitiveCollection<McpServerTool>` entgegen und registriert Tools via `McpServerTool.Create(...)`.
2. **Wiederverwendung von Truncation (`find_references` auf `McpTruncation`):**
   - `McpTruncation.TruncateLines(...)` und `McpTruncationResult.IsTruncated(...)` werden bereits in 8 Tools genutzt. `find_magic_values` kann denselben Standard (`maxResults: 50`) übernehmen.
3. **Wiederverwendung der Git-Diff-Logik (`get_file_skeleton` & `get_symbol_body` auf `GetImpactTool.cs`):**
   - `GetImpactTool.ExecuteGitRefBranchAsync` nutzt `DiffImpactAnalyzer.AnalyzeEntriesAsync(solution, targetPath, input.GitRef, verbose: false)` aus `DiffImpactAnalyzer.cs`.
   - `find_magic_values` kann für `changedOnly: true` direkt auf `DiffImpactAnalyzer.ParseGitDiffHunks` und `FindDocumentByPath` aufsetzen.
4. **Aufdecken von Duplikaten (`search_pattern` & `find_duplicates`):**
   - `search_pattern` fand die im Konzept beschriebene Konstanten-Duplikation `WarnThreshold = 0.80`:
     - `src/AiNetLinter/Maps/HotspotMapBuilder.cs:23`
     - `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs:27`
   - Dies bestätigt exakt die im Konzept formulierte Heuristik für `constant_candidates`.

---

## 5. Fazit & Priorisierte Handlungsempfehlungen

Bevor neue Tools wie `find_magic_values` implementiert werden, sollten folgende Wartungsaufgaben durchgeführt werden:

1. **Priorität 1:** Pfad-Normalisierung (`/` vs `\`) in `ViolationScopeFilter.cs` und `GetHotspotsScanner.cs` einbauen, damit `scopeFilter` mit Linux-/Agent-Forward-Slashes unter Windows transparent funktioniert.
2. **Priorität 2:** Die 3 falschen `hint:`-Texte in `DependencyGraphTool.cs` und `DuplicateDetectionTool.cs` korrigieren (Beseitigung der copy-pasteten `gitRef ODER symbolIdentifier`-Meldungen).
3. **Priorität 3:** Test-Coverage für Forward-Slash-Filterpfade in `src/AiNetLinter.FastTests` ergänzen.
