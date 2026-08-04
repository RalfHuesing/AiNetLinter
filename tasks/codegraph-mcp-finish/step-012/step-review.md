---
status: done
type: step-review
task: codegraph-mcp-finish
step: 012
epic: EPIC-07,EPIC-08
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3 (orientiert an Modell-Vorgabe `claude-sonnet-5, Stufe High`)
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-04
verdict: issues
tech_debt_ids: [TD-013]
---

# Review Step 012: Restliche Tech-Debt-Einträge (EPIC-07) + Symbolgraph-Erweiterungen (EPIC-08)

**Hinweis zur Review-Situation:** Der Coder-Aufruf wurde durch ein Token-Plan-Limit abgebrochen. Der Nutzer hat den Zwischenstand per Commit `93caa8a` gesichert; das `step-result.md` wurde vom Orchestrator geschrieben; ein Smoke-Test-Drift-Fix folgte per `b55b065`. Dieser Review ist de facto die letzte Qualitätssicherung. Build und Volllauf reproduzieren grün (0/0, 1241/1241, 0 Violations, kein TD-005-Flake) — die inhaltlichen Findings sind plan-/regel- und doku-seitig.

## Verdict

- [ ] approved — alle vier Prüfebenen ok
- [x] **issues** — Fix-Step `step-012/fix-01` wird empfohlen (siehe Findings 1-5)
- [ ] blocked — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: 8 Sub-Bereiche inhaltlich umgesetzt, aber **5 Plan-Abweichungen** in Doku-/Commit-/Override-Begründung (siehe Findings)
- [x] Rules-Konformität: `AiNetLinterRichtlinien.mdc` §5 (Verbot Task-/Planungsartefakt-Referenzen) **verletzt** in `SymbolBodyToolRegistrations.cs:18`; `AiNetLinter.mdc` Grenzwerte eingehalten (Build 0/0); PathOverrides in `rules.json` ohne Begründung im `step-result.md`
- [x] Logische Korrektheit: E.1/E.2/E.3 semantisch korrekt, Smoke-Test-Drift-Fix (6→7, 8 verboten) sinnvoll, A3-Pfad erhalten; zwei Test-Coverage-Schwächen (Findings 6+7)
- [x] Konzept-Treue: Muss-Haben D (5 TD-Items) + Muss-Haben E (E.1-E.3) + DoD funktional erfüllt; **Doku-Teile (integration.md, ROADMAP.md, task-state.md-inhaltlich) fehlen** als Plan-DoD-Lücken
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (1241/1241 in 2:54 min, kein TD-005-Flake)
- [x] Selbst-Lint: selbst nachgeprüft, grün (0 Violations)
- [x] Keine offenen `AiNetLinter.exe`/`testhost.exe`-Prozesse nach Volllauf

## Befund

### Plan-Erfüllung

| Sub-Bereich | Status | Anmerkung |
|---|---|---|
| TD-001 (Paket-Referenz) | ✓ geschlossen | Grep-Verifikation plausibel dokumentiert, csproj unverändert |
| TD-002 (Fixture-Pool) | ✓ geschlossen | Begründung in `tech-debt.md` nachvollziehbar |
| TD-004 (Footprint) | ✓ zurückgestellt | Begründung in `tech-debt.md` nachvollziehbar, eigenes TD-011 angelegt |
| TD-006 (DRY-Konsolidierung) | ✓ geschlossen | `FileSystemExclusionHelpers`, 2 Aufrufer migriert, 6 Tests — 1:1-Verhalten erhalten |
| TD-008 (XML-Doc) | ✓ geschlossen | 1-Zeilen-Sanierung, Pattern-Vorlage aus TD-007 |
| E.1 (`get_symbol_body` + stabile IDs) | ✓ umgesetzt | 4. Registrar-Klasse `SymbolBodyToolRegistrations`, `SymbolIdentifierResolver.TryResolveByStableIdAsync` |
| E.2 (`depth` + `CallGraphTraversal`) | ✓ umgesetzt | hard-cap 3, `MaxRecursionNodes: 200`, Aggregation-Output |
| E.3 (DI-Hinweis) | ✓ umgesetzt | `DiRegistrationHeuristics` mit `\b`-Word-Boundary, 4. Sektion mit explizitem Header |
| `Docs/agent-api.md` | ✓ aktualisiert | 7-Tools-C#-only, E.1/E.2/E.3-Sektionen, get_symbol_body-Tabellen-Eintrag |
| `Docs/integration.md` | ✗ **NICHT aktualisiert** | Z. 223 sagt weiter "9 granular abfragbare Tools" (sollte 10), Tool-Liste Z. 277 unvollständig |
| `Docs/ROADMAP.md` | ✗ **NICHT aktualisiert** | E-Block (E.1/E.2/E.3) fehlt; nur "EPIC-07 — Test-Infrastruktur" + "EPIC-08 — Doku" (aus step-008) sind gelistet |
| `tasks/codegraph-mcp-finish/task-state.md` | ✓ aktualisiert | `current_step` korrekt auf step-012 |
| 2 Commits (Code + Doku) | ✗ **nicht getrennt** | 1 Code-Commit + 1 Smoke-Test-Fix-Commit; `Docs/agent-api.md` ist im Code-Commit mit drin |
| Commit-Subject | ✗ **informal** | Commit-Text "zwischen commit von 'Coder step-012 EPIC-07+08' abbruch" statt geplantem `feat(mcp): tech-debt-abschluss-und-symbolgraph-erweiterungen [codegraph-mcp-finish]` |
| PathOverride-Begründung | ✗ **fehlt im `step-result.md`** | 2 neue PathOverrides (`GetSymbolBodyTool.cs: 2700`, `SymbolBodyToolRegistrations.cs: 2800`) ohne Begründung; Plan verlangte explizite Begründung im `step-result.md` "falls wider Erwarten über 2500" |
| `td-005`-Flake-Status | ✓ reproduziert | 1241/1241 grün, kein Flake |

### Rules-Konformität

- **§5 (Verbot Task-/Planungsartefakt-Referenzen) — VERLETZT:**
  - `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs:18` — XML-Doc enthält das Wort "TD-011-Puffer-bis-Limit-Risiko". `AiNetLinterRichtlinien.mdc` §5 Z. 100 verbietet explizit "`TD-005`, `EPIC-06` … Diese Ordner/Dokumente werden nach Task-Abschluss gelöscht". TD-011 ist im selben Sinne eine Task-/Planungs-Referenz und gehört ersatzlos gestrichen. **Sanierung trivial (1 Satz).**

- **§5 (Verbot redundanter Nacherzeugung) — leicht verletzt:**
  - `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:29-33` — XML-Doc bricht mitten im Satz ab ("Stellt dem Aufrufstellen-Output einen … Dateien hat"). Textpassage unvollständig. **TD-001-verwandt** (bestehendes Problem aus früheren Schritten, im Sanierungs-Zug erneut übersehen).

- **§5 (kaputte XML-Doc-Tags) — VERLETZT in 3 Dateien außerhalb des Step-Scopes:**
  - `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs:14-17` — `<c>`-Tag geöffnet und nie geschlossen ("`siehe <c> nicht mit jedem neu registrierten Tool waechst`"). Text hängt im Doc-String.
  - `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs:14-19` — gleicher Defekt ("`(siehe <c> nicht durch reine Hilfslogik unnoetig waechst`)").
  - `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs:14-19` — gleicher Defekt ("`(siehe <c> klein bleibt`)").
  Diese drei Befunde gehören in `tech-debt.md` als **TD-013** (Pointer-Prinzip) — siehe unten.

- **§5 (Verbot Symptom-Fixing) — eingehalten:** Keine auskommentierten Tests, keine abgeschwächten Assertions in den geänderten Test-Dateien. (Test-`ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult` ist schwach, prüft aber keine abgeschwächte Bedingung — siehe Logik-Befund.)

- **`AiNetLinter.mdc` Grenzwerte — eingehalten:** Build 0/0, keine Warnungen. Alle neuen Klassen tragen `#nullable enable`, `sealed` (Records), Methoden-Länge ≤ 60 Z. (`GetSymbolBodyTool.cs:94-114` ExtractSymbolBody = 21 Z., `CallGraphTraversal.cs` = 132 Z. mit Helper-Logik, unter 150er Soft-Limit), `MaxMethodParameterCount: 4` (mit `MethodParameterCountIgnoreTypeNames: [CancellationToken]` beachtet — `FindReferencesTool.ExecuteAsync` und `GetImpactTool.ExecuteAsync` nutzen `GetImpactInput`-Record).

- **PathOverride-Mechanik (siehe `rules.json:467-478`):** 2 neue Einträge gesetzt, **keiner im `step-result.md` begründet**, obwohl der Plan Z. 481-483 explizit verlangt: "Falls der Footprint wider Erwarten über 2500 steigt: PathOverride hinzufügen und im `step-result.md` begründen." → **Finding 1 (MAJOR, Plan-Erfüllung).**

### Logische Korrektheit

- **E.1 stabile Symbol-IDs:** `DocumentationCommentId.CreateDeclarationId` ist die Roslyn-Standard-API, IDs sind über Zeilenverschiebungen stabil, solange der FQN stabil bleibt. `SymbolIdentifierResolver.TryResolveByStableIdAsync` iteriert korrekt über `solution.Projects → SymbolFinder.FindSourceDeclarationsAsync`. Fallback auf `FindReferencesTool.ResolveSymbolAsync` ist korrekt verdrahtet (`GetSymbolBodyTool.cs:35-43`). Disambiguierung von Overloads erfolgt automatisch über die voll-qualifizierte Parameter-Signatur in der ID. **Korrekt.**

- **E.2 `CallGraphTraversal`:** Iterative BFS (`TraversalState` mit `Queue<(ISymbol, int)>` + `HashSet<ISymbol>` per `SymbolEqualityComparer.Default`) — keine Endlos-Rekursion möglich. Hard-cap 3 (`MaxRecursionDepth`) und Knoten-cap 200 (`MaxRecursionNodes`) arbeiten unabhängig. `MarkSeenAndEnqueue` verhindert Zyklen. Git-Branch in `GetImpactTool.ExecuteGitRefBranchAsync` ignoriert `depth` korrekt (kein Symbol-Konzept für Diff-Symboltiefe) — Doku-konform. **Korrekt.**

- **E.3 `DiRegistrationHeuristics`:** `\b`-Word-Boundary-Regex auf `AddScoped`/`AddSingleton`/`AddTransient` schützt vor `MyAddScopedHelper`-Substring-Match. Heuristik-Filter auf `type.ToDisplayString()` (plus `Name` plus `Namespace.Name`) verhindert Massen-Treffer bei generischen `AddScoped<ILogger<>>`-Patterns. Convention-/Factory-basiertes Scanning ist explizit ausgeschlossen (XML-Doc Z. 20-22) und über den `MaxRegistrationHits: 20`-Cap + Word-Boundary doppelt abgesichert. **Korrekt.**

- **Smoke-Test-Drift-Fix (`b55b065`):** `AgentApi_CountsCsharpOnlyToolsCorrectly` assertiert "7 Tools sind C#-only" und verbietet "8 Tools". A3-Drift-Pfad bleibt erhalten: jede zukünftige Doku-Manipulation wird durch die Assertion gefangen. Die Korrektur 6→7 ist korrekt (E.1 fügt `get_symbol_body` als 7. C#-only-Tool hinzu). **Korrekt.**

- **Test-Coverage-Schwäche [MINOR]:** `FindReferencesToolTests.ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult` assertiert nur `Assert.NotEqual(true, result.IsError)` — der Testname suggeriert "ClampsToThree", aber es wird nicht verifiziert, dass tatsächlich der depth-3-Pfad (mit Aggregations-Output) genommen wurde, nicht depth-1. Verbesserung: zusätzlich `Assert.Contains("Treffer gesamt", text)` o.ä. **Finding 6 (MINOR, Logik).**

- **Test-Coverage-Schwäche [MINOR]:** `GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithDepth2_StillReturnsCallSite` testet `depth=2` im Symbol-Branch, aber kein Test prüft explizit, dass `depth` im **Git-Branch** ignoriert wird. Konzept sagt: "Git-Branch ignoriert `depth`". Aktuell wird das nur über Doku behauptet, nicht getestet. **Finding 7 (MINOR, Logik).**

- **`GetSymbolBodyTool.cs:94` — `ExtractSymbolBody` zeilenweises Kappen:** `lines = text.Split('\n')` splittet auf `\n`, aber `declaringReference.GetSyntax().ToFullString()` kann CRLF-Zeilenenden enthalten. Konsequenz: bei CRLF-Dateien ist `lines.Length` größer als die im Editor sichtbare Zeilenzahl, der Ellipse-Indikator meldet eine überhöhte `total`-Zeilenzahl. **Kein Defekt, nur kosmetisch** — Body-Inhalt bleibt korrekt, nur die Meta-Zahl weicht ab. Stilistisch suboptimal (`Split('\n')` sollte `Split('\n', StringSplitOptions.None)` + `Replace("\r", "")` o.ä. sein, oder `text.GetLines()` aus `Microsoft.CodeAnalysis.Text`). **NITPICK, kein Finding.**

- **Beobachtung: `SymbolBodyToolRegistrations.cs:60` LOC mit `MaxAIContextFootprint: 2800`:** Override ist 47× höher als die Code-Zeilen. Das deutet darauf hin, dass der Footprint **transitiv** über die Abhängigkeiten wächst (Roslyn `FindReferencesAsync`, `McpCodeGraphServer`, etc.). Der Wert 2800 ist konsistent mit dem Override für `SymbolGraphToolRegistrations.cs:2850`. Ohne Begründung im `step-result.md` bleibt aber unklar, ob der Override **gemessen** oder **auf Verdacht** gesetzt wurde. **Finding 1 (MAJOR, Plan-Erfüllung) — Begründungspflicht.**

### Konzept-Treue (Ebene 4)

- **Muss-Haben D (5 TD-Items):** Alle 5 entweder geschlossen oder bewusst zurückgestellt mit Begründung im `tech-debt.md` (TD-009 bis TD-012 angelegt). Konzept-Vorgabe "Schließen mit Begründung vs. Zurückstellen mit Begründung" ist eingehalten.

- **Muss-Haben E (3 E-Punkte):** Alle 3 umgesetzt mit Test-Coverage. E.1 DoD: `get_symbol_body` + stabile IDs in `get_file_skeleton` + 4. Registrar-Klasse ✓. E.2 DoD: `depth`-Parameter an `find_references`/`get_impact` mit aggregierter Ausgabe ✓. E.3 DoD: DI-Registrierungs-Hinweis als 4. Sektion in `get_type_hierarchy` ✓.

- **Konzept-DoD Z. 650-668:** Funktional erfüllt, **dokumentationsseitig lückenhaft** (siehe Plan-Erfüllungs-Tabelle oben: `Docs/integration.md` + `Docs/ROADMAP.md` nicht aktualisiert). Da das Konzept selbst in Z. 668 "`Docs/ROADMAP.md` Zeilen 478-493 sind von 'Geplant' auf den tatsächlichen Stand aktualisiert, E.1-E.3 sind neu ergänzt" explizit als DoD formuliert, ist die fehlende Aktualisierung **kein NITPICK** sondern eine **echte DoD-Lücke** → Finding 3.

- **Non-Goals (Konzept Z. 457-489):** Keine Editier-Tools, kein Embedding, kein Multi-Sprache-Support, kein Plugin/ALC/DI-Container, kein CLI-Batch-Mode-Replacement, keine Test-Inhalts-Änderungen außerhalb des Scopes. Alle eingehalten.

- **Konzept-Reihenfolge-Vorgabe (Z. 295):** EPIC-07 vor EPIC-08, damit `get_symbol_body` nicht gegen den ohnehin knappen Registrar-Footprint kämpft. Eingehalten: TD-004 (Registrar-Footprint) wurde **vor** E.1 abgehandelt (Entscheidung im `step-plan.md` Z. 47-53 dokumentiert).

### Build-/Test-Status (eigene Reproduktion)

```
git --no-pager log -1 --format='%h %s'         → fed3935 docs(task): step-012 Verifikation ...
dotnet build AiNetLinter.slnx                   → grün (0 Warnungen, 0 Fehler, 0.89 s)
dotnet test  AiNetLinter.slnx --no-build        → grün (1241/1241 in 2:54 min, kein TD-005-Flake)
dotnet run --project src\AiNetLinter -- --config rules.json --path .   → grün (OK, 0 Violations)
offene AiNetLinter.exe/testhost.exe             → keine
```

## Findings

1. `tasks/codegraph-mcp-finish/step-012/step-result.md` — **[MAJOR] [Plan-Erfüllung]** Die 2 neuen PathOverrides in `rules.json` (`src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs: 2700` und `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs: 2800`) sind ohne Begründung gesetzt. Der Plan (`step-012/step-plan.md` Z. 481-483) verlangt explizit: "Falls der Footprint wider Erwarten über 2500 steigt: PathOverride hinzufügen und im `step-result.md` begründen." Auch der DoD-Block Z. 705-707 nennt "Falls die `SymbolBodyToolRegistrations` einen PathOverride braucht: im `step-result.md` mit gemessenem Footprint + Begründung dokumentiert" als Pflicht. **Fix:** gemessenen Footprint pro Override nachholen, Begründung in `step-result.md` ergänzen (kurzer Absatz pro Override, mit Verweis auf den konkreten Type-Dependency-Pfad, der den Wert treibt — vermutlich Roslyn `FindReferencesAsync` + `McpCodeGraphServer`).

2. `Docs/integration.md` — **[MAJOR] [Plan-Erfüllung/Konzept-Treue]** Z. 223 sagt weiterhin "9 granular abfragbare Tools", Z. 277 listet nur 5 Symbolgraph-Tools statt 7. Plan-DoD (`step-012/step-plan.md` Z. 636-638) verlangt: "Hinweis auf 10 Tools (statt 9) im `initialize`-Beschreibungstext; neue Tool-Beispiele für `get_symbol_body` und `depth`-Aufrufe." Konzept-DoD Z. 654-658 impliziert konsistente Doku über alle MCP-bezogenen Dateien. **Fix:** Z. 223 auf "10 granular abfragbare Tools" aktualisieren, Tool-Liste Z. 277 um `get_symbol_body` ergänzen, neue Beispiel-Aufrufe für `get_symbol_body` und `depth`-Parameter ergänzen.

3. `Docs/ROADMAP.md` — **[MAJOR] [Plan-Erfüllung/Konzept-Treue]** E-Block (E.1, E.2, E.3) fehlt. Aktuell nur "EPIC-07 — Test-Infrastruktur" (Z. 475, aus step-008) + "EPIC-08 — Doku" (Z. 476, aus step-008) sind gelistet. Plan-DoD Z. 639-641 verlangt: "B-Block (B.6 + B.7) auf 'Umgesetzt', E-Block (E.1, E.2, E.3) von 'Geplant' auf 'Umgesetzt (step-012)'". Konzept-DoD Z. 659-661: "`Docs/ROADMAP.md` Zeilen 478-493 sind von 'Geplant' auf den tatsächlichen Stand aktualisiert, E.1-E.3 sind neu ergänzt". **Fix:** EPIC-07-Eintrag (Tech-Debt-Abschluss, 4 TD-Items geschlossen + TD-004 zurückgestellt) + EPIC-08-Eintrag (E.1/E.2/E.3 umgesetzt) analog dem EPIC-04/05/06-Stil ergänzen; die existierenden EPIC-07-Test-Infrastruktur und EPIC-08-Doku aus step-008 klar abgrenzen.

4. Commit-Struktur + Subject — **[MAJOR] [Plan-Erfüllung]** Der Plan DoD Z. 707-714 verlangt zwei getrennte Commits ("1. Code-Commit" + "2. Doku-Commit"), beide mit Conventional-Commit-Subject auf Deutsch, imperativ, mit Task-Suffix `[codegraph-mcp-finish]`. Tatsächlich liegt nur ein Code-Commit (`93caa8a`) mit informalem Subject "zwischen commit von 'Coder step-012 EPIC-07+08' abbruch" vor, plus der Orchestrator-Nachtrag `b55b065` (Smoke-Test-Fix). Die Doku-Änderung in `Docs/agent-api.md` ist im Code-Commit mit drin. **Fix:** in einem Folge-Commit den korrekten Conventional-Commit-Subject nachholen (z. B. `docs(mcp): agent-api-um-step-012-erweiterungen [codegraph-mcp-finish]`) für die Doku-Änderungen, und die Coder-Commit-Historie mit `git commit --amend` oder einem Re-Subject-Commit (sofern gewünscht) bereinigen. Empfehlung: als History-Hygiene-Schritt im `step-012/fix-01`.

5. `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs:18` — **[MAJOR] [Rules-Konformität]** XML-Doc enthält das Wort "TD-011-Puffer-bis-Limit-Risiko". `AiNetLinterRichtlinien.mdc` §5 Z. 100 verbietet explizit: "Jede Referenz auf Task-/Planungsartefakte … `TD-005` … Diese Ordner/Dokumente werden nach Task-Abschluss gelöscht; der Verweis wird dann bedeutungslos. Architektur-Rationale gehört als ID-freier *Why*-Kommentar in den Code". **Fix:** "TD-011-Puffer-bis-Limit-Risiko" ersatzlos streichen oder in ID-freies Why umformulieren ("Symbolgraph-Registrar-Klasse ist bereits nahe am projektweiten Footprint-Limit; ein zusaetzliches Tool in derselben Klasse wuerde das Puffer-bis-Limit-Risiko untragbar machen"). Pattern-Vorlage: forward-looking Rationale statt Verweis auf TD-Tracking-IDs.

6. `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` — **[MINOR] [Logik]** `ExecuteAsync_DepthAboveCap_ClampsToThreeAndReturnsResult` (Z. 130-141) prüft nur `Assert.NotEqual(true, result.IsError)`. Der Testname suggeriert "ClampsToThree", aber es wird nicht verifiziert, dass tatsächlich der `depth=3`-Aggregations-Pfad genommen wurde. Ein depth=1-Regression (z. B. ein Refactor, der `clampedDepth = 1` hardcoded) würde den Test weiterhin grün lassen. **Fix:** zusätzlich `Assert.Contains("Treffer gesamt", text)` o.ä., um den Aggregations-Output-Marker zu verifizieren.

7. `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` — **[MINOR] [Logik]** Kein Test prüft, dass `depth` im Git-Branch ignoriert wird. Konzept und Doku sagen explizit "Git-Branch ignoriert depth", aber das Verhalten ist nur über Doku behauptet, nicht getestet. **Fix:** `ExecuteAsync_GitRefBranch_DepthIsIgnored`-Test, der z. B. `depth=3` setzt und verifiziert, dass die Ausgabe **nicht** die Aggregations-Markierung enthält (depth=1-Verhalten trotz `depth=3`-Input).

## Tech-Debt-Einträge aus diesem Review

- `TD-013` (siehe `tech-debt.md`, neu anzulegen) — Drei kaputte/ungeschlossene `<c>`-Tags in XML-Doc-Kommentaren in `Mcp/SymbolGraphToolRegistrations.cs:14-17`, `Mcp/Tools/SymbolIdentifierResolver.cs:14-19`, `Mcp/Tools/GetTypeHierarchyFormatter.cs:14-19` (siehe "außerhalb des Step-Scopes" — diese Bugs existierten bereits vor step-012, sind im Sanierungs-Zug erneut übersehen worden). Sanierung mechanisch (3 Tag-Schließungen), Priorität: niedrig.

- Beobachtung außerhalb TD-013: `FindReferencesTool.cs:29-33` mit abgerissenem XML-Doc-Satz ("stellt dem Aufrufstellen-Output einen … Dateien hat") ist im **Step-Scope** (E.2 hat `FindReferencesTool.cs` angefasst) — gehört als Finding in den **Fix-Step** statt in `tech-debt.md`. Im Fix-Plan oben nicht als separates Finding gelistet, weil es unter "Rules-Konformität" (Finding-Block oben unter "leicht verletzt") schon abgedeckt ist; bei der §5-Sanierung im Fix-Step mit aufnehmen.

## Modell-Info

- `model_kritiker: MiniMax-M3` (Modell-Vorgabe war `claude-sonnet-5, Stufe High`; falls pro Aufruf nicht selektierbar, gilt das Default-Modell)
- Knowledge-Cutoff: 2026-01
- Modi: `step` (Vorgabe Orchestrator)
- Review-Dauer: ca. 8 min Kontext-Aufbau + 5 min Volllauf-Reproduktion + 10 min Befund-Synthese
- Reproduktion: Build 0/0 grün, Tests 1241/1241 grün in 2:54 min, Selbst-Lint 0 Violations, keine offenen Subprozesse
- TD-005-Flake-Status: **nein** (im aktuellen Lauf nicht reproduziert)
