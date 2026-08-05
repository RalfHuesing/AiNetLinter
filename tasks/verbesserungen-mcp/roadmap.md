---
status: active  # active | done
task: verbesserungen-mcp
derived_from: konzept.md
created_at: 2026-08-05
last_updated: 2026-08-05
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: verbesserungen-mcp

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build` (Solution: `AiNetLinter.slnx`,
  Projekte `src/AiNetLinter/AiNetLinter.csproj` +
  `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj`). Ziel-Framework laut
  `.csproj`: **`net10.0`** — `<TreatWarningsAsErrors>true</...>` ist
  gesetzt, d. h. Build muss 0 Fehler **und** 0 Warnungen liefern.
  (Hinweis: `Konzept.md` und `AGENTS.md` §1 nennen ".NET 9" — das
  `.csproj` selbst sagt `net10.0`, ebenso `AiNetLinterRichtlinien.mdc`
  Kopfzeile ".NET 10"; maßgeblich für Steps ist das `.csproj`.)
- **Test-Command:**
  - Schnelle Iteration: `dotnet test --filter Category=Unit` (bzw.
    `Category!=Integration`)
  - Pflicht vor Task-/Step-Abschluss: **voller Lauf** `dotnet test`
    (muss grün sein, siehe `AGENTS.md` §2 und Konzept „Definition of
    Done")
  - Fehlerdiagnose bei langem/abgeschnittenem Output:
    `TestResults/latest.trx` direkt auslesen statt Testlauf erneut
    unvollständig zu starten (`AiNetLinterRichtlinien.mdc` §3)
  - MCP-/Dogfood-Verifikation ausschließlich über die C#-Testinfrastruktur
    (`McpLiveRepositoryTests`, `McpTestClient` in
    `src/AiNetLinter.Tests/Mcp/**`) — **keine** ad-hoc-Skripte
    (`AiNetLinterRichtlinien.mdc` §4)
- **Lint-Command:** Kein separates externes Lint-Tool — AiNetLinter
  lintet sich selbst (Dogfooding), Regeln sind bereits in
  `.agents/rules/AiNetLinter.mdc` als Kurzfassung synchronisiert
  (generiert aus `rules.json`). Kein zusätzlicher Lint-Schritt nötig,
  sofern kein Step explizit `rules.json`/Regel-Verhalten ändert.
- **Code-Style-Kurzfassung** (aus `<rules_dir>/**`): `sealed` für
  konkrete Klassen, `#nullable enable`, kein leeres `catch`, kein
  `dynamic`, `out` nur in `Try*`, Methoden ≤60 Zeilen, Datei ≤500
  Zeilen, Zyklomatische Komplexität ≤12, kognitive Komplexität ≤15,
  max. 4 Methodenparameter (sonst Parameter-`record`), Result-Pattern
  statt Exceptions wo sinnvoll (nicht linter-erzwungen), sparsame
  Code-Kommentare (keine Task-/Step-/Epic-IDs im Code, kein
  Refactoring-Verlauf), Zero-Warning-Direktive.
- **Commit-Konventionen:** Deutsche Conventional Commits, imperativ
  (`feat:`, `fix:`, `docs:`, `chore:` …). Jede Antwort mit
  Datei-Änderungen **muss** mit einem `### Commit-Vorschlag`-Block
  enden (reiner Commit-Text, kein Shell-Befehl) — Pflicht laut
  `AiNetLinterRichtlinien.mdc` §4 / `AGENTS.md` §4.

## Regel-Index

- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur-Leitplanken
  (monolithisch, kein DI/ALC/Plugin), Windows/PowerShell-Tool-Regeln,
  Build/Test-Workflow, Kommentar- und Commit-Konventionen,
  Qualitätsdrift-Prävention (Zero-Warning, Result-Pattern,
  Symptom-Fixing verboten), Agenten-Arbeitsstil.
- `.agents/rules/AiNetLinter.mdc` — auto-generierte C#-Codequalitäts-
  Kurzreferenz aus `rules.json` (Grenzwerte wie `MaxMethodLineCount`,
  `MaxCyclomaticComplexity`, aktive Regeln wie `EnforceSealedClasses`,
  `EnforceNoSilentCatch`, `DetectAndBanPhantomDependencies`,
  Projekt-Overrides für `*.Tests`).

## Epics

- [ ] EPIC-01: Blazor-Symbolgraph-Integration (P1) — Razor-Source-
      Generator-Output beim Solution-Load in
      `src/AiNetLinter/Baseline/SourceFileCatalog.cs` einbeziehen,
      sodass der Roslyn-Symbolgraph mit `dotnet build` exakt
      übereinstimmt (volle Integration, kein Workaround). Umfasst laut
      Konzept „Wie": neue synthetische `.razor`/`.razor.cs`-Test-Fixture
      (`Microsoft.NET.Sdk.Razor`-Projekt mit Partial-Klasse +
      `override`-Lifecycle-Methoden) in `src/AiNetLinter.Tests`, bevor
      der eigentliche Fix beginnt, sowie Prüfung, ob der globale
      1322-Errors-Rausch-Hinweis danach automatisch entfällt (P2,
      `McpCompileDiagnostics.cs`) oder zusätzlich auf echten
      Solution-Load-Fehler eingegrenzt werden muss. Bezug:
      `Konzept.md` Scope P1 „Blazor-Partials" + P2 „Globaler
      Rausch-Hinweis eindämmen" (funktional zusammenhängend — Rausch-
      Hinweis-Fix hängt von P1-Ergebnis ab, siehe „Wie").
      **In Arbeit → step-001 (Fixture, done/approved), step-002 (Fix,
      geplant).** Root Cause in step-002-Planung empirisch verifiziert
      (siehe `step-002/step-plan.md`): kein Bug in
      `SourceFileCatalog.cs`/`LinterEngine.cs` selbst — Ursache ist eine
      Versions-Diskrepanz zwischen den in `AiNetLinter.csproj`
      referenzierten `Microsoft.CodeAnalysis.*`-NuGet-Paketen (5.3.0)
      und der vom lokal installierten .NET-SDK gebündelten
      Razor-Source-Generator-Assembly (referenziert 5.5.0), wodurch
      Roslyn den Generator-Typ prozessintern nicht laden kann
      (`FileLoadException`, von Roslyns Analyzer-Loader verschluckt).
      Fix testweise verifiziert: Versions-Bump auf 5.6.0 behebt CS0115
      **und** den Rausch-Hinweis im Fixture-Fall vollständig, ganz ohne
      Code-Änderung an `SourceFileCatalog.cs`/`McpCompileDiagnostics.cs`.
- [ ] EPIC-02: Einheitlicher Symbol-Identifikator-Parser (P1) —
      `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs` (aktuell
      laut Dateikommentar nur für `FindReferencesTool` ausgelagert) als
      gemeinsamen Einstiegspunkt für alle drei dokumentierten
      Identifikator-Formate (qualifizierter Name,
      `Datei:Zeile:Spalte`, DocumentationCommentId) etablieren und
      einheitlich von `find_references`, `get_symbol_body`
      (`GetSymbolBodyTool.cs`) **und** `get_impact` (`GetImpactTool.cs`)
      nutzen lassen. Bezug: `Konzept.md` Scope P1 „Einheitlicher
      Symbol-Identifikator-Parser".
- [ ] EPIC-03: Kleinere Tool-Konsistenz-Fixes (P2/P3-Batch) — Cluster
      unabhängig lösbarer, jeweils kleiner Muss-Haben-Punkte aus
      `Konzept.md` Scope, die sich für Step-Modus-Micro-Batching
      eignen (`step_type: batch`, siehe `../spec.md` §10.6):
      - `get_symbol_body`-ID-Korruption beheben (P2):
        `GetSymbolBodyTool.cs` liefert bei
        `Datei:Zeile:Spalte`-Identifikator eine verschachtelte/doppelte
        DocumentationCommentId statt der von `get_file_skeleton`
        (`SkeletonSyntaxWalker.TryCreateDeclarationId`) gelieferten ID
        — auf denselben Pfad angleichen.
      - `get_violations`-Meldung präzisieren (P2):
        `GetViolationsScanner.FormatReport`
        (`GetViolationsScanner.cs:113-121`) muss „N Dateien im Scope,
        0 Violations" von „keine Datei im Scope" unterscheiden (kein
        Eingriff in `MatchesScope`).
      - `ainetlinter://overview`-Status synchronisieren (P3):
        `OverviewResourceRegistration.DescribeSolution` muss den
        tatsächlichen `McpCodeGraphServer.LoadState` widerspiegeln,
        auch unmittelbar nach Serverstart.
      - `find_references`/`get_impact` depth-Hard-Cap dokumentieren
        (P3): `CallGraphTraversal.MaxRecursionNodes` (200) im
        Tool-Schema/-Beschreibungstext sichtbar machen, nicht nur in
        der Trunkierungs-Meldung.
      - (Nice-to-Have, optional) Lesbarere ID-Darstellung für explizite
        Interface-Implementierungen: aktuell Standard-Roslyn-
        `#`-Encoding (kein Bug), optionale zusätzliche
        Agenten-lesbare Darstellung neben der Standard-ID.
      Bezug: `Konzept.md` Scope P2/P3 (vier Muss-Haben-Punkte) + das
      eine Nice-to-Have. Granularität bewusst als ein Epic gebündelt
      (Nutzer-Vorgabe: keine Einzel-Epics für kleine, unabhängige
      Punkte) — ob daraus im Step-Modus ein einzelner Batch-Step oder
      mehrere Steps werden, entscheidet der Step-Modus-Planer anhand
      des dann aktuellen Codestands und der Batch-Deckelung
      (`max_batch_items`/`max_batch_diff_lines`, `../spec.md` §10.6).

<Reihenfolge spiegelt die Priorität aus `Konzept.md` (P1 vor P2/P3) und
die im „Wie"-Abschnitt genannte Abhängigkeit EPIC-01 → Rausch-Hinweis;
EPIC-02 ist von EPIC-01 unabhängig und könnte auch vorgezogen werden —
das entscheidet der Step-Modus-Planer anhand des tatsächlichen
Codestands, keine endgültige Festlegung hier.>
