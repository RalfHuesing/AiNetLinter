---
task: codegraph-mcp-finish
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-04 # step-012: TD-008 geschlossen, TD-009..TD-012 (EPIC-07) ergänzt (TD-001, TD-002 closed; TD-004/TD-011 zurueckgestellt; TD-006/TD-012 closed via DRY-Konsolidierung)
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
| TD-007 | `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs:42-46, 62-64` | niedrig | Factory- und `McpCodeGraphServerOptionsFromParameters`-XML-Doc enthalten „ehemaligen 5 Parameter"/„ehemalige 5-Parameter-Signatur" (semantisch äquivalent zu „früheren") — Refactoring-Historie im Sinne von §5 |
| TD-008 | `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:192` | niedrig | XML-Doc enthielt „die ehemalige 6-Parameter-Signatur zusammen" — gleichartige §5-Refactoring-Historie-Variante wie TD-001/TD-007, beim Sanieren in step-010 nicht mitgenommen |
| TD-009 | `src/AiNetLinter/AiNetLinter.csproj:17` (Paket-Referenz, EPIC-07) | niedrig | `Microsoft.Extensions.AI.Abstractions` wird transitiv ueber `ModelContextProtocol` 2.0.0 mitgezogen, im direkten Code ungenutzt — geschlossen in step-012 |
| TD-010 | `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (Fixture-Pool, EPIC-07) | niedrig | Subprozess-E2E-Tests starten je Testklasse einen `AiNetLinter.exe`-Prozess; `InMemoryTransport`-Eskalation ist Nice-to-Have — geschlossen in step-012 |
| TD-011 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` + 2 Geschwister (EPIC-07) | mittel | Footprint-Druck auf 3 Tool-Registrar-Sammelklassen; gemeinsame Basis-Klasse wuerde das Dispatcher-Pattern verwaessern — zurueckgestellt in step-012 |
| TD-012 | `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` + `src/AiNetLinter/Web/WebFileCatalog.cs` (DRY-Duplikation, EPIC-07) | niedrig | `SafeEnumerateFiles`/`IsGeneratedPath` 1:1 dupliziert, wurde in `FileSystemExclusionHelpers` konsolidiert — geschlossen in step-012 |

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
- **Status:** geschlossen (umgesetzt in `step-010`, Code-Commit `0458250` — Option (a)+(b): Gate 4 → 6 Slots, expliziter 60s-Timeout; im selben Volllauf reproduziert mit 1199/1199 grün in 2:34 min, kein TD-005-Flake mehr).

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

### TD-007 — Refactoring-Historie „ehemalige 5 Parameter" in zwei XML-Docs von `McpCodeGraphServerOptions` [Priorität: niedrig]

- **Gefunden in:** step-009/fix-01 (Kritiker-Review vom 2026-08-04).
- **Ort:**
  - `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs:42-46`
    (Factory-Methode `From`) — „kapselt die ehemaligen 5 Parameter in
    einem Record".
  - `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs:62-64`
    (`McpCodeGraphServerOptionsFromParameters` Record) — „Fasst die
    ehemalige 5-Parameter-Signatur zusammen".
- **Befund:** Beide XML-Doc-Kommentare enthalten das Wort
  „ehemaligen"/„ehemalige", das semantisch identisch zu dem im
  Plan explizit als §5-Verbots-Beispiel genannten „frueheren" ist
  (siehe `AiNetLinterRichtlinien.mdc` §5 Verbots-Liste: „war früher
  private"). Der Fix-Plan `step-009/fix-01/step-plan.md` Z. 154
  hatte diese Stellen fälschlich als „nicht betroffen (enthalten
  kein frueheren-Wort)" eingestuft — die Plan-Annahme war
  ungenau, weil „ehemaligen" dieselbe Refactoring-Historie
  transportiert wie „frueheren". Der Coder hat den Plan exakt
  befolgt (nur die Klassen-XML-Doc Z. 9-13 und der
  `McpCodeGraphServer.cs`-Kommentar Z. 31-34 wurden saniert),
  daher keine Coder-Schuld. Funktional folgenlos — die Aussage
  bleibt verständlich, „ehemalige" ist aber ein „war-früher-Marker"
  im Sinne der §5-Liste.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von
  step-009/fix-01 (Plan hatte diese Stellen explizit ausgenommen).
  Die Sanierung ist mechanisch (zwei Wort-Änderungen in zwei
  XML-Doc-Absätzen, „ehemaligen"/„ehemalige" durch eine
  forward-looking Formulierung ersetzen analog dem bereits
  sanierten Klassen-XML-Doc). Kein Build-, Test- oder
  Verhaltens-Impact.
- **Vorschlag:** Bei nächster inhaltlicher Berührung von
  `McpCodeGraphServerOptions.cs` (z. B. im Rahmen einer
  P0/P1-Erweiterung am Server-Options-Satz) die beiden
  XML-Docs auf forward-looking Rationale umstellen. Pattern-Vorlage:
  „kapselt 5 Konfigurations-Eingaben in einem Record, damit
  `MaxMethodParameterCount: 4` (public static, siehe
  `AiNetLinter.mdc`) eingehalten wird" statt „ehemaligen 5 Parameter".
- **Status:** geschlossen (umgesetzt in `step-010`, Code-Commit `0458250` — McpCodeGraphServerOptions.cs:42-46 + 62-64 saniert; konsistent zu Patch 3 in step-009/fix-01).

### TD-008 — Verbleibende „ehemalige 6-Parameter-Signatur"-Refactoring-Historie in `GetViolationsScanner` [Priorität: niedrig]

- **Gefunden in:** step-010 (Kritiker-Review vom 2026-08-04).
- **Ort:** `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:192` — XML-Doc
  am `Format`-Methoden-Eintrag (oder vergleichbarem Symbol; verifiziert per
  Grep) enthält das Wort „ehemalige 6-Parameter-Signatur zusammen".
- **Befund:** Semantisch identisch zu TD-001 / TD-007 — das Wort
  „ehemalige" ist ein „war-früher-Marker" im Sinne der
  `AiNetLinterRichtlinien.mdc` §5 Verbots-Liste („war früher private"). Der
  step-010-Plan hatte explizit nur `McpCodeGraphServerOptions.cs:42-46,
  62-64` als TD-007-Sanierungs-Scope benannt
  (`step-010/step-plan.md` Z. 650-654); diese dritte Stelle wurde beim
  Grep über `Mcp/` im Sanierungs-Zug übersehen, weil sie im
  Scanner-Unterordner liegt. Funktional folgenlos — XML-Doc bleibt
  verständlich, ist aber §5-Verstoß.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-010 (Plan
  hatte diese Stelle nicht benannt, der Coder hat den Plan korrekt
  befolgt). Sanierung ist mechanisch (eine Wort-Änderung analog
  TD-007-Sanierung), aber gehört in einen eigenen Mini-Cleanup-Schritt
  oder wird bei nächster inhaltlicher Berührung von
  `GetViolationsScanner.cs` mitgenommen.
- **Vorschlag:** Bei nächster Berührung von `GetViolationsScanner.cs`
  den XML-Doc auf forward-looking Rationale umstellen
  (Pattern-Vorlage: „kapselt 6 Konfigurations-Eingaenge in einem Record,
  damit `MaxMethodParameterCount: 4` eingehalten wird" statt
  „ehemalige 6-Parameter-Signatur"). Grep-Stelle bietet sich als
  Anlass fuer einen Mini-Projekt-weiten Grep ueber alle
  `Mcp/`-XML-Docs an, um weitere „ehemalige"-/„frueheren"-Vorkommen
  aufzudecken.
- **Status:** geschlossen (umgesetzt in `step-012`, Code-Commit siehe
  `tasks/codegraph-mcp-finish/step-012/step-result.md` — XML-Doc am
  `GetViolationsScannerParameters`-Record auf forward-looking Rationale
  umgestellt, Pattern-Vorlage aus TD-007-Sanierung uebernommen; konsistent
  zu Patch 3 in step-009/fix-01).

### TD-009 — `Microsoft.Extensions.AI.Abstractions` transitiv ueber `ModelContextProtocol` [Priorität: niedrig]

- **Gefunden in:** Konzept-Muss-Haben-D Z. 297-304 (EPIC-07, step-012).
- **Ort:** `src/AiNetLinter/AiNetLinter.csproj:17` referenziert
  `ModelContextProtocol` 2.0.0. Dieses Paket zieht
  `Microsoft.Extensions.AI.Abstractions` als transitive Abhaengigkeit mit.
- **Befund:** Grep-Verifikation im step-012 zeigt: keine direkten
  `Microsoft.Extensions.AI.*`-Imports im `src/AiNetLinter/`-Baum
  (verifiziert per ripgrep auf das gesamte Source-Verzeichnis — keine
  Treffer). Das Abstractions-Paket ist Teil der MCP-SDK-Vertragsflaeche
  (ModelContextProtocol 2.0.0 verweist intern darauf), kein ersetzbares
  Add-On. Kein csproj-Eingriff sinnvoll: die Konzept-Vorgabe war „bei
  Bedarf pruefen, ob eine gezieltere Paket-Referenz existiert" — Antwort
  ist nein.
- **Warum nicht sofort gefixt:** n/a — closed.
- **Vorschlag:** keine Aktion. Die transitive Abhaengigkeit ist im
  SDK-Vertrag begruendet und nicht vermeidbar. Bei einem kuenftigen
  Wechsel auf eine andere MCP-SDK-Version, die das Abstractions-Paket
  nicht mehr mitzieht, faellt das Paket automatisch weg; dann ist auch
  keine Doku-Aktion noetig.
- **Status:** geschlossen (verifiziert in `step-012` — Grep im
  `src/AiNetLinter/`-Baum ohne Treffer; csproj bleibt unveraendert;
  Entscheidung in `step-012/step-result.md` unter "Sub-Bereich 1
  (TD-001)" dokumentiert).

### TD-010 — Subprozess-E2E-Test ohne Fixture-Pool [Priorität: niedrig]

- **Gefunden in:** Konzept-Muss-Haben-D Z. 305-309 (EPIC-07, step-012).
- **Ort:** `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` ist
  der einzige Subprozess-E2E-Test-Container mit echten
  `AiNetLinter.exe`-Prozessen via `McpTestClient.ConnectAsync`
  (Retry-Loop seit `step-011/TD-019`). Fixture-Pattern: zwei
  `IClassFixture<>`-Felder (`SymbolGraphMcpFixture`,
  `BaselineMcpFixture`) — jede Fixture startet **einen**
  `AiNetLinter.exe`-Prozess pro Test-Klasse via `IAsyncLifetime`.
- **Befund:** Bestandsaufnahme im step-012 zeigt: das aktuelle
  `IClassFixture<>`-Pattern startet pro Testklasse bereits einen
  geteilten Prozess pro Workspace; `SubprocessConcurrencyGate` (6 Slots,
  60 s, aus `step-010`) kappt Spitzenlast; der
  `McpTestClient.ConnectAsync`-Retry-Loop absorbiert parallele
  Init-Flakes. Konzept-Vorgabe war „bei **weiteren** Subprozess-Tests"
  — der Ausloeser ist bei der aktuellen 1-Klassen-Container-Situation
  nicht gegeben.
- **Warum nicht sofort gefixt:** n/a — closed.
- **Vorschlag:** Bei kuenftigen Erweiterungen um mehrere neue
  Subprozess-E2E-Testklassen den `InMemoryTransport`-Pattern des
  `ModelContextProtocol`-SDK 2.0.0 als Eskalation pruefen, dann TD-010
  wieder oeffnen.
- **Status:** geschlossen (Begruendung im `step-012/step-result.md`
  unter "Sub-Bereich 4 (TD-002)" dokumentiert; keine Code-Aenderung
  am Test- oder Produktions-Code).

### TD-011 — Footprint-Druck auf 3 Tool-Registrar-Sammelklassen [Priorität: mittel]

- **Gefunden in:** Konzept-Muss-Haben-D Z. 310-315 (EPIC-07, step-012).
- **Ort:** `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (160
  Z., 4 Tools, PathOverride 2850),
  `FileStructureToolRegistrations.cs` (127 Z., 3 Tools, PathOverride
  2810), `AnalysisToolRegistrations.cs` (104 Z., 2 Tools, PathOverride
  2800).
- **Befund:** Die 3 Klassen sind kategorial verschieden — eine
  gemeinsame Basis-Klasse wuerde das etablierte Pattern (duenner
  Dispatch + Scanner/Formatter-Datei, Konzept-Muss-Haben-C) verwaessern,
  den Footprint durch virtuelle `BuildTool(...)`-Helfer **erhoehen**
  statt reduzieren und die eigenstaendige Unit-Testbarkeit jeder Klasse
  einschraenken. Der Footprint-Druck ist mit der
  `PathOverride`-Mechanik (`rules.json` → `PathOverrides`, 4 Eintraege
  aus `step-011`, 12 Eintraege aus `step-008`/`step-010`) beherrschbar.
  Die `ILinterEngineConfig`-Entlastung in `step-008` hat den
  strukturell erreichbaren Hebel bereits gehoben.
- **Warum nicht sofort gefixt:** Die `step-008/010/011`-Pfade haben
  gezeigt, dass der Footprint-Druck systematisch ueber
  `ILinterEngineConfig` (C-Block) und `PathOverride`-Mechanik
  adressierbar ist. Eine Generalisierung waere eine **Verschlechterung**
  der Architektur.
- **Vorschlag:** Falls ein kuenftiger Schritt zeigt, dass der
  Footprint-Druck durch eine kategoriespezifische Konsolidierung (z. B.
  gemeinsamer `CallLogEnabled`-Lambda-Body-Helper zwischen den
  Registrars) reduzierbar ist **ohne** das Dispatcher-Pattern zu
  verwaessern, TD-011 wieder aufnehmen. Bis dahin: bewusst zurueckgestellt.
- **Status:** zurueckgestellt (Begruendung im
  `step-012/step-result.md` unter "Sub-Bereich 5 (TD-004)"
  dokumentiert; keine Code-Aenderung am Produktions- oder Test-Code).

### TD-012 — `SafeEnumerateFiles`/`IsGeneratedPath` 1:1-Duplikation in Scanner und Web-Katalog [Priorität: niedrig]

- **Gefunden in:** Konzept-Muss-Haben-D Z. 321-327 (EPIC-07, step-012).
- **Ort:** `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:78-94` und
  `src/AiNetLinter/Web/WebFileCatalog.cs:105-113 + 149-155` (vor
  step-012). Nach step-012: gemeinsame Hilfsklasse
  `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`.
- **Befund:** Beide privaten statischen Methoden waren exakt 1:1
  dupliziert (8 Z. bzw. 7 Z., kein Verhaltens-Drift). Konsolidierung in
  `Baseline/FileSystemExclusionHelpers` macht die `Baseline/`-Namespace-
  Konvention sichtbar (Dateisystem-Kataloge) und liefert eine
  wiederverwendbare Hilfsklasse fuer kuenftige freie
  Dateisystem-Scans.
- **Warum nicht sofort gefixt:** n/a — closed.
- **Vorschlag:** keine Aktion. Kuenftige Dateisystem-Scans greifen
  ohne Duplikation auf `FileSystemExclusionHelpers.SafeEnumerateFiles`
  und `FileSystemExclusionHelpers.IsGeneratedPath` zu.
- **Status:** geschlossen (umgesetzt in `step-012` — neue Datei
  `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`, 2 Aufrufer
  umgestellt, 6 Unit-Tests in
  `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs`;
  Build + Tests gruen; Details im
  `step-012/step-result.md` unter "Sub-Bereich 2 (TD-006)").
