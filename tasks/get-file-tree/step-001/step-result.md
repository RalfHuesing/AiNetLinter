---
status: blocked
type: step-result
task: get-file-tree
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: GPT-5 (Codex)
coded_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
coded_at: 2026-08-26T22:30:29+02:00
code_commit_hash: 2bd4cb38ca41106d77b736123adf4ffa025cc647
status_after: blocked
blocker_category: content
---

# Result Step 001: Filesystem-only Dispatch und boundary-sicherer Root-Resolver

## Zusammenfassung

`ProjectToolCall` besitzt jetzt einen intern projektgebundenen Filesystem-Dispatch, der den bestehenden Root-Guard und die Registry-/Lease-Fehlerübersetzung wiederverwendet, den Lease bis zum Callback hält und den Roslyn-Load-State nicht kurzschließt. `FileTreePathResolver` löst relative Roots lexikalisch innerhalb des absoluten Projektroots auf und liefert bei ungültigen Eingaben ein `INVALID_ARGUMENT`-Result ohne erwartbare Exceptions. Die geplanten Wiring- und Resolver-Tests sind implementiert und gezielt grün.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` — gemeinsamer Lease-Erwerb plus `ExecuteFilesystemAsync` ohne Loading-/LoadFailed-Abkürzung.
- `src/AiNetLinter/Mcp/Tools/FileStructure/FileTreePathResolver.cs` (neu) — unveränderliches Result-Modell und boundary-sichere relative Root-Auflösung.
- `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs` — Guard-, Load-State- und Lease-Lifetime-Verträge für den Filesystem-Dispatch.
- `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/FileTreePathResolverTests.cs` (neu) — Default-, verschachtelte, absolute, ausbrechende, sibling- und ungültige Pfade.

## Commit

- **Code-Commit-Hash:** `2bd4cb38ca41106d77b736123adf4ffa025cc647`
- **Message:**
  ```
  feat(mcp): Ergänze Filesystem-Dispatch und Root-Resolver [get-file-tree]

  Binde physische Callbacks an die registrierte Projekt-Lease und sichere relative Roots.
  Refs: tasks/get-file-tree/step-001
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~WiringContractTests|FullyQualifiedName~FileTreePathResolverTests"` → grün (27 Tests, 0 Fehler).
- `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` → grün (1.295 Tests, 0 Fehler).
- `dotnet build` → grün (0 Warnungen, 0 Fehler).
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → rot (1.804 grün, 2 Fehler): bestehende `GetHotspotsToolTests.ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles` und `ExecuteAsync_ScopeFilterWithForwardSlashPath_MatchesFiles` erwarten 5, die vorhandene `SymbolGraphMiniSolutionSpec` liefert 6 Dokumente.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → rot (352 grün, 1 Fehler, 4 übersprungen); der Lauf meldete zunächst das durch die Testergänzung überschrittene 500-Zeilen-Limit von `WiringContractTests.cs`, das danach innerhalb des Step-Scopes behoben wurde.
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests"` → grün (3 Tests, 0 Fehler) nach der Zeilenlimit-Korrektur.

## Abweichungen vom Plan

Der Resolver liefert zusätzlich `ErrorCode` und `ErrorMessage` im unveränderlichen Result-Modell, damit erwartbare Eingabefehler direkt als `INVALID_ARGUMENT` validierbar sind; die im Plan geforderte nicht-werfende Fehlersemantik bleibt erhalten. Für die Lease-Lifetime-Tests wurde der bereits vorhandene `FakeClock` aus dem Projekt-Testbereich wiederverwendet; redundanter lokaler Testcode und Leerzeilen in der berührten Wiring-Datei wurden entfernt, damit das bestehende 500-Zeilen-Limit eingehalten wird. Keine Walk-, Glob-, Scan- oder Registrierungslogik wurde geändert.

## Beobachtungen

Die vollständige Fast-Suite enthält einen vorhandenen Fixture-/Assertion-Mismatch: `SymbolGraphMiniSolutionSpec.Documents` umfasst sechs C#-Dokumente, die beiden unveränderten Hotspots-Tests prüfen weiterhin die Zahl 5. Der vollständige Integration-Lauf wurde dadurch nicht an diesen Tests, sondern nachfolgend an der durch die neue Testabdeckung ausgelösten Zeilengrenze sichtbar; der betroffene `CliRepositoryDogfoodTests`-Slice ist nach der in-scope Korrektur grün. Es wurde kein Tech-Debt-Eintrag angelegt.

## Bekannte Unschärfen

Der vollständige Gate-Lauf wurde gemäß Auftrag genau einmal ausgeführt und war wegen des bestehenden Hotspots-Mismatch nicht grün; er wurde nach der rein mechanischen Wiring-Zeilenkorrektur nicht wiederholt. Der Kritiker sollte deshalb die zwei bestehenden Hotspots-Erwartungen separat gegen den Fixture-Stand einordnen und den Dispatch-/Resolver-Diff unabhängig vom Gate-Ergebnis prüfen.

## Falls Status `blocked`

**Blocker-Art:** `content`

**Blockiert weil:** Zwei unveränderte Fast-Tests erwarten fünf Fixture-Dateien, obwohl die vorhandene Mini-Solution sechs Dokumente enthält; dadurch ist der vorgeschriebene vollständige Gate-Lauf nicht grün.

**Brauche von Nutzer:** Keine Entscheidung für den Step-Umfang; der bestehende Fixture-/Assertion-Mismatch muss außerhalb dieses Steps bewertet werden.

**Aktueller Stand:** Code und geplante Tests sind committed und gezielt grün; der Filesystem-Dispatch, Resolver und die Step-Abdeckung sind vollständig umgesetzt.
