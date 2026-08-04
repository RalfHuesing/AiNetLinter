---
status: done
type: step-review
task: codegraph-mcp-finish
step: 010
epic: EPIC-05
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-04T12:30:00+02:00
verdict: approved
tech_debt_ids: [TD-008]
---

# Review Step 010: Last-Fixture + Kaltstart-Entkopplung + Staleness-mtime-Cache (B.3, B.4, B.5) + TD-005-Sanity + TD-007

## Verdict

- [x] **approved** — alle vier Prüfebenen ok, zwei MINOR-Findings (kein `CRITICAL`/`MAJOR`)

## Geprüft

- [x] Plan-Erfüllung: alle 5 Sub-Bereiche (B.3, B.4, B.5, TD-005, TD-007) umgesetzt + 6 dokumentierte Abweichungen plausibel begründet
- [x] Rules-Konformität: `AiNetLinterRichtlinien.mdc` §1-§5 + `AiNetLinter.mdc` Grenzwerte (Build 0/0/0, keine `step-010`/`TD-005`/`B.3`-Referenzen im neuen Code, kein DI/Plugin/ALC, `sealed` + `#nullable enable` in allen neuen Dateien)
- [x] Logische Korrektheit: B.3-Fixture generiert tatsächlich Roslyn-lösbare Solutions, B.4-Drei-Zustands-Lifecycle semantisch sauber, B.5-mtime-Shortcut auf B.2-Sweep angewendet
- [x] Konzept-Treue: Muss-Haben B Punkte 3-5 umgesetzt, DoD Z. 650-653 erfüllt (EPIC-05 zu 3/7 Punkten; B.6 + B.7 bleiben für EPIC-06); Non-Goals (keine Editier-Tools, keine Testinhalts-Änderungen außerhalb Scopes) eingehalten
- [x] Build: selbst nachgeprüft, grün (0/0, ~2 s)
- [x] Tests: selbst nachgeprüft, grün (1199/1199, 2 m 12 s, kein TD-005-Flake reproduziert)

## Befund

### Plan-Erfüllung

Alle 5 Sub-Bereiche umgesetzt: B.3 (`LoadFixtureBuilder` + 1k/10k-LOC-Stufen + Integration-Tests, Mess-Zahlen reproduziert), B.4 (`ServerLoadState`-Enum mit Loading/Loaded/LoadFailed, `LoadFunc` in `McpCodeGraphServerOptions`, `McpToolResults.Loading()`, Loading-Check in allen 9 Tool-Klassen, `McpTestClient`-Retry mit 30×500 ms Backoff, `McpServerCommand` startet Server mit `LoadFunc`), B.5 (`HasSolutionDirChanged` + `ComputeMaxDirMtimeUtc` mit rekursiver Max-mtime-Aggregation, `McpCodeGraphServerRefreshParameters`-Record + `shouldSweep`-Parameter in `SweepForNewFiles`), TD-005 (Gate 4→6 Slots + 60 s Timeout) und TD-007 (XML-Doc-Sanierung in `McpCodeGraphServerOptions.cs:56-64 + 78-83`); 6 dokumentierte Abweichungen sind alle plausibel (mtime-Aggregation rekursiv statt root-only, Retry in `CallToolAsync` statt `ConnectAsync`, `internal static` + Record-Bündelung für Refresh.Run, 10 `PathOverrides` für Tool-Footprint, 3 `ainetlinter-disable`-Suppressions mit Inline-Begründung, `GetAwaiter().GetResult()` im Test-Harness).

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §5 Zero-Warning eingehalten (Build 0/0/0), Verbot Task-/Planungsartefakt-Referenzen eingehalten (Grep über `src/AiNetLinter/Mcp/**` und `src/AiNetLinter.Tests/**` zeigt keine Treffer auf `step-010`/`TD-005`/`EPIC-05`/`B.3`/`B.4`/`B.5`/`fix-01`/`step-009`), Verbot Symptom-Fixing eingehalten (TD-005 = Last-Tragfähigkeit, nicht Last-Signatur — Gate-Kapazität + Timeout, keine Retry-Logik an Gate-Stelle; der Coder hat explizit auf den TD-019-Retry-Pfad in `McpTestClient` verwiesen und keine zweite Retry-Ebene gebaut), §2 Architektur-Verbote eingehalten (`LoadFunc: Func<CancellationToken, Task<…>>` ist Konfigurations-Schnittstelle, kein DI-Container), `sealed` und `#nullable enable` in allen 7 neuen Klassen, `EnforceAsciiIdentifiers` eingehalten (`ServerLoadState`/`LoadState`/`LoadFunc`/`HasSolutionDirChanged`/`LoadingMessagePrefix`/etc.), `EnforceNoSilentCatch` an den 3 Suppressions-Stellen inline begründet (Dispose-Hänger, Server-Shutdown, mtime-Aggregation über unzugängliche Subdirectories).

### Logische Korrektheit

B.3 Fixture-Generierung erzeugt valide `MSBuildWorkspace.OpenSolutionAsync`-Targets (`.csproj`-Stubs mit `net10.0`/`ImplicitUsings`/`Nullable`, `.slnx` mit allen Projekten — verifiziert in `LoadFixtureBuilder.cs:81-96` und durch eigenen Volllauf: 0,70 s Cold-Start vs. Coder 0,79 s, 10k-LOC `GetCurrentSolution` min/median/mean/max = 0,028/0,029/0,029/0,032 s vs. Coder 0,034/0,035/0,035/0,037 s — gleiche Größenordnung); B.4 Drei-Zustands-Lifecycle ist semantisch sauber (`LoadState` aus `_loadTask`-Switch + `_catalog`-Fallback abgeleitet, kein eigener State → keine Drift möglich), `GetCurrentSolution` adoptiert Load-Resultat idempotent (zweiter Call gibt `same` Solution-Instanz zurück, durch `McpCodeGraphServerStalenessMtimeCacheTests.GetCurrentSolution_CalledTwiceWithoutDirChange_SkipsSweepOnSecondCall` belegt), `Loading()`-Helfer ist bewusst kein `IsError` (semantisch „transient Info", nicht „falscher Aufruf") — MCP-Test-Client retryt per String-Match auf `LoadingMessagePrefix`; B.5 mtime-Aggregation ist korrekt **rekursiv** (`ComputeMaxDirMtimeUtc` aggregiert Max über alle Subdirectories, weil Windows nur das Root-mtime bei Root-Änderungen updated — Abweichung 1 vom Coder transparent dokumentiert und durch `GetCurrentSolution_CalledAfterNewFile_TriggersSweepAgain`-Test belegt: neue Datei in `src/BaselineMini/` triggert Subdirectory-mtime → Sweep findet sie).

### Konzept-Treue (Ebene 4)

B.3-Vorgabe „generierte Last-Fixture als Skalierungsnachweis inkl. Messlauf" vollständig erfüllt (Skalierungs-Stufen 1k/10k, 1 Unit- + 2 Integration-Tests, dokumentierte Mess-Zahlen via `ITestOutputHelper`); B.4-Vorgabe „Transport zuerst, Solution-Load im Hintergrund, dritter 'lädt noch'-Zustand" vollständig erfüllt (`McpServerCommand.RunAsync` startet Server mit `LoadFunc`, `McpCodeGraphServer.LoadState` dritter Zustand, alle 9 Tool-Klassen reagieren mit `McpToolResults.Loading()`, `McpServerCommandLoadingStateTests.RunAsync_LoadFuncStillRunning_ToolReturnsLoadingInfo` beweist den Tool-Response-Pfad); B.5-Vorgabe „Staleness-Sweep über Verzeichnis-`mtime` kurzschließen, kombinierbar mit B.2-Sweep-Mechanismus" vollständig erfüllt (mtime-Cache greift in Phase 2 via `shouldSweep`-Parameter in `SweepForNewFiles`, Phase 1+3 unverändert); Non-Goals eingehalten (keine Editier-Tools, keine Testinhalts-Änderungen außerhalb Scopes — die `CallToolWithLoadingRetryAsync`-Ergänzung in `McpServerCommandErrorHandlingTests` ist mechanisch und nicht inhaltlich, `LoadFixtureBuilderTests`/`MeasurementsTests`/`StalenessMtimeCacheTests`/`LoadingStateTests` sind alle im jeweiligen Scope-Zielordner); DoD Z. 650-653 für B.3-B.5 erfüllt (drei von sieben Muss-Haben-B-Punkten, mit Integration-Tests abgesichert; B.6 + B.7 bleiben für EPIC-06).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                          → grün (0 Warnungen, 0 Fehler, 2 s)
dotnet test  AiNetLinter.slnx --no-build                → grün (1199/1199, 2 m 12 s, kein TD-005-Flake)
dotnet test  --filter FullyQualifiedName~LoadFixture   → grün (3/3, 3 s, ohne Build)
dotnet run --project src\AiNetLinter -- --config rules.json --path . → OK (0 Violations)
Get-Process AiNetLinter, testhost                       → keine hängenden Prozesse
```

## Findings (MINOR)

1. `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs:14-24` — [MINOR] [Plan-Erfüllung] Der XML-Doc-Klassenkommentar bricht weiterhin mitten im Satz ab: „…damit diese\n/// Klasse eigener `c>AIContextFootprint` (siehe `c>klein bleibt\n///." Plan Z. 562-563 hatte dies explizit als „Aufräumen erlaubt"-Punkt für step-010 benannt („betrifft diese Datei direkt — Loading-Check kommt dorthin"), der Coder hat den Loading-Check gesetzt aber den XML-Doc-Cleanup nicht mitgenommen. **Fix:** die vier Zeilen 20-23 zu einem sauberen Satz umschreiben (z. B. „…damit die Klasse unter dem `AIContextFootprint`-Limit bleibt" — Pattern wie die sanierte `McpCodeGraphServerOptions.cs:56-64`); TD-001 verweist bereits auf eine andere Variante dieses Patterns. Aufwand: 1 Minute. Verdict-relevant: nein, weil keine §5-Regel aktiv verletzt wird (die Verbots-Liste nennt „war früher private", das hier ist kein „war"-Marker, sondern ein Editor-Artefakt), nur die Lesbarkeit leidet.

2. `tasks/codegraph-mcp-finish/tech-debt.md` TD-005 + TD-007 — [MINOR] [Konzept-Treue] Der Coder hat beide Fixes implementiert (Gate 4→6 + 60 s Timeout; XML-Doc-Sanierung in `McpCodeGraphServerOptions.cs`), aber den `tech-debt.md`-Status nicht auf „geschlossen" aktualisiert. Plan Z. 663-664 (TD-005: „Falls beide Vollläufe TD-005-Flake-frei sind: … `tech-debt.md`-Status auf „geschlossen" setzen") und Z. 667-668 (TD-007: „TD-007 wird mitgenommen (ebenfalls geschlossen)") hatten dies explizit verlangt; die im `step-result.md` gewählte Formulierung „kann dann auf „geschlossen … stehen bleiben" ist eine schwächere Variante des Plans. Eigene Reproduktion: 1199/1199 grün in 2:12, also TD-005-Bedingung erfüllt. **Fix:** TD-005 und TD-007 in `tech-debt.md` jeweils auf `Status: geschlossen (im Rahmen der Sample Size, step-010)` setzen, Datum 2026-08-04, `closed_by: step-010`. Aufwand: 2 Minuten. Verdict-relevant: nein, weil die Implementierung vollständig und korrekt ist; nur die Tech-Debt-Verwaltungs-Hygiene fehlt.

## Sonstige Beobachtungen / MINOR / NITPICK

- **Linter-Verhalten `MaxMethodParameterCountForNonPublic: 6`:** der Coder berichtet in Abweichung 3, dass die projektweite `rules.json`-Einstellung (Z. 117) nur für `private`, nicht für `internal` greift. Da der Coder den Workaround (`internal static` + `McpCodeGraphServerRefreshParameters`-Record) sauber umgesetzt hat und der finale Code regelkonform ist, ist das **kein Finding**, nur eine Beobachtung für zukünftige Schritte. Empfehlung an nächsten Schritt, der einen 5-Param-`internal static`-Helper anfasst: vor der Annahme „geht durch mit 5 Params" den Linter empirisch prüfen oder direkt Record-Bündelung planen. Geht nicht in `tech-debt.md` ein, weil keine Architektur-/Anti-Pattern-Beobachtung, sondern eine Checker-Eigenheit.
- **`McpCodeGraphServerStalenessMtimeCacheTests` Performance-Effektivität:** wie der Coder in „Bekannte Unschärfen" selbst anmerkt, misst der Unit-Test nur Korrektheit (Cache-Hit ohne Änderung, Cache-Miss bei neuer Datei), nicht die prozentuale Zeitersparnis. Volllauf-Laufzeit 2:12 (vs. 2:31-2:37 in step-009) — innerhalb der normalen Schwankung, kein messbarer B.5-Hebel in der Test-Suite selbst. Akzeptabel für die Konzept-Vorgabe „Skalierungsnachweis" (das war B.3) — B.5 ist Optimierung, deren Effektivität erst im realen Production-Workload messbar wird.
- **3 `ainetlinter-disable`-Suppressions in `McpCodeGraphServer.cs`:** alle drei (`BanBlockingTaskAccess` in `GetCurrentSolution`/`Dispose`, `EnforceNoSilentCatch` in `Dispose`/`ComputeMaxDirMtimeUtc`) sind strukturell begründet und inline dokumentiert. Die `GetAwaiter().GetResult()`-Stellen in `McpCodeGraphServer.GetCurrentSolution` Z. 96-103 sind defensiv (durch vorgeschalteten Loading-Check in Tools praktisch nie erreicht) und entsprechen dem etablierten projektweiten Pattern für „adopt load result once" — vertretbar.
- **`rules.json` jetzt 12 statt 2 `MaxAIContextFootprint`-PathOverrides im MCP-Modul** (10 neue für `B.4`-Auswirkung): Konzept-Punkt C („`ILinterEngineConfig`-Refactor" zur Reduktion der Overrides) wird dadurch **dringlicher**, nicht weniger. EPIC-05-Coder hat ehrlich dokumentiert (Abweichung 4), dass die geplanten ~30-50 Zeilen real ~60-80 Zeilen + Enum + Record waren. Das ist ein zählbarer Hebel für EPIC-07 (TD-008/TD-010), kein neuer Fund — reiner Hinweis.

## Tech-Debt-Einträge aus diesem Review

- `TD-008` (siehe `tech-debt.md`) — `GetViolationsScanner.cs:192` enthält noch das Wort „ehemalige 6-Parameter-Signatur zusammen" — gleichartige §5-Refactoring-Historie-Variante wie TD-001/TD-007, beim step-010-Grep über `Mcp/` im Sanierungs-Zug übersehen (Plan benannte nur `McpCodeGraphServerOptions.cs`).
