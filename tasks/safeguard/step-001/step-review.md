---
status: done
type: step-review
task: safeguard
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-06T14:10:00+02:00
verdict: issues
tech_debt_ids: [TD-001]
---

# Review Step 001: SafeguardScanner mit deterministischer Score-Berechnung

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Fix-Step `step-001/fix-<XX>` mit Fix-Plan nötig
- [ ] **blocked** — Nutzer-Entscheidung nötig

**Begründung:** Sechs explizite Rules-Verletzungen im Produktionscode
(`SafeguardScanner.cs`), die alle in der `step-plan.md` §"Rules-Refs"
kuratierten Regel-Liste explizit aufgeführt sind. Build (`dotnet build`)
und Tests (`dotnet test`) sind grün, aber `dotnet build` führt den
AiNetLinter-Linter nicht aus — `dotnet run --project src/AiNetLinter --
--config rules.json --path .` zeigt 7 Verstöße in der Scanner-Datei, von
denen 6 in den Plan-zitierten Regeln liegen. Kein Logikfehler, keine
verfehlte Konzept-Anforderung — ausschließlich Regel-Konformität auf
Ebene 2.

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [ ] Rules-Konformität: `<rules_dir>/**` eingehalten — **6 Verstöße** (siehe Findings)
- [x] Logische Korrektheit: Code macht was er soll, Tests aussagekräftig
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md`
- [x] Build: selbst nachgeprüft, grün (`dotnet build` → 0 Warnungen, 0 Fehler)
- [x] Tests: selbst nachgeprüft, grün (`dotnet test --filter FullyQualifiedName~SafeguardScannerTests` → 13/13, `dotnet test --filter Category=Unit` → 141/141)
- [x] Linter: selbst nachgeprüft, **7 Verstöße in `SafeguardScanner.cs`** — davon 6 in den Plan-Rules-Refs

## Befund

### Plan-Erfüllung

- Datei `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (neu, 413 Zeilen) — erstellt, enthält `internal static class SafeguardScanner`, `ComputeScoreAsync`, `BuildScoreResult`, `BuildRemediation`, sechs `internal sealed record`s (Parameters, Score, ViolationEntry, Remediation, Result, ScannedClass).
- Datei `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` (neu, 396 Zeilen) — erstellt, 13 xUnit-v3-Tests, deckt leere Solution / einzelne Violation / hoher Score / niedriger Score / Threshold-Logik / Determinismus / Malfunction / Remediation-Fallback / Remediation-Empty / Clamp / Parameter-Defaults / Parameter-Overrides / KnownFixture ab.
- Build: `dotnet build` → 0 Warnungen, 0 Fehler (eigenständig nachgeprüft).
- Tests: `dotnet test --filter FullyQualifiedName~SafeguardScannerTests` → 13/13 grün, `dotnet test --filter Category=Unit` → 141/141 grün (eigenständig nachgeprüft).
- Code-Commit `afb6146`: Conventional-Commit-Format auf Deutsch, imperativ, Subject `feat(mcp): SafeguardScanner mit deterministischer Score-Berechnung [safeguard]` (66 Zeichen inkl. `[safeguard]`, unter dem 72-Zeichen-Limit), `Refs: tasks/safeguard/step-001` im Body, `### Commit-Vorschlag`-Block vorhanden.
- Doku-Commit `1bb5f99`: trägt `Ref Code-Commit: afb6146` und verweist auf den Code-Hash.
- Abweichung 1: `BuildRemediation` nimmt `IReadOnlyList<ViolationEntry>` statt `IReadOnlyCollection<RuleViolation>` (Plan-Skizze). Begründung stichhaltig — Re-Mapping wäre redundant. **Akzeptiert.**
- Abweichung 2: Internes `ScannedClass`-Record statt `IReadOnlyCollection<INamedTypeSymbol>`. Begründung stichhaltig — entkoppelt `BuildScoreResult` von Roslyn-Symbols für isolierte Tests. **Akzeptiert.**
- Abweichung 3: `ViolationPenaltyUnit` von 0.1 auf 1.5 angehoben, im Commit-Body dokumentiert. Plan erlaubt Anpassung in §"Bekannte Ausnahmen". Begründung nachvollziehbar (1 Error = 3.0 Penalty → Score 7.0 < 8.0). **Akzeptiert.**

### Rules-Konformität

Der Plan listet in §"Rules-Refs" die explizit zu prüfenden Regeln. Der
AiNetLinter-Linter (eigenständig ausgeführt) meldet **7 Verstöße in
`src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs`**, davon **6 in den
Plan-zitierten Regeln**:

| # | Zeile | Regel (Plan-Refs) | Verstoß | Soll-Zustand |
|---|---|---|---|---|
| 1 | 123 | `MaxMethodParameterCount=4` | `BuildScoreResult` hat 5 Parameter, Limit 4 | ≤ 4 (Parameter-Object-Record) oder 5–6 nur falls `MaxMethodParameterCountForNonPublic: 6` angewendet wird — der Linter wendet es hier nicht an |
| 2 | 311 | `MaxCognitiveComplexity=15` | `EnumerateConcreteClasses` hat CC = **33** (>2× Limit) | ≤ 15 (Extract Method) |
| 3 | 311 | `MaxCyclomaticComplexity=12` | `EnumerateConcreteClasses` hat CC = **16** | ≤ 12 (Extract Method, Guard Clauses) |
| 4 | 321 | `BanBlockingTaskAccess` | `.GetAwaiter().GetResult()` blockierender Task-Zugriff | `await` verwenden, oder `// ainetlinter-disable BanBlockingTaskAccess` mit Begründung (Pattern: `McpCodeGraphServer.cs:73`) |
| 5 | 345 | `BanBlockingTaskAccess` | `.GetAwaiter().GetResult()` zweiter blockierender Task-Zugriff | wie #4 |
| 6 | 327 | `EnforceNoSilentCatch` | `catch { continue; }` — leerer Catch-Block (Plan-Begründung: „kein leerer Catch") | `catch (Exception ignored)` mit Begründung im Variablennamen, oder explizite `OperationCanceledException`-Re-Throw-Logik, oder `// ainetlinter-disable EnforceNoSilentCatch` mit Begründung |

**Außerhalb der Plan-Rules-Refs (in `rules.json` global aktiv, aber nicht
vom Planer zitiert):**

| # | Zeile | Regel | Verstoß | Anmerkung |
|---|---|---|---|---|
| 7 | 285 | `MaxSwitchArms=10` | `ResolveHintForRule` Switch-Expression hat 11 Arms | Plan-Skizze sah explizit eine `IReadOnlyDictionary<string, string>`-Lookup-Tabelle vor; Coder hat Switch statt Dictionary implementiert. Zählt formell nicht zu den Plan-Rules-Refs, ist aber eine Plan-Abweichung (Mapping-Tabelle) und eine globale Regel-Verletzung. |

**Plan-Abweichung ohne Linter-Flag:** Der Plan
`step-plan.md` §"Konkrete Änderungen" verlangte für `BuildRemediation`
explizit „Mapping-Tabelle als `static IReadOnlyDictionary<string, string>`
mit den ~10 häufigsten RuleNames". Die Coder-Umsetzung ist ein
Switch-Expression mit 11 Arms — kein Dictionary. Funktional gleichwertig,
aber von der Plan-Vorgabe abweichend. Begründung nicht im
`step-result.md` dokumentiert. Da die Plan-Vorgabe nicht zu den
zitierten Rules-Refs gehört, fließt das nicht in den Verdict ein, ist
aber im Fix-Step zu beachten.

**Eingehaltene Plan-Rules-Refs (verifiziert):**

- `MaxLineCount=500` → 413 Zeilen ✓
- `MaxMethodLineCount=60` → `ComputeScoreAsync` ≈ 34 Z., `BuildScoreResult` ≈ 45 Z., `BuildRemediation` ≈ 29 Z., `EnumerateConcreteClasses` ≈ 52 Z., `BuildScannedClass` ≈ 20 Z., alle privaten Helper ≤ 25 Z. ✓
- `AIContextFootprint=2500` → kein PathOverride nötig (Plan-Vermutung bestätigt; Linter meldet hier keine Verletzung)
- `EnforceNullableEnable` → `#nullable enable` in Z.1 ✓
- `EnforceSealedClasses` → `SafeguardScanner` ist `static class` (implizit sealed), Records sind `internal sealed record` ✓
- `EnforceAsciiIdentifiers` → keine Umlaute in Bezeichnern; Kommentare in ASCII-Transliteration (`fuehrt`, `zugehoerigen`, `Komplexitaet`) ✓
- `EnforcePascalCase` → alle öffentlichen Typen/Properties PascalCase ✓
- `BanAsyncVoid` → keine `async void`-Methoden ✓
- `DetectAndBanPhantomDependencies` → alle `using`s auflösbar (Build grün) ✓
- `AvoidExcessiveMiddleMen` → Scanner hat eigene Berechnungslogik, nicht reine Middleman-Klasse ✓
- `AiNetLinterRichtlinien.mdc` §1 (monolithisch/statisch): `internal static class`, kein DI/ALC/Plugin ✓
- §2 (Architektur-Verbote): keine ALC/Plugin/DI-Maschinerie ✓
- §4 (xUnit v3): `public sealed class SafeguardScannerTests`, keine zwangsserialisierende Collection ✓
- §5 (Result-Pattern, Zero-Warning, sparsame Kommentare): `SafeguardScoreResult` mit `IsMalfunction`/`Context` statt Exception, keine Task-/Step-/TD-/EPIC-Referenzen im Code (verifiziert per `grep`), Records `internal sealed record` ✓

### Logische Korrektheit

- Score-Formel (`BuildScoreResult`, Z.135–137): `raw = 10.0 - violationPenalty - ccPenalty - footprintPenalty + sealedBonus`, `Math.Clamp(raw, 0, 10)`, `passed = score >= threshold` — entspricht der Plan-Spezifikation und Konzept §"Wie" (Formel-Skizze). ✓
- Violation-Penalty (`ComputeViolationPenalty`, Z.205–227): kumuliert `ViolationErrorSeverity`/`ViolationWarningSeverity`/`ViolationInfoSeverity` × `ViolationPenaltyUnit` (1.5). Coder-Anpassung von 0.1 → 1.5 nachvollziehbar (1 Error = 3.0 Penalty → Score 7.0, sonst wären 20 Errors für den Threshold-Bruch nötig — unplausibel). Plan §"Bekannte Ausnahmen" erlaubt die Anpassung explizit. ✓
- CC-Penalty (`ComputeCcPenalty`, Z.229–235): `avgCc = classes.Average(c => c.MaxCognitiveComplexity)`, Overage gegen `Metrics.MaxCognitiveComplexity`. Plan-Vorgabe „Mittelwert der MaxCognitiveComplexity" korrekt umgesetzt. ✓
- Footprint-Penalty (`ComputeFootprintPenalty`, Z.237–243): analog, gegen `Metrics.MaxAIContextFootprint`. ✓
- Sealed-Bonus (`ComputeSealedBonus`, Z.245–252): nur Klassen (in `EnumerateConcreteClasses` werden `TypeKind.Class && !IsAbstract` gefiltert — Records, Interfaces, Structs, Enums sind korrekt ausgeschlossen), Quote `(sealedCount / classes.Count)`, Bonus nur wenn `config.Global.EnforceSealedClasses = true`. ✓
- Threshold-Logik: `passed = score >= threshold` (Z.137), durch `ComputeScoreAsync_ThresholdLogic_ScoreEqualToThreshold_Passes` verifiziert (Z.152–174). ✓
- Determinismus (`ComputeScoreAsync_Determinismus_ZweiLaufeIdentischerScore`, Z.177–197): vergleicht `Score`, `Summary`, `Violations.Count` und den geordneten `(FilePath:LineNumber:RuleName)`-Schlüssel zwischen zwei Läufen. Sortierung in `BuildScoreResult` Z.142–155: `OrderBy(SeverityRank)` → `ThenBy(FilePath, OrdinalIgnoreCase)` → `ThenBy(LineNumber)` → `ThenBy(RuleName, OrdinalIgnoreCase)` — stabil, deterministisch. ✓
- Tests aussagekräftig: Synthetic-`AdhocWorkspace` (Z.346–368) ist sauber, `ThrowingTextLoader` (Z.388–395) ist Pattern-konsistent zu `GetViolationsToolTests` (Z.161–173), `NullConsole` (Z.375–380) entkoppelt den Test von Konsolen-Output. ✓
- Edge-Cases abgedeckt: leere Solution (Z.36–49), leere Violations-Liste im `BuildRemediation` (Z.260–268), Score < 0 (Clamp-Test, Z.271–310), unbekannter RuleName (Z.240–257), Threshold = 0 (Z.152–174). ✓
- `ResolveSeverity` (Z.262–266): Reihenfolge `EffectiveSeverity` → `RuleRegistry.TryResolve` → Default `"warning"` — konsistent mit `GetViolationsScanner.ResolveSeverity` (Z.170–174). ✓

### Konzept-Treue (Ebene 4)

- **Muss-Haben Punkt 4** (deterministische Score-Berechnung): ✓ verifiziert per Determinismus-Test.
- **Muss-Haben Punkt 5** (Score-Komponenten: Violations + CC + Footprint + Sealed-Quote): ✓ alle vier Komponenten umgesetzt.
- **Muss-Haben Punkt 6** (Remediation-Generator pro Violation-Typ kontextspezifisch): ✓ `ResolveHintForRule` mit 10 benannten Regeln + Default-Fallback für unbekannte RuleNames.
- **Muss-Haben Punkt 8** (10+ Unit-Tests): ✓ 13 Tests, davon 5+ für Score-Berechnung (EmptySolution, SingleViolation, KnownFixture, HighScore, LowScore, ThresholdLogic, Determinismus, Clamp).
- **Wie Schritt 1** (Scanner als reine Funktion, ohne MCP-Abhängigkeiten): ✓ — Datei ist tool-frei, delegiert nur an `LinterEngine`.
- **Non-Goals respektiert:** Kein mutable Server-State, kein Auto-Apply, kein Cloud-Storage, kein HTML/Mermaid, keine Coverage-Integration. ✓
- **Wo im Projekt** (nur 2 neue Dateien für EPIC-01): ✓ — `SafeguardScanner.cs` + `SafeguardScannerTests.cs`, keine erweiterten Dateien angefasst.
- **Konzept §"Wo im Projekt" — „Nicht angefasst (bewusst)":** `McpToolResults`, `LinterEngine`, `McpSufficiencyHints`, andere `*ToolRegistrations.cs` — alle unangetastet. ✓

### Build-/Test-Status

```
dotnet build                                  → grün (0 Warnungen, 0 Fehler)
dotnet test --filter FullyQualifiedName~SafeguardScannerTests  → 13/13 grün
dotnet test --filter Category=Unit            → 141/141 grün
dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache
                                               → 7 Verstöße in SafeguardScanner.cs (siehe Findings)
```

**Linter-Hinweis:** `dotnet build` allein führt den AiNetLinter-Linter
nicht aus. Der Coder hat nur `dotnet build` und `dotnet test`
verifiziert — die Plan-Sektion "Tests" listete diese beiden Commands
auch nicht weiter. Das ist ein Plan-Lücke (kein Lint-Command
gefordert), aber die Linter-Verstöße sind in den Plan-Rules-Refs
verankert und müssen im Fix-Step behoben werden. Der Coder hätte
proaktiv den Linter laufen sollen — der Linter-Pfad ist im AGENTS.md
`AiNetLinterRichtlinien.mdc §3` als `dotnet run --project src/AiNetLinter
-- --config rules.json --path .` Standard-Pattern etabliert.

## Findings (issues)

1. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:123` — [MAJOR] [Rules] `MaxMethodParameterCount` (Plan-Rules-Refs) verletzt: `BuildScoreResult` hat 5 Parameter, erlaubt sind 4. **Fix:** `sealed record BuildScoreResultParameters(IReadOnlyCollection<RuleViolation> Violations, IReadOnlyList<ScannedClass> Classes, Config Config, double Threshold, int MaxRemediationEntries)` einführen und Methoden-Signatur auf `(BuildScoreResultParameters p)` reduzieren (Parameter-Object-Pattern, konsistent mit `SafeguardScannerParameters`); Records sind nach Plan §"Bekannte Ausnahmen" vom Limit ausgenommen.

2. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:311` — [MAJOR] [Rules] `MaxCognitiveComplexity=15` (Plan-Rules-Refs) verletzt: `EnumerateConcreteClasses` hat CC = **33** (>2× Limit). **Fix:** Methode in benannte Hilfsmethoden aufteilen — vorgeschlagene Extraktionen: `TryCompileProject(Project, ct)` (beinhaltet Compilation + `try/catch`), `ShouldIncludeDocument(Document, Project, scopeFilter)` (Scope-Filter-Logik), `TryGetClassDeclaration(ClassDeclarationSyntax, SemanticModel, ct, out INamedTypeSymbol?)` (Symbol-Resolution), `CollectClassDeclarations(SyntaxNode, SemanticModel)` (Schleife). Hilfsmethoden-Signaturen mit CC ≤ 8 anstreben.

3. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:311` — [MAJOR] [Rules] `MaxCyclomaticComplexity=12` (Plan-Rules-Refs) verletzt: `EnumerateConcreteClasses` hat CC = **16**. **Fix:** wird mit Finding #2 durch Extraktion behoben.

4. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:321` — [MAJOR] [Rules] `BanBlockingTaskAccess` (Plan-Rules-Refs) verletzt: `.GetAwaiter().GetResult()` blockierender Task-Zugriff. **Fix:** `EnumerateConcreteClasses` zu `async Task<...>` umwandeln und `await project.GetCompilationAsync(ct)` / `await document.GetSyntaxTreeAsync(ct)` verwenden. Aufrufstelle in `ComputeScoreAsync` (Z.113) entsprechend auf `await EnumerateConcreteClassesAsync(...)` anpassen. Alternative: `// ainetlinter-disable BanBlockingTaskAccess` mit Begründung (Präzedenz: `McpCodeGraphServer.cs:73`), aber nur falls die Refactoring-Async-Propagation außerhalb des Step-Scopes liegt (eigene Bewertung des Planers).

5. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:345` — [MAJOR] [Rules] `BanBlockingTaskAccess` (Plan-Rules-Refs) verletzt: zweiter blockierender Task-Zugriff. **Fix:** wird mit Finding #4 durch `async`-Migration behoben.

6. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:327` — [MAJOR] [Rules] `EnforceNoSilentCatch` (Plan-Rules-Refs) verletzt: leerer `catch { continue; }` in `EnumerateConcreteClasses`. Plan §"Konkrete Änderungen" verlangt explizit „kein leerer Catch". **Fix:** `catch (Exception ignored)` mit Begründungs-Kommentar, dass Compilation-Fehler per Design nicht zum Abbruch führen sollen (Linter erkennt `ignored` als explizit gewolltes Ignorieren — siehe Linter-Doku Z.124ff im Output). Alternative: `catch (Exception ex) when (ex is not OperationCanceledException)` + Re-Throw — dann aber den Empty-Catch-Fall vorher über `if (compilation is null) continue;` abfangen.

**Zusätzliche Beobachtung ohne Verdict-Wirkung** (in den Plan-Rules-Refs
nicht enthalten, aber Plan-Abweichung):

7. `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs:285` — `ResolveHintForRule` Switch-Expression mit 11 Arms verletzt `MaxSwitchArms=10` (global aktiv in `rules.json`, aber **nicht** in den Plan-Rules-Refs zitiert). Zusätzlich weicht das Pattern vom Plan ab: `step-plan.md` §"Konkrete Änderungen" verlangt explizit „Mapping-Tabelle als `static IReadOnlyDictionary<string, string>`". **Fix-Empfehlung:** Dictionary-Lookup mit `TryGetValue` und Default-Fallback einbauen — reduziert die Switch-Arms auf 0 und entspricht der Plan-Vorgabe. Wenn im Fix-Step ohnehin Refactoring ansteht, mit-erledigen.

## Sonstige Beobachtungen / MINOR / NITPICK

- **Verdict-bezogene Anmerkung (kein neuer Fund):** Der Coder hat nur `dotnet build` + `dotnet test` verifiziert, nicht den AiNetLinter-Linter. Der Plan-Sektion "Tests" hatte keinen Lint-Command aufgeführt — formell also kein Coder-Fehler. Empfehlung an Planer für künftige Steps: `dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache` als DoD-Command in der "Tests"-Sektion aufnehmen, da Linter-Verstöße in den Plan-Rules-Refs verankert sind und sonst erst im Kritiker-Review auffallen.
- **Coder-Beobachtung `EnumerateConcreteClasses` ist `private` (nicht `internal`):** aktuell ok, weil kein externer Test `EnumerateConcreteClasses` direkt aufruft. Falls EPIC-02/EPIC-03 die Methode promotions-auf `internal` benötigt, dann nachziehen.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — `GetViolationsScannerTests.cs` fehlt; Scanner-Logik nur indirekt über `GetViolationsToolTests` getestet. Coder hat im `step-result.md` §"Beobachtungen" korrekt dokumentiert, dass das außerhalb des EPIC-01-Scopes liegt, und das `SafeguardScannerTests.cs` als Pattern-Vorbild etabliert. Mittlere Priorität — ein eigenes kleines Epic „Scanner-Tests für Bestandsscanner" in `roadmap.md` ist sinnvoll, sobald EPIC-02/EPIC-03 stabile Tool-Wrapper-Layer etabliert haben.
