---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 015
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-24T17:40:00+02:00
verdict: approved
tech_debt_ids: [TD-009, TD-010]
---

# Review Step 015: Task-weites Drift-Audit — Duplicates, Magic Values, Dead Code

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (gezielte Filterläufe; Vollstack-Angaben an Rohdaten und Fehlersignaturen verifiziert statt rerun)

## Befund

### Plan-Erfüllung

Alle drei Audit-Läufe sind nachvollziehbar ausgeführt und im Result je Werkzeug mit Fundanzahl, Triage und Entscheidung dokumentiert; die Rohdaten in `%LOCALAPPDATA%/Temp/step015-audit/` belegen die Zahlen exakt (`duplicates.json`: 1 Cluster/3739 Methoden = TD-006-Fall; `magic_production_full.json`: 878 Einträge/758 eindeutige Werte/„335 Dateien" wörtlich aus dem Tool-Header, keine Trunkierung; `deadcode.json`: 4 Symbole/639 gescannt inkl. ask_user-Empfehlung), die 13 Cluster und 4 Symbole wurden gemäß Fundliste gefixt und alle DoD-Punkte sind erfüllt.

### Rules-Konformität

Richtlinien §5 (Zero-Warning, Drift-Abbau, Symptom-Fixing-Verbot — es wurden keinerlei Testdateien angefasst), §1 (MCP-first via stdio-JSON-RPC mit Health-Poll vor Batch, Dokumentations-Objektivität im berührten Code) und §3-Testkategorien (Vollstack genau einmal, Stress nie) sind eingehalten.

### Logische Korrektheit

Verhalten ist unverändert: alle gebundenen Konstanten tragen wortgleiche Werte zu den entfernten Literalen (`LinterRuleIds.cs:36,68–78`), die Referenzfreiheit aller vier Totcode-Symbole wurde eigenständig repo-weit bestätigt (inkl. Tests; einziger Treffer zu `ForCurrentUser` ist ein Binär-False-Positive in einer Fremd-DLL), `MaxUtf8Bytes` bleibt wie angegeben von beiden Suiten referenziert, und die Klassifikation der zwei Integrationsausfälle als TD-008-Kontamination ist mechanisch plausibel (`DaemonProcessContractHarness.EndpointGate` = statisches `SemaphoreSlim(1,1)`; `WaitAsync(token)` wirft exakt die dokumentierte `OperationCanceledException`) plus isolierte Nachläufe 1/1 grün.

### Konzept-Treue (Ebene 4)

Der Commit-Diff enthält ausschließlich Fundlisten-Maßnahmen (kein Scope-Creep, keine Assertions-Änderungen), der Duplicate-No-op entspricht dem als TD-006 geführten Befund, und die AK-5-Nutzerentscheidung „Option A" (stderr-[WARN], Konzept Zeile 570/621) ist vom Diff unberührt — kein Rückfall.

### Build-/Test-Status

```
dotnet build                                                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FQN~MiddleMan|NamespaceCoupling|UiFileSeparation|CssAnalyzer|ServerInstructions|OverviewResourceRegistration|ThinClient|DaemonProtocol|GetCallTree|FindReferences|GetSymbolBody|MetricsLookup" → 159/159 grün
```

Vollstack-/Integrationsangaben des Coders nicht wiederholt (Effizienzvorgabe, TD-008-Kontaminationsrisiko eigener Läufe), sondern über Rohdaten, TRX-Signatur-Plausibilität und isolierte Nachlauf-Dokumentation verifiziert.

## Sonstige Beobachtungen / MINOR / NITPICK

- **Stale Doku-Zeile `AMBIGUOUS_SOLUTION` (`Docs/agent-api.md:834`) ist MINOR, keine unvollständige Step-Ausführung:** die Zeile war bereits vor diesem Step stale — der letzte Emitter fiel im EPIC-A-Wiring (`ccf7b33a`, dort fiel auch der assertierende E2E-Test weg); step-015 entfernte nur die seitdem verwaiste Konstante. Der Plan schloss Doku-Sync explizit aus („Keine Doku-Sync-Pflicht außer Codemap-Zeile"), und Richtlinien §4 greift weder nach Dateiliste (`agent-api.md` fehlt in der Liste) noch nach Anlass (Entfernung einer referenzfreien internen Konstante ist keine Feature-/Konfigurationsänderung, null Verhaltensdelta). → als TD-010 vermerkt.
- Der Test-Harness spawnt weiter das Literal `"--daemon-start"` (`DaemonProcessContractHarness.cs:125`) — lag außerhalb des Audit-Scope (Produktion + Stichproben) und wäre durch unbekannte-Flag-Hard-Cuts bei einem Rename laut auffällig; kosmetisch.

## Tech-Debt-Einträge aus diesem Review

- `TD-009` (siehe `tech-debt.md`) — MCP-Toolnamen als Literale an Registrierung UND Overview-Tabelle (~24 Tools) plus Prosanennungen; Registrierung↔Tabelle-Drift ist durch `ToolSummaries_MatchesRegisteredToolNames` testbewacht, Rest ist Doppel-Pflege und ungeschützte Prosa.
- `TD-010` (siehe `tech-debt.md`) — `Docs/agent-api.md:834` beschreibt den emitterlosen Code `AMBIGUOUS_SOLUTION` weiter als aktiv (§1-Dokumentations-Objektivität), korrigierbar ohne Verhaltensbezug.
