---
status: done (pending audit)
type: step-result
task: codegraph-mcp-finish
step: 009/fix-01
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-04
code_commit_hash: 60429e2
status_after: done
blocker_category: n/a
---

# Result Step 009/fix-01: B.1-Tests nachreichen, result.md synchronisieren, 2-Kommentar-Sanierungen

## Zusammenfassung

Patch 1 umgesetzt: 3 B.1-Unit-Tests in der bestehenden `McpServerCommandTests.cs` ergänzt — `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered`, `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory` und `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault`, jeweils mit `[Trait("Category", "Unit")]`. Patch 3 umgesetzt: die Refactoring-Historie-Kommentare in `McpCodeGraphServer.cs` (Konstruktor-Begründung) und `McpCodeGraphServerOptions.cs` (Klassen-XML-Doc) auf forward-looking Rationale umgestellt. Patch 4 umgesetzt: die stille `IOException`-Catch-Suppression in `McpCodeGraphServerRefresh.TryApplyContentChange` zugunsten einer konsistenten `[WARN]`-Emission aufgelöst, `writeWarn` durch `RefreshModifiedDocuments` und `TryRefreshDocument` durchgereicht. Patch 2 (Doku-Korrektur `step-009/step-result.md`: Dauer `2 m 44 s` → `2 m 33 s`, Test-Anzahl-Begründung als Inline-Kommentar) wird im separaten Doku-Commit nachgereicht.

## Geänderte Dateien

- `src\AiNetLinter.Tests\Commands\McpServerCommandTests.cs` — 3 neue B.1-Unit-Tests mit `[Trait("Category", "Unit")]`:
  - `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAutoDiscovered` — explizites `--config` schlägt Auto-Discovery.
  - `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonInSolutionDirectory` — Auto-Discovery findet `rules.json` neben der Solution.
  - `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault` — kein `rules.json` → Default-Config + `[WARN]`-Emission in `console.Errors`.
- `src\AiNetLinter\Mcp\McpCodeGraphServer.cs` — 3-zeiliger `//`-Kommentar am Konstruktor (Z.31-34) auf forward-looking Rationale umgestellt („Input-Record als Parameter-Object, damit `MaxConstructorDependencies: 5` eingehalten wird und kuenftige Config-Properties additiv wachsen koennen, ohne die Konstruktor-Signatur zu aendern."). Wegfall der Refactoring-Historie „ersetzt den frueheren 5-Parameter-Konstruktor" (Rules §5-Verbot).
- `src\AiNetLinter\Mcp\McpCodeGraphServerOptions.cs` — Klassen-XML-Doc (Z.9-16) analog saniert. Wegfall der „Eingefuehrt als Ersatz fuer den frueheren 5-Parameter-Konstruktor"-Formulierung; Kern-Aussage (Record dient der Einhaltung des Constructor-Dependencies-Limits und der additiven Erweiterbarkeit) bleibt erhalten.
- `src\AiNetLinter\Mcp\McpCodeGraphServerRefresh.cs` — `writeWarn` durch `RefreshModifiedDocuments` und `TryRefreshDocument` durchgereicht; `try/catch (IOException)` aus `TryApplyContentChange` in `TryRefreshDocument` hochgezogen (Catch-Block emittiert jetzt inline einen `[WARN]` analog `TryAddDocument`); `ainetlinter-disable EnforceNoSilentCatch`-Suppression ersatzlos entfernt. Zusätzlich: `ex` → `ignored` in `EnumerateCsFilesSafe` Catch-Block, weil der Linter diese pre-existing `EnforceNoSilentCatch`-Violation nach dem Datei-Refactor erstmals flaggte (siehe Abweichungen).

## Commit

- **Code-Commit-Hash:** `60429e2`
- **Message:**
  ```
  fix(mcp): b.1-unit-tests-nachreichen-und-stille-catch-suppression-entfernen [codegraph-mcp-finish]

  Refs: tasks/codegraph-mcp-finish/step-009/fix-01
  Refs: tasks/codegraph-mcp-finish/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx  → grün (0 Warnungen, 0 Fehler, 1.47s inkrementell)
dotnet test  AiNetLinter.slnx --no-build  → grün (1192/1192 Tests, 2 m 33 s)
dotnet test  --filter "FullyQualifiedName~ResolveConfig"  → 5/5 grün (311 ms; 2 bestehende + 3 neue B.1-Tests)
dotnet run --project src\AiNetLinter -- --config rules.json --path .  → grün (0 Violations auf eigenem Code)
```

## Abweichungen vom Plan

- **Patch 4, Schritt 4.1: try/catch aus `TryApplyContentChange` in `TryRefreshDocument` hochgezogen** (statt `writeWarn` als 7. Parameter durch `TryApplyContentChange` zu threaden und eine `EmitReadWarn`-Helper-Methode zu extrahieren, wie im Plan als Fallback vorgeschlagen). Grund: das Threading von `writeWarn` als 7. Parameter hätte `MaxMethodParameterCountForNonPublic: 6` aus `AiNetLinter.mdc` Z. 22 verletzt; der Linter hat das beim ersten Build verifiziert (`Die Methode 'TryApplyContentChange' hat 7 Parameter, davon 7 gewertet (erlaubt sind maximal 6)`). Das Hochziehen des try/catch hält `TryApplyContentChange` bei 6 Parametern und der Catch-Block in `TryRefreshDocument` emittiert die `[WARN]`-Zeile inline (gleiches Muster wie `TryAddDocument` Z.147-151), so dass die `EnforceNoSilentCatch`-Regel auch ohne Helper-Methode erfüllt ist. Plan-konform: der Plan genehmigt explizit den Helper-Fallback („Coder prüft das beim ersten Build und entscheidet"); das Hochziehen ist eine gleichwertige Variante ohne 7. Parameter.
- **Patch 4, Zusatz: `ex` → `ignored` in `EnumerateCsFilesSafe` Catch-Block** (war nicht im Plan, ergibt sich aus der `Aufräumen erlaubt`-Regel in `AiNetLinterRichtlinien.mdc` §5). Grund: nach dem Datei-Refactor flaggte der Linter erstmals die pre-existing `EnforceNoSilentCatch`-Violation in `EnumerateCsFilesSafe` (Z.227, `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { yield break; }`). Auf dem unveränderten Original-Stand der Datei (vor meinen Patches) war diese Stelle noch nicht geflaggt — der Linter hat sie durch die Re-Analyse nach der Datei-Änderung gefunden. Minimale Sanierung gemäß Linter-Empfehlung: `Exception ex` → `Exception ignored` (Linter akzeptiert den Variablennamen `ignored` als explizit gewolltes Ignorieren). Der `when`-Filter verwendet `ignored` weiterhin, das `yield break` bleibt unverändert.
- **Patch 1, Test 3: try/catch statt `Assert.ThrowsAsync<OperationCanceledException>`.** Grund: `MSBuildWorkspace.OpenSolutionAsync` prüft einen pre-cancelled Token nicht upfront und returnt im Test-Environment (kein stdin) normal mit Exit-Code 0, statt `OperationCanceledException` zu werfen. Der Plan ging davon aus, dass der pre-cancelled Token sich durch `TryLoadSolutionAsync` propagiert (siehe catch-Block Z.192 mit `when (ex is not OperationCanceledException)`); das ist im Test-Setup nicht reproduzierbar, weil `OpenSolutionAsync` den Token ignoriert und der Server ohne stdin sofort wieder beendet wird. Lösung: `try { await RunAsync(...) } catch (OperationCanceledException) { }` + separate Assertion auf `console.Errors`. Die [WARN]-Emission in `RunAsync` erfolgt synchron vor dem Solution-Load und ist daher in beiden Fällen (Throw oder Return) bereits in `console.Errors`.

## Beobachtungen

- **Test-3-Pattern „synchron-emittierter `[WARN]` + pre-cancelled Token + stderr-Check"** ist ein nützliches Idiom für künftige Tests, die die `ILintConsole`-Channel-Trennung verifizieren. Der `try/catch` statt `Assert.ThrowsAsync` ist robuster gegenüber `MSBuildWorkspace`-Interna, die nicht im Test-Scope liegen. Könnte als Vorlage für weitere `RunAsync_*`-Tests dienen, die eine stderr-Emission vor dem Solution-Load prüfen.
- **`MaxMethodParameterCountForNonPublic: 6` ist die relevante Regel für private static Helper-Methoden** in dieser Codebasis (nicht `MaxMethodParameterCount: 4`, das nur für public Methoden gilt). Der Plan erwähnte „Sollte wider Erwarten `MaxMethodParameterCount: 4` für `private static` greifen" — das ist eine ungenaue Formulierung; tatsächlich greift die relaxierte Variante `MaxMethodParameterCountForNonPublic: 6` (siehe `MetricsConfig.cs` Z. 46). Künftige Schritte, die Parameter durch private Helper-Methoden threaden, sollten diese relaxierte Grenze beachten.
- **Linter-Re-Analyse nach Datei-Änderungen** kann pre-existing Violations erstmals sichtbar machen. Der `EnumerateCsFilesSafe`-Catch war im Original-Build OK, wurde aber nach dem Refactor geflaggt. Das ist kein Linter-Bug, sondern Folge der Re-Analyse; die `Aufräumen erlaubt`-Regel (`AiNetLinterRichtlinien.mdc` §5) deckt die Sanierung im selben Zug ab.
- **3 B.1-Tests benötigen keine `McpTestClient`-Fixture** (anders als die 5 bestehenden `RunAsync_*`-E2E-Tests in derselben Datei). Die reine Pfad-Auflösung und der `[WARN]`-Emissions-Pfad sind deterministisch genug für einen Unit-Test, was die Test-Laufzeit signifikant verkürzt (~310 ms für alle 3 + 2 bestehenden). Bestätigt die Granularitäts-Entscheidung im Plan: Synchron-Unit-Tests wo möglich, Subprozess-Tests nur wo nötig.

## Bekannte Unschärfen

- **TD-005-Last-Flake ist im Volllauf nicht aufgetreten** (1192/1192 grün in 2 m 33 s, kein `SubprocessConcurrencyGate.AcquireAsync`-Timeout im TRX). Das ist positiv, aber wie schon im step-009-step-result vermerkt kein Beweis, dass der Flake weg ist. Bei der nächsten Last-Fixture (B.3 in EPIC-05) vermutlich wieder reproduzierbar, dann wie gehabt als `infrastructure` klassifizieren.
- **Der `RunAsync`-Test 3 verlässt sich auf das Verhalten von `MSBuildWorkspace.OpenSolutionAsync`** (ignoriert pre-cancelled Token und returnt ohne Exception, wenn kein stdin verfügbar ist). Falls eine zukünftige Roslyn-Version das Verhalten ändert (z. B. durch früheren Token-Check), könnte der Test brechen. In diesem Fall wäre `Assert.ThrowsAsync<OperationCanceledException>` wieder die korrekte Form. Aktueller Stand: Test grün, 311 ms Laufzeit, deterministisch.

## Modell-Info

- `coded_by_model: claude-sonnet-5`
- `coded_by_model_knowledge_cutoff: 2026-01`
