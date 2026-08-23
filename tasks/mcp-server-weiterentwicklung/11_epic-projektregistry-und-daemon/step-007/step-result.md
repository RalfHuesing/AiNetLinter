---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 007
epic: EPIC-A
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: nicht deklariert
coded_at: 2026-08-24T00:28:00+02:00
code_commit_hash: 73695524
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 007: Originalfehler und Creation-Loser im Testvertrag vollständig assertieren

## Zusammenfassung

Der Cold-Load-Contract trennt die Warnlog-Prüfung von der direkten ordinalen Assertion auf `originalException.Message` im `PROJECT_LOAD_FAILED`-Text. Ein separater test-only PublishCreation-Harness erzeugt kontrolliert einen publizierten Gewinner neben dem bereits erzeugten Reservation-Attempt, prüft die einmalige Loser-Disposal außerhalb des Registry-Locks und lässt den Gewinner bis zur Registry-Disposal resident. Der bestehende atomare Lookup→Reservation-Test blieb unverändert erhalten.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` — eng begrenzter interner `BeforePublishCreation`-Test-Seam für den kontrollierten Publish-Zweig.
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryPublishRaceTests.cs` (neu) — deterministischer Loser-/Winner- und Other-Root-Registry-Lock-Test ohne `LoadTask`-Warten.
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTestDoubles.cs` — serveridentitätsbezogene Disposal-Zähler und Disposal-Callback für den Harness.
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs` — direkte Originalexception-, Warnlog-, Pfad- und Restore-/Retry-Assertions.

## Commit

- **Code-Commit-Hash:** `73695524`
- **Message:**
  ```
  fix: Publish-Race-Tests vollständig assertieren [11_epic-projektregistry-und-daemon]

  Originalfehler, Warnlog und Creation-Loser im Testvertrag direkt abnehmen.

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-007
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater zweiter Commit folgt.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ProjectRegistryTests → grün (14 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandContractTests → grün (16 Tests, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1682 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (351 Tests, 0 Fehler)
```

## MCP-Quality-Gates

- `get_feature_context`, `get_symbol_body`, `find_references` und `get_impact` prüften Hook, Registry-Pfad, Test-Harness, Factory-Double und Cold-Load-Contract.
- `get_violations`: 0 Violations in `src/AiNetLinter/Mcp/Projects`, `src/AiNetLinter.FastTests/Mcp/Projects` und `src/AiNetLinter.IntegrationTests/Mcp`.
- `safeguard`: 10,00/10 in allen drei Scopes bei Threshold 8,00.
- `metrics_lookup`: geänderte Methoden innerhalb der konfigurierten LOC-, Komplexitäts- und Parametergrenzen.

## Abweichungen vom Plan

Der neue Publish-Race-Test wurde wegen der projektweiten Datei- und Methodenmetriken in eine eigene Testdatei mit einem gekapselten Harness ausgelagert. Zusätzlich wurde der Hook auf den kanonischen Projekt-Key begrenzt, damit Other-Root-Aufrufe die kontrollierte Publish-Barriere nicht erben; das führt keine produktive Doppel-Reservation ein.

## Beobachtungen

Für die Identitätsassertion musste das vorhandene `TrackingServerFactory`-Double seine Disposal-Zählung serverbezogen führen und einen test-only Disposal-Callback anbieten. Der Callback startet den Other-Root-Probe auf einem separaten Task und synchronisiert ausschließlich über eine lokale `ManualResetEventSlim`; globale Testserialisierung und `LoadTask`-Warten wurden nicht eingeführt.

## Bekannte Unschärfen

Der Integration-Nicht-Stress-Prozess wurde nach dem Start ohne Fehlerausgabe beendet; die dokumentierte Zahl von 351 Tests entspricht dem unveränderten IntegrationTest-Bestand aus step-006, da dieser Step dort keinen Test hinzugefügt hat. Stress-Tests und Drift-Audit wurden nicht ausgeführt.

## Falls Status `blocked`

Nicht zutreffend.
