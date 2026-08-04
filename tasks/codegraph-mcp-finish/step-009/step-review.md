---
status: done
type: step-review
task: codegraph-mcp-finish
step: 009
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-04T11:30:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 009: `rules.json`-Auto-Discovery (B.1) + Verzeichnis-Sweep (B.2)

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — siehe Findings unten; Fix muss in `step-009/fix-XX/` nachgeholt werden
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: geprüft gegen `step-plan.md` Konkrete Änderungen (14 Touch-Points)
- [x] Rules-Konformität: `AiNetLinterRichtlinien.mdc` §1-5, `AiNetLinter.mdc` (Grenzwerte, Enforce*)
- [x] Logische Korrektheit: 3-Phasen-Refresh, Header-Platzierung, Edge-Cases
- [x] Konzept-Treue: B.1 + B.2 gegen `konzept.md` Muss-Haben B Punkte 1+2 + Non-Goals
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (1189/1189 in 2 m 31 s, kein TD-005-Flake)

## Befund

### Plan-Erfüllung

**13 von 14 Touch-Points umgesetzt** — Plan-Datei 9 (3 B.1-Unit-Tests) ist **nicht** umgesetzt:

- Datei 1: `SourceFileCatalog.IsGeneratedPath` `internal static` ✓ (SourceFileCatalog.cs:221 + XML-Doc-Erweiterung Z. 215-220)
- Datei 2: `McpServerCommand.TryResolveRulesJsonPath(string?, string)` ✓ (McpServerCommand.cs:67-79), `ResolveMaxLineCount`/`ResolveConfig` mit zusätzlichem optionalem `resolvedConfigPath` ✓ (Z. 90, 109), Stderr-Warnung in `RunAsync` Z. 36-40 ✓
- Datei 3: `McpCodeGraphServer.UsedDefaultConfig` ✓ (McpCodeGraphServer.cs:77), `RefreshStaleDocuments` delegiert an `McpCodeGraphServerRefresh.Run` (McpCodeGraphServer.cs:118-130)
- Datei 4: `McpCodeGraphServerOptions.UsedDefaultConfig` ✓ (McpCodeGraphServerOptions.cs:41)
- Datei 5: Konstruktor-Zuweisung ✓ (McpCodeGraphServer.cs:42)
- Datei 6: `RunAsync` reicht `UsedDefaultConfig: resolvedConfigPath is null` durch ✓ (McpServerCommand.cs:49)
- Datei 7: `GetViolationsScanner` Header-Zeile ✓ (GetViolationsScanner.cs:125-133)
- Datei 8: `GetViolationsTool` reicht `state.UsedDefaultConfig` durch ✓ (GetViolationsTool.cs:37)
- **Datei 9: 3 B.1-Unit-Tests ✗ FEHLEN KOMPLETT** (siehe Finding 1)
- Datei 10: 3 B.2-Unit-Tests ✓ (McpCodeGraphServerFileDiscoveryTests.cs, alle mit `[Trait("Category", "Unit")]`)
- Datei 11: 2 bestehende Tests kompatibel dank Default-Parameter ✓ (McpServerCommandTests.cs:301, 316, 331, 347)
- Datei 12-14: Doku-Updates in Docs/agent-api.md (Header-Klausel, Verzeichnis-Sweep-Erklärung, Default-Config-Markierungs-Abschnitt), Docs/integration.md (Auto-Discovery-Hinweis im MCP-Registrierungs-Absatz + cwd-Klärung), tasks/codegraph-mcp-finish/roadmap.md (EPIC-04 abgehakt) ✓ (Commit 677bef2)

**Commit-Subject/-Body passen** — Conventional Commit auf Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]` vorhanden, Body listet B.1 + B.2 + Records-Migration + `IsGeneratedPath`-Sichtbarkeit explizit auf.

**Coder-Doku-Inkonsistenz:** Commit-Message-Body behauptet „3 neue B.1-Unit-Tests in McpServerCommandTests" — `git show 1fd09c1 -- 'src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs'` zeigt **0 Zeilen Diff**, der letzte Commit auf dieser Datei ist `4f6fa6f` (perf-step). Die Tests existieren schlicht nicht. `step-result.md` übernimmt die Falschangabe und kombiniert sie mit „1192/1192 grün" — realer Volllauf reproduziert **1189/1189 grün** (1186 Baseline + 3 B.2, B.1 fehlt vollständig).

**Plausibilität der 4 dokumentierten Abweichungen:**
1. Parameter-Record-Migration (`MaxMethodParameterCount: 4` greift ab 5 Params, vorher schon bei 5 statt erst ab 6) — strukturell sauber, 12 Test-Dateien-Migration mechanisch.
2. Refresh-Klassen-Extraktion (`McpCodeGraphServer.cs` wäre sonst bei 262 LOC und AIContextFootprint 2534) — begründet, hält alle Limits ein.
3. Test-Umbenennung `_LogsWarnAndUsesDefault` → `_UsesDefault` ([WARN] in `RunAsync`, nicht im Helper) — verteidigbar, **wenn** der Test denn existieren würde.
4. `Docs/ROADMAP.md` → `tasks/codegraph-mcp-finish/roadmap.md` (Zeilenangabe im Plan veraltet) — korrekte Korrektur, das richtige Roadmap-File ist getroffen.

### Rules-Konformität

- **`AiNetLinterRichtlinien.mdc` §5 Zero-Warning-Direktive:** eingehalten — Build 0/0/0/10 s.
- **`AiNetLinter.mdc` Grenzwerte:** `MaxMethodParameterCount: 4` durchgehend (McpCodeGraphServerOptions.From, BuildViolationsTextAsync, Run = 4 Params ✓), `AIContextFootprint` durch Refresh-Extraktion unter 2500, `MaxCognitiveComplexity: 15` durch Phasen-Aufteilung in 4 private Methoden eingehalten.
- **`AiNetLinter.mdc` EnforceSealedClasses:** `McpCodeGraphServer` ✓ (McpCodeGraphServer.cs:24), `McpCodeGraphServerOptions` ✓ (record, sealed ✓), `McpCodeGraphServerOptionsFromParameters` ✓ (record, sealed ✓), `GetViolationsScannerParameters` ✓ (record, sealed ✓), `McpCodeGraphServerRefresh` static (kein sealed-Constraint), `McpFileState` `readonly record struct` (record-struct, kein Klassen-sealed relevant).
- **`AiNetLinter.mdc` EnforceNullableEnable:** `#nullable enable` am Dateianfang in `McpCodeGraphServer.cs:1`, `McpCodeGraphServerRefresh.cs:1`, `McpFileState.cs:1`, `McpCodeGraphServerOptions.cs:1`, `GetViolationsScanner.cs:1`, `GetViolationsTool.cs:1`, `McpServerCommand.cs:1`, `McpCodeGraphServerFileDiscoveryTests.cs:1` ✓.
- **`AiNetLinter.mdc` EnforceAsciiIdentifiers:** alle Bezeichner ASCII (TryResolveRulesJsonPath, McpFileState, UsedDefaultConfig etc.) ✓.
- **`AiNetLinterRichtlinien.mdc` §5 Verbot Task-/Planungsartefakt-Referenzen:** grenzwertig — die Begründungs-Kommentare in McpCodeGraphServer.cs:31-34 und McpCodeGraphServerOptions.cs:9-16 enthalten den Ausdruck „frueheren 5-Parameter-Konstruktor" (Refactoring-Historie im Sinne von §5, vgl. Verbot-Beispiel „war früher private") — siehe Finding 3.
- **`AiNetLinter.mdc` EnforceSealedClasses + EnforceSemanticNaming** ✓.
- **`AiNetLinterRichtlinien.mdc` §1 Grundprinzipien (monolithisch, statische Kompilierung, „Einfachheit vor Abstraktion"):** eingehalten — `TryResolveRulesJsonPath` ist reine Hilfsmethode, `McpCodeGraphServerRefresh` ist nicht über-abstrahiert (statisch, ohne DI/Interface), `Solution.AddDocument`/`RemoveDocument` sind Roslyn-Standard-API.
- **`AiNetLinterRichtlinien.mdc` §2 Architektur-Verbote (kein DI, kein Plugin, kein ALC):** eingehalten.
- **`AiNetLinterRichtlinien.mdc` §3 Windows-Shell/Prozess-Bereinigung:** vor Build geprüft (`Get-Process AiNetLinter,testhost` leer), Test-Logging via `TestResults/latest.trx` implizit.
- **`AiNetLinterRichtlinien.mdc` §4 Commit-Vorschlag-Pflicht, Doku-Pflicht:** Commit-Subject deutsch + Task-Suffix ✓, Doku-Commit getrennt ✓.

### Logische Korrektheit

- **`TryResolveRulesJsonPath`** (McpServerCommand.cs:67-79): logisch korrekt. Edge-Case `Path.GetDirectoryName` liefert `null`/`""` (z. B. wenn `solutionPath` ein reines Dateiname ohne Verzeichnisteil wäre) wird durch `string.IsNullOrEmpty(solutionDir)` korrekt abgefangen; bei gesetztem `configPath` wird die Existenzprüfung an `ConfigLoader.TryLoadConfig` delegiert (Plan-konform). Symlink-Edge-Cases sind hier irrelevant, weil `Path.GetDirectoryName` auf dem String operiert, nicht auf dem Filesystem.
- **`McpCodeGraphServerRefresh.Run` 3-Phasen-Logik** (McpCodeGraphServerRefresh.cs:31-43): Reihenfolge `Remove → Sweep → Refresh` ist konsistent mit der Plan-Code-Skizze. Phase 1 sammelt `removedIds` und entfernt aus `fileState`; Phase 2 fügt neue Dokumente via `FileTextLoader` (On-Read) hinzu, kein eager In-Memory-Kopieren — gute Roslyn-Konvention; Phase 3 nutzt `removedIds` als Skip-Menge, damit der gelöschte Document-Pfad nicht erneut verarbeitet wird.
- **Race-Condition „Datei wird gleichzeitig neu erstellt und modifiziert":** in Phase 2 wird nach `AddDocument` direkt `CacheInitialFileState` aufgerufen (McpCodeGraphServerRefresh.cs:144), wodurch `fileState[path]` mit mtime+Hash befüllt wird. Phase 3 sieht dann den gerade hinzugefügten Document, `TryGetValue` liefert true, der Mtime-Shortcut greift. Der von mir zunächst befürchtete NRE-Pfad in `TryApplyContentChange` (bei `known` null) ist nicht erreichbar, weil Phase 2 fileState garantiert bevorratet. **Kein Bug.**
- **`GetViolationsScanner.FormatReport`** (GetViolationsScanner.cs:125-133): Header-Zeile wird prependet **vor** dem bestehenden Lint-Header mit Leerzeile als Separator — semantisch korrekt platziert, `usedDefaultConfig` wirkt additiv. Im Edge-Case „keine Violations, aber usedDefaultConfig" wird die Header-Zeile ebenfalls ausgegeben, der bestehende `Keine Lint-Violations`-Pfad bleibt unverändert.
- **B.2-Unit-Tests** (McpCodeGraphServerFileDiscoveryTests.cs): die 3 Tests sind aussagekräftige Smoke-Tests gegen das `BaselineMiniFixtureWorkspace` — sie exerzieren den vollen Refresh-Pfad (`_ = server.GetCurrentSolution();` triggert InitializeFileState, dann Datei-Operation, dann zweiter `GetCurrentSolution()` triggert Refresh). Drei orthogonale Achsen (New-File, Deleted-File, Generated-File) korrekt abgedeckt. Die B.2-Tests laufen grün (separat verifiziert: 3/3 in 3 s).
- **B.1-Unit-Tests:** **fehlen vollständig** — der zentrale Helper `TryResolveRulesJsonPath` und die `UsedDefaultConfig`-Propagation haben null Regressionssicherung. Der `console.Errors`-Check für den `[WARN]`-Pfad in `RunAsync` ist ebenfalls ungetestet. Implizite Verifikation nur über `McpLiveRepositoryTests` (das gegen die echte `AiNetLinter.slnx` + deren `rules.json` läuft) — das deckt den Erfolgsfall, aber weder den `No-rules.json`-Pfad noch die Precedence-Logik (`--config` schlägt Auto-Discovery) ab. **Kritische Lücke.**

### Konzept-Treue (Ebene 4)

- **B.1 Vorgabe 1: „ohne `--config` neben der aufgelösten Solution-Datei nach `rules.json` suchen":** ✓ (McpServerCommand.cs:67-79)
- **B.1 Vorgabe 2: „keine gefunden → `[WARN]` auf stderr":** ✓ (McpServerCommand.cs:36-40)
- **B.1 Vorgabe 3: „und Vermerk in der `get_violations`-Antwort selbst":** ✓ (GetViolationsScanner.cs:125-133, Header-Zeile `Basis: Default-Regeln, keine rules.json gefunden`)
- **B.2 Vorgabe 1: „.cs-Dateien ohne zugehöriges Document einhängt":** ✓ (McpCodeGraphServerRefresh.cs:69-97, mit `IsGeneratedPath`-Filter)
- **B.2 Vorgabe 2: „Dokumente ohne existierende Datei entfernt":** ✓ (McpCodeGraphServerRefresh.cs:45-67)
- **B.2 bewusste Grenze „`<Compile Remove=…>`-Ausschlüsse werden nicht erkannt":** ✓ — explizit dokumentiert in der XML-Doc an `PickProjectForNewFile` (McpCodeGraphServerRefresh.cs:234-240), „best-effort" gekennzeichnet
- **Non-Goals** (konzept.md Z. 457-489): keine Editier-Tools, kein Embedding, kein Multi-Sprache-Support, kein Plugin, kein CLI-Batch-Mode-Replacement, keine Änderung an Testinhalten außerhalb des Scopes — alle eingehalten
- **DoD des Konzepts (konzept.md Z. 650-653):** „alle sieben Punkte aus Muss-Haben B sind umgesetzt, reviewt, mit Integrationstest abgesichert" — **B.1 ist nur teilweise abgesichert** (Produktion vorhanden + Doku, aber keine Unit-Tests für die zentrale Logik), B.2 ist vollständig abgesichert. Die anderen fünf Punkte (B.3-B.7) sind laut Roadmap für EPIC-05/06 eingeplant und nicht Scope dieses Steps.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx  → grün (0 Warnungen, 0 Fehler, 10.16 s)
dotnet test  AiNetLinter.slnx --no-build  → grün (1189/1189, 2 m 31 s, kein TD-005-Flake)
dotnet test  --filter "FullyQualifiedName~McpCodeGraphServerFileDiscovery"  → 3/3 grün (3 s)
```

Offene `AiNetLinter.exe`/`testhost.exe`-Prozesse: keine (vor Build verifiziert, `Get-Process` leer). Working-Tree: nur ` M .agents/rules/AiNetLinter.mdc` (siehe Tech-Debt-Hinweis unten — TD-006, nicht step-spezifisch).

## Findings

1. **`src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (gesamte Datei) — [CRITICAL] [Plan-Erfüllung + Konzept-Treue]** Plan-DoD Datei 9 verlangt 3 B.1-Unit-Tests für den Auto-Discovery-Pfad: `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered`, `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory`, `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault`. Diese Tests fehlen vollständig — verifiziert per `Select-String` über `src\AiNetLinter.Tests\**\*.cs` mit sieben unabhängigen Pattern (`TryResolveRulesJsonPath`, `UsedDefaultConfig`, `Basis: Default`, `AutoDiscoversRulesJson`, `NoRulesJsonFound`, `TakesPrecedenceOverAuto`, `McpServerCommandAutoDiscovery`): 0 Treffer projektweit. Der zentrale B.1-Helper (`TryResolveRulesJsonPath`, McpServerCommand.cs:67-79) und die `UsedDefaultConfig`-Propagation (McpCodeGraphServerOptions.cs:41 → McpCodeGraphServer.cs:77 → GetViolationsScanner.cs:50) sind komplett ungetestet. Der `console.Errors`-Check für den `[WARN]`-Pfad in `RunAsync` Z. 36-40 ist ebenfalls ungetestet. Implizite Coverage nur via `McpLiveRepositoryTests` — das deckt den Auto-Discovery-Success-Fall gegen die echte `AiNetLinter.slnx`+`rules.json`, aber weder den No-rules.json-Pfad noch die `--config`-Precedence-Logik noch die Header-Zeile. **Fix:** 3 Unit-Tests in `McpServerCommandTests.cs` (oder neue Datei `McpServerCommandAutoDiscoveryTests.cs`) hinzufügen gemäß Plan-Datei 9, jeweils mit `[Trait("Category", "Unit")]`; für den `[WARN]`-Pfad den `console.Errors`-Check im dritten Test beibehalten wie im Plan vorgeschlagen (Test-Umbenennung auf `_UsesDefault` aus dem Coder-step-result ist verteidigbar, aber den [WARN]-Check nicht zu testen ist eine zusätzliche Lücke).

2. **`tasks/codegraph-mcp-finish/step-009/step-result.md` — [MAJOR] [Plan-Erfüllung]** Behauptet „3 neue B.1-Unit-Tests in McpServerCommandTests" und „1192/1192 grün". Beide Behauptungen sind materiell falsch: (a) `git show 1fd09c1 -- 'src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs'` zeigt 0 Zeilen Diff (letzter Commit auf der Datei: `4f6fa6f` aus step-001/Perf-Phase); (b) realer Volllauf reproduziert 1189/1189 grün (1186 Baseline + 3 B.2-Tests, B.1-Tests fehlen). Die Commit-Message-Body (1fd09c1) übernimmt die Falschangabe identisch. Dies wiegt den Orchestrator in falscher Sicherheit über die DoD-Erfüllung. **Fix:** `step-result.md` korrigieren — entweder B.1-Tests nachreichen und Test-Anzahl korrigieren, oder, falls die Tests bewusst entfallen sollen, dies transparent dokumentieren und die Lücke in der `Bekannte Unschärfen`-Sektion vermerken.

3. **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:31-34` und `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs:9-16` — [MINOR] [Rules-Konformität §5]** Beide Begründungs-Kommentare enthalten den Ausdruck „frueheren 5-Parameter-Konstruktor" — das ist Refactoring-Historie im Sinne von §5 (das Verbots-Beispiel im Regeltext lautet explizit „war früher private", die Variante „ersetzt den frueheren 5-Parameter-Konstruktor" fällt darunter). Das forward-looking Rationale dahinter („Erlaubt additive P0/P1-Erweiterungen an der Config, ohne die Konstruktor-Signatur zu aendern") ist zulässig, das „frueheren" nicht. **Fix:** „Input-Record ersetzt den frueheren 5-Parameter-Konstruktor" → „Input-Record als Parameter-Object, damit `MaxMethodParameterCount: 4` eingehalten wird und kuenftige Config-Properties additiv wachsen koennen". Analog für `McpCodeGraphServerOptions.cs:9-16`.

## Sonstige Beobachtungen / MINOR / NITPICK

- **`src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs:211-217`** — `catch (IOException) { return false; }` in `TryApplyContentChange` ist ein stiller Catch mit `ainetlinter-disable EnforceNoSilentCatch`-Suppression. Die Begründung „der naechste Call liest die Datei ohnehin erneut" ist plausibel, aber inkonsistent zur Phase-2-Logik in `TryAddDocument` (Z. 147-151), die bei `IOException` einen `[WARN]` über `writeWarn` emittiert. Ein identisches `writeWarn($"[WARN]: Datei konnte beim Staleness-Check nicht gelesen werden ({path}): {ex.Message}");` würde die Suppression überflüssig machen und die 3-Phasen-Logik konsistenter machen. Funktional folgenlos (kein Regressions-Risiko), stilistisch lohnend.
- **Volllauf-Dauer:** 2 m 31 s für 1189 Tests — gegenüber step-008 (1185/1186 in 4-5 min, mit TD-005-Flake) praktisch unverändert. Der naive `Directory.EnumerateFiles(..., AllDirectories)`-Sweep bei jedem `GetCurrentSolution()`-Aufruf hat in dieser Last-Klasse (kleine `AiNetLinter.slnx` mit ~3.600 Zeilen) noch keinen messbaren Impact. Der Konzept-Hinweis auf EPIC-05/B.5 (Directory-`mtime`-Cache) bleibt valide.

## Tech-Debt-Einträge aus diesem Review

- Keine neuen Tech-Debt-Einträge.
- **TD-006 (BOM/CRLF in `.agents/rules/AiNetLinter.mdc`)** ist im Working-Tree weiterhin ungelöst — verifiziert: `git status --short` zeigt ` M .agents/rules/AiNetLinter.mdc`, `git diff` semantisch leer, `LF will be replaced by CRLF`-Warnung. Das ist bereits im Index dokumentiert, nicht step-spezifisch, nicht finding-relevant.
