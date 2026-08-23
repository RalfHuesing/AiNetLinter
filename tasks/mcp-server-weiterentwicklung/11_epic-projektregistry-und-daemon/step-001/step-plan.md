---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 001               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist
title: "Projektregistry-Grundlage: Definitionsdatei, Loader, Fehlerverträge, Config-Materialisierung"
epic: EPIC-A          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet
estimated_risk: medium  # neue Subsystem-Klasse(n) ohne Wiring; Batch-Pipeline wird angefasst (Delegation), Verhalten aber gepinnt
step_type: single  # single (Default) | batch — zusammenhängender Grundlagenblock, keine trivialen Einzel-Fixes
items: []  # nur bei step_type: batch
created_by: planer  # planer | orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
created_at: 2026-08-23T13:13:00+02:00
related_to: []  # Pointer auf andere step-NNN — noch keine existierenden Steps im Task
---

# Step 001: Projektregistry-Grundlage: Definitionsdatei, Loader, Fehlerverträge, Config-Materialisierung

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-A` aus `roadmap.md` — erstes Epic, komplett offen (Roadmap-Modus frisch
  abgeschlossen, noch kein Step umgesetzt). Dieser Step legt das Fundament: die
  transportunabhängige Definitionsdatei-Ebene (Konzept A.2), ihre harten Fehlerverträge
  (A.5) und die gemeinsame Config-Materialisierung (A.4, Review 3). Registry (Lease/Eviction)
  und Tool-Wiring folgen in eigenen Steps.
- **Konzept-Referenz:** `Konzept.md` — A.2 (Definitionsdatei `ainetlinter.project.json`),
  A.5 (Fehlerverträge inkl. Template-Block), A.4 (Klassenbaum `Mcp/Projects/`, Review 3:
  Config-Materialisierung als gemeinsamer Helper), F8 (Auto-Suche bleibt Batch-only),
  Abschnitt „Innerhalb jedes Epics: Contract-Tests zuerst …“.

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert am 2026-08-23 gegen HEAD über die AiNetLinter-MCP-Tools
(`get_file_skeleton`/`find_symbol`), nicht angenommen:

- **`src/AiNetLinter/Commands/McpServerCommand.cs`** (statisch): enthält heute DIE eine
  Serverinstanz-Erzeugung UND die Config-Pipeline (`ResolveConfig`, `ResolveMaxLineCount`,
  `TryResolveRulesJsonPath`) sowie die Solution-Auto-Suche (`FindSolutionCandidates`,
  `ReportAmbiguousSolution`, `ResolveSolutionPathOrError`). Genau diese Pipeline ist der
  Review-3-Umzugskandidat; die Auto-Suche bleibt unberührt und stirbt im MCP-Pfad erst mit
  dem späteren Wiring-Step (F8) — dieser Step fasst sie NICHT an.
- **`src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`**: Options-Record mit `required`
  Feldern (`Catalog`, `Console`, `Config`) plus `LoadFunc` und einer bestehenden
  `From(McpCodeGraphServerOptionsFromParameters)`-Fabrikmethode — ProjectInstanceFactory
  materialisiert genau diesen Record aus einer Definition, es wird KEIN zweites
  Options-Muster neu erfunden.
- **`src/AiNetLinter/Mcp/McpToolResults.cs`**: bestehender Result-Builder
  (`Error(code, message, context, hint)`, `Recoverable(...)`, `Loading()`) — die neuen
  A.5-Fehlerverträge laufen am Tool-Endpunkt über dieses Muster; der Loader selbst gibt
  strukturierte Ergebnisse zurück (Result-Pattern, Richtlinien §5), NICHT Exceptions für
  erwartbare Fehler.
- **`src/AiNetLinter/Configuration/ConfigLoader.cs`**: `TryLoadConfig(configPath,
  isRequired)` — bestehende Lade-Pipeline, die die Factory je Definition aufruft.
- **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`**: instanzbasiert (F1), Konstruktor nimmt
  ausschließlich `McpCodeGraphServerOptions` — für diesen Step nur Lesekontext, keine Änderung.
- **`src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`** existiert; für DIESEN
  Step nicht nötig (Loader/Factory sind pure File-I/O + Options-Bau, kein Server nötig).
- **`src/AiNetLinter/Mcp/Projects/` existiert noch nicht** — verbindliche Zielstruktur steht
  im Konzept-Strukturbaum und wird hier teilweise angelegt.
- Einfluss auf den Plan: Die Existenz von `McpCodeGraphServerOptions.From(...)` und
  `McpToolResults.Error(...)` bedeutet, dass dieser Step rein additive neue Klassen plus
  EINEN Delegations-Refactor in `McpServerCommand` ist. Kein Umbau bestehender Verträge.

## Intention

Nach diesem Step existiert die komplette Definitionsdatei-Ebene des Epics A als getestete
Einheit: `ainetlinter.project.json` wird geladen (beide Pflichtfelder, Anker = Definitionsdatei,
Existenzprüfung beider Ziele, kein Fallback/Raten), alle vier loader-seitigen Fehlerverträge
aus A.5 antworten deterministisch mit dem vorgeschriebenen Textaufbau (inkl. kopierfähigem
Template bei `PROJECT_NOT_INITIALIZED`), und die Config-Materialisierung läuft über EINE
gemeinsame Helper-Klasse, die bereits das Batch-Kommando nutzt — damit entsteht null
Duplizierung zwischen Batch- und Registry-Pfad (Review 3), bevor spätere Steps darauf aufbauen.
Contract-Tests zuerst: Der Unit-Testkatalog fixiert die Verträge, bevor/while die Implementierung
entsteht.

## Konkrete Änderungen

Reihenfolge (Contract-Tests zuerst): 1) Testprojekt-Ordner `FastTests/Mcp/Projects/` +
Testkatalog rot anlegen, 2) Produktionsklassen grün machen, 3) Batch-Delegation umstellen,
4) Gates.

### Datei 1: `src/AiNetLinter/Mcp/Projects/ProjectDefinition.cs` (NEU)

- **Was:** `internal sealed record ProjectDefinition(string SolutionPath, string RulesPath)`
  gemäß Konzept-Strukturbaum — beide Pfade ABSOLUT und existenzgeprüft (das garantiert der
  Loader, nicht der Record selbst). Keine weiteren Felder in v1 („Kein `$schema`-Feld, keine
  optionalen Felder“).
- **Warum:** Konzept A.2/A.4 — unveränderlicher Werttyp als Vertrag zwischen Loader, Factory
  und (später) Registry.

### Datei 2: `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` (NEU)

- **Was:** `internal static` Loader (Namenskonvention Bestand: `internal sealed`/static,
  file-scoped namespace `AiNetLinter.Mcp.Projects`, `#nullable enable`). Vertrag:
  - Liest `<root>/ainetlinter.project.json`; fehlt die Datei (oder das Root-Verzeichnis) →
    Ergebnis `PROJECT_NOT_INITIALIZED` mit dem EXAKT vorgeschriebenen Template-Block aus
    Konzept A.5 (Aufbau inkl. `Create <root>/ainetlinter.project.json with:` … `Then retry
    the call with the same projectRoot.` — wörtlich, damit Agenten ihn kopieren können).
  - Beide Felder `solution` und `rules` sind Pflicht; fehlt eins oder ist das JSON defekt →
    `PROJECT_DEFINITION_INVALID` mit betroffenem Feldnamen + Definitionsdatei-Pfad; KEINE
    Teil-Initialisierung (entweder vollständige Definition oder Fehlerergebnis).
  - Relative Pfade werden relativ zur DEFINITIONSDATEI aufgelöst, nie zum cwd (Ankerregel
    A.2); absolute Pfade bleiben unverändert.
  - Nach Auflösung Existenzprüfung beider Zieldateien: Solution fehlt → `SOLUTION_NOT_FOUND`
    (mit aufgelöstem absolutem Pfad, Anker genannt); Rules fehlt → `RULES_NOT_FOUND`
    (absoluter Pfad, KEIN Default, KEINE Nachbar-Suche — `TryResolveRulesJsonPath` wird vom
    Loader nie berührt, F8).
  - Rückgabe als strukturiertes Ergebnis (Result-Pattern): entweder `ProjectDefinition`
    oder (ErrorCode, Nachricht) — exakte Form (eigenes kleines Result-Record vs. out-Paar)
    liegt beim Coder; erwartbare Fehler werfen KEINE Exceptions. Unbekannte JSON-Felder
    werden ignoriert (System.Text.Json-Default; v1 definiert keinen strikten Vertrag dafür).
  - Parsen mit `System.Text.Json` (BCL-only, kein neues NuGet — Konzept
    „Abhängigkeiten“).
- **Warum:** Konzept A.2/A.5 — deterministische, selbstheilende Fehler statt stiller
  Fehl-Bindung; das ist die Kern-Wiederöffnungs-Begründung des ganzen Epics.

### Datei 3: `src/AiNetLinter/Mcp/Projects/ProjectErrorCodes.cs` (NEU)

- **Was:** `internal static class ProjectErrorCodes` mit `const string` für ALLE SECHS
  A.5-Codes (`PROJECT_ROOT_REQUIRED`, `PROJECT_ROOT_INVALID`, `PROJECT_NOT_INITIALIZED`,
  `PROJECT_DEFINITION_INVALID`, `SOLUTION_NOT_FOUND`, `RULES_NOT_FOUND`) — Magic-Value-Prävention
  (Richtlinien §5). Nur die vier loader-seitigen Codes werden in diesem Step aktiv genutzt;
  die beiden projectRoot-Codes werden erst mit dem Wiring-Step (späterer Step) ausgegeben,
  stehen aber ab jetzt als einzige Quelle bereit.
- **Warum:** Eine Quelle für die Vertragscodes, bevor mehrere Klassen (Loader, Registry,
  Registrations) sie referenzieren.

### Datei 4: `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` (NEU)

- **Was:** `internal static` (oder minimal-instanziierbare, schlank wegen F7) Fabrik:
  materialisiert aus einer `ProjectDefinition` ein `McpCodeGraphServerOptions` über die
  BESTEHENDE `McpCodeGraphServerOptions.From(...)`-Pipeline — rules laden via
  `ConfigLoader.TryLoadConfig(rulesPath, isRequired: true)`, MaxLineCount-/Metrics-Defaults
  wie heute, `ResolvedConfigPath` = der aus der Definition aufgelöste rules-Pfad,
  `UsedDefaultConfig` = false (im Registry-Pfad bedeutungslos, A.2). Die Existenzprüfung der
  rules BLEIBT IM LOADER (`RULES_NOT_FOUND`) — die Factory lädt nur (Review 3).
- **Warum:** Review 3: „null Duplizierung, identische Semantik“ zwischen Batch und Registry —
  deshalb wandert der Kern JETZT, nicht erst beim Wiring.

### Datei 5: `src/AiNetLinter/Commands/McpServerCommand.cs` (ÄNDERN, minimal)

- **Was:** `ResolveConfig`/`ResolveMaxLineCount` delegieren den geteilten Kern (rules laden +
  Defaults) an `ProjectInstanceFactory`. Das BATCH-Verhalten bleibt byte-identisch: die
  Pfadauflösung inkl. Nachbar-Fallback `TryResolveRulesJsonPath` und Auto-Suche bleibt
  vollständig in `McpServerCommand` (Batch-only, F8) und reicht lediglich den bereits
  aufgelösten Pfad an die gemeinsame Materialisierung weiter. Keine Signaturänderungen nach
  außen, keine Entfernung von Methoden in diesem Step (der harte Cut kommt mit dem Wiring).
- **Warum:** Ohne Delegation würde dieser Step zwei parallele Pipelines erzeugen — genau die
  Duplizierung, die Review 3 und Richtlinien §5 (DRY) verbieten; die bestehenden
  Command-/Config-Tests pinnen das unveränderte Verhalten.

### Datei 6–7: Tests (NEU)

- **Was:** `src/AiNetLinter.FastTests/Mcp/Projects/ProjectDefinitionLoaderTests.cs` und
  `.../ProjectInstanceFactoryTests.cs` — xUnit v3, `[Trait("Category", "Unit")]`, Fixtures
  über `AiNetLinter.TestKit.TestTempDirectory` (NIEMALS OS-Temp, Richtlinien §4). Kein
  cwd-Umbauen im Test (parallelitätsgefährlich) — die Ankerregel wird bewiesen, indem die
  erwarteten absoluten Pfade gegen die Lage DER DEFINITIONSDATEI berechnet werden, nicht
  gegen das cwd.
- **Warum:** Contract-Tests zuerst (Konzept-Vorgabe); der A.8-Teilkatalog für Loader/Verträge.

## Tests

Entwicklung iterativ mit `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
(bei Bedarf zusätzlich gezielt per `FullyQualifiedName~Projects` eingrenzen). Der komplette
Nicht-Stress-Stack läuft GENAU EINMAL als Abschluss-Gate (siehe DoD).

- [ ] Loader: beide Pflichtfelder vorhanden → `ProjectDefinition` mit absoluten Pfaden
      (relative Eingaben relativ zur Definitionsdatei aufgelöst, Ankerregel A.2)
- [ ] Loader: fehlendes Feld `solution` bzw. `rules` → `PROJECT_DEFINITION_INVALID` mit
      betroffenem Feld + Definitionsdatei-Pfad; defektes JSON → derselbe Code; in beiden
      Fällen KEINE Teil-Initialisierung
- [ ] Loader: Definitionsdatei fehlt → `PROJECT_NOT_INITIALIZED`; Nachricht enthält den
      vorgeschriebenen Template-Block WÖRTLICH (Text-Assertion auf den exakten Aufbau inkl.
      „Then retry the call with the same projectRoot.“ — Self-Service-Vertrag A.6/A.8)
- [ ] Loader: Solution-Zieldatei fehlt → `SOLUTION_NOT_FOUND` mit aufgelöst absolutem Pfad;
      Rules-Zieldatei fehlt → `RULES_NOT_FOUND` mit absolutem Pfad
- [ ] Kein-Fallback-Vertrag (A.8): benachbarte `rules.json` existiert im Fixture-Baum und
      wird TROTZDEM ignoriert, wenn der Definitionsdatei-Eintrag fehlt/nicht existiert —
      keine Auto-Suche im MCP-Pfad (F8)
- [ ] Absolute Pfade in der Definitionsdatei bleiben unverändert übernommen
- [ ] Factory: erzeugt `McpCodeGraphServerOptions` aus einer Definition — Config aus dem
      Definition-rules-Pfad geladen, MaxLineCount-/Defaults identisch zur bisherigen
      Pipeline, `ResolvedConfigPath` = aufgelöster rules-Pfad
- [ ] Batch unverändert: bestehende `McpServerCommand`-Config-Pipeline-Tests bleiben
      UNGEÄNDERT grün (pinnt die Delegation ohne Verhaltensänderung)

## Definition of Done

- [ ] Alle „Konkrete Änderungen“ umgesetzt (Contract-Tests zuerst geschrieben)
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build`) fehler- UND warnungsfrei
      (TreatWarningsAsErrors)
- [ ] Abschluss-Gate EINMALIG grün: `dotnet test src/AiNetLinter.FastTests --filter
      Category!=Stress` UND `dotnet test src/AiNetLinter.IntegrationTests --filter
      Category!=Stress` (Iteration davor gefiltert `Category=Unit`)
- [ ] Quality-Gates VOR jedem Commit über MCP: `get_violations` (Solution-scope) und
      `safeguard` für die neuen Klassen; `metrics_lookup` für LOC/Komplexität/Footprint der
      neuen Typen (Grenzwerte AiNetLinter.mdc: ≤500 Zeilen/Datei, Methoden ≤60, ≤4 Parameter,
      sealed, `#nullable enable`, Namespace=Verzeichnis `AiNetLinter.Mcp.Projects`)
- [ ] AiNetLinter-MCP-Tools (`find_symbol`, `get_file_skeleton`, `find_references`,
      `get_impact`) statt grep/Volltext-Lesen für alle C#-Recherchen im Step genutzt;
      rg/grep nur für Nicht-C#-Dateien
- [ ] Kein neuer NuGet-Verweis (BCL-only: System.Text.Json); FastTests referenzieren KEIN
      `System.Diagnostics.Process` (Guard-Test)
- [ ] Kein Task-/Step-ID-Bezug in Kommentaren (Richtlinien §5)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ, mit
      `### Commit-Vorschlag`-Block)
- [ ] `step-001/step-result.md` geschrieben (inkl. codemap-Pflege des Coders vor Doku-Commit)
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Grenzwerte (Produktion)` — Limits für die neuen Klassen
  (MaxLineCount 500, MaxMethodLineCount 60, Parameter ≤4 → Input-Record, CC/CogC,
  EnforceNamespaceDirectoryMapping: `AiNetLinter.Mcp.Projects` unter
  `src/AiNetLinter/Mcp/Projects/`)
- `.agents/rules/AiNetLinter.mdc#agent-resilience` — kein leerer catch (File-I/O im Loader),
  kein Blocking (.Wait/.Result) falls LoadFunc berührt wird
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — TestTempDirectory-Pflicht,
  xUnit v3, keine Serialisierungs-Collection, TRX-Diagnose bei rotem Gate
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Result-Pattern
  statt Exceptions für erwartbare Fehler, Zero-Warning, DRY/Magic-Values (→ ProjectErrorCodes),
  Kommentar-Sparsamkeit
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` — MCP-Dogfooding-Pflicht,
  Doku-Objektivität (nur Implementiertes dokumentieren)

## Bekannte Ausnahmen

- Keine bekannten flaky Tests für diesen Bereich. Hinweis (keine Ausnahme): Die beiden
  projectRoot-Fehlercodes werden in diesem Step definiert, aber erst im späteren Wiring-Step
  ausgeführt und dort contract-getestet (Konzept A.8 „uniforme Pflicht“ gehört zum
  Schema-Vertrag des tools/list).

## Notes

- **Doku-Bewusstsein:** `Docs/agent-api.md` (Referenzabschnitt „ainetlinter.project.json“)
  wird BEWUSST NICHT in diesem Step beschrieben — der Vertrag ist erst mit dem Wiring aus
  agentensicht erreichbar; vorab-Doku verstößt gegen Doku-Objektivität (Richtlinien §1:
  „Nur Implementiertes dokumentieren“). Die Doku-Pflicht wird im fachlich berührenden
  Wiring-/DoD-Step verankert (steht bereits so in roadmap EPIC-A.x).
- **Bestehende Strukturen bewusst WIEDERVERWENDET statt dupliziert:**
  `McpCodeGraphServerOptions.From(Parameters)` (Options-Muster F7), `ConfigLoader.TryLoadConfig`,
  `McpToolResults.Error/Recoverable` (am späteren Tool-Rand), `TestTempDirectory`.
- **Stolperfallen:** (1) Der Loader darf NIEMALS `Directory.GetCurrentDirectory()` oder
  Environment-Hilfen für Pfadauflösung nutzen — Anker ist ausschließlich der Definitionsdatei-Ort
  (sonst Nichtdeterminismus, A.2/Self-Audit 3). (2) `TryResolveRulesJsonPath` und Auto-Suche
  in diesem Step NICHT entfernen — sie sind Batch-vertraglich gepinnt und sterben erst im
  MCP-Wiring (F8). (3) Neue Dateien liegen in einem NEUEN Unterverzeichnis — kein
  `MaxDirectoryChildren`-Risiko; `Core/` hat 30 Dateien (Limit erreicht), dort nichts anlegen.
  (4) JSON-Deserialisierung tolerant gegenüber BOM/Whitespace, strikt bei fehlenden Pflicht-
  feldern; Unbekannte Felder ignorieren (v1-Entscheidung, oben begründet). (5) Root-Verzeichnis
  existiert nicht → ebenfalls `PROJECT_NOT_INITIALIZED` mit erwartetem Pfad (deterministisch,
  keine eigene Root-Validierung — die kommt mit dem Registry/Wiring-Schritt als
  `PROJECT_ROOT_INVALID` auf Argumentebene).
- **Kein drift-audit in diesem Step** (nutzervorgegeben: einmal pro Epic vor Epic-Abschluss).
- Codemap: Coder trägt die neuen Klassen nach `codemap.md` ein (Pflege-Pflicht vor Doku-Commit);
  der Planer hat bereits `McpToolResults.cs` und `McpCodeGraphServerOptions.cs` ergänzt.
