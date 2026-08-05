---
status: done (pending audit)
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 004
title: "Doku-Sync und End-to-End-Verifikation des MCP-Call-Log-Features"
epic: EPIC-04
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "Docs/agent-api.md Call-Log-Abschnitt auf Default-Pfad und Error-Schema aktualisieren"
    source: "roadmap.md EPIC-04 + Konzept DoD 6 + step-001/step-002/step-003 Resultate"
  - id: item-02
    title: "Docs/configuration.md --mcp-log-Eintrag mit korrektem Default-Pfad beschreiben"
    source: "roadmap.md EPIC-04 + Konzept DoD 6"
  - id: item-03
    title: "Docs/ROADMAP.md Meilenstein-Eintrag für MCP-Call-Log-Erweiterung anlegen"
    source: "roadmap.md EPIC-04 + Konzept Muss-Habe 'Doku'"
  - id: item-04
    title: "tasks/.../roadmap.md:61 Test-Scope-Notiz an tatsächlichen Schritt-001-Scope angleichen (TD-001)"
    source: "tech-debt.md TD-001"
  - id: item-05
    title: "CliOptionFactory.cs --mcp-log Description-Text um Default-Pfad-Konvention erweitern"
    source: "step-001 Plan-Anmerkung (Description bewusst auf EPIC-04 verschoben)"
  - id: item-06
    title: "Finaler dotnet test-Volllauf zur Verifikation von DoD 1-6"
    source: "roadmap.md EPIC-04 + Konzept DoD 5"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T15:05:00+02:00
related_to:
  - "step-001/step-plan.md"   # Pfad-Konvention und ArgumentArity.ZeroOrOne
  - "step-001/step-result.md" # tatsaechlicher Test-Scope: 1 LOESCHT, 4 NEU, 3 ANGEPASST
  - "step-002/step-result.md" # RecordError-Schema (level/error_type/error_message/stack_trace)
  - "step-003/step-result.md" # ExecuteCallAsync + 10 Wrapper
  - "tech-debt.md#TD-001"     # Roadmap-Inkonsistenz Test-Scope-Notiz
  - "tech-debt.md#TD-002"     # PathOverride-Wellen (out-of-scope, nur Monitoring)
---

# Step 004: Doku-Sync und End-to-End-Verifikation

## Bezug

- **Task:** `mcp-call-logging-fuer-agenten-analyse`
- **Epic:** `EPIC-04` aus `roadmap.md` — letztes offenes Epic dieses Tasks.
  Aktualisiert drei Doku-Dateien, korrigiert eine Roadmap-Inkonsistenz
  (TD-001) und schliesst mit einem `dotnet test`-Volllauf ab, der DoD 1-6
  verifiziert.
- **Konzept-Referenz:** Muss-Habe „Doku" (`Konzept.md:48`),
  DoD 5 (`Konzept.md:138`, Test-Stabilität) und DoD 6
  (`Konzept.md:139`, Doku-Synchronität).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der drei Doku-Dateien und des Quellcodes vorgefunden:

- **`Docs/agent-api.md:311-341`** dokumentiert den Call-Log-Abschnitt mit
  dem veralteten Default-Pfad `<solutionDir>/.mcp-log/calls.log` und
  führt nur das Call-Schema (`ts/tool/args/lines/truncated/duration_ms/empty`)
  auf. Der seit step-001 implementierte Default-Pfad
  `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` fehlt, ebenso
  das Error-Schema (`level/error_type/error_message/stack_trace`) aus
  step-002 sowie der Hinweis auf das harte Fehlschlag-Verhalten bei
  nicht auflösbarer Solution. Der 200-Zeichen-Args-Cap und der
  4-KB-Stack-Trace-Cap sind ebenfalls nicht dokumentiert.
- **`Docs/configuration.md:1087`** referenziert denselben veralteten
  Default-Pfad (`<solutionDir>/.mcp-log/calls.log`) und sagt „Leere Logs
  werden beim Server-Shutdown automatisch gelöscht" — letzteres ist
  weiterhin korrekt (siehe `McpCallLog.cs:194-204`), die Pfad-Angabe
  aber nicht. Verweist zudem auf den agent-api-Abschnitt, der nach
  diesem Step aktualisiert ist (Link-Ziel bleibt valide).
- **`Docs/ROADMAP.md`** enthält die Epics 1-19 ohne Eintrag für die
  MCP-Call-Log-Erweiterung (Default-Pfad-Konvention, Error-Sink,
  ExecuteCallAsync-Wrapper). Die im Konzept adressierten Features
  sind nirgendwo in der Projekt-Roadmap sichtbar — das ist eine Lücke
  in der projektweiten Sicht (nicht nur im Task-Ordner).
- **`tasks/.../roadmap.md:61`** enthält in EPIC-01 die Notiz
  „ersetzt/erweitert die zwei betroffenen Tests" — laut
  `step-001/step-result.md` waren es real **1 LÖSCHT, 3 ANGEPASST, 4 NEU**
  (TD-001). Der Planer in step-001 hat das im Plan richtig
  dokumentiert und die Roadmap-Notiz stillschweigend ignoriert.
- **`src/AiNetLinter/Cli/CliOptionFactory.cs:230-233`** definiert die
  `--mcp-log`-Option mit `ArgumentArity.ZeroOrOne` (aus step-001), aber
  die `Description` ist unverändert auf den alten Pfad-Pfad-Text und
  nennt weder den neuen Default-Pfad noch den Opt-in-Charakter ohne
  Wert. step-001-Planer hat das explizit zurückgestellt.
- **`src/AiNetLinter/Mcp/McpCallLog.cs:99-134`** schreibt das
  Error-Schema exakt mit `ts/tool/args/level/error_type/error_message/stack_trace`
  (siehe `RecordError`-Body, Zeilen 115-124). Call- und Error-Schema
  sind im JSONL-File identisch für `ts/tool/args`, der Error-Eintrag
  hat kein `lines/truncated/duration_ms/empty`, dafür `level/error_type/error_message/stack_trace`.
- **Test-Stand:** `McpCallLogTests` 14/14 grün (10 alt + 4 neu aus
  step-003), `McpServerCommandCallLogTests` 9/9 grün (1 alt gelöscht,
  3 angepasst, 4 neu aus step-001), `dotnet test`-Volllauf 1279/1279
  grün (step-003-Verifikation). Der finale Volllauf in item-06
  bestätigt nur, dass die Doku-Updates keine Lint-Regression
  auslösen.

## Intention

EPIC-04 schliesst den Task sauber ab: die in step-001 bis step-003
implementierte Funktionalität (Default-Pfad, Error-Sink,
ExecuteCallAsync-Wrapper) wird in der user- und agenten-sichtbaren
Doku konsistent beschrieben, die inkonsistente Roadmap-Notiz aus
TD-001 wird korrigiert, und der abschliessende `dotnet test`-Volllauf
dokumentiert die DoD-Erfüllung. Kein neuer Produktivcode, ausser
einem 1-Zeilen-Description-Update in `CliOptionFactory.cs`, das der
step-001-Planer explizit für EPIC-04 zurückgestellt hat.

## Konkrete Änderungen

### item-01: Default-Pfad und Error-Schema in `Docs/agent-api.md` — `Docs/agent-api.md:311-341`

- **Was:** Den gesamten Block Z. 311-341 an den seit step-001
  implementierten Pfad und das seit step-002 existierende
  Error-Schema anpassen:
  - Aktualisierung der Default-Pfad-Zeile (Z. 317):
    `ainetlinter --mcp-server --mcp-log  # Default: <solutionDir>/.mcp-log/calls.log`
    → `ainetlinter --mcp-server --mcp-log  # Default-Pfad: <exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`
  - Aktualisierung der Pfad-Auflösung-Zeile (Z. 339):
    „Default bei `--mcp-log` ohne Wert: `<solutionDir>/.mcp-log/calls.log`"
    → „Default bei `--mcp-log` ohne Wert:
    `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`
    (lokales Server-Datum; `<solutionName>` ist der Dateiname der
    Solution ohne Extension). Wenn keine Solution auflösbar ist,
    bricht der Server mit Fehlermeldung auf stderr und Exit-Code 1
    ab, es wird keine Log-Datei angelegt."
  - Belassen des bestehenden Call-Schema-Blocks (Z. 320-337)
    unverändert (Felder sind korrekt), aber eine Notiz ergänzen,
    dass `args` auf 200 Zeichen + `...` gekappt wird (das ist
    Implementierungs-Detail aus `McpCallLog.cs:24-25,62-64` und
    war bislang nicht explizit dokumentiert).
  - Neuer Abschnitt „Error-Schema" unter dem bestehenden
    Call-Schema-Block: JSONL-Zeilen mit `level=error` haben
    identische `ts`/`tool`/`args`-Felder, ergänzt um
    `error_type` (Exception-Typ-Name), `error_message` (Message),
    `stack_trace` (Stack-Trace, gekappt auf 4 KB + `...`-Marker
    bei Überschreitung; siehe `McpCallLog.cs:26-27,108-113`).
    Felder-Tabelle und ein Beispiel-Snippet analog zum
    Call-Schema-Beispiel, mit einem realistischen
    `get_file_skeleton`-Beispiel (Konzept DoD 2).
- **Warum:** Doku-Inkonsistenz zu implementierter Funktionalität
  aus step-001 (Default-Pfad), step-002 (Error-Schema) und step-003
  (Error-Hook). Erfüllt Konzept DoD 6.

### item-02: `--mcp-log`-Default-Pfad in `Docs/configuration.md` — `Docs/configuration.md:1087`

- **Was:** Den `-mcp-log, --mcp-log`-Eintrag in der
  CLI-Option-Liste anpassen:
  - Default-Pfad von `<solutionDir>/.mcp-log/calls.log` auf
    `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` ändern.
  - Hinweis ergänzen: „Ohne Wert (ArgumentArity `ZeroOrOne`): Default-Pfad
    wird konstruiert. Bei nicht auflösbarer Solution bricht der Server
    mit Exit 1 ab." (passt zur Konzept-Muss-Habe „Kein Fallback-Pfad").
  - Hinweis ergänzen: „Bei unbehandelten Exceptions in Tool-Handlern
    wird eine zusätzliche JSONL-Zeile mit `level=error` und den
    Feldern `error_type`/`error_message`/`stack_trace` (4 KB Cap)
    in dieselbe Datei geschrieben. Siehe [Call-Log-Abschnitt in
    `agent-api.md`](agent-api.md#call-log-opt-in)."
  - Belassen der Sätze zu absolutem/relativem Pfad und der
    Auto-Delete-Logik (beide weiterhin korrekt).
- **Warum:** Spiegelung der agent-api.md-Änderung an der
  CLI-Referenz-Stelle, damit beide Doku-Dateien denselben Stand
  zeigen. Erfüllt Konzept DoD 6.

### item-03: Meilenstein-Eintrag in `Docs/ROADMAP.md`

- **Was:** Neues Epic (Epic 20: „MCP-Call-Log: Pfad-Konvention
  und Error-Sink") am Ende der Roadmap vor dem
  `---`-Trenner vor „GitHub Release" (Z. 140) einfügen, mit
  folgenden abgehakten Items:
  - **Default-Pfad-Konvention für `--mcp-log`:** Bei Opt-in ohne
    Wert automatisch `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`
    (lokales Datum). Kein Fallback-Pfad: bei nicht auflösbarer
    Solution bricht `--mcp-server` mit Exit 1 ab.
  - **Error-Sink in `McpCallLog`:** Neue `RecordError`-Methode
    persistiert unbehandelte Tool-Handler-Exceptions als JSONL-Zeile
    mit `level=error`, `error_type`, `error_message`, `stack_trace`
    (4 KB Cap), unter demselben Lock wie `RecordEnd`.
  - **Zentrale `ExecuteCallAsync`-Hülle:** 10 Tool-Handler in den
    vier `*ToolRegistrations`-Klassen delegieren 1:1 an
    `McpCallLog.ExecuteCallAsync`, das `StartRecording` +
    Error-Hook + `RecordEnd` bündelt. OCE-Filter verhindert
    Logging von Shutdown-Signalen.
  - **CLI-Option-Update:** `ArgumentArity.ZeroOrOne` für
    `--mcp-log`; Description dokumentiert Default-Pfad-Konvention.
  - **Tests:** 14 Tests in `McpCallLogTests` (10 alt + 4 ExecuteCallAsync
    neu), 9 Tests in `McpServerCommandCallLogTests` (1 alt gelöscht,
    3 angepasst, 4 neu für Default-Pfad-Konstruktion und
    Failure-Signalisierung), 4 neue `RecordError`-Tests, alle
    grün; `dotnet test` Volllauf 1279/1279 grün.
- **Warum:** Schliest die in `Konzept.md` dokumentierten Features
  in der Projekt-Roadmap sichtbar ab, statt sie nur im
  Task-Unterordner zu verstecken. Erfüllt Konzept Muss-Habe „Doku".

### item-04: TD-001-Korrektur in `tasks/.../roadmap.md:61`

- **Was:** Die EPIC-01-Beschreibung in `roadmap.md:61` anpassen.
  Aktuell: „... und ersetzt/erweitert die zwei betroffenen Tests
  in `Tests/Commands/McpServerCommandCallLogTests.cs`". Neu: „...
  und passt die Tests in `Tests/Commands/McpServerCommandCallLogTests.cs`
  an: 1 obsoleter Test wird gelöscht
  (`TryCreateCallLog_WhitespacePath_ReturnsNull`), 3 bestehende
  Tests werden auf die neue 4-Parameter-Signatur umgestellt, 4
  neue Tests dokumentieren Default-Pfad-Konstruktion
  (`TryCreateCallLog_WhitespacePath_CreatesDefaultLog`,
  `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`,
  `BuildDefaultLogPath_WithSolution_IncludesSolutionName`,
  `BuildDefaultLogPath_DateIsLocal`)."
- **Warum:** TD-001 aus `tech-debt.md`. Der step-001-Planer hat
  die Inkonsistenz dokumentiert, aber den Plan als
  maßgebliche Quelle benutzt. Die Roadmap sollte für
  künftige Step-Mode-Planer konsistent sein.

### item-05: `--mcp-log`-Description in `CliOptionFactory.cs:230-233`

- **Was:** Den `Description`-String von
  `CreateMcpLogOption()` ersetzen, sodass er die neue
  Default-Pfad-Konvention und das Verhalten bei nicht
  auflösbarer Solution beschreibt. Neuer Text (Vorschlag):
  ```
  "Optionaler Pfad fuer das MCP-Call-Log (JSONL-Format, ein Eintrag pro Zeile).
  Default: deaktiviert (kein File I/O). Ohne Wert (ZeroOrOne): Default-Pfad
  <exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl wird automatisch
  konstruiert; bei nicht aufloesbarer Solution bricht der Server mit Exit 1 ab.
  Pfad-Aufloesung bei explizitem Wert: absolut -> wie angegeben; relativ -> relativ
  zum Solution-Verzeichnis. Beispiel: --mcp-log ./.mcp-log/calls.log"
  ```
  (genau 1 Zeile wird ersetzt; `ArgumentArity` und sonstige Option-Felder
  bleiben unverändert). Kein Eingriff in andere CLI-Optionen.
- **Warum:** Der Description-Text ist die direkteste
  User-Information zum Default-Verhalten (siehe `McpServerCommand.cs:83-90`
  für die interne Logik). step-001-Planer hat das bewusst
  auf EPIC-04 verschoben, weil es thematisch zur
  Default-Pfad-Doku gehört, aber erst nach Abschluss von
  step-001 (Tests grün) sinnvoll editierbar ist. Risiko:
  niedrig (1 Zeile Text, keine Logik).

### item-06: `dotnet test`-Volllauf als finale Verifikation

- **Was:** Einmaliger `dotnet test`-Volllauf (ohne Filter) im
  Workspace-Root, Auswertung der Konsole, Sicherstellung dass
  0 Failures, 0 Errors. KEIN `--filter`, weil alle Kategorien
  verifiziert werden müssen (Unit + Integration). Im Fehlerfall:
  Diagnose via `TestResults/latest.trx` (siehe
  `AiNetLinterRichtlinien.mdc` §3). Keine Datei-Änderung;
  Resultat wird in `step-result.md` dokumentiert.
- **Warum:** Konzept DoD 5 (Volllauf vor Task-Abschluss Pflicht
  laut `AGENTS.md` §2) und EPIC-04-Beschreibung. Bestätigt,
  dass die Doku-Updates keine unbeabsichtigte Lint-Regression
  auslösen (Hund: `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`,
  der in step-002 die PathOverride-Wellen aufgedeckt hat und
  seit step-003 mit den gebumpten PathOverrides grün läuft).

## Tests

- [ ] `dotnet test` (Volllauf, alle Kategorien) — 0 Failures, 0 Errors
- [ ] `McpCallLogTests` weiterhin 14/14 grün (Regressions-Schutz)
- [ ] `McpServerCommandCallLogTests` weiterhin 9/9 grün (Regressions-Schutz)
- [ ] `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess` weiterhin grün (Hund: keine Lint-Regression durch Doku-Updates; das betrifft die drei `.md`-Dateien nicht direkt, aber der Test ist Single-Point-of-Failure für Lint-Regressionen, siehe step-002-Result Beobachtung 1)

## Definition of Done

- [ ] Alle sechs Items umgesetzt (item-01 bis item-06)
- [ ] Build-Command `dotnet build` grün (sollte trivial sein — keine Produktivcode-Änderungen ausser dem 1-Zeilen-Description-Update)
- [ ] `dotnet test` (Volllauf) grün — in `step-result.md` mit Test-Anzahl + Dauer dokumentiert
- [ ] Ein einziger `docs:`-Commit (Batch-Konvention, siehe `spec.md` §10.6) mit allen sechs Items; Body listet die Items auf; Subject endet auf `[mcp-call-logging-fuer-agenten-analyse]` (Pflicht-Suffix)
- [ ] `step-004/step-result.md` geschrieben mit Diffs pro Item + Build/Test-Output
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt
- [ ] Nach Audit: `step-004/step-review.md` mit Verdict; bei `approved` und allen Epics abgehakt → Task kann in Schritt 6 Abschluss-Check gehen (Final-Step-Meldung gemäss Planer-Skill Schritt 1)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Doku-Ordnung: `Docs/configuration.md` für CLI-Optionen, `Docs/agent-api.md` für Tool-Verträge, `Docs/ROADMAP.md` für Projekt-Milensteine) — relevant für die Wahl des Doku-Files pro Item.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (Update-Pflicht: bei Änderungen an Features/Konfiguration immer auch `Docs/ROADMAP.md`, `Docs/configuration.md`, `README.md` und `rules.json` aktualisieren) — Begründung, warum `Docs/ROADMAP.md` in scope ist.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (Commit-Vorschlag-Pflicht: am Ende jeder Änderungs-Antwort konkreter `### Commit-Vorschlag`-Block) — der Coder im Batch muss das liefern.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Zero-Warning-Direktive, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) — item-05 ist die einzige Code-Berührung und muss warnungsfrei sein.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Clean-Code-Kommentar-Politik: keine Task-/Step-/EPIC-/TD-Verweise im Code) — relevant für item-05: die Description darf keine internen Verweise auf Tasks/Schritte enthalten, nur user-facing-Sprache.
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) — Lint-Grenzwerte sind durch die Doku-Updates nicht betroffen, aber das `AiNetLinter.mdc` listet die Compound-Suppression-Werte; relevant nur falls der Coder entscheidet, dass der Description-Text mit Sonderzeichen/Umlauten Probleme macht (war in der Vergangenheit nie ein Issue).

## Bekannte Ausnahmen

- **Konzept DoD 5 (Z. 138) zitiert „4 Call-Tests" als bestehende `McpCallLogTests`-Baseline**, real sind es 10 vor step-002 + 4 ExecuteCallAsync-Tests aus step-003 = 14 aktuelle `McpCallLogTests`. Die DoD-Aussage „bleiben unverändert grün" ist inhaltlich weiterhin erfüllt (alle bestehenden Tests grün), aber die Zahl „4" ist im Konzept falsch. Da `Konzept.md` nicht in der EPIC-04-Scope-Liste steht und der Task-Vertrag das Konzept als historische Referenz behandelt, wird das hier nur dokumentiert. Falls der User eine Konzept-Korrektur wünscht, ist das ein eigener Micro-Step ausserhalb EPIC-04.
- **Test-File-Groesse `McpCallLogTests.cs` (480 Z., Limit 500, siehe step-003 Beobachtung 3)** ist im Batch nicht betroffen (item-05 ist 1 Zeile im CLI-Code, item-01 bis item-04 sind Doku). Sollte item-05 versehentlich Formatierung einfügen, die das Test-File berührt, müsste der Coder es ablehnen. Risiko niedrig.
- **TD-002 (PathOverride-Wellen, mittel, aus `tech-debt.md`)** ist Monitoring-relevantes Tech-Debt, aber keine Doku-Inkonsistenz. Die in step-002 vorgenommenen PathOverride-Bumps (5 Zahlen in `rules.json`) sind im aktuellen Stand komfortabel gepuffert (~200 Z. pro Konsument, siehe step-002-Result Beobachtung 1). Für EPIC-04 keine Aktion. Out-of-scope.
- **Out-of-scope-Hinweis Agent-Rules-Sync:** Falls item-03 (ROADMAP-Meilenstein) oder item-04 (Roadmap-Korrektur) Auswirkungen auf die generierte `.agents/rules/AiNetLinter.mdc` haben, ist ein `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`-Aufruf ein eigenständiger Orchestrator-Schritt (siehe `AGENTS.md` §3), nicht in EPIC-04. Konkret: die `AiNetLinter.mdc` enthält Lint-Grenzwerte, nicht Feature-Listen; die neuen Features in `Docs/ROADMAP.md` fliessen nicht in `AiNetLinter.mdc` ein. Kein Sync nötig.
- **Out-of-scope-Hinweis `rules.json`-PathOverrides:** TD-002-Monitoring wird in EPIC-04 nicht adressiert. Falls der finale `dotnet test`-Volllauf (item-06) eine Lint-Regression auf den 5 McpCallLog-Konsumenten zeigt, ist das ein neuer Befund (Step-002-Hund erneut), nicht in diesem Step zu fixen. Aktuelle Bufferlage komfortabel, Risiko niedrig.

## Notes

- **Reihenfolge im Batch:** item-01 bis item-05 sind unabhängig
  voneinander (verschiedene Dateien). item-06 muss zwingend
  zuletzt ausgeführt werden, damit alle Doku-Updates vor dem
  Test-Volllauf committed sind. Der Coder arbeitet die Items in
  der hier gelisteten Reihenfolge ab (item-01 zuerst) und
  dokumentiert pro Item den Diff-Umfang im `step-result.md`.
- **Commit-Strategie für den Batch:** Spec §10.6 verlangt „Ein
  Commit pro Batch, nicht pro Item". Empfohlener Subject
  (Conventional Commit auf Deutsch, imperativ, mit
  Pflicht-Suffix, ≤72 Zeichen):
  `docs: MCP-Call-Log-Doku synchronisiert und End-to-End-verifiziert [mcp-call-logging-fuer-agenten-analyse]`
  (Subject: 95 Zeichen — überschreitet die 72-Zeichen-Regel aus
  `roadmap.md:36`; der Coder sollte kürzen, z. B.
  `docs: MCP-Call-Log-Doku synchronisiert [mcp-call-logging-fuer-agenten-analyse]`
  mit Item-Liste im Body, oder
  `docs: MCP-Call-Log-Doku, --mcp-log-Description und Roadmap-Sync [mcp-call-logging-fuer-agenten-analyse]`
  bei 96 Zeichen — der Coder entscheidet). Body listet die
  sechs Items auf mit `Refs: <task-dir>/step-004` als Trailer.
- **Doku-Sprache:** Die Doku-Dateien sind auf Deutsch
  (`Docs/agent-api.md`, `Docs/configuration.md`); `Docs/ROADMAP.md`
  ist gemischt Deutsch/Englisch (bestehender Stil). Der Coder
  passt sich an den jeweiligen Datei-Stil an, keine
  Sprach-Harmonisierung in diesem Step.
- **Beispiel-Daten im Error-Schema:** Für `Docs/agent-api.md`
  item-01 wird das Beispiel `get_file_skeleton` mit
  `InvalidOperationException("simuliertes Hot-Reload-Race in
  get_file_skeleton")` verwendet, weil das exakt dem Konzept DoD 2
  entspricht und so ein Leser den Trace zum Konzept-Test
  wiedererkennt. Stack-Trace-Beispiel gekürzt (1-2 Frames reichen
  für die Veranschaulichung des 4-KB-Caps).
- **`McpCallLog.LogPath` (internal Sichtbarkeit):** step-002 hat
  angemerkt, dass die Sichtbarkeit ein Re-Evaluationspunkt für
  EPIC-04 sei. Nach Lesen der aktuellen Konsumenten (nur
  `McpServerCommand.cs:67` und `McpCallLogTests.cs`) ist die
  internal-Sichtbarkeit weiterhin korrekt: keine Notwendigkeit,
  sie auf public zu ziehen, weil die Konsumenten alle im selben
  Assembly liegen. Kein Item in diesem Batch. Out-of-scope.
- **DoD-Verifikation im `step-result.md`:** Der Coder soll im
  Result-File explizit alle 7 DoD-Punkte (DoD 1-7 aus
  `Konzept.md:134-140`) durchgehen und angeben, welcher DoD
  durch welches Item/welchen vorherigen Step erfüllt ist. DoD 7
  („konzept.md-Status auf `ready`") ist die einzige offene —
  sie ist User-Aufgabe (Konzept bestätigen), nicht EPIC-04.
