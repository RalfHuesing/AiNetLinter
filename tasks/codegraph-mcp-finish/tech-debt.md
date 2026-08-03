---
task: codegraph-mcp-finish
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-03 # TD-005 (SubprocessConcurrencyGate-Sättigung) + TD-006 (AiNetLinter.mdc BOM) aus step-007/fix-01-Review ergänzt
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
| TD-004 | 6 Testdateien (`Architecture/ArchitectureTests.cs`, `Core/LinterAnalyzerTests.cs`, `Core/LinterEngineCacheTests.cs`, `Core/LinterEngineTests.cs`, `Core/Checkers/MaxInheritanceDepthTests.cs`, `Core/Checkers/NamespaceDirectoryMappingTests.cs`) | niedrig | Lokale private Methode `CreateDefaultConfig()` kollidiert namentlich mit `TestHelper.CreateDefaultConfig()` |
| TD-005 | `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` (4 Slots, 30s Wait-Timeout) | mittel | Last-Flake unter Volllauf — 1-2 Failures in `McpServerCommandErrorHandlingTests`, exakt am Gate-Timeout-Stack |
| TD-006 | `.agents/rules/AiNetLinter.mdc` | niedrig | Working-Tree-vs-Index-BOM-Diskrepanz, semantisch leerer Diff, Working-Tree-Noise |

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

### TD-004 — Namenskollision `CreateDefaultConfig()` in 6 Testdateien [Priorität: niedrig]

- **Gefunden in:** step-004 (4 Dateien) + step-005 (2 weitere Dateien),
  gebündelt im Kritiker-Review von step-005 (2026-08-03) — vom Coder in
  step-005 `step-result.md` unter „Beobachtungen" vorgemerkt, Bündelung
  laut Notiz im step-005-Plan explizit dem Kritiker überlassen.
- **Ort:** `src/AiNetLinter.Tests/Architecture/ArchitectureTests.cs`,
  `src/AiNetLinter.Tests/Core/LinterAnalyzerTests.cs`,
  `src/AiNetLinter.Tests/Core/LinterEngineCacheTests.cs`,
  `src/AiNetLinter.Tests/Core/LinterEngineTests.cs`,
  `src/AiNetLinter.Tests/Core/Checkers/MaxInheritanceDepthTests.cs`,
  `src/AiNetLinter.Tests/Core/Checkers/NamespaceDirectoryMappingTests.cs`
  — jeweils eine private statische Methode `CreateDefaultConfig()`
  (verifiziert per Grep, exakt diese 6 Treffer projektweit).
- **Befund:** Der lokale Methodenname ist identisch zu
  `TestHelper.CreateDefaultConfig()`, das seit step-004/005 in denselben
  Dateien per `TestHelper.CreateDefaultConfig() with {...}` aufgerufen
  wird. Kein Compile-Konflikt (unterschiedliche Klassen/Scopes), aber für
  Leser verwirrend zu unterscheiden, welcher `CreateDefaultConfig()`-Aufruf
  gemeint ist, insbesondere da die lokale Methode selbst jetzt den
  `TestHelper`-Aufruf im Rumpf enthält.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-004/step-005
  (reine Konstruktions-Ausdruck-Konsolidierung, keine
  Methodenumbenennung/Aufrufstellen-Änderung) — in step-005 explizit als
  „bewusst nicht umbenannt" dokumentiert, Bündelungsentscheidung an den
  Kritiker delegiert.
- **Vorschlag:** Bei nächster inhaltlicher Berührung einer dieser 6
  Dateien die lokale Methode umbenennen (z. B. `LocalConfig()`/
  `BaseConfig()`), inkl. aller Aufrufstellen im jeweiligen Testkörper.
- **Status:** offen

### TD-005 — `SubprocessConcurrencyGate` regelmäßig unter Volllauf-Last gesättigt [Priorität: mittel]

- **Gefunden in:** step-007/fix-01 (Kritiker-Review vom 2026-08-03),
  Beobachtung bereits vom Coder im `step-result.md` unter „Beobachtungen"
  vorgemerkt, vom Kritiker im eigenen Volllauf reproduziert (1186
  Tests / 1184 grün / 2 fehlgeschlagen, beide in derselben Klasse mit
  Gate-Timeout-Stack).
- **Ort:** `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs:30`
  (`AcquireAsync`, `SemaphoreSlim.WaitUntilCountOrTimeoutAsync`) — Gate
  hat 4 Slots und 30s Wait-Timeout. Sichtbar betroffen ist primär
  `src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs`
  (zwei Tests dort mit reproduzierbarem 30s-Stack am Gate: einmal
  exakt 30.07s, einmal 35.23s, beide Stack-Bottom
  `SubprocessConcurrencyGate.AcquireAsync`).
- **Befund:** Unter Volllauf-Last (`dotnet test … --no-build`, ~4-6 min
  Wall-Clock) reichen 4 Gate-Slots + 30s Timeout für die parallel
  laufenden Subprozess-Tests in `McpServerCommandErrorHandlingTests` nicht
  immer aus — die Tests scheitern deterministisch am Gate-Wait-Timeout
  mit `OperationCanceledException`, *nicht* an einem echten
  Code-Defekt. Im leichteren `Category=Unit`-Slice tritt das Problem
  nicht auf, im Volllauf schon. Im step-007-Lauf (1m 41s, 0 Fehler)
  liefen dieselben Tests grün, in schwereren Läufen (4-6 min) 1-2
  Failures. Signatur typisch für Last-Sättigung, nicht für
  Regressionen.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von
  step-007/fix-01 (reiner Kommentar-Text-Fix, keine
  Test-Infrastruktur-Berührung) — und kein Blocker für die
  Schritt-Abnahme, da `step-007` (Einheit-011-Abschluss) inhaltlich
  approved ist und der Flake keine Funktionalausfall bedeutet, sondern
  nur Volllauf-Ergänzungs-Lärm.
- **Vorschlag:** Bei nächster Berührung der Test-Infrastruktur eine der
  drei Richtungen wählen (oder kombinieren): (a) Gate-Kapazität
  erhöhen (z. B. 6-8 Slots, gemessen an der parallel laufenden
  Subprozess-Spitzenlast), (b) Test-Time-Out im
  `McpServerCommandErrorHandlingTests`-Fixture anheben, (c) Retry-Logik
  analog dem bestehenden `McpTestClient.ConnectAsync`-Pattern
  einbauen, das genau diese Form von Last-Flake bereits sauber
  abfängt. Vorher: Last-Profil der parallel laufenden Subprozess-Tests
  im Volllauf messen, damit die Anpassung gezielt erfolgen kann statt
  auf Verdacht.
- **Status:** offen

### TD-006 — UTF-8-BOM-Diskrepanz auf `.agents/rules/AiNetLinter.mdc` [Priorität: niedrig]

- **Gefunden in:** step-007/fix-01 (Kritiker-Review vom 2026-08-03),
  Beobachtung bereits vom Coder im `step-result.md` unter „Beobachtungen"
  vorgemerkt, vom Kritiker verifiziert (`git status --short --
  .agents/rules/AiNetLinter.mdc` zeigt ` M`, `git diff` semantisch
  leer, Git-Warnung „LF will be replaced by CRLF the next time Git
  touches it").
- **Ort:** `.agents/rules/AiNetLinter.mdc` — Working-Tree-Variante trägt
  eine UTF-8-BOM-Sequenz, die Index-Variante (HEAD) trägt sie nicht.
- **Befund:** Reiner Working-Tree-Noise, kein Code-Schaden und keine
  Auswirkung auf Tooling (die `.mdc`-Datei wird vom Linter als
  Text-Quelle gelesen, BOM ist in diesem Kontext unkritisch). `git
  diff` ist semantisch leer — nur die BOM-Bytes bzw. CRLF/LF-Bytes
  unterscheiden sich. Stört aber die Git-Working-Tree-Sauberkeit
  (jeder `git status` zeigt die Datei als modified) und kann bei
  einem späteren Commit versehentlich echte Inhalts-Änderungen
  überlagern, falls jemand die Datei editiert ohne den BOM-Stand zu
  beachten.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von
  step-007/fix-01 (Kommentar-Text-Fix, keine Regel-Datei-Berührung).
  Reine Hygiene-Beobachtung, kein Funktional-Impact.
- **Vorschlag:** Bei nächster Berührung von `.agents/rules/AiNetLinter.mdc`
  (z. B. nach dem nächsten `dotnet run … -- --sync-agent-rules-only`)
  die BOM-Diskrepanz mit einem einmaligen `git checkout HEAD --
  .agents/rules/AiNetLinter.mdc` (oder einem expliziten
  Encoding-Fix) auflösen und mit einem Mini-Hygiene-Commit abhaken.
  Idealerweise im selben Aufwasch mit der nächsten Sync-Iteration.
- **Status:** offen
