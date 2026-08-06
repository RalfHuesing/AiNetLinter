---
status: active
task: safeguard
derived_from: konzept.md
created_at: 2026-08-06T13:48:11+02:00
last_updated: 2026-08-06T13:48:11+02:00
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
---

# Roadmap: safeguard

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `../spec.md` §7.2. Diese Datei wird
laufend angepasst (Epics abgehakt, ergänzt, umformuliert oder als
obsolet markiert) — kein starres Vorab-Dokument.

## Tech-Stack-Notiz

- **Build-Command:** `dotnet build` (Solution: `AiNetLinter.slnx` im Projekt-Root; Hauptprojekt `src/AiNetLinter/AiNetLinter.csproj` zielt auf `net10.0`, `TreatWarningsAsErrors=true`)
- **Test-Command:** `dotnet test` (xUnit v3 in `src/AiNetLinter.Tests/`, parallele Test-Collections **nicht** zwangsserialisieren — siehe Richtlinien §4; bei Testfehlern `TestResults/latest.trx` für Diagnose auswerten)
- **Lint-/Regel-Sync:** `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` (regeneriert `.agents/rules/AiNetLinter.mdc` aus `rules.json`); CI-Release-Workflow `.github/workflows/release.yml` läuft nur auf Tag-Push (`v*`) — kein Push-Gate, lokal verifizieren
- **Code-Style-Kurzfassung (aus `.agents/rules/AiNetLinterRichtlinien.mdc` §1-§5 + `AiNetLinter.mdc`):** monolithisches CLI ohne DI/ALC/Plugins; statische Klassen, `sealed` für konkrete Klassen, `record` für unveränderliche Daten, `#nullable enable` am Dateianfang, keine leeren `catch`, kein `dynamic`, kein `out` außerhalb `Try*`; `Result<T>`-Pattern bevorzugt; Methoden ≤ 60 Zeilen, Klassen-Footprint ≤ 2500 transitive Typ-Zeilen; `sealed`-Quote geht in `safeguard`-Score ein — Selbstkonsistenz direkt anwenden; `*.Tests` hat gelockertes `MaxMethodLineCount=100` und ausgeschaltetes `EnforceSealedClasses`; **keine Task-/Plan-/Step-/TD-/EPIC-Referenzen im Produktionscode** (Richtlinien §5), Kommentare sparsam und nur für nicht-offensichtliches *Why*
- **Commit-Konventionen (aus `AGENTS.md` §4 + `AiNetLinterRichtlinien.mdc` §4 + spec §10.3):** Conventional Commits **auf Deutsch, imperativ** (z. B. `feat(mcp): SafeguardScanner mit deterministischer Score-Berechnung [safeguard]`); Task-Suffix `[safeguard]` im Subject **bei jedem** Commit dieses Tasks (spec §10.3); jeder Commit endet mit `### Commit-Vorschlag`-Block im Agent-Output; **nie** `--amend`/`rebase`/`reset --hard` auf bereits committete Commits (spec §10.3) — pro Step mehrere kleine Commits (Code → Doku → Planung → Review), kein Push durch den Workflow; Push bleibt beim Nutzer

## Regel-Index

- `.agents/rules/AiNetLinterRichtlinien.mdc` — manuell gepflegte Architektur-, Workflow- und Verhaltens-Leitplanken für AiNetLinter (Grundprinzipien monolithisch/statisch/Records, Architektur-Verbote gegen ALC/DI/Plugins, Windows-/PowerShell-Tooling-Regeln, xUnit-v3-Testpflicht, MCP-Live-Tests über C#-Infrastruktur, Result-Pattern, Zero-Warning-Direktive, Commit-Vorschlag-Pflicht, sparsame Kommentare ohne Task-/Plan-Referenzen).
- `.agents/rules/AiNetLinter.mdc` — automatisch aus `rules.json` generierte C#-Codequalitäts-Regeln (Grenzwerte `MaxLineCount=500`, `MaxMethodLineCount=60`, `MaxCyclomaticComplexity=12`, `MaxCognitiveComplexity=15`, `AIContextFootprint=2500` u. a., `sealed`-Klassen, `EnforceNullableEnable`, `EnforceAsciiIdentifiers`, `BanAsyncVoid`, `BanBlockingTaskAccess`); Test-Projekt-Override `MaxMethodLineCount=100` + `EnforceSealedClasses` aus.

## Epics

- [ ] EPIC-01: SafeguardScanner (deterministische Score-Berechnung) — Reine Funktion
      `SafeguardScanner.ComputeScoreAsync(solution, config, scope, ct) → ScoreResult`
      mit gewichteten Komponenten (Violations/CC-Durchschnitt/Footprint/Sealed-Quote),
      deterministisch und ohne MCP-Abhängigkeit; Score-Records
      (`ScoreResult`/`ViolationEntry`/`RemediationHint`); JSON-Schema-Bausteine für
      structured content; 5+ Unit-Tests (leere Solution, einzelne Violation, hoher/
      niedriger Score, Threshold-Logik, Determinismus über zwei Läufe). Bezieht sich
      auf `konzept.md` §"Muss-Haven" Punkte 4-6+8 (deterministische Score, Komponenten,
      Remediation-Generator, 10+ Unit-Tests) und §"Wie" Schritt 1. Beobachtung
      `AIContextFootprint` an `AnalysisToolRegistrations` (siehe konzept.md
      §"Entdeckte Mängel"): in einem der Steps entscheiden, ob Konsolidierung oder
      PathOverride — Entscheidung ad-hoc, kein Vorab-Block. **Geplante Schritt-Anzahl:
      2-4** (Richtwert — Step-Modus entscheidet JIT auf Basis des tatsächlichen
      `Mcp/Tools/GetViolationsScanner.cs`-Patterns, z. B. Scanner-Grundgerüst →
      Score-Komponenten → Remediation → Scanner-Tests).
- [ ] EPIC-02: safeguard-Tool (MCP-Wrapper, Registrierung, Live-Repo-Integration) —
      `SafeguardTool.ExecuteAsync` als dünner Dispatcher auf den Scanner; neue
      `AddSafeguard(...)` in `AnalysisToolRegistrations.Register`; Input/Output als
      JSON Schema 2020-12 (`{ passed, score, threshold, violations[], remediation,
      summary }`) im `CallToolResult.Content` als structured content; `passed=false`
      ist **nicht** `isError: true` (siehe `Mcp/IsErrorPolicy.md`); `ServerInstructions`
      um `safeguard` als Quality-Gate erweitern; 5+ Unit-Tests (Loading-State,
      Solution-Not-Loaded, Scope-Filter, Tool-Wrapper, IsError-Verhalten) plus 1
      Integration-Test in `McpLiveRepositoryTests`/`McpTestClient` auf das
      AiNetLinter-Repo selbst (Live-Score im erwarteten Korridor ≥ 5.0 — sonst Bug
      in Score-Formel). Bezieht sich auf `konzept.md` §"Muss-Haven" Punkte 1-3+7+9
      (Tool-Registrierung, Input/Output, structured JSON, ServerInstructions,
      Integration-Test) und §"Wie" Schritt 2. **Geplante Schritt-Anzahl: 2-3**.
- [ ] EPIC-03: Verifikation, Doku, Roadmap-Abschluss — Volllauf `dotnet test` grün
      (alle 200+ bestehenden Tests weiterhin grün, keine Regressionen); Volllauf
      `dotnet build` mit `TreatWarningsAsErrors` grün; `Docs/agent-api.md` um
      `safeguard`-Sektion erweitern (Use-Cases, Input/Output-Schema, Beispiel-Call,
      Beispiel-Antwort mit Score+Violations+Remediation); `Docs/ROADMAP.md`
      Status-Update falls S1.2 dort geführt; `tasks/features/05-roadmap.md` Zeile 91
      Status `[ ]*` → `[x]` **und** Akzeptanzkriterien-Block Zeilen 179-184 abhaken
      (siehe S1.2 dort); `dotnet run -- --sync-agent-rules-only` für
      `.agents/rules/AiNetLinter.mdc`; ggf. Tech-Debt-Eintrag in
      `tasks/safeguard/tech-debt.md` (z. B. Footprint-Limit-Beobachtung aus
      EPIC-01); Conventional-Commit-Suffix `[safeguard]` bei jedem Commit
      beibehalten. Bezieht sich auf `konzept.md` §"Muss-Haven" Punkte 10-11,
      §"Definition of Done" (gesamt) und §"Wie" Schritt 3. **Geplante Schritt-Anzahl:
      1-2** (typischerweise ein Volllauf- und ein Doku-/Sync-Commit).
