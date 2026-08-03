---
task: codegraph-mcp-finish
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-03
---

# Tech-Debt-Log: codegraph-mcp-finish

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem Fix-Step oder einem neuen Epic. Das entscheidet ausschließlich der
Nutzer (manuell, z. B. durch Ergänzen eines Epics in `roadmap.md` mit
Verweis auf die Tech-Debt-ID).

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs`, `McpServerOptionsFactoryTests.cs`, `McpTestClientRetryTests.cs` | niedrig | Vorbestehende XML-Doc-Kommentare brechen mitten im Satz ab |
| TD-002 | `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs:92` | niedrig | Tote, vorbestehende Variable `baselineAfter` (deklariert, nie assertet) |
| TD-003 | `src/AiNetLinter/Cli/LinterArgs.cs:223-224` | niedrig | `--sync-agent-rules-only` fehlt in `HasStandaloneCommand()`, verlangt unnötig `--path`/`--config` |

## Einträge

### TD-001 — Abgerissene XML-Doc-Kommentare in drei Mcp-Testklassen [Priorität: niedrig]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-03)
- **Ort:**
  - `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs:9-12`
    — „Eingefuehrt mit `MaxConstructorDependencies: 5`-Limit lag." (Satz
    bricht ab, fehlender Sinnzusammenhang zum vorangehenden Satzteil)
  - `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs:11-13`
    — „...siehe Plan-Abweichung im `result.md` von." (Satz endet mitten
    im Wort/Verweis)
  - `src/AiNetLinter.Tests/Mcp/McpTestClientRetryTests.cs:12-14`
    — „...der Retry-Loop wird sichtbar (A3 fuer." (Satz und Klammer
    unvollständig)
- **Befund:** Die XML-Doc-Kommentare an den Klassen wurden offenbar bei
  einer früheren Bearbeitung abgeschnitten (vermutlich
  Editier-/Merge-Artefakt aus einer vorherigen Session) und ergeben
  keinen vollständigen Sinn mehr. Funktional folgenlos (nur
  Dokumentation), aber irreführend für jeden, der die Klasse liest.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 — der
  Step sollte an diesen Dateien ausschließlich das
  `[Collection("ConsoleTestCollection")]`-Attribut entfernen, nicht die
  bestehende Doku umschreiben. Die Lücken existierten bereits vor diesem
  Step (verifiziert: der Commit-Diff `e466020` ändert an diesen drei
  Dateien nur die Collection-Zeile, nicht den Doc-Text).
- **Vorschlag:** Bei nächster inhaltlicher Berührung dieser drei Klassen
  (z. B. im Rahmen eines künftigen Steps zu `Mcp/`) die abgerissenen
  Sätze vervollständigen oder kürzen, statt sie weiter mitzuschleppen.
- **Status:** offen

### TD-002 — Tote Variable `baselineAfter` in `WebBaselineTests` [Priorität: niedrig]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-03), vom Coder
  bereits im `step-result.md` unter „Beobachtungen" vorgemerkt.
- **Ort:** `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs:92` (Methode
  `AuditWithBaseline_ChangedWebFile_ReportsViolationsAndUpdatesBaseline`)
  — `var baselineAfter = BaselineReader.Read(baselinePath);` wird
  deklariert, aber nie in einem Assert verwendet.
- **Befund:** War bereits vor step-002 unbenutzt (verifiziert: der Diff in
  `a566ea4` ändert an dieser Zeile nur `void` → `async Task`-Umbau des
  umschließenden Test-Signatur-Kontexts, nicht die Zeile selbst). Kein
  Compiler-Warncode für unbenutzte lokale Variablen mit direkter
  Zuweisung durch eine Methode mit Seiteneffekt (kein CS0219 hier, da die
  Methode `BaselineReader.Read` aufgerufen und ihr Rückgabewert nur nicht
  genutzt wird) — daher bleibt sie unbemerkt.
- **Warum nicht sofort gefixt:** Außerhalb des reinen
  Boilerplate-/Aufruf-Mechanik-Scopes von step-002 (Non-Goal „Keine
  Änderung an Testinhalten/Assertions" aus `Konzept.md`) — ob die Zeile
  entfernt oder (wahrscheinlicher beabsichtigt) um einen fehlenden Assert
  auf den aktualisierten Baseline-Checksum ergänzt werden soll, ist eine
  inhaltliche Testentscheidung, keine mechanische.
- **Vorschlag:** Bei nächster inhaltlicher Berührung dieses Tests klären,
  ob ein Assert auf `baselineAfter` fehlt (wahrscheinlicher, da die
  Methode explizit „UpdatesBaseline" im Namen trägt) oder die Variable
  ersatzlos entfernt werden kann.
- **Status:** offen

### TD-003 — `--sync-agent-rules-only` verlangt unnötig `--path`/`--config` [Priorität: niedrig]

- **Gefunden in:** step-003 (Kritiker-Review vom 2026-08-03), vom Coder
  bereits im `step-result.md` unter „Beobachtungen" vorgemerkt und vom
  Kritiker verifiziert (`dotnet run --project src/AiNetLinter --
  --sync-agent-rules-only` → `[ERROR]: --path ist erforderlich (außer
  bei --docs, --list-rules, --describe-rule, --search-rules, --map,
  --eval, --list-evals)`).
- **Ort:** `src/AiNetLinter/Cli/LinterArgs.cs:223-224`,
  `HasStandaloneCommand()` — listet `Docs`, `ListRules`, `DescribeRule`,
  `SearchRules`, `MapType`, `EvalType`, `ListEvals`, `McpServer` als
  eigenständig lauffähige Kommandos, aber nicht `SyncAgentRulesOnly`.
- **Befund:** `--sync-agent-rules-only` ist konzeptionell ein
  Fast-Path-Kommando ohne Audit (siehe XML-Doc an der Property, Zeile
  70: „Fast-Path ohne Audit"), verhält sich CLI-seitig aber nicht wie
  die anderen Standalone-Kommandos — es benötigt zusätzlich `--path .`
  und `--config rules.json`, obwohl es inhaltlich nur `rules.json`
  liest und `.agents/rules/*.mdc` neu schreibt, keinen Solution-Scan
  braucht.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-003 (F.3
  ist reines Testordner-/Grenzwert-Refactoring, kein CLI-Argument-Fix).
  Der Workaround (`--path . --config rules.json` mitgeben) ist bekannt
  und funktioniert.
- **Vorschlag:** Bei nächster inhaltlicher Berührung von `LinterArgs.cs`
  `SyncAgentRulesOnly` in `HasStandaloneCommand()` aufnehmen, damit der
  in mehreren Step-Plänen dieses Tasks referenzierte Kurzbefehl
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` ohne
  Zusatzargumente funktioniert.
- **Status:** offen
