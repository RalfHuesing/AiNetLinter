---
status: done
type: step-review
task: 03_get-impact-zum-diff-kontext-erweitern
step: 008
epic: EPIC-6
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha
reviewed_by_model_knowledge_cutoff: unbekannt
reviewed_at: 2026-08-23T11:52:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 008: get_impact-Vertrag „change-context" & strukturierte Antwort

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-<NNN>`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle vier „Konkrete Änderungen" gegen den Commit-Diff (`5425f95f`) verifiziert:
additive Lambda-/Beschreibungserweiterung an DEM einen `get_impact`-Eintrag
(keine zweite `tools.Add`-Zeile, keine neue Tool-ID), Validierung vor dem
Dispatch, D.7-ct-Bindung in beiden Git-Zweigen, Kappung im Analyzer-Kern,
neue DTO-Datei; Tests decken die komplette Plan-Checkliste ab (inkl.
Feldnamen-Pinning über serialisierte Property-Namen-Arrays auf jeder
Verschachtelungsebene); CodeMap trägt die DTO-Datei und alle berührten
Module mit „(zuletzt: step-008)".

### Rules-Konformität

Grenzwerte selbst per `metrics_lookup` nachgemessen (`ExecuteChangeContextBranchAsync`
50 LOC/CC 6/CogC 5/**4 effektive** Parameter — der Linter zählt `CancellationToken`
nicht mit; Mapper-Methoden 15–27 LOC): alle OK; Kommentar-Disziplin (keine
Task-/Step-/EPIC-Referenzen im neuen Code) und xUnit-v3-/TestTempDirectory-Pflicht
eingehalten; Docs/** bewusst EPIC-7 vorbehalten (im Plan als Ausnahme dokumentiert).

### Logische Korrektheit

Kappung sitzt im Kern zwischen `GetChangedSymbolsFromHunksAsync` und
`BuildReferencesAsync` und ist bei `matches.Count <= cap` ein No-op ohne
Sortierung (callers bleibt strukturell bytegleich, Snapshot-Tests grün);
die Batch-Test-Stufe erhält die ISymbol-Handles der gezeigten Symbole, die
Violations-Stufe nur die gezeigten Entries — weggekappte Symbole können in
keiner Folgeanalyse auftreten (Integrationstest pinnt das über den Raw-Text);
`testsTruncated`/`recommendedTestCommands` nur aus NACH-Cap-Treffern; leere
Ziellisten lösen weder Testscan noch Lint aus (Skip-empty aus step-004/007).

### Konzept-Treue (Ebene 4)

StructuredContent-Feldnamen EXAKT wie das Konzept-Beispiel (Top-Level,
`changedFiles[].ranges[].startLine|lineCount`, `changedSymbols[]` inkl.
`accessibility` als String "Public", `testAssociations[]`, `violations[]`,
`completeness` mit den fünf Metadaten-Feldnamen); Caps/Defaults exakt
20/100/10/50 bei Default `callers`; change-context+symbolIdentifier →
recoverable INVALID_ARGUMENT mit `get_feature_context`-Hint; Sufficiency-Hint
nur bei vollständigem Ergebnis, sonst Meta-Zeile; `depth` im gesamten
Git-Branch wirkungslos und im Vertragstext so ausgewiesen (D.3); kein neues
Tool registriert (DoD in der vom Plan dokumentierten additiven Lesart erfüllt);
kein Non-Goal berührt (Violations ohne Snippet, keine Metrics-Duplikation).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (1628 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (350 Tests, 0 Fehler)
```

Hinweis zur ersten FastTests-Wiederholung (1627/1628):
`SourceFileCatalogPolicyTests.PolicyCalls_DoNotLoadDeniedInfrastructure` meldete
`Microsoft.CodeAnalysis.Workspaces.MSBuild` im Test-AppDomain plus
`TestPipelineException` beim Assembly-Cleanup — ein AppDomain-weiter
Pollution-Guard ohne Bezug zum Step-Code; Versuch 2 grün (1628/1628),
Orchestrator-Zweitmessung ebenfalls 2× grün → als transientes xUnit-Cleanup-/
Paralleitäts-Artefakt der Umgebung eingestuft, nicht reproduzierbar, kein Finding.

## Sonstige Beobachtungen / MINOR / NITPICK

Magic-Values-Einordnung (vom Orchestrator angestoßen): die Vertragswerte
20/100/10/50 und "change-context" sind bereits als benannte Konstanten in
`ChangeContextContract` zentralisiert und werden an allen Verwendungsstellen
(einschließlich der Delegat-Defaults) referenziert — die Regel „Magic Values
in typisierte Konstanten auslagern" ist erfüllt, nicht verletzt. Dass
`find_magic_values` die const-Deklarationen selbst sowie Wertgleichheit mit
fachlich fremden Konstanten (`DiRegistrationHeuristics.MaxRegistrationHits`,
Type-Hierarchy-Limit) als Kandidaten meldet, ist Werkzeug-Rauschen an einer
vertraglich fixierten Stelle — **kein Tech-Debt-Eintrag**.
