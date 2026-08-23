---
status: active  # active | done
task: 03_get-impact-zum-diff-kontext-erweitern
derived_from: konzept.md
created_at: 2026-08-22
last_updated: 2026-08-23
created_by_model: ox-alpha
created_by_model_knowledge_cutoff: unbekannt
---

# Roadmap: 03_get-impact-zum-diff-kontext-erweitern

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe
`.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §7.2. Diese Datei
wird laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

Aus dem Projekt abgeleitet (AGENTS.md, `.agents/rules/**`,
`Directory.Build.props`, `.github/workflows/release.yml`):

- **Build-Command:** `dotnet build` — baut die Solution `AiNetLinter.slnx`
  (net10.0) fehler- **und warnungsfrei** (`TreatWarningsAsErrors=true`,
  Zero-Warning-Direktive).
- **Test-Command:**
  - Schnelle Iteration: `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
    (bzw. `Category=Component`, <10 s).
  - Abschluss-Gate pro Step/Task (Pflicht laut AGENTS.md §2): 
    `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND
    `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` —
    beide grün. `Stress` läuft nie automatisch.
- **Lint-Command:** Dogfooding über die eigene Engine:
  `dotnet run --project src/AiNetLinter -- --config rules.json --path ./AiNetLinter.slnx`;
  qualitätsrelevante Zusatzchecks (Duplikate/Metriken geänderter Symbole)
  über die MCP-Tools (`find_duplicates`, `metrics_lookup`) laut
  Richtlinien §5.
- **Code-Style-Kurzfassung** (aus `.agents/rules/AiNetLinter.mdc`):
  `sealed` für konkrete Klassen; Methoden ≤60 Zeilen; ab 5 Parametern
  Input-`record`; `#nullable enable`; kein leeres `catch`, kein `dynamic`,
  `out` nur in `Try*`; Result-Pattern bevorzugt statt Exceptions;
  **keine repo-spezifischen Hardcodings** (Projekt-/Pfadliterale) in
  Engine/MCP-Tools; Kommentare sparsam und ohne Task-/Step-Referenzen.
  Testprojekte/TestKit: `MaxMethodLineCount` 100, `EnforceSealedClasses` aus.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ
  (`feat:`, `fix:`, `docs:`, `perf:` …); Antworten enden mit Pflicht-Block
  `### Commit-Vorschlag` (nur Commit-Text, ohne Shell-Befehl). Der
  Orchestrator committet — Subagenten berühren Git nicht.
- **Umgebung:** Windows; Git stets mit `--no-pager` (Projektkonvention:
  PowerShell 7; die drift-loop-Umgebung führt Befehle hier unter git-bash
  aus — Befehle so formulieren, dass sie in beidem funktionieren).

## Regel-Index

Ein Eintrag pro Datei in `<rules_dir>/**` (`.agents/rules`) — Kurzbeschreibung,
kein Volltext. Zweck: Der Step-Modus-Planer ist pro Aufruf eine frische,
isolierte Session; er wählt aus diesem Index gezielt die 1–2 zum Step
passenden Dateien (drift-loop spec §7.2 / planer-SKILL Schritt 4a). Wird
laufend gepflegt.

- `.agents/rules/AiNetLinter.mdc` — Auto-generierte C#-Codequalitätsregeln
  aus `rules.json`: Grenzwerte-Tabelle (LOC, Methodenlänge, Parameterzahl,
  Komplexität, Footprint), Resilience-/Architektur-/Naming-Regeln,
  Projekt-Overrides für Tests/TestKit — Maßstab für jeden neuen
  Produktionscode (u. a. relevant: `GetImpactInput` wächst als Record,
  Delegat-Signaturen ≤4 Parameter).
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuell gepflegte
  Architektur-/Workflow-Leitplanken: Design-Philosophie (monolithisch,
  kein DI/Plugin, Roslyn-Zugriffe sparsam), Doku-Objektivität (nur
  Implementiertes dokumentieren, verifizieren gegen Code), Windows/pwsh/
  git `--no-pager`, xUnit-v3-Testpflicht ohne Serialisierungs-Collections,
  zentrales `TestTempDirectory`, Zero-Warning, Symptom-Fixing-Verbot,
  DRY/Magic-Values/Dead-Code-Abbau, Kommentar-Disziplin (kein Task-ID-
  Referenzen), Commit-Vorschlag-Pflicht.

## Epics

Reihenfolge = grobe Abhängigkeit (EPIC-4/5 setzen das Ergebnisobjekt aus
EPIC-2 voraus; EPIC-6 integriert alles am Tool; EPIC-7 dokumentiert das
Endergebnis inkl. EPIC-1-Verhaltenskorrektur). Querschnitts-Constraint aus
`Konzept.md` (§Ziel, DoD): **es wird kein neues MCP-Tool registriert** —
alle Epics erweitern `get_impact` additiv. Non-Goals stehen im Konzept
(§Non-Goals) und werden hier bewusst nicht als Epics geführt.

- [x] EPIC-1: Traversierungs-Korrektur & Hint-Parität im Symbolgraph —
      `CallGraphTraversal.ExpandAsync`/`EnqueueChildren` enqueut künftig den
      tatsächlichen einschließenden Aufrufer
      (`GetEnclosingSymbol().NormalizeToOwningMember()`) statt
      `reference.Definition`, damit `depth > 1` echte mehrstufige
      Aufruferketten liefert (Konzept Muss-Have + Audit A.1/B; betrifft
      `find_references` und den `get_impact`-Symbol-Branch). Bestehende
      `ExpandAsync_Depth2_*`-Tests bewusst reviewen und entweder als korrekt
      bestätigen oder als Kodifikation des Defekts umstellen (Konzept
      Audit F — Symptom-Fixing-Verbot). Dazu Sufficiency-Hint-Parität:
      `GetImpactTool.ExecuteSymbolBranchAsync` hängt bei vollständigen
      Ergebnissen konsistent `McpSufficiencyHints.Append` an wie
      `FindReferencesTool` (Audit A.3).
- [x] EPIC-2: Analyzer-Kern — strukturiertes `DiffImpactAnalysis`-Ergebnis &
      breiter Diff-Symbolscanner — `DiffImpactAnalyzer` liefert intern ein
      strukturiertes Ergebnisobjekt (RepositoryRoot, SinceRef,
      ChangedFiles inkl. kompakter Hunk-Ranges, ChangedSymbols, References),
      ohne Git erneut auszuführen; `AnalyzeEntriesAsync` bleibt als
      kompatibler Wrapper bestehen; Git läuft pro Toolaufruf genau einmal
      (Konzept §Internes Ergebnisobjekt, §Performance-Regeln). Zweiter,
      klar benannter Scannerpfad (oder expliziter Scope-Parameter — kein
      verstecktes bool-Flag) für den breiten Symbolscope von
      `change-context`: private/protected/internal/public Methoden und
      Konstruktoren, Properties/Indexer, Events, Felder, Typdeklarationen,
      lokale Funktionen; pro Zeile die innerste passende Deklaration (nicht
      zusätzlich der enthaltende Typ), partielle Typen über Datei + Spanne
      unterscheidbar, stabile ID = DocCommentId oder deterministischer
      Fallback; Symbol-Einträge tragen Accessibility, Kind, Anzeigename,
      Projekt, Datei, Deklarationszeilen. Der bisherige `callers`-Scope
      (public/internal Methoden/Konstruktoren) bleibt unverändert
      (Konzept §Scope Must-have, Audit A.2/D.4).
      *Planungsnotiz:* Epic wurde in zwei Steps geführt — step-002 (Teil 1:
      strukturiertes `DiffImpactAnalysis`-Ergebnisobjekt, Wrapper-Analyse,
      kompakte Hunk-Ranges, `ChangedSymbolEntry` mit stabiler ID;
      done/approved, Commit 5b26c63b) und
      step-003 (Teil 2: breiter Scannerpfad inkl. TD-002-ID-Sonderfall
      für lokale Funktionen; done/approved, Commit 85c7fdce). Beide Teile
      erledigt → Epic abgehakt.
- [x] EPIC-3: Testfundament & Einmal-Ausführungs-Nachweis — neutrale
      Test-Fixture (mind. zwei Produktionsprojekte + ein Testprojekt; Diff
      ändert zwei Methoden in zwei Dateien, davon eine privat ohne externe
      Aufrufstellen) als gemeinsame Grundlage der Konzept-Testfälle;
      instrumentierter Test/Counter weist pro `change-context`-Aufruf nach:
      Git einmal, Testsolution einmal, Linter einmal (Konzept §Tests, DoD;
      Audit C/D.5). *Abgeschlossen:* step-004 (Fixture, Batch-Zuordnung,
      Counter-Kanal; approved via Korrektur step-006); der Linter-Beleg
      (LintRuns) folgt mit EPIC-5/step-007.
- [x] EPIC-4: Gebatchte Test-Zuordnung & recommendedTestCommands —
      `TestCoverageScanner` um echte Batch-Zuordnung erweitern: Testdokumente
      pro Aufruf höchstens einmal parsen/semantisch auswerten und gegen
      **alle** gekappten geänderten Symbole matchen — kein vollständiger
      Testprojekt-Scan pro Symbol (Konzept Muss-Have; Audit C nennt dies den
      größten Einzelblock des Tasks). Evidenzarten getrennt ausweisen
      (mindestens direkte Invocation und Namenskonvention);
      deduplizierte `dotnet test`-Filterbefehle pro betroffenem Testprojekt
      als vertragliches `recommendedTestCommands` (ehemals Nice-to-have,
      hochgestuft — Konzept §Must-have/E).
      *Abgeschlossen:* step-004 (done/approved, Commit 7b3b0284) inkl.
      Korrektur step-006 (Filter-Quoting, approved, Commit 4b53579a).
- [x] EPIC-5: Solutionweite Violations & diffbezogene Filterung — Violations
      werden einmal solutionweit berechnet („Linter genau einmal") und danach
      auf geänderte Hunks bzw. Deklarationsspannen gezeigter Symbole
      gefiltert; andere Violations derselben Datei bleiben außen vor, damit
      die Antwort diffbezogen bleibt und kein zweites ungescoptes
      `get_violations` entsteht (Konzept §Filterregeln, §Performance-Regeln;
      Basis `GetViolationsScanner`).
      *Abgeschlossen:* step-007 (interne Violations-Stufe
      `DiffViolationScanner.CollectAsync` + gemeinsamer Helper
      `RunSolutionLintAsync` + LintRuns-Inkrement + 7 FastTests + Tripel-
      Nachweis GitRuns/TestSolutionScans/LintRuns==1; done/approved,
      Commit 8bc3e919). Tool-Anschluss an den Antwortvertrag bleibt EPIC-6.
- [x] EPIC-6: `get_impact`-Vertrag „change-context" & strukturierte Antwort —
      neue Optionen additiv in `GetImpactInput`: `detailLevel` (`callers` |
      `change-context`, Default `callers`), `maxChangedSymbols` (Default 20,
      Cap 100), `maxTestsPerSymbol` (Default 10, Cap 50); Delegat-Signatur/
      Record gegen die Linter-Grenzwerte prüfen (Audit D.6).
      `change-context` nur im Git-Diff-Modus; zusammen mit
      `symbolIdentifier` recoverable `INVALID_ARGUMENT` plus Hinweis auf
      `get_feature_context`; `gitSinceRef`/`depth`/`maxResults` bleiben,
      `depth` bleibt im gesamten Git-Branch wirkungslos (Audit D.3);
      `BuildAggregateWarningAsync` an den echten `ct` anbinden (Audit D.7).
      Antwort: strukturiertes Objekt (changedFiles mit Hunk-Ranges,
      changedSymbols, callSites aus dem Traversal-Ergebnis,
      testAssociations, violations, recommendedTestCommands, completeness)
      via `McpToolResults.Text<T>`; deterministische Kappung vor teuren
      Folgeanalysen (Projekt, Datei, Startzeile, Symbol-ID); explizite
      Vollständigkeitsmetadaten für Symbol-, Call-Site- und Test-Caps;
      kompakte Textzusammenfassung (Counts + gekappte Top-Einträge), keine
      Source-Bodies, keine zwei Vollkopien langer Bodies; `callers` bleibt
      abwärts-/snapshot-kompatibel (Konzept §Öffentlicher Vertrag,
      §StructuredContent, §Performance- und Größenregeln, §Tests).
      *Abgeschlossen:* step-008 (gesamtes Epic als einzelner Step:
      Parameter + Validierung + Kappung im Analyzer-Kern + strukturierte
      Antwort + ct-Bindung + Tests; done/approved, Commit 5425f95f; Doku
      bleibt EPIC-7).
- [ ] EPIC-7: Dokumentation in `Docs/agent-api.md` inkl. Grenzen —
      JSON-Feldnamen des StructuredContent exakt dokumentieren (additive
      Felder); den `ExpandAsync`-Fix als **Verhaltenskorrektur** von
      `find_references`/`get_impact` (Symbol-Branch) ausweisen, nicht nur
      als additive Erweiterung (Audit B); dokumentierte Grenzen: gelöschte
      Dateien liefern keine Hunks und erscheinen nie in `changedSymbols`
      (Audit D.1/F), Umbenennungs-Randfälle (D.2), `depth` im gesamten
      Git-Branch wirkungslos, stabile ID = DocCommentId oder deterministischer
      Fallback (lokale Funktionen), Testinformationen korrekt als „statische
      Zuordnung" benannt (DoD); README-MCP-Tabellenzeile zu `get_impact`
      aktualisieren; nach Task-Abschluss `Docs/ROADMAP.md`-Eintrag ergänzen
      (Update-Pflicht Richtlinien §4).
      *Planungsnotiz:* in Arbeit → step-009 (gesamtes Epic als einzelner
      Step: agent-api.md-Vertrag/-Grenzen/-Verhaltenskorrektur, README-Zeile,
      ROADMAP-Eintrag; danach keine offenen Epics mehr → Loop-Ende).
