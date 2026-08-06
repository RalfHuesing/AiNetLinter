---
status: open
type: step-plan
task: safeguard
step: 001
fix: 01
epic: EPIC-01
title: "SafeguardScanner — Linter-Verstöße beheben (Parameter-Record, Extract-Method, Async-Migration, Dictionary-Lookup, Catch-Name)"
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-06T15:45:00+02:00
last_updated: 2026-08-06T15:45:00+02:00
related_to:
  - tasks/safeguard/step-001/step-review.md
---

# Step 001 / fix-01: Linter-Verstöße in SafeguardScanner beheben

## Bezug

- **Task:** `safeguard`
- **Bezugs-Step:** `step-001` (Status `done (fix-01 pending)`)
- **Step-Review-Verdict:** `issues` — 6 MAJOR-Findings auf Ebene 2 (Rules-Konformität) in `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs`, alle in `step-plan.md` §"Rules-Refs" kuratiert
- **Zusätzlich adressiert:** Beobachtung 7 (Plan-Abweichung `MaxSwitchArms=10`) — siehe "Bekannte Ausnahmen / Entscheidungen" unten für die Scope-Begründung
- **Konzept-Referenz:** keine Änderung an Konzept-Anforderungen — funktionaler Scope von EPIC-01 bleibt unangetastet (Score-Formel identisch, Records identisch, Test-Set identisch, nur Refactoring)

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der Live-Dateien vorgefunden — beeinflusst den Plan direkt:

- **`SafeguardScanner.cs` (Stand: Commit `afb6146`, 413 Zeilen):** enthält 6 Linter-Verstöße exakt an den vom Reviewer benannten Zeilen (123, 285, 311, 321, 327, 345). Der Linter (`dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache`) zeigt 7 Verstöße in dieser einen Datei (siehe `tasks/safeguard/lint-output-step-001.txt`); der 7. ist Beobachtung 7 (Z.285). Build (`dotnet build`) ist grün, alle 13 SafeguardScanner-Tests sind grün, alle 141 Unit-Tests sind grün.
- **`SafeguardScannerParameters`-Pattern (Z.393-400 derselben Datei):** `internal sealed record` mit 7 Feldern wird bereits verwendet, um `ComputeScoreAsync` an `MaxMethodParameterCount=4` vorbeizuführen. Derselbe Pattern passt für `BuildScoreResultParameters` — Records sind per `AiNetLinterRichtlinien.mdc` §1 + `AiNetLinter.mdc` aus dem `MaxMethodParameterCount`-Limit ausgenommen.
- **Test-Aufrufstellen von `BuildScoreResult` (test-Datei Z.282-287 und Z.302-307):** Beide Tests rufen die Methode mit benannten Argumenten auf — bei der Umstellung auf Parameter-Record muss die Aufruf-Signatur angepasst werden. Andere Tests rufen nur `ComputeScoreAsync` (async, signature-stabil) oder `BuildRemediation` (signature-stabil) auf, daher keine breitere Test-Kaskade.
- **Async-Kontext:** `ComputeScoreAsync` ist bereits `internal static async Task<SafeguardScoreResult>` (Z.83) und `await`et `LinterEngine.RunAsync` (Z.105) — Async-Propagation für `EnumerateConcreteClasses` ist eine in-place-Umstellung, **kein** Cascade über die Klasse hinaus: `BuildScoreResult` (Z.123, sync) nimmt weiterhin `IReadOnlyList<ScannedClass>`, Records bleiben unverändert, EPIC-02 (Tool-Wrapper, noch nicht implementiert) wird `ComputeScoreAsync` ohnehin `await`en, weil es bereits `Task<…>` zurückgibt.
- **Catch-Pattern-Kontext:** `EnumerateConcreteClasses` hat in Z.323-326 bereits einen separaten `catch (OperationCanceledException) { throw; }` — das ist korrekt. Der `catch { … }` bei Z.327-332 fängt *alles andere* mit `continue`. `Exception ignored` ist die Linter-konforme Benennung dieses Catch-Alls ohne Verhaltensänderung.
- **Switch-Pattern im Plan:** `step-001/step-plan.md` Z.181-192 verlangte explizit „Mapping-Tabelle als `static IReadOnlyDictionary<string, string>`". Coder hat stattdessen ein Switch-Expression gebaut (Funktional gleichwertig, aber Switch-Arms = 11 > Limit 10) — die Dictionary-Variante ist die Plan-konforme Form und reduziert die Linter-Verstöße um 1.
- **Konzept-Anforderungen:** Bewusst **nicht** angetastet — Score-Formel (`BuildScoreResult` Z.135-137), Sortierung (Z.142-146), Threshold-Logik, Remediation-Generator-Semantik und Records bleiben Bit-identisch zu Commit `afb6146`. Der Fix-Step ist rein mechanisches Refactoring zur Linter-Konformität.

## Intention

Nach diesem Fix-Step ist `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` **linter-konform** (0 Rules-Verstöße in der Datei) und behält gleichzeitig volle Funktions- und Test-Kompatibilität: alle 13 SafeguardScanner-Tests grün, alle 141 Unit-Tests grün, Build grün, Score-Formel und öffentliche API identisch zu Commit `afb6146`. Die 6 MAJOR-Findings des Reviews + Beobachtung 7 sind behoben; Verhalten des Scanners ist Bit-identisch (Sortierung, Remediation-Output, Catch-Verhalten).

## Konkrete Änderungen

Alle Änderungen konzentrieren sich auf `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs`, plus zwei Aufrufstellen in `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` (für den Parameter-Record-Umbau von `BuildScoreResult`).

### Änderung 1 — Finding #1: `BuildScoreResult` Parameter-Object-Record (Z.123)

- **Datei:** `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Z.118-167)
- **Was:** Neuen `internal sealed record BuildScoreResultParameters` einführen, exakt analog zu `SafeguardScannerParameters` (Z.393-400). Methode `BuildScoreResult` nimmt nur noch `(BuildScoreResultParameters p)` als Parameter; alle internen Verweise ändern sich von `violations/classes/config/threshold/maxRemediationEntries` auf `p.Violations/p.Classes/p.Config/p.Threshold/p.MaxRemediationEntries`.
- **Warum:** Pattern-Konsistenz mit `SafeguardScannerParameters` (selbe Datei), per `AiNetLinterRichtlinien.mdc` §1 / `AiNetLinter.mdc` "Grenzwerte (Produktion)" sind `record`-Konstruktoren vom `MaxMethodParameterCount: 4`-Limit ausgenommen. Alternative „Methode in private Helper aufteilen" wurde geprüft und verworfen — die 5 Parameter sind semantisch zusammengehörig (Eingabe + Schwellwerte für *eine* Score-Berechnung), ein Split würde zwei Methoden mit jeweils 2-3 Parametern erzeugen, die nichtsynchron laufbar wären (Sealed-Bonus hängt von `EnforceSealedClasses` aus `config` ab, CC/Footprint-Penalties hängen von `metrics` aus `config` ab — alle vier Score-Komponenten greifen auf denselben `config` zu).
- **Auswirkung auf Tests:** Die beiden direkten `BuildScoreResult`-Aufrufe in `SafeguardScannerTests.cs` (Z.282-287 und Z.302-307) müssen umgestellt werden auf `new BuildScoreResultParameters(Violations: …, Classes: …, Config: …, Threshold: …, MaxRemediationEntries: …)`. Verhalten der Tests bleibt identisch, nur die Aufruf-Syntax ändert sich.

### Änderung 2 — Finding #2 + #3: `EnumerateConcreteClasses` Extract-Method (Z.311, CC=33 und CC=16)

- **Datei:** `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Z.311-362)
- **Was:** `EnumerateConcreteClasses` (Z.311) in drei private Hilfsmethoden aufteilen, sodass die verbleibende „Schleifen-Methode" CC ≤ 4 hat. Konkrete Extraktionen (in der Reihenfolge ihrer Anwendung in der Schleife):
  1. **`private static async Task<Compilation?> TryGetCompilationAsync(Project project, CancellationToken ct)`** (neu) — kapselt `if (!project.SupportsCompilation) return null;` + `try { compilation = await project.GetCompilationAsync(ct); } catch (OperationCanceledException) { throw; } catch (Exception ignored) { return null; }` + `return compilation;`. Wird in Änderung 3 (Async-Migration) und Änderung 4 (Catch-Name) mit-erledigt.
  2. **`private static bool ShouldIncludeDocument(Document document, Project project, string? scopeFilter)`** (neu) — kapselt den Scope-Filter-Vergleich (Z.337-343) als `return string.IsNullOrEmpty(scopeFilter) || Pfad-Match || Projekt-Name-Match;`. Macht den Filter leichter testbar (kann ggf. in späterer Test-Erweiterung isoliert geprüft werden — nicht Scope dieses Steps).
  3. **`private static async Task<IReadOnlyList<ScannedClass>> CollectClassDeclarationsAsync(Document document, Project project, Compilation compilation, Config config, CancellationToken ct)`** (neu) — kapselt `var syntaxTree = await document.GetSyntaxTreeAsync(ct); if (syntaxTree is null) return []; var semanticModel = compilation.GetSemanticModel(syntaxTree); var root = syntaxTree.GetRoot(ct); return root.DescendantNodes().OfType<ClassDeclarationSyntax>().Select(cd => TryBuildScannedClass(cd, semanticModel, config, ct)).Where(s => s is not null).Cast<ScannedClass>().ToList();` mit `TryBuildScannedClass` als weiterer Mini-Helper, der `null`-Symbol/TypeKind-Filter enthält.
- **Verbleibendes `EnumerateConcreteClasses` (jetzt `EnumerateConcreteClassesAsync`, siehe Änderung 3):** Nur noch die äußere `foreach (var project in solution.Projects)`-Schleife + innerer `if (compilation is null) continue;` + `foreach (var document in project.Documents) { if (!ShouldIncludeDocument(...)) continue; var classes = await CollectClassDeclarationsAsync(...); collected.AddRange(classes); }`. CC der Rumpf-Schleife ≤ 3.
- **Warum diese Aufteilung (Reihenfolge / Rückgabewerte):** Reihenfolge entspricht dem Datenfluss (Project → Compilation → Document → Klassen). `TryGetCompilationAsync` ist `async Task<Compilation?>` (Nullable, weil `null` = „nicht-kompilierbar, skip"). `ShouldIncludeDocument` ist sync `bool` (rein deterministisch, kein I/O). `CollectClassDeclarationsAsync` ist `async Task<IReadOnlyList<ScannedClass>>` (vermeidet LINQ-Lazy-Evaluation mit `await` im Select). Mini-Helper `TryBuildScannedClass` ist sync, mit `INamedTypeSymbol?`-Rückgabe (`null` = "skip, falsche Symbol-Art oder abstract").
- **Auswirkung auf Tests:** Keine direkten Test-Aufrufe von `EnumerateConcreteClasses` (es ist `private`) — keine Test-Anpassung nötig. Die existierenden 5 Tests, die `ComputeScoreAsync` mit synthetischen Solutions aufrufen, validieren das Verhalten End-to-End; ein CC-Refactoring ändert keine beobachtbare Semantik.

### Änderung 3 — Finding #4 + #5: Async-Migration für `EnumerateConcreteClasses` (Z.321, Z.345)

- **Datei:** `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Z.311-362)
- **Was:** `EnumerateConcreteClasses` umbenennen zu `EnumerateConcreteClassesAsync` und Signatur ändern zu `private static async Task<IReadOnlyList<ScannedClass>> EnumerateConcreteClassesAsync(Solution solution, string? scopeFilter, Config config, CancellationToken ct)`. Alle blockierenden `.GetAwaiter().GetResult()`-Aufrufe in dieser Methode (Z.321, Z.345) durch `await` ersetzen:
  - `await project.GetCompilationAsync(ct)` (statt `.GetAwaiter().GetResult()`)
  - `await document.GetSyntaxTreeAsync(ct)` (statt `.GetAwaiter().GetResult()`)
  - `await syntaxTree.GetRootAsync(ct)` (statt `syntaxTree.GetRoot(ct)`, soweit vorhanden — der aktuelle Code Z.349 hat `GetRoot(ct)`, das ist eigentlich eine non-blocking Sync-Überladung von `GetRootAsync(ct).Result`; in der Async-Migration auf `GetRootAsync(ct)` umstellen für Konsistenz, **nur** wenn die Methode dadurch nicht zusätzlichen CC einführt — sonst belassen und nur die zwei `.GetAwaiter().GetResult()`-Stellen fixen)
- **Aufrufstelle in `ComputeScoreAsync` (Z.113):** `var classes = EnumerateConcreteClasses(...)` → `var classes = await EnumerateConcreteClassesAsync(...)`. `ComputeScoreAsync` ist bereits `async` (Z.83), also keine zusätzliche `async`-Propagation nötig.
- **Warum Async-Migration statt Disable-Suppression:** Async-Migration ist hier klar im Scope und Pattern-konsistent:
  - `ComputeScoreAsync` ist bereits `async Task<…>` (Z.83).
  - Aufrufstelle in Z.113 ist in einem `async`-Kontext, ein `await` ist trivial.
  - Pattern passt zu `LinterEngine.RunAsync` (async) und `GetViolationsScanner.BuildViolationsTextAsync` (async) — der `BanBlockingTaskAccess`-Linter würde sonst diese Inkonsistenz immer wieder an `EnumerateConcreteClasses` markieren.
  - Records (`ScoreResult`, `SafeguardScoreResult`, …) sind unveränderlich, brauchen kein async-Interface.
  - EPIC-02 (Tool-Wrapper) wird `ComputeScoreAsync` ohnehin `await`en (Rückgabe ist `Task<SafeguardScoreResult>`), kein Cascade nach EPIC-02.
  - Alternative `// ainetlinter-disable BanBlockingTaskAccess` mit Verweis auf `McpCodeGraphServer.cs:73` (Präzedenz des Reviewers) wurde geprüft, aber: im SafeguardScanner-Kontext ist `async` trivial einzuführen (Aufrufstelle ist bereits async), die Suppression wäre eine bewusste Verkleinerung der Code-Qualität für reinen Komfort. Async-Migration ist die *strukturell saubere* Lösung.
- **Auswirkung auf Tests:** Keine — kein Test ruft `EnumerateConcreteClasses`/`EnumerateConcreteClassesAsync` direkt auf. Die zwei direkten `BuildScoreResult`-Aufrufe in `BuildScoreResult_ClampsScoreToZeroAndTen` (Z.282-287, Z.302-307) sind von der Async-Migration nicht betroffen, weil `BuildScoreResult` weiterhin sync `IReadOnlyList<ScannedClass>` akzeptiert.

### Änderung 4 — Finding #6: Stummer Catch bekommt Variablenname (Z.327)

- **Datei:** `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Z.327-332, innerhalb der nach Änderung 3 entstehenden `TryGetCompilationAsync`)
- **Was:** `catch { … }` ersetzen durch `catch (Exception ignored) { return null; }`. Der separate `catch (OperationCanceledException) { throw; }` (Z.323-326) bleibt unverändert. Kurzer Begründungs-Kommentar oberhalb des Catch-Alls: `// Compilation-Fehler fuehren per Design nicht zum Scanner-Abbruch — andere Projekte der Solution sollen weiter analysiert werden koennen.`
- **Warum `Exception ignored` und nicht Variante (b) (expliziter Re-Throw-Logik):** Die `OperationCanceledException` ist bereits in einem separaten Catch-Block korrekt behandelt (Z.323-326) — eine Zusammenfassung in `catch (Exception ex) when (ex is not OperationCanceledException)` wäre redundant. Die Linter-Doku (siehe `lint-output-step-001.txt` Z.49) erkennt `ignored` explizit als „der Linter erkennt den Variablennamen als explizit gewolltes Ignorieren" — das ist die saubere und minimal-invasive Variante. Verhalten ist Bit-identisch zum Status Quo (nur die Exception-Variable bekommt einen Namen).
- **Auswirkung auf Tests:** Keine — der Catch-Pfad ist im `ComputeScoreAsync_EmptySolution_ReturnsHighScore`-Test nicht relevant (AdhocWorkspace-Projekte kompilieren erfolgreich) und im `ComputeScoreAsync_LinterEngineThrows_ReturnsMalfunctionWithContext`-Test wird die Exception im LinterEngine-Block gefangen (Z.107), nicht im Compile-Block.

### Änderung 5 — Beobachtung #7: `ResolveHintForRule` Switch-Expression → Dictionary-Lookup (Z.285)

- **Datei:** `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (Z.283-309)
- **Was:** Switch-Expression durch eine `private static readonly IReadOnlyDictionary<string, string> RuleHints` (Instanziierung als `static readonly` Field) ersetzen. `ResolveHintForRule` wird zu einem Einzeiler: `return RuleHints.TryGetValue(ruleName, out var hint) ? hint : $"Regel-Verstoss '{ruleName}' pruefen — Details in Docs/configuration.md.";` (oder Variante mit `StringComparer.Ordinal` als Key-Comparer, falls die Linter-IDs case-sensitiv sind — verifizieren durch kurzen Blick auf `LinterRuleIds.MaxLineCount` etc., sie sind als `const string` exakt die Schlüssel im Switch, also `StringComparer.Ordinal`).
- **Warum im Scope (Begründung Scope-Disziplin):** Der Reviewer hat das Finding ausdrücklich als "nicht verdict-relevant, aber Plan-Abweichung" markiert. Spec §6.2.1 sagt "Ein Fix-Step betrifft ausschließlich den Scope des ursprünglichen Findings" — strenggenommen ist es out-of-scope. **Aber:** (a) die Plan-Vorgabe in `step-001/step-plan.md` Z.181-192 verlangte explizit „Mapping-Tabelle als `static IReadOnlyDictionary<string, string>`", der Coder hat das ohne Begründung verworfen (siehe `step-result.md` §"Beobachtungen" — die Switch-Implementierung wird dokumentiert, aber die Abweichung von der Plan-Vorgabe nicht); (b) die Korrektur ist klein (1 Funktion, 11-Arm-Switch → 1 Dictionary + 1 Lookup = ~20 Zeilen); (c) es ist eine `MaxSwitchArms=10`-Regelverletzung in `rules.json` global aktiv — wenn nicht hier, dann beim nächsten Linter-Run; (d) wir refactorieren die Datei ohnehin (Async-Migration, Extract-Method, Parameter-Record), das Dictionary ist ein Aufwasch. Die zusätzlichen ~20 Zeilen sind im Budget eines `low`-Risk-Fix-Steps gut aufgehoben.
- **Auswirkung auf Tests:** Keine — `BuildRemediation_UnknownRuleName_FallsBackToDefaultHint` (Z.239-257) testet den Default-Pfad (unbekannter RuleName) — der bleibt identisch. `BuildRemediation_EmptyList_ReturnsEmptyRemediation` (Z.259-268) ist vom Refactoring unabhängig. Die Sortierung der `ActionableSteps` (`ThenBy(g.Key, OrdinalIgnoreCase)` in Z.190) bleibt ebenfalls unverändert.

## Tests

- [ ] `dotnet test --filter FullyQualifiedName~SafeguardScannerTests` → 13/13 grün (Anpassung: 2 Aufrufstellen von `BuildScoreResult` umstellen auf `BuildScoreResultParameters`-Konstruktor)
- [ ] `dotnet test --filter Category=Unit` → 141/141 grün (keine Regressionen)
- [ ] `dotnet build` → 0 Warnungen, 0 Fehler (TreatWarningsAsErrors aktiv)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache` → **0** Verstöße in `SafeguardScanner.cs` (Lauf-Kommando aus `AiNetLinterRichtlinien.mdc` §3)
- [ ] Verhaltens-Identität: Determinismus-Test (`ComputeScoreAsync_Determinismus_ZweiLaufeIdentischerScore`) und Score-Werte aller anderen Tests Bit-identisch zu Commit `afb6146` (Refactoring ändert keine Semantik)
- [ ] `MaxLineCount=500` weiterhin eingehalten (Datei wächst um ~30 Zeilen aus dem Refactoring, bleibt deutlich unter 500)

## Definition of Done

- [ ] Alle 6 MAJOR-Findings aus `step-001/step-review.md` §"Findings (issues)" behoben
- [ ] Beobachtung 7 (Plan-Abweichung `MaxSwitchArms`) behoben (siehe "Bekannte Ausnahmen / Entscheidungen" unten für Scope-Begründung)
- [ ] `dotnet build` grün (0 Warnungen, 0 Fehler, TreatWarningsAsErrors)
- [ ] `dotnet test --filter FullyQualifiedName~SafeguardScannerTests` grün (13/13, mit den 2 angepassten `BuildScoreResult`-Aufrufstellen)
- [ ] `dotnet test --filter Category=Unit` grün (141/141, keine Regressionen)
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache` zeigt **0** Verstöße in `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (und unverändert in den übrigen Dateien)
- [ ] Code-Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, mit `[safeguard]`-Suffix; Subject max. 72 Zeichen; Body mit `Refs: tasks/safeguard/step-001/fix-01`; `### Commit-Vorschlag`-Block nach `AiNetLinterRichtlinien.mdc` §4)
- [ ] Doku-Commit (separater Commit per Spec §10.3) trägt `tasks/safeguard/step-001/fix-01/step-result.md` + Status-Update in `tasks/safeguard/step-001/fix-01/step-plan.md` (auf `done (pending audit)`) und `Ref Code-Commit: <hash>` im Body
- [ ] `tasks/safeguard/step-001/fix-01/step-result.md` geschrieben mit beiden Commit-Hashes, grünem Test-Output (einzeilig pro Spec §10.7), Linter-Output (0 Verstöße in `SafeguardScanner.cs`), und kurzer Notiz "Refactoring ohne Verhaltensänderung, Score-Formel identisch"
- [ ] `status` in `tasks/safeguard/step-001/fix-01/step-plan.md` von `open` auf `done (pending audit)` gesetzt (vom Coder im Doku-Commit)
- [ ] `related_to` zeigt ausschließlich auf `tasks/safeguard/step-001/step-review.md` (Pointer, kein Inhalt-Cache — Spec §10.6)

## Rules-Refs

(Die Rules-Refs aus `step-001/step-plan.md` gelten weiterhin unverändert — die Fixes betreffen exakt die dort kuratierten Regeln.)

- `.agents/rules/AiNetLinter.mdc` — `MaxMethodParameterCount: 4` (Finding #1 → Parameter-Record), `MaxCognitiveComplexity: 15` + `MaxCyclomaticComplexity: 12` (Findings #2/#3 → Extract-Method), `BanBlockingTaskAccess` (Findings #4/#5 → Async-Migration), `EnforceNoSilentCatch` (Finding #6 → `catch (Exception ignored)`)
- `.agents/rules/AiNetLinter.mdc` — `MaxSwitchArms: 10` (Beobachtung #7 → Dictionary-Lookup; war nicht in den Plan-Rules-Refs zitiert, ist aber global aktiv und wird mit-adressiert — siehe Scope-Begründung unten)
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Result-Pattern (durch Fix unangetastet: `SafeguardScoreResult.IsMalfunction`/`Context` bleibt), sparsame Kommentare (Begründungs-Kommentar am `catch (Exception ignored)` ist *Why*-Kommentar und damit gemäß §5 erlaubt, nicht redundante Nacherzeugung)
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — Commit-Vorschlag-Pflicht (siehe DoD)

## Bekannte Ausnahmen / Entscheidungen

- **Variante für Finding #4+#5 (Async-Migration vs. Disable-Suppression):** Async-Migration gewählt. Begründung: (a) `ComputeScoreAsync` ist bereits `async`, der Aufruf an Z.113 ist trivial `await`-bar; (b) Pattern-Konsistenz mit `LinterEngine.RunAsync` und `GetViolationsScanner.BuildViolationsTextAsync`; (c) keine Cascade — `BuildScoreResult` bleibt sync, Records bleiben unverändert, EPIC-02-Tool-Wrapper wartet ohnehin `ComputeScoreAsync`; (d) Disable-Suppression wäre ein bewusstes Akzeptieren einer Linter-Verletzung für reinen Komfort, wo die strukturell saubere Lösung gleich aufwändig ist. Reviewer-Hinweis "Async-Propagation, weil im Pattern-Kontext von `LinterEngine.RunAsync`/`BuildViolationsTextAsync` konsistent" wird hier ausdrücklich gefolgt.
- **Variante für Finding #6 (`catch (Exception ignored)` vs. expliziter Skip mit Re-Throw):** `catch (Exception ignored)` gewählt. Begründung: (a) `OperationCanceledException` ist bereits in einem separaten `catch` korrekt behandelt (Z.323-326) — eine `when`-Klausel-Konsolidierung wäre redundant; (b) `ignored` ist explizit im Linter als "explizit gewolltes Ignorieren" anerkannt (`lint-output-step-001.txt` Z.49); (c) Verhalten Bit-identisch zum Status Quo, nur der Variablenname ändert sich. Die Intention "Compilation-Fehler pro Projekt führen per Design nicht zum Scanner-Abbruch" wird durch einen kurzen Begründungs-Kommentar dokumentiert.
- **Scope-Entscheidung Beobachtung #7 (Switch → Dictionary):** In den Scope aufgenommen. Begründung: (a) Plan-Vorgabe (`step-001/step-plan.md` Z.181-192) verlangte explizit `IReadOnlyDictionary<string, string>`-Mapping, der Coder hat ohne Begründung davon abgewichen — die Wiederherstellung der Plan-Konformität ist ein Fix des Coder-Drift, kein neues Refactoring; (b) das Refactoring ist klein (~20 Zeilen), risikoarm (Pattern-Standard, kein Verhaltens-Risiko); (c) das Linter-Limit `MaxSwitchArms: 10` ist global aktiv und würde sonst beim nächsten Lint-Run sofort wieder markiert — mit-erledigen spart einen separaten Fix-Step; (d) der Fix-Step refactoriert die Datei ohnehin umfänglich (Parameter-Record + Extract-Method + Async-Migration), das Dictionary passt organisch in denselben Commit. Bewusste Abweichung von der strengen "nur verdict-relevante Findings"-Regel (Spec §6.2.1) zugunsten von Plan-Treue + minimaler Zusatz-Aufwand.
- **`GetRoot(ct)` vs. `GetRootAsync(ct)` in Änderung 2 / 3:** Soweit der ursprüngliche `GetRoot(ct)`-Aufruf (Z.349) keine zusätzlichen CC-Knoten in den extrahierten Helper einführt, wird er **nicht** auf `GetRootAsync` umgestellt — der aktuelle Sync-Überladungs-Aufruf zählt nicht als `BanBlockingTaskAccess`-Verstoß (es ist kein `.GetAwaiter().GetResult()`). Nur die zwei explizit vom Linter markierten Stellen (Z.321, Z.345) werden async-migriert. Falls Coder beim Refactoring erkennt, dass `GetRootAsync` an dieser Stelle idiomatisch passender ist (weil der umgebende Helper ohnehin `async` ist), darf er konvertieren — das ist dann *Konsistenz* innerhalb desselben async-Helpers, nicht eine eigenständige Async-Migration.
- **Reihenfolge der Commits:** Erst Code-Commit (`fix(mcp): Linter-Verstöße in SafeguardScanner behoben [safeguard]`), dann Doku-Commit (`chore(task): Fix-Step-Result für step-001/fix-01 [safeguard]`). Beide tragen den `[safeguard]`-Suffix, wie in Spec §10.3 vorgeschrieben.
- **Nicht im Scope (bewusst out-of-scope):**
  - Die 3 akzeptierten Coder-Abweichungen aus `step-001/step-result.md` (Score-Gewicht `ViolationPenaltyUnit = 1.5`, `BuildRemediation(IReadOnlyList<ViolationEntry>)` statt `IReadOnlyCollection<RuleViolation>`, internes `ScannedClass`-Record) — explizit vom Reviewer akzeptiert, keine Änderung.
  - `tech-debt.md`-Eintrag `TD-001` (fehlende `GetViolationsScannerTests.cs`) — Nutzer-Sache, nicht Scope eines Fix-Steps.
  - `McpCodeGraphServer.cs:73` (Präzedenz für `// ainetlinter-disable BanBlockingTaskAccess`) — wird nicht angefasst, da die Alternative (Async-Migration) im SafeguardScanner-Kontext sauberer ist.
  - EPIC-02-Aspekte (`SafeguardTool.cs`, `AnalysisToolRegistrations`-Erweiterung, ServerInstructions, Doku-Updates) — separates Epic, nicht im Fix-Scope.
  - `EnumerateConcreteClasses` Promotion auf `internal` — aktuell nicht nötig (kein externer Test ruft sie auf), wird im `step-result.md` §"Sonstige Beobachtungen" aus step-001 als „ggf. nachziehen in EPIC-02/EPIC-03" vermerkt.

## Out-of-Scope

- `tech-debt.md` (TD-001 ist Nutzer-Sache)
- Akzeptierte Coder-Abweichungen aus step-001 (Score-Gewicht, `BuildRemediation`-Signatur, `ScannedClass`-Record)
- Test-Refactorings, die nicht von den 6 Findings erzwungen werden (z. B. `BuildScoreResult_ClampsScoreToZeroAndTen` bleibt funktional identisch, nur die 2 `BuildScoreResult`-Aufrufstellen werden an die Parameter-Record-Signatur angepasst)
- EPIC-02 (Tool-Wrapper, Registrierung, ServerInstructions, Doku)
- Neue Features / "wäre-nice"-Refactorings (z. B. zusätzliche `internal static`-Helpers, die nicht direkt zur Linter-Konformität beitragen)

## Code-Skizze (optional)

```
// Finding #1 — Parameter-Object-Record
internal sealed record BuildScoreResultParameters(
    IReadOnlyCollection<RuleViolation> Violations,
    IReadOnlyList<ScannedClass> Classes,
    Config Config,
    double Threshold,
    int MaxRemediationEntries);

internal static ScoreResult BuildScoreResult(BuildScoreResultParameters p)
{
    var violationPenalty = ComputeViolationPenalty(p.Violations);
    var ccPenalty = ComputeCcPenalty(p.Classes, p.Config.Metrics.MaxCognitiveComplexity);
    var footprintPenalty = ComputeFootprintPenalty(p.Classes, p.Config.Metrics.MaxAIContextFootprint);
    var sealedBonus = ComputeSealedBonus(p.Classes, p.Config.Global.EnforceSealedClasses);

    var raw = 10.0 - violationPenalty - ccPenalty - footprintPenalty + sealedBonus;
    var score = Math.Clamp(raw, 0.0, 10.0);
    var passed = score >= p.Threshold;
    // … Sortierung, Remediation, Summary wie bisher, mit p. statt direkter Parameternamen
}

// Findings #2/#3/#4/#5/#6 — EnumerateConcreteClasses Async + Extract-Method
private static async Task<IReadOnlyList<ScannedClass>> EnumerateConcreteClassesAsync(
    Solution solution, string? scopeFilter, Config config, CancellationToken ct)
{
    var collected = new List<ScannedClass>();
    foreach (var project in solution.Projects)
    {
        var compilation = await TryGetCompilationAsync(project, ct);
        if (compilation is null) continue;

        foreach (var document in project.Documents)
        {
            if (!ShouldIncludeDocument(document, project, scopeFilter)) continue;
            collected.AddRange(
                await CollectClassDeclarationsAsync(document, project, compilation, config, ct));
        }
    }
    return collected;
}

private static async Task<Compilation?> TryGetCompilationAsync(Project project, CancellationToken ct)
{
    if (!project.SupportsCompilation) return null;
    try
    {
        return await project.GetCompilationAsync(ct);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ignored)
    {
        // Compilation-Fehler fuehren per Design nicht zum Scanner-Abbruch — andere Projekte
        // der Solution sollen weiter analysiert werden koennen.
        return null;
    }
}

private static bool ShouldIncludeDocument(Document document, Project project, string? scopeFilter)
{
    if (string.IsNullOrEmpty(scopeFilter)) return true;
    var filePath = document.FilePath;
    if (filePath is { } p && p.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase)) return true;
    if (project.Name.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
}

private static async Task<IReadOnlyList<ScannedClass>> CollectClassDeclarationsAsync(
    Document document, Project project, Compilation compilation, Config config, CancellationToken ct)
{
    var syntaxTree = await document.GetSyntaxTreeAsync(ct);
    if (syntaxTree is null) return Array.Empty<ScannedClass>();

    var semanticModel = compilation.GetSemanticModel(syntaxTree);
    var root = syntaxTree.GetRoot(ct);
    var result = new List<ScannedClass>();
    foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
    {
        if (TryBuildScannedClass(classDecl, semanticModel, config) is { } scanned)
        {
            result.Add(scanned);
        }
    }
    return result;
}

private static ScannedClass? TryBuildScannedClass(
    ClassDeclarationSyntax classDecl, SemanticModel semanticModel, Config config)
{
    var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
    if (symbol is null || symbol.TypeKind != TypeKind.Class || symbol.IsAbstract) return null;
    return BuildScannedClass(symbol, classDecl, config);
}

// Beobachtung #7 — Dictionary-Lookup
private static readonly IReadOnlyDictionary<string, string> RuleHints =
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [LinterRuleIds.MaxLineCount] =
            "Datei aufteilen — Klassen/Methoden extrahieren, Partial-Klassen pruefen.",
        [LinterRuleIds.MaxMethodLineCount] =
            "Methode aufteilen — Hilfsmethoden extrahieren, Verantwortlichkeit aufspalten.",
        [LinterRuleIds.MaxMethodParameterCount] =
            "Parameter-Record einfuehren — verwandte Argumente in einem Werteobjekt buendeln.",
        // ... restliche ~7 Eintraege, 1:1 aus dem aktuellen Switch uebernommen
    };

private static string ResolveHintForRule(string ruleName, Config config)
    => RuleHints.TryGetValue(ruleName, out var hint)
        ? hint
        : $"Regel-Verstoss '{ruleName}' pruefen — Details in Docs/configuration.md.";
```

## Notes

- **Verhaltens-Identität ist Pflicht:** Der Fix-Step ist rein mechanisches Refactoring. Score-Werte aller Tests, Sortierung der Violations, Remediation-Output, Catch-Verhalten im Compile-Block und Default-Hint bei unbekannten RuleNames müssen Bit-identisch zu Commit `afb6146` sein. Der Determinismus-Test (`ComputeScoreAsync_Determinismus_ZweiLaufeIdentischerScore`) ist die primäre Regression-Sicherung — er würde ein verhaltensänderndes Refactoring sofort aufdecken.
- **Reihenfolge der Coder-Arbeit (Empfehlung, nicht hart):** (1) `BuildScoreResultParameters` einführen + `BuildScoreResult`-Signatur umstellen + Test-Aufrufstellen anpassen → (2) `EnumerateConcreteClasses` → `EnumerateConcreteClassesAsync` migrieren + Helper extrahieren + `TryGetCompilationAsync` mit `catch (Exception ignored)` → (3) `ResolveHintForRule` Switch → Dictionary. Jeder Zwischenschritt baut und kompiliert (TreatWarningsAsErrors); der Linter-Run am Ende zeigt 0 Verstöße.
- **Bewusst NICHT angetastet:** `SafeguardScannerParameters` (Z.393-400), `SafeguardScoreResult` (Z.407-410), `ScoreResult` (Z.418-424), `ViolationEntry` (Z.430-436), `RemediationHint` (Z.445-448), `ScannedClass` (Z.451-456) — alle Records bleiben Bit-identisch, kein Anpassungsbedarf aus den Findings.
- **Linter-Pfad:** `dotnet run --project src/AiNetLinter -- --config rules.json --path . --no-cache` (siehe `AiNetLinterRichtlinien.mdc` §3, ergänzt im Original-Step-Plan-Review als fehlender DoD-Punkt — in diesem Fix-Plan als Pflicht-Test aufgenommen).
- **Commit-Strategie:** Spec §10.3 verlangt separate Commits für Code + Doku. Der Code-Commit fasst alle 4 Datei-Änderungen (Scanner + 2 Test-Aufrufstellen + ggf. Dictionary-Konstante) in einem Commit zusammen — die Änderungen sind semantisch gekoppelt (Tests müssen zum Refactoring passen) und sollten nicht zersplittet werden. Body listet alle 4 Findings + Beobachtung 7 mit den neuen Zeilen-Referenzen (der Coder misst die exakten Zeilen NACH dem Refactoring).
- **Bewusste Abweichung von der strengen Scope-Regel** (Spec §6.2.1 "Ein Fix-Step betrifft ausschließlich den Scope des ursprünglichen Findings"): Beobachtung 7 ist kein verdict-relevantes Finding, wird aber in den Scope aufgenommen — vollständige Begründung siehe "Bekannte Ausnahmen / Entscheidungen" oben.
