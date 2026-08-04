---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 009/fix-01
title: "B.1-Unit-Tests nachreichen + step-result-Korrektur + Refactoring-Historie-Kommentare sanieren + stille Catch-Suppression entfernen (Fix für step-009-Review-Findings 1-3 + Code-Qualität)"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-04
related_to:
  - step-009/step-review.md  # Verdict: issues, 3 Findings + 1 Code-Qualität
  - step-009/step-plan.md    # Original-Scope, Datei 9 (Tests) und Datei 2/6 (TryResolveRulesJsonPath-Verdrahtung)
  - step-009/step-result.md  # zu korrigierende Test-Liste und Test-Anzahl
fix_for_step: 009
fix_number: 01
---

# Step 009/fix-01: B.1-Tests nachreichen, result.md synchronisieren, 2-Kommentar-Sanierungen

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-04` aus `tasks/codegraph-mcp-finish/roadmap.md` — Betriebsrisiko-Fixes (B.1 Auto-Discovery, B.2 Verzeichnis-Sweep). Der ursprüngliche step-009 hat 13 von 14 Touch-Points umgesetzt; Datei 9 (3 B.1-Unit-Tests) fehlt.
- **Konzept-Referenz:** `tasks/codegraph-mcp-finish/Konzept.md` „Muss-Haben B" Punkte **1** (Auto-Discovery) und **2** (Verzeichnis-Sweep), Zeile 190-217, sowie DoD Zeile 650-653 („alle sieben Punkte aus Muss-Haben B sind umgesetzt, reviewt, mit Integrationstest abgesichert" — B.1 ist nur teilweise abgesichert, dieser Fix schließt die Unit-Test-Lücke für den Auto-Discovery-Helper).
- **Review-Verdict:** `issues` mit 3 Findings (CRITICAL Plan-Erfüllung/Konzept-Treue für fehlende Tests, MAJOR Plan-Erfüllung für fehlerhafte step-result.md-Behauptungen, MINOR Rules §5 für Refactoring-Historie-Kommentare) plus 1 Code-Qualitäts-Beobachtung (stille `IOException`-Catch-Suppression in `McpCodeGraphServerRefresh.cs:211-217`).
- **Scope-Erweiterung (Nutzer-Hinweis 2026-08-04):** zusätzlich zu den 3 Review-Findings wird die im Review unter „Sonstige Beobachtungen" gelistete stille-Catch-Suppression mit eingeplant. Voraussetzungen geprüft: (a) der betroffene Code wurde in step-009 vom selben Coder gebaut, (b) `EnforceNoSilentCatch` ist eine explizite `agent-resilience`-Regel in `AiNetLinter.mdc` und ein dauerhaftes Linter-Anliegen, (c) keine Nutzer-Entscheidung nötig — die Sanierung ist mechanisch (Konsistenz mit der bereits existierenden `TryAddDocument`-Logik in derselben Datei), (d) Aufwand ≤ 1 Stunde (1 Zeile Catch-Body + Suppression-Kommentar entfernen, `writeWarn` durch 3 Methoden-Aufrufe threaden).
- **Nicht im Scope (explizit):** `roadmap.md` (Fix-Modus), `tech-debt.md`-Einträge TD-001..TD-006 (projektweit, nicht step-spezifisch), weitere Refactorings, Doku-Updates in `Docs/agent-api.md`/`Docs/integration.md`/`Docs/ROADMAP.md` (in step-009 bereits in Commit `677bef2` erledigt, kein erneuter Bedarf).

## Aktueller Projektzustand (JIT-Kontext)

Beim Code-Lesen am 2026-08-04 vorgefunden (Stand nach step-009-Code-Commit `1fd09c1`, Review-Commit `914e0ba`):

1. **`McpServerCommand.TryResolveRulesJsonPath` (McpServerCommand.cs:67-79):** `internal static string? TryResolveRulesJsonPath(string? configPath, string solutionPath)` ist sauber implementiert (3 Pfade: explizit → zurückgeben, Auto-Discovery via `Path.Combine(solutionDir, "rules.json")` + `File.Exists`, sonst `null`). **Signatur und Sichtbarkeit passen für direkten Test-Zugriff** — `InternalsVisibleTo("AiNetLinter.Tests")` ist in `Core/LinterEngine.cs:18` gesetzt, daher ist die Methode aus `AiNetLinter.Tests` ohne weitere Plumbing aufrufbar. Bestehende Tests rufen `McpServerCommand.ResolveMaxLineCount`/`ResolveConfig` (beide `internal static`) bereits direkt auf (McpServerCommandTests.cs:301, 316, 331, 347) — kein `InternalsVisibleTo`-Eintrag in `AiNetLinter.csproj` nötig (und auch nicht sinnvoll, da bereits global gesetzt).
2. **`McpServerCommandTests.cs` (359 LOC, Stand 2026-08-04):** trägt **keinen** Klassen-`[Trait("Category", "Unit")]`-Header. Enthält 4 `ResolveSolutionPathOrError_*`-Tests (mit `CreateTempDir`-Helper), 1 `TryLoadSolutionAsync_*`-Test, 5 `RunAsync_*`-E2E-Tests via `McpTestClient`-Fixture, 2 `ResolveMaxLineCount_*`-Tests und 2 `ResolveConfig_*`-Tests (alle mit `[Fact]`, ohne `Category`-Trait). Der `CreateTempDir`-Helper (Z. 353-358) ist `private static` und wiederverwendbar. Der `TestLintConsole` (in `AiNetLinter.Tests\Output\TestLintConsole.cs`) hat `Errors: List<string>` und `Output: List<string>` — der `console.Errors`-Check aus dem Plan-Datei 9 ist mit diesem Console-Mock sauber testbar.
3. **`McpCodeGraphServer.cs:31-34` (zu sanierender Kommentar):**
   ```csharp
   // Input-Record ersetzt den frueheren 5-Parameter-Konstruktor, der am
   // projektweiten MaxConstructorDependencies: 5-Limit lag (siehe
   // McpCodeGraphServerOptions.cs). Erlaubt additive P0/P1-Erweiterungen an der
   // Config, ohne die Konstruktor-Signatur zu aendern.
   ```
   Der Ausdruck „frueheren 5-Parameter-Konstruktor" ist Refactoring-Historie im Sinne von `AiNetLinterRichtlinien.mdc` §5 (Verbots-Beispiel „war früher private"); das forward-looking Rationale („Erlaubt additive P0/P1-Erweiterungen") ist zulässig und bleibt erhalten.
4. **`McpCodeGraphServerOptions.cs:9-16` (zu sanierender XML-Doc):** XML-Doc der Klasse (nicht der Factory) enthält dieselbe Refactoring-Historie. Der Factory-Block (Z. 43-49) und der `McpCodeGraphServerOptionsFromParameters`-Block (Z. 63-67) sind sauber — nur der Klassen-XML-Doc ist betroffen.
5. **`McpCodeGraphServerRefresh.cs:147-151` (Konsistenz-Vorbild für Patch 4):**
   ```csharp
   catch (IOException ex)
   {
       writeWarn($"[WARN]: Neue Datei konnte nicht einghaengt werden ({path}): {ex.Message}");
       return false;
   }
   ```
6. **`McpCodeGraphServerRefresh.cs:211-217` (Sanierungs-Ziel):**
   ```csharp
   // ainetlinter-disable EnforceNoSilentCatch — stillschweigend: Hash-Lese-Fehler beim
   // Staleness-Check duerfen den Server-Loop nicht abbrechen; der naechste Call liest
   // die Datei ohnehin erneut.
   catch (IOException)
   {
       return false;
   }
   ```
   `writeWarn` wird aktuell nur in `Run` (Z. 35) entgegengenommen und an `SweepForNewFiles`/`TryAddDocument` weitergereicht, **nicht** an `RefreshModifiedDocuments`/`TryRefreshDocument`/`TryApplyContentChange`. Sanierung erfordert `writeWarn` durch diese 3 Methoden zu threaden — die Datei hat bereits Methoden mit 5+ Parametern als `private static` (`TryAddDocument`: 5, `TryApplyContentChange`: 6) und der Build ist grün, also kein neuer Linter-Konflikt durch 1 zusätzlichen Parameter.
7. **`step-result.md:33` (zu korrigieren):** behauptet „3 neue B.1-Unit-Tests in `McpServerCommandTests.cs`" — nach diesem Fix wahr, da die Tests hinzugefügt werden. Inhalt der Zeile (Test-Namen, Umbenennungs-Begründung) bleibt korrekt.
8. **`step-result.md:53` (zu korrigieren):** behauptet „1192/1192 Tests, 2 m 44 s". Die Zahl 1192 ist nach dem Fix korrekt (1189 Baseline + 3 B.1 = 1192); die Dauer muss nach dem Volllauf re-evaluiert werden. Der Coder soll die tatsächliche Dauer eintragen.

## Intention

Nach diesem Fix ist der zentrale Auto-Discovery-Helper `TryResolveRulesJsonPath` regressionssicher getestet (3 neue Unit-Tests mit `[Trait("Category", "Unit")]` in der bestehenden `McpServerCommandTests.cs`): explizit-gesetztes `--config` schlägt Auto-Discovery, Auto-Discovery findet `rules.json` neben der Solution, kein `rules.json` → Default-Config + `[WARN]` auf stderr. Die `step-result.md`-Behauptungen werden mit der Realität synchronisiert (Test-Anzahl und Test-Liste). Zwei Begründungs-Kommentare werden von Refactoring-Historie auf forward-looking Rationale umgestellt (Rules §5-Konformität). Die stille `IOException`-Catch-Suppression in `McpCodeGraphServerRefresh.TryApplyContentChange` wird zugunsten einer konsistenten `[WARN]`-Emission (analog `TryAddDocument`) aufgelöst — kein Linter-Suppress mehr nötig, alle drei Refresh-Phasen verhalten sich bei `IOException` einheitlich.

## Konkrete Änderungen

### Patch 1 (Finding 1): `src\AiNetLinter.Tests\Commands\McpServerCommandTests.cs`

**Entscheidung Tests-Datei:** die 3 B.1-Tests werden in die bestehende `McpServerCommandTests.cs` eingefügt (nicht in eine neue `McpServerCommandAutoDiscoveryTests.cs`). Begründung:
- gleicher methodischer Stil wie die bereits existierenden `ResolveMaxLineCount_*`/`ResolveConfig_*`-Tests (Temp-Dir, `LinterArgs`, `[Fact]`, keine Fixture nötig);
- vorhandener `CreateTempDir`-Helper (Z. 353-358) ist direkt wiederverwendbar;
- keine neue Datei = keine zusätzliche Datei-Kopplung und kein zusätzlicher Boilerplate-Header;
- die B.2-Tests liegen in einer separaten Datei (`McpCodeGraphServerFileDiscoveryTests.cs`), weil sie `BaselineMiniFixtureWorkspace` und den vollen Server-Lifecycle benötigen — diese Begründung trifft auf die B.1-Tests nicht zu (sie testen reine Pfad-Auflösungs-Logik, keinen Server).

**Trait-Placement:** pro Test (`[Trait("Category", "Unit")]` direkt über jedem `[Fact]`), nicht am Klassen-Header. Begründung: die bestehende Klasse hat keinen Klassen-Trait, und ein am Klassen-Header hinzugefügter Trait würde alle bestehenden ~16 Tests in die `Category=Unit` einreihen (Scope-Drift — diese Tests sind heute trait-los und laufen in der Default-Kategorie).

**Test 1: `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered`**
- Arrange: Temp-Dir mit einer `Only.slnx` und einer `rules.json` direkt daneben (custom `MaxLineCount: 7`). Separater Pfad für `args.ConfigPath` zeigt auf eine andere `rules.json` mit `MaxLineCount: 5` (außerhalb des Temp-Dirs, um zu zeigen, dass die explizite Angabe Auto-Discovery aushebelt).
- Act: `McpServerCommand.TryResolveRulesJsonPath(args.ConfigPath, slnxPath)` aufrufen; danach `McpServerCommand.ResolveConfig(args, resolvedConfigPath)`.
- Assert:
  - `TryResolveRulesJsonPath` liefert den expliziten Pfad (nicht den daneben-gefundenen).
  - `ResolveConfig(...).Metrics.MaxLineCount == 5` (Wert aus dem expliziten Pfad, nicht aus dem neben-der-Solution-gefundenen).
  - Optional: direkter Call `McpServerCommand.ResolveConfig(args, resolvedConfigPath)` mit dem `resolvedConfigPath`-Wert aus dem vorigen Call liefert ebenfalls `MaxLineCount == 5`.

**Test 2: `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory`**
- Arrange: Temp-Dir mit `Only.slnx` + `rules.json` (`MaxLineCount: 11`). `args.ConfigPath = null`.
- Act: `McpServerCommand.TryResolveRulesJsonPath(null, slnxPath)`.
- Assert: liefert genau den erwarteten Pfad (full path zum `rules.json` neben der `.slnx`); zusätzlich `ResolveConfig(args, resolvedPath).Metrics.MaxLineCount == 11` (zeigt, dass der Auto-Discovery-Pfad in `ResolveConfig` korrekt ankommt).

**Test 3: `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault` (Test-Name aus Coder-step-result übernommen, da „verteidigbar" laut Review)**
- Diese Test-Logik testet **zwei** Aspekte in einem Test (atomar, kein Setup-Sharing möglich, da der `[WARN]`-Pfad nur durch den vollen `RunAsync`-Flow ausgelöst wird):
  - (a) `TryResolveRulesJsonPath` liefert `null` bei leerem `args.ConfigPath` und fehlender `rules.json` neben der `.slnx` → `ResolveConfig(args, null)` liefert Default-`MetricsConfig.MaxLineCount`.
  - (b) `McpServerCommand.RunAsync(args, ct, console)` emittiert den `[WARN]: Keine rules.json neben der Solution gefunden ...` auf stderr, wenn weder `--config` noch Auto-Discovery eine `rules.json` finden.
- Arrange: Temp-Dir mit `Only.slnx` (kein `rules.json`); `args.ConfigPath = null`; `TestLintConsole console = new()`; `CancellationTokenSource cts = new()` + `cts.Cancel()`.
- Act:
  1. `var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnxPath);` → `Assert.Null(resolved);`
  2. `var config = McpServerCommand.ResolveConfig(args, resolved);` → `Assert.Equal(new MetricsConfig().MaxLineCount, config.Metrics.MaxLineCount);`
  3. `await Assert.ThrowsAsync<OperationCanceledException>(async () => await McpServerCommand.RunAsync(args, cts.Token, console));` — der pre-cancelled Token lässt `TryLoadSolutionAsync(slnxPath, ct, ...)` mit `OperationCanceledException` werfen (siehe catch-Block in `TryLoadSolutionAsync` Z. 192, der `OperationCanceledException` **nicht** abfängt); `RunAsync` propagiert. Die `[WARN]`-Emission in `RunAsync` Z. 39 ist **vor** dem `TryLoadSolutionAsync`-Call und landet daher in `console.Errors` **bevor** die Exception geworfen wird.
- Assert: `Assert.Contains(console.Errors, e => e.Contains("[WARN]", StringComparison.Ordinal) && e.Contains("Keine rules.json neben der Solution gefunden", StringComparison.Ordinal));`

**Imports:** `AiNetLinter.Cli` (für `LinterArgs`) und `AiNetLinter.Configuration` (für `MetricsConfig`, `Config`) sind bereits im `using`-Block (Z. 8, 11); `System.Threading` für `CancellationTokenSource` ist ebenfalls schon importiert. **Keine neuen `using`-Direktiven nötig.**

**Hinweise für den Coder:**
- Das leere `Only.slnx` für Tests 1+2 darf nur eine leere Datei sein (kein XML-Inhalt nötig — `ResolveSolutionPathOrError` prüft nur `File.Exists`). Für Test 3 braucht es auch keine XML-Struktur, weil der pre-cancelled Token den Solution-Load gar nicht erst abschließt.
- Test 3 braucht `using System.Threading;` (bereits importiert) und `using System.Threading.Tasks;` (bereits importiert).
- `TestLintConsole` ist in `AiNetLinter.Tests\Output` — der bestehende `using AiNetLinter.Tests.Output;` (Z. 12) deckt das ab.
- `OperationCanceledException` liegt in `System` (global using via `<ImplicitUsings>enable</ImplicitUsings>`).
- Test 1 und Test 2 nutzen die identische `CreateTempDir`-/`try`/`finally`-/`Directory.Delete`-Struktur wie die bestehenden `ResolveConfig_*`-Tests — mechanisch 1:1 übernehmen.

### Patch 2 (Finding 2): `tasks\codegraph-mcp-finish\step-009\step-result.md`

**Korrekturen:**

- **Z. 33** (Auflistung der B.1-Tests): inhaltlich korrekt nach Patch 1 (die 3 Tests existieren dann tatsächlich). **Keine Text-Änderung nötig**, nur die Doku-Pflicht via Doku-Commit erledigt (commit-message bestätigt die Existenz).
- **Z. 53** (Test-Output): Zahl `1192/1192` bleibt korrekt (1186 Baseline + 3 B.2 + 3 B.1 = 1192). **Dauer** `2 m 44 s` muss nach dem Volllauf des Fixes re-evaluiert und durch die tatsächliche Dauer ersetzt werden (typisch +10-20 s für 3 zusätzliche reine Unit-Tests ohne Subprozess, also realistisch 2 m 50 s — 3 m 00 s). Coder-Vorgehen: nach Patch 1 `dotnet test AiNetLinter.slnx --no-build` ausführen, Dauer stoppen, in Z. 53 eintragen.
- **Z. 53 (Ergänzung):** Test-Anzahl-Begründung hinzufügen, damit künftige Reviewer die Herkunft nachvollziehen können: `1192/1192 Tests (= 1186 Baseline + 3 B.2-Unit-Tests + 3 B.1-Unit-Tests, alle aus dem vorherigen fix-01-Roundtrip)`.
- **Commit-Reference-Block (Z. 39-47):** der `code_commit_hash: 1fd09c1` bleibt der Code-Commit aus step-009. Dieser Fix-Step bringt einen **eigenen** Code-Commit (für Patch 1, 3, 4) und einen **eigenen** Doku-Commit (für Patch 2). Der `code_commit_hash` in `step-result.md` Z. 12 bezieht sich auf den ursprünglichen step-009 und bleibt unverändert; der neue Commit-Hash wird in `fix-01/step-result.md` dokumentiert (außerhalb dieses Plans).

### Patch 3 (Finding 3): `src\AiNetLinter\Mcp\McpCodeGraphServer.cs:31-34` und `src\AiNetLinter\Mcp\McpCodeGraphServerOptions.cs:9-16`

**Korrektur McpCodeGraphServer.cs:31-34** (4-zeiliger `//`-Kommentar ersetzen):

```csharp
// Input-Record als Parameter-Object, damit MaxConstructorDependencies: 5 eingehalten wird
// und kuenftige Config-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur
// zu aendern.
```

(Wegfall: „ersetzt den frueheren 5-Parameter-Konstruktor, der am … lag" und „(siehe McpCodeGraphServerOptions.cs)" — die Begründung wird forward-looking, ohne Verweis auf einen früheren Zustand. Die „additiv wachsen"-Formulierung aus der Originalversion bleibt, weil sie das Warum der Record-Wahl klar macht.)

**Korrektur McpCodeGraphServerOptions.cs:9-16** (Klassen-XML-Doc ersetzen):

```csharp
/// <summary>
/// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Kapselt die Optionen in einem
/// Record, damit <c>MaxConstructorDependencies: 5</c> eingehalten wird und kuenftige
/// Konfigurations-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur zu
/// aendern.
/// </summary>
```

(Wegfall: „Eingefuehrt als Ersatz fuer den frueheren 5-Parameter-Konstruktor, der am projektweiten MaxConstructorDependencies: 5-Limit (siehe AiNetLinter.mdc) exakt angelangt war — jede weitere P0/P1-Erweiterung am Konstruktor haette den Build gebrochen." Der XML-Doc wird kürzer und behält die Kern-Aussage: Record dient der Einhaltung des Constructor-Dependencies-Limits und der additiven Erweiterbarkeit.)

**Wichtig:** die Factory-XML-Doc (Z. 43-49) und der `McpCodeGraphServerOptionsFromParameters`-XML-Doc (Z. 63-67) sind **nicht** betroffen (enthalten kein „frueheren"-Wort). Nur die **Klassen-**-XML-Doc von `McpCodeGraphServerOptions` und der `//`-Kommentar in `McpCodeGraphServer.cs` sind anzufassen.

**Hinweis zur Regel-Referenz:** der Review-Vorschlag nannte `MaxMethodParameterCount: 4` — das ist die **falsche** Regel. Der `McpCodeGraphServerOptions`-Record wird über seinen **Konstruktor** instanziiert (mit 5 `init`-Properties, die als Dependencies zählen), nicht über eine Methode mit Parameterliste. Die korrekte Regel ist `MaxConstructorDependencies: 5` aus `AiNetLinter.mdc` Z. 27. Der Fix verwendet daher `MaxConstructorDependencies: 5` in beiden Kommentaren.

### Patch 4 (Code-Qualität): `src\AiNetLinter\Mcp\McpCodeGraphServerRefresh.cs:211-217`

**Schritt 4.1: `writeWarn` durch die Aufrufkette threaden.**

- **`Run` (Z. 31-43):** Signatur bleibt unverändert (`writeWarn` ist bereits Parameter).
- **`RefreshModifiedDocuments` (Z. 99-115):** Signatur um `Action<string> writeWarn` erweitern (5. Parameter, vorher 5 mit `ref bool`, jetzt 5 mit `ref bool` + 1 = 6, also 1 zusätzlicher Parameter); Aufruf in `Run` Z. 41 entsprechend erweitern: `RefreshModifiedDocuments(ref updated, solutionDir, removedIds, fileState, writeWarn, ref anyChanged);`
- **`TryRefreshDocument` (Z. 172-187):** Signatur um `Action<string> writeWarn` erweitern (4. Parameter); Aufruf in `RefreshModifiedDocuments` Z. 112 entsprechend: `if (TryRefreshDocument(document, ref updated, fileState, writeWarn)) anyChanged = true;`
- **`TryApplyContentChange` (Z. 189-218):** Signatur um `Action<string> writeWarn` erweitern (7. Parameter); Aufruf in `TryRefreshDocument` Z. 186 entsprechend: `return TryApplyContentChange(document, path, currentMtime, known, ref updated, fileState, writeWarn);`

**Hinweis Parameter-Count:** die Datei hat bereits 5- bis 6-Param-Methoden als `private static` (`TryAddDocument`: 5 Parameter, `TryApplyContentChange`: 6 Parameter), und der Build ist grün. Das Hinzufügen von `writeWarn` bringt `TryApplyContentChange` auf 7 Parameter — konsistent zum bestehenden Muster. Sollte wider Erwarten `MaxMethodParameterCount: 4` für `private static` greifen (laut Codebasis-Pattern offenbar nicht), ist der Fallback: lokale Helper-Methode `EmitReadWarn(string path, IOException ex, Action<string> writeWarn)` extrahieren, die der `TryApplyContentChange`-Catch-Block ruft — das hält den Parameter-Count stabil. **Coder prüft das beim ersten Build und entscheidet.**

**Schritt 4.2: `catch`-Body und Kommentar in `TryApplyContentChange` (Z. 211-217) ersetzen.**

```csharp
catch (IOException ex)
{
    writeWarn($"[WARN]: Datei konnte beim Staleness-Check nicht gelesen werden ({path}): {ex.Message}");
    return false;
}
```

(Wegfall: 3-zeiliger `// ainetlinter-disable EnforceNoSilentCatch — stillschweigend: ...`-Kommentar. Der Catch-Body emittiert jetzt eine `[WARN]`-Zeile, identisches Muster zu `TryAddDocument` Z. 147-151. Suppression ist nicht mehr nötig, weil `EnforceNoSilentCatch` durch die `writeWarn`-Emission erfüllt ist.)

## Tests

- [ ] **Build grün mit 0 Warnungen** (Zero-Warning-Direktive, `AiNetLinterRichtlinien.mdc` §5) — `dotnet build AiNetLinter.slnx`.
- [ ] **Volllauf grün** (erwartete Test-Anzahl nach Patch 1: 1192 = 1186 Baseline + 3 B.2 + 3 B.1) — `dotnet test AiNetLinter.slnx --no-build`. Falls TD-005-Flake auftritt: dreimaliger Re-Run, bei wiederholtem Auftreten als `infrastructure` klassifizieren (siehe Bekannte Ausnahmen), keine Code-Änderung.
- [ ] **B.1-Unit-Tests** (Patch 1) — `dotnet test --filter "FullyQualifiedName~ResolveConfig"`, 5 Tests grün (2 bestehende + 3 neue). Optional zur Fokussierung: `dotnet test --filter "FullyQualifiedName~ResolveConfig_NoExplicitConfigPath"`, 2 Tests grün (Tests 2 + 3).
- [ ] **Patch-3-Konformität:** nach Build `dotnet run --project src\AiNetLinter -- --config rules.json --path .` → 0 Violations auf eigenem Code (verifiziert, dass die neuen Kommentare die Linter-Regel `EnforceNoSilentCatch` etc. nicht unbeabsichtigt triggern).
- [ ] **Patch-4-Konformität:** `EnforceNoSilentCatch` greift nicht mehr auf `TryApplyContentChange` (verifiziert über dieselbe `dotnet run`-Selbst-Lint-Probe). Die `ainetlinter-disable`-Suppression wurde entfernt; das Lint-Ergebnis ist die strukturelle Verifikation.
- [ ] **Vor jedem Build/Test:** offene `AiNetLinter.exe`/`testhost.exe`-Prozesse prüfen und ggf. beenden (Konzept-Warnung, `tasks\codegraph-mcp-finish\roadmap.md` Tech-Stack-Notiz).

## Definition of Done

- [ ] Patch 1 umgesetzt: 3 B.1-Unit-Tests in `src\AiNetLinter.Tests\Commands\McpServerCommandTests.cs` (bestehende Datei), jeweils mit `[Trait("Category", "Unit")]`, Test-Namen wie oben, Test-Bodies wie in Patch-1-Skizze.
- [ ] Patch 2 umgesetzt: `step-result.md` Z. 53 Dauer re-evaluiert und korrekt eingetragen, Test-Anzahl-Begründung als Inline-Kommentar ergänzt.
- [ ] Patch 3 umgesetzt: `McpCodeGraphServer.cs:31-34` und `McpCodeGraphServerOptions.cs:9-16` mit forward-looking Kommentaren (kein „frueheren 5-Parameter-Konstruktor" mehr).
- [ ] Patch 4 umgesetzt: `McpCodeGraphServerRefresh.TryApplyContentChange` (Z. 211-217) emittiert konsistenten `[WARN]` analog `TryAddDocument`; `ainetlinter-disable EnforceNoSilentCatch`-Suppression entfernt; `writeWarn` durch `RefreshModifiedDocuments` → `TryRefreshDocument` → `TryApplyContentChange` durchgereicht.
- [ ] Build grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors`).
- [ ] Volllauf grün (1192 Tests erwartet, ggf. TD-005-Flake als infrastructure dokumentiert).
- [ ] Selbst-Lint grün: `dotnet run --project src\AiNetLinter -- --config rules.json --path .` → 0 Violations auf eigenem Code (verifiziert die Sanierung der `EnforceNoSilentCatch`-Suppression und die Kommentar-Korrekturen).
- [ ] Code-Commit (Conventional Commit auf Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]`), z. B. `fix(mcp): b.1-unit-tests-nachreichen-und-stille-catch-suppression-entfernen [codegraph-mcp-finish]`.
- [ ] Doku-Commit (nur `step-result.md`-Korrektur aus Patch 2), z. B. `docs(task): step-009-test-anzahl-und-liste-synchronisieren [codegraph-mcp-finish]`.
- [ ] `fix-01/step-result.md` geschrieben mit: Build-/Test-Output (Anzahl, Dauer), Commit-Hashes, Verweis auf die 4 Patches, ggf. TD-005-Flake-Status.
- [ ] Status in `fix-01/step-plan.md` von `open` auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Zero-Warning, **Verbot von Refactoring-Historie in Code-Kommentaren**, Verbot von Task-/Planungsartefakt-Referenzen, Clean Code) — direkt relevant für Patch 3 (Refactoring-Historie „frueheren 5-Parameter-Konstruktor" → forward-looking Rationale).
- `.agents/rules/AiNetLinter.mdc` `EnforceNoSilentCatch` (Zeile 13 + Zeile 53) — direkt relevant für Patch 4 (stille Catch-Suppression entfernen, konsistente `[WARN]`-Emission).
- `.agents/rules/AiNetLinter.mdc` `MaxConstructorDependencies: 5` (Zeile 27) — relevant für Patch 3 (richtige Regel-Referenz in den korrigierten Kommentaren, nicht `MaxMethodParameterCount: 4`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (xUnit v3, `Category=Unit` für Logik-Tests, Doku-Update-Pflicht) — relevant für Patch 1 (`[Trait("Category", "Unit")]` pro Test) und Patch 2 (Doku-Commit für `step-result.md`-Synchronisation).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion) — Patch 1 nutzt keine neue Fixture/Abstraktion, sondern den bestehenden `CreateTempDir`-Helper und die direkte `internal static`-Methode (kein `InternalsVisibleTo`-Workaround, kein `InternalsVisibleTo`-Eintrag in `AiNetLinter.csproj`, kein `Reflection`-Hack).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 (Windows-Shell, Test-Logging via `TestResults/latest.trx`, Prozess-Bereinigung) — Build/Test-Workflow wie in step-008/009.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 Verbot Symptom-Fixing — Patch 4 saniert die Ursache (fehlende `[WARN]`-Emission), nicht das Symptom (Linter-Warnung). Patch 1 saniert die Ursache (fehlende Regressions-Tests), nicht das Symptom (Reviewer-Finding).

## Bekannte Ausnahmen

- **TD-005 (Last-Flake in `McpServerCommandErrorHandlingTests`):** kann unter Volllauf-Last weiterhin 1-2 Failures am `SubprocessConcurrencyGate.AcquireAsync`-Timeout produzieren. Falls der Volllauf dadurch nicht grün wird: dreimaliger Re-Run, bei wiederholtem Auftreten als **infrastructure** klassifizieren, nicht eigenhändig fixen (Scope-Drift). Im `fix-01/step-result.md` unter „Bekannte Unschärfen" vermerken. Die 3 neuen B.1-Tests sind reine Synchron-Tests ohne Subprozess, daher sind sie **kein** TD-005-Risiko.
- **TD-001..TD-004, TD-006 (nicht in diesem Step-Scope):** projektweite Tech-Debt-Einträge, die in `tech-debt.md` dokumentiert sind. Fix-Modus berührt sie nicht. TD-006 (`IsGeneratedPath`-Duplikation) ist in step-009 bewusst nicht angefasst worden (B.2 nutzt den bereits in `SourceFileCatalog` vorhandenen Filter via minimaler Sichtbarkeits-Erweiterung); der `McpCodeGraphServerRefresh.cs`-Code in Patch 4 berührt `IsGeneratedPath` nicht.
- **Coder-Test-Umbenennung `_LogsWarnAndUsesDefault` → `_UsesDefault`:** vom Review als „verteidigbar" eingestuft. Patch 1 behält den Coder-Namen `_UsesDefault` für die Test-Konsistenz mit der bereits committeden `step-result.md` (Z. 33, die den umbenannten Namen dokumentiert), behält aber den ursprünglich geplanten `console.Errors`-Check für den `[WARN]`-Pfad. Der `[WARN]`-Check kommt in denselben Test, nicht in einen vierten Test — das vermeidet Test-Fragmentierung.
- **Patch-4-Parameter-Count:** falls wider Erwarten `MaxMethodParameterCount: 4` für `private static`-Methoden greift (bisheriges Codebasis-Pattern zeigt, dass es nicht greift — `TryApplyContentChange` hat schon 6 Parameter im grünen Build), Fallback: kleine Helper-Methode `EmitReadWarn(string path, IOException ex, Action<string> writeWarn)` extrahieren, damit der Catch-Block nur 2 Methoden-Aufrufe enthält und der Parameter-Count stabil bleibt. Coder entscheidet nach erstem Build.

## Code-Skizze (optional)

**Patch 1, Test-Skelett (zur Illustration der 3 Test-Bodies):**

```csharp
[Fact]
[Trait("Category", "Unit")]
public void ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered()
{
    var tempDir = CreateTempDir();
    try
    {
        var slnx = Path.Combine(tempDir, "Only.slnx");
        File.WriteAllText(slnx, "");
        File.WriteAllText(Path.Combine(tempDir, "rules.json"),
            """{ "Global": {}, "Metrics": { "MaxLineCount": 7 } }""");

        // Separate rules.json ausserhalb des Temp-Dirs, das args.ConfigPath zeigt darauf.
        var explicitDir = CreateTempDir();
        try
        {
            var explicitConfig = Path.Combine(explicitDir, "explicit.json");
            File.WriteAllText(explicitConfig,
                """{ "Global": {}, "Metrics": { "MaxLineCount": 5 } }""");

            var args = new LinterArgs { ConfigPath = explicitConfig, TargetPath = slnx, Verbose = false };

            var resolved = McpServerCommand.TryResolveRulesJsonPath(args.ConfigPath, slnx);
            var config = McpServerCommand.ResolveConfig(args, resolved);

            Assert.Equal(explicitConfig, resolved);
            Assert.Equal(5, config.Metrics.MaxLineCount);
        }
        finally
        {
            Directory.Delete(explicitDir, recursive: true);
        }
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
[Trait("Category", "Unit")]
public void ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory()
{
    var tempDir = CreateTempDir();
    try
    {
        var slnx = Path.Combine(tempDir, "Only.slnx");
        File.WriteAllText(slnx, "");
        var rulesPath = Path.Combine(tempDir, "rules.json");
        File.WriteAllText(rulesPath,
            """{ "Global": {}, "Metrics": { "MaxLineCount": 11 } }""");

        var args = new LinterArgs { ConfigPath = null, TargetPath = slnx, Verbose = false };

        var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnx);
        var config = McpServerCommand.ResolveConfig(args, resolved);

        Assert.Equal(rulesPath, resolved);
        Assert.Equal(11, config.Metrics.MaxLineCount);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}

[Fact]
[Trait("Category", "Unit")]
public async Task ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault()
{
    var tempDir = CreateTempDir();
    try
    {
        var slnx = Path.Combine(tempDir, "Only.slnx");
        File.WriteAllText(slnx, "");

        var args = new LinterArgs { ConfigPath = null, TargetPath = slnx, Verbose = false };

        var resolved = McpServerCommand.TryResolveRulesJsonPath(null, slnx);
        var config = McpServerCommand.ResolveConfig(args, resolved);

        Assert.Null(resolved);
        Assert.Equal(new AiNetLinter.Configuration.MetricsConfig().MaxLineCount, config.Metrics.MaxLineCount);

        // [WARN]-Emission in RunAsync verifizieren: pre-cancelled Token laesst
        // TryLoadSolutionAsync OperationCanceledException werfen, BEVOR der Server-Loop startet;
        // die [WARN]-Zeile in Z. 39 ist davor emittiert worden.
        var console = new TestLintConsole();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await McpServerCommand.RunAsync(args, cts.Token, console));

        Assert.Contains(console.Errors,
            e => e.Contains("[WARN]", StringComparison.Ordinal)
              && e.Contains("Keine rules.json neben der Solution gefunden", StringComparison.Ordinal));
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

**Patch 4, Catch-Sanierung in `TryApplyContentChange` (Z. 211-217):**

```csharp
catch (IOException ex)
{
    writeWarn($"[WARN]: Datei konnte beim Staleness-Check nicht gelesen werden ({path}): {ex.Message}");
    return false;
}
```

**Patch 3, Kommentar-Korrektur-Vorlage:**

```csharp
// In McpCodeGraphServer.cs (Z. 31-34):
// Input-Record als Parameter-Object, damit MaxConstructorDependencies: 5 eingehalten wird
// und kuenftige Config-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur
// zu aendern.
```

```csharp
// In McpCodeGraphServerOptions.cs (Z. 9-16, Klassen-XML-Doc):
/// <summary>
/// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Kapselt die Optionen in einem
/// Record, damit <c>MaxConstructorDependencies: 5</c> eingehalten wird und kuenftige
/// Konfigurations-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur zu
/// aendern.
/// </summary>
```

## Notes

- **Scope-Disziplin:** die 4 Patches sind die einzigen Inhalte. Tech-Debt-Einträge TD-001..TD-006 sind **nicht** Scope dieses Fixes (sie sind projektweit, nicht step-spezifisch). Der Review-Verweis auf TD-005 (Last-Flake) ist in „Bekannte Ausnahmen" aufgenommen, nicht als Patch — das wäre Scope-Drift.
- **Weniger Fragmentierung:** alle 4 Patches werden in einem einzigen Code-Commit + einem Doku-Commit zusammengefasst. Test-Erstellung, Kommentar-Sanierung und Catch-Sanierung berühren unterschiedliche Dateien, gehören aber konzeptuell zusammen (alle drei sind „step-009-Findings-Sanierung"). Ein zweiter Code-Commit nur für Patch 3 + 4 wäre Mini-Step-Fragmentierung.
- **Doku-Pflicht für `Docs/*.md`:** kein erneuter Bedarf — die in step-009/Commit `677bef2` bereits aktualisierten `Docs/agent-api.md`, `Docs/integration.md` und `tasks/codegraph-mcp-finish/roadmap.md` bleiben inhaltlich korrekt. Nur `step-009/step-result.md` (Patch 2) wird korrigiert, das ist die einzige Doku-Änderung.
- **Kein Push:** wie in step-008/009-Konvention, lokale Commits nur — der Orchestrator entscheidet über Push nach `approved`.
- **Kein Vorausplanen weiterer Fix-Steps:** dieser Plan deckt exakt Runde 1/3 (`max_fix_rounds_per_step: 3`). Sollte der Reviewer nach diesem Fix weitere Findings haben, wird `fix-02` geplant (Orchestrator-Aufruf).
- **Patch-1 Test-Granularität bewusst Unit:** keine Subprozesse, keine `McpTestClient`-Fixture, keine MSBuild-Workspace-Initialisierung — die reine Pfad-Auflösungs-Logik braucht keinen End-to-End-Roundtrip. Der implizite Dogfooding-Lauf in `McpLiveRepositoryTests` bestätigt den Erfolgsfall gegen die echte `AiNetLinter.slnx`+`rules.json`; die 3 Unit-Tests ergänzen die expliziten Precedence- und Fail-Pfade.
- **Patch-4 Konsistenz-Begründung:** der Review-Hinweis nennt die Inkonsistenz zu `TryAddDocument` explizit als Sanierungs-Motivation. Beide Methoden sind im selben File, führen `Solution`-IO durch, und sollen sich bei identischem Fehler-Typ (`IOException`) identisch verhalten — sonst entsteht ein Coder-Drift-Risiko bei künftigen Erweiterungen (z. B. wenn ein dritter Aufrufer dazukommt und `writeWarn` nicht konsistent durchgereicht wird).
