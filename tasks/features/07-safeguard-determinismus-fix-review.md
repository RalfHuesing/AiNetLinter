---
task: safeguard-determinismus-fix
type: bugfix-review
status: resolved
created: 2026-08-06
purpose: Audit-Trail für den Safeguard-Score-Determinismus-Bug (S1.2) — Fund, Root Cause, Fix, Kritiker-Freigabe
references:
  - 05-roadmap.md
---

# Safeguard-Score-Determinismus — Fund, Root Cause, Fix

## Vorgeschichte

`safeguard` (S1.2) wurde in einem separaten, nicht orchestrierten Drift-Loop-Lauf implementiert (eigene `tasks/safeguard/`-Struktur mit EPIC-01/EPIC-02, Modell "MiniMax-M3" als Planer/Kritiker). Der letzte Schritt (Live-Repo-Integrationstest) erhielt vom eigenen Kritiker das Verdikt **`blocked`**: in 13 Reproduktionsläufen (`dotnet test --filter Category=Integration`) fiel der Safeguard-Score bei identischem Code in 38 % der Fälle von 10.0 auf 1.1486146095717884. Der Review stellte dem Nutzer explizit eine Entscheidungsfrage (3 Optionen) — die Antwort ist in der Historie nicht dokumentiert. Zwei Commits später wurde die komplette `tasks/safeguard/`-Verzeichnisstruktur mit der Nachricht „task safeguard gelöscht da erledigt" entfernt, obwohl der letzte `task-state.md`-Stand `status: executing`, step-003 `blocked` zeigte.

Beim Audit dieser Session (2026-08-06) wurde das live reproduziert: `dotnet test` schlug mit exakt derselben Fehlermeldung/demselben Score fehl.

## Root-Cause-Analyse

**Ausgangshypothese** (transiente `GetCompilationAsync`-Fehlschläge unter paralleler Last verzerren `avgCC`/`avgFootprint`): per Datei-Instrumentierung geprüft und **widerlegt** — Klassen- und Dokumentenzahl waren in fehlgeschlagenen wie erfolgreichen Läufen identisch.

**Tatsächliche Root Cause:** `McpCodeGraphServerRefresh.SweepForNewFiles` durchsucht bei jedem Staleness-Check das komplette Solution-Verzeichnis (`Directory.EnumerateFiles(solutionDir, "*.cs", AllDirectories)`) — inklusive `tests/Fixtures/**`, wo absichtlich fehlerhafte Mini-Test-Solutions liegen (z. B. `BrokenClassA.cs`, `ViolationTrigger.cs` mit bewussten Lint-Verstößen, außerhalb jedes echten Projektverzeichnisses). Fand die Zuordnungs-Heuristik `PickProjectForNewFile` keinen Verzeichnis-Präfix-Treffer, fiel sie auf `updated.ProjectIds.FirstOrDefault()` zurück und hängte die fremde Datei lautlos an das erste Projekt der echten Solution. Unter Last (häufigere Sweep-Trigger durch parallele Test-/Build-Aktivität) wurde so gelegentlich eine Fixture-Datei mit 3 absichtlichen `dynamic`-Verstößen ins `AiNetLinter`-Projekt gemischt — reproduzierbar exakt derselbe Score-Wert.

## Fix

1. **`McpCodeGraphServerRefresh.cs`** — Fallback auf „erstes Projekt" entfernt. Dateien ohne Verzeichnis-Präfix-Treffer werden übersprungen statt geraten zugeordnet. Neuer Regressionstest `GetCurrentSolution_NewFileOutsideAnyProjectDirectory_IsNotAnnexedToFirstProject` (gegen den alten Code rot, gegen den neuen grün).
2. **`SafeguardScanner.cs` / neue `SafeguardModels.cs`** (Datei-Split wegen `MaxLineCount`) — `GetCompilationAsync` zusätzlich gehärtet: Retry mit linearem Backoff (max. 3 Versuche), dauerhafter Fehlschlag eines kompilierbaren Projekts eskaliert zu `SafeguardCompilationException` → Malfunction-Meldung statt stillem Teil-Score. War nicht die Hauptursache, schließt aber eine verwandte Nicht-Determinismus-Quelle strukturell aus.
3. Zwei durch die `safeguard`-Registrierung kaputte, nicht-flaky Tests gefixt (Tool-Zahl 12→13) plus zwei weitere direkte Folgefehler behoben (`OverviewResourceRegistration.ToolSummaries` fehlte `safeguard`, `McpDocumentationSmokeTests` erwartete falsche C#-only-Tool-Zahl).
4. `Docs/agent-api.md` um `safeguard`-Eintrag ergänzt (war zuvor 0 Erwähnungen trotz Akzeptanzkriterium); README/integration.md Tool-Zahl korrigiert.
5. `tasks/features/05-roadmap.md`: S1.2 auf `[x]`, tote Referenz auf gelöschte `tasks/safeguard/konzept.md` entfernt, Akzeptanzkriterien-Checkliste durchgegangen.

## Verifikation (Coder + unabhängig Kritiker)

- Coder: 10× `dotnet test --filter Category=Integration --no-build` — `LiveDogfood_Safeguard_ReturnsResults` 0/10 Fehlschläge (vorher 5/13 ≈ 38 %).
- Kritiker (unabhängig, eigene Reproduktion): 7× denselben Filter wiederholt — 0/7 Fehlschläge für den Safeguard-Test. Zusätzlich 1× voller `dotnet test` (1325/1325 grün), `dotnet build` grün, Self-Lint `OK`.
- Ein unabhängiger, vorbestehender Flake (`McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`, anderes Subsystem — `LoadState`-Statemachine statt Datei-Sweep) trat in beiden Reproduktionsreihen gelegentlich auf, wurde korrekt als separates, nicht zu diesem Fix gehörendes Problem identifiziert und ausgelagert.
- Positivfall geprüft: bestehender Test `GetCurrentSolution_CalledAfterNewFile_TriggersSweepAgain` bestätigt, dass Dateien innerhalb eines echten Projektverzeichnisses weiterhin korrekt zugeordnet werden — der entfernte Fallback hat den gewollten Pfad nicht beschädigt.
- Retry-Logik geprüft: bounded (max. 3 Versuche), korrekte Cancellation-Propagation, kein falscher Malfunction-Trigger für legitime Nicht-C#-Projekte.
- Doku-/Roadmap-Konsistenz stichprobenartig gegen den tatsächlichen Code verifiziert (Defaults, Tool-Zahlen, Testzahlen).

## Verdikt

**FREIGEGEBEN.** Root-Cause-Analyse unabhängig verifiziert (stimmt mit realer Verzeichnisstruktur, Diff und historischer Blocked-Review-Symptomatik überein). Fix behebt die Ursache, nicht das Symptom — keine der beiden vom vorherigen Review als Anti-Pattern markierten Auswege (Scope-Cheat, Test aus Integration-Filter ziehen) wurde gewählt.

## Lehre für künftige Drift-Loop-Läufe

Ein Task darf nicht als „erledigt" geschlossen werden, während sein letzter Review-Schritt `blocked` ist und eine offene Nutzerfrage unbeantwortet im Raum steht. Wird eine Task-Struktur aufgeräumt/gelöscht, muss der Zielzustand (Fix, bewusstes Vertagen mit Tech-Debt-Eintrag, oder Revert) vorher erreicht sein — das Löschen der Nachweis-Struktur ersetzt nicht die Lösung des Problems.
