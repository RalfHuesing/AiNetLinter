---
status: done (pending audit)
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 009
corrects: null
title: "Doku: change-context-Vertrag, Grenzen & Verhaltenskorrektur (agent-api.md, README, ROADMAP)"
epic: EPIC-7
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-23T12:15:00+02:00
related_to:
  - step-008/step-result.md
  - step-001/step-result.md
  - step-003/step-result.md
---

# Step 009: Doku — change-context-Vertrag, Grenzen & Verhaltenskorrektur (agent-api.md, README, ROADMAP)

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-7` aus `roadmap.md` — letztes offenes Epic. Die in
  step-001..008 gebaute Funktionalität (depth>1-Verhaltenskorrektur, breiter
  Diff-Symbolscanner, Batch-Testzuordnung, solutionweite Violations-Stufe,
  change-context-Vertrag) ist vollständig implementiert und approved, aber in
  der Agent-Doku noch nicht sichtbar.
- **Konzept-Referenz:** §StructuredContent („JSON-Feldnamen sind additiv und in
  `Docs/agent-api.md` exakt zu dokumentieren"), §Öffentlicher Vertrag, Audit B
  (Verhaltenskorrektur ausweisen), Audit D.1–D.4/F (Grenzen), DoD
  („Dokumentation nennt die Testdaten korrekt ‚statische Zuordnung'"),
  Update-Pflicht Richtlinien §4 (`Docs/ROADMAP.md`, `README.md`).

## Aktueller Projektzustand (JIT-Kontext)

Gegen den aktuellen Stand verifiziert (Prüfpflicht Richtlinien §1 — gegen
Code/Doku gelesen, nicht gegen Erinnerung):

- **`Docs/agent-api.md`** (732 Zeilen, Stand 2026-08-23):
  - Tool-Tabelle Z. 231 (`get_impact`): kennt nur `gitRef`/`symbolIdentifier`/
    `maxResults`/`depth`; die drei step-008-Parameter (`detailLevel`,
    `maxChangedSymbols`, `maxTestsPerSymbol`) fehlen. Der depth-Klammerzusatz
    („nur Symbol-Branch, Git-Branch ignoriert") ist seit step-008 unpräzise —
    korrekt: wirkungslos im **gesamten** Git-Branch (callers UND
    change-context).
  - **Kein einziger Treffer** für `detailLevel` oder `change-context` in der
    ganzen Datei — der ausgelieferte Vertrag existiert dokumentativ nicht.
  - Structured-Output-Intro Z. 257: „der Git-Diff-Branch von `get_impact`
    behaelt seine bestehende `CallSiteEntry`-Form" — seit step-008 nur noch
    für `detailLevel=callers` wahr.
  - E.2-depth-Abschnitt Z. 612–625: beschreibt `depth > 1` neutral, ohne die
    step-001-Verhaltenskorrektur (Audit B), und endet mit „get_impact
    ignoriert depth im Git-Branch".
  - Statische-Test-Zuordnung-Notiz Z. 253: nennt nur `get_feature_context`/
    `get_test_context`, nicht die neuen `testAssociations` des
    change-context-Modus.
- **`README.md`** Z. 96: get_impact-Zeile ohne jede change-context-Erwähnung.
- **`Docs/ROADMAP.md`** (784 Zeilen): Abschnittsformat `## Titel` +
  Einleitungssatz + `[x]`-Bullet-Cluster, getrennt durch `---`; letzter
  Abschnitt endet Z. 782, GitHub-Footer Z. 784. Kein Eintrag zum
  change-context-Feature.
- **Code-Fakten** (geprüft in `ChangeContextResponseModels.cs`,
  `SymbolGraphToolRegistrations.cs` Z. 105–140, step-008-Result/-Review):
  JSON-Feldnamen entstehen per zentraler CamelCase-Policy 1:1 aus den
  Property-Namen (`mode`, `detailLevel`, `changedFiles[].filePath`/`ranges[]`
  mit `startLine`/`lineCount`, `changedSymbols[].documentationCommentId`/
  `displayName`/`kind`/`accessibility`/`projectName`/`filePath`/`startLine`/
  `endLine`, `callSites` (TransitiveCallSiteEntry-Struktur wie
  `find_references`), `testAssociations[].symbolId`/`filePath`/`testMethods`/
  `matchReason`, `violations[].filePath`/`lineNumber`/`ruleName`/`severity`/
  `details`, `recommendedTestCommands`, `completeness` mit
  `changedSymbolsTotal`/`changedSymbolsShown`/`symbolsTruncated`/
  `callSitesTruncated`/`testsTruncated`); `accessibility` bewusst STRING;
  Vertragskonstanten `"gitDiff"`/`"callers"`/`"change-context"`, Defaults
  20/10, Caps 100/50, Clamp `<1 → Default`, `>Cap → Cap`; Signatur-Argument
  heißt `gitRef` (nicht `gitSinceRef`); case-insensitive detailLevel-
  Validierung, `INVALID_ARGUMENT` mit `get_feature_context`-Hinweis bei
  Kombination mit `symbolIdentifier`; leere, vertragsgültige Struktur bei
  „kein Repo / leerer Diff"; Sufficiency-Hint nur bei vollständigem Ergebnis,
  sonst Trunkierungs-Meta-Zeile.
- **Wiederverwendung statt Neubau:** Die bestehenden Doku-Muster werden
  fortgeschrieben — Detailabschnitte „… — Structured Output im Detail" mit
  JSON-Block (wie `safeguard`/`pattern_detect`/transitive Response), die
  statische-Zuordnung-Notiz Z. 253, das E.2-Muster. Kein neues Dokument, keine
  neue Strukturform.
- **Anti-Loop-Check (CodeMap):** keine Kollision — die Map markiert
  `Docs/agent-api.md`/`README.md` als Doku-Ziele genau dieser Punkte;
  `Docs/ROADMAP.md` fehlte in der Map und wurde beim Abgleich ergänzt.

## Intention

Nach diesem Step ist der ausgelieferte Stand vollständig und objektiv
dokumentiert: Ein Agent kann aus `Docs/agent-api.md` den change-context-
Vertrag zeichenexakt (Feldnamen, Defaults, Caps, Fehlerfälle) und seine
Grenzen entnehmen, versteht die depth>1-Änderung als **Verhaltenskorrektur**
von `find_references`/`get_impact` (nicht als additive Erweiterung), und
README/ROADMAP spiegeln den Feature-Stand. Reine Doku-Änderung — es wird
ausschließlich Implementiertes beschrieben.

## Konkrete Änderungen

### Datei 1: `Docs/agent-api.md`

1. **Tool-Tabelle, Zeile `get_impact` (Z. 231)**
   - **Was:** Input-Spalte um `detailLevel?` (`"callers"` Default |
     `"change-context"`, nur Git-Diff-Modus, nie zusammen mit
     `symbolIdentifier`), `maxChangedSymbols?` (Default 20, Cap 100) und
     `maxTestsPerSymbol?` (Default 10, Cap 50) ergänzen; depth-Klammer auf
     „im gesamten Git-Branch (callers UND change-context) wirkungslos"
     korrigieren; Output-Spalte um das strukturierte change-context-Objekt
     ergänzen (geänderte Dateien/Symbole, Call-Sites, statisch zugeordnete
     Tests, diffbezogene Violations, empfohlene `dotnet test`-Befehle,
     Completeness-Metadaten).
   - **Warum:** Zeile ist seit step-008 faktisch unvollständig/falsch
     (Doku-Objektivität §1).
2. **Statische-Zuordnung-Notiz (Z. 253)**
   - **Was:** Absatz um `get_impact` mit `detailLevel=change-context`
     (`testAssociations`) erweitern — gleiche Grenze, gleiche Formulierung
     „statische Test-Zuordnung".
   - **Warum:** Konzept-DoD „Dokumentation nennt die Testdaten korrekt
     ‚statische Zuordnung'".
3. **Structured-Output-Intro (Z. 257)**
   - **Was:** CallSiteEntry-Satz präzisieren: die bestehende Form gilt für
     `detailLevel=callers` (Default); `detailLevel=change-context` liefert das
     neue Payload-Objekt (Verweis auf den neuen Detailabschnitt).
   - **Warum:** Aussage ist seit step-008 für den change-context-Modus falsch.
4. **NEU: Abschnitt „`get_impact` (`detailLevel=change-context`) — Structured
   Output im Detail"** (Einfügen nach dem transitiven Abschnitt, nach Z. 316,
   vor dem `safeguard`-Abschnitt; Format wie die bestehenden Detailabschnitte)
   - **Was:**
     - JSON-Beispiel mit den EXAKTEN Feldnamen (siehe Code-Skizze; Quelle
       `ChangeContextResponseModels.cs`, CamelCase-Policy; Feldnamen sind
       durch `ChangeContextResponseModelTests` gepinnt).
     - Vertragsregeln: nur im Git-Diff-Modus; mit `symbolIdentifier` bzw. bei
       unbekanntem Wert recoverable `INVALID_ARGUMENT` (Kombinationsfall mit
       Hinweis auf `get_feature_context`); `maxChangedSymbols`/
       `maxTestsPerSymbol` mit Default/Cap und Clamp (`<1 → Default`,
       `>Cap → Cap`); deterministische Symbol-Kappung (Projekt → Datei →
       Startzeile → Symbol-ID) VOR den teuren Call-Site-/Test-/Violation-
       Analysen; `maxResults` kappet nur die Text-Topliste; kompakte
       Textzusammenfassung, Sufficiency-Hint nur bei vollständigem Ergebnis,
       sonst Trunkierungs-Meta-Zeile; „kein Repo / leerer Diff" liefert ein
       leeres, vertragsgültiges Objekt (kein Fehler); `violations` ohne
       Snippets/Source-Ausschnitte; `recommendedTestCommands` dedupliziert,
       ein Befehl je betroffenem Testprojekt.
     - Unterblock **„Dokumentierte Grenzen"** (alle sechs, je 1–3 Sätze):
       a) **Gelöschte Dateien** liefern keine Hunks (`+++ /dev/null` wird
          nicht ausgewertet) und erscheinen nie in `changedSymbols` —
          dokumentierte Grenze, kein Fehlerfall (Audit D.1/F).
       b) **Umbenennungen:** mit Git-Rename-Detection landen Hunks unter dem
          neuen Pfad; ohne Detection erscheinen Löschung + Neuanlage — mit
          denselben Grenzen wie gelöschte Dateien (Audit D.2).
       c) **`depth` ist im gesamten Git-Branch** (callers UND change-context)
          wirkungslos; die Call-Site-Tiefe ergibt sich aus dem Traversal-
          Ergebnis (Audit D.3).
       d) **Stabile ID** = `DocumentationCommentId`, sonst deterministischer
          Fallback; lokale Funktionen erhalten `#lf:`-IDs (Audit D.4,
          step-003).
       e) **Testinformationen sind eine statische Zuordnung** (keine
          Laufzeit-Coverage, keine Coverage-Dateien) — Verweis auf die Notiz
          zu Z. 253.
       f) **Multi-Hunk-Container-Regel:** die innerste Deklaration wird
          dateiweit über alle Hunks entschieden — trifft ein Hunk einen
          Member und ein zweiter Hunk derselben Datei die Deklarationszeile
          des enthaltenen Typs, erscheint nur der Member (step-003).
   - **Warum:** Konzept §StructuredContent („exakt zu dokumentieren"),
     Audit B/D.1–D.4/F, DoD.
5. **Transitiver Abschnitt `find_references`/`get_impact` Symbol-Branch
   (Z. 289–316)**
   - **Was:** Reached-From-Anmerkung ergänzen: `reachedFromSymbolId` von
     lokal-funktionsumgebenen Call-Sites trägt `#lf:`-IDs; der String-Wert
     änderte sich dadurch von (geerbter, mehrdeutiger) Methoden-ID zu
     eindeutig (step-003-Verhaltensanmerkung).
   - **Warum:** Verhalten ist seit step-003 anders, Doku beschreibt noch die
     alte generische Fallback-Formulierung ohne den Sonderfall.
6. **E.2-depth-Abschnitt (Z. 612–625)**
   - **Was:** Die `EnqueueChildren`-Korrektur als **Verhaltenskorrektur**
     ausweisen (Audit B): `depth > 1` liefert seit step-001 echte
     Aufruferketten (`A → B → C`) statt faktisch nur Override-/Interface-
     Expansion — betrifft `find_references` UND den `get_impact`-Symbol-
     Branch und ändert Bestandsausgaben. Schlusswortlaut auf „`depth` ist im
     gesamten Git-Branch (callers und change-context) wirkungslos" erweitern.
   - **Warum:** Audit B verlangt ausdrücklich die Ausweisung als
     Verhaltenskorrektur, nicht nur additive Erweiterung.

### Datei 2: `README.md` (Zeile 96)

- **Was:** MCP-Tool-Zeile zu `get_impact` aktualisieren, z. B.: „Betroffene
  Call-Sites für uncommittete Änderungen oder ein Symbol (Symbol-Branch mit
  derselben transitiven Struktur wie `find_references`); optional
  `detailLevel=change-context` mit geänderten Dateien/Symbolen, Call-Sites,
  statisch zugeordneten Tests, diffbezogenen Violations und empfohlenen
  `dotnet test`-Befehlen".
- **Warum:** Update-Pflicht Richtlinien §4; Zeile ist seit step-008
  unvollständig.

### Datei 3: `Docs/ROADMAP.md` (neuer Abschnitt vor dem Footer, nach Z. 782)

- **Was:** Neuer Abschnitt im bestehenden Format (`## Titel`,
  Einleitungssatz, `[x]`-Bullets), der das abgeschlossene Feature
  zusammenfasst: `detailLevel=change-context` (drei neue Parameter mit
  Default/Cap, strukturierte Antwort, deterministische Symbol-Kappung vor
  Folgeanalysen, Violations-Stufe „Linter genau einmal", gebatchte
  Test-Zuordnung mit `recommendedTestCommands`, depth>1-Verhaltenskorrektur,
  Doku). Sachlich, nur Implementiertes, ohne Task-/Step-/Epic-IDs.
- **Warum:** Update-Pflicht Richtlinien §4. „Nach Task-Abschluss" heißt hier:
  in diesem Step — er ist der letzte des Tasks; danach gibt es keinen Step
  mehr, der den Eintrag nachliefern könnte (danach folgen nur noch Kritiker
  `global` und Task-Summary).

## Tests

Keine neuen Tests — reine Doku-Änderung ohne Codepfad; die Feldnamen sind
bereits durch `ChangeContextResponseModelTests` und die
`GetImpactToolTests`-Vertragstests gepinnt. Stattdessen Doku-Verifikation
(Teil des DoD): jede dokumentierte Behauptung ist gegen Code bzw. step-Results
zu prüfen — Feldnamen gegen `ChangeContextResponseModels.cs`, Validierung/
Fehlerfälle gegen `GetImpactTool`/step-008-Result, Verhaltenskorrektur gegen
`CallGraphTraversal`/step-001-Result, Grenzen gegen step-003/step-008-Results.
Build- und Test-Gates bleiben laut AGENTS.md §2 trotzdem Pflicht.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (agent-api.md Z. 231/253/257/neuer
      Abschnitt/Z. 289–316/Z. 612–625; README.md Z. 96; ROADMAP-Abschnitt)
- [ ] JSON-Beispiel-Feldnamen stimmen zeichenexakt mit der CamelCase-
      Serialisierung der DTOs überein (gegen `ChangeContextResponseModels.cs`
      geprüft)
- [ ] ExpandAsync-Fix ist explizit als **Verhaltenskorrektur** benannt (nicht
      nur additiv); alle sechs Grenzen dokumentiert; „statische Zuordnung"
      durchgehend korrekt benannt
- [ ] Keine Task-/Step-/Epic-IDs im Doku-Text (Richtlinien §5)
- [ ] `dotnet build` grün; beide Nicht-Stress-Testläufe grün (Abschluss-Gate
      AGENTS.md §2)
- [ ] Commit (Orchestrator, `docs: …`) — dabei die uncommittete Fremdänderung
      an `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/Konzept.md`
      NICHT stagen/berühren
- [ ] `step-009/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien — Dokumentations-Objektivität` —
  nur Implementiertes dokumentieren, Prüfpflicht gegen Code (nicht gegen
  Erinnerung/ältere Doku), sachlich ohne Wertung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests — Update-Pflicht` —
  `Docs/ROADMAP.md` + `README.md` mitziehen; `Docs/configuration.md`/
  `rules.json` bleiben unberührt (kein Config-Feld, keine Linter-Regel
  geändert — nur ein MCP-Tool-Parametervertrag).

## Bekannte Ausnahmen

- Keine flaky Tests betroffen. Hinweis: das Integrations-Gate dauert
  (~2 Minuten, Dutzende echte Subprozesse) — bleibt Pflicht, kein Abweichen.

## Code-Skizze (optional)

JSON-Gerüst für den neuen Detailabschnitt (Feldnamen zeichenexakt aus
`ChangeContextResponseModels.cs` / Konzept §StructuredContent):

```json
{
  "mode": "gitDiff",
  "detailLevel": "change-context",
  "changedFiles": [
    { "filePath": "src/App/OrderService.cs", "ranges": [{ "startLine": 40, "lineCount": 8 }] }
  ],
  "changedSymbols": [
    {
      "documentationCommentId": "M:App.OrderService.PlaceAsync",
      "displayName": "OrderService.PlaceAsync",
      "kind": "Method",
      "accessibility": "Public",
      "projectName": "App",
      "filePath": "src/App/OrderService.cs",
      "startLine": 37,
      "endLine": 61
    }
  ],
  "callSites": [],
  "testAssociations": [
    {
      "symbolId": "M:App.OrderService.PlaceAsync",
      "filePath": "tests/App.Tests/OrderServiceTests.cs",
      "testMethods": ["PlaceAsync_ValidOrder_Persists"],
      "matchReason": "…"
    }
  ],
  "violations": [
    { "filePath": "src/App/OrderService.cs", "lineNumber": 44, "ruleName": "…", "severity": "warning", "details": "…" }
  ],
  "recommendedTestCommands": ["dotnet test … --filter …"],
  "completeness": {
    "changedSymbolsTotal": 3,
    "changedSymbolsShown": 3,
    "symbolsTruncated": false,
    "callSitesTruncated": false,
    "testsTruncated": false
  }
}
```

## Notes

- **Fremdänderung:** `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/Konzept.md`
  ist uncommittete Nutzer-Arbeit (anderes Task) — nicht berühren, nicht
  stagen, nicht reverten. Der Doku-Commit darf nur die drei Doku-Dateien
  enthalten.
- **Namensfakten:** Das Tool-Argument heißt `gitRef` (nicht `gitSinceRef`,
  step-008-Abweichung 2) — die Doku dokumentiert die tatsächlichen Namen.
- **matchReason-Werte:** Das Konzept-Beispiel („Direct Member Match /
  Invocation") ist beispielhaft. Die Doku benennt die getrennten Evidenzarten
  (direkte Invocation, Namenskonvention) und verifiziert die Literal-Formen
  gegen `TestCoverageMatchReasons` (Prüfpflicht §1), statt Beispielwerte als
  Vertrag auszugeben.
- **Trunkierungs-Format-Abschnitt (Z. 476 ff.):** prüfen, ob die Vier-Tools-
  Aussage zu `maxResults` für den change-context-Modus (kappet nur die
  Text-Topliste) noch exakt stimmt; nur bei faktischer Abweichung anpassen —
  kein sonstiger Scope-Drift.
- **Nicht anfassen:** Die INVALID_ARGUMENT-Zeile Z. 682 („exklusive Parameter
  verletzt (`get_impact`)") ist bereits korrekt.
- **Optionale Nuance (step-003):** Die Accessibility lokaler Funktionen liest
  sich als „private" (Roslyn-Default-Angabe) — darf als Grenzen-Detailsatz
  aufgenommen werden, ist aber kein Muss des Epics.
- **CodeMap:** `Docs/ROADMAP.md` ist durch den Planer bereits eingetragen;
  der Coder aktualisiert die „zuletzt"-Marker der Doku-Einträge vor dem
  Doku-Commit (coder-SKILL Schritt 6a).
- **Danach:** Nach approved dieses Steps sind alle Epics abgehakt — der
  Planer meldet „keine offenen Epics mehr", der Orchestrator führt Kritiker
  `global` + `task-summary.md` (siehe task-state.md Resume-Notiz).
