---
status: active  # active | done
task: metrics-tree
derived_from: konzept.md
created_at: 2026-08-08
last_updated: 2026-08-08
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: metrics-tree

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build` (Solution `src/AiNetLinter.slnx` bzw.
  Root-`.sln`) — beide Projekte (`AiNetLinter`, `AiNetLinter.Tests`)
  laufen mit `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  (Zero-Warning-Direktive, `AiNetLinterRichtlinien.mdc` §5) — neuer Code
  darf keine Compiler-Warnung einführen.
- **Test-Command — abweichend vom Standard-Coder-Gate, explizite
  User-Vorgabe fuer DIESEN Task (`konzept.md` Abschnitt "Test-Strategie
  waehrend der Umsetzung"):**
  - **Waehrend beider Bloecke (Iteration), vor jedem Zwischen-Commit
    innerhalb eines Blocks:** NUR gezielter/gefilterter Lauf, z. B.
    `dotnet test --filter Category=Unit` (~23-24s) oder eingegrenzt auf
    die neu/veraendert beruehrten Testklassen. **Kein** Volllauf pro
    Step/Commit.
  - **Vor jedem der zwei Block-Commits (Ende Epic 1, Ende Epic 2):**
    ebenfalls nur der gezielte/gefilterte Lauf ist Pflicht.
  - **Genau EIN vollstaendiger Lauf fuer den gesamten Task:**
    `dotnet test --filter Category!=Stress` — erst ganz am Ende, nach
    Block 2, als Abschluss-Verifikation. Erst danach gilt der Task als
    fertig.
  - Tradeoff (vom Nutzer bewusst akzeptiert): eine Regression aus Epic 1
    faellt im ungünstigsten Fall erst beim Abschluss-Volllauf nach Epic 2
    auf — dafuer keine mehrfachen 800s-Wartezeiten waehrend der
    Umsetzung. Coder/Kritiker sollen dieses abweichende Gate bei jedem
    Aufruf aus dieser Roadmap uebernehmen, nicht das Standard-Gate
    (Volllauf vor jedem Commit) anwenden.
  - `Stress`-Kategorie (absichtlich lastintensive/parallele Tests) läuft
    nie automatisch mit — weder im gefilterten Lauf noch im
    Abschluss-Volllauf.
- **Lint-Command:** `dotnet run --project src/AiNetLinter -- --config
  rules.json --path ./src/` (Dogfooding — das Repo lintet sich selbst,
  siehe `AiNetLinterRichtlinien.mdc` §1). Für Agent-Loops bevorzugt: der
  eigene MCP-Server `ainetlinter` (`get_violations`, `find_symbol`,
  `get_hotspots` usw.) statt `dotnet run` oder `rg`.
- **Code-Style-Kurzfassung (aus `AiNetLinter.mdc`):** `sealed` für
  konkrete Klassen; `#nullable enable` je Datei; Methoden ≤60 Zeilen,
  ≤4 Parameter (sonst Parameter-`record`); kein leeres `catch`; kein
  `dynamic`; `out` nur in `Try*`; `MaxLineCount` 500/Datei;
  `MaxCyclomaticComplexity` 12, `MaxCognitiveComplexity` 15;
  `AIContextFootprint` ≤2500 transitive Zeilen pro Typ — bei
  MCP-Tool-Klassen historisch der Grund für die
  Tool/Scanner-Aufsplittung (siehe Regel-Index unten). Sparsame
  Kommentare (Clean Code, kein Task-ID-Bezug im Code).
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ
  (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`). Jede Antwort mit
  Datei-Änderungen schließt mit einem `### Commit-Vorschlag`-Block
  (reiner Commit-Text, keine Shell-Befehle). Projektpräferenz laut
  Memory: verifizierte Änderungen automatisch committen, nicht auf
  Nachfrage warten.

## Regel-Index

- `.agents/rules/AiNetLinter.mdc` — auto-generierte Kurzfassung der
  aktiven Linter-Regeln/Grenzwerte (aus `rules.json`); Ziel-Konformität
  für neuen Produktionscode in diesem Repo selbst.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuell gepflegte
  Architektur-/Workflow-/Kommentar-/Doku-Regeln (Design-Philosophie,
  Windows-Shell-Regeln, Test-Kategorien, Doku-Update-Pflicht,
  Commit-Vorschlag-Pflicht, Kommentar-Sparsamkeit).

## Epics

- [ ] EPIC-01: Datei-Walk-Modi (`code_size`, `comment_density`) +
      gemeinsamer ASCII-Tree-Renderer + Input-Parameter + Tool —
      ein grosser Block: neues Tool `metrics_tree` in
      `FileStructureToolRegistrations.cs`, generalisierter Walk-Kern
      (aus `GetHotspotsScanner.cs` extrahiert/wiederverwendet statt
      zweiter unabhängiger Walk-Implementierung, siehe `konzept.md`
      "Entdeckte Mängel/Redundanzen"), Modi `code_size` (Dateien/LoC/
      Bytes) und `comment_density` (Comment-LoC/Code-LoC-Ratio), Input
      `root`/`mode`/`depth`(1-5)/`top_n`/`file_filter`, ASCII-Tree-Output
      mit Top-N-Sortierung pro Modus, Sufficiency-Hinweis
      (`McpSufficiencyHints`-Pattern), Tool-Registrierung, Tests
      (1 pro Modus + Edge-Cases: leeres Verzeichnis, single File,
      depth=5). Bezug: `konzept.md` Muss-Haben + "Hinweis zur
      Umsetzungsgranularitaet" Block 1.
- [ ] EPIC-02: Roslyn-Modi (`violation_density`, `complexity`) auf
      demselben Renderer — zweiter grosser Block: `violation_density`
      über `LinterEngine` (analog `GetViolationsScanner`-Pattern:
      Severity-Mix + Aggregation pro Hierarchie-Ebene statt flacher
      Liste), `complexity` über `ComplexityCalculator`
      (Ø CC/max CC/max CogC pro Knoten), beide auf dem in EPIC-01
      gebauten ASCII-Tree-Renderer aufsetzend (kein zweiter Renderer),
      Top-N-Sortierung je Modus, Tests (1 pro Modus + Edge-Cases) + 1
      Integrationstest auf dem Live-Repo (Dogfooding). Zusätzlich:
      Doku-Update (`Docs/agent-api.md`, `Docs/ROADMAP.md`, ggf.
      `README.md`) und Epic S2.5 in `tasks/features/05-roadmap.md`
      (Übersichtstabelle Zeile ~103 + Detail-Abschnitt ab Zeile ~211)
      als `[x]` abhaken — gehört inhaltlich zu diesem Block, da erst nach
      Fertigstellung aller 4 Modi sinnvoll dokumentierbar. Bezug:
      `konzept.md` Muss-Haben + "Hinweis zur Umsetzungsgranularitaet"
      Block 2 + Definition of Done.

**Explizite User-Vorgabe zum Zuschnitt (`konzept.md` "Hinweis zur
Umsetzungsgranularitaet"):** genau diese 2 Epics, keine feinere
Aufteilung pro Modus/Datei — Referenzgröße "gross" analog `safeguard`
(S1.2). Jedes Epic bleibt trotzdem vollständig testabgesichert (kein
Abstrich bei Tests, nur bei Step-Anzahl). Innerhalb eines Epics darf der
Planer im Step-Modus dennoch mehrere Steps bilden, falls das Epic real
grösser als ein committbarer Block ist (Edge-Case laut SKILL.md) —
das widerspricht der Vorgabe nicht, solange nicht künstlich pro
Datei/Modus zerlegt wird.
