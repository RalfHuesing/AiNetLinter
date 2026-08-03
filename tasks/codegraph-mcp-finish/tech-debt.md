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
