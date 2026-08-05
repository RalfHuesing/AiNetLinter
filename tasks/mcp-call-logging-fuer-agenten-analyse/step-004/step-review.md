---
status: issues
type: step-review
verdict: issues
mode: step
task: mcp-call-logging-fuer-agenten-analyse
step: 004
reviewer: kritiker
reviewed_at: 2026-08-05T15:20:00+02:00
---

# Step-Review: step-004 (Doku-Sync + End-to-End-Verifikation)

## Verdict

**`issues`** — Zwei MAJOR-Findings. Inhaltlich ist der Step zu ~95 % korrekt, aber zwei
reale Lücken verhindern ein `approved`:

1. **item-01: Doku-Schema `error_type` widerspricht der Implementierung.**
   `Docs/agent-api.md:346` behauptet „Vollständiger Exception-Typ-Name (z. B.
   `System.InvalidOperationException`)" und das Beispiel bei `:353` zeigt
   `"error_type":"System.InvalidOperationException"`. Der Code
   (`McpCallLog.cs:121`) serialisiert aber `exception.GetType().Name` (ohne
   Namespace → `"InvalidOperationException"`), und die Tests
   (`McpCallLogTests.cs:169`, `:361`) assertieren genau dieses simple Format.
   User/Agenten, die der Doku folgen, werden von der Realität überrascht.
2. **item-06: Test-Count-Diskrepanz im `step-result.md`.** Der Plan und das
   Result-File behaupten `McpServerCommandCallLogTests` 5/5 grün; der
   tatsächliche Count ist **9/9** (1 `PathNotSet` + 2 RelativePath/AbsolutePath
   4-param + 4 neue + 2 unveränderte `ResolveMcpLogPath_*`). Der TD-001-Breakdown
   (1+3+4=8 Änderungen) ist korrekt, addiert sich aber nicht zur Gesamtzahl
   9 — die zwei `ResolveMcpLogPath_*`-Tests wurden in der Notiz nie
   mit-erwähnt, was dazu führte, dass die „5/5"-Folgezahl in step-result.md
   ungeprüft übernommen wurde.

Build und Test-Volllauf sind sonst sauber: `dotnet build` 0/0, `dotnet test`
1279/1279 grün (2 m 13 s), `McpCallLogTests` 14/14, `CliIntegrationTests`
29/29.

## Pro-Item-Befund (alle 4 Ebenen)

### item-01 — `Docs/agent-api.md:311-354` (Default-Pfad + Error-Schema)

**Ebene 1 (Plan-Erfüllung):** Diff +19/-2, alle vom Plan geforderten
Änderungen sind im File:
- Default-Pfad Z. 317 auf `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` korrigiert.
- Pfad-Auflösung Z. 339 erweitert (Default-Pfad, Exit-1, Auto-Delete-Satz erhalten).
- Neuer Error-Schema-Block Z. 341-354 mit Felder-Tabelle + `get_file_skeleton`-Beispiel.
- Args-200-Cap im Schema-Block Z. 326 jetzt explizit („max. 200 Zeichen + `...`“).
- Anchor `### Call-Log (opt-in)` bei Z. 311 (Anchor `#call-log-opt-in`) bleibt valide.
**PASS**

**Ebene 2 (Rules-Konformität):** Keine Task-/Step-/EPIC-/TD-Verweise im
neuen Doku-Text. Keine Umlaut-Sonderzeichen-Probleme (Konvention `Docs/`
ist deutsche Kleinschreibung mit Umlauten, das wird konsequent durchgehalten —
auch im neuen Block). Die in `AiNetLinterRichtlinien.mdc` §1 geforderte
„Docs/agent-api.md für Tool-Verträge"-Zuordnung ist eingehalten.
**PASS**

**Ebene 3 (Logische Korrektheit):** **MAJOR — Schema-Mismatch bei
`error_type`.**

`McpCallLog.cs:121` serialisiert
```csharp
error_type = exception.GetType().Name
```
Das liefert für `new InvalidOperationException(...)` den String
`"InvalidOperationException"`, nicht `"System.InvalidOperationException"`.
Verifiziert via `GetType().Name`/`GetType().FullName` Powershell-Probe
und durch die existierenden Tests in
`McpCallLogTests.cs:169` (`Assert.Equal("TestException", ...)`) und
`:361` (`Assert.Equal("InvalidOperationException", ...)`).

`Docs/agent-api.md:346` sagt aber:
> `error_type` | string | Vollstaendiger Exception-Typ-Name (z. B.
> `System.InvalidOperationException`)

Und das Beispiel bei `:353` zeigt:
> `"error_type":"System.InvalidOperationException"`

Beides ist faktisch falsch — die Implementierung wird **nie** den vollen
Namespace-Pfad liefern. Ein Leser der Doku, der nach
`System.InvalidOperationException` grep-t, findet keinen Eintrag im Log.

Der restliche Block ist konsistent: `ts`/`tool`/`args` identisch zum
Call-Schema, `level=error` immer, `error_message` = `Exception.Message`,
`stack_trace` mit 4 KB Cap + `...`-Marker. Der `get_file_skeleton`-
Beispielstacktrace ist 2 Frames kurz und endet mit `..."` (Truncation-
Marker) — passt zur Implementierung, illustriert aber den 4-KB-Cap nicht
explizit (siehe MINOR unten).
**FAIL** (Severity MAJOR)

**Ebene 4 (Konzept-Treue):** Default-Pfad-Konvention korrekt dokumentiert
(DoD 1, Konzept Z. 134). Error-Schema dokumentiert (DoD 2, Konzept Z. 135).
Args-200-Cap + Stack-Trace-4-KB-Cap explizit genannt. Anchor
`#call-log-opt-in` bleibt für `configuration.md`-Link valide. Non-Goals
respektiert: kein Hot-Reload-Hardening, kein Serilog, keine
Rotation-Strategie.
**PASS** (Ebene 4 ist erfüllt; das Schema-Mismatch ist Ebene 3)

---

### item-02 — `Docs/configuration.md:1087` (`--mcp-log`-Eintrag)

**Ebene 1 (Plan-Erfüllung):** Diff +1/-1, alle vom Plan geforderten
Ergänzungen sind drin:
- Default-Pfad aktualisiert auf `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`.
- `ArgumentArity.ZeroOrOne`-Hinweis ergänzt.
- Exit-1-Hinweis bei nicht auflösbarer Solution ergänzt.
- Error-Schema-Verweis auf `[Docs/agent-api.md#call-log-opt-in]` ergänzt.
**PASS**

**Ebene 2 (Rules-Konformität):** Sauber, keine Task-/Step-/EPIC-/TD-Verweise.
Die `AiNetLinterRichtlinien.mdc` §1-Zuordnung „Docs/configuration.md für
CLI-Optionen" ist eingehalten.
**PASS**

**Ebene 3 (Logische Korrektheit):** Inhaltlich konsistent zu `agent-api.md`:
- Default-Pfad identisch zu `agent-api.md:317`.
- Exit-1-Verhalten identisch zu `agent-api.md:339`.
- Auto-Delete-Logik erhalten.
- Anchor `#call-log-opt-in` matched (Heading in `agent-api.md:311`).
**PASS**

**Ebene 4 (Konzept-Treue):** DoD 6 erfüllt. CLI-Option-Spec ist aktualisiert.
**PASS**

---

### item-03 — `Docs/ROADMAP.md` (Meilenstein-Eintrag)

**Ebene 1 (Plan-Erfüllung):** **MINOR — Plan-Abweichung.** Der Plan sagte
„Epic 20: MCP-Call-Log: Pfad-Konvention und Error-Sink" vor Z. 140. Tatsächlich
wurde **EPIC-09** in der bestehenden `## MCP-Codegraph-Server (EPIC-01..08)`-
Sektion (Z. 477-482) angelegt. Im step-result.md (Z. 65-76) ist die Abweichung
dokumentiert und begründet: aktuelle Roadmap hat bereits Epics 1-33 + eine
separate MCP-Sektion mit eigener EPIC-Nummerierung. Eine zweite „Epic 20"
wäre ein Duplikat gewesen. Inhaltlich 1:1 zu den 5 abgehakten Items aus
dem Plan (Default-Pfad, Error-Sink, ExecuteCallAsync, CLI-Option, Tests).
Die Begründung ist plausibel — der Planer-Snapshot in Z. 140 war veraltet.
Inhalt vollständig vorhanden, nur die Position wurde an die aktuelle
Datei-Struktur angepasst. **Bewertung: angemessen.**
**PASS** (mit dokumentierter Abweichung — kein Logik- oder Konzept-Konflikt)

**Ebene 2 (Rules-Konformität):** Sauber, keine Task-/Step-/EPIC-/TD-Verweise.
**PASS**

**Ebene 3 (Logische Korrektheit):** Inhaltlich korrekt. EPIC-09 baut auf
EPIC-06 (B.7) auf, was im Eintrag explizit erwähnt wird. Die 5 Sub-Items
decken exakt den Plan-Inhalt ab.
**PASS**

**Ebene 4 (Konzept-Treue):** Muss-Habe „Doku" (Konzept Z. 48) erfüllt.
Sichtbarkeit der Features in der Projekt-Roadmap hergestellt.
**PASS**

---

### item-04 — `tasks/.../roadmap.md:61` (TD-001-Korrektur)

**Ebene 1 (Plan-Erfüllung):** Diff +1/-1, TD-001-Notiz korrekt
ersetzt. Neue Formulierung listet explizit: 1 gelöscht
(`TryCreateCallLog_WhitespacePath_ReturnsNull`), 3 angepasst auf 4-Param-
Signatur, 4 neue mit Test-Namen
(`TryCreateCallLog_WhitespacePath_CreatesDefaultLog`,
`TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`,
`BuildDefaultLogPath_WithSolution_IncludesSolutionName`,
`BuildDefaultLogPath_DateIsLocal`).
**PASS**

**MINOR Ebene 1:** Die Notiz zählt 1+3+4=8, aber die Datei hat 9 Tests —
die 2 unveränderten `ResolveMcpLogPath_AbsolutePath_ReturnsAsIs` und
`ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory`
werden nicht erwähnt. Das ist keine Falschangabe (die 1+3+4-Breakdown
beschreibt nur, was sich **geändert** hat), aber ein Leser erwartet
vielleicht 8 Tests statt 9, weil die Roadmap-Notiz den Status der
ResolveMcpLogPath-Tests schuldig bleibt. **Kosmetisch.**
**PASS** (mit MINOR-Anmerkung)

**Ebene 2 (Rules-Konformität):** Sauber, keine Task-/Step-/EPIC-/TD-Verweise
(neben dem TD-001-Verweis, der hier der eigentliche Inhalt ist).
**PASS**

**Ebene 3 (Logische Korrektheit):** Verifiziert:
- `WhitespacePath_ReturnsNull` ist tatsächlich weg (im File nicht mehr vorhanden).
- 3 Tests mit 4-Param-Signatur vorhanden: `PathNotSet_ReturnsNull` (Z. 30),
  `RelativePath_...` (Z. 50), `AbsolutePath_...` (Z. 75).
- 4 neue Tests mit den genannten Namen vorhanden.
- 2 `ResolveMcpLogPath_*`-Tests mit 2-Param-Signatur vorhanden (unverändert
  geblieben, was korrekt ist, da `ResolveMcpLogPath` selbst nicht betroffen war).
**PASS**

**Ebene 4 (Konzept-Treue):** TD-001 geschlossen, Roadmap ist jetzt
konsistent mit dem realen step-001-Scope. Wichtig für künftige Step-Mode-
Planer, die den Index lesen.
**PASS**

---

### item-05 — `src/AiNetLinter/Cli/CliOptionFactory.cs:230-233` (Description-Update)

**Ebene 1 (Plan-Erfüllung):** Diff +1/-1, **genau 1 Zeile** `Description`
ersetzt. `Arity = ArgumentArity.ZeroOrOne` (Z. 233) unverändert.
**PASS**

**Ebene 2 (Rules-Konformität):** Sauber:
- Keine Task-/Step-/EPIC-/TD-Verweise im Description-Text (Richtlinien §5
  Clean-Code-Kommentar-Politik).
- ASCII-only (Umlaute als `ae`/`oe`/`ue`/`ss` ausgeschrieben — passt zum
  bestehenden Stil der Datei, der auch in der ursprünglichen Description
  verwendet wurde).
- Keine Sonderzeichen, keine Anführungszeichen-Probleme.
- `AiNetLinterRichtlinien.mdc` §5 Zero-Warning-Direktive: Build war 0/0.
**PASS**

**Ebene 3 (Logische Korrektheit):** Description beschreibt:
- Default deaktiviert (kein File I/O).
- ZeroOrOne-Verhalten mit Default-Pfad-Konstruktion.
- Exit-1 bei nicht auflösbarer Solution.
- Pfad-Auflösung bei explizitem Wert (absolut/relativ).
- Beispiel: `--mcp-log ./.mcp-log/calls.log`.

Inhaltlich konsistent zu `Docs/agent-api.md` und `Docs/configuration.md`.
**PASS**

**Ebene 4 (Konzept-Treue):** DoD 6 (Doku-Synchronität) für den CLI-Description-
Touchpoint erfüllt. Risiko-Einschätzung aus dem Plan („1 Zeile Text, keine
Logik") korrekt umgesetzt.
**PASS**

---

### item-06 — `dotnet test`-Volllauf (Verifikation)

**Ebene 1 (Plan-Erfüllung):** Volllauf wurde durchgeführt und in step-result.md
dokumentiert. Test-Anzahl + Dauer vorhanden.
**PASS** (mit Vorbehalt, siehe Ebene 3)

**Ebene 2 (Rules-Konformität):** Trivial — keine Code-Änderung.
**PASS**

**Ebene 3 (Logische Korrektheit):** Verifiziert durch eigenen Lauf:
- `dotnet build` → 0/0 (3.4 s).
- `dotnet test` Volllauf → 1279/1279 grün (2 m 13 s — Plan-Claim war 2 m 6 s,
  normaler Varianzbereich für multi-process Integration-Tests).
- `McpCallLogTests` → 14/14 grün (Regressions-Schutz bestätigt).
- `McpServerCommandCallLogTests` → **9/9** grün (nicht 5/5 wie im
  step-result.md behauptet, siehe MAJOR unten).
- `CliIntegrationTests` → 29/29 grün (Dogfooding-Hund grün — keine
  Lint-Regression auf den 5 McpCallLog-Konsumenten).
- `McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
  ist der Long-Running-Test, der die 1 m 24 s ausmacht — kein Fehler, Test-
  Charakteristik.

**MAJOR Ebene 3 — Test-Count-Fehler in step-result.md:** Der Plan (Z. 96-97)
und step-result.md (Z. 49, 58) behaupten konsistent
`McpServerCommandCallLogTests` 5/5 grün. Tatsächlich sind es **9 Tests**
im File `McpServerCommandCallLogTests.cs` (Z. 22-184, 9 [Fact]-Attribute):
1. `TryCreateCallLog_PathNotSet_ReturnsNull`
2. `TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir`
3. `TryCreateCallLog_AbsolutePath_CreatesLogFileAtGivenPath`
4. `TryCreateCallLog_WhitespacePath_CreatesDefaultLog`
5. `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`
6. `BuildDefaultLogPath_WithSolution_IncludesSolutionName`
7. `BuildDefaultLogPath_DateIsLocal`
8. `ResolveMcpLogPath_AbsolutePath_ReturnsAsIs`
9. `ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory`

Die item-04-Roadmap-Notiz zählt korrekt 1+3+4=8 Änderungen, lässt aber
die 2 unveränderten `ResolveMcpLogPath_*`-Tests unerwähnt → ergibt 8 statt
9. Im Plan und step-result.md wurde daraus fälschlich „5/5", was weder
zur 8 (1+3+4) noch zur 9 (8+2) passt. Coder hat die Zahl ungeprüft aus
dem Plan übernommen.
**FAIL** (Severity MAJOR)

**Ebene 4 (Konzept-Treue):** DoD 5 (Test-Stabilität) und DoD 4 (Volllauf
1279/1279) sind **tatsächlich** erfüllt — die 1279-Zahl stimmt im Volllauf,
nur die Sub-Count-Aussage für `McpServerCommandCallLogTests` ist falsch
dokumentiert. DoD 1-3, 6, 7 sind in step-result.md korrekt durchgegangen.
**PASS** (mit MAJOR-Dokumentations-Korrektur für Sub-Count)

## Findings (Übersicht)

| # | Item | Ebene | Severity | Datei:Zeile | Befund |
|---|------|-------|----------|-------------|--------|
| 1 | item-01 | 3 | MAJOR | `Docs/agent-api.md:346` und `:353` | `error_type` wird als „Vollständiger Exception-Typ-Name (z. B. `System.InvalidOperationException`)" beschrieben und im Beispiel so gezeigt, aber `McpCallLog.cs:121` serialisiert `exception.GetType().Name` (ohne Namespace). Tests in `McpCallLogTests.cs:169` und `:361` assertieren das simple Format. Doku widerspricht Code. |
| 2 | item-06 | 3 | MAJOR | `step-004/step-result.md:49` und `:58` (sowie `step-plan.md:96`) | `McpServerCommandCallLogTests` 5/5 dokumentiert, tatsächlich 9/9. Breakdown 1+3+4=8 Änderungen + 2 unveränderte `ResolveMcpLogPath_*` = 9 Total. Coder hat 5/5 ungeprüft aus dem Plan übernommen. |
| 3 | item-04 | 1 | MINOR | `tasks/.../roadmap.md:61` | TD-001-Notiz zählt 1+3+4=8, ignoriert aber die 2 unveränderten `ResolveMcpLogPath_*`-Tests in der Gesamtzahl. Wer die Roadmap liest, könnte 8 Tests statt 9 erwarten. |
| 4 | item-03 | 1 | MINOR | `Docs/ROADMAP.md:477` (Plan-Abweichung) | EPIC-09 statt EPIC-20 — angemessen begründet, weil aktuelle Roadmap bereits Epics 1-33 + separate MCP-Sektion hat. Inhaltlich 1:1 zum Plan. Dokumentiert. |

## Tech-Debt-Beobachtungen

**Keine neuen substantiellen Tech-Debt-Einträge erforderlich.**

- Finding #1 (error_type-Schema) ist ein **direkter Step-interner Doku-Fix**,
  nicht ein latentes Architektur-Problem. Empfohlene Korrektur: Doku an Code
  anpassen (Description „Exception-Typ-Name (kurz, ohne Namespace, z. B.
  `InvalidOperationException`)" + Beispiel `"error_type":"InvalidOperationException`).
  Die Alternative (`FullName` im Code) wäre eine Schema-Änderung und damit
  out-of-scope für EPIC-04.
- Finding #2 (Test-Count 5/5 vs. 9/9) ist ein **step-result.md-Tippfehler**,
  gehört zur unmittelbaren Korrektur.
- TD-001 + TD-002 sind bereits in `tech-debt.md` und bleiben unverändert.
- Beobachtung im step-result (LF/CRLF-Warnung in `AiNetLinter.mdc` ohne
  Inhalts-Diff) ist ein bekanntes Cosmetic-Thema aus früheren Auto-Syncs,
  nicht step-004-relevant.

## Empfohlene Korrekturen (in Reihenfolge der Wichtigkeit)

1. **`Docs/agent-api.md:346`** ändern auf z. B.:
   > `error_type` | string | Exception-Typ-Name ohne Namespace (z. B. `InvalidOperationException`)

2. **`Docs/agent-api.md:353`** Beispiel-`error_type` von `"System.InvalidOperationException"` auf `"InvalidOperationException"` korrigieren.

3. **`step-004/step-result.md:49` und `:58`** „5/5" durch „9/9" ersetzen (oder zumindest die korrekte Sub-Count-Aussage mit den 1+3+4+2 = 9 Tests). Falls `step-plan.md:96` ebenfalls die 5/5-Zahl trägt, dort gleich korrigieren.

4. **Optional (`tasks/.../roadmap.md:61`)** Klarstellung, dass die 8 Änderungen
   + 2 unveränderte `ResolveMcpLogPath_*` = 9 Tests ergeben. Aktuell nicht
   falsch, nur unvollständig.

## Test-/Build-Status (eigene Verifikation)

- `dotnet build` → 0 Warnung(en), 0 Fehler, 3.4 s.
- `dotnet test` Volllauf → 1279/1279 grün, 2 m 13 s, 0 Failures, 0 Errors.
- `dotnet test --filter FullyQualifiedName~McpCallLogTests` → 14/14 grün, 197 ms.
- `dotnet test --filter FullyQualifiedName~McpServerCommandCallLogTests` → 9/9 grün, 40 ms.
- `dotnet test --filter FullyQualifiedName~CliIntegrationTests` → 29/29 grün, 57 s.

## Modell-Info

- Reviewer: kritiker (MiniMax-M3, Knowledge Cutoff 2026-01)
- Geprüfte Commits: `fc550f2` (Code+Doku, 5 files +26/-5), `e625caa` (step-result + plan-status, 2 files +97/-1)
- Geprüfte Dateien: `Docs/agent-api.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`, `src/AiNetLinter/Cli/CliOptionFactory.cs`, `tasks/.../roadmap.md`, `src/AiNetLinter/Mcp/McpCallLog.cs:99-134` (für Schema-Verifikation), `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` (für Test-Count), `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs:169`, `:361` (für `error_type`-Schema)
- Konzept-Refs: `Konzept.md:48` (Muss-Habe Doku), `:134-140` (DoD 1-7), `:62-65` (Stack-Trace-Cap, Thread-Safety)
- Rules-Refs: `AiNetLinterRichtlinien.mdc` §1 (Doku-Ordnung), §4 (Update-Pflicht), §5 (Zero-Warning, Clean-Code-Kommentar-Politik)
