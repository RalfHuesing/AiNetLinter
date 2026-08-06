---
task: safeguard
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-06T14:09:00+02:00
---

# Tech-Debt-Log: safeguard

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
| TD-001 | `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsScannerTests.cs` (fehlt) | mittel | Dedizierte Scanner-Tests für `GetViolationsScanner` existieren nicht — Logik nur indirekt über `GetViolationsToolTests` getestet. |

## Einträge

### TD-001 — Fehlende dedizierte Tests für `GetViolationsScanner` [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-06)
- **Ort:** `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsScannerTests.cs` (Datei existiert nicht)
- **Befund:** `GetViolationsScanner.BuildViolationsTextAsync` und `FormatReport` werden nur indirekt über `GetViolationsToolTests.ExecuteAsync_*` getestet. Es gibt keine dedizierte Scanner-Test-Datei, die die Format-Logik isoliert prüft (Scope-Filter, Severity-Bucket-Trennung in `AppendSection`, „Keine Dateien im Scope"-Sonderfall, Default-Config-Marker). Der Coder von step-001 hat das im Result dokumentiert und das neue `SafeguardScannerTests.cs` bewusst als Pattern-Vorbild etabliert — die fehlende Scanner-Test-Datei für `GetViolationsScanner` ist damit konsistent beobachtbar, aber out of scope für EPIC-01.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von EPIC-01 (SafeguardScanner). Eine rückwirkende Scanner-Test-Datei für `GetViolationsScanner` ist ein eigenständiger Test-Refactor, der die bestehenden Tool-Tests duplizieren oder restrukturieren würde. Der Planer hat in `step-plan.md` §"Bekannte Ausnahmen" explizit auf diese Beobachtung als Tech-Debt-Kandidat hingewiesen, ohne sie als Step-Auftrag zu definieren.
- **Vorschlag:** Eigenes kleines Epic „Scanner-Tests für Bestandsscanner" in `roadmap.md` ergänzen, das `GetViolationsScannerTests.cs` nach dem `SafeguardScannerTests`-Pattern (AdhocWorkspace-Helper + direkter `FormatReport`-Zugriff via `InternalsVisibleTo`) aufbaut. Falls weitere Bestandsscanner (`FindSymbolTool`/`FindReferencesTool`/etc.) ebenfalls keine dedizierten Tests haben: in derselben Welle mit-aufnehmen.
- **Status:** offen  # offen | erledigt | verworfen — Änderung ist manuell (Nutzer), kein Subagent aktualisiert dieses Feld selbst
