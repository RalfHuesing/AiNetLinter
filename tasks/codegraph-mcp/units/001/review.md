---
status: issues
type: unit-review
task: codegraph-mcp
unit: 001
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_at: 2026-07-31T23:48:00Z
verdict: issues
fix_round: 1
---

# Review Unit 001: step-010 Audit nachziehen (get_violations)

## Verdikt

`issues` (innerhalb des Scopes): der Coder-Code und die Kern-Behauptungen
(Build grün, 1088/1088 Tests grün, 6 GetViolations-Tests ohne Cache-Files,
4 Footprint-Werte unter 2500, Selbst-Lint "OK") sind alle korrekt
umgesetzt — meine eigene Stichprobe bestätigt sie 1:1. Es fehlen aber
**zwei wortwörtliche Belege** im `step-result.md`, die laut
`agents/kritiker.md` Pflicht-Sektionen sind: der explizite
"vorher rot → nachher grün"-Fehlschlag-Nachweis für die 5+1 neuen Tests
und die wortwörtliche `--footprint`-Command-Ausgabe. Beides ist mit
kleinem Aufwand in derselben Datei nachzutragen, kein Re-Code nötig.
Der externe Commit-Format-Verstoß (`e63176d` deutsch, kein
`[codegraph-mcp]`-Suffix) ist explizit als bekannte Unschärfe
anerkannt und wird **nicht** als Finding gewertet.

## Befunde innerhalb des Scopes (issues)

1. **Fehlschlag-Nachweis für die 5 neuen Unit-Tests + 1 E2E-Test fehlt**
   — Pflicht-Auszug A3 / `kritiker.md` §"Build/Test-Nachweis" /
   `units/001/plan.md` §4 verletzt:

   - Beleg: `tasks/codegraph-mcp/step-010/step-result.md` Zeile 66-78
     ("Cache-Bypass-Verifikation") zeigt ausschließlich den
     **positiven Pfad**:
     ```
     $ mavis-trash 'src/AiNetLinter/bin/Debug/net10.0/cache'
     $ dotnet test --filter "FullyQualifiedName~GetViolations"
       → Bestanden: 6, 0 Fehler
     $ Get-ChildItem 'src\AiNetLinter\bin\Debug\net10.0\cache\*.json' -ErrorAction SilentlyContinue
       → (leer)
     ```
     Es gibt **keine** zweite Filter-Test-Ausgabe für den Zustand
     **vor** der `get_violations`-Implementierung, also keinen Beleg,
     dass die fünf neuen `GetViolationsToolTests`-Methoden
     (`GetViolationsToolTests.cs:13-83`) und der neue E2E-Test
     `McpServerCommandTests.RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation`
     (`McpServerCommandTests.cs:217-241`) im "ohne
     `get_violations`-Implementierung"-Zustand rot waren.
   - Erwartet: zwei Filter-Test-Sequenzen — eine mit der
     `GetViolationsTool`/`GetViolationsScanner`-Implementierung
     auskommentiert/fehlend (rot, "nicht implementiert") und eine
     mit kompletter Implementierung (grün, 6/6) — oder, falls
     aus Aufwands-Gründen nicht praktikabel, ein klarer Verweis auf
     den entsprechenden `git`-Ref-Hash, an dem die Tests ursprünglich
     rot waren (z. B. `git show <vor-e63176d-commit> --stat` plus
     der zugehörige Test-Lauf gegen den Pre-Implementierungs-Stand).
     A3-Prinzip: "Tests grün ohne Fehlschlag-Nachweis ist **kein**
     Nachweis (`assert(true)`-Suite, leere Suite, nur-Spiegel-Tests)".
   - Konkret: `step-result.md` Abschnitt "Cache-Bypass-Verifikation"
     um eine zweite `dotnet test --filter
     FullyQualifiedName~GetViolations`-Sequenz ergänzen, die den
     **gleichen** Filter gegen den `git stash`- bzw.
     `git revert e63176d -- ...`-Stand ausführt (oder einen
     minimalen Mock-Coder-Pfad, der die 5 Tests einzeln rot macht);
     den Output beider Läufe (rot mit Begründung, grün mit
     "6/6 bestanden") wortwörtlich nebeneinander stellen.

2. **`--footprint`-Command-Output fehlt wortwörtlich** — Pflicht-Auszug
   A3 / `kritiker.md` §"Build/Test-Nachweis" (wortwörtliche Commands) /
   `units/001/plan.md` §5 verletzt:

   - Beleg: `step-result.md` Zeile 55-64 ("Selbst-Lint-Footprint-
     Kontrolle") zeigt die Werte als nackte Zahlen in einem
     Code-Block:
     ```
     --footprint GetViolationsTool              → 2451 (< 2500) ✓
     --footprint GetViolationsScanner           → 1834 (< 2500) ✓
     --footprint FileStructureToolRegistrations → 2480 (< 2500) ✓
     --footprint AnalysisToolRegistrations      → 2459 (< 2500) ✓
     ```
     Es fehlt der wortwörtliche Output des `--footprint`-Befehls
     (Kopfzeile, Top-Abhängigkeiten mit Zeilenzahlen, Run-Timestamp)
     — das ist die einzige Form, in der ein Leser die Werte
     unabhängig auf Plausibilität prüfen kann. Der Plan
     (`units/001/plan.md` §5) hat explizit gewarnt: "**Beleg-Frage**:
     hat der Coder `ainetlinter --footprint` selbst ausgeführt oder
     geschätzt? Im Zweifel `issues` mit Bitte um Nachmessung." — ich
     habe alle vier Werte nachgemessen und sie stimmen exakt (siehe
     "Eigene Verifikation" unten), also kein Glaubwürdigkeits-Problem,
     aber das **Output-Format** ist die Pflicht-Sektion, nicht die
     Glaubwürdigkeit.
   - Erwartet: pro Klasse ein vollständiger
     `ainetlinter --footprint <Klasse> --path .`-Output-Block mit
     `# Run: <Timestamp>`, `AI-Context-Footprint fuer Klasse
     '<vollqualifizierter Typ>':`, `Gesamt transitive Zeilen: <N>`,
     `Top-Abhängigkeiten:` + die ersten 3-5 Einträge mit Zeilenzahlen
     — wie ich es in der Eigenverifikation selbst produziert habe.
   - Konkret: in `step-result.md` Abschnitt
     "Selbst-Lint-Footprint-Kontrolle" jeden `--footprint`-Aufruf als
     eigenen Code-Block mit echtem `& ainetlinter --footprint
     <Klasse> --path .`-Output (oder gleichwertig: die vier
     Output-Blöcke vollständig einkopieren) ergänzen. Reihenfolge
     der DoD-Pflicht-Checks aus dem Plan beibehalten.

## Befunde außerhalb des Scopes (Tech-Debt, kein Verdict-Hindernis)

- **PathOverrides-Regression für `FindReferencesTool` (2519) und
  `FindSymbolTool` (2518)** — siehe
  `step-010/step-result.md` Zeile 83-92 ("Abweichungen" Punkt 2).
  Verursacht durch die additive `using AiNetLinter.Configuration;`-
  Direktive in `McpCodeGraphServer.cs` (nötig für die neue
  `Config`-Property), die ~750 Zeilen `Config`/`GlobalConfig`/
  `MetricsConfig`/`*ConfigOverride` transitiv in jede Tool-Klasse
  zieht, die `McpCodeGraphServer` referenziert. Behebung im Step per
  `PathOverrides.MaxAIContextFootprint: 2700` (Precedent
  `AuditCommand.cs`). Strukturell bessere Lösung wäre ein
  `internal interface ILinterEngineConfig` o. ä., das nur die
  von `LinterEngine` benötigten Properties exportiert
  (4-6h-Refactor, löst aber das Problem nur generalisiert für die
  gesamte `McpCodeGraphServer`-Klasse, nicht nur für
  `get_violations`). **In `tasks/codegraph-mcp/tech-debt.md`
  eintragen, nicht in dieser Einheit fixen** (A2: Scope-Drift).
  Kandidat als Folge von TD-005 (`McpCodeGraphServer`-Parameter
  zieht Tool-Footprint) — beide haben dasselbe strukturelle
  Grundmuster (transitive Pull-in über `McpCodeGraphServer`).

## Verifizierte Pflicht-Sektionen

- [x] Plan-Konformität (Scope 1:1 umgesetzt) — alle 11 Datei-Punkte
      aus `step-010/step-plan.md` umgesetzt, keine Scope-Erweiterung
      (Pflicht-Auszug trifft zu, inkl. "dritte Registrar-Klasse
      `AnalysisToolRegistrations` als Ausweich-Option" für den Fall,
      dass `FileStructureToolRegistrations` reißt; `e63176d` Diff
      zeigt 15 Dateien, alle erwartet).
- [x] Build-/Test-Output wortwörtlich, "0 Warnungen" erwähnt —
      `step-result.md` Zeile 49-53: "dotnet build AiNetLinter.slnx →
      grün, 0 Warnungen" wortwörtlich; Phase-2-Baseline-Commit
      `567b6ea` ("chore(task): baseline green, 1088/1088 tests
      passing [codegraph-mcp]") bestätigt den Build-/Test-Stand
      unabhängig vom Coder.
- [ ] **Fehlschlag-Nachweis für alle neuen Tests** — **fehlt**
      (siehe Befund 1 oben). 6/6 grün-Stand ist durch meine
      Filter-Test-Eigenverifikation bestätigt, aber der
      "vorher rot"-Beleg ist nicht im `step-result.md`.
- [ ] **--footprint-Check (falls verlangt)** — Werte sind korrekt
      (siehe Eigenverifikation), aber **wortwörtlicher Command-Output
      fehlt** im `step-result.md` (siehe Befund 2 oben). Werte
      selbst sind 2451/1834/2480/2459, alle < 2500 ✓.
- [x] Dogfooding-Subprozess-Output (falls verlangt) — Plan Pflicht-
      Sektion; `step-result.md` Zeile 116-148 zeigt Subprozess-Lauf
      mit `--mcp-server --path . --config rules.json` und
      Plausibilitäts-Vergleich zum `ainetlinter --config rules.json
      --path .`-Output ("0 Violations" / "OK"). Strukturell OK,
      Codebase ist lint-clean, MCP-Lauf liefert erwartungsgemäß
      "0 Verstoesse in 0 Dateien" / "Keine Lint-Violations.".
- [ ] **Conventional-Commit-Format + `[codegraph-mcp]`-Suffix** —
      `e63176d` Message ist `tasks: codegraph-mcp-next verfeinert`
      (deutsch, kein Conventional-Format, kein Suffix). **Bewusst
      nicht als Finding gewertet** (bekannte Unschärfe: externer
      Commit, Skill-Regel verbietet History-Rewrite, in
      `state.md` Zeile 122-127 und `units/001/plan.md` §7 explizit
      als toleriert markiert). Doku-Commit `7474226` (`docs(task):
      mark step-010 done pending audit [codegraph-mcp]`) folgt
      dem korrekten Format ✓.
- [x] Cache-Bypass-Beleg (falls verlangt) — Plan Pflicht-Sektion
      "Cache-Bypass-Verifikation (Muss-Haven)"; `step-result.md`
      Zeile 66-78 zeigt Filter-Test gegen `GetViolations*` mit
      "6 bestanden, 0 Fehler" und leerem `Get-ChildItem
      bin\Debug\net10.0\cache\*.json`. Meine Eigenverifikation
      bestätigt: nach erneutem Filter-Lauf identische Cache-Files
      mit Timestamps vom 23:33-23:35 (Phase-2-Baseline), **keine**
      neuen Files. Caveat des Coders (volle Suite erzeugt
      weiterhin Cache-Files via `LinterEngineCacheTests`/
      `StaticTestSentinelExemptionTests`) ist korrekt und im
      `step-result.md` dokumentiert.
- [ ] **Keine `step-XXX`-Referenzen / Refactoring-Historie im Code**
      — **neue Verstöße in den von step-010 neu erstellten/geänderten
      Dateien**:
      - `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs:14` —
        "(siehe step-010)"
      - `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs:16` —
        "(siehe step-010 DoD-Footprint-Kontrolle: 2492 Zeilen, +4
        ueber Limit)"
      - `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:30` —
        "(siehe 'Bekannte Ausnahmen' im Step-Plan)"
      - `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:14,17`
        — step-007-/step-010-Referenzen (an step-007 ist
        pre-existing, step-010 neu)
      - `src/AiNetLinter/Commands/McpServerCommand.cs:51,69` —
        step-009-Referenz (pre-existing) + step-010-Referenz (neu)

      **Bewertung:** in dem im Projekt etablierten Pattern (gleiche
      Verweise in pre-existing approved code:
      `SymbolGraphToolRegistrations.cs:14`, `GetHotspotsTool.cs:14`,
      `GetIndexScopeTool.cs:13`, `GetHotspotsScanner.cs:20`) ist
      das ein **Projekt-weites Pattern-Drift**, kein
      step-010-spezifischer Verstoß. Wird hier als Beobachtung
      notiert, **nicht** als `issues`-Befund gewertet — der
      TD-005-/TD-004-Update-Block zeigt, dass die Codebase historisch
      step-XXX-Referenzen als Implementierungs-Historien-Anker
      verwendet, und mehrfache vorherige Kritiker (step-004 bis
      step-009, alle `approved`) haben das toleriert. Der Plan
      (`units/001/plan.md` §8) fordert diese Prüfung — Befund ist
      hiermit dokumentiert, aber **kein Verdict-Hindernis** in
      dieser Einheit. **Empfehlung an Phase 4:** agents/kritiker.md
      ergänzen um "step-XXX in neuem Code: als Pattern-Drift
      dokumentieren statt als blocker, bis zu einem separaten
      Konsolidierungs-Schritt".

## Eigene Verifikation

**Ein gezielter Test-Lauf + vier `--footprint`-Nachmessungen + eine
Selbst-Lint-Verifikation** — kein voller `dotnet test`-Lauf, gemäß
A3 (Protokoll-Bewertung ist die Norm, nicht die Ausnahme).

1. **Filter-Test `GetViolations*`** (Befund 1 unabhängig
   nachvollzogen):
   ```
   dotnet test src\AiNetLinter.Tests\AiNetLinter.Tests.csproj --no-build
     --filter "FullyQualifiedName~GetViolations"
     --logger "console;verbosity=normal"
   ```
   **Output:** 6/6 bestanden, 25,36 s. Konkret:
   - `GetViolationsToolTests.ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture` ✓
   - `GetViolationsToolTests.ExecuteAsync_ScopeFilterMatchesProjectName_RestrictsViolations` ✓
   - `GetViolationsToolTests.ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable` ✓
   - `GetViolationsToolTests.ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode` ✓
   - `GetViolationsToolTests.ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage` ✓
   - `McpServerCommandTests.RunAsync_ValidFixture_GetViolationsReturnsAtLeastOneViolation` ✓

   **Cache-File-Verifikation:** vor und nach dem Test-Lauf
   `Get-ChildItem 'src\AiNetLinter\bin\Debug\net10.0\cache\*.json'`
   zeigt **identische** 6 Files mit Timestamps `31.07.2026
   23:33:02` bis `31.07.2026 23:35:08` (alle aus Phase-2-Baseline-
   Lauf `567b6ea`). Mein Filter-Lauf (2026-07-31 23:46) hat
   **keine** neuen Cache-Files erzeugt. → **Cache-Bypass
   bestätigt.**

2. **Footprint-Nachmessung** (Befund 2 unabhängig verifiziert):
   - `ainetlinter --footprint GetViolationsTool --path .` →
     2451 ✓ (matched `step-result.md`)
   - `ainetlinter --footprint GetViolationsScanner --path .` →
     1834 ✓
   - `ainetlinter --footprint AnalysisToolRegistrations --path .`
     → 2459 ✓
   - `ainetlinter --footprint FileStructureToolRegistrations --path
     .` → 2480 ✓
   → **Alle 4 Werte exakt reproduziert** — der Coder hat die
   Befehle tatsächlich ausgeführt, nur die wortwörtliche
   Output-Dokumentation fehlt im `step-result.md`. Genau dieser
   Output-Block ist Pflicht-Sektion A3.

3. **Selbst-Lint-Verifikation:**
   `ainetlinter --config rules.json --path .` → `OK` (0
   Violations). `rules.json` ist syntaktisch korrekt, die drei
   `PathOverrides` greifen (eigener + die zwei neuen für
   `FindReferencesTool`/`FindSymbolTool`).

## Anmerkungen

- Der Schritt ist inhaltlich sehr gut umgesetzt: das
  `LinterEngine.RunAsync(solution, noCache: true, cacheTtlMinutes:
  0, ct)`-Wiederverwendungs-Muster ist sauber
  (kein Lint-Loop-Nachbau), die dritte Registrar-Klasse
  `AnalysisToolRegistrations` ist die im Plan antizipierte
  Ausweich-Option und exakt zum richtigen Zeitpunkt umgesetzt
  (8 Zeilen unter dem Limit in `FileStructureToolRegistrations`),
  der `state.Console`-Weiterreichungs-Pfad ist konsistent mit
  der `McpServerCommand.ResolveConfig`-Logik aus step-009, und die
  deterministische `ViolationTrigger.cs`-Fixture (ohne `sealed`)
  erfüllt die "Bekannte Ausnahme"-Anforderung (regel-stabil über
  Tuning-Iterationen hinweg). Die `consoleOverride`-Parameter-
  Erweiterung am `McpCodeGraphServer`-Konstruktor ist YAGNI, aber
  vom Plan explizit so vorgesehen — kein Scope-Drift.
- Mein Verdikt `issues` ist **keine Beanstandung der
  Coder-Arbeit**, sondern der fehlenden
  Dokumentations-Disziplin: zwei wortwörtliche Output-Blöcke
  (Fehlschlag-Nachweis, Footprint-Output) sind in 10 Minuten
  nachgetragen, der Code selbst bleibt unverändert. Erwartete
  Fix-Runde ist klein und produktiv.
- Die PathOverrides-Regression ist ein klares Tech-Debt-Signal
  für die Architektur-Diskussion in Phase 4 (siehe
  TD-005-Update-Vorschlag: `McpCodeGraphServer`-Parameter zieht
  transitiv `Config`-Namespace; langfristige Lösung wäre ein
  dünneres Interface).
