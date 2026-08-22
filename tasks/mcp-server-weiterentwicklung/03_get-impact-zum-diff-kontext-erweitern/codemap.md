---
task: 03_get-impact-zum-diff-kontext-erweitern
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-22
---

# CodeMap: 03_get-impact-zum-diff-kontext-erweitern

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(drift-loop `spec.md` §6). Zwischen einem Coder-Update und dem nächsten
Lesezugriff kann sich am Code strukturell nichts geändert haben, was hier
nicht auch eingetragen wurde. Die Map ist also, solange sie gepflegt wird,
tatsächlich aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im
Step-Modus des Planers („tatsächlichen Projektzustand lesen",
`spec.md` §7.2) bleibt trotzdem Pflicht** — die Map sagt *wo* nachschauen,
ersetzt nie das Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (planer-SKILL Roadmap-Modus Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (coder-SKILL Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. ein späterer Step würde zurückdrehen, was ein
früherer Schritt laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

Initialbefüllung aus dem Grobüberblick des Roadmap-Modus; noch keine
Steps abgeschlossen, daher überall „(zuletzt: roadmap)".

Produktionscode:

- **`src/AiNetLinter/Core/DiffImpactAnalyzer.cs`** — Git-Diff-Auswertung
  (`RunGitDiff`, `ParseGitDiffHunks`) plus public/internal-Symbolfilter
  (`IsPublicOrInternal`, `GetValidChangedSymbol`) bis zu den Call-Sites;
  Umbauziel für das `DiffImpactAnalysis`-Ergebnisobjekt und den zweiten
  Scannerpfad mit breitem Symbolscope (EPIC-2). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs`** —
  BFS-Aufrufer-Traversierung für `find_references` und den
  `get_impact`-Symbol-Branch; `EnqueueChildren` enqueued aktuell
  `reference.Definition` statt des einschließenden Aufrufers — Fixstelle
  der depth>1-Korrektur (EPIC-1). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`** —
  `get_impact`-Dispatch zwischen Git- und Symbol-Branch inkl.
  `GetImpactInput`-Record (bisher 4 Parameter); Hauptort des neuen
  `detailLevel=change-context`-Vertrags, der Antwortform und der
  Parameter-/Validierungsregeln (EPIC-1 Hint-Parität, EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`** —
  Referenz-Tool mit `ResolveSymbolAsync` und angehängtem Sufficiency-Hint;
  Paritäts-Vorbild für den `GetImpactTool`-Symbol-Branch (EPIC-1).
  (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphModels.cs`** —
  strukturiertes `ReferenceTraversalResult` (callSites + completeness) aus
  der transitive-Ausgaben-Aufgabe; Wiederverwendungsquelle für die
  Call-Sites in `change-context` (EPIC-2/EPIC-6). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs`** —
  Formatter derselben strukturierten Traversal-Antwort; Muster für die
  kompakte Textzusammenfassung von `change-context` (EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter/Core/TestCoverageScanner.cs`** — statische
  Test-Zuordnung als per-Symbol-API (scannt je Aufruf alle Testprojekte);
  Refactoring-Ziel für die gebatchte Zuordnung gegen alle gekappten
  Symbole (EPIC-4). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/TestContext/`** — `get_test_context`-Tool
  inkl. Ermittlung direkt ausführbarer `dotnet test`-Filterbefehle;
  Formatvorlage für `recommendedTestCommands` (EPIC-4). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** —
  Violations-Ermittlung (solutionweit/scoped) für `get_violations`; Basis
  für „Linter genau einmal" plus diffbezogene Filterung auf Hunks/
  Symbolspannen (EPIC-5). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — Antwort-Helper (`Text<T>`
  mit structuredContent, `Recoverable`, `InvalidArgument`); Formatkanal
  für die strukturierte `change-context`-Antwort (EPIC-6). (zuletzt:
  roadmap)
- **`src/AiNetLinter/Mcp/McpSufficiencyHints.cs`** — Sufficiency-Hint-
  Logik (Vollständigkeits-Marker am Textende); anzuhängen im
  Symbol-Branch von `GetImpactTool` (EPIC-1). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/McpTruncation.cs`** — einheitliche Meta-Zeilen
  und Listen-Trunkierung; relevant für Caps und „höchstens gekappte
  Top-Einträge" im Text (EPIC-6). (zuletzt: roadmap)
- **`src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`** — Registrierung
  der Symbolgraph-Tools inkl. `get_impact` (Z.111 ff.) — Kontrollstelle für
  die DoD-Regel „kein neues MCP-Tool wurde registriert"; neuer Vertrag
  läuft über den bestehenden Eintrag (EPIC-6). (zuletzt: roadmap)

Doku:

- **`Docs/agent-api.md`** — Tool-Referenz der Agent-API (u. a.
  `get_impact`/`find_references`-Verträge, Structured-Output-Schemata,
  Trunkierungs-Format); Doku-Ziel dieses Tasks: JSON-Feldnamen exakt,
  Verhaltenskorrektur depth>1 ausweisen, Grenzen dokumentieren (EPIC-7).
  (zuletzt: roadmap)
- **`README.md`** — MCP-Tool-Tabelle mit Zeile zu `get_impact`; bei
  Vertragsänderung mitzupflegen (EPIC-7). (zuletzt: roadmap)

Tests:

- **`src/AiNetLinter.FastTests/Mcp/Tools/CallTree/CallGraphTraversalTests.cs`**
  — Unit-Tests der Traversierung, enthält die `ExpandAsync_Depth2_*`-Tests,
  die ggf. das defekte Altverhalten kodieren — bewusst reviewen/umstellen,
  nicht mechanisch grün zwingen (EPIC-1, Audit B/F). (zuletzt: roadmap)
- **`src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs`**
  — Unit-Tests des `get_impact`-Dispatchs/der Antwortform; erweitert um
  `change-context`-Vertrag und `INVALID_ARGUMENT`-Fälle (EPIC-6).
  (zuletzt: roadmap)
- **`src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerTests.cs`** —
  Unit-Tests zu Hunks/Symbolermittlung; erweitert um Ergebnisobjekt und
  breiten Scannerpfad (EPIC-2). (zuletzt: roadmap)
- **`src/AiNetLinter.FastTests/Core/TestCoverageScannerTests.cs`** —
  Unit-Tests der per-Symbol-Testzuordnung; erweitert um Batch-Zuordnung
  (EPIC-4). (zuletzt: roadmap)
- **`src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`**
  — Integrationstests von `get_impact` im echten Server-Kontext; Zielort
  der Konzept-End-to-End-Fälle (Fixture aus EPIC-3). (zuletzt: roadmap)
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandGetImpactTests.cs`**
  — Subprozess-/Protokoll-Level-Tests von `get_impact`; Absicherung der
  Abwärtskompatibilität des `callers`-Modus (EPIC-3/EPIC-6). (zuletzt:
  roadmap)
