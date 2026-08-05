---
task: verbesserungen-mcp
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-05T09:45:00Z
---

# Tech-Debt-Log: verbesserungen-mcp

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
| TD-001 | `src/AiNetLinter.Tests/Mcp/Tools/*ToolTests.cs` (Aggregat-Warnung-Regex) | mittel | Regex `Dateien?` in mehreren bestehenden Aggregat-Warnung-Tests matcht Plural, nicht Singular „1 Datei" — aktuell durch Mehrfach-Datei-Fixtures maskiert. |
| TD-002 | `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:87-92` (`FormatBreakdown`) | niedrig | Produktionscode hartkodiert „Dateien" (Plural) für alle sechs Datei-Typ-Zeilen, unabhängig vom tatsächlichen Count — „1 Dateien" statt „1 Datei" bei genau einer Datei. |
| TD-003 | `src/AiNetLinter.Tests` (Volllauf, `dotnet test AiNetLinter.slnx`) | mittel | Voller Testlauf stürzt in dieser Sandbox-Umgebung intermittierend mit „Testhostprozess ist abgestürzt" ab (kein einzelner Testfehler) — reproduziert sowohl vor als auch nach dem step-002-Paket-Bump, also unabhängig von diesem Step. |
| TD-004 | `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:27-35` (`ExecuteAsync` XML-Doc) | niedrig | Vorbestehender, grammatikalisch zerrissener XML-Doc-Kommentar an `ExecuteAsync` (abgebrochener Satz „…einen Dateien hat…") — unabhängig von step-003, zufällig beim Lesen der Datei aufgefallen. |

## Einträge

### TD-001 — Fehlerhafte `Dateien?`-Regex in Aggregat-Warnung-Tests maskiert Singular-Fall [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-05)
- **Ort:** `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:107` und
  `GetHotspotsToolTests.cs:109` (verifiziert: exakt derselbe
  `Assert.Matches(@"\b\d+\s+Dateien?\s+haben\s+Compile-Fehler", text)`).
  Dieselbe Testmethode `ExecuteAsync_CompileErrorFixture_OutputStartsWithAggregateWarning`
  existiert zusätzlich (mit vermutlich analoger oder einfacherer
  `Assert.Contains`-Prüfung, nicht im Detail geprüft) in
  `FindReferencesToolTests.cs`, `FindSymbolToolTests.cs`,
  `GetImpactToolTests.cs`, `GetTypeHierarchyToolTests.cs`,
  `SearchPatternToolTests.cs` — alle nutzen dieselbe
  `CompileErrorMiniFixtureWorkspace` (3 kaputte Dateien, Plural-Fall).
- **Befund:** Die Regex `\d+\s+Dateien?\s+haben` matcht nur den Plural
  „N Dateien" (das `?` bezieht sich grammatikalisch nur auf das letzte
  „n" von „Dateien", nicht auf das ganze Wort „Datei"). Die Produktions-
  logik selbst ist korrekt: `McpCompileDiagnostics.FormatAggregateWarning`
  (`src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs:103-109`)
  unterscheidet bereits sauber zwischen „Datei" (1) und „Dateien" (>1).
  Der Bug steckt ausschließlich in der Test-Assertion. Da
  `CompileErrorMiniFixtureWorkspace` immer 3 Dateien bricht, bleibt das
  Problem in allen aktuell existierenden Nutzungen unsichtbar (False
  Negative nur im Singular-Fall). Neu entdeckt beim Anlegen der
  `BlazorPartialMini`-Fixture in diesem Step, die genau einen
  Compile-Fehler-File-Fall erzeugt und beim ersten Testlauf exakt daran
  scheiterte (Coder-Hinweis in `step-001/step-result.md`, „Beobachtungen").
- **Warum nicht sofort gefixt:** Betrifft ausschließlich bestehende
  Testklassen aus früheren, nicht in diesem Step behandelten
  Arbeitsschritten — außerhalb des Scopes von step-001 (der nur die neue
  `BlazorPartialMini`-Fixture/Testklasse mit bereits korrigierter Regex
  `Datei(en)?` einführt).
- **Vorschlag:** In einem künftigen kleinen Schritt die Regex in allen
  betroffenen `*ToolTests.cs`-Dateien einheitlich auf
  `\d+\s+Datei(en)?\s+haben` (oder gleichwertig) korrigieren — ggf. auch
  auf eine gemeinsame Test-Hilfsmethode/Konstante auslagern, um die
  Duplikation über sieben Testklassen zu vermeiden.
- **Status:** offen

### TD-002 — `GetIndexScopeScanner.FormatBreakdown` pluralisiert „Datei" nie [Priorität: niedrig]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-05), beim
  Verifizieren der neuen Assertion
  `GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint` (prüft
  `Assert.Contains(".cs: 1 Dateien (voll vom Symbolgraph abgedeckt)", ...)`).
- **Ort:** `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:85-93`
  (`FormatBreakdown`) — alle sechs Zeilen (`.cs`, `.css`, `.html`, `.js`,
  `.razor`, `.xaml`) verwenden fest das Wort „Dateien", z. B.
  `$".cs: {csCount} Dateien (voll vom Symbolgraph abgedeckt)"`, unabhängig
  vom Wert von `csCount`/`cssCount`/etc.
- **Befund:** Bei genau einer Datei eines Typs zeigt `get_index_scope`
  grammatikalisch falsch „1 Dateien" statt „1 Datei" an. Diese Datei war
  nicht Teil des step-002-Diffs (nur `AiNetLinter.csproj` und
  `SourceFileCatalogBlazorPartialTests.cs` wurden geändert) — reiner
  Zufallsfund beim Nachvollziehen der neuen `BlazorPartialMini`-Fixture-
  Assertion (die genau 1 `.cs`-Datei hat und damit den Singular-Fall
  auslöst). Verwandt mit TD-001 (gleiche Pluralisierungs-Kategorie), hier
  aber im tatsächlichen Produktionscode statt nur in einer Test-Regex —
  reine Kosmetik, keine funktionale Auswirkung.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-002 (Datei
  nicht Teil der geplanten Änderungen; ein Fix hier hätte die neue
  Assertion selbst ändern müssen, was der Plan nicht vorsah).
- **Vorschlag:** `FormatBreakdown` um einfache Singular/Plural-Unterscheidung
  ergänzen (analog zu `McpCompileDiagnostics.FormatAggregateWarning`, die
  das laut TD-001 bereits korrekt macht).
- **Status:** offen

### TD-003 — Voller `dotnet test`-Lauf stürzt in dieser Sandbox intermittierend ab [Priorität: mittel]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-05), beim
  Nachvollziehen des Coder-Ergebnisses „`dotnet test` → grün (1257 Tests,
  0 Fehler)".
- **Ort:** Kein einzelner Testfall — `dotnet test AiNetLinter.slnx` als
  Ganzes. Betroffen wirken v. a. stark parallele MSBuildWorkspace-/
  Subprozess-Tests (`AiNetLinter.Tests.Mcp.McpTestClientParallelTests.
  ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`,
  `AiNetLinter.Tests.Baseline.SourceFileCatalogRegisterMSBuildTests.
  LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed`) liefen kurz vor
  den beiden beobachteten Abstürzen.
- **Befund:** Drei Volllauf-Versuche auf dem aktuellen Stand (Commit
  `a14b3cd`) ergaben: Absturz bei 663/1257, Absturz bei 1205/1257, dann
  grün bei 1257/1257 — jeweils „Der aktive Testlauf wurde abgebrochen.
  Grund: Der Testhostprozess ist abgestürzt.", ohne dass ein einzelner
  Test als fehlgeschlagen gemeldet wurde. Zur Abgrenzung vom step-002-
  Paket-Bump wurde per temporärem Git-Worktree derselbe Volllauf gegen
  Commit `25b9f7a` (Stand unmittelbar **vor** dem Bump) wiederholt: dort
  einmal grün (1257/1257), einmal ebenfalls Absturz (bei 1217/1257). Der
  Absturz ist damit **nicht** auf den Paket-Bump dieses Steps
  zurückzuführen (reproduziert identisch auf dem Vorgänger-Commit) —
  echtes, umgebungsabhängiges Infrastruktur-/Nebenläufigkeits-Problem
  dieser Sandbox, kein Code-Defekt aus step-002. Genug Arbeitsspeicher
  war vorhanden (~95 GB frei von ~126 GB), 32 logische Kerne.
- **Warum nicht sofort gefixt:** Nicht reproduzierbar auf einen
  einzelnen, deterministischen Testfall eingrenzbar innerhalb des
  Prüfaufwands dieses Reviews; betrifft die gesamte Testsuite
  projektübergreifend, nicht step-002 spezifisch.
- **Vorschlag:** Bei Gelegenheit gezielt nachstellen (z. B. wiederholte
  Einzelläufe der stark parallelen MSBuildWorkspace-/Subprozess-Tests),
  ob ein bestimmter Test unter Last einen nativen Crash auslöst (Kandidat:
  `McpTestClientParallelTests`/`SourceFileCatalogRegisterMSBuildTests`);
  ggf. `AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität bewahren"
  gezielt anwenden (Semaphore/Retry statt Collection-Serialisierung).
- **Status:** offen

### TD-004 — Zerrissener XML-Doc-Kommentar an `FindReferencesTool.ExecuteAsync` [Priorität: niedrig]

- **Gefunden in:** step-003 (Kritiker-Review vom 2026-08-05), beim Lesen
  der vollständigen Datei zur Verifikation des Stable-ID-Zweigs.
- **Ort:** `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:27-35`
  (`ExecuteAsync`-Summary). Wortlaut aktuell: „…liefert dessen
  Aufrufstellen als Text. Stellt dem Aufrufstellen-Output einen\nDateien
  hat (Roslyn toleriert sie, aber der Agent weiss sonst nicht, dass die
  Antwort unvollstaendig sein kann). Defensiver try/catch-Wrapper…" — der
  Satz bricht mitten im Gedanken ab und ein Fragment („Dateien hat…")
  hängt ohne erkennbaren Bezugspunkt in der Luft.
- **Befund:** Verifiziert per `git show 48d596c~1:...FindReferencesTool.cs`
  — der Kommentar war bereits vor step-003 in genau diesem zerrissenen
  Zustand, dieser Step hat ihn nicht angefasst (nur Klassen- und
  `ResolveSymbolAsync`-Doc wurden in diesem Step geändert). Vermutlich ein
  Editier-Unfall aus einem früheren Schritt (Passage zum globalen
  Compile-Error-Hinweis wurde offenbar mittendrin gekürzt).
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-003 (Plan
  sah für diese Datei nur den Klassen- und `ResolveSymbolAsync`-Kommentar
  vor, nicht `ExecuteAsync`s Doc).
- **Vorschlag:** Bei nächster Berührung von `FindReferencesTool.cs` den
  `ExecuteAsync`-Kommentar zu einem vollständigen, kohärenten Satz
  reparieren (vermutlich sollte er den globalen Compile-Error-Rausch-
  Hinweis erklären, analog zum Warnungs-Aufbau in anderen Tools).
- **Status:** offen
